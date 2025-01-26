using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using DiscUtils;
using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using LibXboxOne;
using Spectre.Console;
using Spectre.Console.Rendering;
using XvdTool.Streaming.Crypto;
using Aes = System.Runtime.Intrinsics.X86.Aes;

namespace XvdTool.Streaming;

public partial class StreamedXvdFile : IDisposable
{
    private readonly Stream _stream;
    private readonly BinaryReader _reader;

    private XvdHeader _header;

    private bool _hasXvcInfo;
    private XvcInfo _xvcInfo;
    private XvcRegionHeader[] _xvcRegions;
    private XvcUpdateSegment[] _xvcUpdateSegments;
    private XvcRegionSpecifier[] _xvcRegionSpecifiers;
    private XvcRegionPresenceInfo[] _xvcRegionPresenceInfo;

    private bool _hasUserData;
    private XvdUserDataHeader _userDataHeader;
    private XvdUserDataPackageFilesHeader _userDataPackageFilesHeader;
    private readonly Dictionary<string, XvdUserDataPackageFileEntry> _userDataPackages;
    private readonly Dictionary<string, byte[]> _userDataPackageContents;

    private bool _hasSegmentMetadata;
    private XvdSegmentMetadataHeader _segmentMetadataHeader;
    private XvdSegmentMetadataSegment[] _segments;
    private string[] _segmentPaths;

    private bool _hasPartitionFiles;
    private (string Path, ulong Size)[] _partitionFileEntries = [];

    // XVD header extracted infos
    private bool _isXvc;
    private bool _dataIntegrity;
    private bool _resiliency;
    private bool _encrypted;

    private int _hashEntryLength;
    private ulong _hashTreePageCount;
    private ulong _hashTreeLevels;

    private readonly ulong _embeddedXvdOffset = XvdFile.XVD_HEADER_INCL_SIGNATURE_SIZE;
    private ulong _mutableDataOffset;
    private ulong _hashTreeOffset;
    private ulong _userDataOffset;
    private ulong _xvcInfoOffset;
    private ulong _dynamicHeaderOffset;
    private ulong _driveDataOffset;

    private const string SegmentMetadataFilename = "SegmentMetadata.bin";

    private StreamedXvdFile(Stream stream)
    {
        _stream = stream;
        _reader = new BinaryReader(stream);

        _xvcRegions = [];
        _xvcUpdateSegments = [];
        _xvcRegionSpecifiers = [];
        _xvcRegionPresenceInfo = [];

        _userDataPackages = [];
        _userDataPackageContents = [];

        _segments = [];
        _segmentPaths = [];
    }

    public static StreamedXvdFile OpenFromUrl(string url)
    {
        return new StreamedXvdFile(HttpFileStream.Open(url));
    }

    public static StreamedXvdFile OpenFromFile(string filePath, bool writing = true)
    {
        if (!File.Exists(filePath))
            throw new InvalidOperationException("File does not exist");

        return new StreamedXvdFile(File.Open(filePath, FileMode.Open, writing ? FileAccess.ReadWrite : FileAccess.Read, FileShare.Read));
    }

    public void Parse()
    {
        ParseHeader();

        if (_header.UserDataLength > 0)
        {
            ParseUserData();

            if (_userDataPackageContents.ContainsKey(SegmentMetadataFilename))
                ParseSegmentMetadata();
        }

        if (_isXvc && _header.XvcDataLength > 0)
        {
            ParseXvcInfo();
        }

        if (!_hasSegmentMetadata && _header.Type == XvdType.Fixed)
        {
            ParseNtfsPartition();
        }
    }

