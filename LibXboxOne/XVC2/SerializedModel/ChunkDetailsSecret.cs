#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public sealed record ChunkDetailsSecret(int Id, List<FileSecret> Files) : IRootSerialize
{
    public string OpcPath => $"/Chunks/{Id}-secret.cbor";
    public string OpcRelationship => "http://xbox.com/MSIXVC2/ChunkSecret";

    public void Serialize(CborWriter writer)
    {
        writer.WriteSelfDescribeTag(CborTagEx.XVCC);

        writer.WriteStartMap(2);

        writer.WriteLabel(SerializedLabel.Id);
        writer.WriteInt32(Id);

        writer.WriteLabel(SerializedLabel.Files);
        writer.WriteStartArray(Files.Count);
        foreach (var file in Files)
        {
            file.Serialize(writer);
        }
        writer.WriteEndArray();

        writer.WriteEndMap();
    }

    public static ChunkDetailsSecret Deserialize(CborReader reader)
    {
        reader.ReadSelfDescribeTag(CborTagEx.XVCC);

        int id = default;
        List<FileSecret>? files = default;

        var count = reader.ReadStartMap();

        while (count-- != 0)
        {
            var key = reader.ReadLabel();
            switch (key)
            {
                case SerializedLabel.Id:
                    id = reader.ReadInt32();
                    break;
                case SerializedLabel.Files:
                    files = [];
                    var fileCount = reader.ReadStartArray();
                    while (fileCount-- != 0)
                    {
                        files.Add(FileSecret.Deserialize(reader));
                    }
                    reader.ReadEndArray();

                    break;
                default:
                    reader.AssertInvalidValue();
                    break;
            }
        }

        reader.ReadEndMap();

        Debug.Assert(files != null);
        return new ChunkDetailsSecret(id, files);
    }
}