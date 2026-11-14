#nullable enable
using System.Diagnostics;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public sealed record FileSecret(string FileName) : ISerialize
{
    public void Serialize(CborWriter writer)
    {
        writer.WriteStartMap(1);

        // I think this is a typo, and they meant to use SerializedLabel.FileName here
        writer.WriteLabel(SerializedLabel.Name);
        writer.WriteTextString(FileName);

        writer.WriteEndMap();
    }

    public static FileSecret Deserialize(CborReader reader)
    {
        string? name = default;

        var count = reader.ReadStartMap();

        while (count-- != 0)
        {
            var key = reader.ReadLabel();
            switch (key)
            {
                case SerializedLabel.Name:
                    name = reader.ReadTextString();
                    break;
                default:
                    reader.AssertInvalidValue();
                    break;
            }
        }

        reader.ReadEndMap();

        Debug.Assert(name != null);
        return new FileSecret(name);
    }
}