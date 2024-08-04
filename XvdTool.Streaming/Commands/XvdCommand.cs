using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;

namespace XvdTool.Streaming.Commands;

internal abstract class XvdCommand<T> : Command<T> where T : XvdCommandSettings
{
    protected StreamedXvdFile XvdFile = default!;

    protected void Initialize(XvdCommandSettings settings, bool requiresWriting)
    {
        Debug.Assert(settings.XvcPath != null, "settings.XvcPath != null");

        var path = settings.XvcPath;

        XvdFile = path.StartsWith("http")
            ? StreamedXvdFile.OpenFromUrl(path)
            : StreamedXvdFile.OpenFromFile(path, requiresWriting);

        XvdFile.Parse();
    }

    public override ValidationResult Validate(CommandContext context, T settings)
    {
        if (settings.XvcPath != null && !settings.XvcPath.StartsWith("http") && !File.Exists(settings.XvcPath))
            return ValidationResult.Error("Provided file does not exist.");

        return ValidationResult.Success();
    }
}