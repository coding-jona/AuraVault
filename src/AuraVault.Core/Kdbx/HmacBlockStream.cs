using System.Buffers.Binary;
using System.Security.Cryptography;

namespace AuraVault.Core.Kdbx;

/// <summary>
/// KDBX 4 HMAC-SHA-256 block framing over the ciphertext body. Each block is
/// <c>hmac(32) ‖ length(uint32 LE) ‖ data[length]</c>; the block HMAC is taken over
/// <c>LE64(index) ‖ length ‖ data</c> with a per-block key. A final zero-length block terminates.
/// </summary>
internal static class HmacBlockStream
{
    public const int DefaultBlockSize = 1024 * 1024;

    /// <summary>Verifies and concatenates all blocks. Throws <see cref="KdbxIntegrityException"/> on any mismatch.</summary>
    public static byte[] Read(ReadOnlySpan<byte> framed, ReadOnlySpan<byte> hmacBaseKey64)
    {
        using var output = new MemoryStream();
        int pos = 0;
        ulong index = 0;
        Span<byte> computed = stackalloc byte[32];

        while (true)
        {
            if (pos + 36 > framed.Length)
            {
                throw new KdbxIntegrityException("Truncated HMAC block header.");
            }

            ReadOnlySpan<byte> storedHmac = framed.Slice(pos, 32);
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(framed.Slice(pos + 32, 4));
            int dataStart = pos + 36;

            if (dataStart + length > framed.Length)
            {
                throw new KdbxIntegrityException("HMAC block runs past end of file.");
            }

            ReadOnlySpan<byte> data = framed.Slice(dataStart, (int)length);

            byte[] blockKey = KdbxCryptoKeys.DeriveBlockHmacKey(index, hmacBaseKey64);
            try
            {
                ComputeBlockHmac(blockKey, index, length, data, computed);
                if (!CryptographicOperations.FixedTimeEquals(computed, storedHmac))
                {
                    throw new KdbxIntegrityException(
                        "HMAC verification failed — wrong master key, corrupted file, or tampering.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(blockKey);
            }

            if (length == 0)
            {
                break;
            }

            output.Write(data);
            pos = dataStart + (int)length;
            index++;
        }

        return output.ToArray();
    }

    /// <summary>Frames <paramref name="payload"/> into HMAC blocks plus the terminating empty block.</summary>
    public static byte[] Write(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> hmacBaseKey64, int blockSize = DefaultBlockSize)
    {
        using var output = new MemoryStream();
        Span<byte> lenBuf = stackalloc byte[4];
        int pos = 0;
        ulong index = 0;

        while (pos < payload.Length)
        {
            int chunk = Math.Min(blockSize, payload.Length - pos);
            ReadOnlySpan<byte> data = payload.Slice(pos, chunk);
            WriteBlock(output, hmacBaseKey64, index, data, lenBuf);
            pos += chunk;
            index++;
        }

        // Terminating zero-length block.
        WriteBlock(output, hmacBaseKey64, index, ReadOnlySpan<byte>.Empty, lenBuf);
        return output.ToArray();
    }

    private static void WriteBlock(Stream output, ReadOnlySpan<byte> hmacBaseKey64, ulong index, ReadOnlySpan<byte> data, Span<byte> lenBuf)
    {
        byte[] blockKey = KdbxCryptoKeys.DeriveBlockHmacKey(index, hmacBaseKey64);
        try
        {
            Span<byte> hmac = stackalloc byte[32];
            ComputeBlockHmac(blockKey, index, (uint)data.Length, data, hmac);
            output.Write(hmac);
            BinaryPrimitives.WriteUInt32LittleEndian(lenBuf, (uint)data.Length);
            output.Write(lenBuf);
            output.Write(data);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(blockKey);
        }
    }

    private static void ComputeBlockHmac(ReadOnlySpan<byte> key, ulong index, uint length, ReadOnlySpan<byte> data, Span<byte> destination)
    {
        using var hmac = new HMACSHA256(key.ToArray());
        Span<byte> prefix = stackalloc byte[12];
        BinaryPrimitives.WriteUInt64LittleEndian(prefix[..8], index);
        BinaryPrimitives.WriteUInt32LittleEndian(prefix.Slice(8, 4), length);
        hmac.TransformBlock(prefix.ToArray(), 0, 12, null, 0);
        if (!data.IsEmpty)
        {
            byte[] dataArray = data.ToArray();
            hmac.TransformBlock(dataArray, 0, dataArray.Length, null, 0);
        }

        hmac.TransformFinalBlock([], 0, 0);
        hmac.Hash!.CopyTo(destination);
    }
}
