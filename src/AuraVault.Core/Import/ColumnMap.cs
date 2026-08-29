using System.Globalization;

namespace AuraVault.Core.Import;

/// <summary>Where a source column's value goes in the target <see cref="Model.Entry"/>.</summary>
public enum TargetField
{
    Ignore,
    Title,
    UserName,
    Password,
    Url,
    Notes,
    Tags,
    GroupPath,
    Created,
    Modified,
    Totp,
    CustomField,
}

/// <summary>Optional per-column value transform.</summary>
public enum ColumnTransform
{
    None,

    /// <summary>Strip scheme/path/<c>www.</c> down to a registrable-ish domain (for a title).</summary>
    DomainToTitle,

    /// <summary>Prefix <c>https://</c> if the value has no scheme.</summary>
    EnsureUrlScheme,

    /// <summary>Parse a timestamp using the map's format/culture.</summary>
    ParseTimestamp,

    /// <summary>Split on <c>; , /</c> into multiple tags.</summary>
    SplitTags,
}

/// <summary>One source-column → target-field rule.</summary>
public sealed record ColumnMapping(
    string SourceColumn,
    TargetField Target,
    ColumnTransform Transform = ColumnTransform.None,
    string? CustomFieldName = null,
    bool Protected = false);

/// <summary>A complete mapping from a tabular source to entries.</summary>
public sealed class ColumnMap
{
    public List<ColumnMapping> Mappings { get; } = [];

    /// <summary>Group every imported row lands in (unless a column maps to <see cref="TargetField.GroupPath"/>).</summary>
    public string ConstantGroupPath { get; set; } = "Import";

    public List<string> ConstantTags { get; } = [];

    /// <summary>Explicit timestamp format, or <c>null</c> to try a set of common ones.</summary>
    public string? TimestampFormat { get; set; }

    public CultureInfo Culture { get; set; } = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>If the title ends up empty, fall back to the value of this source column.</summary>
    public string? TitleFallbackColumn { get; set; }

    /// <summary>Rows land in the vault Recycle Bin instead of the active tree.</summary>
    public bool IntoRecycleBin { get; set; }

    /// <summary>Entries carry passwords (false = metadata-only reference rows, e.g. token dumps).</summary>
    public bool CarriesSecrets { get; set; } = true;

    public ColumnMap Add(string column, TargetField target, ColumnTransform transform = ColumnTransform.None, string? customName = null, bool @protected = false)
    {
        Mappings.Add(new ColumnMapping(column, target, transform, customName, @protected));
        return this;
    }

    private static readonly string[] TimestampFallbacks =
    [
        "yyyy-MM-dd", "dd.MM.yyyy", "MM/dd/yyyy", "yyyy-MM-ddTHH:mm:ssZ", "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss", "dd.MM.yyyy HH:mm:ss", "yyyyMMddHHmmss", "o",
    ];

    public bool TryParseTimestamp(string raw, out DateTimeOffset value)
    {
        raw = raw.Trim();
        value = default;
        if (raw.Length == 0)
        {
            return false;
        }

        // Compact digits like "2025092016" (yyyyMMddHH) seen in the iPhone export.
        if (raw.Length is >= 8 and <= 14 && raw.All(char.IsAsciiDigit))
        {
            string padded = raw.PadRight(14, '0')[..14];
            if (DateTimeOffset.TryParseExact(padded, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value))
            {
                return true;
            }
        }

        if (TimestampFormat is not null &&
            DateTimeOffset.TryParseExact(raw, TimestampFormat, Culture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value))
        {
            return true;
        }

        foreach (string fmt in TimestampFallbacks)
        {
            if (DateTimeOffset.TryParseExact(raw, fmt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value))
            {
                return true;
            }
        }

        return DateTimeOffset.TryParse(raw, Culture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value);
    }
}
