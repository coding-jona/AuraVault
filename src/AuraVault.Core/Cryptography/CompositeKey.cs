using System.Security.Cryptography;
using System.Text;

namespace AuraVault.Core.Cryptography;

/// <summary>
/// A KeePass composite key: an ordered set of 32-byte factor hashes (password, key file, hardware
/// secret, …). The composite is <c>SHA-256(factor0 ‖ factor1 ‖ …)</c>; the KDF then stretches that.
/// </summary>
public sealed class CompositeKey : IDisposable
{
    private readonly List<byte[]> _factors = [];
    private bool _disposed;

    /// <summary>Adds a password factor: <c>SHA-256(UTF-8 bytes)</c>.</summary>
    public CompositeKey AddPassword(ReadOnlySpan<byte> utf8Password)
    {
        ThrowIfDisposed();
        _factors.Add(SHA256.HashData(utf8Password));
        return this;
    }

    /// <summary>Adds a password factor from a string (the transient UTF-8 buffer is zeroed).</summary>
    public CompositeKey AddPassword(string password)
    {
        ThrowIfDisposed();
        int max = Encoding.UTF8.GetMaxByteCount(password.Length);
        byte[] buffer = new byte[max];
        try
        {
            int n = Encoding.UTF8.GetBytes(password, buffer);
            _factors.Add(SHA256.HashData(buffer.AsSpan(0, n)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }

        return this;
    }

    /// <summary>Adds an already-resolved 32-byte key-file / hardware factor (used verbatim, not re-hashed).</summary>
    public CompositeKey AddResolvedFactor(ReadOnlySpan<byte> factor32)
    {
        ThrowIfDisposed();
        if (factor32.Length != 32)
        {
            throw new ArgumentException("A resolved composite-key factor must be 32 bytes.", nameof(factor32));
        }

        _factors.Add(factor32.ToArray());
        return this;
    }

    public int FactorCount => _factors.Count;

    /// <summary>Computes the 32-byte composite key. The caller owns and should zero the result.</summary>
    public byte[] ComputeComposite()
    {
        ThrowIfDisposed();
        if (_factors.Count == 0)
        {
            throw new InvalidOperationException("A composite key needs at least one factor.");
        }

        using var sha = SHA256.Create();
        foreach (var factor in _factors)
        {
            sha.TransformBlock(factor, 0, factor.Length, null, 0);
        }

        sha.TransformFinalBlock([], 0, 0);
        return sha.Hash!;
    }

    /// <summary>Computes the composite then runs it through the KDF. Returns the 32-byte transformed key.</summary>
    public byte[] Transform(KdfParameters kdfParameters)
    {
        byte[] composite = ComputeComposite();
        try
        {
            return Kdf.Create(kdfParameters).Transform(composite);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(composite);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var factor in _factors)
        {
            CryptographicOperations.ZeroMemory(factor);
        }

        _factors.Clear();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
