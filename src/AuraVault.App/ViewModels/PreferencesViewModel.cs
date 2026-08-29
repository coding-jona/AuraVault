using AuraVault.App.Services;
using AuraVault.App.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuraVault.App.ViewModels;

public partial class PreferencesViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly ThemeService _theme;

    public PreferencesViewModel(SettingsService settings, ThemeService theme)
    {
        _settings = settings;
        _theme = theme;

        var a = settings.Current.Appearance;
        var au = settings.Current.Aura;
        _theme_ = (int)a.Theme;
        _accent = a.AccentColor;
        _intensity = au.Intensity;
        _animated = au.EnableAnimatedBackground;
        _glass = au.EnableGlass;
        _speed = au.AnimationSpeed;
        _reduced = au.ReducedMotion;
    }

    public string[] Themes { get; } = ["System", "Light", "Dark", "AMOLED"];

    [ObservableProperty]
    private int _theme_;

    [ObservableProperty]
    private string _accent;

    [ObservableProperty]
    private double _intensity;

    [ObservableProperty]
    private bool _animated;

    [ObservableProperty]
    private bool _glass;

    [ObservableProperty]
    private double _speed;

    [ObservableProperty]
    private bool _reduced;

    partial void OnTheme_Changed(int value) => Apply();

    partial void OnAccentChanged(string value) => Apply();

    partial void OnIntensityChanged(double value) => Apply();

    partial void OnAnimatedChanged(bool value) => Apply();

    partial void OnGlassChanged(bool value) => Apply();

    partial void OnSpeedChanged(double value) => Apply();

    partial void OnReducedChanged(bool value) => Apply();

    private void Apply()
    {
        var s = _settings.Current;
        s.Appearance.Theme = (ThemeMode)Theme_;
        s.Appearance.AccentColor = string.IsNullOrWhiteSpace(Accent) ? "#7C5CFF" : Accent;
        s.Aura.Intensity = Intensity;
        s.Aura.EnableAnimatedBackground = Animated;
        s.Aura.EnableGlass = Glass;
        s.Aura.AnimationSpeed = Speed;
        s.Aura.ReducedMotion = Reduced;
        _settings.Save(); // fires Changed -> ThemeService.Apply -> live update
    }
}
