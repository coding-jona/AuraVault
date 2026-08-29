using System.Collections.ObjectModel;
using System.Linq;
using AuraVault.App.Services;
using AuraVault.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuraVault.App.ViewModels;

public partial class VaultViewModel : ObservableObject
{
    private readonly VaultService _vault;

    [ObservableProperty]
    private string _search = "";

    [ObservableProperty]
    private GroupNode? _selectedGroup;

    [ObservableProperty]
    private EntryRow? _selectedEntry;

    [ObservableProperty]
    private bool _passwordRevealed;

    public VaultViewModel(VaultService vault)
    {
        _vault = vault;
        Groups = new ObservableCollection<GroupNode>(BuildTree());
        SelectedGroup = Groups.FirstOrDefault();
        RefreshEntries();
    }

    public ObservableCollection<GroupNode> Groups { get; }

    public ObservableCollection<EntryRow> Entries { get; } = [];

    partial void OnSearchChanged(string value) => RefreshEntries();

    partial void OnSelectedGroupChanged(GroupNode? value) => RefreshEntries();

    partial void OnSelectedEntryChanged(EntryRow? value) => PasswordRevealed = false;

    private System.Collections.Generic.IEnumerable<GroupNode> BuildTree()
    {
        var root = _vault.Database!.Vault.Root;
        yield return new GroupNode("All entries", null);
        foreach (var g in root.Groups)
        {
            yield return GroupNode.From(g, depth: 0);
        }
    }

    private void RefreshEntries()
    {
        Entries.Clear();

        System.Collections.Generic.IEnumerable<(Entry Entry, string Group)> source;
        if (!string.IsNullOrWhiteSpace(Search))
        {
            source = _vault.Index.Search(Search, 200).Select(h => (h.Entry, h.Group.Name));
        }
        else if (SelectedGroup?.Group is { } g)
        {
            source = g.AllEntries().Select(e => (e, g.Name));
        }
        else
        {
            source = _vault.Database!.Vault.Root.AllGroups().SelectMany(gr => gr.Entries.Select(e => (e, gr.Name)));
        }

        foreach (var (entry, group) in source.Take(500))
        {
            Entries.Add(new EntryRow(entry, group));
        }

        SelectedEntry ??= Entries.FirstOrDefault();
    }

    [RelayCommand]
    private void ToggleReveal() => PasswordRevealed = !PasswordRevealed;

    [RelayCommand]
    private async System.Threading.Tasks.Task CopyPasswordAsync()
    {
        if (SelectedEntry is { } row)
        {
            await Services.ClipboardHelper.SetTextAsync(row.Entry.Password);
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task CopyUsernameAsync()
    {
        if (SelectedEntry is { } row)
        {
            await Services.ClipboardHelper.SetTextAsync(row.Entry.UserName);
        }
    }
}

/// <summary>A tree node wrapping a <see cref="Group"/> (or the synthetic "All entries" root).</summary>
public sealed class GroupNode(string name, Group? group)
{
    public string Name { get; } = name;

    public Group? Group { get; } = group;

    public ObservableCollection<GroupNode> Children { get; } = [];

    public int Count => Group?.AllEntries().Count() ?? 0;

    public static GroupNode From(Group g, int depth)
    {
        var node = new GroupNode(g.Name, g);
        foreach (var child in g.Groups)
        {
            node.Children.Add(From(child, depth + 1));
        }

        return node;
    }
}

/// <summary>A row in the entry list.</summary>
public sealed class EntryRow(Entry entry, string groupName)
{
    public Entry Entry { get; } = entry;

    public string GroupName { get; } = groupName;

    public string Title => Entry.Title.Length > 0 ? Entry.Title : "(untitled)";

    public string UserName => Entry.UserName;

    public string Url => Entry.Url;
}
