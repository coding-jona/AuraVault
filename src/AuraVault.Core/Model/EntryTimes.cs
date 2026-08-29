namespace AuraVault.Core.Model;

/// <summary>Timestamps and usage counters shared by <see cref="Entry"/> and <see cref="Group"/>.</summary>
public sealed class EntryTimes
{
    public DateTimeOffset? CreationTime { get; set; }

    public DateTimeOffset? LastModificationTime { get; set; }

    public DateTimeOffset? LastAccessTime { get; set; }

    public DateTimeOffset? ExpiryTime { get; set; }

    public bool Expires { get; set; }

    public int UsageCount { get; set; }

    public DateTimeOffset? LocationChanged { get; set; }

    /// <summary>Creates a set of times all stamped at <paramref name="now"/>.</summary>
    public static EntryTimes CreatedNow(DateTimeOffset now) => new()
    {
        CreationTime = now,
        LastModificationTime = now,
        LastAccessTime = now,
        LocationChanged = now,
        Expires = false,
    };

    public EntryTimes Clone() => new()
    {
        CreationTime = CreationTime,
        LastModificationTime = LastModificationTime,
        LastAccessTime = LastAccessTime,
        ExpiryTime = ExpiryTime,
        Expires = Expires,
        UsageCount = UsageCount,
        LocationChanged = LocationChanged,
    };
}
