using AuraVault.Cli;
using AuraVault.Cli.Commands;

try
{
    if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
    {
        HelpCommand.Print();
        return 0;
    }

    return args[0].ToLowerInvariant() switch
    {
        "create" => CreateCommand.Run(args),
        "import" => ImportCommand.Run(args),
        "ls" or "list" => ListCommand.Run(args),
        "gen" or "generate" => GenerateCommand.Run(args),
        "version" or "--version" => VersionCommand.Run(),
        _ => Unknown(args[0]),
    };
}
catch (CliException ex)
{
    Console2.Error(ex.Message);
    return 2;
}
catch (AuraVault.Core.Kdbx.KdbxIntegrityException)
{
    Console2.Error("Wrong master password (or the file was tampered with).");
    return 3;
}
catch (FileNotFoundException ex)
{
    Console2.Error($"File not found: {ex.FileName ?? ex.Message}");
    return 4;
}

static int Unknown(string command)
{
    Console2.Error($"Unknown command '{command}'. Run 'auravault help'.");
    return 2;
}