    private void ParseHeader()
    {
        _stream.Position = 0;

        _header = _reader.ReadStruct<XvdHeader>();

        _isXvc = XvdFile.XvcContentTypes.Contains(_header.ContentType);
        _dataIntegrity = !_header.VolumeFlags.HasFlag(XvdVolumeFlags.DataIntegrityDisabled);
        _resiliency = _header.VolumeFlags.HasFlag(XvdVolumeFlags.ResiliencyEnabled);
        _encrypted = !_header.VolumeFlags.HasFlag(XvdVolumeFlags.EncryptionDisabled);

        _hashTreePageCount = XvdMath.CalculateNumberHashPages(out _hashTreeLevels, _header.NumberOfHashedPages, _resiliency);
        _hashEntryLength = _encrypted ? (int)XvdFile.HASH_ENTRY_LENGTH_ENCRYPTED : (int)XvdFile.HASH_ENTRY_LENGTH;

        _mutableDataOffset = XvdMath.PageNumberToOffset(_header.EmbeddedXvdPageCount) + _embeddedXvdOffset;
        _hashTreeOffset = _header.MutableDataLength + _mutableDataOffset;
        _userDataOffset = (_dataIntegrity ? XvdMath.PageNumberToOffset(_hashTreePageCount) : 0) + _hashTreeOffset;
        _xvcInfoOffset = XvdMath.PageNumberToOffset(_header.UserDataPageCount) + _userDataOffset;
        _dynamicHeaderOffset = XvdMath.PageNumberToOffset(_header.XvcInfoPageCount) + _xvcInfoOffset;
        _driveDataOffset = XvdMath.PageNumberToOffset(_header.DynamicHeaderPageCount) + _dynamicHeaderOffset;
    }

    private void ParseUserData()
    {
        _stream.Position = (long)_userDataOffset;

        var userData = new byte[_header.UserDataLength];
        var read = _stream.Read(userData.AsSpan());
        Debug.Assert(read == userData.Length, "read == userData.Length");

        using var userDataReader = new BinaryReader(new MemoryStream(userData));

        _userDataHeader = userDataReader.ReadStruct<XvdUserDataHeader>();
        if (_userDataHeader.Type == XvdUserDataType.PackageFiles)
        {
            _hasUserData = true;

            userDataReader.BaseStream.Position = _userDataHeader.Length;

            _userDataPackageFilesHeader = userDataReader.ReadStruct<XvdUserDataPackageFilesHeader>();
            Debug.Assert(int.MaxValue > _userDataPackageFilesHeader.FileCount, "int.MaxValue > _userDataPackageFilesHeader.FileCount");

            var fileCount = (int)_userDataPackageFilesHeader.FileCount;

            _userDataPackages.EnsureCapacity(fileCount);

            foreach (var entry in userDataReader.ReadStructArray<XvdUserDataPackageFileEntry>(fileCount))
            {
                userDataReader.BaseStream.Position = _userDataHeader.Length + entry.Offset;
                var contents = new byte[entry.Size];
                read = userDataReader.BaseStream.Read(contents.AsSpan());
                Debug.Assert(read == entry.Size, "read == entry.Size");

                _userDataPackages[entry.FilePath] = entry;
                _userDataPackageContents[entry.FilePath] = contents;
            }
        }
    }

    private void ParseSegmentMetadata()
    {
        var segmentMetadata = _userDataPackageContents[SegmentMetadataFilename];

        using var segmentMetadataReader = new BinaryReader(new MemoryStream(segmentMetadata));

        _segmentMetadataHeader = segmentMetadataReader.ReadStruct<XvdSegmentMetadataHeader>();
        _hasSegmentMetadata = true;

        Debug.Assert(int.MaxValue > _segmentMetadataHeader.SegmentCount, "int.MaxValue > _segmentMetadataHeader.SegmentCount");

        _segments =
            segmentMetadataReader.ReadStructArray<XvdSegmentMetadataSegment>((int) _segmentMetadataHeader.SegmentCount);

        _segmentPaths = new string[_segmentMetadataHeader.SegmentCount];

        var segmentPathsOffset = 
            _segmentMetadataHeader.HeaderLength
            + _segmentMetadataHeader.SegmentCount * 0x10 /* segment size */;

        for (int i = 0; i < _segments.Length; i++)
        {
            var segment = _segments[i];

            segmentMetadataReader.BaseStream.Position = segmentPathsOffset + segment.PathOffset;

            var stringSpan = segmentMetadataReader.ReadBytes(segment.PathLength * 2).AsSpan();

            _segmentPaths[i] = new string(MemoryMarshal.Cast<byte, char>(stringSpan));
        }
    }

