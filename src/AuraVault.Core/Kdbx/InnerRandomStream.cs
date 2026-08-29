using System.Security.Cryptography;
using AuraVault.Core.Cryptography;

namespace AuraVault.Core.Kdbx;

/// <summary>
/// The order-sensitive keystream that obfuscates <c>Protected="True"</c> field values inside the
/// KDBX XML. Read and write must visit protected values in the exact same document order.
/// </summary>
internal interface IInnerRandomStream : IDisposable
{
    /// <summary>XORs <paramref name="data"/> in place with the next keystream bytes.</summary>
    void Apply(Span<byte> data);
}

internal static class InnerRandomStream
{
    public static IInnerRandomStream Create(KdbxFormat.InnerRandomStreamId id, ReadOnlySpan<byte> key)
    {
        return id switch
        {
            KdbxFormat.InnerRandomStreamId.None => new PlainInnerRandomStream(),
            KdbxFormat.InnerRandomStreamId.Salsa20 => new Salsa20InnerRandomStream(key),
            KdbxFormat.InnerRandomStreamId.ChaCha20 => new ChaCha20InnerRandomStream(key),
            _ => throw new KdbxFormatException($"Unsupported inner random stream id {(uint)id}."),
        };
    }
}

internal sealed class PlainInnerRandomStream : IInnerRandomStream
{
    public void Apply(Span<byte> data)
    {
        // No obfuscation.
    }

    public void Dispose()
    {
        // Nothing to release.
    }
}

internal sealed class Salsa20InnerRandomStream : IInnerRandomStream
{
    private readonly Cryptography.Salsa20Engine _engine;

    public Salsa20InnerRandomStream(ReadOnlySpan<byte> key)
    {
        byte[] hashed = SHA256.HashData(key);
        try
        {
            _engine = new Cryptography.Salsa20Engine(hashed, Cryptography.Salsa20Engine.KeePassInnerNonce);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hashed);
        }
    }

    public void Apply(Span<byte> data)
    {
        byte[] keystream = new byte[data.Length];
        try
        {
            _engine.NextKeyStream(keystream);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= keystream[i];
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keystream);
        }
    }

    public void Dispose() => _engine.Dispose();
}

internal sealed class ChaCha20InnerRandomStream : IInnerRandomStream
{
    private readonly ChaCha20Engine _engine;

    public ChaCha20InnerRandomStream(ReadOnlySpan<byte> key)
    {
        byte[] hash = SHA512.HashData(key);
        try
        {
            _engine = new ChaCha20Engine(hash.AsSpan(0, 32), hash.AsSpan(32, 12));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(hash);
        }
    }

    public void Apply(Span<byte> data)
    {
        byte[] keystream = new byte[data.Length];
        try
        {
            _engine.NextKeyStream(keystream);
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= keystream[i];
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keystream);
        }
    }

    public void Dispose() => _engine.Dispose();
}
