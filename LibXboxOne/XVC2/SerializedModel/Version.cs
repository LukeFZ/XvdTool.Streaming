using System;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public record struct Version(
    ushort Major, 
    ushort Minor, 
    ushort Build, 
    ushort Revision, 
    Guid BuildId, 
    Guid? OriginalBuildId
) : ISerialize
{
    public override string ToString() => $"{Major}.{Minor}.{Build}.{Revision}.{BuildId}";

    public void Serialize(CborWriter writer)
    {
        writer.WriteStartMap(5 + (OriginalBuildId.HasValue ? 1 : 0));

        writer.WriteLabel(SerializedLabel.MajorVersion);
        writer.WriteUInt32(Major);

        writer.WriteLabel(SerializedLabel.MinorVersion);
        writer.WriteUInt32(Minor);

        writer.WriteLabel(SerializedLabel.Build);
        writer.WriteUInt32(Build);

        writer.WriteLabel(SerializedLabel.Revision);
        writer.WriteUInt32(Revision);

        writer.WriteLabel(SerializedLabel.BuildId);
        writer.WriteGuid(BuildId);

        if (OriginalBuildId is { } value)
        {
            writer.WriteLabel(SerializedLabel.OriginalBuildId);
            writer.WriteGuid(value);
        }

        writer.WriteEndMap();
    }

    public static Version Deserialize(CborReader reader)
    {
        ushort major = default;
        ushort minor = default;
        ushort build = default;
        ushort revision = default;
        Guid buildId = default;
        Guid? originalBuildId = default;

        var remaining = reader.ReadStartMap();
        while (remaining-- != 0)
        {
            var key = reader.ReadLabel();
            switch (key)
            {
                case SerializedLabel.MajorVersion:
                    major = (ushort)reader.ReadUInt32();
                    break;
                case SerializedLabel.MinorVersion:
                    minor = (ushort)reader.ReadUInt32();
                    break;
                case SerializedLabel.Build:
                    build = (ushort)reader.ReadUInt32();
                    break;
                case SerializedLabel.Revision:
                    revision = (ushort)reader.ReadUInt32();
                    break;
                case SerializedLabel.BuildId:
                    buildId = reader.ReadGuid();
                    break;
                case SerializedLabel.OriginalBuildId:
                    originalBuildId = reader.ReadGuid();
                    break;
                default:
                    reader.AssertInvalidValue();
                    break;
            }
        }

        reader.ReadEndMap();

        return new Version(major, minor, build, revision, buildId, originalBuildId);
    }
}