using AuraVault.Core.Generator;
using AwesomeAssertions;
using Xunit;

namespace AuraVault.Core.Tests.Generator;

public sealed class PasswordGeneratorTests
{
    [Fact]
    public void Respects_length_and_includes_every_enabled_class()
    {
        var profile = new CharacterProfile { Length = 40, RequireEachEnabledClass = true };

        for (int i = 0; i < 50; i++)
        {
            string pw = PasswordGenerator.Generate(profile);
            pw.Should().HaveLength(40);
            pw.Should().MatchRegex("[a-z]").And.MatchRegex("[A-Z]").And.MatchRegex("[0-9]");
            pw.Any(c => "!#$%&*+-=?@^_~".Contains(c)).Should().BeTrue();
        }
    }

    [Fact]
    public void ExcludeLookAlike_removes_ambiguous_characters()
    {
        var profile = new CharacterProfile { Length = 200, ExcludeLookAlike = true };
        string pw = PasswordGenerator.Generate(profile);
        pw.Should().NotContainAny("O", "0", "l", "I", "1", "|");
    }

    [Fact]
    public void Throws_when_length_cannot_fit_every_required_class()
    {
        var profile = new CharacterProfile { Length = 2, RequireEachEnabledClass = true };
        var act = () => PasswordGenerator.Generate(profile);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Passphrase_has_the_requested_shape()
    {
        var profile = new PassphraseProfile { WordCount = 6, Separator = ".", AppendDigits = 3, Casing = PassphraseCasing.Title };
        string phrase = PasswordGenerator.GeneratePassphrase(profile);

        var parts = phrase.Split('.');
        parts.Should().HaveCount(7); // 6 words + a digit group
        parts.Take(6).Should().OnlyContain(p => char.IsUpper(p[0]));
        parts[6].Should().MatchRegex("^[0-9]{3}$");
    }

    [Fact]
    public void Passphrase_entropy_increases_with_word_count()
    {
        int list = EffLargeWordList.Instance.Count;
        list.Should().Be(7776);
        double five = EntropyEstimator.PassphraseBits(5, list);
        double six = EntropyEstimator.PassphraseBits(6, list);
        six.Should().BeGreaterThan(five);
        five.Should().BeApproximately(64.6, 0.5);
    }

    [Fact]
    public void Pool_entropy_penalises_repeats_and_runs()
    {
        double random = EntropyEstimator.PoolBits("gT7q-Xp2!aZ");
        double patterned = EntropyEstimator.PoolBits("aaaaaaaaaaa");
        random.Should().BeGreaterThan(patterned);
    }
}
