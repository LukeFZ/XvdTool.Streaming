#nullable enable
using System.Diagnostics;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public sealed record PackageKeySource(
    string SourceKeyId,
    PackagingKeyPurpose SourcePurpose, 
    PackagingDerivationAlgorithm DerivationAlgorithm,
    byte[] KdfContext,
    PackagingEncryptionAlgorithm WrapAlgorithm,
    byte[]? WrapIV, // unverified
    byte[] WrappedKey,
    PackagingEncryptionAlgorithm Algorithm

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
        writer.WriteStartMap(7 + (WrapIV != null ? 1 : 0));

        writer.WriteLabel(SerializedLabel.SourcePurpose);
        writer.WriteEnum(ToSerialized(SourcePurpose));

        writer.WriteLabel(SerializedLabel.SourceKeyId);
        writer.WriteTextString(SourceKeyId);

        writer.WriteLabel(SerializedLabel.DerivationAlgorithm);
        writer.WriteEnum(ToSerialized(DerivationAlgorithm));

        writer.WriteLabel(SerializedLabel.KdfContext);
        writer.WriteByteString(KdfContext);

        writer.WriteLabel(SerializedLabel.WrapAlgorithm);
        writer.WriteEnum(ToSerialized(WrapAlgorithm));

        if (WrapIV != null)
        {
            writer.WriteLabel(SerializedLabel.WrapIV);
            writer.WriteByteString(WrapIV);
        }

        writer.WriteLabel(SerializedLabel.WrappedKey);
        writer.WriteByteString(WrappedKey);

        writer.WriteLabel(SerializedLabel.Algorithm);
        writer.WriteEnum(ToSerialized(Algorithm));

        writer.WriteEndMap();
    }

    public static PackageKeySource Deserialize(CborReader reader)
    {
        string? sourceKeyId = default;
        PackagingKeyPurpose keyPurpose = default;
        PackagingDerivationAlgorithm derivationAlgorithm = default;
        PackagingEncryptionAlgorithm wrapAlgorithm = default;
        PackagingEncryptionAlgorithm algorithm = default;
        byte[]? kdfContext = default;
        byte[]? wrapIV = default;
        byte[]? wrappedKey = default;

        var count = reader.ReadStartMap();
        while (count-- != 0)
        {
            var key = reader.ReadLabel();
            switch (key)
            {
                case SerializedLabel.SourcePurpose:
                    keyPurpose = ToKeyPurpose(reader.ReadEnum<SerializedKeyPurpose>());
                    break;
                case SerializedLabel.SourceKeyId:
                    sourceKeyId = reader.ReadTextString();
                    break;
                case SerializedLabel.DerivationAlgorithm:
                    derivationAlgorithm = ToDerivationAlgorithm(reader.ReadEnum<SerializedAlgorithm>());
                    break;
                case SerializedLabel.KdfContext:
                    kdfContext = reader.ReadByteString();
                    break;
                case SerializedLabel.WrapAlgorithm:
                    wrapAlgorithm = ToEncryptionAlgorithm(reader.ReadEnum<SerializedAlgorithm>());
                    break;
                case SerializedLabel.WrapIV:
                    wrapIV = reader.ReadByteString();
                    break;
                case SerializedLabel.WrappedKey:
                    wrappedKey = reader.ReadByteString();
                    break;
                case SerializedLabel.Algorithm:
                    algorithm = ToEncryptionAlgorithm(reader.ReadEnum<SerializedAlgorithm>());
                    break;
                default:
                    reader.AssertInvalidValue();
                    break;
            }
        }
        reader.ReadEndMap();

        Debug.Assert(sourceKeyId != null && kdfContext != null && wrappedKey != null);
        return new PackageKeySource(sourceKeyId, keyPurpose, derivationAlgorithm, kdfContext, wrapAlgorithm,
            wrapIV, wrappedKey, algorithm);
    }
}