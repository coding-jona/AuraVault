using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AuraVault.App.Services;
using AuraVault.Core.Import;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuraVault.App.ViewModels;

public partial class ImportFileRow : ObservableObject
{
    public required string FileName { get; init; }

    public required string Path { get; init; }

    public required Func<ColumnMap> CreateMap { get; init; }

    [ObservableProperty]
    private bool _include = true;

    [ObservableProperty]
    private int _new;

    [ObservableProperty]
    private int _duplicate;

    [ObservableProperty]
    private int _skipped;
}

public partial class ImportWizardViewModel : ObservableObject
{
    private readonly VaultService _vault;

    [ObservableProperty]
    private string _folderPath = "";

    [ObservableProperty]
    private int _strategyIndex; // 0 skip, 1 merge, 2 keep both

    [ObservableProperty]
    private bool _includeReference;

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    private bool _busy;

    public ImportWizardViewModel(VaultService vault)
    {
        _vault = vault;

        // Remember the last folder the user imported from.
        string? remembered = Environment.GetEnvironmentVariable("AURAVAULT_IMPORT_DIR");
        if (!string.IsNullOrEmpty(remembered) && Directory.Exists(remembered))
        {
            _folderPath = remembered;
            _ = ScanAsync();
        }
    }

    public string[] Strategies { get; } = ["Skip duplicates", "Merge into existing", "Keep both"];

    public ObservableCollection<ImportFileRow> Files { get; } = [];

    public bool CanImport => !Busy && Files.Any(f => f.Include);

    private DedupeStrategy Strategy => StrategyIndex switch
    {
        1 => DedupeStrategy.Merge,
        2 => DedupeStrategy.KeepBoth,
        _ => DedupeStrategy.Skip,
    };

    partial void OnFolderPathChanged(string value) => _ = ScanAsync();

    partial void OnIncludeReferenceChanged(bool value) => _ = ScanAsync();

    [RelayCommand]
    private async Task Browse()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
        {
            return;
        }

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose a folder with exported CSV files",
            AllowMultiple = false,
        });

        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            FolderPath = path;
        }
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        Files.Clear();
        Status = "";
        if (!Directory.Exists(FolderPath) || !_vault.IsOpen)
        {
            return;
        }

        Busy = true;
        try
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var preset in IPhoneRecoveryPreset.Files)
            {
                if (!IncludeReference && preset.FileName == "4_tokens_und_system.csv")
                {
                    continue;
                }

                string path = System.IO.Path.Combine(FolderPath, preset.FileName);
                if (!File.Exists(path))
                {
                    continue;
                }

                var (n, d, s) = await Task.Run(() =>
                {
                    var table = DelimitedText.ParseFile(path);
                    var preview = ImportPipeline.Preview(table, preset.CreateMap(), _vault.Database!.Vault, Strategy, now);
                    return (preview.NewCount + preview.UpdatedCount, preview.DuplicateCount, preview.SkippedCount);
                });

                Files.Add(new ImportFileRow
                {
                    FileName = preset.FileName,
                    Path = path,
                    CreateMap = preset.CreateMap,
                    New = n,
                    Duplicate = d,
                    Skipped = s,
                });
            }

            Status = Files.Count == 0
                ? "No recognised CSV files found in that folder."
                : $"{Files.Sum(f => f.New)} new · {Files.Sum(f => f.Duplicate)} duplicate · {Files.Sum(f => f.Skipped)} skipped";
        }
        finally
        {
            Busy = false;
            OnPropertyChanged(nameof(CanImport));
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        Busy = true;
        try
        {
            int added = 0, merged = 0;
            var now = DateTimeOffset.UtcNow;

            await Task.Run(() =>
            {
                foreach (var file in Files.Where(f => f.Include))
                {
                    var table = DelimitedText.ParseFile(file.Path);
                    var preview = ImportPipeline.Preview(table, file.CreateMap(), _vault.Database!.Vault, Strategy, now);
                    var result = ImportPipeline.Commit(preview, _vault.Database!.Vault, now);
                    added += result.Added;
                    merged += result.Updated;
                }
            });

            _vault.MarkDirty();
            _vault.Save();
            Status = $"Imported {added} entries" + (merged > 0 ? $", merged {merged}." : ".") + " Saved.";
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Import failed");
            Status = "Import failed: " + ex.Message;
        }
        finally
        {
            Busy = false;
        }
    }
}
