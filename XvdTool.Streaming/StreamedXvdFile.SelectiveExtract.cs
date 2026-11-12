using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using LibXboxOne;
using Spectre.Console;
using XvdTool.Streaming.Commands;
using XvdTool.Streaming.Crypto;
using Aes = System.Runtime.Intrinsics.X86.Aes;

namespace XvdTool.Streaming;

public partial class StreamedXvdFile
{
    private static ulong SegmentPageCount(ulong fileSize) => 
        fileSize == 0 ? 1UL : (fileSize - 1) / XvdFile.PAGE_SIZE + 1;
    
    private void ExtractSelectedFiles(string outputPath, AesXtsDecryptorNi? cipher, bool skipHashCheck, uint[]? skippedRegionList, uint[]? downloadRegionList, XvdFilePattern[] filePatterns)
    {
        if (_xvcUpdateSegments.Length < _segments.Length) 
        {
            throw new InvalidDataException("XVC segment table is shorter than SegmentMetadata.bin.");
        }
        
        var firstSegmentOffset = XvdMath.PageNumberToOffset(_xvcUpdateSegments[0].PageNum);
        var regions = _xvcRegions.Where(region => (downloadRegionList == null || downloadRegionList.Contains((uint)region.Id))
                                                  && (skippedRegionList == null || !skippedRegionList.Contains((uint)region.Id))
                                                  && (region.FirstSegmentIndex != 0 || firstSegmentOffset == region.Offset)).ToArray();

        if (filePatterns.Length == 0) 
        {
            throw new ArgumentException("At least one non-empty --file pattern is required.", nameof(filePatterns));
        }

        var selected = new List<(int Index, XvcRegionHeader Region)>();
        for (var i = 0; i < _segments.Length; i++) 
        {
            var normalized = XvdFilePattern.NormalizePath(_segmentPaths[i]);
            var fileName = Path.GetFileName(normalized);
            if (!filePatterns.Any(pat => pat.Matches(normalized, fileName)))
            {
                continue;
            }
            
            var segmentOffset = XvdMath.PageNumberToOffset(_xvcUpdateSegments[i].PageNum);
            var pageCount = SegmentPageCount(_segments[i].FileSize);
            var paddedLength = checked(pageCount * XvdFile.PAGE_SIZE);

            //resolve the actual region for each selected file separately.
            foreach (var region in regions) 
            {
                if ((ulong)i < region.FirstSegmentIndex || segmentOffset < region.Offset) 
                {
                    continue;
                }

                var withinRegion = segmentOffset - region.Offset;
                if (withinRegion >= region.Length || paddedLength > region.Length - withinRegion) 
                {
                    continue;
                }

                selected.Add((i, region));
                break;
            }
        }

        if (selected.Count == 0) 
        {
            throw new FileNotFoundException("No extractable XVC segments matched the specified --file patterns and region filters.");
        }

        ConsoleLogger.WriteInfoLine($"Selected [green bold]{selected.Count}[/] file(s) for random-access extraction.");

        var destinationRoot = Path.GetFullPath(outputPath);
        var totalBytes = selected.Sum(entry => (double)SegmentPageCount(_segments[entry.Index].FileSize) * XvdFile.PAGE_SIZE);
        AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new TransferSpeedColumn(),
                new DownloadedColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .Start(ctx => {
                var task = ctx.AddTask("Extracting selected files", maxValue: totalBytes);
                foreach (var (index, region) in selected) {
                    task.Description = $"Extracting [white bold]{Markup.Escape(_segmentPaths[index])}[/]";
                    ExtractSelectedSegment(task, destinationRoot, index, region, cipher, skipHashCheck);
                }
            });
    }

