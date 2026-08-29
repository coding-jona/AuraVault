using System;
using System.Collections.ObjectModel;
using System.Linq;
using AuraVault.App.Services;
using AuraVault.Core.Generator;
using AuraVault.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuraVault.App.ViewModels;

/// <summary>Editable row for a custom string field.</summary>
public partial class CustomFieldRow : ObservableObject
{
    [ObservableProperty]
    private string _key = "";

    [ObservableProperty]
    private string _value = "";

    [ObservableProperty]
    private bool _protectedField;
}

public partial class EntryEditorViewModel : ObservableObject
{
    private readonly VaultService _vault;
    private readonly Entry _entry;
    private readonly Group _group;
    private readonly bool _isNew;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _userName = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _url = "";

    [ObservableProperty]
    private string _notes = "";

    [ObservableProperty]
    private string _tags = "";

    [ObservableProperty]
    private bool _revealPassword;

    public char PasswordCharValue => RevealPassword ? '\0' : '●';

    partial void OnRevealPasswordChanged(bool value) => OnPropertyChanged(nameof(PasswordCharValue));

    /// <summary>Set to true by <see cref="Save"/> so the caller knows to refresh.</summary>
    public bool Saved { get; private set; }

    public EntryEditorViewModel(VaultService vault, Group group, Entry? entry)
    {
        _vault = vault;
        _group = group;
        _isNew = entry is null;
        _entry = entry ?? new Entry { Times = EntryTimes.CreatedNow(DateTimeOffset.UtcNow) };

        _title = _entry.Title;
        _userName = _entry.UserName;
        _password = _entry.Password;
        _url = _entry.Url;
        _notes = _entry.Notes;
        _tags = string.Join(", ", _entry.Tags);

        foreach (var (key, value) in _entry.Strings)
        {
            if (key is EntryFields.Title or EntryFields.UserName or EntryFields.Password or EntryFields.Url or EntryFields.Notes)
            {
                continue;
            }

            CustomFields.Add(new CustomFieldRow { Key = key, Value = value.Value, ProtectedField = value.IsProtected });
        }
    }

    public ObservableCollection<CustomFieldRow> CustomFields { get; } = [];

    public string HeaderText => _isNew ? "New entry" : "Edit entry";

    [RelayCommand]
    private void AddCustomField() => CustomFields.Add(new CustomFieldRow { Key = "Field", Value = "" });

    [RelayCommand]
    private void RemoveCustomField(CustomFieldRow row) => CustomFields.Remove(row);

    [RelayCommand]
    private void GeneratePassword() =>
        Password = PasswordGenerator.Generate(new CharacterProfile { Length = 20 });

    [RelayCommand]
    private void ToggleReveal() => RevealPassword = !RevealPassword;

    [RelayCommand]
    private void Save()
    {
        var now = DateTimeOffset.UtcNow;

        if (!_isNew)
        {
            _entry.History.Add(_entry.Clone(includeHistory: false));
        }

        _entry.Title = Title.Trim();
        _entry.UserName = UserName;
        _entry.Set(EntryFields.Password, Password, protect: true);
        _entry.Url = Url.Trim();
        _entry.Notes = Notes;

        _entry.Tags.Clear();
        foreach (var tag in Tags.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct())
        {
            _entry.Tags.Add(tag);
        }

        // Reconcile custom fields.
        var keep = CustomFields
            .Where(f => !string.IsNullOrWhiteSpace(f.Key))
            .ToDictionary(f => f.Key.Trim(), f => new ProtectedString(f.Value, f.ProtectedField), StringComparer.Ordinal);

        foreach (var key in _entry.Strings.Keys
                     .Where(k => k is not (EntryFields.Title or EntryFields.UserName or EntryFields.Password or EntryFields.Url or EntryFields.Notes))
                     .ToList())
        {
            if (!keep.ContainsKey(key))
            {
                _entry.Strings.Remove(key);
            }
        }

        foreach (var (key, value) in keep)
        {
            _entry.Strings[key] = value;
        }

        _entry.Times.LastModificationTime = now;

        if (_isNew)
        {
            _entry.Times.CreationTime ??= now;
            _group.Entries.Add(_entry);
        }

        _vault.MarkDirty();
        Saved = true;
    }
}
