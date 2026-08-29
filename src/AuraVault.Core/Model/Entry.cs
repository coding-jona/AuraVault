namespace AuraVault.Core.Model;

/// <summary>Well-known KeePass string field keys.</summary>
public static class EntryFields
{
    public const string Title = "Title";
    public const string UserName = "UserName";
    public const string Password = "Password";
    public const string Url = "URL";
    public const string Notes = "Notes";
}

/// <summary>A credential record. Fields beyond the five well-known keys are custom fields.</summary>
public sealed class Entry
{
    public KdbxId Uuid { get; set; } = KdbxId.New();

    public int IconId { get; set; }

    public KdbxId? CustomIconUuid { get; set; }

    public string? ForegroundColor { get; set; }

    public string? BackgroundColor { get; set; }

    public string? OverrideUrl { get; set; }

    public string QualityCheck { get; set; } = "True";

    /// <summary>Comma/semicolon-free tag tokens (KeePass stores them joined by ';').</summary>
    public List<string> Tags { get; } = [];

    /// <summary>String fields, keyed by field name. Values may be protected.</summary>
    public Dictionary<string, ProtectedString> Strings { get; } = new(StringComparer.Ordinal);

    /// <summary>Attachment name -> index into <see cref="Vault.Binaries"/>.</summary>
    public Dictionary<string, int> Binaries { get; } = new(StringComparer.Ordinal);

    public EntryTimes Times { get; set; } = new();

    public AutoTypeConfig AutoType { get; set; } = new();

    /// <summary>Previous versions, oldest first. Snapshotted on every save.</summary>
    public List<Entry> History { get; } = [];

    public Dictionary<string, string> CustomData { get; } = new(StringComparer.Ordinal);

    public bool IsFavorite { get; set; }

    // ---- Convenience accessors over Strings ----

    public string Title
    {
        get => Get(EntryFields.Title);
        set => Set(EntryFields.Title, value, protect: false);
    }

    public string UserName
    {
        get => Get(EntryFields.UserName);
        set => Set(EntryFields.UserName, value, protect: false);
    }

    public string Password
    {
        get => Get(EntryFields.Password);
        set => Set(EntryFields.Password, value, protect: true);
    }

    public string Url
    {
        get => Get(EntryFields.Url);
        set => Set(EntryFields.Url, value, protect: false);
    }

    public string Notes
    {
        get => Get(EntryFields.Notes);
        set => Set(EntryFields.Notes, value, protect: false);
    }

    public string Get(string key) => Strings.TryGetValue(key, out var v) ? v.Value : string.Empty;

    public void Set(string key, string value, bool protect) => Strings[key] = new ProtectedString(value, protect);

    /// <summary>Deep copy, used for history snapshots and importer previews.</summary>
    public Entry Clone(bool includeHistory = true)
    {
        var copy = new Entry
        {
            Uuid = Uuid,
            IconId = IconId,
            CustomIconUuid = CustomIconUuid,
            ForegroundColor = ForegroundColor,
            BackgroundColor = BackgroundColor,
            OverrideUrl = OverrideUrl,
            QualityCheck = QualityCheck,
            Times = Times.Clone(),
            AutoType = AutoType.Clone(),
            IsFavorite = IsFavorite,
        };
        copy.Tags.AddRange(Tags);
        foreach (var (k, v) in Strings)
        {
            copy.Strings[k] = v;
        }

        foreach (var (k, v) in Binaries)
        {
            copy.Binaries[k] = v;
        }

        foreach (var (k, v) in CustomData)
        {
            copy.CustomData[k] = v;
        }

        if (includeHistory)
        {
            foreach (var h in History)
            {
                copy.History.Add(h.Clone(includeHistory: false));
            }
        }

        return copy;
    }
}
