// source: https://github.com/Thealexbarney/LibHac/blob/master/src/LibHac/Common/Buffer.cs

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace XvdTool.Streaming.Crypto;

/// <summary>
/// Represents a buffer of 16 bytes.
/// Contains functions that assist with common operations on small buffers.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 16)]
public struct Buffer16
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private ulong _dummy0;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private ulong _dummy1;

    public byte this[int i]
    {
        get => Bytes[i];
        set => Bytes[i] = value;
    }

    [UnscopedRef] public Span<byte> Bytes => SpanHelpers.AsByteSpan(ref this);

    // Prevent a defensive copy by changing the read-only in reference to a reference with Unsafe.AsRef()
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Span<byte>(in Buffer16 value)
    {
        return SpanHelpers.AsByteSpan(ref Unsafe.AsRef(in value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<byte>(in Buffer16 value)
    {
        return SpanHelpers.AsReadOnlyByteSpan(in value);
    }

    [UnscopedRef, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T As<T>() where T : unmanaged
    {
        if (Unsafe.SizeOf<T>() > (uint)Unsafe.SizeOf<Buffer16>())
        {
            throw new ArgumentException();
        }

        return ref MemoryMarshal.GetReference(AsSpan<T>());
    }

    [UnscopedRef, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan<T>() where T : unmanaged
    {
        return SpanHelpers.AsSpan<Buffer16, T>(ref this);
    }

    [UnscopedRef, MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlySpan<T> AsReadOnlySpan<T>() where T : unmanaged
    {
        return SpanHelpers.AsReadOnlySpan<Buffer16, T>(in this);
    }
}

// source: https://github.com/Thealexbarney/LibHac/blob/master/src/LibHac/Common/SpanHelpers

file class SpanHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> CreateSpan<T>(ref T reference, int length)
    {
        return MemoryMarshal.CreateSpan(ref reference, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<TSpan> AsSpan<TStruct, TSpan>(ref TStruct reference)
        where TStruct : unmanaged where TSpan : unmanaged
    {
        return CreateSpan(ref Unsafe.As<TStruct, TSpan>(ref reference),
            Unsafe.SizeOf<TStruct>() / Unsafe.SizeOf<TSpan>());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<byte> AsByteSpan<T>(ref T reference) where T : unmanaged
    {
        return CreateSpan(ref Unsafe.As<T, byte>(ref reference), Unsafe.SizeOf<T>());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> CreateReadOnlySpan<T>(in T reference, int length)
    {
        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in reference), length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(in T reference) where T : unmanaged
    {
        return new ReadOnlySpan<T>(in reference);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<TSpan> AsReadOnlySpan<TStruct, TSpan>(in TStruct reference)
        where TStruct : unmanaged where TSpan : unmanaged
    {
        return CreateReadOnlySpan(in Unsafe.As<TStruct, TSpan>(ref Unsafe.AsRef(in reference)),
            Unsafe.SizeOf<TStruct>() / Unsafe.SizeOf<TSpan>());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> AsReadOnlyByteSpan<T>(in T reference) where T : unmanaged
    {
        return CreateReadOnlySpan(in Unsafe.As<T, byte>(ref Unsafe.AsRef(in reference)), Unsafe.SizeOf<T>());
    }
}