    private void ParseXvcInfo()
    {
        _stream.Position = (long)_xvcInfoOffset;

        var xvcInfo = new byte[_header.XvcDataLength];
        var xvcInfoSpan = xvcInfo.AsSpan();

        var read = _stream.Read(xvcInfo.AsSpan());
        Debug.Assert(read == xvcInfoSpan.Length, "read == xvcInfoSpan.Length");

        using var xvcInfoReader = new BinaryReader(new MemoryStream(xvcInfo));

        _xvcInfo = xvcInfoReader.ReadStruct<XvcInfo>();
        _hasXvcInfo = true;

        if (_xvcInfo.Version >= 1)
        {
            _xvcRegions = xvcInfoReader.ReadStructArray<XvcRegionHeader>(checked((int)_xvcInfo.RegionCount));
            _xvcUpdateSegments = xvcInfoReader.ReadStructArray<XvcUpdateSegment>(checked((int)_xvcInfo.UpdateSegmentCount));

            if (_xvcInfo.Version >= 2)
            {
                _xvcRegionSpecifiers = xvcInfoReader.ReadStructArray<XvcRegionSpecifier>(checked((int)_xvcInfo.RegionSpecifierCount));

                if (_header.MutableDataPageCount > 0)
                {
                    var presenceInfo = xvcInfoSpan.Slice((int)xvcInfoReader.BaseStream.Position, (int)_xvcInfo.RegionCount);

                    _xvcRegionPresenceInfo = MemoryMarshal.Cast<byte, XvcRegionPresenceInfo>(presenceInfo).ToArray();
                }
            }
        }
    }

    private void ParseNtfsPartition()
    {
        var driveSize = checked((long)_header.DriveSize);

        using var fsStream =
            new StreamedXvdFileSystemStream(
                driveSize,
                checked((long)_driveDataOffset),
                0,
                checked((long)_xvcInfoOffset),
                _stream);

        PartitionTable? partitionTable;

        try
        {
            partitionTable =
                new GuidPartitionTable(fsStream, Geometry.FromCapacity(driveSize, (int)XvdFile.SECTOR_SIZE));
        }
        catch (Exception)
        {
            partitionTable = null;
        }

        if (partitionTable == null)
        {
            try
            {
                partitionTable =
                    new BiosPartitionTable(fsStream, Geometry.FromCapacity(driveSize, (int)XvdFile.SECTOR_SIZE));
            }
            catch (Exception)
            {
                partitionTable = null;
            }
        }

        if (partitionTable == null)
        {
            ConsoleLogger.WriteErrLine("Failed to drive contents as either GPT or MBR.");
            return;
        }

        var partionTable = new BiosPartitionTable(fsStream, Geometry.FromCapacity(driveSize, (int)XvdFile.SECTOR_SIZE));
        if (partionTable.Partitions.Count == 0)
        {
            ConsoleLogger.WriteInfoLine("File does not contain a partition.");
            return;
        }

        if (partionTable.Partitions.Count > 1)
        {
            ConsoleLogger.WriteInfoLine($"File contains [white bold]{partionTable.Partitions.Count}[/] partitions.");
        }

        using var partiton = new NtfsFileSystem(partionTable.Partitions[0].Open());

        _hasPartitionFiles = true;
        _partitionFileEntries = partiton.Root
            .GetFiles("*.*", SearchOption.AllDirectories)
            .Select(x => (x.FullName, (ulong)x.Length))
            .ToArray();
    }

    public Guid GetKeyId()
    {
        if (!_encrypted || !_hasXvcInfo || !_xvcInfo.IsAnyKeySet)
            return Guid.Empty;

        foreach (var key in _xvcInfo.EncryptionKeyIds)
        {
            if (key.IsKeyNulled)
                continue;

            return new Guid(key.KeyId);
        }

        throw new UnreachableException("IsAnyKeySet == false but no key was not nulled");
    }

