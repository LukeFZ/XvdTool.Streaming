#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public sealed record ChunkDetails(List<File> Files, int Id, PackagingIV? IV) : IRootSerialize
{
    public string OpcPath => $"/Chunks/{Id}.cbor";
    public string OpcRelationship => "http://xbox.com/MSIXVC2/Chunk";

    public void Serialize(CborWriter writer)
    {
        writer.WriteSelfDescribeTag(CborTagEx.XVCC);

        writer.WriteStartMap(2 + (IV.HasValue ? 1 : 0));

        writer.WriteLabel(SerializedLabel.Id);
        writer.WriteInt32(Id);

        if (IV is { } initialIV)
        {
            writer.WriteLabel(SerializedLabel.InitialIV);
            initialIV.Serialize(writer);
        }

        writer.WriteLabel(SerializedLabel.Files);
        writer.WriteStartArray(Files.Count);

        var iv = IV;
        foreach (var file in Files)
        {
            file.Serialize(writer, ref iv, false);
        }
        writer.WriteEndArray();

        writer.WriteEndMap();
    }

    public static ChunkDetails Deserialize(CborReader reader)
    {
        int id = default;
        PackagingIV? iv = default;
        List<File>? files = default;

        reader.ReadSelfDescribeTag(CborTagEx.XVCC);

        var count = reader.ReadStartMap();
        while (count-- != 0)
        {
            var key = reader.ReadLabel();
            switch (key)
            {
                case SerializedLabel.Id:
                    id = reader.ReadInt32();
                    break;
                case SerializedLabel.InitialIV:
                    iv = PackagingIV.Deserialize(reader);
                    break;
                case SerializedLabel.Files:
                    var fileCount = reader.ReadStartArray();
                    files = [];
                    while (fileCount-- != 0)
                    {
                        files.Add(File.Deserialize(reader, ref iv));
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
        return new ChunkDetails(files, id, iv);
    }
}