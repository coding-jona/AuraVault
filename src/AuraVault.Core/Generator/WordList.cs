using System.Reflection;
using System.Resources;

namespace AuraVault.Core.Generator;

/// <summary>An indexed list of words for diceware passphrase generation.</summary>
public interface IWordList
{
    int Count { get; }

    string this[int index] { get; }
}

/// <summary>The EFF "large" wordlist (7776 words), embedded as a resource. CC-BY 3.0 US, EFF.</summary>
public sealed class EffLargeWordList : IWordList
{
    private static readonly Lazy<string[]> Words = new(Load);

    public static EffLargeWordList Instance { get; } = new();

    public int Count => Words.Value.Length;

    public string this[int index] => Words.Value[index];

    private static string[] Load()
    {
        var assembly = typeof(EffLargeWordList).Assembly;
        const string name = "AuraVault.Core.Generator.Resources.wordlist-eff-large.txt";
        using Stream stream = assembly.GetManifestResourceStream(name)
            ?? throw new MissingManifestResourceException($"Embedded wordlist '{name}' not found.");
        using var reader = new StreamReader(stream);

        var list = new List<string>(7776);
        while (reader.ReadLine() is { } line)
        {
            string word = line.Trim();
            if (word.Length != 0)
            {
                list.Add(word);
            }
        }

        return [.. list];
    }
}
