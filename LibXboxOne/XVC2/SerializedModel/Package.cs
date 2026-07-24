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

        writer.WriteLabel(SerializedLabel.FileFormat);
        FileFormat.Serialize(writer);

        if (InitialIV is {} initialIV)
        {
            writer.WriteLabel(SerializedLabel.InitialIV);
            initialIV.Serialize(writer);
        }

        writer.WriteLabel(SerializedLabel.ContentId);
        writer.WriteGuid(ContentId);

        writer.WriteLabel(SerializedLabel.FulfillmentContentId);
        writer.WriteGuid(FulfillmentContentId);

        writer.WriteLabel(SerializedLabel.ProductId);
        writer.WriteGuid(ProductId);

        writer.WriteLabel(SerializedLabel.Version);
        Version.Serialize(writer);

        writer.WriteLabel(SerializedLabel.MinimumSystemVersion);
        MinimumSystemVersion.Serialize(writer);

        writer.WriteLabel(SerializedLabel.StoreId);
        writer.WriteTextString(StoreId);

        writer.WriteLabel(SerializedLabel.SupportedPlatforms);
        writer.WriteEnum(SupportedPlatforms);

        writer.WriteLabel(SerializedLabel.Segmentation);
        Segmentation.Serialize(writer);

        if (Keys.Count > 0)
        {
            writer.WriteLabel(SerializedLabel.Keys);
            writer.WriteStartArray(Keys.Count);
            foreach (var key in Keys)
            {
                key.Serialize(writer);
            }
            writer.WriteEndArray();
        }

        writer.WriteLabel(SerializedLabel.Boxes);
        writer.WriteStartArray(Boxes.Count);
        foreach (var box in Boxes)
        {
            box.Serialize(writer);
        }
        writer.WriteEndArray();

        writer.WriteLabel(SerializedLabel.Chunks);
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
        SerializedPlatform supportedPlatforms = default;

        reader.ReadSelfDescribeTag(CborTagEx.XVCP);

        var count = reader.ReadStartMap();
        while (count-- != 0)
        {
            var key = reader.ReadLabel();
            switch (key)
            {
                case SerializedLabel.FileFormat:
                    fileFormat = FileFormat.Deserialize(reader);
                    break;
                case SerializedLabel.InitialIV:
                    initialIV = PackagingIV.FromBytes(reader.ReadByteString());
                    break;
                case SerializedLabel.ContentId:
                    contentId = reader.ReadGuid();
                    break;
                case SerializedLabel.FulfillmentContentId:
                    fulfillmentContentId = reader.ReadGuid();
                    break;
                case SerializedLabel.ProductId:
                    productId = reader.ReadGuid();
                    break;
                case SerializedLabel.Version:
                    version = Version.Deserialize(reader);
                    break;
                case SerializedLabel.MinimumSystemVersion:
                    minimumSystemVersion = Version.Deserialize(reader);
                    break;
                case SerializedLabel.StoreId:
                    storeId = reader.ReadTextString();
                    break;
                case SerializedLabel.SupportedPlatforms:
                    supportedPlatforms = (SerializedPlatform)reader.ReadUInt32();
                    break;
                case SerializedLabel.Segmentation:
                    segmentation = Segmentation.Deserialize(reader);
                    break;
                case SerializedLabel.Keys:
                    var keyCount = reader.ReadStartArray();
                    keys = new List<PackageKey>(keyCount ?? 0);
                    while (keyCount-- != 0)
                    {
                        keys.Add(PackageKey.Deserialize(reader));
                    }
                    reader.ReadEndArray();
                    break;
                case SerializedLabel.Boxes:
                    var boxCount = reader.ReadStartArray();
                    boxes = new List<BoxReference>(boxCount ?? 0);
                    while (boxCount-- != 0)
                    {
                        boxes.Add(BoxReference.Deserialize(reader));
                    }
                    reader.ReadEndArray();
                    break;
                case SerializedLabel.Chunks:
                    var chunkCount = reader.ReadStartArray();
                    chunks = new List<Chunk>(chunkCount ?? 0);
                    while (chunkCount-- != 0)
                    {
                        chunks.Add(Chunk.Deserialize(reader, ref initialIV));
                    }
                    reader.ReadEndArray();
                    break;
                default:
                    reader.AssertInvalidValue();
                    break;
            }
        }
        reader.ReadEndMap();

        Debug.Assert(fileFormat != null && segmentation != null && boxes != null && chunks != null && storeId != null);
        return new Package(fileFormat, contentId, version, initialIV, keys, segmentation, boxes, chunks,
            fulfillmentContentId, productId, minimumSystemVersion, storeId, supportedPlatforms);

    }
}