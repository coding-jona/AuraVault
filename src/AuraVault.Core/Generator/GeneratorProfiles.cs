namespace AuraVault.Core.Generator;

/// <summary>Letter casing applied to generated passphrases.</summary>
public enum PassphraseCasing
{
    Lower,
    Title,
    Upper,
}

/// <summary>Character-set password rules.</summary>
public sealed class CharacterProfile
{
    public int Length { get; set; } = 20;

    public bool Lowercase { get; set; } = true;

    public bool Uppercase { get; set; } = true;

    public bool Digits { get; set; } = true;

    public bool Symbols { get; set; } = true;

    /// <summary>Drop visually ambiguous characters (<c>O 0 o l I 1 | ` '</c> …).</summary>
    public bool ExcludeLookAlike { get; set; } = true;

    /// <summary>Additional characters to remove from the pool.</summary>
    public string ExcludeCharacters { get; set; } = string.Empty;

    /// <summary>Guarantee at least one character from every enabled class.</summary>
    public bool RequireEachEnabledClass { get; set; } = true;

    public static CharacterProfile Strong() => new();
}

/// <summary>Diceware-style passphrase rules.</summary>
public sealed class PassphraseProfile
{
    public int WordCount { get; set; } = 5;

    public string Separator { get; set; } = "-";

    public PassphraseCasing Casing { get; set; } = PassphraseCasing.Lower;

    /// <summary>Append this many random digits to the end (0 = none).</summary>
    public int AppendDigits { get; set; }

    public static PassphraseProfile Default() => new();
}
