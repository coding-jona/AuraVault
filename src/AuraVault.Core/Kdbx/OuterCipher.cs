using System.Security.Cryptography;
using AuraVault.Core.Cryptography;

namespace AuraVault.Core.Kdbx;

/// <summary>The KDBX outer payload cipher: AES-256-CBC (PKCS#7) or ChaCha20 (RFC 8439).</summary>
internal static class OuterCipher
{
    public static bool IsSupported(ReadOnlySpan<byte> cipherUuid) =>
        cipherUuid.SequenceEqual(KdbxFormat.CipherUuids.Aes256Cbc) ||
        cipherUuid.SequenceEqual(KdbxFormat.CipherUuids.ChaCha20);

    public static int IvLength(ReadOnlySpan<byte> cipherUuid) =>
        cipherUuid.SequenceEqual(KdbxFormat.CipherUuids.ChaCha20) ? 12 : 16;

    public static byte[] Decrypt(ReadOnlySpan<byte> cipherUuid, ReadOnlySpan<byte> key32, ReadOnlySpan<byte> iv, ReadOnlySpan<byte> ciphertext)
    {
        if (cipherUuid.SequenceEqual(KdbxFormat.CipherUuids.Aes256Cbc))
        {
            using var aes = Aes.Create();
            aes.Key = key32.ToArray();
            try
            {
                return aes.DecryptCbc(ciphertext, iv, PaddingMode.PKCS7);
            }
            catch (CryptographicException ex)
            {
                throw new KdbxIntegrityException("AES-CBC padding is invalid — wrong key or corrupted file.")
                {
                    Source = ex.Source,
                };
            }
        }

        if (cipherUuid.SequenceEqual(KdbxFormat.CipherUuids.ChaCha20))
        {
            using var engine = new ChaCha20Engine(key32, iv);
            byte[] output = new byte[ciphertext.Length];
            engine.Process(ciphertext, output);
            return output;
        }

        throw new KdbxFormatException("Unsupported outer cipher UUID.");
    }

    public static byte[] Encrypt(ReadOnlySpan<byte> cipherUuid, ReadOnlySpan<byte> key32, ReadOnlySpan<byte> iv, ReadOnlySpan<byte> plaintext)
    {
        if (cipherUuid.SequenceEqual(KdbxFormat.CipherUuids.Aes256Cbc))
        {
            using var aes = Aes.Create();
            aes.Key = key32.ToArray();
            return aes.EncryptCbc(plaintext, iv, PaddingMode.PKCS7);
        }

        if (cipherUuid.SequenceEqual(KdbxFormat.CipherUuids.ChaCha20))
        {
            using var engine = new ChaCha20Engine(key32, iv);
            byte[] output = new byte[plaintext.Length];
            engine.Process(plaintext, output);
            return output;
        }

        throw new KdbxFormatException("Unsupported outer cipher UUID.");
    }
}
