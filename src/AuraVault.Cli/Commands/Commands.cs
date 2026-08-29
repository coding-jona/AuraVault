using AuraVault.Core.Cryptography;
using AuraVault.Core.Generator;
using AuraVault.Core.Import;
using AuraVault.Core.Kdbx;
using AuraVault.Core.Model;
using AuraVault.Core.Search;
using AuraVault.Core.Vaults;

namespace AuraVault.Cli.Commands;

internal static class HelpCommand
{
    public static void Print()
    {
        Console2.Info(
            """
            AuraVault CLI — KeePass-compatible vault operations (KDBX 4.1)

            USAGE
              auravault create <vault.kdbx>
              auravault import <vault.kdbx> --dir <folder> [--preset iphone] [--dedupe skip|merge|keepboth] [--commit]
              auravault import <vault.kdbx> --file <export.csv> [--group <path>] [--dedupe ...] [--commit]
              auravault ls <vault.kdbx> [query...] [--show-passwords] [--limit N]
              auravault gen [--length 20] [--no-symbols] [--no-digits]
              auravault gen --passphrase [--words 5] [--sep -] [--digits 0] [--title|--upper]

            PASSWORD INPUT (any command that opens/creates a vault)
              interactive masked prompt (default)
              --password-env NAME     read the master password from an environment variable
              --password-stdin        read the master password from the first line of stdin

            NOTES
              'import' without --commit is a dry run: it prints New/Duplicate/Updated/Skipped counts.
              'ls' hides passwords unless --show-passwords is given.
            """);
    }
}

internal static class VersionCommand
{
    public static int Run()
    {
        var v = typeof(VersionCommand).Assembly.GetName().Version;
        Console2.Info($"auravault {v?.ToString(3) ?? "0.0.0"} (KDBX 4.1)");
        return 0;
    }
}

internal static class CreateCommand
{
    public static int Run(string[] args)
    {
        var a = ArgMap.Parse(args, 1);
        string path = a.RequirePositional(0, "vault.kdbx");
        if (File.Exists(path))
        {
            throw new CliException($"'{path}' already exists.");
        }

        using var key = Console2.ReadMasterKey(a, "New master password: ", confirm: true);
        var now = DateTimeOffset.UtcNow;
        var db = KdbxDatabase.CreateEmpty(Path.GetFileNameWithoutExtension(path), now);
        VaultFile.Save(path, db, key);
        Console2.Ok($"Created {path} (Argon2id + ChaCha20, KDBX 4.1).");
        return 0;
    }
}

internal static class ImportCommand
{
    public static int Run(string[] args)
    {
        var a = ArgMap.Parse(args, 1);
        string vaultPath = a.RequirePositional(0, "vault.kdbx");
        string? dir = a.Option("dir");
        string? file = a.Option("file");
        if (dir is null && file is null)
        {
            throw new CliException("Provide --dir <folder> or --file <export.csv>.");
        }

        var dedupe = a.OptionOr("dedupe", "skip").ToLowerInvariant() switch
        {
            "merge" => DedupeStrategy.Merge,
            "keepboth" => DedupeStrategy.KeepBoth,
            _ => DedupeStrategy.Skip,
        };
        bool commit = a.HasFlag("commit");

        using var key = Console2.ReadMasterKey(a, $"Master password for {Path.GetFileName(vaultPath)}: ");
        KdbxDatabase db = VaultFile.Open(vaultPath, key);
        var now = DateTimeOffset.UtcNow;

        var sources = new List<(string Path, ColumnMap Map)>();
        if (dir is not null)
        {
            // The token/system dump is metadata-only noise by default — opt in with --include-reference.
            bool includeReference = a.HasFlag("include-reference");
            foreach (var preset in IPhoneRecoveryPreset.Files)
            {
                if (!includeReference && preset.FileName == "4_tokens_und_system.csv")
                {
                    continue;
                }

                string candidate = Path.Combine(dir, preset.FileName);
                if (File.Exists(candidate))
                {
                    sources.Add((candidate, preset.CreateMap()));
                }
            }

            if (sources.Count == 0)
            {
                throw new CliException($"No recognised recovery CSVs found in '{dir}'.");
            }
        }
        else
        {
            var preset = IPhoneRecoveryPreset.ForFile(file!);
            ColumnMap map = preset?.CreateMap() ?? throw new CliException(
                $"No built-in preset for '{Path.GetFileName(file)}'. Custom mapping is a UI feature; use the iPhone CSVs for now.");
            if (a.Option("group") is { } g)
            {
                map.ConstantGroupPath = g;
            }

            sources.Add((file!, map));
        }

        int totalNew = 0, totalDup = 0, totalUpd = 0, totalSkip = 0, totalAdded = 0, totalMerged = 0;

        foreach (var (srcPath, map) in sources)
        {
            var table = DelimitedText.ParseFile(srcPath);
            var preview = ImportPipeline.Preview(table, map, db.Vault, dedupe, now);
            Console2.Info($"  {Path.GetFileName(srcPath),-28}  new {preview.NewCount,5}  dup {preview.DuplicateCount,5}  upd {preview.UpdatedCount,5}  skip {preview.SkippedCount,5}");
            totalNew += preview.NewCount;
            totalDup += preview.DuplicateCount;
            totalUpd += preview.UpdatedCount;
            totalSkip += preview.SkippedCount;

            if (commit)
            {
                var result = ImportPipeline.Commit(preview, db.Vault, now);
                totalAdded += result.Added;
                totalMerged += result.Updated;
            }
        }

        Console2.Info($"  {"TOTAL",-28}  new {totalNew,5}  dup {totalDup,5}  upd {totalUpd,5}  skip {totalSkip,5}");

        if (!commit)
        {
            Console2.Warn("Dry run — nothing written. Re-run with --commit to apply.");
            return 0;
        }

        VaultFile.Save(vaultPath, db, key);
        Console2.Ok($"Committed: {totalAdded} added, {totalMerged} merged. Saved {vaultPath} (.bak kept).");
        return 0;
    }
}

