namespace AuraVault.Core.Kdbx;

/// <summary>The byte stream is not a well-formed KDBX file.</summary>
public class KdbxFormatException : Exception
{
    public KdbxFormatException(string message) : base(message) { }

    public KdbxFormatException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// The KDBX file is structurally plausible but failed cryptographic verification — a wrong key,
/// a truncated file, or tampering. The caller must not use any partially decrypted data.
/// </summary>
public sealed class KdbxIntegrityException : KdbxFormatException
{
    public KdbxIntegrityException(string message) : base(message) { }
}
