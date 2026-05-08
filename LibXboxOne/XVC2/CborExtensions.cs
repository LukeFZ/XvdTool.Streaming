using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Cbor;
using System.IO;
using LibXboxOne.XVC2.SerializedModel;

namespace LibXboxOne.XVC2;

public static class CborExtensions
{
    extension(CborWriter writer)
    {
        public void WriteTagEx(CborTagEx tag)
            => writer.WriteTag((CborTag)tag);

        public void WriteSelfDescribeTag(CborTagEx tag)
        {
            writer.WriteTag(CborTag.SelfDescribeCbor);
            writer.WriteTagEx(tag);
        }

        public void WriteHash(PackagingHash hash)
        {
            var tag = hash.Algorithm switch
            {
                PackagingHashAlgorithm.SHA256 => CborTagEx.SHA256,
                PackagingHashAlgorithm.SHA384 => CborTagEx.SHA384,
                PackagingHashAlgorithm.SHA512 => CborTagEx.SHA512,
                _ => throw new UnreachableException()
            };

            writer.WriteTagEx(tag);
            writer.WriteByteString(hash.Hash);
        }

        public void WriteMap<TKey>(Dictionary<TKey, object> map)
        {
            writer.WriteStartMap(map.Count);

            foreach (var (key, value) in map)
            {
                writer.WriteInt32((int)(object)key);

                switch (value)
                {
                    case string str:
                        writer.WriteTextString(str);
                        break;
                    case byte[] bytes:
                        writer.WriteByteString(bytes);
                        break;
                    case int i32:
                        writer.WriteInt32(i32);
                        break;
                    case long i64:
                        writer.WriteInt64(i64);
                        break;
                    case uint u32:
                        writer.WriteUInt32(u32);
                        break;
                    case ulong u64:
                        writer.WriteUInt64(u64);
                        break;
                    case bool boolean:
                        writer.WriteBoolean(boolean);
                        break;
                    default:
                        Debug.Assert(false);
                        break;
                }
            }

            writer.WriteEndMap();
        }

        public void WriteGuid(Guid value) 
            => writer.WriteTextString(value.ToString());

        public void WriteEnum<T>(T value) where T : Enum
            => writer.WriteInt32((int)(object)value);

        public void WriteLabel(SerializedLabel label)
            => writer.WriteInt32((int)label);
    }

    extension(CborReader reader)
    {
        public CborTagEx ReadTagEx()
            => (CborTagEx)reader.ReadTag();

        public void ReadSelfDescribeTag(CborTagEx tag)
        {
            var tag0 = reader.ReadTag();
            if (tag0 != CborTag.SelfDescribeCbor)
                throw new InvalidDataException();

            var tag1 = reader.ReadTagEx();
            if (tag1 != tag)
                throw new InvalidDataException();
        }

        public PackagingHash ReadHash()
        {
            var tagType = reader.ReadTagEx();
            var type = tagType switch
            {
                CborTagEx.SHA256 => PackagingHashAlgorithm.SHA256,
                CborTagEx.SHA384 => PackagingHashAlgorithm.SHA384,
                CborTagEx.SHA512 => PackagingHashAlgorithm.SHA512,
                _ => throw new UnreachableException()
            };

            var hash = reader.ReadByteString();
            return new PackagingHash(type, hash);
        }

        public Dictionary<TKey, object> ReadMap<TKey>()
        {
            var count = reader.ReadStartMap();

            var dict = new Dictionary<TKey, object>();
            while (count-- != 0)
            {
                var key = (TKey)(object)reader.ReadInt32();
                dict[key] = reader.PeekState() switch
                {
                    CborReaderState.TextString => reader.ReadTextString(),
                    CborReaderState.ByteString => reader.ReadByteString(),
                    CborReaderState.UnsignedInteger => reader.ReadUInt64(),
                    CborReaderState.NegativeInteger => reader.ReadInt64(),
                    CborReaderState.Boolean => reader.ReadBoolean(),
                    _ => throw new UnreachableException()
                };
            }

            reader.ReadEndMap();

            return dict;
        }

        public Guid ReadGuid()
            => Guid.Parse(reader.ReadTextString());

        public T ReadEnum<T>() where T : Enum
            => (T)(object)reader.ReadInt32();

        public void AssertInvalidValue()
        {
            Debug.Assert(false);
            reader.SkipValue();
        }

        public SerializedLabel ReadLabel()
            => (SerializedLabel)reader.ReadInt32();
    }
}