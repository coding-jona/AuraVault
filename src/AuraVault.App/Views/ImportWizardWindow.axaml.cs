using Avalonia.Controls;

namespace AuraVault.App.Views;

public partial class ImportWizardWindow : Window
{
    public ImportWizardWindow()
    {
        InitializeComponent();
        this.FindControl<Button>("CloseButton")!.Click += (_, _) => Close();
    }
}
