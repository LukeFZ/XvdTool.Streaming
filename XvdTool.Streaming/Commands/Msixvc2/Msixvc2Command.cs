using System.Diagnostics;
using LibXboxOne.XVC2;
using Spectre.Console;
using Spectre.Console.Cli;

namespace XvdTool.Streaming.Commands.Msixvc2;

internal abstract class Msixvc2Command<T> : Command<T> where T : Msixvc2CommandSettings
{
    protected Msixvc2File Msixvc2 = default!;

    private static Stream OpenStream(string path)
        => path.StartsWith("http") 
            ? HttpFileStream.Open(path) 
            : File.OpenRead(path);

    private static bool CheckIfMsixvc2(string path)
    {
        using var stream = OpenStream(path);
        var header = (stackalloc byte[4]);
        stream.ReadExactly(header);

        return header.SequenceEqual("PK\x03\x04"u8);
    }

    protected void Initialize(Msixvc2CommandSettings settings)
    {
        Debug.Assert(settings.XvcPath != null);

        var path = settings.XvcPath;
        if (!CheckIfMsixvc2(path))
        {
            throw new NotSupportedException("File is not a MSIXVC2.");
        }

        Msixvc2 = new Msixvc2File(OpenStream(path));

        var contentKey = settings.ContentKey != null ? Convert.FromBase64String(settings.ContentKey) : null;
        var versionKey = settings.VersionKey != null ? Convert.FromBase64String(settings.VersionKey) : null;
        Msixvc2.SubmitKeys(contentKey, versionKey);

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