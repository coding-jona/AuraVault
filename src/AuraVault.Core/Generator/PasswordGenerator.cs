using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AuraVault.Core.Generator;

/// <summary>Cryptographically uniform password and passphrase generation.</summary>
public static class PasswordGenerator
{
    private const string Lower = "abcdefghijklmnopqrstuvwxyz";
    private const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Digits = "23456789";
    private const string DigitsFull = "0123456789";
    private const string Symbols = "!#$%&*+-=?@^_~";
    private const string LookAlike = "O0oIl1|`'\";:,.{}[]()/\\";

    public static string Generate(CharacterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentOutOfRangeException.ThrowIfLessThan(profile.Length, 1);

        var classes = BuildClasses(profile);
        if (classes.Count == 0)
        {
            throw new InvalidOperationException("At least one character class must be enabled and non-empty.");
        }

        if (profile.RequireEachEnabledClass && profile.Length < classes.Count)
        {
            throw new InvalidOperationException(
                $"Length {profile.Length} is too short to include one character from each of the {classes.Count} enabled classes.");
        }

        string fullPool = string.Concat(classes);
        var result = new char[profile.Length];
        int filled = 0;

        if (profile.RequireEachEnabledClass)
        {
            foreach (string cls in classes)
            {
                result[filled++] = cls[RandomNumberGenerator.GetInt32(cls.Length)];
            }
        }

        for (; filled < profile.Length; filled++)
        {
            result[filled] = fullPool[RandomNumberGenerator.GetInt32(fullPool.Length)];
        }

        Shuffle(result);
        return new string(result);
    }

    public static string GeneratePassphrase(PassphraseProfile profile, IWordList? wordList = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentOutOfRangeException.ThrowIfLessThan(profile.WordCount, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(profile.AppendDigits);

        IWordList words = wordList ?? EffLargeWordList.Instance;
        if (words.Count < 2)
        {
            throw new InvalidOperationException("Word list is empty.");
        }

        var parts = new List<string>(profile.WordCount + 1);
        for (int i = 0; i < profile.WordCount; i++)
        {
            string word = words[RandomNumberGenerator.GetInt32(words.Count)];
            parts.Add(profile.Casing switch
            {
                PassphraseCasing.Upper => word.ToUpperInvariant(),
                PassphraseCasing.Title => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word),
                _ => word.ToLowerInvariant(),
            });
        }

        string phrase = string.Join(profile.Separator, parts);

        if (profile.AppendDigits > 0)
        {
            var sb = new StringBuilder(phrase);
            sb.Append(profile.Separator);
            for (int i = 0; i < profile.AppendDigits; i++)
            {
                sb.Append((char)('0' + RandomNumberGenerator.GetInt32(10)));
            }

            phrase = sb.ToString();
        }

        return phrase;
    }

    private static List<string> BuildClasses(CharacterProfile p)
    {
        var exclude = new HashSet<char>(p.ExcludeCharacters);
        if (p.ExcludeLookAlike)
        {
            exclude.UnionWith(LookAlike);
        }

        var classes = new List<string>(4);
        AddClass(p.Lowercase, Lower);
        AddClass(p.Uppercase, Upper);
        AddClass(p.Digits, p.ExcludeLookAlike ? Digits : DigitsFull);
        AddClass(p.Symbols, Symbols);
        return classes;

        void AddClass(bool enabled, string source)
        {
            if (!enabled)
            {
                return;
            }

            string filtered = new(source.Where(c => !exclude.Contains(c)).ToArray());
            if (filtered.Length > 0)
            {
                classes.Add(filtered);
            }
        }
    }

    private static void Shuffle(Span<char> span)
    {
        for (int i = span.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (span[i], span[j]) = (span[j], span[i]);
        }
    }
}
