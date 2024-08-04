using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;

namespace XvdTool.Streaming.Commands;

internal sealed class ExtractCommand : CryptoCommand<ExtractCommand.Settings>
{
    public sealed class Settings : CryptoCommandSettings
    {
        [DefaultValue("output")]
        [Description("Output directory to extract the files into.")]
        [CommandOption("-o|--output")]
        public string? OutputDirectory { get; init; }

        [Description("List of regions to skip downloading. Defaults to none.")]
        [CommandOption("-b|--skip-region")]
        public uint[]? SkipRegions { get; init; }

        [Description("List of regions to download. Defaults to all.")]
        [CommandOption("-w|--download-region")]
        public uint[]? DownloadRegions { get; init; }

        [Description("Skips performing hash verification on the pages prior to decryption.\nMassively improves performance at the cost of integrity.\nOnly use this if you know the file is not corrupt!")]
        [CommandOption("-n|--no-hash-check")]
        public bool SkipHashCheck { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        Debug.Assert(settings.OutputDirectory != null, "settings.OutputDirectory != null");

        if (!Initialize(settings, out var keyEntry))
        {
            return -1;
        }

        var outputPath = Path.GetFullPath(settings.OutputDirectory);

        var hashStatus = settings.SkipHashCheck ? "[red]disabled[/]" : "[green]enabled[/]";

        ConsoleLogger.WriteInfoLine($"Extracting files into [green bold]{outputPath}[/]. (Hash check {hashStatus})");

        using (XvdFile)
        {
            XvdFile.ExtractFiles(outputPath, keyEntry, settings.SkipHashCheck, settings.SkipRegions, settings.DownloadRegions);
        }

        ConsoleLogger.WriteInfoLine("[green bold]Successfully[/] extracted files.");

        return 0;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (settings is { DownloadRegions: not null, SkipRegions: not null })
            return ValidationResult.Error("'--skip-region' and '--download-region' cannot be used together.");

        return ValidationResult.Success();
    }
}