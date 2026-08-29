using AuraVault.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AuraVault.App.Views;

public partial class VaultView : UserControl
{
    public VaultView() => InitializeComponent();

    private void OnEntryDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (DataContext is VaultViewModel vm && vm.EditEntryCommand.CanExecute(null))
        {
            vm.EditEntryCommand.Execute(null);
        }
    }
}
