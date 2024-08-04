using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;

namespace XvdTool.Streaming.Commands;

internal sealed class VerifyCommand : XvdCommand<VerifyCommand.Settings>
{
    public sealed class Settings : XvdCommandSettings;

    public override int Execute(CommandContext context, Settings settings)
    {
        Initialize(settings, requiresWriting: false);

        Debug.Assert(XvdFile != null, "XvdFile != null");

        using (XvdFile)
        {
            var result = XvdFile.VerifyDataHashes();

            ConsoleLogger.WriteInfoLine(result
                ? "Integrity check [green bold]successful[/]."
                : "Integrity check [red bold]failed[/].");
        }

        return 0;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        base.Validate(context, settings);

        Debug.Assert(settings.XvcPath != null, "settings.XvcPath != null");

        if (settings.XvcPath.StartsWith("http"))
            return ValidationResult.Error("Only local files are supported for integrity verification.");

        if (!File.Exists(settings.XvcPath))
            return ValidationResult.Error("Provided file does not exist.");

        return ValidationResult.Success();
    }
}