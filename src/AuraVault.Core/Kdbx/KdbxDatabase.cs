using AuraVault.Core.Cryptography;
using AuraVault.Core.Model;

namespace AuraVault.Core.Kdbx;

/// <summary>Cipher / compression / KDF choices for writing a KDBX file.</summary>
public sealed class KdbxSaveParameters
{
    /// <summary>16-byte cipher UUID. Defaults to ChaCha20.</summary>
    public byte[] CipherUuid { get; init; } = KdbxFormat.CipherUuids.ChaCha20.ToArray();

    public KdbxFormat.CompressionAlgorithm Compression { get; init; } = KdbxFormat.CompressionAlgorithm.GZip;

    public KdfParameters Kdf { get; init; } = Argon2KdfParameters.CreateDefault();

    public KdbxFormat.InnerRandomStreamId InnerRandomStreamId { get; init; } = KdbxFormat.InnerRandomStreamId.ChaCha20;

    /// <summary>When true (default), the master seed, IV, inner key and KDF salt are regenerated on every save.</summary>
    public bool RegenerateNoncesOnWrite { get; init; } = true;

    public static KdbxSaveParameters CreateDefault() => new();
}

/// <summary>A decrypted KDBX database: the model plus the parameters needed to write it back.</summary>
public sealed class KdbxDatabase
{
    public required Vault Vault { get; init; }

    public KdbxSaveParameters SaveParameters { get; set; } = KdbxSaveParameters.CreateDefault();

    public VariantDictionary PublicCustomData { get; init; } = new();

    /// <summary>File version actually read / to be written. Writes target KDBX 4.1.</summary>
    public uint FileVersion { get; set; } = KdbxFormat.FileVersion41;

    public static KdbxDatabase CreateEmpty(string name, DateTimeOffset now) => new()
    {
        Vault = Vault.CreateEmpty(name, now),
    };
}

/// <summary>Reads and writes KDBX files.</summary>
public interface IKdbxCodec
{
    /// <summary>Decrypts and parses a KDBX stream. Throws <see cref="KdbxIntegrityException"/> on a wrong key.</summary>
    KdbxDatabase Read(Stream input, CompositeKey key);

    /// <summary>Encrypts and writes <paramref name="database"/> to <paramref name="output"/>.</summary>
    void Write(Stream output, KdbxDatabase database, CompositeKey key);
}
