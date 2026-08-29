using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using AuraVault.Core.Cryptography;
using AuraVault.Core.Model;

namespace AuraVault.Core.Kdbx;

/// <summary>
/// Clean-room KDBX 4.x reader/writer. Validated against KeePassXC / KeePass 2.x fixtures.
/// KDBX 3.1 files are detected and rejected with a clear message (read support arrives separately).
/// </summary>
public sealed class Kdbx4Codec : IKdbxCodec
{
    private static readonly byte[] EndOfHeaderMarker = [0x0D, 0x0A, 0x0D, 0x0A];

    public KdbxDatabase Read(Stream input, CompositeKey key)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(key);

        using var reader = new BinaryReader(input, System.Text.Encoding.UTF8, leaveOpen: true);

        uint sig1 = reader.ReadUInt32();
        uint sig2 = reader.ReadUInt32();
        if (sig1 != KdbxFormat.Signature1 || sig2 != KdbxFormat.Signature2)
        {
            throw new KdbxFormatException("Not a KDBX file (bad magic).");
        }

        uint version = reader.ReadUInt32();
        uint major = version & KdbxFormat.FileVersionCriticalMask;
        if (major == (KdbxFormat.FileVersion31 & KdbxFormat.FileVersionCriticalMask))
        {
            throw new NotSupportedException("This is a KDBX 3.1 file. AuraVault currently reads KDBX 4 only; open and re-save it with KeePass 2.x / KeePassXC as KDBX 4 first.");
        }

        if (major != KdbxFormat.FileVersion4)
        {
            throw new KdbxFormatException($"Unsupported KDBX file version 0x{version:X8}.");
        }

        // ---- outer header ----
        byte[]? cipherUuid = null;
        var compression = KdbxFormat.CompressionAlgorithm.GZip;
        byte[]? masterSeed = null;
        byte[]? encryptionIv = null;
        VariantDictionary? kdfDict = null;
        var publicCustomData = new VariantDictionary();

        while (true)
        {
            byte fieldId = reader.ReadByte();
            uint fieldLen = reader.ReadUInt32();
            byte[] data = reader.ReadBytes(checked((int)fieldLen));
            if (data.Length != fieldLen)
            {
                throw new KdbxFormatException("Truncated KDBX header field.");
            }

            var field = (KdbxFormat.HeaderField)fieldId;
            if (field == KdbxFormat.HeaderField.EndOfHeader)
            {
                break;
            }

            switch (field)
            {
                case KdbxFormat.HeaderField.CipherId:
                    if (data.Length != 16)
                    {
                        throw new KdbxFormatException("CipherID must be 16 bytes.");
                    }

                    cipherUuid = data;
                    break;

                case KdbxFormat.HeaderField.CompressionFlags:
                    compression = (KdbxFormat.CompressionAlgorithm)BinaryPrimitives.ReadUInt32LittleEndian(data);
                    break;

                case KdbxFormat.HeaderField.MasterSeed:
                    masterSeed = data;
                    break;

                case KdbxFormat.HeaderField.EncryptionIv:
                    encryptionIv = data;
                    break;

                case KdbxFormat.HeaderField.KdfParameters:
                    kdfDict = VariantDictionary.Parse(data);
                    break;

                case KdbxFormat.HeaderField.PublicCustomData:
                    publicCustomData = VariantDictionary.Parse(data);
                    break;

                default:
                    // Comment or KDBX3-only fields — ignore.
                    break;
            }
        }

        long headerEnd = input.Position;
        byte[] headerBytes = ReadRange(input, 0, headerEnd);

        byte[] storedSha = reader.ReadBytes(32);
        byte[] storedHmac = reader.ReadBytes(32);
        if (storedSha.Length != 32 || storedHmac.Length != 32)
        {
            throw new KdbxFormatException("Truncated KDBX header integrity block.");
        }

        if (cipherUuid is null || masterSeed is null || encryptionIv is null || kdfDict is null)
        {
            throw new KdbxFormatException("KDBX header is missing a required field.");
        }