    private ulong CalculateHashEntryOffset(ulong blockNo, uint hashLevel = 0)
    {
        var hashBlockPage = XvdMath.CalculateHashBlockNumForBlockNum(_header.Type, _hashTreeLevels,
            _header.NumberOfHashedPages, blockNo, hashLevel, out var hashEntryNum, _resiliency);

        return
            _hashTreeOffset
            + XvdMath.PageNumberToOffset(hashBlockPage)
            + hashEntryNum * XvdFile.HASH_ENTRY_LENGTH;
    }

    private ulong CalculateHashEntryBlockOffset(ulong blockNo, out ulong hashEntryId)
    {
        var hashBlockPage = XvdMath.CalculateHashBlockNumForBlockNum(_header.Type, _hashTreeLevels,
            _header.NumberOfHashedPages, blockNo, 0, out hashEntryId, _resiliency);

        return
            _hashTreeOffset
            + XvdMath.PageNumberToOffset(hashBlockPage);
    }

    public void DecryptData(in KeyEntry key, bool recalculateHashes)
    {
        if (_stream is not FileStream)
        {
            ConsoleLogger.WriteErrLine("Decryption is only supported for [bold]local[/] files.");
            ConsoleLogger.WriteErrLine("[bold red]Streamed[/] files can only be extracted.");
            return;
        }

        if (!_encrypted)
        {
            ConsoleLogger.WriteInfoLine("Skipping decryption as the file is [green bold]not encrypted[/].");
            return;
        }

        LocalDecryptData(key, recalculateHashes);
    }

    public bool VerifyDataHashes()
    {
        if (_stream is not FileStream)
        {
            ConsoleLogger.WriteErrLine("Hash verification is only supported for local files.");
            ConsoleLogger.WriteErrLine("Streamed files can only be extracted.");
            return false;
        }

        if (!_dataIntegrity)
        {
            ConsoleLogger.WriteInfoLine("Skipping hash verification as this file does not have data integrity enabled.");
            return true;
        }

        return LocalVerifyDataHashes();
    }

    public void ExtractFiles(string outputPath, in KeyEntry key, bool skipHashCheck = false, uint[]? skippedRegionList = null, uint[]? downloadRegionList = null)
    {
        if (!_hasXvcInfo)
        {
            ConsoleLogger.WriteErrLine("Cannot extract non-XVC files.");
            return;
        }

        if (!_hasSegmentMetadata)
        {
            ConsoleLogger.WriteErrLine("Cannot extract files that do not contain a segment->file map (SegmentMetadata.bin).");
            return;
        }

        if (0 >= _segments.Length)
        {
            ConsoleLogger.WriteWarnLine("XVC does not contain any segments/files.");
            return;
        }

        AesXtsDecryptorNi cipher = null!;

        if (_encrypted)
        {
            if (!Aes.IsSupported)
            {
                ConsoleLogger.WriteErrLine("Your CPU does not support AES-NI.");
                ConsoleLogger.WriteErrLine("Either upgrade to a newer CPU or use the regular XVDTool to extract this file.");
                return;
            }

            cipher = new AesXtsDecryptorNi(key.DataKey.AsReadOnlySpan<byte>(), key.TweakKey.AsReadOnlySpan<byte>());
        }

        var fullOutputPath = Path.GetFullPath(outputPath); // Expand path to prevent issues later

        var firstSegmentOffset = XvdMath.PageNumberToOffset(_xvcUpdateSegments[0].PageNum);

        XvcRegionHeader[] extractableRegionList;

        if (downloadRegionList != null)
        {
            extractableRegionList = _xvcRegions
                .Where(region => downloadRegionList.Contains((uint)region.Id)
                && (region.FirstSegmentIndex != 0 || firstSegmentOffset == region.Offset))
                .ToArray();
        }
        else
        {
            extractableRegionList =
                _xvcRegions
                    .Where(region =>
                        (skippedRegionList == null || !skippedRegionList.Contains((uint)region.Id))
                        && (region.FirstSegmentIndex != 0 || firstSegmentOffset == region.Offset))
                    .ToArray();
        }

        // Fancy console version
        AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new TransferSpeedColumn(),
                new DownloadedColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .Start(ctx =>
            {
                var tasks = new ProgressTask[extractableRegionList.Length];
                for (int i = 0; i < extractableRegionList.Length; i++)
                {
                    var region = extractableRegionList[i];
                    var totalValue = region.Length;

                    tasks[i] = ctx.AddTask(
                        $"Extracting region [white bold]0x{(uint) region.Id:x8}[/]",
                        autoStart: false,
                        maxValue: totalValue);
                }

                for (int i = 0; i < tasks.Length; i++)
                {
                    var region = extractableRegionList[i];
                    var task = tasks[i];

                    task.StartTask();

                    ExtractRegion(
                        task,
                        fullOutputPath,
                        cipher,
                        (uint)region.Id,
                        region.Offset,
                        region.Length,
                        region.FirstSegmentIndex,
                        _encrypted && region.KeyId != XvcConstants.XVC_KEY_NONE,
                        skipHashCheck
                    );

                    task.StopTask();
                }
            });
    }

