#nullable enable
using System.Diagnostics;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public sealed record PackageKeySource(
    string SourceKeyId,
    int SourcePurpose, 
    int DerivationAlgorithm,
    byte[] KdfContext,
    int EncryptionAlgorithm,
    byte[]? Unknown1,
    byte[] WrappedKey,
    int PackagingEncryptionAlgorithm2

) : ISerialize
{
    public void Serialize(CborWriter writer)
    {
        writer.WriteStartMap(7 + (Unknown1 != null ? 1 : 0));

        writer.WriteInt32(287);
        writer.WriteInt32(SourcePurpose);

        writer.WriteInt32(288);
        writer.WriteTextString(SourceKeyId);

        writer.WriteInt32(284);
        writer.WriteInt32(DerivationAlgorithm);

        writer.WriteInt32(286);
        writer.WriteByteString(KdfContext);

        writer.WriteInt32(285);
        writer.WriteInt32(EncryptionAlgorithm);

        if (Unknown1 != null)
        {
            writer.WriteInt32(7);
            writer.WriteByteString(Unknown1);
        }

        writer.WriteInt32(6);
        writer.WriteByteString(WrappedKey);

        writer.WriteInt32(259);
        writer.WriteInt32(PackagingEncryptionAlgorithm2);

        writer.WriteEndMap();
    }

    public static PackageKeySource Deserialize(CborReader reader)
    {
        string? sourceKeyId = null;
        var keyPurpose = 0;
        var derivationAlgorithm = 0;
        var encryptionAlgorithm = 0;
        var encryptionAlgorithm2 = 0;
        byte[]? kdfContext = null;
        byte[]? unknown1 = null;
        byte[]? wrappedKey = null;

        var count = reader.ReadStartMap();
        while (count-- != 0)
        {
            var key = reader.ReadInt32();
            switch (key)
            {
                case 287:
                    keyPurpose = reader.ReadInt32();
                    break;
                case 288:
                    sourceKeyId = reader.ReadTextString();
                    break;
                case 284:
                    derivationAlgorithm = reader.ReadInt32();
                    break;
                case 286:
                    kdfContext = reader.ReadByteString();
                    break;
                case 285:
                    encryptionAlgorithm = reader.ReadInt32();
                    break;
                case 7:
                    unknown1 = reader.ReadByteString();
                    break;
                case 6:
                    wrappedKey = reader.ReadByteString();
                    break;
                case 259:
                    encryptionAlgorithm2 = reader.ReadInt32();
                    break;
                default:
                    Debug.Assert(false);
                    reader.SkipValue();
                    break;
            }
        }
        reader.ReadEndMap();

        Debug.Assert(sourceKeyId != null && kdfContext != null && wrappedKey != null);
        return new PackageKeySource(sourceKeyId, keyPurpose, derivationAlgorithm, kdfContext, encryptionAlgorithm,
            unknown1, wrappedKey, encryptionAlgorithm2);
    }
}