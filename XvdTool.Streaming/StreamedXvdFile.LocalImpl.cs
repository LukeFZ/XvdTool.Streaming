using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using DotNext.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using LibXboxOne;
using Spectre.Console;
using XvdTool.Streaming.Crypto;

namespace XvdTool.Streaming;

// Everything here is optimized for working with local (on-disk) files
public partial class StreamedXvdFile
{
    // Used in both VerifyDataPageHashes and CacheDataUnits
    private const int PageCountPerCache = 16;

    // ReSharper disable AccessToDisposedClosure
    private void LocalDecryptData(in KeyEntry key, bool recalculateHashes)
    {
        Debug.Assert(_stream is FileStream, "_stream is FileStream");

        var cipher = new AesXtsDecryptorNi(key.DataKey.AsReadOnlySpan<byte>(), key.TweakKey.AsReadOnlySpan<byte>());

        Action<ProgressContext> decryptor;

        using var memoryFile = MemoryMappedFile.CreateFromFile((FileStream)_stream, null, 0,
            MemoryMappedFileAccess.ReadWrite,
            HandleInheritability.None, true);

        if (_hasXvcInfo)
        {
            if (_xvcInfo.ContentID == null || !_xvcInfo.IsAnyKeySet)
                return;

            var decryptableRegionList =
                _xvcRegions
                    .Where(region => 
                        region.KeyId != XvcConstants.XVC_KEY_NONE
                        && _xvcInfo.KeyCount > region.KeyId - 1)
                    .ToArray();

            Debug.Assert(decryptableRegionList.Length > 0, "decryptableRegionList.Length > 0");

            decryptor = ctx =>
            {
                var tasks = new ProgressTask[decryptableRegionList.Length];

                for (int i = 0; i < decryptableRegionList.Length; i++)
                {
                    var region = decryptableRegionList[i];

                    Debug.Assert(region.KeyId == 0x0, "region.KeyId == 0x0");

                    tasks[i] = ctx.AddTask(
                        $"Decrypting region [white bold]0x{(uint) region.Id:x8}[/]",
                        autoStart: false,
                        maxValue: region.Length);
                }

                for (int i = 0; i < tasks.Length; i++)
                {
                    var region = decryptableRegionList[i];
                    var task = tasks[i];

                    task.StartTask();

                    LocalDecryptSection(task, memoryFile, cipher, (uint) region.Id, region.Offset, region.Length);

                    ctx.Refresh();

                    task.StopTask();
                }
            };
        }
        else
        {
            decryptor = ctx =>
            {
                var total = _stream.Length - (long) _userDataOffset;

                var task = ctx.AddTask("Decrypting data", autoStart: false, maxValue: total);

                task.StartTask();

                LocalDecryptSection(task, memoryFile, cipher, 0x1, _userDataOffset, (ulong) total);

                ctx.Refresh();

                task.StopTask();
            };
        }

        AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new TransferSpeedColumn(),
                new DownloadedColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .Start(decryptor);

        ConsoleLogger.WriteInfoLine("[green bold]Successfully[/] decrypted regions.");

        using var headerAccessor = memoryFile.CreateViewAccessor(0, (long) XvdFile.XVD_HEADER_INCL_SIGNATURE_SIZE,
            MemoryMappedFileAccess.Write);

        var header = _header;
        header.VolumeFlags ^= XvdVolumeFlags.EncryptionDisabled;

        var headerBytes = Shared.StructToBytes(header);

        headerAccessor.WriteArray(0, headerBytes, 0, headerBytes.Length);

        if (recalculateHashes)
        {
            // TODO
        }
    }
    // ReSharper restore AccessToDisposedClosure

    private void LocalDecryptSection(ProgressTask progressTask, MemoryMappedFile memoryFile, AesXtsDecryptorNi cipher, uint headerId, ulong offset, ulong length)
    {
        var startPage = XvdMath.OffsetToPageNumber(offset - _userDataOffset);
        var totalPageCount = XvdMath.OffsetToPageNumber(length);

        var dataUnits = Span<uint>.Empty;

        if (_dataIntegrity)
        {
            dataUnits = CacheDataUnits(startPage, totalPageCount);
        }

        var tweakIv = (stackalloc byte[16]);

        // <data unit (will be added later) x4> <headerId x4> <vduid x8>

        MemoryMarshal.Cast<byte, uint>(tweakIv)[1] = headerId;
        _header.VDUID.AsSpan(0, 8).CopyTo(tweakIv[8..]);

        const int pagesPerMaxInt = int.MaxValue / 0x1000;

        var remainingPages = totalPageCount;

        var transformedSpan = (stackalloc byte[0x1000]);

        for (ulong mappedPageOffset = 0; mappedPageOffset < totalPageCount; mappedPageOffset += pagesPerMaxInt)
        {
            var pageCountThisOffset = Math.Min(remainingPages, pagesPerMaxInt);
            remainingPages -= pageCountThisOffset;

            using var directAccessor = memoryFile.CreateDirectAccessor(
                (long)(offset + mappedPageOffset * XvdFile.PAGE_SIZE),
                (long)pageCountThisOffset * XvdFile.PAGE_SIZE);

            for (uint i = 0; i < pageCountThisOffset; i++)
            {
                var currentTotalPage = i + (long)mappedPageOffset;

                progressTask.Increment((int)XvdFile.PAGE_SIZE);

                var currentPageSpan =
                    directAccessor.Bytes.Slice((int)(i * XvdFile.PAGE_SIZE), (int)XvdFile.PAGE_SIZE);

                if (dataUnits.Length > 0)
                {
                    MemoryMarshal.Cast<byte, uint>(tweakIv)[0] = dataUnits[(int) currentTotalPage];
                }

                cipher.Transform(currentPageSpan, transformedSpan, tweakIv);

                transformedSpan.CopyTo(currentPageSpan);
            }
        }
    }

