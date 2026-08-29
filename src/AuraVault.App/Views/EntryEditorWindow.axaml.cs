using AuraVault.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AuraVault.App.Views;

public partial class EntryEditorWindow : Window
{
    public EntryEditorWindow()
    {
        InitializeComponent();
        this.FindControl<Button>("CancelButton")!.Click += (_, _) => Close(false);
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        // SaveCommand has already run via the button's Command; close if it took.
        if (DataContext is EntryEditorViewModel { Saved: true })
        {
            Close(true);
        }
    }
}