internal static class ListCommand
{
    public static int Run(string[] args)
    {
        var a = ArgMap.Parse(args, 1);
        string vaultPath = a.RequirePositional(0, "vault.kdbx");
        bool showPasswords = a.HasFlag("show-passwords");
        int limit = a.OptionInt("limit", 50);
        string query = string.Join(' ', Enumerable.Range(1, 8).Select(a.Positional).Where(s => s is not null)!);

        using var key = Console2.ReadMasterKey(a, $"Master password for {Path.GetFileName(vaultPath)}: ");
        KdbxDatabase db = VaultFile.Open(vaultPath, key);

        IEnumerable<(Entry Entry, string Group)> rows;
        if (string.IsNullOrWhiteSpace(query))
        {
            rows = db.Vault.Root.AllGroups()
                .SelectMany(g => g.Entries.Select(e => (e, g.Name)))
                .Take(limit);
        }
        else
        {
            var index = new SearchIndex();
            index.Rebuild(db.Vault);
            rows = index.Search(query, limit).Select(h => (h.Entry, h.Group.Name));
        }

        int shown = 0;
        foreach (var (entry, group) in rows)
        {
            string pw = showPasswords ? entry.Password : new string('•', Math.Min(entry.Password.Length, 10));
            Console2.Info($"  [{group}] {Trunc(entry.Title, 32),-32}  {Trunc(entry.UserName, 28),-28}  {pw}");
            shown++;
        }

        Console2.Info($"  {shown} entr{(shown == 1 ? "y" : "ies")}.");
        return 0;
    }

    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";
}

internal static class GenerateCommand
{
    public static int Run(string[] args)
    {
        var a = ArgMap.Parse(args, 1);

        if (a.HasFlag("passphrase") || a.Option("words") is not null)
        {
            var profile = new PassphraseProfile
            {
                WordCount = a.OptionInt("words", 5),
                Separator = a.OptionOr("sep", "-"),
                AppendDigits = a.OptionInt("digits", 0),
                Casing = a.HasFlag("upper") ? PassphraseCasing.Upper
                    : a.HasFlag("title") ? PassphraseCasing.Title
                    : PassphraseCasing.Lower,
            };
            string phrase = PasswordGenerator.GeneratePassphrase(profile);
            double bits = EntropyEstimator.PassphraseBits(profile.WordCount, EffLargeWordList.Instance.Count, profile.AppendDigits);
            Console2.Info(phrase);
            Console2.Info($"  ~{bits:F0} bits — {EntropyEstimator.Classify(bits)}");
            return 0;
        }

        var chars = new CharacterProfile
        {
            Length = a.OptionInt("length", 20),
            Digits = !a.HasFlag("no-digits"),
            Symbols = !a.HasFlag("no-symbols"),
            Uppercase = !a.HasFlag("no-upper"),
            Lowercase = !a.HasFlag("no-lower"),
        };
        string password = PasswordGenerator.Generate(chars);
        double b = EntropyEstimator.PoolBits(password);
        Console2.Info(password);
        Console2.Info($"  ~{b:F0} bits — {EntropyEstimator.Classify(b)}");
        return 0;
    }
}
