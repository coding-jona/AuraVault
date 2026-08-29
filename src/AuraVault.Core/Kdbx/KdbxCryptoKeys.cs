using System.Buffers.Binary;
using System.Security.Cryptography;

namespace AuraVault.Core.Kdbx;

/// <summary>
/// Derives the per-file cipher key and HMAC keys from the master seed and the KDF-transformed key,
/// following the KDBX 4 rules.
/// </summary>
internal static class KdbxCryptoKeys
{
    /// <summary>Block index used for the header's own HMAC.</summary>
    public const ulong HeaderBlockIndex = 0xFFFFFFFFFFFFFFFF;

    /// <summary>cipherKey = SHA-256(masterSeed ‖ transformedKey).</summary>
    public static byte[] DeriveCipherKey(ReadOnlySpan<byte> masterSeed32, ReadOnlySpan<byte> transformedKey32)
    {
        using var sha = SHA256.Create();
        sha.TransformBlock(masterSeed32.ToArray(), 0, masterSeed32.Length, null, 0);
        sha.TransformFinalBlock(transformedKey32.ToArray(), 0, transformedKey32.Length);
        return sha.Hash!;
    }

    /// <summary>hmacBaseKey = SHA-512(masterSeed ‖ transformedKey ‖ 0x01).</summary>
    public static byte[] DeriveHmacBaseKey(ReadOnlySpan<byte> masterSeed32, ReadOnlySpan<byte> transformedKey32)
    {
        using var sha = SHA512.Create();
        sha.TransformBlock(masterSeed32.ToArray(), 0, masterSeed32.Length, null, 0);
        sha.TransformBlock(transformedKey32.ToArray(), 0, transformedKey32.Length, null, 0);
        sha.TransformFinalBlock([0x01], 0, 1);
        return sha.Hash!;
    }

    /// <summary>Per-block HMAC key = SHA-512(LE64(blockIndex) ‖ hmacBaseKey).</summary>
    public static byte[] DeriveBlockHmacKey(ulong blockIndex, ReadOnlySpan<byte> hmacBaseKey64)
    {
        Span<byte> idx = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(idx, blockIndex);

        using var sha = SHA512.Create();
        sha.TransformBlock(idx.ToArray(), 0, 8, null, 0);
        sha.TransformFinalBlock(hmacBaseKey64.ToArray(), 0, hmacBaseKey64.Length);
        return sha.Hash!;
    }
}
