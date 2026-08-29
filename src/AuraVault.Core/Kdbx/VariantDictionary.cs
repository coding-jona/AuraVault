using System.Buffers.Binary;
using System.Text;

namespace AuraVault.Core.Kdbx;

/// <summary>
/// KDBX 4 "VariantDictionary" — a typed, ordered string-keyed map used for KDF parameters and
/// public custom data. Wire format: <c>uint16 version</c> then repeated
/// <c>(byte type, int32 keyLen, key, int32 valueLen, value)</c> terminated by a single <c>0x00</c> type.
/// </summary>
public sealed class VariantDictionary
{
    private const ushort MaxSupportedVersion = 0x0100;

    private readonly Dictionary<string, (byte Type, byte[] Raw)> _items = new(StringComparer.Ordinal);
    private readonly List<string> _order = [];

    private enum EntryType : byte
    {
        End = 0x00,
        UInt32 = 0x04,
        UInt64 = 0x05,
        Bool = 0x08,
        Int32 = 0x0C,
        Int64 = 0x0D,
        String = 0x18,
        ByteArray = 0x42,
    }

    public IReadOnlyList<string> Keys => _order;

    public bool ContainsKey(string key) => _items.ContainsKey(key);

    public uint GetUInt32(string key) => BinaryPrimitives.ReadUInt32LittleEndian(Raw(key, EntryType.UInt32));

    public ulong GetUInt64(string key) => BinaryPrimitives.ReadUInt64LittleEndian(Raw(key, EntryType.UInt64));

    public bool GetBool(string key) => Raw(key, EntryType.Bool)[0] != 0;

    public int GetInt32(string key) => BinaryPrimitives.ReadInt32LittleEndian(Raw(key, EntryType.Int32));

    public long GetInt64(string key) => BinaryPrimitives.ReadInt64LittleEndian(Raw(key, EntryType.Int64));

    public string GetString(string key) => Encoding.UTF8.GetString(Raw(key, EntryType.String));

    public byte[] GetByteArray(string key) => (byte[])Raw(key, EntryType.ByteArray).Clone();

    public byte[]? TryGetByteArray(string key) =>
        _items.TryGetValue(key, out var v) && v.Type == (byte)EntryType.ByteArray ? (byte[])v.Raw.Clone() : null;

    public ulong? TryGetUInt64(string key) =>
        _items.TryGetValue(key, out var v) && v.Type == (byte)EntryType.UInt64
            ? BinaryPrimitives.ReadUInt64LittleEndian(v.Raw)
            : null;

    public uint? TryGetUInt32(string key) =>
        _items.TryGetValue(key, out var v) && v.Type == (byte)EntryType.UInt32
            ? BinaryPrimitives.ReadUInt32LittleEndian(v.Raw)
            : null;

    public void SetUInt32(string key, uint value)
    {
        var raw = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(raw, value);
        Set(key, EntryType.UInt32, raw);
    }

    public void SetUInt64(string key, ulong value)
    {
        var raw = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(raw, value);
        Set(key, EntryType.UInt64, raw);
    }

    public void SetBool(string key, bool value) => Set(key, EntryType.Bool, [value ? (byte)1 : (byte)0]);

    public void SetInt32(string key, int value)
    {
        var raw = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(raw, value);
        Set(key, EntryType.Int32, raw);
    }

    public void SetInt64(string key, long value)
    {
        var raw = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(raw, value);
        Set(key, EntryType.Int64, raw);
    }

    public void SetString(string key, string value) => Set(key, EntryType.String, Encoding.UTF8.GetBytes(value));

    public void SetByteArray(string key, ReadOnlySpan<byte> value) => Set(key, EntryType.ByteArray, value.ToArray());

    public static VariantDictionary Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2)
        {
            throw new KdbxFormatException("VariantDictionary too short.");
        }

        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(data);
        if ((version & 0xFF00) > (MaxSupportedVersion & 0xFF00))
        {
            throw new KdbxFormatException($"Unsupported VariantDictionary version 0x{version:X4}.");
        }

        var dict = new VariantDictionary();
        int pos = 2;
        while (true)
        {
            if (pos >= data.Length)
            {
                throw new KdbxFormatException("VariantDictionary is not terminated.");
            }

            byte type = data[pos++];
            if (type == (byte)EntryType.End)
            {
                break;
            }

            int keyLen = ReadInt32(data, ref pos);
            string key = Encoding.UTF8.GetString(Slice(data, ref pos, keyLen));
            int valLen = ReadInt32(data, ref pos);
            byte[] raw = Slice(data, ref pos, valLen).ToArray();
            dict.Set(key, (EntryType)type, raw);
        }

        return dict;
    }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        Span<byte> u16 = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(u16, MaxSupportedVersion);
        ms.Write(u16);

        Span<byte> u32 = stackalloc byte[4];
        foreach (string key in _order)
        {
            var (type, raw) = _items[key];
            ms.WriteByte(type);

            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            BinaryPrimitives.WriteInt32LittleEndian(u32, keyBytes.Length);
            ms.Write(u32);
            ms.Write(keyBytes);

            BinaryPrimitives.WriteInt32LittleEndian(u32, raw.Length);
            ms.Write(u32);
            ms.Write(raw);
        }

        ms.WriteByte((byte)EntryType.End);
        return ms.ToArray();
    }

    private void Set(string key, EntryType type, byte[] raw)
    {
        if (!_items.ContainsKey(key))
        {
            _order.Add(key);
        }

        _items[key] = ((byte)type, raw);
    }

    private byte[] Raw(string key, EntryType expected)
    {
        if (!_items.TryGetValue(key, out var v))
        {
            throw new KdbxFormatException($"VariantDictionary is missing required key '{key}'.");
        }

        if (v.Type != (byte)expected)
        {
            throw new KdbxFormatException($"VariantDictionary key '{key}' has type 0x{v.Type:X2}, expected 0x{(byte)expected:X2}.");
        }

        return v.Raw;
    }

    private static int ReadInt32(ReadOnlySpan<byte> data, ref int pos)
    {
        var value = BinaryPrimitives.ReadInt32LittleEndian(Slice(data, ref pos, 4));
        return value;
    }

    private static ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> data, ref int pos, int length)
    {
        if (length < 0 || pos + length > data.Length)
        {
            throw new KdbxFormatException("VariantDictionary field runs past end of buffer.");
        }

        var slice = data.Slice(pos, length);
        pos += length;
        return slice;
    }
}
