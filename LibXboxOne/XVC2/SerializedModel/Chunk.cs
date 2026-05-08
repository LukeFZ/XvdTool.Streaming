#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Cbor;
using LibXboxOne.XVC2.Specifiers;

namespace LibXboxOne.XVC2.SerializedModel;

public sealed record Chunk(
    IPackagingSpecifier? Specifier0,
    IPackagingSpecifier? Specifier1,
    IPackagingSpecifier? Specifier2, 
    int Id,
    long Value1,
    bool? Unknown0, 
    bool? Unknown1, 
    int Unknown2,
    int Unknown3,
    SegmentReference SegmentReference
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
                             + (Specifier0 != null ? 1 : 0)
                             + (Specifier1 != null ? 1 : 0)
                             + (Specifier2 != null ? 1 : 0)
                             + (Unknown0.HasValue ? 1 : 0)
                             + (Unknown1.HasValue ? 1 : 0)
                             + (Unknown2 != 0 ? 1 : 0)
                             + (Unknown3 != 0 ? 1 : 0));

        writer.WriteInt32(24);
        writer.WriteInt32(Id);

        if (Unknown0 is { } unknown0)
        {
            writer.WriteInt32(36);
            writer.WriteBoolean(unknown0);
        }

        if (Specifier0 != null)
        {
            writer.WriteInt32(30);
            SerializeSpecifier(writer, Specifier0);
        }

        if (Specifier1 != null)
        {
            writer.WriteInt32(31);
            SerializeSpecifier(writer, Specifier1);
        }

        if (Specifier2 != null)
        {
            writer.WriteInt32(32);
            SerializeSpecifier(writer, Specifier2);
        }

        if (Unknown1 is { } unknown1)
        {
            writer.WriteInt32(33);
            writer.WriteBoolean(unknown1);
        }

        if (Unknown2 != 0)
        {
            writer.WriteInt32(34);
            writer.WriteInt32(Unknown2);
        }

        writer.WriteInt32(2);
        writer.WriteInt64(Value1);

        if (Unknown3 != 0)
        {
            writer.WriteInt32(11);
            writer.WriteInt32(Unknown3);
        }

        writer.WriteInt32(26);
        SegmentReference.Serialize(writer);

        writer.WriteEndMap();
    }

    public static Chunk Deserialize(CborReader reader, ref PackagingIV? initialIV)
    {
        IPackagingSpecifier? specifier0 = default;
        IPackagingSpecifier? specifier1 = default;
        IPackagingSpecifier? specifier2 = default;
        bool? unknown0 = default;
        bool? unknown1 = default;
        int id = default;
        int unknown2 = default;
        long value1 = default;
        int unknown3 = default;
        SegmentReference? segmentReference = default;

        var count = reader.ReadStartMap();
        while (count-- != 0)
        {
            var key = reader.ReadInt32();
            switch (key)
            {
                case 24:
                    id = reader.ReadInt32();
                    break;
                case 36:
                    unknown0 = reader.ReadBoolean();
                    break;
                case 30:
                    specifier0 = DeserializeSpecifier(reader);
                    break;
                case 31:
                    specifier1 = DeserializeSpecifier(reader);
                    break;
                case 32:
                    specifier2 = DeserializeSpecifier(reader);
                    break;
                case 33:
                    unknown1 = reader.ReadBoolean();
                    break;
                case 34:
                    unknown2 = reader.ReadInt32();
                    break;
                case 2:
                    value1 = reader.ReadInt64();
                    break;
                case 11:
                    unknown3 = reader.ReadInt32();
                    break;
                case 26:
                    segmentReference = SegmentReference.Deserialize(reader, ref initialIV);
                    break;
                default:
                    reader.AssertInvalidValue();
                    break;
            }
        }
        reader.ReadEndMap();

        Debug.Assert(segmentReference != null);
        return new Chunk(specifier0, specifier1, specifier2, id, value1, unknown0, unknown1, unknown2, unknown3,
            segmentReference);
    }
}