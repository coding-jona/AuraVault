using System;
using AuraVault.Core.Cryptography;
using AuraVault.Core.Kdbx;
using AuraVault.Core.Model;
using AuraVault.Core.Search;
using AuraVault.Core.Vaults;

namespace AuraVault.App.Services;

/// <summary>Holds the one open vault for the session: open / create / save / lock, plus the search index.</summary>
public sealed class VaultService
{
    private CompositeKey? _key;

    public KdbxDatabase? Database { get; private set; }

    public string? Path { get; private set; }

    public bool IsOpen => Database is not null;

    public bool IsDirty { get; private set; }

    public SearchIndex Index { get; } = new();

    public event EventHandler? Opened;

    public event EventHandler? Closed;

    public event EventHandler? Saved;

    public event EventHandler? DirtyChanged;

    public event EventHandler? Changed;

    /// <summary>The vault's Recycle Bin group, resolving/creating it if needed.</summary>
    public Group RecycleBin
    {
        get
        {
            var vault = Database!.Vault;
            var bin = vault.FindGroup(vault.Meta.RecycleBinUuid);
            if (bin is null)
            {
                bin = new Group { Name = "Recycle Bin", IconId = 43, Times = EntryTimes.CreatedNow(DateTimeOffset.UtcNow) };
                vault.Root.Groups.Add(bin);
                vault.Meta.RecycleBinUuid = bin.Uuid;
                vault.Meta.RecycleBinEnabled = true;
            }

            return bin;
        }
    }

    /// <summary>Marks the model changed since the last save and refreshes the search index.</summary>
    public void MarkDirty()
    {
        ReindexSearch();
        Changed?.Invoke(this, EventArgs.Empty);
        if (!IsDirty)
        {
            IsDirty = true;
            DirtyChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Create(string path, string name, CompositeKey key)
    {
        var db = KdbxDatabase.CreateEmpty(name, DateTimeOffset.UtcNow);
        VaultFile.Save(path, db, key);
        Adopt(path, db, key);
    }

    public void Open(string path, CompositeKey key)
    {
        var db = VaultFile.Open(path, key);
        Adopt(path, db, key);
    }

    public void Save()
    {
        if (Database is null || Path is null || _key is null)
        {
            throw new InvalidOperationException("No vault is open.");
        }

        VaultFile.Save(Path, Database, _key);
        if (IsDirty)
        {
            IsDirty = false;
            DirtyChanged?.Invoke(this, EventArgs.Empty);
        }

        Saved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Locks the vault: forget the key and the decrypted model.</summary>
    public void Close()
    {
        _key?.Dispose();
        _key = null;
        Database = null;
        Path = null;
        IsDirty = false;
        Index.Rebuild(Vault.CreateEmpty("empty", DateTimeOffset.UtcNow));
        GC.Collect();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void Adopt(string path, KdbxDatabase db, CompositeKey key)
    {
        _key?.Dispose();
        _key = key;
        Database = db;
        Path = path;
        Index.Rebuild(db.Vault);
        Opened?.Invoke(this, EventArgs.Empty);
    }

    public void ReindexSearch()
    {
        if (Database is not null)
        {
            Index.Rebuild(Database.Vault);
        }
    }
}
