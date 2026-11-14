using LibXboxOne;
using System.Runtime.InteropServices;

namespace XvdTool.Streaming;

public static class Extensions
{
    extension(BinaryReader reader)
    {
        public T ReadStruct<T>()
        {
            var size = Marshal.SizeOf<T>();
            // Read in a byte array
            var bytes = reader.ReadBytes(size);

            // Pin the managed memory while, copy it out the data, then unpin it
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            var theStructure = Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject())!;
            handle.Free();

            return theStructure;
        }

        public T[] ReadStructArray<T>(int count) where T : struct
        {
            var size = Marshal.SizeOf<T>();
            // Read in a byte array
            var bytes = reader.ReadBytes(size * count);

            // Pin the managed memory while, copy it out the data, then unpin it
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            var handleAddr = handle.AddrOfPinnedObject();

            var array = new T[count];
            for (int i = 0; i < count; i++)
            {
                array[i] = Marshal.PtrToStructure<T>(handleAddr + size * i)!;
            }

            handle.Free();

            return array;
        }
    }

    extension(Stream)
    {
        public static Stream OpenFileStream(string path, bool writing)
        {
            if (path.StartsWith("http"))
            {
                if (writing)
                    throw new InvalidOperationException("Cannot write to a HTTP stream.");

                return HttpFileStream.Open(path);
            }

            return File.Open(path, FileMode.Open, writing ? FileAccess.ReadWrite : FileAccess.Read);
        }
    }
}