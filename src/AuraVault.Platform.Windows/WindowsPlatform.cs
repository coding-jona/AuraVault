using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AuraVault.Platform;

namespace AuraVault.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed partial class WindowsSecureMemory : ISecureMemory
{
    public void Lock(nint address, int length)
    {
        if (length > 0)
        {
            _ = VirtualLock(address, (nuint)length);
        }
    }

    public void Unlock(nint address, int length)
    {
        if (length > 0)
        {
            _ = VirtualUnlock(address, (nuint)length);
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool VirtualLock(nint address, nuint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool VirtualUnlock(nint address, nuint size);
}

[SupportedOSPlatform("windows")]
public sealed partial class WindowsIdleMonitor : IIdleMonitor
{
    public TimeSpan GetIdleTime()
    {
        var info = new LastInputInfo { CbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        uint idleMs = unchecked((uint)Environment.TickCount) - info.DwTime;
        return TimeSpan.FromMilliseconds(idleMs);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint CbSize;
        public uint DwTime;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLastInputInfo(ref LastInputInfo info);
}

/// <summary>Standard Windows per-user paths under <c>AuraVault</c>.</summary>
public sealed class WindowsAppPaths : IAppPaths
{
    public string ConfigDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AuraVault");

    public string LocalDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AuraVault");

    public string DocumentsVaultDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AuraVault");

    public string BackupDirectory =>
        Path.Combine(LocalDirectory, "Backups");

    public WindowsAppPaths()
    {
        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(LocalDirectory);
        Directory.CreateDirectory(BackupDirectory);
    }
}
