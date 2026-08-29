using System;
using System.Collections.ObjectModel;
using System.Linq;
using AuraVault.App.Commands;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuraVault.App.ViewModels;

public partial class CommandPaletteViewModel : ObservableObject
{
    private readonly CommandRegistry _registry;
    private readonly Action _close;

    [ObservableProperty]
    private string _query = "";

    [ObservableProperty]
    private AppCommand? _selected;

    public CommandPaletteViewModel(CommandRegistry registry, Action close)
    {
        _registry = registry;
        _close = close;
        Reset();
    }

    public ObservableCollection<AppCommand> Results { get; } = [];

    public void Reset()
    {
        Query = string.Empty;
        Populate();
    }

    partial void OnQueryChanged(string value) => Populate();

    private void Populate()
    {
        Results.Clear();
        foreach (var c in _registry.Search(Query).Take(30))
        {
            Results.Add(c);
        }

        Selected = Results.FirstOrDefault();
    }

    [RelayCommand]
    private void Run(AppCommand? command)
    {
        command ??= Selected;
        _close();
        if (command is not null && command.CanExecute())
        {
            command.Execute();
        }
    }
}