        if (masterSeed.Length != 32)
        {
            throw new KdbxFormatException("MasterSeed must be 32 bytes.");
        }

        if (!OuterCipher.IsSupported(cipherUuid))
        {
            throw new NotSupportedException("Unsupported KDBX cipher (only AES-256-CBC and ChaCha20 are supported).");
        }

        if (encryptionIv.Length != OuterCipher.IvLength(cipherUuid))
        {
            throw new KdbxFormatException("EncryptionIV length does not match the cipher.");
        }

        KdfParameters kdfParams = KdbxKdf.FromVariantDictionary(kdfDict);

        // ---- key derivation ----
        byte[] composite = key.ComputeComposite();
        byte[] transformed;
        try
        {
            transformed = Kdf.Create(kdfParams).Transform(composite);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(composite);
        }

        byte[] cipherKey = KdbxCryptoKeys.DeriveCipherKey(masterSeed, transformed);
        byte[] hmacBase = KdbxCryptoKeys.DeriveHmacBaseKey(masterSeed, transformed);
        CryptographicOperations.ZeroMemory(transformed);

        try
        {
            // Header SHA-256 (detects corruption).
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(headerBytes), storedSha))
            {
                throw new KdbxFormatException("KDBX header hash mismatch — the file is corrupted.");
            }

            // Header HMAC (detects a wrong key or tampering).
            byte[] headerHmacKey = KdbxCryptoKeys.DeriveBlockHmacKey(KdbxCryptoKeys.HeaderBlockIndex, hmacBase);
            try
            {
                byte[] computed = HMACSHA256.HashData(headerHmacKey, headerBytes);
                if (!CryptographicOperations.FixedTimeEquals(computed, storedHmac))
                {
                    throw new KdbxIntegrityException("Wrong master key (or the file was tampered with).");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(headerHmacKey);
            }

            // ---- body ----
            byte[] framed = ReadToEnd(input);
            byte[] ciphertext = HmacBlockStream.Read(framed, hmacBase);
            byte[] plaintext = OuterCipher.Decrypt(cipherUuid, cipherKey, encryptionIv, ciphertext);
            CryptographicOperations.ZeroMemory(ciphertext);

            byte[] payload = compression == KdbxFormat.CompressionAlgorithm.GZip ? GzipDecompress(plaintext) : plaintext;
            if (!ReferenceEquals(payload, plaintext))
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }

            // ---- inner header ----
            int pos = 0;
            var innerStreamId = KdbxFormat.InnerRandomStreamId.None;
            byte[] innerStreamKey = [];
            var binaries = new List<KdbxBinary>();

            while (true)
            {
                byte fieldId = payload[pos++];
                uint len = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(pos, 4));
                pos += 4;
                ReadOnlySpan<byte> data = payload.AsSpan(pos, (int)len);
                pos += (int)len;

                var field = (KdbxFormat.InnerHeaderField)fieldId;
                if (field == KdbxFormat.InnerHeaderField.EndOfHeader)
                {
                    break;
                }

                switch (field)
                {
                    case KdbxFormat.InnerHeaderField.InnerRandomStreamId:
                        innerStreamId = (KdbxFormat.InnerRandomStreamId)BinaryPrimitives.ReadUInt32LittleEndian(data);
                        break;

                    case KdbxFormat.InnerHeaderField.InnerRandomStreamKey:
                        innerStreamKey = data.ToArray();
                        break;

                    case KdbxFormat.InnerHeaderField.Binary:
                        var flags = (KdbxFormat.BinaryFlags)data[0];
                        binaries.Add(new KdbxBinary
                        {
                            Data = data[1..].ToArray(),
                            MemoryProtected = flags.HasFlag(KdbxFormat.BinaryFlags.MemoryProtected),
                        });
                        break;
                }
            }

            using IInnerRandomStream innerStream = InnerRandomStream.Create(innerStreamId, innerStreamKey);
            CryptographicOperations.ZeroMemory(innerStreamKey);

            byte[] xmlBytes = payload[pos..];
            Vault vault = KdbxXml.Read(xmlBytes, innerStream, binaries);
            CryptographicOperations.ZeroMemory(payload);

            return new KdbxDatabase
            {
                Vault = vault,
                FileVersion = version,
                PublicCustomData = publicCustomData,
                SaveParameters = new KdbxSaveParameters
                {
                    CipherUuid = cipherUuid,
                    Compression = compression,
                    Kdf = kdfParams,
                    InnerRandomStreamId = innerStreamId == KdbxFormat.InnerRandomStreamId.None
                        ? KdbxFormat.InnerRandomStreamId.ChaCha20
                        : innerStreamId,
                },
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(cipherKey);
            CryptographicOperations.ZeroMemory(hmacBase);
        }
    }

    public void Write(Stream output, KdbxDatabase database, CompositeKey key)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(key);

        var save = database.SaveParameters;
        byte[] cipherUuid = save.CipherUuid;
        if (!OuterCipher.IsSupported(cipherUuid))
        {
            throw new NotSupportedException("Unsupported cipher selected for writing.");
        }

        KdfParameters kdfParams = save.RegenerateNoncesOnWrite ? KdbxKdf.WithFreshSalt(save.Kdf) : save.Kdf;
        byte[] masterSeed = CryptoRandom.GetBytes(32);
        byte[] encryptionIv = CryptoRandom.GetBytes(OuterCipher.IvLength(cipherUuid));
        byte[] innerStreamKey = CryptoRandom.GetBytes(64);

        // ---- key derivation ----
        byte[] composite = key.ComputeComposite();
        byte[] transformed;
        try
        {
            transformed = Kdf.Create(kdfParams).Transform(composite);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(composite);
        }

        byte[] cipherKey = KdbxCryptoKeys.DeriveCipherKey(masterSeed, transformed);
        byte[] hmacBase = KdbxCryptoKeys.DeriveHmacBaseKey(masterSeed, transformed);
        CryptographicOperations.ZeroMemory(transformed);

        try
        {
            // ---- XML + inner header ----
            using IInnerRandomStream innerStream = InnerRandomStream.Create(save.InnerRandomStreamId, innerStreamKey);
            byte[] xmlBytes = KdbxXml.Write(database.Vault, innerStream);

            using var payloadStream = new MemoryStream();
            WriteInnerHeader(payloadStream, save.InnerRandomStreamId, innerStreamKey, database.Vault.Binaries);
            payloadStream.Write(xmlBytes);
            CryptographicOperations.ZeroMemory(xmlBytes);
            byte[] payload = payloadStream.ToArray();

            byte[] toEncrypt = save.Compression == KdbxFormat.CompressionAlgorithm.GZip ? GzipCompress(payload) : payload;
            if (!ReferenceEquals(toEncrypt, payload))
            {
                CryptographicOperations.ZeroMemory(payload);
            }

            byte[] ciphertext = OuterCipher.Encrypt(cipherUuid, cipherKey, encryptionIv, toEncrypt);
            CryptographicOperations.ZeroMemory(toEncrypt);
            byte[] framed = HmacBlockStream.Write(ciphertext, hmacBase);
            CryptographicOperations.ZeroMemory(ciphertext);

            // ---- outer header ----
            byte[] headerBytes = BuildOuterHeader(cipherUuid, save.Compression, masterSeed, encryptionIv, kdfParams, database.PublicCustomData);

            output.Write(headerBytes);
            output.Write(SHA256.HashData(headerBytes));

            byte[] headerHmacKey = KdbxCryptoKeys.DeriveBlockHmacKey(KdbxCryptoKeys.HeaderBlockIndex, hmacBase);
            try
            {
                output.Write(HMACSHA256.HashData(headerHmacKey, headerBytes));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(headerHmacKey);
            }

            output.Write(framed);
            output.Flush();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(cipherKey);
            CryptographicOperations.ZeroMemory(hmacBase);
            CryptographicOperations.ZeroMemory(innerStreamKey);
        }
    }

    private static byte[] BuildOuterHeader(
        ReadOnlySpan<byte> cipherUuid,
        KdbxFormat.CompressionAlgorithm compression,
        ReadOnlySpan<byte> masterSeed,
        ReadOnlySpan<byte> encryptionIv,
        KdfParameters kdfParams,
        VariantDictionary publicCustomData)
    {
        using var ms = new MemoryStream();
        Span<byte> u32 = stackalloc byte[4];

        BinaryPrimitives.WriteUInt32LittleEndian(u32, KdbxFormat.Signature1);
        ms.Write(u32);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, KdbxFormat.Signature2);
        ms.Write(u32);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, KdbxFormat.FileVersion41);
        ms.Write(u32);

        WriteHeaderField(ms, KdbxFormat.HeaderField.CipherId, cipherUuid);

        Span<byte> comp = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(comp, (uint)compression);
        WriteHeaderField(ms, KdbxFormat.HeaderField.CompressionFlags, comp);

        WriteHeaderField(ms, KdbxFormat.HeaderField.MasterSeed, masterSeed);
        WriteHeaderField(ms, KdbxFormat.HeaderField.EncryptionIv, encryptionIv);
        WriteHeaderField(ms, KdbxFormat.HeaderField.KdfParameters, KdbxKdf.ToVariantDictionary(kdfParams).Serialize());

        if (publicCustomData.Keys.Count > 0)
        {
            WriteHeaderField(ms, KdbxFormat.HeaderField.PublicCustomData, publicCustomData.Serialize());
        }

        WriteHeaderField(ms, KdbxFormat.HeaderField.EndOfHeader, EndOfHeaderMarker);
        return ms.ToArray();
    }

    private static void WriteHeaderField(Stream stream, KdbxFormat.HeaderField field, ReadOnlySpan<byte> data)
    {
        stream.WriteByte((byte)field);
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(len, (uint)data.Length);
        stream.Write(len);
        stream.Write(data);
    }

    private static void WriteInnerHeader(Stream stream, KdbxFormat.InnerRandomStreamId streamId, ReadOnlySpan<byte> streamKey, IReadOnlyList<KdbxBinary> binaries)
    {
        Span<byte> u32 = stackalloc byte[4];

        stream.WriteByte((byte)KdbxFormat.InnerHeaderField.InnerRandomStreamId);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, 4);
        stream.Write(u32);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, (uint)streamId);
        stream.Write(u32);

        stream.WriteByte((byte)KdbxFormat.InnerHeaderField.InnerRandomStreamKey);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, (uint)streamKey.Length);
        stream.Write(u32);
        stream.Write(streamKey);

        foreach (var bin in binaries)
        {
            stream.WriteByte((byte)KdbxFormat.InnerHeaderField.Binary);
            BinaryPrimitives.WriteUInt32LittleEndian(u32, (uint)(bin.Data.Length + 1));
            stream.Write(u32);
            stream.WriteByte((byte)(bin.MemoryProtected ? KdbxFormat.BinaryFlags.MemoryProtected : KdbxFormat.BinaryFlags.None));
            stream.Write(bin.Data);
        }

        stream.WriteByte((byte)KdbxFormat.InnerHeaderField.EndOfHeader);
        BinaryPrimitives.WriteUInt32LittleEndian(u32, 0);
        stream.Write(u32);
    }

    private static byte[] ReadRange(Stream stream, long start, long endExclusive)
    {
        long saved = stream.Position;
        stream.Position = start;
        byte[] buffer = new byte[endExclusive - start];
        stream.ReadExactly(buffer);
        stream.Position = saved;
        return buffer;
    }

    private static byte[] ReadToEnd(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static byte[] GzipDecompress(byte[] input)
    {
        using var source = new MemoryStream(input, writable: false);
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] GzipCompress(byte[] input)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(input);
        }

        return output.ToArray();
    }
}
