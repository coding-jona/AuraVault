using AuraVault.Core.Kdbx;
using AwesomeAssertions;
using Xunit;

namespace AuraVault.Core.Tests.Kdbx;

public sealed class VariantDictionaryTests
{
    [Fact]
    public void Round_trips_every_supported_type_in_insertion_order()
    {
        var dict = new VariantDictionary();
        dict.SetUInt32("u32", 0xDEADBEEF);
        dict.SetUInt64("u64", 0x0102030405060708);
        dict.SetInt32("i32", -12345);
        dict.SetInt64("i64", -9_000_000_000L);
        dict.SetBool("flag", true);
        dict.SetString("str", "hällo · wörld");
        dict.SetByteArray("bytes", [1, 2, 3, 4, 250, 251]);

        byte[] serialized = dict.Serialize();
        var parsed = VariantDictionary.Parse(serialized);

        parsed.Keys.Should().Equal("u32", "u64", "i32", "i64", "flag", "str", "bytes");
        parsed.GetUInt32("u32").Should().Be(0xDEADBEEF);
        parsed.GetUInt64("u64").Should().Be(0x0102030405060708);
        parsed.GetInt32("i32").Should().Be(-12345);
        parsed.GetInt64("i64").Should().Be(-9_000_000_000L);
        parsed.GetBool("flag").Should().BeTrue();
        parsed.GetString("str").Should().Be("hällo · wörld");
        parsed.GetByteArray("bytes").Should().Equal(1, 2, 3, 4, 250, 251);
    }

    [Fact]
    public void Parsing_an_unterminated_dictionary_throws()
    {
        // version 0x0100, then a bogus dangling type byte.
        byte[] bad = [0x00, 0x01, 0x04];
        var act = () => VariantDictionary.Parse(bad);
        act.Should().Throw<KdbxFormatException>();
    }
}
