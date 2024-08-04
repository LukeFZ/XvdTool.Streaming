using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;

namespace XvdTool.Streaming.Commands;

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