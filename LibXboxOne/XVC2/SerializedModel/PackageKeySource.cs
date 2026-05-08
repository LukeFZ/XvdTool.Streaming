#nullable enable
using System.Diagnostics;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public sealed record PackageKeySource(
    string SourceKeyId,
    PackagingKeyPurpose SourcePurpose, 
    PackagingDerivationAlgorithm DerivationAlgorithm,
    byte[] KdfContext,
    PackagingEncryptionAlgorithm EncryptionAlgorithm,
    byte[]? EncryptionKey, // unverified
    byte[] WrappedKey,
    PackagingEncryptionAlgorithm PackagingEncryptionAlgorithm2

) : ISerialize
{
    private static PackagingDerivationAlgorithm ToDerivationAlgorithm(SerializedAlgorithm algorithm) => algorithm switch
    {
        SerializedAlgorithm.None => PackagingDerivationAlgorithm.None,
        SerializedAlgorithm.SP800_108_HMAC_SHA256 => PackagingDerivationAlgorithm.SP800_108_HMAC_SHA256,
        _ => throw new UnreachableException()
    };

    private static PackagingEncryptionAlgorithm ToEncryptionAlgorithm(SerializedAlgorithm algorithm) => algorithm switch
    {
        SerializedAlgorithm.None => PackagingEncryptionAlgorithm.None,
        SerializedAlgorithm.AES_256_CBC => PackagingEncryptionAlgorithm.AES_256_CBC,
        SerializedAlgorithm.AES_256_KW => PackagingEncryptionAlgorithm.AES_256_KW,
        _ => throw new UnreachableException()
    };

    private static PackagingKeyPurpose ToKeyPurpose(SerializedKeyPurpose keyPurpose) => keyPurpose switch
    {
        SerializedKeyPurpose.Content => PackagingKeyPurpose.Content,
        SerializedKeyPurpose.Version => PackagingKeyPurpose.Version,
        SerializedKeyPurpose.PackageData => PackagingKeyPurpose.PackageData,
        _ => throw new UnreachableException()
    };

    private static SerializedAlgorithm ToSerialized(PackagingDerivationAlgorithm algorithm) => algorithm switch
    {
        PackagingDerivationAlgorithm.None => SerializedAlgorithm.None,
        PackagingDerivationAlgorithm.SP800_108_HMAC_SHA256 => SerializedAlgorithm.SP800_108_HMAC_SHA256,
        _ => throw new UnreachableException()
    };

    private static SerializedAlgorithm ToSerialized(PackagingEncryptionAlgorithm algorithm) => algorithm switch
    {
        PackagingEncryptionAlgorithm.None => SerializedAlgorithm.None,
        PackagingEncryptionAlgorithm.AES_256_CBC => SerializedAlgorithm.AES_256_CBC,
        PackagingEncryptionAlgorithm.AES_256_KW => SerializedAlgorithm.AES_256_KW,
        _ => throw new UnreachableException()
    };

    private static SerializedKeyPurpose ToSerialized(PackagingKeyPurpose keyPurpose) => keyPurpose switch
    {
        PackagingKeyPurpose.Content => SerializedKeyPurpose.Content,
        PackagingKeyPurpose.Version => SerializedKeyPurpose.Version,
        PackagingKeyPurpose.PackageData => SerializedKeyPurpose.PackageData,
        _ => throw new UnreachableException()
    };

    public void Serialize(CborWriter writer)
    {
        writer.WriteStartMap(7 + (EncryptionKey != null ? 1 : 0));

        writer.WriteInt32(287);
        writer.WriteEnum(ToSerialized(SourcePurpose));

        writer.WriteInt32(288);
        writer.WriteTextString(SourceKeyId);

        writer.WriteInt32(284);
        writer.WriteEnum(ToSerialized(DerivationAlgorithm));

        writer.WriteInt32(286);
        writer.WriteByteString(KdfContext);

        writer.WriteInt32(285);
        writer.WriteEnum(ToSerialized(EncryptionAlgorithm));

        if (EncryptionKey != null)
        {
            writer.WriteInt32(7);
            writer.WriteByteString(EncryptionKey);
        }

        writer.WriteInt32(6);
        writer.WriteByteString(WrappedKey);

        writer.WriteInt32(259);
        writer.WriteEnum(ToSerialized(PackagingEncryptionAlgorithm2));

        writer.WriteEndMap();
    }

    public static PackageKeySource Deserialize(CborReader reader)
    {
        string? sourceKeyId = default;
        PackagingKeyPurpose keyPurpose = default;
        PackagingDerivationAlgorithm derivationAlgorithm = default;
        PackagingEncryptionAlgorithm encryptionAlgorithm = default;
        PackagingEncryptionAlgorithm encryptionAlgorithm2 = default;
        byte[]? kdfContext = default;
        byte[]? encryptionKey = default;
        byte[]? wrappedKey = default;

        var count = reader.ReadStartMap();
        while (count-- != 0)
        {
            var key = reader.ReadInt32();
            switch (key)
            {
                case 287:
                    keyPurpose = ToKeyPurpose(reader.ReadEnum<SerializedKeyPurpose>());
                    break;
                case 288:
                    sourceKeyId = reader.ReadTextString();
                    break;
                case 284:
                    derivationAlgorithm = ToDerivationAlgorithm(reader.ReadEnum<SerializedAlgorithm>());
                    break;
                case 286:
                    kdfContext = reader.ReadByteString();
                    break;
                case 285:
                    encryptionAlgorithm = ToEncryptionAlgorithm(reader.ReadEnum<SerializedAlgorithm>());
                    break;
                case 7:
                    encryptionKey = reader.ReadByteString();
                    break;
                case 6:
                    wrappedKey = reader.ReadByteString();
                    break;
                case 259:
                    encryptionAlgorithm2 = ToEncryptionAlgorithm(reader.ReadEnum<SerializedAlgorithm>());
                    break;
                default:
                    reader.AssertInvalidValue();
                    break;
            }
        }
        reader.ReadEndMap();

        Debug.Assert(sourceKeyId != null && kdfContext != null && wrappedKey != null);
        return new PackageKeySource(sourceKeyId, keyPurpose, derivationAlgorithm, kdfContext, encryptionAlgorithm,
            encryptionKey, wrappedKey, encryptionAlgorithm2);
    }
}