namespace AuraVault.Cli;

/// <summary>Tiny positional + <c>--flag value</c> / <c>--switch</c> argument reader.</summary>
internal sealed class ArgMap
{
    private readonly List<string> _positionals = [];
    private readonly Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);

    public static ArgMap Parse(IReadOnlyList<string> args, int startIndex)
    {
        var map = new ArgMap();
        for (int i = startIndex; i < args.Count; i++)
        {
            string a = args[i];
            if (a.StartsWith("--", StringComparison.Ordinal))
            {
                string key = a[2..];
                if (i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    map._options[key] = args[++i];
                }
                else
                {
                    map._options[key] = null; // bare switch
                }
            }
            else
            {
                map._positionals.Add(a);
            }
        }

        return map;
    }

    public string? Positional(int index) => index < _positionals.Count ? _positionals[index] : null;

    public string RequirePositional(int index, string name) =>
        Positional(index) ?? throw new CliException($"Missing required argument <{name}>.");

    public bool HasFlag(string name) => _options.ContainsKey(name);

    public string? Option(string name) => _options.GetValueOrDefault(name);

    public string OptionOr(string name, string fallback) => _options.TryGetValue(name, out var v) && v is not null ? v : fallback;

    public int OptionInt(string name, int fallback) =>
        _options.TryGetValue(name, out var v) && int.TryParse(v, out int n) ? n : fallback;
}

internal sealed class CliException(string message) : Exception(message);
