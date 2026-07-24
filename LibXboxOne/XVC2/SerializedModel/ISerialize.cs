using System.Formats.Cbor;

namespace LibXboxOne.XVC2.SerializedModel;

public interface ISerialize
{
    void Serialize(CborWriter writer);
}