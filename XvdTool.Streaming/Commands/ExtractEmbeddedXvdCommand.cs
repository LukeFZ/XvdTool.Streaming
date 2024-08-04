using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;

namespace XvdTool.Streaming.Commands;

internal sealed class ExtractEmbeddedXvdCommand : XvdCommand<ExtractEmbeddedXvdCommand.Settings>
{
    internal sealed class Settings : XvdCommandSettings
    {
        [CommandOption("-o|--output")]
        [Description("Output path of the embedded XVD.")]
        [DefaultValue("embedded.xvd")]
        public string EmbeddedXvdOutputPath { get; set; } = null!;
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        Initialize(settings, requiresWriting: false);

        Debug.Assert(XvdFile != null, "XvdFile != null");

        var directory = Path.GetDirectoryName(settings.EmbeddedXvdOutputPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using (XvdFile)
        {
            XvdFile.ExtractEmbeddedXvd(settings.EmbeddedXvdOutputPath);
        }

        return 0;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (Directory.Exists(settings.EmbeddedXvdOutputPath))
            return ValidationResult.Error("The embedded XVD output path is a directory.");

        return base.Validate(context, settings);
    }
}