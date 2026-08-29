namespace AuraVault.Core.Model;

/// <summary>A binary attachment payload, pooled at the database level and referenced by index.</summary>
public sealed class KdbxBinary
{
    public required byte[] Data { get; init; }

    /// <summary>KDBX 4 inner-header "protected" flag (obfuscated with the inner random stream on disk).</summary>
    public bool MemoryProtected { get; init; }
}

/// <summary>A deletion tombstone, used to make merges converge.</summary>
public sealed record DeletedObject(KdbxId Uuid, DateTimeOffset DeletionTime);

/// <summary>Database-level metadata (KeePass <c>&lt;Meta&gt;</c>).</summary>
public sealed class VaultMeta
{
    public string Generator { get; set; } = "AuraVault";

    public string DatabaseName { get; set; } = string.Empty;

    public DateTimeOffset? DatabaseNameChanged { get; set; }

    public string DatabaseDescription { get; set; } = string.Empty;

    public DateTimeOffset? DatabaseDescriptionChanged { get; set; }

    public string DefaultUserName { get; set; } = string.Empty;

    public DateTimeOffset? DefaultUserNameChanged { get; set; }

    public int MaintenanceHistoryDays { get; set; } = 365;

    public string Color { get; set; } = string.Empty;

    public DateTimeOffset? MasterKeyChanged { get; set; }

    public int MasterKeyChangeRec { get; set; } = -1;

    public int MasterKeyChangeForce { get; set; } = -1;

    public bool RecycleBinEnabled { get; set; } = true;

    public KdbxId RecycleBinUuid { get; set; } = KdbxId.Empty;

    public DateTimeOffset? RecycleBinChanged { get; set; }

    public KdbxId EntryTemplatesGroup { get; set; } = KdbxId.Empty;

    public DateTimeOffset? EntryTemplatesGroupChanged { get; set; }

    public int HistoryMaxItems { get; set; } = 10;

    public long HistoryMaxSize { get; set; } = 6L * 1024 * 1024;

    public DateTimeOffset? SettingsChanged { get; set; }

    public Dictionary<string, string> CustomData { get; } = new(StringComparer.Ordinal);
}

/// <summary>The decrypted, in-memory database.</summary>
public sealed class Vault
{
    public VaultMeta Meta { get; set; } = new();

    public Group Root { get; set; } = new() { Name = "Root" };

    /// <summary>Attachment pool; entry <c>Binaries</c> values index into this list.</summary>
    public List<KdbxBinary> Binaries { get; } = [];

    public List<DeletedObject> DeletedObjects { get; } = [];

    /// <summary>Creates an empty vault with a Root group and a Recycle Bin, stamped at <paramref name="now"/>.</summary>
    public static Vault CreateEmpty(string name, DateTimeOffset now)
    {
        var root = new Group { Name = string.IsNullOrWhiteSpace(name) ? "Root" : name, Times = EntryTimes.CreatedNow(now) };
        var recycleBin = new Group { Name = "Recycle Bin", IconId = 43, Times = EntryTimes.CreatedNow(now), EnableAutoType = false, EnableSearching = false };
        root.Groups.Add(recycleBin);

        return new Vault
        {
            Root = root,
            Meta = new VaultMeta
            {
                DatabaseName = name,
                DatabaseNameChanged = now,
                MasterKeyChanged = now,
                SettingsChanged = now,
                RecycleBinEnabled = true,
                RecycleBinUuid = recycleBin.Uuid,
                RecycleBinChanged = now,
            },
        };
    }

    public Group? FindGroup(KdbxId id) => Root.AllGroups().FirstOrDefault(g => g.Uuid == id);
}
