#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public record FileFormat(List<string> Tags, int MajorVersion, int MinorVersion, int Patch) : ISerialize
{
    public System.Version Version => new(MajorVersion, MinorVersion);

    public override string ToString()
        => $"{MajorVersion}.{MinorVersion}.{Patch};";

    public void Serialize(CborWriter writer)
    {
        writer.WriteStartMap(2 + (Patch > 0 ? 1 : 0) + (Tags.Count > 0 ? 1 : 0));

        writer.WriteInt32(257);
        writer.WriteInt32(MajorVersion);

        writer.WriteInt32(258);
        writer.WriteInt32(MinorVersion);

        if (Patch > 0)
        {
            writer.WriteInt32(267);
            writer.WriteInt32(Patch);
        }

        if (Tags.Count > 0)
        {
            writer.WriteInt32(290);
            writer.WriteStartArray(Tags.Count);
            foreach (var tag in Tags)
                writer.WriteTextString(tag);

            writer.WriteEndArray();
        }

        writer.WriteEndMap();
    }

    public static FileFormat Deserialize(CborReader reader)
    {
        int? major = null, minor = null;
        var patch = 0;
        List<string> tags = [];

        var remaining = reader.ReadStartMap();
        while (remaining-- != 0)
        {
            var key = reader.ReadInt32();
            switch (key)
            {
                case 257:
                    major = reader.ReadInt32();
                    break;
                case 258:
                    minor = reader.ReadInt32();
                    break;
                case 267:
                    patch = reader.ReadInt32();
                    break;
                case 290:
                    tags = [];
                    var count = reader.ReadStartArray();
                    while (count-- != 0)
                    {
                        tags.Add(reader.ReadTextString());
                    }

                    reader.ReadEndArray();
                    break;
                default:
                    reader.SkipValue();
                    break;
            }
        }

        reader.ReadEndMap();

        Debug.Assert(major != null && minor != null);
        return new FileFormat(tags, major.Value, minor.Value, patch);
    }
}