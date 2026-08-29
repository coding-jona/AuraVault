namespace AuraVault.Core.Model;

/// <summary>
/// A single entry field value. <see cref="IsProtected"/> mirrors the KeePass
/// <c>Protected="True"</c> attribute — such values are memory-protected in KeePass and are
/// obfuscated with the inner random stream on disk.
/// </summary>
public readonly record struct ProtectedString(string Value, bool IsProtected)
{
    public static ProtectedString Plain(string value) => new(value, IsProtected: false);

    public static ProtectedString Secret(string value) => new(value, IsProtected: true);

    public static readonly ProtectedString Empty = new(string.Empty, IsProtected: false);

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public override string ToString() => IsProtected ? "••••••" : Value;
}
