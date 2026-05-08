#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Cbor;
using LibXboxOne.XVC2.Specifiers;

namespace LibXboxOne.XVC2.SerializedModel;

public sealed record Chunk(
    IPackagingSpecifier? Tags,
    IPackagingSpecifier? Languages,
    IPackagingSpecifier? Devices, 
    int Id,
    long Length,
    bool OnDemand, 
    bool RequiredToLaunch, 
    int KeyIndex,
    int BoxLength,
    SegmentReference SecretReference
) : ISerialize
{
    private static void SerializeSpecifier(CborWriter writer, IPackagingSpecifier specifier)
    {
        if (specifier is PackagingSpecifierValue packagingSpecifierValue)
        {
            writer.WriteTextString(packagingSpecifierValue.Value);
            return;
        }

        if (specifier is PackagingLogicalSpecifier logicalSpecifierValue)
        {
            writer.WriteTagEx(logicalSpecifierValue.Type switch
            {
                LogicalSpecifierType.Any => CborTagEx.LogicalAny,
                LogicalSpecifierType.All => CborTagEx.LogicalAll,
                _ => throw new UnreachableException()
            });
            
            writer.WriteStartArray(logicalSpecifierValue.Specifiers.Count);
            foreach (var subSpecifier in logicalSpecifierValue.Specifiers)
            {
                SerializeSpecifier(writer, subSpecifier);
            }
            writer.WriteEndArray();
            return;
        }

        Debug.Assert(false);
    }

    private static IPackagingSpecifier DeserializeSpecifier(CborReader reader)
    {
        var nextTag = reader.PeekState();
        if (nextTag == CborReaderState.TextString)
        {
            return new PackagingSpecifierValue(reader.ReadTextString());
        }

        if (nextTag == CborReaderState.Tag)
        {
            var tag = reader.ReadTagEx();
            if (tag is CborTagEx.LogicalAny or CborTagEx.LogicalAll)
            {
                var logicalSpecifier = tag switch
                {
                    CborTagEx.LogicalAny => LogicalSpecifierType.Any,
                    CborTagEx.LogicalAll => LogicalSpecifierType.All,
                    _ => throw new UnreachableException()
                };

                var subSpecifiers = new List<IPackagingSpecifier>();

                var count = reader.ReadStartArray();
                while (count-- != 0)
                {
                    subSpecifiers.Add(DeserializeSpecifier(reader));
                }
                reader.ReadEndArray();

                return new PackagingLogicalSpecifier(logicalSpecifier, subSpecifiers);
            }
        }

        throw new InvalidOperationException("Invalid packaging specifier");
    }

    public void Serialize(CborWriter writer)
    {
        writer.WriteStartMap(3 
                             + (Tags != null ? 1 : 0)
                             + (Languages != null ? 1 : 0)
                             + (Devices != null ? 1 : 0)
                             + (OnDemand ? 1 : 0)
                             + (RequiredToLaunch ? 1 : 0)
                             + (KeyIndex != 0 ? 1 : 0)
                             + (BoxLength != 0 ? 1 : 0));

        writer.WriteLabel(SerializedLabel.Id);
        writer.WriteInt32(Id);

        if (OnDemand)
        {
            writer.WriteLabel(SerializedLabel.OnDemand);
            writer.WriteBoolean(OnDemand);
        }

        if (Tags != null)
        {
            writer.WriteLabel(SerializedLabel.Tags);
            SerializeSpecifier(writer, Tags);
        }

        if (Languages != null)
        {
            writer.WriteLabel(SerializedLabel.Languages);
            SerializeSpecifier(writer, Languages);
        }

        if (Devices != null)
        {
            writer.WriteLabel(SerializedLabel.Devices);
            SerializeSpecifier(writer, Devices);
        }

        if (RequiredToLaunch)
        {
            writer.WriteLabel(SerializedLabel.RequiredToLaunch);
            writer.WriteBoolean(RequiredToLaunch);
        }

        if (KeyIndex != 0)
        {
            writer.WriteLabel(SerializedLabel.KeyIndex);
            writer.WriteInt32(KeyIndex);
        }

        writer.WriteLabel(SerializedLabel.Length);
        writer.WriteInt64(Length);

        if (BoxLength != 0)
        {
            writer.WriteLabel(SerializedLabel.BoxLength);
            writer.WriteInt32(BoxLength);
        }

        writer.WriteLabel(SerializedLabel.SecretReference);
        SecretReference.Serialize(writer);

        writer.WriteEndMap();
    }

    public static Chunk Deserialize(CborReader reader, ref PackagingIV? initialIV)
    {
        IPackagingSpecifier? tags = default;
        IPackagingSpecifier? languages = default;
        IPackagingSpecifier? devices = default;
        bool onDemand = default;
        bool requiredToLaunch = default;
        int id = default;
        int keyIndex = default;
        long length = default;
        int boxLength = default;
        SegmentReference? secretReference = default;

        var count = reader.ReadStartMap();
        while (count-- != 0)
        {
            var key = reader.ReadLabel();
            switch (key)
            {
                case SerializedLabel.Id:
                    id = reader.ReadInt32();
                    break;
                case SerializedLabel.OnDemand:
                    onDemand = reader.ReadBoolean();
                    break;
                case SerializedLabel.Tags:
                    tags = DeserializeSpecifier(reader);
                    break;
                case SerializedLabel.Languages:
                    languages = DeserializeSpecifier(reader);
                    break;
                case SerializedLabel.Devices:
                    devices = DeserializeSpecifier(reader);
                    break;
                case SerializedLabel.RequiredToLaunch:
                    requiredToLaunch = reader.ReadBoolean();
                    break;
                case SerializedLabel.KeyIndex:
                    keyIndex = reader.ReadInt32();
                    break;
                case SerializedLabel.Length:
                    length = reader.ReadInt64();
                    break;
                case SerializedLabel.BoxLength:
                    boxLength = reader.ReadInt32();
                    break;
                case SerializedLabel.SecretReference:
                    secretReference = SegmentReference.Deserialize(reader, ref initialIV);
                    break;
                default:
                    reader.AssertInvalidValue();
                    break;
            }
        }
        reader.ReadEndMap();

        Debug.Assert(secretReference != null);
        return new Chunk(tags, languages, devices, id, length, onDemand, requiredToLaunch, keyIndex, boxLength, secretReference);
    }
}