using AuraVault.Core.Cryptography;
using AuraVault.Core.Kdbx;
using AuraVault.Core.Model;
using AwesomeAssertions;
using Xunit;

namespace AuraVault.Core.Tests.Kdbx;

public sealed class Kdbx4RoundTripTests
{
    private const string Password = "correct horse battery staple 42";

    private static CompositeKey Key() => new CompositeKey().AddPassword(Password);

    /// <summary>Fast KDF so the test suite stays quick; the codec paths are what we exercise here.</summary>
    private static KdbxSaveParameters FastParams(byte[] cipherUuid, KdbxFormat.CompressionAlgorithm compression) => new()
    {
        CipherUuid = cipherUuid,
        Compression = compression,
        Kdf = new Argon2KdfParameters(
            Salt: CryptoRandom.GetBytes(16),
            Parallelism: 1,
            MemoryBytes: 1024 * 1024,
            Iterations: 1,
            Version: 0x13,
            IsArgon2id: true),
        InnerRandomStreamId = KdbxFormat.InnerRandomStreamId.ChaCha20,
    };

    private static KdbxDatabase BuildSampleDatabase(KdbxSaveParameters save)
    {
        var now = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
        var db = KdbxDatabase.CreateEmpty("Round Trip", now);
        db.SaveParameters = save;

        var web = db.Vault.Root.FindOrCreatePath(["Import", "iPhone", "Web"], now);

        var entry = new Entry { Times = EntryTimes.CreatedNow(now) };
        entry.Title = "GitHub";
        entry.UserName = "octocat";
        entry.Password = "s3cr3t-üni¢ode-🔒";
        entry.Url = "https://github.com";
        entry.Notes = "Line one\nLine two";
        entry.Strings["Recovery Code"] = ProtectedString.Secret("ABCD-EFGH-IJKL");
        entry.Strings["Environment"] = ProtectedString.Plain("production");
        entry.Tags.Add("dev");
        entry.Tags.Add("vcs");

        var older = entry.Clone(includeHistory: false);
        older.Password = "old-password";
        older.Times.LastModificationTime = now.AddDays(-30);
        entry.History.Add(older);

        web.Entries.Add(entry);

        var wifi = db.Vault.Root.FindOrCreatePath(["Import", "iPhone", "Wi-Fi"], now);
        var wifiEntry = new Entry { Times = EntryTimes.CreatedNow(now) };
        wifiEntry.Title = "HomeNet";
        wifiEntry.Password = "hunter2hunter2";
        wifi.Entries.Add(wifiEntry);

        return db;
    }

    public static TheoryData<byte[], KdbxFormat.CompressionAlgorithm> CipherAndCompression() => new()
    {
        { KdbxFormat.CipherUuids.ChaCha20.ToArray(), KdbxFormat.CompressionAlgorithm.GZip },
        { KdbxFormat.CipherUuids.ChaCha20.ToArray(), KdbxFormat.CompressionAlgorithm.None },
        { KdbxFormat.CipherUuids.Aes256Cbc.ToArray(), KdbxFormat.CompressionAlgorithm.GZip },
        { KdbxFormat.CipherUuids.Aes256Cbc.ToArray(), KdbxFormat.CompressionAlgorithm.None },
    };

    [Theory]
    [MemberData(nameof(CipherAndCompression))]
    public void Write_then_read_preserves_the_model(byte[] cipherUuid, KdbxFormat.CompressionAlgorithm compression)
    {
        var save = FastParams(cipherUuid, compression);
        var original = BuildSampleDatabase(save);

        byte[] bytes = VaultFileWrite(original);
        KdbxDatabase reloaded = VaultFileRead(bytes);

        reloaded.Vault.Meta.DatabaseName.Should().Be("Round Trip");

        var webEntry = reloaded.Vault.Root.AllEntries().Single(e => e.Title == "GitHub");
        webEntry.UserName.Should().Be("octocat");
        webEntry.Password.Should().Be("s3cr3t-üni¢ode-🔒");
        webEntry.Url.Should().Be("https://github.com");
        webEntry.Notes.Should().Be("Line one\nLine two");
        webEntry.Strings["Recovery Code"].Should().Be(ProtectedString.Secret("ABCD-EFGH-IJKL"));
        webEntry.Strings["Environment"].Should().Be(ProtectedString.Plain("production"));
        webEntry.Tags.Should().BeEquivalentTo("dev", "vcs");
        webEntry.History.Should().ContainSingle();
        webEntry.History[0].Password.Should().Be("old-password");

        var wifiEntry = reloaded.Vault.Root.AllEntries().Single(e => e.Title == "HomeNet");
        wifiEntry.Password.Should().Be("hunter2hunter2");

        // Group path survived.
        reloaded.Vault.Root
            .AllGroups()
            .Select(g => g.Name)
            .Should().Contain(["Import", "iPhone", "Web", "Wi-Fi"]);
    }

    [Fact]
    public void Reading_with_the_wrong_password_throws_integrity_and_yields_no_plaintext()
    {
        var save = FastParams(KdbxFormat.CipherUuids.ChaCha20.ToArray(), KdbxFormat.CompressionAlgorithm.GZip);
        byte[] bytes = VaultFileWrite(BuildSampleDatabase(save));

        var act = () =>
        {
            using var ms = new MemoryStream(bytes);
            new Kdbx4Codec().Read(ms, new CompositeKey().AddPassword("wrong password"));
        };

        act.Should().Throw<KdbxIntegrityException>();
    }

    [Fact]
    public void A_flipped_ciphertext_byte_is_rejected()
    {
        var save = FastParams(KdbxFormat.CipherUuids.ChaCha20.ToArray(), KdbxFormat.CompressionAlgorithm.GZip);
        byte[] bytes = VaultFileWrite(BuildSampleDatabase(save));
        bytes[^1] ^= 0xFF; // corrupt the last HMAC block

        var act = () =>
        {
            using var ms = new MemoryStream(bytes);
            new Kdbx4Codec().Read(ms, Key());
        };

        act.Should().Throw<KdbxFormatException>();
    }

    [Fact]
    public void A_non_kdbx_stream_is_rejected_cleanly()
    {
        var act = () =>
        {
            using var ms = new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]);
            new Kdbx4Codec().Read(ms, Key());
        };

        act.Should().Throw<KdbxFormatException>();
    }

    private static byte[] VaultFileWrite(KdbxDatabase db)
    {
        using var ms = new MemoryStream();
        new Kdbx4Codec().Write(ms, db, Key());
        return ms.ToArray();
    }

    private static KdbxDatabase VaultFileRead(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        return new Kdbx4Codec().Read(ms, Key());
    }
}
