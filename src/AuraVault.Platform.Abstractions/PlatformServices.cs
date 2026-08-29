using AuraVault.Core.Cryptography;

namespace AuraVault.Platform;

/// <summary>Locks/zeroes plaintext buffers in RAM. Wired into <see cref="SecureBuffer.Locker"/> at startup.</summary>
public interface ISecureMemory : IMemoryLocker;

/// <summary>Copies text to the clipboard and clears it after a TTL unless the user changed it first.</summary>
public interface IClipboardService
{
    Task SetTextAsync(string text, TimeSpan clearAfter, bool isSecret, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>Reports how long the machine has been idle (no keyboard/mouse input).</summary>
public interface IIdleMonitor
{
    TimeSpan GetIdleTime();
}

/// <summary>Raised when the Windows session locks/unlocks or the machine suspends/resumes.</summary>
public interface ISessionMonitor
{
    event EventHandler? SessionLocked;

    event EventHandler? SessionUnlocked;

    event EventHandler? Suspending;

    event EventHandler? Resumed;

    void Start();

    void Stop();
}

/// <summary>Standard per-user application directories.</summary>
public interface IAppPaths
{
    /// <summary>Roaming config: <c>%APPDATA%\AuraVault</c>.</summary>
    string ConfigDirectory { get; }

    /// <summary>Machine-local data: <c>%LOCALAPPDATA%\AuraVault</c>.</summary>
    string LocalDirectory { get; }

    /// <summary>Default vault location: <c>Documents\AuraVault</c>.</summary>
    string DocumentsVaultDirectory { get; }

    string BackupDirectory { get; }
}

/// <summary>No-op platform services for design-time / headless use.</summary>
public static class NullPlatform
{
    public static ISecureMemory SecureMemory { get; } = new NullSecureMemory();

    private sealed class NullSecureMemory : ISecureMemory
    {
        public void Lock(nint address, int length) { }

        public void Unlock(nint address, int length) { }
    }
}
