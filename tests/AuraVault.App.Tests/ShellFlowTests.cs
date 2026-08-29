using System;
using System.IO;
using System.Threading.Tasks;
using AuraVault.App;
using AuraVault.App.Services;
using AuraVault.App.Settings;
using AuraVault.App.ViewModels;
using AuraVault.App.Views;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraVault.App.Tests;

public class ShellFlowTests
{
    private static T Service<T>() where T : notnull => App.Services.GetRequiredService<T>();

    [AvaloniaFact]
    public void App_starts_on_the_unlock_page()
    {
        var shell = Service<ShellViewModel>();
        shell.Start();
        shell.CurrentPage.Should().BeOfType<UnlockViewModel>();
    }

    [AvaloniaFact]
    public async Task Creating_a_vault_completes_without_crashing()
    {
        var shell = Service<ShellViewModel>();       // subscribes to VaultService events
        var vault = Service<VaultService>();
        var settings = Service<SettingsService>();

        if (vault.IsOpen)
        {
            vault.Close();
        }

        string path = Path.Combine(Path.GetTempPath(), $"av-headless-{Guid.NewGuid():N}.kdbx");
        var unlock = new UnlockViewModel(vault, settings, path, startInCreateMode: true)
        {
            VaultName = "Headless",
            Password = "headless-master-pw",
            ConfirmPassword = "headless-master-pw",
        };

        await unlock.SubmitCommand.ExecuteAsync(null);

        vault.IsOpen.Should().BeTrue();
        unlock.Error.Should().BeNull();
        shell.CurrentPage.Should().BeOfType<VaultViewModel>("the Opened event must switch the shell page on the UI thread");

        File.Delete(path);
        if (File.Exists(path + ".bak"))
        {
            File.Delete(path + ".bak");
        }
    }

    [AvaloniaTheory]
    [InlineData(0.0, false)]
    [InlineData(0.7, false)]
    [InlineData(1.0, false)]
    [InlineData(1.0, true)]
    public void MainWindow_renders_across_aura_settings(double intensity, bool reducedMotion)
    {
        var shell = Service<ShellViewModel>();
        var settings = Service<SettingsService>();
        settings.Current.Aura.Intensity = intensity;
        settings.Current.Aura.ReducedMotion = reducedMotion;
        Service<ThemeService>().Apply();

        var window = new MainWindow { DataContext = shell };
        window.Show();

        var act = () => window.CaptureRenderedFrame();
        act.Should().NotThrow();

        window.Close();
    }
}
