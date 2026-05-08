using LibXboxOne.XVC2.SerializedModel;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace LibXboxOne.XVC2;

public partial class Msixvc2File
{
    // TODO: Make this look nice + optimized

    public string PrintInfo(bool showAllFiles)
    {
        var sb = new StringBuilder();

        sb.AppendLine("MSIXVC2 Package");
        sb.AppendLine($"  ContentId: {_package.ContentId}");
        sb.AppendLine($"  FulfillmentContentId: {_package.FulfillmentContentId}");
        sb.AppendLine($"  Version: {_package.Version}");
        sb.AppendLine($"  StoreId: {_package.StoreId}");
        sb.AppendLine($"  ProductId: {_package.ProductId}");
        sb.AppendLine($"  MinimumSystemVersion: {_package.MinimumSystemVersion}");
        sb.AppendLine($"  SupportedPlatforms: {_package.SupportedPlatforms}");
        sb.AppendLine();

        sb.AppendLine("Segmentation:");
        sb.AppendLine($"  Algorithm: {_package.Segmentation.Algorithm}");
        sb.AppendLine($"  Hash algorithm: 0x{_package.Segmentation.HashAlgorithm:x}");
        sb.AppendLine("Options:");
        foreach (var (key, value) in _package.Segmentation.Options)
        {
            sb.AppendLine($"  {key}: {value}");
        }
        sb.AppendLine();

        sb.AppendLine("Boxes:");
        foreach (var box in _package.Boxes)
        {
            sb.AppendLine($"  Name: {box.Name}");
        }
        sb.AppendLine();

        if (_package.Keys.Count > 0)
        {
            sb.AppendLine("Encrypted: true");
            sb.AppendLine($"Initial IV: {_package.InitialIV}");
            
            sb.AppendLine("Keys:");
            for (var i = 0; i < _package.Keys.Count; i++)
            {
                sb.AppendLine($"  Key #{i} - Sources:");
                var key = _package.Keys[i];
                foreach (var source in key.Sources)
                {
                    sb.AppendLine($"    Key ID: {source.SourceKeyId}");
                    sb.AppendLine($"    Purpose: {source.SourcePurpose}");
                    sb.AppendLine($"    Derivation: {source.DerivationAlgorithm}");
                    sb.AppendLine($"    KDF Context: {Convert.ToHexString(source.KdfContext)}");
                    sb.AppendLine($"    Wrap: {source.WrapAlgorithm}");
                    sb.AppendLine($"    Wrapping IV: {source.WrapIV}");
                    sb.AppendLine($"    Wrapped Key: {Convert.ToHexString(source.WrappedKey)}");
                    sb.AppendLine($"    Encryption: {source.Algorithm}");
                    sb.AppendLine();
                }

                sb.AppendLine();
            }

            if (!_loadedFileNames)
            {
                var remainingFilesToShow = 4096;
                foreach (var chunk in _chunkDetails)
                {
                    foreach (var file in chunk.Value.Files)
                    {
                        sb.AppendLine($"    File {file.Id}:");
                        sb.AppendLine($"      ChunkId: 0x{chunk.Key:x}");
                        sb.AppendLine($"      Length: 0x{file.Length:x}");
                        sb.AppendLine($"      Hash: {file.Hash}");
                        sb.AppendLine($"      ReadProtected: {file.ReadProtected}");
                        sb.AppendLine();

                        if (!showAllFiles && remainingFilesToShow-- == 0)
                            break;
                    }

                    if (remainingFilesToShow == 0)
                        break;
                }
            }
            else
            {
                sb.AppendLine("  Files in package:");
                foreach (var (name, file) in showAllFiles ? _files : _files.Take(Math.Min(4096, _files.Count)))
                {
                    sb.AppendLine($"    File {name}:");
                    sb.AppendLine($"      ChunkId: 0x{file.ChunkId:x}");
                    sb.AppendLine($"      Length: 0x{file.Length:x}");
                    sb.AppendLine($"      Hash: {file.Hash}");
                    sb.AppendLine($"      ReadProtected: {file.ReadProtected}");
                    sb.AppendLine();
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    public void ExtractFiles(string outputDirectory)
    {
        if (!_loadedFileNames)
        {
            throw new InvalidOperationException("Cannot extract files without filenames being loaded");
        }

        foreach (var path in _files.Keys)
        {
            var outputPath = Path.Join(outputDirectory, path);
            var dir = Path.GetDirectoryName(outputPath);
            if (dir != null)
            {
                Directory.CreateDirectory(dir);
            }
        }

        foreach (var (name, file) in _files)
        {
            Console.WriteLine($"Extracting {name}");

            var chunk = _chunks[file.ChunkId];

            var outputPath = Path.Join(outputDirectory, name);
            using var output = System.IO.File.OpenWrite(outputPath);

            if (file.Segments == null)
                continue;

            var buffer = new byte[file.Segments.Select(x => x.Length).Max()].AsSpan();

            foreach (var segment in file.Segments)
            {
                var currentSegmentBuffer = buffer[..segment.Length];
                ReadSegmentContent(segment, chunk.KeyIndex, currentSegmentBuffer);
                output.Write(currentSegmentBuffer);
            }
        }
    }
}