using System;
using System.Collections.Generic;
using System.Linq;

namespace AuraVault.App.Commands;

/// <summary>A single invokable action. Menu bar, command palette and shortcuts all read from these.</summary>
public sealed class AppCommand(
    string id,
    string title,
    string category,
    Action execute,
    Func<bool>? canExecute = null,
    string? gesture = null,
    string keywords = "")
{
    public string Id { get; } = id;

    public string Title { get; } = title;

    public string Category { get; } = category;

    public string? Gesture { get; } = gesture;

    public string Keywords { get; } = keywords;

    public Func<bool> CanExecute { get; } = canExecute ?? (static () => true);

    public Action Execute { get; } = execute;
}

/// <summary>The single source of commands for the whole app.</summary>
public sealed class CommandRegistry
{
    private readonly List<AppCommand> _commands = [];

    public IReadOnlyList<AppCommand> All => _commands;

    public AppCommand Add(AppCommand command)
    {
        _commands.Add(command);
        return command;
    }

    public AppCommand? ById(string id) => _commands.FirstOrDefault(c => c.Id == id);

    public IEnumerable<AppCommand> ByCategory(string category) =>
        _commands.Where(c => string.Equals(c.Category, category, StringComparison.Ordinal));

    public IEnumerable<string> Categories => _commands.Select(c => c.Category).Distinct();

    /// <summary>Fuzzy-ish filter for the command palette.</summary>
    public IEnumerable<AppCommand> Search(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return _commands;
        }

        string q = text.Trim();
        return _commands
            .Select(c => (Command: c, Score: Score(c, q)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Command);
    }

    private static int Score(AppCommand c, string q)
    {
        string haystack = $"{c.Title} {c.Category} {c.Keywords}";
        if (haystack.Contains(q, StringComparison.OrdinalIgnoreCase))
        {
            return c.Title.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 100 : 50;
        }

        // subsequence
        int qi = 0;
        foreach (char ch in haystack)
        {
            if (qi < q.Length && char.ToLowerInvariant(ch) == char.ToLowerInvariant(q[qi]))
            {
                qi++;
            }
        }

        return qi == q.Length ? 10 : 0;
    }
}
