namespace AuraVault.Core.Model;

/// <summary>A folder in the vault tree.</summary>
public sealed class Group
{
    public KdbxId Uuid { get; set; } = KdbxId.New();

    public string Name { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public int IconId { get; set; } = 48; // KeePass "folder" icon

    public KdbxId? CustomIconUuid { get; set; }

    public EntryTimes Times { get; set; } = new();

    public bool IsExpanded { get; set; } = true;

    public string? DefaultAutoTypeSequence { get; set; }

    public bool? EnableAutoType { get; set; }

    public bool? EnableSearching { get; set; }

    public KdbxId? LastTopVisibleEntry { get; set; }

    public Dictionary<string, string> CustomData { get; } = new(StringComparer.Ordinal);

    public List<Group> Groups { get; } = [];

    public List<Entry> Entries { get; } = [];

    /// <summary>Depth-first walk of this group and all descendants (this group first).</summary>
    public IEnumerable<Group> AllGroups()
    {
        yield return this;
        foreach (var child in Groups)
        {
            foreach (var g in child.AllGroups())
            {
                yield return g;
            }
        }
    }

    /// <summary>All entries in this subtree, excluding history items.</summary>
    public IEnumerable<Entry> AllEntries()
    {
        foreach (var g in AllGroups())
        {
            foreach (var e in g.Entries)
            {
                yield return e;
            }
        }
    }

    public Group FindOrCreateSubgroup(string name, DateTimeOffset now)
    {
        var existing = Groups.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.Ordinal));
        if (existing is not null)
        {
            return existing;
        }

        var created = new Group { Name = name, Times = EntryTimes.CreatedNow(now) };
        Groups.Add(created);
        return created;
    }

    /// <summary>Resolves or creates a nested path like <c>Import/iPhone/Web</c>.</summary>
    public Group FindOrCreatePath(IEnumerable<string> segments, DateTimeOffset now)
    {
        var current = this;
        foreach (var segment in segments)
        {
            current = current.FindOrCreateSubgroup(segment, now);
        }

        return current;
    }
}
