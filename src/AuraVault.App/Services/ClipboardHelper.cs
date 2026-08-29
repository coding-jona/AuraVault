using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace AuraVault.App.Services;

/// <summary>Best-effort clipboard access from a view model (the real auto-clear service lands in P3).</summary>
public static class ClipboardHelper
{
    public static async Task SetTextAsync(string text)
    {
        if (GetClipboard() is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    private static IClipboard? GetClipboard()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            return TopLevel.GetTopLevel(window)?.Clipboard;
        }

        return null;
    }
}
