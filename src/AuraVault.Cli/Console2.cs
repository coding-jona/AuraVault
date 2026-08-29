using AuraVault.Core.Cryptography;

namespace AuraVault.Cli;

/// <summary>Console helpers: masked password entry with an env-var / stdin fallback for scripts.</summary>
internal static class Console2
{
    public static void Info(string message) => Console.WriteLine(message);

    public static void Ok(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ForegroundColor = prev;
    }

    public static void Warn(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Error.WriteLine(message);
        Console.ForegroundColor = prev;
    }

    public static void Error(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ForegroundColor = prev;
    }

    /// <summary>
    /// Reads a master password. Priority: <c>--password-env NAME</c> → <c>--password-stdin</c> →
    /// interactive masked prompt. Returns a <see cref="CompositeKey"/> (password-only for now).
    /// </summary>
    public static CompositeKey ReadMasterKey(ArgMap args, string prompt, bool confirm = false)
    {
        string? envName = args.Option("password-env");
        if (envName is not null)
        {
            string value = Environment.GetEnvironmentVariable(envName)
                ?? throw new CliException($"Environment variable '{envName}' is not set.");
            return new CompositeKey().AddPassword(value);
        }

        if (args.HasFlag("password-stdin"))
        {
            string value = Console.In.ReadLine() ?? string.Empty;
            return new CompositeKey().AddPassword(value);
        }

        string entered = ReadHidden(prompt);
        if (confirm)
        {
            string again = ReadHidden("Repeat master password: ");
            if (!string.Equals(entered, again, StringComparison.Ordinal))
            {
                throw new CliException("Passwords did not match.");
            }
        }

        return new CompositeKey().AddPassword(entered);
    }

    private static string ReadHidden(string prompt)
    {
        Console.Write(prompt);
        if (Console.IsInputRedirected)
        {
            return Console.In.ReadLine() ?? string.Empty;
        }

        var chars = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return new string([.. chars]);
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (chars.Count > 0)
                {
                    chars.RemoveAt(chars.Count - 1);
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                chars.Add(key.KeyChar);
            }
        }
    }
}
