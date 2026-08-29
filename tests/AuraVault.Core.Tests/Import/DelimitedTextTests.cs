using AuraVault.Core.Import;
using AwesomeAssertions;
using Xunit;

namespace AuraVault.Core.Tests.Import;

public sealed class DelimitedTextTests
{
    [Fact]
    public void Handles_quotes_embedded_delimiters_and_newlines()
    {
        string csv = "a,b,c\n\"x,y\",\"line1\nline2\",\"he said \"\"hi\"\"\"\n";
        var table = DelimitedText.Parse(csv);

        table.Headers.Should().Equal("a", "b", "c");
        table.Rows.Should().ContainSingle();
        table.Rows[0]["a"].Should().Be("x,y");
        table.Rows[0]["b"].Should().Be("line1\nline2");
        table.Rows[0]["c"].Should().Be("he said \"hi\"");
    }

    [Fact]
    public void Auto_detects_a_semicolon_delimiter()
    {
        var table = DelimitedText.Parse("name;value\nfoo;bar\n");
        table.Delimiter.Should().Be(';');
        table.Rows[0]["value"].Should().Be("bar");
    }

    [Fact]
    public void Strips_a_utf8_bom()
    {
        var table = DelimitedText.Parse("﻿col\nv\n");
        table.Headers.Should().Equal("col");
        table.Rows[0]["col"].Should().Be("v");
    }

    [Fact]
    public void Skips_a_blank_trailing_line()
    {
        var table = DelimitedText.Parse("h\n1\n2\n\n");
        table.Rows.Should().HaveCount(2);
    }
}
