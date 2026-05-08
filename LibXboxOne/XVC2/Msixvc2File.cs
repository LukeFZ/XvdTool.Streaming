using System;
using System.Collections.Generic;
using System.Formats.Cbor;
using System.IO;
using System.IO.Compression;
using LibXboxOne.XVC2.SerializedModel;
using File = System.IO.File;

namespace LibXboxOne.XVC2;

public sealed class Msixvc2File : IDisposable
{
    private readonly ZipArchive _archive;
    private readonly Package _package;
    private readonly Dictionary<int, ChunkDetails> _chunks;


    public Msixvc2File(Stream stream, bool leaveOpen = false)
    {
        _archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen);
        _package = ReadPackageMetadata();

        _chunks = [];
        foreach (var chunk in _package.Chunks)
        {
            _chunks[chunk.Id] = ReadChunkDetails(chunk);
        }
    }

    public static Msixvc2File FromPath(string path) => new(File.OpenRead(path));

    private Package ReadPackageMetadata()
    {
        var metadataCbor = GetEntryContent("XboxPackage.cbor");
        var reader = new CborReader(metadataCbor);
        return Package.Deserialize(reader);
    }

    private ChunkDetails ReadChunkDetails(Chunk chunk)
    {
        var path = $"Chunks/{chunk.Id}.cbor";
        var cbor = GetEntryContent(path);
        var reader = new CborReader(cbor);
        return ChunkDetails.Deserialize(reader);
    }

    private byte[] GetEntryContent(string entryPath)
    {
        var packageEntry = _archive.GetEntry(entryPath);
        if (packageEntry == null)
            throw new InvalidOperationException($"Failed to find entry {entryPath} in MSIXVC2");

        using var packageDataStream = packageEntry.Open();
        var ms = new MemoryStream();
        packageDataStream.CopyTo(ms);

        return ms.GetBuffer();
    }

    public void Dispose()
    {
        _archive.Dispose();
    }
}