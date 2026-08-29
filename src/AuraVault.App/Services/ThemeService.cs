using System;
using AuraVault.App.Aura;
using AuraVault.App.Settings;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace AuraVault.App.Services;

/// <summary>Maps <see cref="AppSettings"/> onto the live Avalonia theme, accent and motion state.</summary>
public sealed class ThemeService
{
    private readonly Application _app;
    private readonly SettingsService _settings;

    public ThemeService(Application app, SettingsService settings)
    {
        _app = app;
        _settings = settings;
        _settings.Changed += (_, _) => Apply();
    }

    public event EventHandler? Applied;

    public Color Accent { get; private set; } = Color.Parse("#7C5CFF");

    public Color Accent2 { get; private set; } = Color.Parse("#33D6C4");

    public Color BaseBackground { get; private set; } = Color.Parse("#0B0B10");

    public void Apply()
    {
        var appearance = _settings.Current.Appearance;
        var aura = _settings.Current.Aura;

        _app.RequestedThemeVariant = appearance.Theme switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark or ThemeMode.Amoled => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };

        if (Color.TryParse(appearance.AccentColor, out var accent))
        {
            Accent = accent;
            SetColor("Aura.Accent", accent);
            SetBrush("Aura.AccentBrush", accent);
        }

        bool amoled = appearance.Theme == ThemeMode.Amoled;
        BaseBackground = amoled ? Colors.Black : ReadColor("Aura.Bg0", Color.Parse("#0B0B10"));
        if (amoled)
        {
            SetColor("Aura.Bg0", Colors.Black);
            SetColor("Aura.Bg1", Color.Parse("#050506"));
            SetBrush("Aura.BgBrush", Colors.Black);
        }

        Accent2 = ReadColor("Aura.Accent2", Color.Parse("#33D6C4"));

        MotionScale.Update(aura.AnimationSpeed, aura.ReducedMotion || OsPrefersReducedMotion());
        Applied?.Invoke(this, EventArgs.Empty);
    }

    private static bool OsPrefersReducedMotion()
    {
        // Best-effort; a WinRT UISettings check is added with the platform layer in P3.
        return false;
    }

    private Color ReadColor(string key, Color fallback) =>
        _app.Resources.TryGetResource(key, _app.ActualThemeVariant, out var v) && v is Color c ? c : fallback;

    private void SetColor(string key, Color value) => _app.Resources[key] = value;

    private void SetBrush(string key, Color value) => _app.Resources[key] = new SolidColorBrush(value);
}