    private void ExtractSelectedSegment(ProgressTask progress, string outputRoot, int segmentIndex, XvcRegionHeader region, AesXtsDecryptorNi? cipher, bool skipHashCheck)
    {
        const int maxRetries = 3;
        const int pageCacheCapacity = 0x100000;

        var relativePath = NormalizeSegmentPathForOutput(_segmentPaths[segmentIndex]);
        var outputPath = Path.GetFullPath(Path.Combine(outputRoot, relativePath));
        var relativeOutputPath = Path.GetRelativePath(outputRoot, outputPath);

        //external archive metadata. don't allow them to escape the requested output directory.
        if (Path.IsPathRooted(relativeOutputPath) 
            || relativeOutputPath is "." or ".."
            || relativeOutputPath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)) 
        {
            throw new InvalidDataException($"Unsafe segment path: {_segmentPaths[segmentIndex]}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var file = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);

        var remainingFileBytes = _segments[segmentIndex].FileSize;
        var remainingPages = SegmentPageCount(remainingFileBytes);
        var segmentOffset = XvdMath.PageNumberToOffset(_xvcUpdateSegments[segmentIndex].PageNum);

        var shouldDecrypt = _encrypted && region.KeyId != XvcConstants.XVC_KEY_NONE;
        Span<byte> tweakIv = stackalloc byte[16];
        if (shouldDecrypt) 
        {
            BinaryPrimitives.WriteUInt32LittleEndian(tweakIv[4..8], (uint)region.Id);
            _header.VDUID.AsSpan(0, 8).CopyTo(tweakIv[8..]);
        }

        var pageSize = (int)XvdFile.PAGE_SIZE;
        var pagesPerBatch = pageCacheCapacity / pageSize;
        var pageBuffer = new byte[pageCacheCapacity];
        var hashPageBuffer = new byte[pageSize];
        Span<byte> calculatedHash = stackalloc byte[SHA256.HashSizeInBytes];
        var cachedHashPageOffset = -1L;
        ulong pageIndexInSegment = 0;
        while (remainingPages != 0) 
        {
            var batchPages = (int)Math.Min(remainingPages, (ulong)pagesPerBatch);
            var batchLength = checked(batchPages * pageSize);
            var absoluteOffset = checked(segmentOffset + pageIndexInSegment * (ulong)pageSize);
            var batch = pageBuffer.AsSpan(0, batchLength);

            var verified = false;
            for (var attempt = 0; attempt < maxRetries; attempt++) 
            {
                _stream.Position = checked((long)absoluteOffset);
                _stream.ReadExactly(batch); 
                verified = true;

                for (var j = 0; j < batchPages; j++) 
                {
                    var page = batch.Slice(j * pageSize, pageSize);

                    if (_dataIntegrity) 
                    {
                        var pageOffset = checked(absoluteOffset + (ulong)j * (ulong)pageSize);
                        var hashedPageNumber = XvdMath.OffsetToPageNumber(pageOffset - _userDataOffset);
                        var hashPageOffset = checked((long)CalculateHashEntryBlockOffset(hashedPageNumber, out var entryId));

                        //4 KiB hash page contains many data-page entries; avoid fetching 1 MiB of hash metadata for a tiny file.
                        if (hashPageOffset != cachedHashPageOffset) 
                        {
                            _stream.Position = hashPageOffset;
                            _stream.ReadExactly(hashPageBuffer); 
                            cachedHashPageOffset = hashPageOffset;
                        }

                        var entryOffset = checked((int)(entryId * XvdFile.HASH_ENTRY_LENGTH));
                        var entry = hashPageBuffer.AsSpan(entryOffset, (int)XvdFile.HASH_ENTRY_LENGTH);
                        if (!skipHashCheck) 
                        {
                            SHA256.HashData(page, calculatedHash);
                            if (!entry[.._hashEntryLength].SequenceEqual(calculatedHash[.._hashEntryLength])) 
                            {
                                verified = false;
                                break;
                            }
                        }

                        if (shouldDecrypt) 
                        {
                            BinaryPrimitives.WriteUInt32LittleEndian(tweakIv[..4], BinaryPrimitives.ReadUInt32LittleEndian(entry.Slice(_hashEntryLength, 4)));
                        }
                    }

                    if (shouldDecrypt) 
                    {
                        Debug.Assert(cipher != null, "cipher is null but region is encrypted");
                        cipher.Transform(page, page, tweakIv);
                    }
                }

                if (verified) 
                {
                    break;
                }

                ConsoleLogger.WriteWarnLine($"Hash mismatch in {_segmentPaths[segmentIndex]} (retry {attempt + 1}/{maxRetries}).");
                cachedHashPageOffset = -1;
            }

            if (!verified) 
            {
                throw new InvalidDataException($"Unable to verify pages for {_segmentPaths[segmentIndex]} after {maxRetries} attempts.");
            }

            var fileBytesThisBatch = (int)Math.Min(remainingFileBytes, (ulong)batchLength);
            file.Write(batch[..fileBytesThisBatch]);
            remainingFileBytes -= (ulong)fileBytesThisBatch;
            remainingPages -= (ulong)batchPages;
            pageIndexInSegment += (ulong)batchPages;
            progress.Increment(batchLength);
        }
    }
}
