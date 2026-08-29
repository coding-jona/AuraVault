using System.Globalization;
using System.Security.Cryptography;

namespace AuraVault.Core.Backup;

/// <summary>How many timestamped backups to keep, and a total size ceiling.</summary>
public sealed record RetentionPolicy(int KeepMostRecent = 20, long MaxTotalBytes = 500L * 1024 * 1024)
{
    public static RetentionPolicy Default { get; } = new();
}

/// <summary>
/// Copies a vault file into a per-vault backup folder before each save and prunes old copies.
/// P1 keeps the N most recent within a size budget; daily/weekly/monthly bucketing arrives in P3.
/// </summary>
public static class BackupService
{
    /// <summary>Copies <paramref name="vaultPath"/> to <c>{backupRoot}/{vaultId}/{UTC-timestamp}.kdbx</c>. Returns the backup path, or null if the source does not exist yet.</summary>
    public static string? CreateBackup(string vaultPath, string backupRoot, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrEmpty(vaultPath);
        ArgumentException.ThrowIfNullOrEmpty(backupRoot);

        if (!File.Exists(vaultPath))
        {
            return null;
        }

        string dir = Path.Combine(backupRoot, VaultId(vaultPath));
        Directory.CreateDirectory(dir);

        string stamp = now.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        string target = Path.Combine(dir, stamp + ".kdbx");
        if (File.Exists(target))
        {
            target = Path.Combine(dir, $"{stamp}_{Guid.NewGuid():N}.kdbx");
        }

        File.Copy(vaultPath, target);
        return target;
    }

    public static void ApplyRetention(string vaultPath, string backupRoot, RetentionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        string dir = Path.Combine(backupRoot, VaultId(vaultPath));
        if (!Directory.Exists(dir))
        {
            return;
        }

        var backups = new DirectoryInfo(dir)
            .GetFiles("*.kdbx")
            .OrderByDescending(f => f.CreationTimeUtc)
            .ToList();

        long runningTotal = 0;
        for (int i = 0; i < backups.Count; i++)
        {
            runningTotal += backups[i].Length;
            bool overCount = i >= policy.KeepMostRecent;
            bool overSize = runningTotal > policy.MaxTotalBytes && i > 0;
            if (overCount || overSize)
            {
                TryDelete(backups[i]);
            }
        }
    }

    /// <summary>Stable per-vault id derived from the absolute path (until the file is opened, we don't have its header seed).</summary>
    public static string VaultId(string vaultPath)
    {
        string full = Path.GetFullPath(vaultPath).ToLowerInvariant();
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(full), hash);
        return Convert.ToHexStringLower(hash[..8]);
    }

    private static void TryDelete(FileInfo file)
    {
        try
        {
            file.Delete();
        }
        catch (IOException)
        {
            // A locked/again-referenced backup will be retried next save.
        }
        catch (UnauthorizedAccessException)
        {
            // Same.
        }
    }
}
