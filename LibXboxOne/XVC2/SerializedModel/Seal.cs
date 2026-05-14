using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public sealed record Seal(CborTagEx Target, PackagingHash Hash) : ISerialize
{
    public void Serialize(CborWriter writer)
    {
        writer.WriteSelfDescribeTag(CborTagEx.XVCZ);

        writer.WriteStartMap(2);
        
        writer.WriteLabel(SerializedLabel.Target);
        writer.WriteEnum(Target);

        writer.WriteLabel(SerializedLabel.Hash);
        writer.WriteHash(Hash);

        writer.WriteEndMap();
    }

    public static Seal Deserialize(CborReader reader)
    {
        CborTagEx target = default;
        PackagingHash hash = default;

        reader.ReadSelfDescribeTag(CborTagEx.XVCZ);

        var count = reader.ReadStartMap();
        while (count-- != 0)
        {
            var key = reader.ReadLabel();
            switch (key)
            {
                case SerializedLabel.Target:
                    target = reader.ReadEnum<CborTagEx>();
                    break;
                case SerializedLabel.Hash:
                    hash = reader.ReadHash();
                    break;
                default:
                    reader.AssertInvalidValue();
                    break;
            }
        }

        reader.ReadEndMap();
        return new Seal(target, hash);
    }
}