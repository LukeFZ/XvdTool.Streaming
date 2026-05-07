#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public record Package(
    FileFormat FileFormat, 
    Guid ContentId, 
    Version Version, 
    PackagingIV? InitialIV, 
    List<PackageKey> Keys, 
    Segmentation Segmentation, 
    List<BoxReference> Boxes,
    List<Chunk> Chunks,
    Guid FulfillmentContentId,
    Guid ProductId,
    Version MinimumSystemVersion,
    string StoreId,
    SerializedPlatform SupportedPlatforms
) : IRootSerialize
{
    public string OpcPath => "/XbocPackage.cbor";
    public string OpcRelationship => "http://xbox.com/MSIXVC2/Package";

    public void Serialize(CborWriter writer)
    {
        writer.WriteSelfDescribeTag(CborTagEx.XVCP);

        writer.WriteStartMap(11 + (InitialIV != null ? 1 : 0) + (Keys.Count > 0 ? 1 : 0));

        writer.WriteInt32(256);
        FileFormat.Serialize(writer);

        if (InitialIV is {} initialIV)
        {
            writer.WriteInt32(27);
            writer.WriteByteString(initialIV.ToArray());
        }

        writer.WriteInt32(260);
        writer.WriteTextString(ContentId.ToString());

        writer.WriteInt32(279);
        writer.WriteTextString(FulfillmentContentId.ToString());

        writer.WriteInt32(280);
        writer.WriteTextString(ProductId.ToString());

        writer.WriteInt32(261);
        Version.Serialize(writer);

        writer.WriteInt32(281);
        MinimumSystemVersion.Serialize(writer);

        writer.WriteInt32(282);
        writer.WriteTextString(StoreId);

        writer.WriteInt32(283);
        writer.WriteUInt32((uint)SupportedPlatforms);

        writer.WriteInt32(263);
        Segmentation.Serialize(writer);

        if (Keys.Count > 0)
        {
            writer.WriteInt32(262);
            writer.WriteStartArray(Keys.Count);
            foreach (var key in Keys)
            {
                key.Serialize(writer);
            }
            writer.WriteEndArray();
        }

        writer.WriteInt32(264);
        writer.WriteStartArray(Boxes.Count);
        foreach (var box in Boxes)
        {
            box.Serialize(writer);
        }
        writer.WriteEndArray();

        writer.WriteInt32(265);
        writer.WriteStartArray(Chunks.Count);
        foreach (var chunk in Chunks)
        {
            chunk.Serialize(writer);
        }
        writer.WriteEndArray();

        writer.WriteEndMap();
    }

    public static Package Deserialize(CborReader reader)
    {
        FileFormat? fileFormat = default;
        Guid contentId = default;
        Version version = default;
        PackagingIV? initialIV = default;
        List<PackageKey> keys = [];
        Segmentation? segmentation = default;
        List<BoxReference>? boxes = default;
        List<Chunk>? chunks = default;
        Guid fulfillmentContentId = default;
        Guid productId = default;
        Version minimumSystemVersion = default;
        string? storeId = default;
        SerializedPlatform supportedPlatforms = 0;

        reader.ReadSelfDescribeTag(CborTagEx.XVCP);

        var count = reader.ReadStartMap();
        while (count-- != 0)
        {
            var key = reader.ReadInt32();
            switch (key)
            {
                case 256:
                    fileFormat = FileFormat.Deserialize(reader);
                    break;
                case 27:
                    initialIV = PackagingIV.FromBytes(reader.ReadByteString());
                    break;
                case 260:
                    contentId = Guid.Parse(reader.ReadTextString());
                    break;
                case 279:
                    fulfillmentContentId = Guid.Parse(reader.ReadTextString());
                    break;
                case 280:
                    productId = Guid.Parse(reader.ReadTextString());
                    break;
                case 261:
                    version = Version.Deserialize(reader);
                    break;
                case 281:
                    minimumSystemVersion = Version.Deserialize(reader);
                    break;
                case 282:
                    storeId = reader.ReadTextString();
                    break;
                case 283:
                    supportedPlatforms = (SerializedPlatform)reader.ReadUInt32();
                    break;
                case 263:
                    segmentation = Segmentation.Deserialize(reader);
                    break;
                case 262:
                    var keyCount = reader.ReadStartArray();
                    keys = new List<PackageKey>(keyCount ?? 0);
                    while (keyCount-- != 0)
                    {
                        keys.Add(PackageKey.Deserialize(reader));
                    }
                    reader.ReadEndArray();
                    break;
                case 264:
                    var boxCount = reader.ReadStartArray();
                    boxes = new List<BoxReference>(boxCount ?? 0);
                    while (boxCount-- != 0)
                    {
                        boxes.Add(BoxReference.Deserialize(reader));
                    }
                    reader.ReadEndArray();
                    break;
                case 265:
                    var chunkCount = reader.ReadStartArray();
                    chunks = new List<Chunk>(chunkCount ?? 0);
                    while (chunkCount-- != 0)
                    {
                        chunks.Add(Chunk.Deserialize(reader, ref initialIV));
                    }
                    reader.ReadEndArray();
                    break;
                default:
                    Debug.Assert(false);
                    reader.SkipValue();
                    break;
            }
        }
        reader.ReadEndMap();

        Debug.Assert(fileFormat != null && segmentation != null && boxes != null && chunks != null && storeId != null);
        return new Package(fileFormat, contentId, version, initialIV, keys, segmentation, boxes, chunks,
            fulfillmentContentId, productId, minimumSystemVersion, storeId, supportedPlatforms);

    }
}