#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public record FileFormat(List<string> WrittenBy, int MajorVersion, int MinorVersion, int Build) : ISerialize
{
    public System.Version Version => new(MajorVersion, MinorVersion);

    public override string ToString()
        => $"{MajorVersion}.{MinorVersion}.{Build} ({WrittenBy})";

    public void Serialize(CborWriter writer)
    {
        writer.WriteStartMap(2 
                             + (Build > 0 ? 1 : 0) 
                             + (WrittenBy.Count > 0 ? 1 : 0));

        writer.WriteLabel(SerializedLabel.MajorVersion);
        writer.WriteInt32(MajorVersion);

        writer.WriteLabel(SerializedLabel.MinorVersion);
        writer.WriteInt32(MinorVersion);

        if (Build > 0)
        {
            writer.WriteLabel(SerializedLabel.Build);
            writer.WriteInt32(Build);
        }

        if (WrittenBy.Count > 0)
        {
            writer.WriteLabel(SerializedLabel.WrittenBy);
            writer.WriteStartArray(WrittenBy.Count);
            foreach (var writtenBy in WrittenBy)
            {
                writer.WriteTextString(writtenBy);
            }
            writer.WriteEndArray();
        }

        writer.WriteEndMap();
    }

    public static FileFormat Deserialize(CborReader reader)
    {
        int major = default;
        int minor = default;
        int build = default;
        List<string> writtenBy = [];

        var remaining = reader.ReadStartMap();
        while (remaining-- != 0)
        {
            var key = reader.ReadLabel();
            switch (key)
            {
                case SerializedLabel.MajorVersion:
                    major = reader.ReadInt32();
                    break;
                case SerializedLabel.MinorVersion:
                    minor = reader.ReadInt32();
                    break;
                case SerializedLabel.Build:
                    build = reader.ReadInt32();
                    break;
                case SerializedLabel.WrittenBy:
                    writtenBy = [];
                    var count = reader.ReadStartArray();
                    while (count-- != 0)
                    {
                        writtenBy.Add(reader.ReadTextString());
                    }
                    reader.ReadEndArray();
                    break;
                default:
                    reader.SkipValue();
                    break;
            }
        }

        reader.ReadEndMap();

        return new FileFormat(writtenBy, major, minor, build);
    }
}