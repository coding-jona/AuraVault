namespace AuraVault.Core.Search;

/// <summary>
/// fzf-style fuzzy scoring: the query must appear as a case-insensitive subsequence of the target.
/// Consecutive matches, word boundaries and a prefix match all raise the score.
/// </summary>
public static class FuzzyMatcher
{
    /// <summary>Returns a score (higher is better), or <c>null</c> if <paramref name="query"/> is not a subsequence.</summary>
    public static double? Score(string query, string target)
    {
        if (string.IsNullOrEmpty(query))
        {
            return 0;
        }

        if (string.IsNullOrEmpty(target) || query.Length > target.Length)
        {
            return null;
        }

        double score = 0;
        int qi = 0;
        int lastMatch = -2;
        bool firstMatch = true;

        for (int ti = 0; ti < target.Length && qi < query.Length; ti++)
        {
            if (char.ToLowerInvariant(target[ti]) != char.ToLowerInvariant(query[qi]))
            {
                continue;
            }

            score += 1.0;

            if (ti == lastMatch + 1)
            {
                score += 2.0; // consecutive
            }

            bool atBoundary = ti == 0 || target[ti - 1] is ' ' or '.' or '-' or '_' or '/' or ':' or '@';
            if (atBoundary)
            {
                score += 1.5;
            }

            if (firstMatch)
            {
                score += ti == 0 ? 3.0 : Math.Max(0, 2.0 - (ti * 0.1));
                firstMatch = false;
            }

            lastMatch = ti;
            qi++;
        }

        if (qi < query.Length)
        {
            return null;
        }

        // Prefer shorter targets and an exact (case-insensitive) hit.
        score += 5.0 * query.Length / target.Length;
        if (string.Equals(query, target, StringComparison.OrdinalIgnoreCase))
        {
            score += 10.0;
        }

        return score;
    }
}
