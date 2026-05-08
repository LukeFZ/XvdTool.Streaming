#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using LibXboxOne.XVC2.SerializedModel;

namespace LibXboxOne.XVC2;

public partial class Msixvc2File
{
    private readonly Dictionary<BoxIndex, Stream> _cachedBoxEntryStreams = [];

    public byte[] GetFileContent(string filePath)
    {
        var file = _files[filePath];
        var chunk = _chunks[file.ChunkId];

        var fileContent = new byte[file.Length];
        var currentOffset = 0;

        if (file.Segments != null)
        {
            foreach (var segment in file.Segments)
            {
                ReadSegmentContent(segment, chunk.KeyIndex, fileContent.AsSpan(currentOffset, segment.Length), PackagingKeyPurpose.Content);
                currentOffset += segment.Length;
            }
        }

        if (!ValidateHash(fileContent, file.Hash))
            throw new InvalidDataException("Failed to validate file hash");

        return fileContent;
    }

    public byte[] GetSegmentContent(SegmentReference segment, int keyId, PackagingKeyPurpose purpose)
    {
        var content = new byte[segment.Length];
        ReadSegmentContent(segment, keyId, content, purpose);
        return content;
    }

    public void ReadSegmentContent(SegmentReference segment, int keyId, Span<byte> content, PackagingKeyPurpose purpose)
    {
        var boxContent = new byte[segment.BoxLength];

        ReadBoxContent(segment.BoxIndex, segment.BoxOffset, boxContent);

        if (!ValidateHash(boxContent, segment.BoxHash))
            throw new InvalidDataException("Failed to verify box content hash");

        if (segment.EncryptionKey != null || segment.WrappedKey != null)
        {
            var decrypted = new byte[segment.BoxLength];

            DecryptContent(boxContent, segment.Hash.Hash, decrypted, keyId, segment.EncryptionKey, segment.WrappedKey,
                segment.WrapIV, purpose);

            boxContent = decrypted;
        }

        if (segment.Compression != PackagingCompression.None)
        {
            var compressed = boxContent.AsSpan(0, segment.CompressedLength);
            DecompressContent(compressed, content, segment.Compression);
        }
        else
        {
            boxContent.AsSpan(0, segment.Length).CopyTo(content);
        }

        if (!ValidateHash(content, segment.Hash))
            throw new InvalidDataException("Failed to verify decompressed content hash");
    }

    private static void DecompressContent(ReadOnlySpan<byte> compressed, Span<byte> decompressed,
        PackagingCompression compression)
    {
        switch (compression)
        {
            case PackagingCompression.Deflate:
            {
                unsafe
                {
                    fixed (byte* compressedPtr = compressed, decompressedPtr = decompressed)
                    {
                        using var input = new UnmanagedMemoryStream(compressedPtr, compressed.Length);
                        using var deflateStream = new DeflateStream(input, CompressionMode.Decompress);
                        using var output = new UnmanagedMemoryStream(decompressedPtr, decompressed.Length);
                        deflateStream.CopyTo(output);
                    }
                }
                break;
            }
            case PackagingCompression.Brotli:
                if (!BrotliDecoder.TryDecompress(compressed, decompressed, out _))
                    throw new InvalidDataException("Brotli decompression failed");

                break;
            default:
                throw new UnreachableException();
        }
    }

    private static bool ValidateHash(ReadOnlySpan<byte> content, PackagingHash hash)
    {
        switch (hash.Algorithm)
        {
            case PackagingHashAlgorithm.None:
                return true;
            case PackagingHashAlgorithm.SHA256:
                return SHA256.HashData(content).SequenceEqual(hash.Hash);
            case PackagingHashAlgorithm.SHA384:
                return SHA384.HashData(content).SequenceEqual(hash.Hash);
            case PackagingHashAlgorithm.SHA512:
                return SHA512.HashData(content).SequenceEqual(hash.Hash);
            default:
                Debug.Assert(false);
                return false;
        }
    }

    private void ReadBoxContent(BoxIndex boxIndex, int offset, Span<byte> content)
    {
        var stream = GetBoxEntryStream(boxIndex);
        stream.Seek(offset, SeekOrigin.Begin);
        stream.ReadExactly(content);
    }

    private Stream GetBoxEntryStream(BoxIndex boxIndex)
    {
        if (_cachedBoxEntryStreams.TryGetValue(boxIndex, out var cachedStream))
            return cachedStream;

        var boxName = _package.Boxes[boxIndex.Value].Name;
        var boxPath = $"Boxes/{boxName}";

        var content = GetEntryContent(boxPath);
        var ms = new MemoryStream(content);

        _cachedBoxEntryStreams[boxIndex] = ms;
        return ms;
    }
}