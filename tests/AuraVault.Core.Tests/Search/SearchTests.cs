using AuraVault.Core.Model;
using AuraVault.Core.Search;
using AwesomeAssertions;
using Xunit;

namespace AuraVault.Core.Tests.Search;

public sealed class SearchTests
{
    [Fact]
    public void Fuzzy_matches_a_subsequence_and_rejects_a_non_subsequence()
    {
        FuzzyMatcher.Score("gh", "GitHub").Should().NotBeNull();
        FuzzyMatcher.Score("xyz", "GitHub").Should().BeNull();
    }

    [Fact]
    public void Fuzzy_prefers_prefix_and_exact_matches()
    {
        double prefix = FuzzyMatcher.Score("git", "github.com")!.Value;
        double middle = FuzzyMatcher.Score("hub", "github.com")!.Value;
        prefix.Should().BeGreaterThan(middle);

        double exact = FuzzyMatcher.Score("github.com", "github.com")!.Value;
        exact.Should().BeGreaterThan(prefix);
    }

    private static Vault SampleVault()
    {
        var now = DateTimeOffset.UnixEpoch;
        var vault = Vault.CreateEmpty("t", now);
        var web = vault.Root.FindOrCreatePath(["Import", "Web"], now);
        var wifi = vault.Root.FindOrCreatePath(["Import", "Wi-Fi"], now);

        var gh = new Entry { Times = EntryTimes.CreatedNow(now) };
        gh.Title = "GitHub";
        gh.UserName = "octocat";
        gh.Url = "https://github.com";
        gh.Tags.Add("dev");
        web.Entries.Add(gh);

        var mail = new Entry { Times = EntryTimes.CreatedNow(now) };
        mail.Title = "Fastmail";
        mail.UserName = "alice@fastmail.com";
        mail.Url = "https://fastmail.com";
        web.Entries.Add(mail);

        var net = new Entry { Times = EntryTimes.CreatedNow(now) };
        net.Title = "HomeNet";
        net.Tags.Add("wifi");
        wifi.Entries.Add(net);

        return vault;
    }

    [Fact]
    public void Free_text_finds_by_title_username_and_url()
    {
        var index = new SearchIndex();
        index.Rebuild(SampleVault());

        index.Search("github").Select(h => h.Entry.Title).Should().Contain("GitHub");
        index.Search("octocat").Select(h => h.Entry.Title).Should().Contain("GitHub");
        index.Search("fastmail.com").Select(h => h.Entry.Title).Should().Contain("Fastmail");
    }

    [Fact]
    public void Tag_and_group_filters_narrow_results()
    {
        var index = new SearchIndex();
        index.Rebuild(SampleVault());

        index.Search("tag:wifi").Should().ContainSingle().Which.Entry.Title.Should().Be("HomeNet");
        index.Search("group:Web").Should().HaveCount(2);
        index.Search("group:Web github").Should().ContainSingle();
    }

    [Fact]
    public void Limit_is_respected()
    {
        var index = new SearchIndex();
        index.Rebuild(SampleVault());
        index.Search("i", limit: 1).Should().HaveCount(1);
    }
}