    private Span<uint> CacheDataUnits(ulong startPage, ulong count)
    {
        var dataUnits = new uint[count].AsSpan();

        var hashPageOffset = (long)CalculateHashEntryBlockOffset(startPage,
            out var currentHashPageEntry);

        var currentHashPageOffset = 0;
        var currentHashPage = 0;

        var refreshCache = true;

        var pageCache = (stackalloc byte[(int)XvdFile.PAGE_SIZE * PageCountPerCache]);

        _stream.Position = hashPageOffset;

        for (ulong i = 0; i < count; i++)
        {
            if (refreshCache)
            {
                refreshCache = false;

                var read = _stream.Read(pageCache);
                Debug.Assert(read == pageCache.Length, "read == pageCache.Length");
            }

            dataUnits[(int)i] = MemoryMarshal.Cast<byte, uint>(pageCache.Slice(currentHashPageOffset + 0x14, 4))[0];

            currentHashPageOffset += 0x18;
            currentHashPageEntry++;
            if (currentHashPageEntry == 0xAA)
            {
                currentHashPageOffset += 0x10;
                currentHashPageEntry = 0;
                currentHashPage++;
                if (currentHashPage == PageCountPerCache)
                {
                    refreshCache = true;
                    currentHashPage = 0;
                    currentHashPageOffset = 0;
                }
            }
        }

        return dataUnits;
    }

    private bool LocalVerifyDataHashes()
    {
        if (!_dataIntegrity)
            return true;

        return AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new TransferSpeedColumn(),
                new DownloadedColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .Start(LocalVerifyDataHashesTask);
    }

    private bool LocalVerifyDataHashesTask(ProgressContext ctx)
    {
        var valid = true;

        var cachedHashPages = (stackalloc byte[0x1000 * PageCountPerCache]);
        var calculatedHash = (stackalloc byte[32]);
        var pageData = (stackalloc byte[0x1000]);

        var dataBlockCount = XvdMath.OffsetToPageNumber((ulong)_stream.Length - _userDataOffset);

        var hashPageOffset = (long)CalculateHashEntryOffset(0);
        var dataToHashOffset = _userDataOffset;

        var currentHashPageOffset = 0;
        var currentHashPageEntry = 0;
        var currentHashPage = 0;

        var refreshCache = true;

        var fileHandle = (_stream as FileStream)!.SafeFileHandle;

        _stream.Position = (long)dataToHashOffset;

        var task = ctx.AddTask("Verifying hashes", maxValue: (long) dataBlockCount * (int) XvdFile.PAGE_SIZE);

        for (ulong i = 0; i < dataBlockCount; i++)
        {
            task.Increment(XvdFile.PAGE_SIZE);

            int read;
            if (refreshCache)
            {
                refreshCache = false;

                read = RandomAccess.Read(fileHandle, cachedHashPages, hashPageOffset);
                Debug.Assert(read == cachedHashPages.Length, "read == cachedHashPages.Length");

                hashPageOffset += cachedHashPages.Length;
            }

            read = _stream.Read(pageData);
            Debug.Assert(read == pageData.Length, "read == pageData.Length");

            SHA256.HashData(pageData, calculatedHash);

            if (!cachedHashPages
                    .Slice(currentHashPageOffset, _hashEntryLength)
                    .SequenceEqual(calculatedHash[.._hashEntryLength]))
            {
                ConsoleLogger.WriteErrLine($"Page [bold]0x{i:x16}[i] has an [red bold]invalid[/] hash.");

                valid = false;
            }

            currentHashPageOffset += 0x18;
            currentHashPageEntry++;
            if (currentHashPageEntry == 0xAA)
            {
                currentHashPageOffset += 0x10;
                currentHashPageEntry = 0;
                currentHashPage++;
                if (currentHashPage == PageCountPerCache)
                {
                    refreshCache = true;
                    currentHashPage = 0;
                    currentHashPageOffset = 0;
                }
            }
        }

        return valid;
    }
}