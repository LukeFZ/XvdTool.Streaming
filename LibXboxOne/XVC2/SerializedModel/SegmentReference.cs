#nullable enable
using System.Diagnostics;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public sealed record SegmentReference(
    PackagingHash Hash, 
    int Length, 
    PackagingCompression Compression,
    int CompressedLength,
    byte[]? EncryptionKey,
    byte[]? WrappedKey,
    PackagingIV? WrapIV,
    PackagingHash? BoxHash,
    BoxIndex? BoxIndex,
    int BoxOffset,
    int BoxLength,
    bool Secondary
)
{
    private static SerializedAlgorithm ToSerialized(PackagingCompression compression) => compression switch
    {
        PackagingCompression.None => SerializedAlgorithm.None,
        PackagingCompression.Deflate => SerializedAlgorithm.Deflate,
        PackagingCompression.Brotli => SerializedAlgorithm.Brotli,
        _ => throw new UnreachableException()
    };

    private static PackagingCompression ToCompression(SerializedAlgorithm algorithm) => algorithm switch
    {
        SerializedAlgorithm.None => PackagingCompression.None,
        SerializedAlgorithm.Deflate => PackagingCompression.Deflate,
        SerializedAlgorithm.Brotli => PackagingCompression.Brotli,
        _ => throw new UnreachableException()
    };

    public void Serialize(CborWriter writer)
    {
        writer.WriteStartMap(2 
                             + (Compression != 0 ? 2 : 0) 
                             + (EncryptionKey != null ? 1 : 0)
                             + (WrappedKey != null ? 1 : 0)
                             + (BoxHash.HasValue ? 1 : 0)
                             + (BoxIndex.HasValue ? 1 : 0)
                             + (BoxOffset != 0 ? 1 : 0)
                             + (BoxLength != 0 ? 1 : 0)
                             + (Secondary ? 1 : 0));

        writer.WriteLabel(SerializedLabel.Hash);
        writer.WriteHash(Hash);

        writer.WriteLabel(SerializedLabel.Length);
        writer.WriteInt32(Length);

        if (Compression != 0)
        {
            writer.WriteLabel(SerializedLabel.Compression);
            writer.WriteEnum(ToSerialized(Compression));

            writer.WriteLabel(SerializedLabel.CompressedLength);
            writer.WriteInt32(CompressedLength);
        }

        if (EncryptionKey != null)
        {
            writer.WriteLabel(SerializedLabel.EncryptionKey);
            writer.WriteByteString(EncryptionKey);
        }

        if (WrappedKey != null)
        {
            writer.WriteLabel(SerializedLabel.WrappedKey);
            writer.WriteByteString(WrappedKey);
        }

        // 7 is probably WrapIV? is this used?

        if (BoxHash is { } boxHash)
        {
            writer.WriteLabel(SerializedLabel.BoxHash);
            writer.WriteHash(boxHash);
        }

        if (BoxIndex is { } boxIndex)
        {
            writer.WriteLabel(SerializedLabel.BoxIndex);
            boxIndex.Serialize(writer);
        }

        if (BoxOffset != 0)
        {
            writer.WriteLabel(SerializedLabel.BoxOffset);
            writer.WriteInt32(BoxOffset);
        }

        if (BoxLength != 0)
        {
            writer.WriteLabel(SerializedLabel.BoxLength);
            writer.WriteInt32(BoxLength);
        }

        if (Secondary)
        {
            writer.WriteLabel(SerializedLabel.Secondary);
            writer.WriteBoolean(Secondary);
        }

        writer.WriteEndMap();
    }

    public static SegmentReference Deserialize(CborReader reader, ref PackagingIV? initialIV)
    {
        PackagingHash hash = default;
        int length = default;
        PackagingCompression compression = default;
        int compressedLength = default;
        byte[]? encryptionKey = default;
        byte[]? wrappedKey = default;
        PackagingIV? wrapIV = default;
        PackagingHash? boxHash = default;
        BoxIndex? boxIndex = default;
        int boxOffset = default;
        int boxLength = default;
        bool secondary = default;

        var count = reader.ReadStartMap();
        while (count-- != 0)
        {
            var key = reader.ReadLabel();
            switch (key)
            {
                case SerializedLabel.Hash:
                    hash = reader.ReadHash();
                    break;
                case SerializedLabel.Length:
                    length = reader.ReadInt32();
                    break;
                case SerializedLabel.Compression:
                    compression = ToCompression(reader.ReadEnum<SerializedAlgorithm>());
                    break;
                case SerializedLabel.CompressedLength:
                    compressedLength = reader.ReadInt32();
                    break;
                case SerializedLabel.EncryptionKey:
                    encryptionKey = reader.ReadByteString();
                    break;
                case SerializedLabel.WrappedKey:
                    wrappedKey = reader.ReadByteString();

                    Debug.Assert(initialIV.HasValue);
                    wrapIV = initialIV;
                    initialIV = wrapIV.Value.Increment();
                    break;
                //case 7:
                //    // This is not set in normal references - the iv is gotten from the initial iv
                //    wrapIV = PackagingIV.FromBytes(reader.ReadByteString());
                //    break;
                case SerializedLabel.BoxHash:
                    boxHash = reader.ReadHash();
                    break;
                case SerializedLabel.BoxIndex:
                    boxIndex = XVC2.BoxIndex.Deserialize(reader);
                    break;
                case SerializedLabel.BoxOffset:
                    boxOffset = reader.ReadInt32();
                    break;
                case SerializedLabel.BoxLength:
                    boxLength = reader.ReadInt32();
                    break;
                case SerializedLabel.Secondary:
                    secondary = reader.ReadBoolean();
                    break;
                default:
                    reader.AssertInvalidValue();
                    break;
            }
        }

        reader.ReadEndMap();
        return new SegmentReference(hash, length, compression, compressedLength, encryptionKey, wrappedKey, wrapIV,
            boxHash, boxIndex, boxOffset, boxLength, secondary);
    }
}