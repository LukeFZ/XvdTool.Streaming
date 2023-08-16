using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using LibXboxOne;
using LibXboxOne.Keys;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Spectre.Console;
using XvdTool.Streaming.Crypto;
using Aes = System.Security.Cryptography.Aes;

namespace XvdTool.Streaming;

public class KeyManager
{
    private AesWrapEngine? _uwpKeyWrapEngine;

    private readonly Dictionary<Guid, KeyEntry> _keys = new();
    private readonly Dictionary<OdkIndex, byte[]> _odks = new();

    private const string AppName = "xvdtool"; // Use the same name as xvdtool to support the same directories

    private const string OdkDirectory = "Odk";
    private const string CikDirectory = "Cik";
    private const string LicenseDirectory = "License";

    public void LoadDeviceKey(byte[] deviceKey)
    {
        _uwpKeyWrapEngine = new AesWrapEngine();
        _uwpKeyWrapEngine.Init(false, new KeyParameter(deviceKey));
    }

    public bool TryGetKey(Guid keyId, out KeyEntry entry)
    {
        return _keys.TryGetValue(keyId, out entry);
    }

    public void LoadCachedKeys()
    {
        var globalBaseDirectory = AppDirs.GetApplicationConfigDirectory(AppName);
        LoadFromDirectory(globalBaseDirectory);

        var currentBaseDirectory = Directory.GetCurrentDirectory();
        LoadFromDirectory(currentBaseDirectory);
    }

    private void LoadFromDirectory(string directory)
    {
        var odkPath = Path.Combine(directory, OdkDirectory);
        Directory.CreateDirectory(odkPath);

        foreach (var file in Directory.GetFiles(odkPath, "*.odk", SearchOption.TopDirectoryOnly))
        {
            var filename = Path.GetFileNameWithoutExtension(file);

            if (!Enum.TryParse(filename, true, out OdkIndex odkIndex))
            {
                if (!int.TryParse(filename, out var odkId))
                {
                    ConsoleLogger.WriteWarnLine($"Could not get ODK index from file [white]{file}[/]. Skipping.");
                    continue;
                }

                odkIndex = (OdkIndex) odkId;
            }

            var odk = File.ReadAllBytes(file);

            if (odk.Length != 0x20)
            {
                ConsoleLogger.WriteWarnLine($"ODK file [white]{file}[/] was not 0x20 bytes in length, assuming corrupted. Skipping.");
                continue;
            }

            _odks[odkIndex] = odk;
        }

        var cikPath = Path.Combine(directory, CikDirectory);
        Directory.CreateDirectory(cikPath);

        foreach (var file in Directory.GetFiles(cikPath, "*.cik", SearchOption.TopDirectoryOnly))
        {
            var entry = LoadCik(file);
            if (entry.Id != Guid.Empty)
            {
                _keys[entry.Id] = entry;
            }
        }

        var licensePath = Path.Combine(directory, LicenseDirectory);
        Directory.CreateDirectory(licensePath);

        foreach (var file in Directory.GetFiles(licensePath, "*.*", SearchOption.TopDirectoryOnly))
        {
            LoadLicense(file);
        }
    }

    public KeyEntry LoadCik(string path)
    {
        var cikContents = File.ReadAllBytes(path).AsSpan();
        if (cikContents.Length != KeyEntry.Size)
        {
            ConsoleLogger.WriteWarnLine($"CIK file [white]{path}[/] has an unexpected length of [white bold]0x{cikContents.Length}[/], assuming corrupted. Skipping.");
            return default;
        }

        return new KeyEntry(cikContents);
    }

