using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console.Cli;

namespace XvdTool.Streaming.Commands;

internal sealed class InfoCommand : XvdCommand<InfoCommand.Settings>
{
    public sealed class Settings : XvdCommandSettings
    {
        [Description("File path to save the output into.")]
        [CommandOption("-o|--output")]
        public string? OutputPath { get; init; }

        [Description("If all files should be printed.\nIf unset, only the first 4096 files will be printed.")]
        [CommandOption("-a|--show-all-files")]
        public bool ShowAllFiles { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        Initialize(settings, requiresWriting: false);

        Debug.Assert(XvdFile != null, "XvdFile != null");

        using (XvdFile)
        {
            var infoOutput = XvdFile.PrintInfo(settings.ShowAllFiles);
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