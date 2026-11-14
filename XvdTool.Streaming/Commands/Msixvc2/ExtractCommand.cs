using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console.Cli;

namespace XvdTool.Streaming.Commands.Msixvc2;

internal sealed class ExtractCommand : Msixvc2Command<ExtractCommand.Settings>
{
    public sealed class Settings : Msixvc2CommandSettings
    {
        [Description("File path to save the output into.")]
        [CommandOption("-o|--output")]
        public string? OutputPath { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        Initialize(settings);

        Debug.Assert(Msixvc2 != null);

        using (Msixvc2)
        {
            Msixvc2.ExtractFiles(settings.OutputPath);
        }

        return 0;
    }
}