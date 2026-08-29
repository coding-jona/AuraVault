namespace AuraVault.Core.Search;

/// <summary>A parsed query: free-text terms plus <c>field:value</c> filters.</summary>
public sealed class SearchQuery
{
    public List<string> FreeText { get; } = [];

    public List<(string Field, string Value)> Filters { get; } = [];

    public bool IsEmpty => FreeText.Count == 0 && Filters.Count == 0;

    public static SearchQuery Parse(string raw)
    {
        var query = new SearchQuery();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return query;
        }

        foreach (var token in Tokenize(raw))
        {
            int colon = token.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && colon < token.Length - 1)
            {
                string field = token[..colon].ToLowerInvariant();
                string value = token[(colon + 1)..].Trim('"');
                if (IsKnownField(field))
                {
                    query.Filters.Add((field, value));
                    continue;
                }
            }

            query.FreeText.Add(token.Trim('"'));
        }

        return query;
    }

    private static bool IsKnownField(string field) => field is
        "title" or "user" or "username" or "url" or "tag" or "group" or "notes" or "is" or "expires";

    private static IEnumerable<string> Tokenize(string raw)
    {
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        foreach (char c in raw)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                current.Append(c);
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }
}
