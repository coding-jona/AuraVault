namespace AuraVault.Core.Cryptography;

/// <summary>
/// Platform hook that keeps a memory region out of the page file for the lifetime of a
/// <see cref="SecureBuffer"/>. Core ships a no-op implementation; <c>AuraVault.Platform.Windows</c>
/// supplies a <c>VirtualLock</c>-backed one via <see cref="SecureBuffer.Locker"/>.
/// </summary>
public interface IMemoryLocker
{
    /// <summary>Attempts to lock <paramref name="length"/> bytes at <paramref name="address"/> into RAM.</summary>
    void Lock(nint address, int length);

    /// <summary>Releases a lock previously taken by <see cref="Lock"/>.</summary>
    void Unlock(nint address, int length);
}

/// <summary>Default no-op locker. Buffers are still zeroed on dispose; they are just not <c>mlock</c>ed.</summary>
public sealed class NullMemoryLocker : IMemoryLocker
{
    public static readonly NullMemoryLocker Instance = new();

    public void Lock(nint address, int length) { }

    public void Unlock(nint address, int length) { }
}
