using System.Formats.Cbor;
using LibXboxOne.XVC2.SerializedModel;

namespace LibXboxOne.XVC2;

public readonly record struct BoxIndex(int Value) : ISerialize
{
    public override string ToString() => $"box:{Value}";

    public void Serialize(CborWriter writer)
    {
        writer.WriteInt32(Value);
    }

    public static BoxIndex Deserialize(CborReader reader) => new(reader.ReadInt32());
}