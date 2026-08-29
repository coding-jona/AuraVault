using System.Security.Cryptography;
using Konscious.Security.Cryptography;

namespace AuraVault.Core.Cryptography;

/// <summary>Base for a key-derivation configuration. Concrete records carry the tunable parameters.</summary>
public abstract record KdfParameters
{
    /// <summary>The KDBX KDF UUID (16 bytes) this configuration maps to.</summary>
    public abstract ReadOnlySpan<byte> Uuid { get; }
}

/// <summary>Argon2 parameters. <paramref name="MemoryBytes"/> follows the KDBX convention (bytes, not KiB).</summary>
public sealed record Argon2KdfParameters(
    byte[] Salt,
    uint Parallelism,
    ulong MemoryBytes,
    ulong Iterations,
    uint Version,
    bool IsArgon2id) : KdfParameters
{
    public override ReadOnlySpan<byte> Uuid => IsArgon2id ? KdfUuids.Argon2id : KdfUuids.Argon2d;

    /// <summary>Sensible defaults for a freshly created vault: Argon2id, 64 MiB, t=4, p=2.</summary>
    public static Argon2KdfParameters CreateDefault() => new(
        Salt: CryptoRandom.GetBytes(32),
        Parallelism: 2,
        MemoryBytes: 64UL * 1024 * 1024,
        Iterations: 4,
        Version: 0x13,
        IsArgon2id: true);
}

/// <summary>Legacy KeePass AES-KDF parameters (KDBX 3.1 and optionally KDBX 4).</summary>
public sealed record AesKdfParameters(byte[] Seed, ulong Rounds) : KdfParameters
{
    public override ReadOnlySpan<byte> Uuid => KdfUuids.Aes;
}

/// <summary>Canonical KDBX KDF UUIDs.</summary>
public static class KdfUuids
{
    public static ReadOnlySpan<byte> Aes => [0xC9, 0xD9, 0xF3, 0x9A, 0x62, 0x8A, 0x44, 0x60, 0xBF, 0x74, 0x0D, 0x08, 0xC1, 0x8A, 0x4F, 0xEA];
    public static ReadOnlySpan<byte> Argon2d => [0xEF, 0x63, 0x6D, 0xDF, 0x8C, 0x29, 0x44, 0x4B, 0x91, 0xF7, 0xA9, 0xA4, 0x03, 0xE3, 0x0A, 0x0C];
    public static ReadOnlySpan<byte> Argon2id => [0x9E, 0x29, 0x8B, 0x19, 0x56, 0xDB, 0x47, 0x73, 0xB2, 0x3D, 0xFC, 0x3E, 0xC6, 0xF0, 0xA1, 0xE6];
}

/// <summary>Transforms a 32-byte composite key into a 32-byte transformed key.</summary>
public interface IKdf
{
    byte[] Transform(ReadOnlySpan<byte> compositeKey);
}

public static class Kdf
{
    public static IKdf Create(KdfParameters parameters) => parameters switch
    {
        Argon2KdfParameters a => new Argon2Kdf(a),
        AesKdfParameters aes => new AesKdf(aes),
        _ => throw new NotSupportedException($"Unsupported KDF parameter type {parameters.GetType().Name}."),
    };
}

internal sealed class Argon2Kdf(Argon2KdfParameters p) : IKdf
{
    public byte[] Transform(ReadOnlySpan<byte> compositeKey)
    {
        // KDBX stores the memory cost in bytes; Konscious expects KiB.
        int memoryKib = checked((int)(p.MemoryBytes / 1024));
        byte[] password = compositeKey.ToArray();
        try
        {
            using Argon2 argon = p.IsArgon2id ? new Argon2id(password) : new Argon2d(password);
            argon.Salt = p.Salt;
            argon.DegreeOfParallelism = checked((int)p.Parallelism);
            argon.MemorySize = memoryKib;
            argon.Iterations = checked((int)p.Iterations);
            return argon.GetBytes(32);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(password);
        }
    }
}

internal sealed class AesKdf(AesKdfParameters p) : IKdf
{
    public byte[] Transform(ReadOnlySpan<byte> compositeKey)
    {
        if (compositeKey.Length != 32)
        {
            throw new ArgumentException("AES-KDF requires a 32-byte composite key.", nameof(compositeKey));
        }

        byte[] a = compositeKey.ToArray();
        byte[] b = new byte[32];
        try
        {
            using var aes = Aes.Create();
            aes.Key = p.Seed;

            for (ulong i = 0; i < p.Rounds; i++)
            {
                aes.EncryptEcb(a, b, PaddingMode.None);
                (a, b) = (b, a);
            }

            return SHA256.HashData(a);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(a);
            CryptographicOperations.ZeroMemory(b);
        }
    }
}
