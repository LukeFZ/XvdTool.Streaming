using System;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public record struct Version(
    ushort Major, 
    ushort Minor, 
    ushort Patch, 
    ushort Build, 
    Guid Id, 
    Guid? Unknown
) : ISerialize
{
    public override string ToString() => $"{Major}.{Minor}.{Patch}.{Build}.{Id}";

    public void Serialize(CborWriter writer)
    {
        writer.WriteStartMap(5 + (Unknown.HasValue ? 1 : 0));

        writer.WriteInt32(257);
        writer.WriteUInt32(Major);

        writer.WriteInt32(258);
        writer.WriteUInt32(Minor);

        writer.WriteInt32(267);
        writer.WriteUInt32(Patch);

        writer.WriteInt32(268);
        writer.WriteUInt32(Build);

        writer.WriteInt32(269);
        writer.WriteGuid(Id);

        if (Unknown is { } value)
        {
            writer.WriteInt32(292);
            writer.WriteGuid(value);
        }

        writer.WriteEndMap();
    }

    public static Version Deserialize(CborReader reader)
    {
        ushort major = default;
        ushort minor = default;
        ushort patch = default;
        ushort build = default;
        Guid id = default;
        Guid? unknown = default;

        var remaining = reader.ReadStartMap();
        while (remaining-- != 0)
        {
            var key = reader.ReadInt32();
            switch (key)
            {
                case 257:
                    major = (ushort)reader.ReadUInt32();
                    break;
                case 258:
                    minor = (ushort)reader.ReadUInt32();
                    break;
                case 267:
                    patch = (ushort)reader.ReadUInt32();
                    break;
                case 268:
                    build = (ushort)reader.ReadUInt32();
                    break;
                case 269:
                    id = reader.ReadGuid();
                    break;
                case 292:
                    unknown = reader.ReadGuid();
                    break;
                default:
                    reader.AssertInvalidValue();
                    break;
            }
        }

        reader.ReadEndMap();

        return new Version(major, minor, patch, build, id, unknown);
    }
}