using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace AuraVault.Core.Cryptography;

/// <summary>
/// A fixed-size byte buffer for plaintext secrets. The backing array is pinned for its whole
/// lifetime (so the GC never copies it and leaves a stale copy behind), optionally locked into
/// RAM via <see cref="Locker"/>, and zeroed on <see cref="Dispose"/>.
/// </summary>
/// <remarks>
/// Never copy the contents into a <see cref="string"/>. Use <see cref="AsSpan()"/> at the point of
/// use and let the buffer own the only plaintext copy.
/// </remarks>
public sealed class SecureBuffer : IDisposable
{
    /// <summary>Process-wide locker. The Windows platform layer replaces this at startup.</summary>
    public static IMemoryLocker Locker { get; set; } = NullMemoryLocker.Instance;

    private readonly byte[] _data;
    private GCHandle _handle;
    private bool _disposed;

    public SecureBuffer(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        _data = GC.AllocateArray<byte>(length, pinned: true);
        _handle = GCHandle.Alloc(_data, GCHandleType.Pinned);
        if (length > 0)
        {
            Locker.Lock(_handle.AddrOfPinnedObject(), length);
        }
    }

    /// <summary>Creates a buffer that takes a defensive copy of <paramref name="source"/>.</summary>
    public static SecureBuffer CopyOf(ReadOnlySpan<byte> source)
    {
        var buffer = new SecureBuffer(source.Length);
        source.CopyTo(buffer.AsSpan());
        return buffer;
    }

    /// <summary>Wraps <paramref name="source"/> into a buffer, then zeroes <paramref name="source"/>.</summary>
    public static SecureBuffer TakeOwnershipOf(Span<byte> source)
    {
        var buffer = CopyOf(source);
        CryptographicOperations.ZeroMemory(source);
        return buffer;
    }

    public int Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _data.Length;
        }
    }

    public Span<byte> AsSpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _data;
    }

    public ReadOnlySpan<byte> AsReadOnlySpan()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _data;
    }

    /// <summary>Returns an independent <see cref="SecureBuffer"/> with the same bytes.</summary>
    public SecureBuffer Clone() => CopyOf(AsReadOnlySpan());

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CryptographicOperations.ZeroMemory(_data);
        if (_handle.IsAllocated)
        {
            if (_data.Length > 0)
            {
                Locker.Unlock(_handle.AddrOfPinnedObject(), _data.Length);
            }

            _handle.Free();
        }
    }
}
