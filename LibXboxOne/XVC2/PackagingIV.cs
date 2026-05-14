using System;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Formats.Cbor;

namespace LibXboxOne.XVC2;

public struct PackagingIV
{
    public const int Size = 16;

    private ulong _counter0;
    private ulong _counter1;

    public PackagingIV Increment()
    {
        var copy = new PackagingIV
        {
            _counter0 = _counter0, 
            _counter1 = _counter1
        };

        var prev = copy._counter0++;
        if (prev + 1 < copy._counter0)
            copy._counter1++;

        return copy;
    }

    public byte[] ToArray()
    {
        var array = new byte[16];
        BinaryPrimitives.WriteUInt64BigEndian(array, _counter1);
        BinaryPrimitives.WriteUInt64BigEndian(array.AsSpan(8), _counter0);
        return array;
    }

    public override string ToString() => Base64Url.EncodeToString(ToArray());

    public static PackagingIV FromBase64UrlString(string value)
    {
        var bytes = Base64Url.DecodeFromChars(value);
        return FromBytes(bytes);
    }

    public static PackagingIV FromBytes(ReadOnlySpan<byte> value)
    {
        return new PackagingIV
        {
            _counter1 = BinaryPrimitives.ReadUInt64BigEndian(value),
            _counter0 = BinaryPrimitives.ReadUInt64BigEndian(value[8..])
        };
    }

    public void Serialize(CborWriter writer)
    {
        writer.WriteByteString(ToArray());
    }

    public static PackagingIV Deserialize(CborReader reader)
    {
        return FromBytes(reader.ReadByteString());
    }
}