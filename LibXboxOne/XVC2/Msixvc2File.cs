using System;
using System.Collections.Generic;
using System.Formats.Cbor;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
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
    private readonly Dictionary<BoxIndex, Box> _boxes = [];

    public static Msixvc2File FromPath(string path) => new(File.OpenRead(path));

    public Msixvc2File(Stream stream, bool leaveOpen = false)
    {
        _archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen);
        _package = ReadPackageMetadata();

        foreach (var chunk in _package.Chunks)
        {
            _chunks[chunk.Id] = chunk;
            _chunkDetails[chunk.Id] = ReadChunkDetails(chunk);
        }

        for (int i = 0; i < _package.Boxes.Count; i++)
        {
            var boxIndex = new BoxIndex(i);
            _boxes[boxIndex] = ReadBoxManifest(boxIndex);
        }
    }

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

    private Box ReadBoxManifest(BoxIndex index)
    {
        var entry = GetBoxEntryStream(index);

        entry.Position = 0;
        var header = (stackalloc byte[8]);
        entry.ReadExactly(header);

        if (!header.SequenceEqual("XBOXBOX\0"u8))
            throw new InvalidDataException("Invalid box header");

        entry.Position = entry.Length - 8 - 4;

        var manifestOffset = 0uL;
        var manifestLength = 0u;

        entry.ReadExactly(MemoryMarshal.AsBytes(new Span<ulong>(ref manifestOffset)));
        entry.ReadExactly(MemoryMarshal.AsBytes(new Span<uint>(ref manifestLength)));

        var manifestEndOffset = long.CreateChecked(manifestLength + manifestOffset);
        if (manifestEndOffset > entry.Length)
            throw new InvalidDataException("Invalid box metadata offset or length");

        entry.Position = long.CreateChecked(manifestOffset);
        var manifestContent = new byte[manifestLength];
        entry.ReadExactly(manifestContent);

        var sealLength = entry.Length - (manifestEndOffset + 8 + 4);
        var sealContent = new byte[sealLength];
        entry.ReadExactly(sealContent);

        var sealReader = new CborReader(sealContent);
        var seal = Seal.Deserialize(sealReader);
        if (seal.Target != CborTagEx.XVCB)
            throw new InvalidDataException("Seal did not seal box manifest");

        if (!ValidateHash(manifestContent, seal.Hash))
            throw new InvalidDataException("Failed to validate box manifest hash");

        var reader = new CborReader(manifestContent);
        var box = Box.Deserialize(reader);

        return box;
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