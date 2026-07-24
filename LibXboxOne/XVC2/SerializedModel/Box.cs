#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public sealed record Box(FileFormat FileFormat, string Name, List<SegmentReference> Segments) : ISerialize
{
    public void Serialize(CborWriter writer)
    {
        writer.WriteSelfDescribeTag(CborTagEx.XVCB);

        writer.WriteStartMap(3);
        
        writer.WriteLabel(SerializedLabel.FileFormat);
        FileFormat.Serialize(writer);

        writer.WriteLabel(SerializedLabel.Name);
        writer.WriteTextString(Name);

        writer.WriteLabel(SerializedLabel.Segments);
        writer.WriteStartArray(Segments.Count);
        foreach (var segment in Segments)
        {
            segment.Serialize(writer);
        }
        writer.WriteEndArray();

        writer.WriteEndMap();
    }

    public static Box Deserialize(CborReader reader)
    {
        FileFormat? fileFormat = default;
        string? name = default;
        List<SegmentReference>? segments = default;

        reader.ReadSelfDescribeTag(CborTagEx.XVCB);

        var count = reader.ReadStartMap();
        while (count-- != 0)
        {
            var key = reader.ReadLabel();
            switch (key)
            {
                case SerializedLabel.FileFormat:
                    fileFormat = FileFormat.Deserialize(reader);
                    break;
                case SerializedLabel.Name:
                    name = reader.ReadTextString();
                    break;
                case SerializedLabel.Segments:
                    segments = [];
                    var segmentCount = reader.ReadStartArray();
                    PackagingIV? iv = default;
                    while (segmentCount-- != 0)
                    {
                        segments.Add(SegmentReference.Deserialize(reader, ref iv));
                    }
                    reader.ReadEndArray();
                    break;
                default:
                    reader.AssertInvalidValue();
                    break;
            }
        }

        reader.ReadEndMap();

        Debug.Assert(fileFormat != null && name != null && segments != null);
        return new Box(fileFormat, name, segments);
    }
}