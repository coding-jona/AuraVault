using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AuraVault.Platform;
using Serilog;

namespace AuraVault.App.Settings;

/// <summary>Loads/saves <see cref="AppSettings"/> as JSON and notifies when it changes.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _path;

    public SettingsService(IAppPaths paths)
    {
        _path = Path.Combine(paths.ConfigDirectory, "settings.json");
        Current = Load();
    }

    public AppSettings Current { get; private set; }

    /// <summary>Raised after <see cref="Save"/> so live consumers (theme, aura) can re-apply.</summary>
    public event EventHandler? Changed;

    public void Save()
    {
        try
        {
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Current, JsonOptions));
            if (File.Exists(_path))
            {
                File.Replace(tmp, _path, null);
            }
            else
            {
                File.Move(tmp, _path);
            }
        }
        catch (IOException ex)
        {
            Log.Warning(ex, "Could not persist settings to {Path}", _path);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions) ?? new AppSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            Log.Warning(ex, "settings.json unreadable; starting from defaults.");
        }

        return new AppSettings();
    }
}
