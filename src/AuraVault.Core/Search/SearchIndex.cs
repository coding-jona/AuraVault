using AuraVault.Core.Model;

namespace AuraVault.Core.Search;

/// <summary>A single search result.</summary>
public sealed record SearchHit(Entry Entry, Group Group, double Score);

/// <summary>
/// In-memory search over a vault. v1 does fuzzy matching over title/username/URL/tags/notes plus
/// <c>field:value</c> filters. Rebuilds are cheap for a few thousand entries.
/// </summary>
public sealed class SearchIndex
{
    private readonly List<Record> _records = [];

    private sealed record Record(Entry Entry, Group Group)
    {
        public string Title => Entry.Title;

        public string UserName => Entry.UserName;

        public string Url => Entry.Url;

        public string Notes => Entry.Notes;

        public IReadOnlyList<string> Tags => Entry.Tags;

        public string GroupName => Group.Name;
    }

    public int Count => _records.Count;

    public void Rebuild(Vault vault)
    {
        ArgumentNullException.ThrowIfNull(vault);
        _records.Clear();
        foreach (var group in vault.Root.AllGroups())
        {
            foreach (var entry in group.Entries)
            {
                _records.Add(new Record(entry, group));
            }
        }
    }

    public IReadOnlyList<SearchHit> Search(string rawQuery, int limit = 50)
    {
        var query = SearchQuery.Parse(rawQuery);
        var hits = new List<SearchHit>();

        foreach (var record in _records)
        {
            if (!PassesFilters(record, query))
            {
                continue;
            }

            double score = 0;
            bool matchedEveryTerm = true;

            foreach (var term in query.FreeText)
            {
                double? best = Max(
                    FuzzyMatcher.Score(term, record.Title) is { } t ? t * 3.0 : null,
                    FuzzyMatcher.Score(term, record.UserName) is { } u ? u * 2.0 : null,
                    FuzzyMatcher.Score(term, record.Url) is { } r ? r * 2.0 : null,
                    FuzzyMatcher.Score(term, record.GroupName),
                    FuzzyMatcher.Score(term, string.Join(' ', record.Tags)),
                    FuzzyMatcher.Score(term, record.Notes) is { } n ? n * 0.5 : null);

                if (best is null)
                {
                    matchedEveryTerm = false;
                    break;
                }

                score += best.Value;
            }

            if (!matchedEveryTerm)
            {
                continue;
            }

            hits.Add(new SearchHit(record.Entry, record.Group, score));
        }

        hits.Sort((a, b) => b.Score.CompareTo(a.Score));
        return hits.Count > limit ? hits[..limit] : hits;
    }

    private static bool PassesFilters(Record record, SearchQuery query)
    {
        foreach (var (field, value) in query.Filters)
        {
            bool ok = field switch
            {
                "title" => Contains(record.Title, value),
                "user" or "username" => Contains(record.UserName, value),
                "url" => Contains(record.Url, value),
                "notes" => Contains(record.Notes, value),
                "group" => Contains(record.GroupName, value),
                "tag" => record.Tags.Any(t => string.Equals(t, value, StringComparison.OrdinalIgnoreCase)),
                _ => true, // is:/expires: handled by the security layer later
            };

            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static double? Max(params double?[] values)
    {
        double? best = null;
        foreach (var v in values)
        {
            if (v is { } d && (best is null || d > best))
            {
                best = d;
            }
        }

        return best;
    }
}
