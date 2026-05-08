using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public sealed record Segmentation(SegmentationAlgorithm Algorithm, Dictionary<SerializedLabel, object> Options, int HashAlgorithm) : ISerialize
{
    private static SerializedAlgorithm ToSerialized(SegmentationAlgorithm algorithm) => algorithm switch
    {
        SegmentationAlgorithm.FastCDC => SerializedAlgorithm.FastCDC,
        SegmentationAlgorithm.Fixed => SerializedAlgorithm.Fixed,
        _ => throw new UnreachableException()
    };

    private static SegmentationAlgorithm ToSegmentationAlgorithm(SerializedAlgorithm algorithm) => algorithm switch
    {
        SerializedAlgorithm.FastCDC => SegmentationAlgorithm.FastCDC,
        SerializedAlgorithm.Fixed => SegmentationAlgorithm.Fixed,
        _ => throw new UnreachableException()
    };

    public void Serialize(CborWriter writer)
    {
        writer.WriteStartMap(2 + (HashAlgorithm != 0x301 ? 1 : 0));

        writer.WriteLabel(SerializedLabel.Algorithm);
        writer.WriteEnum(ToSerialized(Algorithm));

        writer.WriteLabel(SerializedLabel.Options);
        writer.WriteMap(Options);

        if (HashAlgorithm != 0x301)
        {
            writer.WriteLabel(SerializedLabel.HashAlgorithm);
            writer.WriteInt32(HashAlgorithm);
        }

        writer.WriteEndMap();
    }

    public static Segmentation Deserialize(CborReader reader)
    {
        SegmentationAlgorithm algorithm = 0;
        var options = new Dictionary<SerializedLabel, object>();
        var hashAlgorithm = 0x301;

        var count = reader.ReadStartMap();
        while (count-- != 0)
        {
            var key = reader.ReadLabel();
            switch (key)
            {
                case SerializedLabel.Algorithm:
                    algorithm = ToSegmentationAlgorithm(reader.ReadEnum<SerializedAlgorithm>());
                    break;
                case SerializedLabel.Options:
                    options = reader.ReadMap<SerializedLabel>();
                    break;
                case SerializedLabel.HashAlgorithm:
                    hashAlgorithm = reader.ReadInt32();
                    break;
                default:
                    reader.AssertInvalidValue();
                    break;
            }
        }

        reader.ReadEndMap();

        return new Segmentation(algorithm, options, hashAlgorithm);
    }
}