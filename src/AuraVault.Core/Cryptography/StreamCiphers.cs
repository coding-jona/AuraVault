using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace AuraVault.Core.Cryptography;

/// <summary>
/// RFC 8439 ChaCha20 (96-bit nonce, 32-bit block counter). Used both as the KDBX 4 outer cipher
/// and, keyed differently, as the KDBX 4 inner random stream. XOR-symmetric: encrypt == decrypt.
/// </summary>
public sealed class ChaCha20Engine : IDisposable
{
    private readonly ChaCha7539Engine _engine = new();

    public ChaCha20Engine(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException("ChaCha20 key must be 32 bytes.", nameof(key));
        }

        if (nonce12.Length != 12)
        {
            throw new ArgumentException("ChaCha20 (RFC 8439) nonce must be 12 bytes.", nameof(nonce12));
        }

        _engine.Init(forEncryption: true, new ParametersWithIV(new KeyParameter(key.ToArray()), nonce12.ToArray()));
    }

    /// <summary>Processes <paramref name="input"/> into <paramref name="output"/> (same length).</summary>
    public void Process(ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (output.Length < input.Length)
        {
            throw new ArgumentException("Output span is shorter than input.", nameof(output));
        }

        byte[] inBuf = input.ToArray();
        byte[] outBuf = new byte[inBuf.Length];
        _engine.ProcessBytes(inBuf, 0, inBuf.Length, outBuf, 0);
        outBuf.CopyTo(output);
        Array.Clear(inBuf);
        Array.Clear(outBuf);
    }

    /// <summary>Fills <paramref name="destination"/> with the next keystream bytes (XORs against zeros).</summary>
    public void NextKeyStream(Span<byte> destination)
    {
        byte[] zeros = new byte[destination.Length];
        byte[] outBuf = new byte[destination.Length];
        _engine.ProcessBytes(zeros, 0, zeros.Length, outBuf, 0);
        outBuf.CopyTo(destination);
        Array.Clear(outBuf);
    }

    public void Dispose()
    {
        // BouncyCastle engines hold key material in managed arrays we cannot reach; re-init with zeros.
        try
        {
            _engine.Init(forEncryption: true, new ParametersWithIV(new KeyParameter(new byte[32]), new byte[12]));
        }
        catch (InvalidOperationException)
        {
            // Engine already in an unusable state; nothing more we can do.
        }
    }
}

/// <summary>Salsa20 with the fixed KeePass inner-random-stream nonce. Legacy KDBX 3.1 / KDBX 4 fallback.</summary>
public sealed class Salsa20Engine : IDisposable
{
    /// <summary>The constant IV KeePass uses for the Salsa20 inner random stream.</summary>
    public static ReadOnlySpan<byte> KeePassInnerNonce => [0xE8, 0x30, 0x09, 0x4B, 0x97, 0x20, 0x5D, 0x2A];

    private readonly Org.BouncyCastle.Crypto.Engines.Salsa20Engine _engine = new();

    public Salsa20Engine(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce8)
    {
        if (nonce8.Length != 8)
        {
            throw new ArgumentException("Salsa20 nonce must be 8 bytes.", nameof(nonce8));
        }

        _engine.Init(forEncryption: true, new ParametersWithIV(new KeyParameter(key.ToArray()), nonce8.ToArray()));
    }

    public void NextKeyStream(Span<byte> destination)
    {
        byte[] zeros = new byte[destination.Length];
        byte[] outBuf = new byte[destination.Length];
        _engine.ProcessBytes(zeros, 0, zeros.Length, outBuf, 0);
        outBuf.CopyTo(destination);
        Array.Clear(outBuf);
    }

    public void Dispose()
    {
        try
        {
            _engine.Reset();
        }
        catch (InvalidOperationException)
        {
            // ignore
        }
    }
}
