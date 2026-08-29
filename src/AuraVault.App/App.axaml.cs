using System;
using AuraVault.App.Commands;
using AuraVault.App.Localization;
using AuraVault.App.Services;
using AuraVault.App.Settings;
using AuraVault.App.ViewModels;
using AuraVault.App.Views;
using AuraVault.Core.Cryptography;
using AuraVault.Platform;
using AuraVault.Platform.Windows;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;

namespace AuraVault.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = default!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<IAppPaths, WindowsAppPaths>();
        collection.AddSingleton<ISecureMemory, WindowsSecureMemory>();
        collection.AddSingleton<IIdleMonitor, WindowsIdleMonitor>();
        collection.AddSingleton<SettingsService>();
        collection.AddSingleton<VaultService>();
        collection.AddSingleton<CommandRegistry>();
        collection.AddSingleton<Application>(this);
        collection.AddSingleton<ThemeService>();
        collection.AddSingleton<ShellViewModel>();

        Services = collection.BuildServiceProvider();

        // Route secure buffers through VirtualLock.
        SecureBuffer.Locker = Services.GetRequiredService<ISecureMemory>();

        var settings = Services.GetRequiredService<SettingsService>();
        Loc.SetLanguage(settings.Current.General.Language);
        Services.GetRequiredService<ThemeService>().Apply();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var shell = Services.GetRequiredService<ShellViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = shell };
            shell.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
