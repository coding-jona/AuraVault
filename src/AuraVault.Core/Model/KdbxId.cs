namespace AuraVault.Core.Model;

/// <summary>
/// A KDBX object identifier: 16 arbitrary bytes, serialized as base64 in the KeePass XML.
/// Not a <see cref="Guid"/> — KeePass does not use the RFC-4122 text form.
/// </summary>
public readonly struct KdbxId : IEquatable<KdbxId>
{
    private readonly byte[] _bytes;

    public KdbxId(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 16)
        {
            throw new ArgumentException("A KDBX id is exactly 16 bytes.", nameof(bytes));
        }

        _bytes = bytes.ToArray();
    }

    public static KdbxId Empty { get; } = new(new byte[16]);

    public static KdbxId New() => new(Guid.NewGuid().ToByteArray());

    public static KdbxId FromBase64(string base64) => new(Convert.FromBase64String(base64));

    public bool IsEmpty => _bytes is null || _bytes.All(static b => b == 0);

    public ReadOnlySpan<byte> Span => _bytes ?? Empty._bytes;

    public string ToBase64() => Convert.ToBase64String(Span);

    public bool Equals(KdbxId other) => Span.SequenceEqual(other.Span);

    public override bool Equals(object? obj) => obj is KdbxId other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(Span);
        return hash.ToHashCode();
    }

    public override string ToString() => ToBase64();

    public static bool operator ==(KdbxId left, KdbxId right) => left.Equals(right);

    public static bool operator !=(KdbxId left, KdbxId right) => !left.Equals(right);
}
