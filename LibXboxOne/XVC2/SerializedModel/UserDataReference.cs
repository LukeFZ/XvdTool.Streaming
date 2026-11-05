#nullable enable
using System.Diagnostics;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public sealed record UserDataReference(
    string Name
)
{
    public void Serialize(CborWriter writer)
    {
        writer.WriteStartMap(1);

        writer.WriteLabel(SerializedLabel.Name);
        writer.WriteTextString(Name);

        writer.WriteEndMap();
    }

    public static UserDataReference Deserialize(CborReader reader)
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
        return new UserDataReference(name);
    }
}