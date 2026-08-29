namespace AuraVault.Core.Import;

/// <summary>
/// Built-in column mappings for the curated iPhone-keychain recovery CSVs
/// (<c>1_logins_web.csv</c>, <c>2_wlan.csv</c>, <c>3_app_passwoerter.csv</c>,
/// <c>4_tokens_und_system.csv</c>, <c>5_veraltet_geloescht.csv</c>).
/// Nothing here is compiled data — only the shape of those files.
/// </summary>
public static class IPhoneRecoveryPreset
{
    /// <summary>A recognised recovery file: how to spot it and how to map it.</summary>
    public sealed record PresetFile(string FileName, Func<ColumnMap> CreateMap, string Description);

    public static IReadOnlyList<PresetFile> Files { get; } =
    [
        new("1_logins_web.csv", WebLogins, "Website logins → Import/iPhone/Web"),
        new("2_wlan.csv", Wifi, "Wi-Fi networks → Import/iPhone/Wi-Fi"),
        new("3_app_passwoerter.csv", AppPasswords, "App accounts → Import/iPhone/Apps/<group>"),
        new("5_veraltet_geloescht.csv", DeletedLegacy, "Deleted/superseded entries → Recycle Bin"),
        new("4_tokens_und_system.csv", TokensReference, "Tokens/system (no secret) → Import/iPhone/Reference"),
    ];

    /// <summary>Finds the preset for a file path by its file name (case-insensitive).</summary>
    public static PresetFile? ForFile(string path)
    {
        string name = System.IO.Path.GetFileName(path);
        return Files.FirstOrDefault(f => string.Equals(f.FileName, name, StringComparison.OrdinalIgnoreCase));
    }

    // domain,benutzer,passwort,pfad,geaendert,erstellt
    public static ColumnMap WebLogins()
    {
        var map = new ColumnMap { ConstantGroupPath = "Import/iPhone/Web", TitleFallbackColumn = "domain" };
        map.Add("domain", TargetField.Title, ColumnTransform.DomainToTitle)
           .Add("domain", TargetField.Url, ColumnTransform.EnsureUrlScheme)
           .Add("benutzer", TargetField.UserName)
           .Add("passwort", TargetField.Password)
           .Add("pfad", TargetField.CustomField, customName: "Path")
           .Add("geaendert", TargetField.Modified)
           .Add("erstellt", TargetField.Created);
        return map;
    }

    // wlan_name,passwort,geaendert
    public static ColumnMap Wifi()
    {
        var map = new ColumnMap { ConstantGroupPath = "Import/iPhone/Wi-Fi" };
        map.ConstantTags.Add("wifi");
        map.Add("wlan_name", TargetField.Title)
           .Add("passwort", TargetField.Password)
           .Add("geaendert", TargetField.Modified);
        return map;
    }

    // dienst,benutzer,passwort,gruppe,geaendert
    public static ColumnMap AppPasswords()
    {
        var map = new ColumnMap { ConstantGroupPath = "Import/iPhone/Apps", TitleFallbackColumn = "dienst" };
        map.Add("dienst", TargetField.Title)
           .Add("benutzer", TargetField.UserName)
           .Add("passwort", TargetField.Password)
           .Add("gruppe", TargetField.GroupPath)
           .Add("gruppe", TargetField.Tags)
           .Add("geaendert", TargetField.Modified);
        return map;
    }

    // kategorie,dienst,benutzer,passwort,geloescht_am
    public static ColumnMap DeletedLegacy()
    {
        var map = new ColumnMap { ConstantGroupPath = "Recycle Bin", IntoRecycleBin = true, TitleFallbackColumn = "dienst" };
        map.ConstantTags.Add("legacy");
        map.Add("dienst", TargetField.Title)
           .Add("benutzer", TargetField.UserName)
           .Add("passwort", TargetField.Password)
           .Add("kategorie", TargetField.Tags)
           .Add("geloescht_am", TargetField.Modified);
        return map;
    }

    // kategorie,dienst,benutzer,gruppe,wert_laenge   (NO password column)
    public static ColumnMap TokensReference()
    {
        var map = new ColumnMap
        {
            ConstantGroupPath = "Import/iPhone/Reference",
            CarriesSecrets = false,
            TitleFallbackColumn = "dienst",
        };
        map.ConstantTags.Add("reference");
        map.Add("dienst", TargetField.Title)
           .Add("benutzer", TargetField.UserName)
           .Add("kategorie", TargetField.Tags)
           .Add("gruppe", TargetField.CustomField, customName: "Access Group")
           .Add("wert_laenge", TargetField.CustomField, customName: "Value Length")
           .Add("kategorie", TargetField.Notes);
        return map;
    }
}
