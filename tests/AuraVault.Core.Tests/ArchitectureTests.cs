using System.Reflection;
using AuraVault.Core.Kdbx;
using AwesomeAssertions;
using NetArchTest.Rules;
using Xunit;

namespace AuraVault.Core.Tests;

/// <summary>Guards the dependency rule from the plan: <c>Core</c> stays free of UI and OS frameworks.</summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly CoreAssembly = typeof(Kdbx4Codec).Assembly;

    [Theory]
    [InlineData("Avalonia")]
    [InlineData("Microsoft.UI")]
    [InlineData("Windows.UI")]
    [InlineData("PresentationFramework")]
    [InlineData("System.Windows.Forms")]
    [InlineData("Microsoft.Maui")]
    public void Core_does_not_depend_on(string forbiddenNamespacePrefix)
    {
        var result = Types.InAssembly(CoreAssembly)
            .That()
            .HaveDependencyOn(forbiddenNamespacePrefix)
            .GetTypes();

        result.Should().BeEmpty($"AuraVault.Core must not reference '{forbiddenNamespacePrefix}'.");
    }

    [Fact]
    public void Core_only_references_allowed_assemblies()
    {
        string[] allowedPrefixes =
        [
            "System", "Microsoft.Win32", "netstandard", "mscorlib",
            "AuraVault.Core",
            "Konscious.Security.Cryptography",
            "BouncyCastle",
        ];

        var referenced = CoreAssembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);

        referenced.Should().OnlyContain(
            name => allowedPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)),
            "Core's reference set is deliberately minimal.");
    }
}