    public void LoadLicense(string path)
    {
        var xmlLicense = File.ReadAllText(path);
        try
        {
            var document = XDocument.Parse(xmlLicense);

            var uwpSpLicenseBlock =
                document.Descendants(
                        XName.Get("SPLicenseBlock", "urn:schemas-microsoft-com:windows:store:licensing:ls"))
                    .FirstOrDefault();

            if (uwpSpLicenseBlock != null)
            {
                var spLicense = new XvcLicenseBlock(Convert.FromBase64String(uwpSpLicenseBlock.Value));

                var contentKeyBlock = spLicense.GetBlockWithId(XvcLicenseBlockId.PackedContentKeys);
                if (contentKeyBlock != null)
                {
                    if (_uwpKeyWrapEngine != null)
                    {
                        using var keyBlockReader = new BinaryReader(new MemoryStream(contentKeyBlock.BlockData));

                        while (keyBlockReader.BaseStream.Position != keyBlockReader.BaseStream.Length)
                        {
                            var keyIdSize = keyBlockReader.ReadUInt16();
                            Debug.Assert(keyIdSize == 0x20, "keyIdSize == 0x20");

                            var keyEntrySize = keyBlockReader.ReadUInt16();
                            Debug.Assert(keyEntrySize == 0x28, "keyEntrySize == 0x28");

                            var keyId1 = new Guid(keyBlockReader.ReadBytes(0x10));
                            var keyId2 = new Guid(keyBlockReader.ReadBytes(0x10));

                            var packedKey = keyBlockReader.ReadBytes(0x28);

                            var unwrappedKey = _uwpKeyWrapEngine.Unwrap(packedKey, 0, packedKey.Length).AsSpan();

                            _keys[keyId1] = new KeyEntry(keyId1, unwrappedKey);
                            _keys[keyId2] = new KeyEntry(keyId2, unwrappedKey);
                        }
                    }
                    else
                    {
                        ConsoleLogger.WriteWarnLine(
                            $"SPLicenseBlock from file [white]{path}[/] contained a content key, but no device key was provided for decryption. Skipping.");
                    }
                }
                else
                {
                    ConsoleLogger.WriteWarnLine(
                        $"SPLicenseBlock from file [white]{path}[/] did not contain a content key. Skipping.");
                }
            }
            else
            {
                // This might be an xbox license

                const string xboxNamespace = "http://schemas.microsoft.com/xboxlive/security/clas/LicResp/v1";

                var xboxLicense = document.Descendants(XName.Get("SignedLicense", xboxNamespace)).FirstOrDefault();
                if (xboxLicense == null)
                {
                    ConsoleLogger.WriteWarnLine(
                        $"License file [white]{path}[/] did not contain any known license type, assuming corrupted. Skipping.");

                    return;
                }

                var xboxSignedLicenseString = Encoding.UTF8.GetString(Convert.FromBase64String(xboxLicense.Value));
                var xboxSignedLicense = XDocument.Parse(xboxSignedLicenseString);

                var spLicenseBlock = xboxSignedLicense.Descendants(XName.Get("SPLicenseBlock", xboxNamespace)).FirstOrDefault();
                if (spLicenseBlock == null)
                {
                    ConsoleLogger.WriteWarnLine(
                        $"Xbox license file [white]{path}[/] did not contain a SPLicenseBlock, assuming corrupted. Skipping.");

                    return;
                }

                var spLicense = new XvcLicenseBlock(Convert.FromBase64String(spLicenseBlock.Value));

                var keyIdBlock = spLicense.GetBlockWithId(XvcLicenseBlockId.KeyId);
                if (keyIdBlock == null)
                {
                    ConsoleLogger.WriteWarnLine(
                        $"SPLicenseBlock from file [white]{path}[/] did not contain a key ID. Skipping.");

                    return;
                }

                var encryptedCikBlock = spLicense.GetBlockWithId(XvcLicenseBlockId.EncryptedCik);

                if (encryptedCikBlock == null)
                {
                    ConsoleLogger.WriteWarnLine(
                        $"SPLicenseBlock from file [white]{path}[/] did not contain an encrypted CIK. Skipping.");

                    return;
                }

                var uplinkKeyIdBlock = spLicense.GetBlockWithId(XvcLicenseBlockId.UplinkKeyId);
                if (uplinkKeyIdBlock == null)
                {
                    ConsoleLogger.WriteWarnLine(
                        $"SPLicenseBlock from file [white]{path}[/] did not contain an uplink key ID. Skipping.");

                    return;
                }

                byte[] cikEncryptionKey;

                if (uplinkKeyIdBlock.BlockData.Skip(1).Sum(x => x) == 0)
                {
                    var odkId = (OdkIndex)uplinkKeyIdBlock.BlockData[0];

                    if (!_odks.TryGetValue(odkId, out var odk))
                    {
                        ConsoleLogger.WriteWarnLine(
                            $"Xbox license from file [white]{path}[/] contains a CIK that is encrypted by ODK [white]{odkId}[/] which is not currently loaded. Skipping.");

                        return;
                    }

                    cikEncryptionKey = odk;
                }
                else
                {
                    var uplinkKeyId = new Guid(uplinkKeyIdBlock.BlockData);

                    ConsoleLogger.WriteWarnLine(
                        $"Xbox license from file [white]{path}[/] contains a CIK that is encrypted by the key [white]{uplinkKeyId}[/]. Skipping as this is not currently supported.");

                    return;
                }

                var keyId = new Guid(keyIdBlock.BlockData);

                var aes = Aes.Create();
                aes.Key = cikEncryptionKey;
                aes.IV = new byte[16];
                
                var cik = aes.DecryptEcb(encryptedCikBlock.BlockData, PaddingMode.None);

                _keys[keyId] = new KeyEntry(keyId, cik);
            }
        }
        catch (Exception ex)
        {
            ConsoleLogger.WriteWarnLine($"License file [white]{path}[/] was not valid XML, assuming corrupted. Skipping");
            #if DEBUG
            AnsiConsole.WriteException(ex);
            #endif
        }
    }
}

public struct KeyEntry
{
    public const int Size = 0x30;

    public readonly Guid Id;
    public readonly Buffer16 TweakKey;
    public readonly Buffer16 DataKey;

    public KeyEntry(ReadOnlySpan<byte> cik)
    {
        Debug.Assert(cik.Length == 0x30, "Invalid cik length (!= 0x30)");
        Debug.Assert(BitConverter.IsLittleEndian);

        Id = new Guid(cik[..0x10]);
        TweakKey = MemoryMarshal.Read<Buffer16>(cik[0x10..0x20]);
        DataKey = MemoryMarshal.Read<Buffer16>(cik[0x20..]);
    }

    public KeyEntry(Guid id, ReadOnlySpan<byte> key)
    {
        Debug.Assert(key.Length != 0x20, "Invalid key length (!= 0x20)");
        Debug.Assert(BitConverter.IsLittleEndian);

        Id = id;
        TweakKey = MemoryMarshal.Read<Buffer16>(key[..0x10]);
        DataKey = MemoryMarshal.Read<Buffer16>(key[0x10..]);
    }

    public void Save(string path)
    {
        Debug.Assert(BitConverter.IsLittleEndian);

        using var fileStream = File.OpenWrite(path);
        
        var buffer = (stackalloc byte[Size]);
        MemoryMarshal.Write(buffer, ref this);

        fileStream.Write(buffer);
    }
}