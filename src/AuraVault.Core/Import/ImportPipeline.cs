using AuraVault.Core.Model;

namespace AuraVault.Core.Import;

/// <summary>Turns a <see cref="TabularTable"/> + <see cref="ColumnMap"/> into a reviewable, committable import.</summary>
public static class ImportPipeline
{
    /// <summary>Builds proposed entries and classifies each against <paramref name="targetVault"/>.</summary>
    public static ImportPreview Preview(
        TabularTable table,
        ColumnMap map,
        Vault targetVault,
        DedupeStrategy strategy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(targetVault);

        var resolver = new DedupeResolver(targetVault);
        var rows = new List<ImportRow>(table.Rows.Count);

        for (int i = 0; i < table.Rows.Count; i++)
        {
            var (entry, groupPath) = BuildEntry(table.Rows[i], map, now);

            var row = new ImportRow
            {
                SourceIndex = i,
                Proposed = entry,
                TargetGroupPath = groupPath,
            };

            bool empty = entry.Title.Length == 0 && entry.UserName.Length == 0 && entry.Password.Length == 0;
            if (empty)
            {
                row.Outcome = ImportOutcome.Skipped;
                row.Note = "no title, username or password";
                rows.Add(row);
                continue;
            }

            var existing = resolver.Match(entry, strategy);
            if (existing is not null)
            {
                row.DuplicateOf = existing;
                row.Outcome = strategy switch
                {
                    DedupeStrategy.KeepBoth => ImportOutcome.New,
                    DedupeStrategy.Merge => HasNewInfo(existing, entry) ? ImportOutcome.Updated : ImportOutcome.Duplicate,
                    _ => ImportOutcome.Duplicate,
                };
            }
            else
            {
                resolver.Register(entry);
            }

            rows.Add(row);
        }

        return new ImportPreview { Rows = rows };
    }

    /// <summary>Applies a preview: adds New rows, merges Updated rows, ignores the rest.</summary>
    public static ImportResult Commit(ImportPreview preview, Vault targetVault, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(targetVault);

        int added = 0, updated = 0, skipped = 0;

        foreach (var row in preview.Rows)
        {
            switch (row.Outcome)
            {
                case ImportOutcome.New:
                    {
                        Group target = ResolveGroup(targetVault, row.TargetGroupPath, now);
                        target.Entries.Add(row.Proposed);
                        added++;
                        break;
                    }

                case ImportOutcome.Updated when row.DuplicateOf is { } existing:
                    {
                        existing.History.Add(existing.Clone(includeHistory: false));
                        MergeInto(existing, row.Proposed, now);
                        updated++;
                        break;
                    }

                default:
                    skipped++;
                    break;
            }
        }

        return new ImportResult(added, updated, skipped);
    }

