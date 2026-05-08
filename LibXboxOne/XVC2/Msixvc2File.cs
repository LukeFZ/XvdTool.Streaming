using System;
using System.Collections.Generic;
using System.Formats.Cbor;
using System.IO;
using System.IO.Compression;
using LibXboxOne.XVC2.SerializedModel;
using File = System.IO.File;

namespace LibXboxOne.XVC2;

public sealed partial class Msixvc2File : IDisposable
{
    private readonly ZipArchive _archive;
    private readonly Package _package;

    private readonly Dictionary<int, Chunk> _chunks = [];
    private readonly Dictionary<int, ChunkDetails> _chunkDetails = [];
    private readonly Dictionary<int, ChunkDetailsSecret> _chunkSecrets = [];
    private readonly Dictionary<string, XVC2.SerializedModel.File> _files = [];


    public Msixvc2File(Stream stream, bool leaveOpen = false)
    {
        _archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen);
        _package = ReadPackageMetadata();

        foreach (var chunk in _package.Chunks)
        {
            _chunks[chunk.Id] = chunk;
            _chunkDetails[chunk.Id] = ReadChunkDetails(chunk);
        }
    }

    public static Msixvc2File FromPath(string path) => new(File.OpenRead(path));

    public void LoadFileNames()
    {
        foreach (var chunk in _chunks.Values)
        {
            var chunkSecretContent = GetSegmentContent(chunk.SecretReference, chunk.KeyIndex);
            var reader = new CborReader(chunkSecretContent);
            _chunkSecrets[chunk.Id] = ChunkDetailsSecret.Deserialize(reader);

            for (int i = 0; i < _chunkDetails[chunk.Id].Files.Count; i++)
            {
                var file = _chunkDetails[chunk.Id].Files[i];
                var fileName = _chunkSecrets[chunk.Id].Files[i].FileName;
                _files[fileName] = file with { ChunkId = chunk.Id };
            }
        }
    }

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