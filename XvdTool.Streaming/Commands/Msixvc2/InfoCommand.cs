using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console.Cli;

namespace XvdTool.Streaming.Commands.Msixvc2;

internal sealed class InfoCommand : Msixvc2Command<InfoCommand.Settings>
{
    public sealed class Settings : Msixvc2CommandSettings
    {
        [Description("File path to save the output into.")]
        [CommandOption("-o|--output")]
        public string? OutputPath { get; init; }

        [Description("If all files should be printed.\nIf unset, only the first 4096 files will be printed.")]
        [CommandOption("-a|--show-all-files")]
        public bool ShowAllFiles { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        Initialize(settings);

        Debug.Assert(Msixvc2 != null);

        using (Msixvc2)
        {
            var infoOutput = Msixvc2.PrintInfo(settings.ShowAllFiles);
            if (settings.OutputPath != null)
            {
                var directory = Path.GetDirectoryName(settings.OutputPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(settings.OutputPath, infoOutput);
            }
        }

        return 0;
    }
}