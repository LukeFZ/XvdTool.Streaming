#nullable enable
using System.Diagnostics;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public sealed record BoxReference(string Name) : ISerialize
{
    public void Serialize(CborWriter writer)
    {
        writer.WriteStartMap(1);
        writer.WriteInt32(25);
        writer.WriteTextString(Name);
        writer.WriteEndMap();
    }

    public static BoxReference Deserialize(CborReader reader)
    {
        string? name = null;

        var count = reader.ReadStartMap();
        while (count-- != 0)
        {
            var key = reader.ReadInt32();
            switch (key)
            {
                case 25:
                    name = reader.ReadTextString();
                    break;
                default:
                    reader.AssertInvalidValue();
                    break;
            }
        }
        reader.ReadEndMap();
        
        Debug.Assert(name != null);
        return new BoxReference(name);
    }
}