    private void ExtractRegion(
        ProgressTask progressTask,
        string output,
        AesXtsDecryptorNi cipher,
        uint headerId, 
        ulong regionOffset,
        ulong regionLength,
        uint startSegment,
        bool shouldDecrypt, 
        bool skipHashCheck = false
    )
    {
        Debug.Assert(_xvcUpdateSegments[(int)startSegment].PageNum == XvdMath.OffsetToPageNumber(regionOffset));

        var tweakIv = (stackalloc byte[16]);

        if (shouldDecrypt)
        {
            MemoryMarshal.Cast<byte, uint>(tweakIv)[1] = headerId;
            _header.VDUID.AsSpan(0, 8).CopyTo(tweakIv[8..]);
        }

        // Page Cache
        var refreshPageCache = true;
        var totalPageCacheOffset = (long)regionOffset;

        var pageCacheOffset = 0;

        //var pageCache = (stackalloc byte[0x10000]);
        var pageCache = new byte[0x100000].AsSpan();

        // Hash Cache
        var refreshHashCache = _dataIntegrity;
        var totalHashCacheOffset =
            (long) CalculateHashEntryBlockOffset(XvdMath.OffsetToPageNumber(regionOffset - _userDataOffset),
                out var hashCacheEntry);

        var hashCacheOffset = (int)(hashCacheEntry * XvdFile.HASH_ENTRY_LENGTH);

        // var hashCache = (stackalloc byte[0x10000]);
        var hashCache = new byte[0x100000].AsSpan();

         // Buffer for calculated hash
        var calculatedHash = (stackalloc byte[SHA256.HashSizeInBytes]);

        // Progression tracking
        var currentSegment = startSegment;
        var currentPageNumber = 0;
        var totalPageNumber = (long) XvdMath.OffsetToPageNumber(regionLength);

        while (_segments.Length > currentSegment && totalPageNumber > currentPageNumber)
        {
            var fileSize = _segments[currentSegment].FileSize;
            var filePath = _segmentPaths[currentSegment];

            var outputPath = Path.Join(output, filePath);
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (outputDirectory != null)
                Directory.CreateDirectory(outputDirectory);

            using var fileStream = File.OpenWrite(outputPath);

            var remainingFileSize = fileSize;

            do // even files that are completely empty take up one page of (padding) data, so this loop needs to run at least once.
            {
                var currentFileSectionLength = (int)Math.Min(remainingFileSize, XvdFile.PAGE_SIZE);

                int read;
                if (refreshHashCache)
                {
                    _stream.Position = totalHashCacheOffset;

                    read = _stream.Read(hashCache);
                    Debug.Assert(read == hashCache.Length);

                    refreshHashCache = false;
                }

                if (refreshPageCache)
                {
                    _stream.Position = totalPageCacheOffset;

                    read = _stream.Read(pageCache);
                    Debug.Assert(read == pageCache.Length || (uint)pageCache.Length > remainingFileSize);

                    refreshPageCache = false;
                }

                var currentPage = pageCache.Slice(pageCacheOffset, (int)XvdFile.PAGE_SIZE);

                if (_dataIntegrity)
                {
                    var currentHashEntry = hashCache.Slice(hashCacheOffset, (int)XvdFile.HASH_ENTRY_LENGTH);

                    if (!skipHashCheck)
                    {
                        SHA256.HashData(currentPage, calculatedHash);

                        if (!currentHashEntry[.._hashEntryLength].SequenceEqual(calculatedHash[.._hashEntryLength]))
                        {
                            ConsoleLogger.WriteErrLine($"Page 0x{currentPageNumber:x} has an invalid hash, retrying.");

                            // This could be corruption during the download, refresh caches and retry
                            refreshHashCache = true;
                            refreshPageCache = true;
                            continue;
                        }
                    }

                    if (shouldDecrypt)
                    {
                        MemoryMarshal.Cast<byte, uint>(tweakIv)[0] =
                            MemoryMarshal.Cast<byte, uint>(currentHashEntry.Slice(_hashEntryLength, sizeof(uint)))[0];
                    }

                    hashCacheOffset += (int)XvdFile.HASH_ENTRY_LENGTH;
                    hashCacheEntry++;
                    if (hashCacheEntry == XvdFile.HASH_ENTRIES_IN_PAGE)
                    {
                        hashCacheEntry = 0;
                        hashCacheOffset += 0x10; // Alignment for page boundaries (0xff0 -> 0x1000)
                    }

                    if (hashCacheOffset == hashCache.Length)
                    {
                        totalHashCacheOffset += hashCacheOffset;
                        hashCacheOffset = 0;
                        hashCacheEntry = 0;
                        refreshHashCache = true;
                    }
                }

                if (shouldDecrypt)
                {
                    cipher.Transform(currentPage, currentPage, tweakIv);
                }

                fileStream.Write(currentPage[..currentFileSectionLength]);

                remainingFileSize -= (uint)currentFileSectionLength;

                pageCacheOffset += (int)XvdFile.PAGE_SIZE;
                if (pageCacheOffset == pageCache.Length)
                {
                    totalPageCacheOffset += pageCacheOffset;
                    pageCacheOffset = 0;
                    refreshPageCache = true;
                }

                currentPageNumber++;
                progressTask.Increment(XvdFile.PAGE_SIZE);
            } while (remainingFileSize > 0);

            currentSegment++;
        }
    }