    private static (Entry Entry, string GroupPath) BuildEntry(TabularRow source, ColumnMap map, DateTimeOffset now)
    {
        var entry = new Entry { Times = EntryTimes.CreatedNow(now) };
        string groupPath = map.ConstantGroupPath;

        foreach (var tag in map.ConstantTags)
        {
            entry.Tags.Add(tag);
        }

        foreach (var mapping in map.Mappings)
        {
            string raw = source[mapping.SourceColumn];
            if (raw.Length == 0 && mapping.Target is not TargetField.GroupPath)
            {
                continue;
            }

            switch (mapping.Target)
            {
                case TargetField.Ignore:
                    break;

                case TargetField.Title:
                    entry.Title = Apply(mapping.Transform, raw, map);
                    break;

                case TargetField.UserName:
                    entry.UserName = raw;
                    break;

                case TargetField.Password:
                    entry.Set(EntryFields.Password, raw, protect: true);
                    break;

                case TargetField.Url:
                    entry.Url = Apply(mapping.Transform, raw, map);
                    break;

                case TargetField.Notes:
                    entry.Notes = string.IsNullOrEmpty(entry.Notes) ? raw : entry.Notes + "\n" + raw;
                    break;

                case TargetField.Tags:
                    foreach (var t in SplitTags(raw))
                    {
                        if (!entry.Tags.Contains(t))
                        {
                            entry.Tags.Add(t);
                        }
                    }

                    break;

                case TargetField.GroupPath:
                    if (raw.Length > 0)
                    {
                        groupPath = CombinePath(map.ConstantGroupPath, raw);
                    }

                    break;

                case TargetField.Created:
                    if (map.TryParseTimestamp(raw, out var created))
                    {
                        entry.Times.CreationTime = created;
                    }

                    break;

                case TargetField.Modified:
                    if (map.TryParseTimestamp(raw, out var modified))
                    {
                        entry.Times.LastModificationTime = modified;
                    }

                    break;

                case TargetField.Totp:
                    entry.Strings["otp"] = new ProtectedString(raw, IsProtected: true);
                    break;

                case TargetField.CustomField:
                    if (!string.IsNullOrEmpty(mapping.CustomFieldName))
                    {
                        entry.Strings[mapping.CustomFieldName] = new ProtectedString(raw, mapping.Protected);
                    }

                    break;
            }
        }

        if (entry.Title.Length == 0 && map.TitleFallbackColumn is { } fb)
        {
            string fallback = source[fb];
            entry.Title = DedupeResolver.NormalizeHost(fallback) is { Length: > 0 } host ? host : fallback;
        }

        if (!map.CarriesSecrets)
        {
            entry.QualityCheck = "False";
        }

        return (entry, groupPath);
    }

    private static string Apply(ColumnTransform transform, string value, ColumnMap map) => transform switch
    {
        ColumnTransform.DomainToTitle => DomainTitle(value),
        ColumnTransform.EnsureUrlScheme => EnsureScheme(value),
        _ => value,
    };

    private static string DomainTitle(string value)
    {
        string host = DedupeResolver.NormalizeHost(value);
        if (host.Length == 0)
        {
            return value;
        }

        // Registrable-ish: keep the last two labels unless it looks like a known 2-level TLD.
        string[] parts = host.Split('.');
        return parts.Length <= 2 ? host : string.Join('.', parts[^2..]);
    }

    private static string EnsureScheme(string value) =>
        value.Contains("://", StringComparison.Ordinal) ? value : "https://" + value;

    private static string[] SplitTags(string raw) =>
        raw.Split([';', ',', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string CombinePath(string basePath, string leaf) =>
        string.IsNullOrEmpty(basePath) ? leaf : $"{basePath}/{leaf}";

    private static Group ResolveGroup(Vault vault, string path, DateTimeOffset now)
    {
        var segments = path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return vault.Root.FindOrCreatePath(segments, now);
    }

    private static bool HasNewInfo(Entry existing, Entry candidate)
    {
        if (existing.Password.Length == 0 && candidate.Password.Length > 0)
        {
            return true;
        }

        if (existing.UserName.Length == 0 && candidate.UserName.Length > 0)
        {
            return true;
        }

        if (existing.Url.Length == 0 && candidate.Url.Length > 0)
        {
            return true;
        }

        return candidate.Tags.Any(t => !existing.Tags.Contains(t));
    }

    private static void MergeInto(Entry existing, Entry candidate, DateTimeOffset now)
    {
        if (existing.Password.Length == 0 && candidate.Password.Length > 0)
        {
            existing.Set(EntryFields.Password, candidate.Password, protect: true);
        }

        if (existing.UserName.Length == 0)
        {
            existing.UserName = candidate.UserName;
        }

        if (existing.Url.Length == 0)
        {
            existing.Url = candidate.Url;
        }

        if (existing.Notes.Length == 0)
        {
            existing.Notes = candidate.Notes;
        }

        foreach (var t in candidate.Tags)
        {
            if (!existing.Tags.Contains(t))
            {
                existing.Tags.Add(t);
            }
        }

        existing.Times.LastModificationTime = now;
    }
}
