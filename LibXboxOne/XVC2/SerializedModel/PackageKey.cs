using System.Collections.Generic;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public sealed record PackageKey(List<PackageKeySource> Sources) : ISerialize
{
    public void Serialize(CborWriter writer)
    {
        writer.WriteStartArray(Sources.Count);
        foreach (var source in Sources)
        {
            source.Serialize(writer);
        }
        writer.WriteEndArray();
    }

    public static PackageKey Deserialize(CborReader reader)
    {
        var count = reader.ReadStartArray();
        var sources = new List<PackageKeySource>(count ?? 0);
        while (count-- != 0)
        {
            sources.Add(PackageKeySource.Deserialize(reader));
        }
        reader.ReadEndArray();

        return new PackageKey(sources);
    }
}