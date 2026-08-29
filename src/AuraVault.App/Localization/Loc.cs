using System;
using System.Collections.Generic;
using System.Globalization;

namespace AuraVault.App.Localization;

/// <summary>Minimal runtime-switchable DE/EN strings. Grows into resx if it gets large.</summary>
public static class Loc
{
    private static Dictionary<string, string> _current;

    static Loc() => _current = En;

    public static event EventHandler? LanguageChanged;

    public static string Language { get; private set; } = "en";

    public static void SetLanguage(string language)
    {
        language = language switch
        {
            "de" or "en" => language,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? "de" : "en",
        };

        Language = language;
        _current = language == "de" ? De : En;
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string T(string key) => _current.TryGetValue(key, out var v) ? v : key;

    private static readonly Dictionary<string, string> En = new(StringComparer.Ordinal)
    {
        ["app.title"] = "AuraVault",
        ["unlock.title"] = "Unlock your vault",
        ["unlock.password"] = "Master password",
        ["unlock.button"] = "Unlock",
        ["unlock.open"] = "Open another vault…",
        ["unlock.create"] = "Create a new vault…",
        ["unlock.wrong"] = "Wrong master password.",
        ["create.title"] = "Create a new vault",
        ["create.name"] = "Vault name",
        ["create.password"] = "Master password",
        ["create.repeat"] = "Repeat password",
        ["create.button"] = "Create vault",
        ["create.mismatch"] = "Passwords do not match.",
        ["vault.search"] = "Search…  (Ctrl+F)",
        ["vault.entries"] = "entries",
        ["vault.username"] = "Username",
        ["vault.password"] = "Password",
        ["vault.url"] = "URL",
        ["vault.notes"] = "Notes",
        ["vault.copy"] = "Copy",
        ["vault.reveal"] = "Reveal",
        ["vault.locked"] = "Locked",
        ["menu.file"] = "File",
        ["menu.edit"] = "Edit",
        ["menu.view"] = "View",
        ["menu.tools"] = "Tools",
        ["menu.help"] = "Help",
        ["cmd.lock"] = "Lock vault",
        ["cmd.save"] = "Save vault",
        ["cmd.palette"] = "Command palette",
        ["cmd.prefs"] = "Preferences",
        ["cmd.generate"] = "Password generator",
        ["cmd.import"] = "Import from CSV…",
        ["cmd.quit"] = "Quit",
        ["prefs.title"] = "Preferences",
        ["prefs.appearance"] = "Appearance",
        ["prefs.aura"] = "Aura",
        ["prefs.theme"] = "Theme",
        ["prefs.accent"] = "Accent colour",
        ["prefs.intensity"] = "Aura intensity",
        ["prefs.animated"] = "Animated background",
        ["prefs.glass"] = "Glass surfaces",
        ["prefs.speed"] = "Animation speed",
        ["prefs.reduced"] = "Reduced motion",
    };

    private static readonly Dictionary<string, string> De = new(StringComparer.Ordinal)
    {
        ["app.title"] = "AuraVault",
        ["unlock.title"] = "Tresor entsperren",
        ["unlock.password"] = "Master-Passwort",
        ["unlock.button"] = "Entsperren",
        ["unlock.open"] = "Anderen Tresor öffnen…",
        ["unlock.create"] = "Neuen Tresor anlegen…",
        ["unlock.wrong"] = "Falsches Master-Passwort.",
        ["create.title"] = "Neuen Tresor anlegen",
        ["create.name"] = "Tresor-Name",
        ["create.password"] = "Master-Passwort",
        ["create.repeat"] = "Passwort wiederholen",
        ["create.button"] = "Tresor anlegen",
        ["create.mismatch"] = "Passwörter stimmen nicht überein.",
        ["vault.search"] = "Suchen…  (Strg+F)",
        ["vault.entries"] = "Einträge",
        ["vault.username"] = "Benutzername",
        ["vault.password"] = "Passwort",
        ["vault.url"] = "URL",
        ["vault.notes"] = "Notizen",
        ["vault.copy"] = "Kopieren",
        ["vault.reveal"] = "Anzeigen",
        ["vault.locked"] = "Gesperrt",
        ["menu.file"] = "Datei",
        ["menu.edit"] = "Bearbeiten",
        ["menu.view"] = "Ansicht",
        ["menu.tools"] = "Werkzeuge",
        ["menu.help"] = "Hilfe",
        ["cmd.lock"] = "Tresor sperren",
        ["cmd.save"] = "Tresor speichern",
        ["cmd.palette"] = "Befehlspalette",
        ["cmd.prefs"] = "Einstellungen",
        ["cmd.generate"] = "Passwort-Generator",
        ["cmd.import"] = "Aus CSV importieren…",
        ["cmd.quit"] = "Beenden",
        ["prefs.title"] = "Einstellungen",
        ["prefs.appearance"] = "Darstellung",
        ["prefs.aura"] = "Aura",
        ["prefs.theme"] = "Design",
        ["prefs.accent"] = "Akzentfarbe",
        ["prefs.intensity"] = "Aura-Intensität",
        ["prefs.animated"] = "Animierter Hintergrund",
        ["prefs.glass"] = "Glas-Oberflächen",
        ["prefs.speed"] = "Animationsgeschwindigkeit",
        ["prefs.reduced"] = "Reduzierte Bewegung",
    };
}
