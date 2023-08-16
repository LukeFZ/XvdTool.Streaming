using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;

namespace XvdTool.Streaming;

internal class Program
{
    static void Main(string[] args)
    {
        var app = new CommandApp();
        app.Configure(config =>
        {
            //config.PropagateExceptions();

            config.AddCommand<InfoCommand>("info")
                .WithDescription("Prints information about a given file.")
                .WithExample("info", "c:/file.msixvc")
                .WithExample("info", "c:/file.msixvc", "-o log.txt")
                .WithExample("info", "https://assets1.xboxlive.com/...");

            config.AddCommand<ExtractCommand>("extract")
                .WithDescription("Decrypts and extracts the files contained in a given file.")
                .WithExample("extract", "c:/file.msixvc")
                .WithExample("extract", "c:/file.msixvc", "-o c:/output")
                .WithExample("extract", "https://assets1.xboxlive.com/...");

            config.AddCommand<VerifyCommand>("verify")
                .WithDescription("Checks the integrity of the given file. (Local only)")
                .WithExample("verify", "c:/file.msixvc");

            config.AddCommand<DecryptCommand>("decrypt")
                .WithDescription("Decrypts the given file. (Local only)")
                .WithExample("decrypt", "c:/file.msixvc");
        });

        app.Run(args);
    }
}

internal abstract class XvdCommandSettings : CommandSettings
{
    [Description("File Path / URL to the XVC.")]
    [CommandArgument(0, "<path/url>")]
    public string? XvcPath { get; init; }
}

internal abstract class CryptoCommandSettings : XvdCommandSettings
{
    [Description("Path to the .cik file to be used regardless of the header key ID.")]
    [CommandOption("-c|--cik")]
    public string? CikPath { get; init; }

    [Description("Device key used to decrypt UWP licenses.")]
    [CommandOption("-d|--device-key")]
    public string? DeviceKey { get; init; }
}

internal abstract class XvdCommand<T> : Command<T> where T : XvdCommandSettings
{
    protected StreamedXvdFile XvdFile = default!;

    protected void Initialize(XvdCommandSettings settings)
    {
        Debug.Assert(settings.XvcPath != null, "settings.XvcPath != null");

        var path = settings.XvcPath;

        XvdFile = path.StartsWith("http")
            ? StreamedXvdFile.OpenFromUrl(path)
            : StreamedXvdFile.OpenFromFile(path);

        XvdFile.Parse();
    }

    public override ValidationResult Validate(CommandContext context, T settings)
    {
        if (settings.XvcPath != null && !settings.XvcPath.StartsWith("http") && !File.Exists(settings.XvcPath))
            return ValidationResult.Error("Provided file does not exist.");

        return ValidationResult.Success();
    }
}

internal abstract class CryptoCommand<T> : XvdCommand<T> where T : CryptoCommandSettings
{
    protected KeyManager KeyManager = default!;

    protected bool Initialize(CryptoCommandSettings settings, out KeyEntry entry)
    {
        base.Initialize(settings);

        Debug.Assert(XvdFile != null, "XvdFile != null");

        entry = default;

        KeyManager = new KeyManager();

        if (settings.DeviceKey != null)
        {
            KeyManager.LoadDeviceKey(Convert.FromHexString(settings.DeviceKey));
        }

        if (settings.CikPath != null)
        {
            entry = KeyManager.LoadCik(settings.CikPath);
        }
        else
        {
            KeyManager.LoadCachedKeys();

            var keyId = XvdFile.GetKeyId();
            if (keyId != Guid.Empty)
            {
                if (!KeyManager.TryGetKey(keyId, out entry))
                {
                    ConsoleLogger.WriteErrLine($"Could not find key [bold]{keyId}[/] loaded in key storage.");

                    return false;
                }
            }
        }

        return true;
    }

    public override ValidationResult Validate(CommandContext context, T settings)
    {
        var result = base.Validate(context, settings);

        if (!result.Successful)
            return result;

        if (settings.CikPath != null && !File.Exists(settings.CikPath))
            return ValidationResult.Error("Provided .cik file does not exist.");

        if (settings.DeviceKey != null && (settings.DeviceKey.Length != 32 ||
                                           settings.DeviceKey.All("0123456789ABCDEFabcdef".Contains)))
            return ValidationResult.Error("Provided device key is invalid. Must be 32 hex characters long.");

        return ValidationResult.Success();
    }
}

internal sealed class InfoCommand : XvdCommand<InfoCommand.Settings>
{
    public sealed class Settings : XvdCommandSettings
    {
        [Description("File path to save the output into.")]
        [CommandOption("-o|--output")]
        public string? OutputPath { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        Initialize(settings);

        Debug.Assert(XvdFile != null, "XvdFile != null");

        using (XvdFile)
        {
            var infoOutput = XvdFile.PrintInfo();
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

internal sealed class ExtractCommand : CryptoCommand<ExtractCommand.Settings>
{
    public sealed class Settings : CryptoCommandSettings
    {
        [DefaultValue("output")]
        [Description("Output directory to extract the files into.")]
        [CommandOption("-o|--output")]
        public string? OutputDirectory { get; init; }

        [Description("List of regions to skip downloading. Defaults to none.")]
        [CommandOption("-r|--skip-region")]
        public uint[]? SkippedRegions { get; init; }

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
            XvdFile.ExtractFiles(outputPath, keyEntry, settings.SkipHashCheck, settings.SkippedRegions);
        }

        ConsoleLogger.WriteInfoLine("[green bold]Successfully[/] extracted files.");

        return 0;
    }
}

internal sealed class VerifyCommand : XvdCommand<VerifyCommand.Settings>
{
    public sealed class Settings : XvdCommandSettings;

    public override int Execute(CommandContext context, Settings settings)
    {
        Initialize(settings);

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

internal sealed class DecryptCommand : CryptoCommand<DecryptCommand.Settings>
{
    public sealed class Settings : CryptoCommandSettings
    {
        [Description("Skips recalculating the hashes after decryption.\nSpeeds up the process, but makes subsequent hash checks on the file fail.")]
        [CommandOption("-n|--no-hash-calc")]
        public bool SkipHashCalculation { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        if (!Initialize(settings, out var keyEntry))
        {
            return -1;
        }

        using (XvdFile)
        {
            XvdFile.DecryptData(keyEntry, false);
        }

        return 0;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        base.Validate(context, settings);

        Debug.Assert(settings.XvcPath != null, "settings.XvcPath != null");

        if (!settings.SkipHashCalculation)
            return ValidationResult.Error(
                "Hash recalculation is not yet supported. Please use the 'extract' command instead, or specify '--no-hash-calc' to skip recomputing the hash table.");

        if (settings.XvcPath.StartsWith("http"))
            return ValidationResult.Error("Only local files are supported for integrity verification.");

        if (!File.Exists(settings.XvcPath))
            return ValidationResult.Error("Provided file does not exist.");

        return ValidationResult.Success();
    }
}