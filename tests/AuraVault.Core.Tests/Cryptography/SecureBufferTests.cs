using System.Reflection;
using AuraVault.Core.Cryptography;
using AwesomeAssertions;
using Xunit;

namespace AuraVault.Core.Tests.Cryptography;

public sealed class SecureBufferTests
{
    [Fact]
    public void Dispose_zeroes_the_backing_array()
    {
        var buffer = new SecureBuffer(64);
        buffer.AsSpan().Fill(0xAB);

        byte[] backing = (byte[])typeof(SecureBuffer)
            .GetField("_data", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(buffer)!;

        backing.Should().OnlyContain(b => b == 0xAB);
        buffer.Dispose();
        backing.Should().OnlyContain(b => b == 0);
    }

    [Fact]
    public void Access_after_dispose_throws()
    {
        var buffer = new SecureBuffer(8);
        buffer.Dispose();

        Action act = () => buffer.AsSpan();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Double_dispose_is_safe()
    {
        var buffer = new SecureBuffer(8);
        buffer.Dispose();
        var act = buffer.Dispose;
        act.Should().NotThrow();
    }

    [Fact]
    public void TakeOwnershipOf_copies_then_zeroes_the_source()
    {
        byte[] source = [1, 2, 3, 4, 5, 6, 7, 8];
        using var buffer = SecureBuffer.TakeOwnershipOf(source);

        buffer.AsReadOnlySpan().ToArray().Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
        source.Should().OnlyContain(b => b == 0);
    }
}
