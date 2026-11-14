using System.Diagnostics;
using LibXboxOne.XVC2;
using Spectre.Console;
using Spectre.Console.Cli;

namespace XvdTool.Streaming.Commands.Msixvc2;

internal abstract class Msixvc2Command<T> : Command<T> where T : Msixvc2CommandSettings
{
    protected Msixvc2File Msixvc2 = default!;

    protected void Initialize(Msixvc2CommandSettings settings)
    {
        Debug.Assert(settings.XvcPath != null);

        var stream = Stream.OpenFileStream(settings.XvcPath, false);

        if (!Msixvc2File.CheckMsixvc2Magic(stream))
        {
            throw new NotSupportedException("File is not a MSIXVC2.");
        }

        Msixvc2 = Msixvc2File.Open(stream);

        if (settings.ContentKey != null || settings.VersionKey != null)
        {
            var contentKey = settings.ContentKey != null ? Convert.FromBase64String(settings.ContentKey) : null;
            var versionKey = settings.VersionKey != null ? Convert.FromBase64String(settings.VersionKey) : null;
            Msixvc2.SubmitKeys(contentKey, versionKey);
        }

        if (settings.CikPath != null)
        {
            var entry = File.ReadAllBytes(settings.CikPath)[16..];
            Msixvc2.SubmitKeys(entry, null);
        }

        try
        {
            Msixvc2.LoadFileNames();
        }
        catch (Exception)
        {
            // Attempt to load file names, the info command can be used without this.
        }
    }

    protected override ValidationResult Validate(CommandContext context, T settings)
    {
        if (settings.XvcPath != null && !settings.XvcPath.StartsWith("http") && !File.Exists(settings.XvcPath))
            return ValidationResult.Error("Provided file does not exist.");

        return ValidationResult.Success();
    }
}