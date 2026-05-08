#nullable enable
using System.Collections.Generic;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public sealed record File(
    int Id, 
    int ChunkId, 
    PackagingIV? IV, 
    long Length, 
    PackagingHash Hash,
    bool ReadProtected,
    List<SegmentReference>? Segments
) : IRootSerialize
{
    public string OpcPath => $"/Files/{Id}.cbor";
    public string OpcRelationship => "http://xbox.com/MSIXVC2/File";

    public void Serialize(CborWriter writer)
    {
        PackagingIV? iv = default;
        Serialize(writer, ref iv, true);
    }

    public void Serialize(CborWriter writer, ref PackagingIV? initialIV, bool isStandaloneSerialize)
    {
        writer.WriteStartMap(3 
                             + (isStandaloneSerialize && ChunkId != 0 ? 1 : 0) 
                             + (IV.HasValue ? 1 : 0)
                             + (ReadProtected ? 1 : 0)
                             + (Segments != null ? 1 : 0));

        writer.WriteLabel(SerializedLabel.Id);
        writer.WriteInt32(Id);

        if (isStandaloneSerialize && ChunkId != 0)
        {
            writer.WriteLabel(SerializedLabel.ChunkId);
            writer.WriteInt32(ChunkId);
        }

        if (IV is {} iv)
        {
            writer.WriteLabel(SerializedLabel.InitialIV);
            iv.Serialize(writer);
        }

        writer.WriteLabel(SerializedLabel.Length);
        writer.WriteInt64(Length);

        writer.WriteLabel(SerializedLabel.Hash);
        writer.WriteHash(Hash);

        writer.WriteLabel(SerializedLabel.ReadProtected);
        writer.WriteBoolean(ReadProtected);

        if (Segments != null)
        {
            writer.WriteLabel(SerializedLabel.Segments);
            writer.WriteStartArray(Segments.Count);
            foreach (var segment in Segments)
            {
                segment.Serialize(writer);
            }
            writer.WriteEndArray();
        }

        writer.WriteEndMap();
    }

    public static File Deserialize(CborReader reader, ref PackagingIV? initialIV)
    {
        int id = default;
        int chunkId = default;
        PackagingIV? iv = default;
        long length = default;
        PackagingHash hash = default;
        bool readProtected = default;
        List<SegmentReference>? segments = default;

        var count = reader.ReadStartMap();

        while (count-- != 0)
        {
            var key = reader.ReadLabel();
            switch (key)
            {
                case SerializedLabel.Id:
                    id = reader.ReadInt32();
                    break;
                case SerializedLabel.ChunkId:
                    chunkId = reader.ReadInt32();
                    break;
                case SerializedLabel.InitialIV:
                    iv = PackagingIV.Deserialize(reader);
                    break;
                case SerializedLabel.Length:
                    length = reader.ReadInt64();
                    break;
                case SerializedLabel.Hash:
                    hash = reader.ReadHash();
                    break;
                case SerializedLabel.ReadProtected:
                    readProtected = reader.ReadBoolean();
                    break;
                case SerializedLabel.Segments:
                    segments = [];
                    var segmentCount = reader.ReadStartArray();
                    while (segmentCount-- != 0)
                    {
                        segments.Add(SegmentReference.Deserialize(reader, ref initialIV));
                    }
                    reader.ReadEndArray();
                    break;
                default:
                    reader.AssertInvalidValue();
                    break;
            }
        }

        reader.ReadEndMap();

        if (initialIV != null)
        {
            initialIV = iv;
        }

        return new File(id, chunkId, iv, length, hash, readProtected, segments);

    }
}