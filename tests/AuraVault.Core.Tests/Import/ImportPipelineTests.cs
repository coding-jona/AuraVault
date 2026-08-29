using AuraVault.Core.Import;
using AuraVault.Core.Model;
using AwesomeAssertions;
using Xunit;

namespace AuraVault.Core.Tests.Import;

public sealed class ImportPipelineTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 0, 0, 0, TimeSpan.Zero);

    private static TabularTable WebTable(params string[] dataLines)
    {
        string csv = "domain,benutzer,passwort,pfad,geaendert,erstellt\n" + string.Join('\n', dataLines);
        return DelimitedText.Parse(csv);
    }

    [Fact]
    public void WebLogins_preset_maps_every_field()
    {
        var table = WebTable("accounts.google.com,alice@example.com,s3cret,/signin,2025092016,2020110609");
        var vault = Vault.CreateEmpty("t", Now);

        var preview = ImportPipeline.Preview(table, IPhoneRecoveryPreset.WebLogins(), vault, DedupeStrategy.Skip, Now);

        preview.Rows.Should().ContainSingle();
        var e = preview.Rows[0].Proposed;
        e.Title.Should().Be("google.com");
        e.Url.Should().Be("https://accounts.google.com");
        e.UserName.Should().Be("alice@example.com");
        e.Strings[EntryFields.Password].Should().Be(ProtectedString.Secret("s3cret"));
        e.Strings["Path"].Value.Should().Be("/signin");
        e.Times.LastModificationTime!.Value.Year.Should().Be(2025);
        e.Times.CreationTime!.Value.Year.Should().Be(2020);
        preview.Rows[0].TargetGroupPath.Should().Be("Import/iPhone/Web");
    }

    [Fact]
    public void Commit_places_entries_in_the_nested_group_and_is_idempotent()
    {
        var table = WebTable(
            "github.com,octocat,pw1,,,",
            "gitlab.com,alice,pw2,,,");
        var vault = Vault.CreateEmpty("t", Now);

        var first = ImportPipeline.Preview(table, IPhoneRecoveryPreset.WebLogins(), vault, DedupeStrategy.Skip, Now);
        var result = ImportPipeline.Commit(first, vault, Now);
        result.Added.Should().Be(2);

        vault.Root.FindOrCreatePath(["Import", "iPhone", "Web"], Now).Entries.Should().HaveCount(2);

        var second = ImportPipeline.Preview(table, IPhoneRecoveryPreset.WebLogins(), vault, DedupeStrategy.Skip, Now);
        second.NewCount.Should().Be(0);
        second.DuplicateCount.Should().Be(2);
        ImportPipeline.Commit(second, vault, Now).Added.Should().Be(0);
    }

    [Fact]
    public void Merge_updates_an_entry_that_is_missing_a_password()
    {
        var vault = Vault.CreateEmpty("t", Now);
        var group = vault.Root.FindOrCreatePath(["Import", "iPhone", "Web"], Now);
        var existing = new Entry { Times = EntryTimes.CreatedNow(Now) };
        existing.Title = "example.com";
        existing.Url = "https://example.com";
        existing.UserName = "bob";
        group.Entries.Add(existing);

        var table = WebTable("example.com,bob,new-password,,,");
        var preview = ImportPipeline.Preview(table, IPhoneRecoveryPreset.WebLogins(), vault, DedupeStrategy.Merge, Now);

        preview.UpdatedCount.Should().Be(1);
        ImportPipeline.Commit(preview, vault, Now).Updated.Should().Be(1);

        existing.Password.Should().Be("new-password");
        existing.History.Should().ContainSingle();
    }

    [Fact]
    public void KeepBoth_imports_a_duplicate_as_a_new_entry()
    {
        var vault = Vault.CreateEmpty("t", Now);
        var table = WebTable("dup.com,sam,same,,,");

        ImportPipeline.Commit(
            ImportPipeline.Preview(table, IPhoneRecoveryPreset.WebLogins(), vault, DedupeStrategy.Skip, Now),
            vault, Now);

        var again = ImportPipeline.Preview(table, IPhoneRecoveryPreset.WebLogins(), vault, DedupeStrategy.KeepBoth, Now);
        again.NewCount.Should().Be(1);
        ImportPipeline.Commit(again, vault, Now).Added.Should().Be(1);

        vault.Root.AllEntries().Count(e => e.Title == "dup.com").Should().Be(2);
    }

    [Fact]
    public void Rows_with_no_useful_content_are_skipped()
    {
        var table = WebTable(",,,,,");
        var vault = Vault.CreateEmpty("t", Now);

        var preview = ImportPipeline.Preview(table, IPhoneRecoveryPreset.WebLogins(), vault, DedupeStrategy.Skip, Now);
        preview.SkippedCount.Should().Be(1);
    }

    [Fact]
    public void Wifi_preset_tags_and_groups_correctly()
    {
        var table = DelimitedText.Parse("wlan_name,passwort,geaendert\nHomeNet,hunter2hunter,2024010101");
        var vault = Vault.CreateEmpty("t", Now);

        var preview = ImportPipeline.Preview(table, IPhoneRecoveryPreset.Wifi(), vault, DedupeStrategy.Skip, Now);
        var e = preview.Rows[0].Proposed;

        e.Title.Should().Be("HomeNet");
        e.Password.Should().Be("hunter2hunter");
        e.Tags.Should().Contain("wifi");
        preview.Rows[0].TargetGroupPath.Should().Be("Import/iPhone/Wi-Fi");
    }
}
