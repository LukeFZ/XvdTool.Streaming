using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public sealed record Segmentation(SegmentationAlgorithm Algorithm, Dictionary<SerializedLabel, object> Labels, int Algorithm2) : ISerialize
{
    public void Serialize(CborWriter writer)
    {
        writer.WriteStartMap(2 + (Algorithm2 != 0x301 ? 1 : 0));

        writer.WriteInt32(259);
        writer.WriteInt32((int)Algorithm);

        writer.WriteInt32(266);
        writer.WriteMap(Labels);

        if (Algorithm2 != 0x301)
        {
            writer.WriteInt32(291);
            writer.WriteInt32(Algorithm2);
        }

        writer.WriteEndMap();
    }

    public static Segmentation Deserialize(CborReader reader)
    {
        SegmentationAlgorithm algorithm = 0;
        var labels = new Dictionary<SerializedLabel, object>();
        var algorithm2 = 0x301;

        var count = reader.ReadStartMap();
        while (count-- != 0)
        {
            var key = reader.ReadInt32();
            switch (key)
            {
                case 259:
                    algorithm = (SegmentationAlgorithm)reader.ReadInt32();
                    break;
                case 266:
                    labels = reader.ReadMap<SerializedLabel>();
                    break;
                case 291:
                    algorithm2 = reader.ReadInt32();
                    break;
                default:
                    Debug.Assert(false);
                    reader.SkipValue();
                    break;
            }
        }

        reader.ReadEndMap();

        return new Segmentation(algorithm, labels, algorithm2);
    }
}