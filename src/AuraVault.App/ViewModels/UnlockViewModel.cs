using System;
using System.IO;
using AuraVault.App.Localization;
using AuraVault.App.Services;
using AuraVault.App.Settings;
using AuraVault.Core.Cryptography;
using AuraVault.Core.Kdbx;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuraVault.App.ViewModels;

public partial class UnlockViewModel : ObservableObject
{
    private readonly VaultService _vault;
    private readonly SettingsService _settings;

    [ObservableProperty]
    private string _vaultPath;

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _confirmPassword = "";

    [ObservableProperty]
    private string _vaultName = "Personal";

    [ObservableProperty]
    private bool _createMode;

    [ObservableProperty]
    private string? _error;

    [ObservableProperty]
    private bool _busy;

    public UnlockViewModel(VaultService vault, SettingsService settings, string? path, bool startInCreateMode)
    {
        _vault = vault;
        _settings = settings;
        _vaultPath = path ?? Path.Combine(
            settings.Current.General.LastVaultPath is { } p ? Path.GetDirectoryName(p)! : Environment.CurrentDirectory,
            "Personal.kdbx");
        _createMode = startInCreateMode || !File.Exists(_vaultPath);
    }

    public string Title => CreateMode ? Loc.T("create.title") : Loc.T("unlock.title");

    public string SubmitLabel => CreateMode ? Loc.T("create.button") : Loc.T("unlock.button");

    public string ToggleLabel => CreateMode ? Loc.T("unlock.button") + " ›" : Loc.T("unlock.create");

    partial void OnCreateModeChanged(bool value)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SubmitLabel));
        OnPropertyChanged(nameof(ToggleLabel));
    }

    [RelayCommand]
    private void ToggleMode()
    {
        CreateMode = !CreateMode;
        Error = null;
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task SubmitAsync()
    {
        Error = null;
        if (string.IsNullOrEmpty(Password))
        {
            Error = Loc.T(CreateMode ? "create.password" : "unlock.password");
            return;
        }

        if (CreateMode && !string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            Error = Loc.T("create.mismatch");
            return;
        }

        Busy = true;
        try
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                var key = new CompositeKey().AddPassword(Password);
                if (CreateMode)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(VaultPath))!);
                    _vault.Create(VaultPath, VaultName, key);
                }
                else
                {
                    _vault.Open(VaultPath, key);
                }
            });

            Password = string.Empty;
            ConfirmPassword = string.Empty;
        }
        catch (KdbxIntegrityException)
        {
            Error = Loc.T("unlock.wrong");
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Vault {Mode} failed for {Path}", CreateMode ? "create" : "open", VaultPath);
            Error = ex.Message;
        }
        finally
        {
            Busy = false;
        }
    }
}
