using System;

namespace AuraVault.App.Settings;

public enum ThemeMode
{
    System,
    Light,
    Dark,
    Amoled,
}

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;

    public AppearanceSettings Appearance { get; set; } = new();

    public AuraSettings Aura { get; set; } = new();

    public SecuritySettings Security { get; set; } = new();

    public GeneralSettings General { get; set; } = new();
}

public sealed class AppearanceSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.Dark;

    /// <summary>Accent colour as <c>#RRGGBB</c>.</summary>
    public string AccentColor { get; set; } = "#7C5CFF";
}

public sealed class AuraSettings
{
    /// <summary>0 = flat theme (no aura layer), 1 = full.</summary>
    public double Intensity { get; set; } = 0.7;

    public bool EnableAnimatedBackground { get; set; } = true;

    public bool EnableGlass { get; set; } = true;

    /// <summary>0.25 – 2.0 multiplier on all animation durations.</summary>
    public double AnimationSpeed { get; set; } = 1.0;

    public bool ReducedMotion { get; set; }
}

public sealed class SecuritySettings
{
    public int AutoLockAfterMinutes { get; set; } = 10;

    public int ClipboardClearSeconds { get; set; } = 12;

    public bool LockOnSessionLock { get; set; } = true;

    public bool LockOnMinimize { get; set; }
}

public sealed class GeneralSettings
{
    public string? LastVaultPath { get; set; }

    public string Language { get; set; } = "system"; // "system" | "de" | "en"
}
