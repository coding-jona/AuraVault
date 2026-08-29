using AuraVault.Core.Cryptography;
using AuraVault.Core.Kdbx;

namespace AuraVault.Core.Vaults;

/// <summary>
/// File-level open/save for KDBX vaults: a single place that owns the codec, does atomic writes,
/// and keeps a <c>.bak</c> of the previous file. Higher layers (VaultSession, BackupService) build on this.
/// </summary>
public static class VaultFile
{
    private static readonly Kdbx4Codec Codec = new();

    /// <summary>Opens and decrypts a KDBX file. The <paramref name="key"/> is consumed by the read.</summary>
    public static KdbxDatabase Open(string path, CompositeKey key)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Codec.Read(stream, key);
    }

    /// <summary>Opens from an arbitrary seekable stream (fixtures, memory, network buffers).</summary>
    public static KdbxDatabase Open(Stream stream, CompositeKey key) => Codec.Read(stream, key);

    /// <summary>Writes <paramref name="database"/> to <paramref name="path"/> atomically, keeping one <c>.bak</c>.</summary>
    public static void Save(string path, KdbxDatabase database, CompositeKey key)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        string tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                Codec.Write(stream, database, key);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
            {
                string backupPath = path + ".bak";
                File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                TryDelete(tempPath);
            }
        }
    }

    /// <summary>Serializes to a new byte array (used by tests and export).</summary>
    public static byte[] Write(KdbxDatabase database, CompositeKey key)
    {
        using var ms = new MemoryStream();
        Codec.Write(ms, database, key);
        return ms.ToArray();
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Non-fatal: a stale .tmp will be overwritten on the next save.
        }
        catch (UnauthorizedAccessException)
        {
            // Same.
        }
    }
}
