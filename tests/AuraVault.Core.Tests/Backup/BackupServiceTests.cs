using AuraVault.Core.Backup;
using AwesomeAssertions;
using Xunit;

namespace AuraVault.Core.Tests.Backup;

public sealed class BackupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "av-backup-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CreateBackup_copies_the_file_under_a_per_vault_folder()
    {
        string vault = Path.Combine(_root, "Personal.kdbx");
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(vault, [1, 2, 3, 4]);

        string backupRoot = Path.Combine(_root, "backups");
        string? backup = BackupService.CreateBackup(vault, backupRoot, DateTimeOffset.UtcNow);

        backup.Should().NotBeNull();
        File.Exists(backup!).Should().BeTrue();
        File.ReadAllBytes(backup!).Should().Equal(1, 2, 3, 4);
        Path.GetDirectoryName(backup)!.Should().Contain(BackupService.VaultId(vault));
    }

    [Fact]
    public void CreateBackup_returns_null_when_the_source_is_missing()
    {
        BackupService.CreateBackup(Path.Combine(_root, "nope.kdbx"), _root, DateTimeOffset.UtcNow)
            .Should().BeNull();
    }

    [Fact]
    public void ApplyRetention_keeps_only_the_most_recent()
    {
        string vault = Path.Combine(_root, "V.kdbx");
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(vault, [0]);
        string backupRoot = Path.Combine(_root, "b");

        for (int i = 0; i < 6; i++)
        {
            string b = BackupService.CreateBackup(vault, backupRoot, DateTimeOffset.UtcNow.AddSeconds(i))!;
            File.SetCreationTimeUtc(b, DateTime.UtcNow.AddMinutes(i));
        }

        BackupService.ApplyRetention(vault, backupRoot, new RetentionPolicy(KeepMostRecent: 3));

        Directory.GetFiles(Path.Combine(backupRoot, BackupService.VaultId(vault)), "*.kdbx")
            .Should().HaveCount(3);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
