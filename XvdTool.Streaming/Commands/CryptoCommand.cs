using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;

namespace XvdTool.Streaming.Commands;

internal abstract class CryptoCommand<T> : XvdCommand<T> where T : CryptoCommandSettings
{
    protected KeyManager KeyManager = default!;

    protected bool Initialize(CryptoCommandSettings settings, out KeyEntry entry)
    {
        Initialize(settings, requiresWriting: true);

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