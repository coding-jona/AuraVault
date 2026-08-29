using System;
using System.IO;
using AuraVault.App.Commands;
using AuraVault.App.Localization;
using AuraVault.App.Services;
using AuraVault.App.Settings;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuraVault.App.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly VaultService _vault;
    private readonly SettingsService _settings;
    private readonly ThemeService _theme;
    private readonly CommandRegistry _commands;

    [ObservableProperty]
    private ObservableObject? _currentPage;

    [ObservableProperty]
    private bool _isPaletteOpen;

    [ObservableProperty]
    private string _statusText = "";

    public ShellViewModel(VaultService vault, SettingsService settings, ThemeService theme, CommandRegistry commands)
    {
        _vault = vault;
        _settings = settings;
        _theme = theme;
        _commands = commands;

        // VaultService events can fire from a background thread (open/create runs off the UI thread);
        // marshal anything that touches bound state or Avalonia resources back onto the UI thread.
        _vault.Opened += (_, _) => OnUi(ShowVault);
        _vault.Closed += (_, _) => OnUi(ShowUnlock);
        _vault.Saved += (_, _) => OnUi(() =>
            StatusText = $"Saved {Path.GetFileName(_vault.Path)}  ·  {DateTime.Now:HH:mm}");

        RegisterCommands();
        Palette = new CommandPaletteViewModel(_commands, () => IsPaletteOpen = false);
    }

    private static void OnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }

    public CommandRegistry Commands => _commands;

    public ThemeService Theme => _theme;

    public SettingsService Settings => _settings;

    public CommandPaletteViewModel Palette { get; private set; } = default!;

    // Localized labels for the menu bar (rebuilt on language change).
    public string MenuFile => Loc.T("menu.file");

    public string MenuView => Loc.T("menu.view");

    public string MenuTools => Loc.T("menu.tools");

    public string MenuHelp => Loc.T("menu.help");

    public string CmdSave => Loc.T("cmd.save");

    public string CmdLock => Loc.T("cmd.lock");

    public string CmdPalette => Loc.T("cmd.palette");

    public string CmdPrefs => Loc.T("cmd.prefs");

    public string CmdGenerate => Loc.T("cmd.generate");

    public string CmdImport => Loc.T("cmd.import");

    public string CmdQuit => Loc.T("cmd.quit");

    public void Start()
    {
        string? last = _settings.Current.General.LastVaultPath;
        bool exists = !string.IsNullOrEmpty(last) && File.Exists(last);
        CurrentPage = new UnlockViewModel(_vault, _settings, last, startInCreateMode: !exists);
    }

    private void ShowVault()
    {
        _settings.Current.General.LastVaultPath = _vault.Path;
        _settings.Save();
        CurrentPage = new VaultViewModel(_vault);
        StatusText = $"{_vault.Database!.Vault.Root.AllEntries().Count()} {Loc.T("vault.entries")}";
    }

    private void ShowUnlock()
    {
        CurrentPage = new UnlockViewModel(_vault, _settings, _vault.Path ?? _settings.Current.General.LastVaultPath, startInCreateMode: false);
        StatusText = Loc.T("vault.locked");
    }

    [RelayCommand]
    private void TogglePalette()
    {
        IsPaletteOpen = !IsPaletteOpen;
        if (IsPaletteOpen)
        {
            Palette.Reset();
        }
    }

    [RelayCommand]
    private void RunCommand(string id)
    {
        var command = _commands.ById(id);
        if (command is not null && command.CanExecute())
        {
            command.Execute();
        }
    }

    private void RegisterCommands()
    {
        _commands.Add(new AppCommand("vault.save", Loc.T("cmd.save"), Loc.T("menu.file"),
            execute: () => { if (_vault.IsOpen) { _vault.Save(); } },
            canExecute: () => _vault.IsOpen, gesture: "Ctrl+S", keywords: "write persist"));

        _commands.Add(new AppCommand("vault.lock", Loc.T("cmd.lock"), Loc.T("menu.file"),
            execute: () => { if (_vault.IsOpen) { _vault.Close(); } },
            canExecute: () => _vault.IsOpen, gesture: "Ctrl+L", keywords: "close secure"));

        _commands.Add(new AppCommand("app.palette", Loc.T("cmd.palette"), Loc.T("menu.view"),
            execute: TogglePalette, gesture: "Ctrl+K", keywords: "command find action"));

        _commands.Add(new AppCommand("app.prefs", Loc.T("cmd.prefs"), Loc.T("menu.tools"),
            execute: OpenPreferences, gesture: "Ctrl+,", keywords: "settings options theme aura"));

        _commands.Add(new AppCommand("app.generate", Loc.T("cmd.generate"), Loc.T("menu.tools"),
            execute: OpenGenerator, gesture: "Ctrl+G", keywords: "password diceware passphrase"));

        _commands.Add(new AppCommand("app.import", Loc.T("cmd.import"), Loc.T("menu.file"),
            execute: OpenImport, canExecute: () => _vault.IsOpen, gesture: "Ctrl+I", keywords: "csv iphone migrate"));

        _commands.Add(new AppCommand("app.quit", Loc.T("cmd.quit"), Loc.T("menu.file"),
            execute: () => Environment.Exit(0), gesture: "Alt+F4", keywords: "exit"));
    }

    private void OpenPreferences()
    {
        var window = new Views.PreferencesWindow { DataContext = new PreferencesViewModel(_settings, _theme) };
        window.Show();
    }

    private void OpenGenerator()
    {
        var window = new Views.GeneratorWindow { DataContext = new GeneratorViewModel() };
        window.Show();
    }

    private void OpenImport()
    {
        if (!_vault.IsOpen)
        {
            return;
        }

        var window = new Views.ImportWizardWindow { DataContext = new ImportWizardViewModel(_vault) };
        if (Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
        {
            window.ShowDialog(owner);
        }
        else
        {
            window.Show();
        }
    }
}
