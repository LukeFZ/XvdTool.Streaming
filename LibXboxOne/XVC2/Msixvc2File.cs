using System;
using System.Formats.Cbor;
using System.IO;
using System.IO.Compression;
using LibXboxOne.XVC2.SerializedModel;

namespace LibXboxOne.XVC2;

public sealed class Msixvc2File : IDisposable
{
    private readonly ZipArchive _archive;
    private readonly Package _package;


    public Msixvc2File(Stream stream, bool leaveOpen = false)
    {
        _archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen);
        _package = ReadPackageMetadata();
    }

    public static Msixvc2File FromPath(string path) => new(File.OpenRead(path));

    private Package ReadPackageMetadata()
    {
        var metadataCbor = GetEntryContent("XboxPackage.cbor");
        var reader = new CborReader(metadataCbor);
        return Package.Deserialize(reader);
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