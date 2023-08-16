using System.Runtime.InteropServices;

namespace XvdTool.Streaming;

public static class Extensions
{
    public static T ReadStruct<T>(this BinaryReader reader)
    {
        var size = Marshal.SizeOf(typeof(T));
        // Read in a byte array
        var bytes = reader.ReadBytes(size);

        // Pin the managed memory while, copy it out the data, then unpin it
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        var theStructure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T))!;
        handle.Free();

        return theStructure;
    }

    public static T[] ReadStructArray<T>(this BinaryReader reader, int count) where T : struct
    {
        var size = Marshal.SizeOf(typeof(T));
        // Read in a byte array
        var bytes = reader.ReadBytes(size * count);

        // Pin the managed memory while, copy it out the data, then unpin it
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        var handleAddr = handle.AddrOfPinnedObject();

        var array = new T[count];
        for (int i = 0; i < count; i++)
        {
            array[i] = (T) Marshal.PtrToStructure(handleAddr + size * i, typeof(T))!;
        }

        handle.Free();

        return array;
    }
}