    public void ExtractEmbeddedXvd(string outputPath)
    {
        if (_header.EmbeddedXVDLength == 0)
            throw new InvalidOperationException("XVC does not contain an embedded XVD.");

        AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new TransferSpeedColumn(),
                new DownloadedColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .Start(ctx =>
            {
                var task = ctx.AddTask("Extracting embedded XVD", 
                    autoStart: false,
                    maxValue: _header.EmbeddedXVDLength);

                using var fs = File.OpenWrite(outputPath);
                _stream.Position = (long)_embeddedXvdOffset;

                var pageCache = new byte[0x100000].AsSpan();
                var remaining = (long)_header.EmbeddedXVDLength;

                task.StartTask();

                while (remaining != 0)
                {
                    var current = (int)Math.Min(remaining, pageCache.Length);
                    var slice = pageCache.Slice(0, current);

                    _stream.ReadExactly(slice);
                    fs.Write(slice);

                    remaining -= current;
                    task.Increment(current);
                }

                task.StopTask();
                ctx.Refresh();
            });

        ConsoleLogger.WriteInfoLine("Finished extracting embedded XVD!");
    }

    public string PrintInfo(bool showAllFiles = false)
    {
        AnsiConsole.Record();

        var xvdHeaderPanel = new Panel(StringToRows(_header.ToString(false)))
            .Header("XVD Header")
            .RoundedBorder()
            .Expand();

        AnsiConsole.Write(xvdHeaderPanel);

        if (_hasXvcInfo)
        {
            var xvcInfoPanel = new Panel(StringToRows(_xvcInfo.ToString(false)))
                .Header("XVC Info")
                .RoundedBorder()
                .Expand();

            AnsiConsole.Write(xvcInfoPanel);

            if (_xvcInfo.Version >= 1)
            {
                var regionSpecifiersPerRegion = _xvcRegionSpecifiers
                    .ToLookup(x => x.RegionId)
                    .ToDictionary(x => x.Key, x => x.ToList());

                var regionTable = new Table()
                    .Title("XVC Regions")
                    .AddColumns(
                        new TableColumn("Region ID"),
                        new TableColumn("Flags"),
                        new TableColumn("Key ID"),
                        new TableColumn("Offset"),
                        new TableColumn("Length"),
                        new TableColumn("Hash"),
                        new TableColumn("First Segment"),
                        //new TableColumn("Region Status"),
                        new TableColumn("Region Specifiers")
                    )
                    .RoundedBorder()
                    .Expand();

                foreach (var region in _xvcRegions)
                {
                    //var regionPresenceElement = new Text(_xvcRegionPresenceInfo.Length > 0
                    //    ? _xvcRegionPresenceInfo[i].ToString()
                    //    : "");

                    IRenderable regionSpecifierElement;

                    if (regionSpecifiersPerRegion.TryGetValue(region.Id, out var specifiers))
                    {
                        var regionSpecifierTable = new Table()
                            .AddColumn("Key")
                            .AddColumn("Value")
                            .RoundedBorder()
                            .Expand();

                        foreach (var specifier in specifiers)
                            regionSpecifierTable.AddRow(specifier.Key, specifier.Value);

                        regionSpecifierElement = regionSpecifierTable;
                    }
                    else
                    {
                        regionSpecifierElement = new Text("");
                    }

                    regionTable.AddRow(
                        new Text(region.Id.ToString()),
                        new Text(region.Flags.ToString()),
                        new Text($"0x{region.KeyId:x}"),
                        new Text($"0x{region.Offset:x}"),
                        new Text($"0x{region.Length:x}"),
                        new Text($"0x{region.Hash:x}"),
                        new Text($"0x{region.FirstSegmentIndex:x}"),
                        //regionPresenceElement,
                        regionSpecifierElement
                    );
                }

                AnsiConsole.Write(regionTable);
            }
        }

        if (_hasUserData)
        {
            var userDataPanel = new Panel(
                    new Rows(
                    new Markup($"[white]Type[/]: [green bold]{_userDataHeader.Type}[/]"),
                    new Markup($"[white]Version[/]: [green bold]{_userDataHeader.Version}[/]"),
                    new Markup($"[white]Length[/]: [green bold]{_userDataHeader.Length}[/]"),
                    new Markup($"[white]Unknown[/]: [green bold]{_userDataHeader.Unknown}[/]")
                ))
                .Header("User Data")
                .RoundedBorder()
                .Expand();

            AnsiConsole.Write(userDataPanel);

            if (_userDataHeader.Type == XvdUserDataType.PackageFiles)
            {
                var packageFilesTable = new Table()
                    .Title("Package Files")
                    .AddColumns(
                        new TableColumn("Offset"),
                        new TableColumn("Size"),
                        new TableColumn("Size in Bytes"),
                        new TableColumn("File Path")
                    )
                    .RoundedBorder()
                    .Expand();

                foreach (var (_, entry) in _userDataPackages)
                {
                    packageFilesTable.AddRow(
                        new Markup($"[green bold]0x{entry.Offset:x8}[/]"), 
                        new Markup($"[green bold]0x{entry.Size:x8}[/]"), 
                        new Markup($"[green bold]{ToFileSize(entry.Size)}[/]"), 
                        new Markup($"[aqua underline]{entry.FilePath}[/]")
                    );
                }

                AnsiConsole.Write(packageFilesTable);
            }

            if (_hasSegmentMetadata)
            {
                var segmentMetadataPanel = new Panel(
                    new Rows(
                        new Markup($"[white]Segment Count[/]: [green]{_segmentMetadataHeader.SegmentCount}[/]"),
                        new Markup($"[white]Version0[/]: [green]{_segmentMetadataHeader.Version0}[/]"),
                        new Markup($"[white]Version1[/]: [green]{_segmentMetadataHeader.Version1}[/]")
                    ))
                    .Header("Segment Metadata")
                    .RoundedBorder()
                    .Expand();

                AnsiConsole.Write(segmentMetadataPanel);

                var segmentTable = new Table()
                    .Title("Segment Files")
                    .AddColumns(
                        new TableColumn("Start Page"),
                        new TableColumn("File Size"),
                        new TableColumn("Size in Bytes"),
                        new TableColumn("Hash"),
                        new TableColumn("Flags"),
                        new TableColumn("File Path")
                    )
                    .RoundedBorder()
                    .Expand();

                for (int i = 0; 
                     i < (showAllFiles ? _segments.Length : Math.Min(_segments.Length, 0x1000)); 
                     i++)
                {
                    var segment = _segments[i];
                    var updateSegment = _xvcUpdateSegments[i];

                    segmentTable.AddRow(
                        new Markup($"[green]0x{updateSegment.PageNum:x8}[/]"),
                        new Markup($"[green]0x{segment.FileSize:x16}[/]"),
                        new Markup($"[green]{ToFileSize(segment.FileSize)}[/]"),
                        new Markup($"[green]0x{updateSegment.Hash:x16}[/]"),
                        new Markup(segment.Flags != 0x0 ? $"[fuchsia]0x{(byte)segment.Flags:x2}[/]" : $"0x{segment.Flags}"),
                        new Markup($"[aqua underline]{_segmentPaths[i]}[/]")
                    );
                }

                if (!showAllFiles && _segments.Length > 0x1000)
                {
                    segmentTable.AddEmptyRow();
                    segmentTable.AddRow(new Markup("[red bold]<Too many files to print>[/]"));
                }

                AnsiConsole.Write(segmentTable);
            }
        }

        if (_hasPartitionFiles && !_hasSegmentMetadata)
        {
            var fileSystemFilesTable = new Table()
                .Title("Partition Files")
                .AddColumns(
                    new TableColumn("File Size"),
                    new TableColumn("Size in Bytes"),
                    new TableColumn("File Path")
                )
                .RoundedBorder()
                .Expand();

            for (int i = 0;
                 i < (showAllFiles ? _partitionFileEntries.Length : Math.Min(_partitionFileEntries.Length, 0x1000));
                 i++)
            {
                var file = _partitionFileEntries[i];

                fileSystemFilesTable.AddRow(
                    new Markup($"[green]0x{file.Size:x16}[/]"),
                    new Markup($"[green]{ToFileSize(file.Size)}[/]"),
                    new Markup($"[aqua underline]{file.Path}[/]")
                );
            }

            if (!showAllFiles && _segments.Length > 0x1000)
            {
                fileSystemFilesTable.AddEmptyRow();
                fileSystemFilesTable.AddRow(new Markup("[red bold]<Too many files to print>[/]"));
            }

            AnsiConsole.Write(fileSystemFilesTable);
        }

        return AnsiConsole.ExportText();

        static Rows StringToRows(string text)
        {
            return new Rows(text.Split(Environment.NewLine).Select(x => new Text(x.Trim())));
        }

        static string ToFileSize(ulong size)
        {
            if (size < 1024)
                return $"{size} B";

            if (size < 1024 * 1024)
                return $"{Math.Round(size / 1024.0, 2)} KB";

            if (size < 1024 * 1024 * 1024)
                return $"{Math.Round(size / (1024.0 * 1024.0), 2)} MB";

            return $"{Math.Round(size / (1024.0 * 1024.0 * 1024.0), 2)} GB";
        }
    }

    public void Dispose()
    {
        _reader.Dispose();
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }
}