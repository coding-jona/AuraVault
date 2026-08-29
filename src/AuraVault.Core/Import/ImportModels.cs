using AuraVault.Core.Model;

namespace AuraVault.Core.Import;

/// <summary>What the pipeline decided to do with one source row.</summary>
public enum ImportOutcome
{
    New,
    Duplicate,
    Updated,
    Skipped,
}

/// <summary>How to treat a source row that matches an existing entry.</summary>
public enum DedupeStrategy
{
    /// <summary>Leave the existing entry untouched; mark the row <see cref="ImportOutcome.Duplicate"/>.</summary>
    Skip,

    /// <summary>Fill only empty fields on the existing entry; snapshot its history first.</summary>
    Merge,

    /// <summary>Import anyway as a separate entry.</summary>
    KeepBoth,
}

/// <summary>One row's proposed result, shown in the preview before commit.</summary>
public sealed class ImportRow
{
    public required int SourceIndex { get; init; }

    public required Entry Proposed { get; init; }

    public required string TargetGroupPath { get; init; }

    public ImportOutcome Outcome { get; set; } = ImportOutcome.New;

    /// <summary>The existing entry this row matched, when <see cref="Outcome"/> is Duplicate/Updated.</summary>
    public Entry? DuplicateOf { get; set; }

    /// <summary>Human-readable reason a row was skipped or flagged.</summary>
    public string? Note { get; set; }
}

/// <summary>The full, reviewable outcome of a dry-run import.</summary>
public sealed class ImportPreview
{
    public required IReadOnlyList<ImportRow> Rows { get; init; }

    public int NewCount => Rows.Count(r => r.Outcome == ImportOutcome.New);

    public int DuplicateCount => Rows.Count(r => r.Outcome == ImportOutcome.Duplicate);

    public int UpdatedCount => Rows.Count(r => r.Outcome == ImportOutcome.Updated);

    public int SkippedCount => Rows.Count(r => r.Outcome == ImportOutcome.Skipped);
}

/// <summary>Counts returned after committing a preview into a vault.</summary>
public sealed record ImportResult(int Added, int Updated, int Skipped);
