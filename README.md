# XvdTool.Streaming

Rewritten and optimized version of XVDTool that lets you view information and extract files from streamed (remote, by URL) XVC/XVD files.
Also allows for very fast extraction/decryption/hash-verification of local XVC/XVD files.

Commands supported for both local and streamed types: 
- `info`
    - Lets you view detailed information (headers, regions, segments, files) for a given file.
- `extract`
    - Lets you extract and decrypted the embedded files contained within a XVC.  
    *Note: Only supports the newer type of XVC which do not just contain a disk partition. (SegmentMetadata.bin)*

Commands only supported by local files:
- `verify`
    - Validates the embedded hashes to check for any corruption.
- `decrypt`
    - Decrypts the file contents.

Some speed estimates on an NVMe drive:
- File extraction from local file (Hash Check enabled): ~200MB/s
- File extraction from local file (Hash Check disbaled): ~800MB/s
- Local file decryption: ~1GB/s

Please note that you still need to acquire the respective CIK for a package before you are able to extract or decrypt it.  
For further information on that, check out [CikExtractor](https://github.com/LukeFZ/CikExtractor).

For further information about XVC/XVD files in general, check out the original [XVDTool repository](https://github.com/emoose/xvdtool).

Thanks to [emoose](https://github.com/emoose), [tuxuser](https://github.com/tuxuser) & contributors for developing the original [XVDTool](https://github.com/emoose/xvdtool).

### Usage
```
USAGE:
    XvdTool.Streaming.exe [OPTIONS] <COMMAND>

EXAMPLES:
    XvdTool.Streaming.exe info c:/file.msixvc
    XvdTool.Streaming.exe info c:/file.msixvc -o log.txt
    XvdTool.Streaming.exe info https://assets1.xboxlive.com/...
    XvdTool.Streaming.exe extract c:/file.msixvc
    XvdTool.Streaming.exe extract c:/file.msixvc -o c:/output

OPTIONS:
    -h, --help       Prints help information
    -v, --version    Prints version information

COMMANDS:
    info <path/url>       Prints information about a given file
    extract <path/url>    Decrypts and extracts the files contained in a given file
    verify <path/url>     Checks the integrity of the given file. (Local only)
    decrypt <path/url>    Decrypts the given file. (Local only)
```

### Third party libraries used
- LibXboxOne (modified, from [regular XVDTool](https://github.com/emoose/xvdtool)):
    * [DiscUtils](https://github.com/DiscUtils/DiscUtils)
- This tool:
    * [BouncyCastle](https://bouncycastle.org)
    * [DotNext](https://github.com/dotnet/dotnext)
    * [Spectre.Console](https://spectreconsole.net)
    * Slightly modified fast AES-XTS Implementation from [Thealexbarney's](https://github.com/thealexbarney) [LibHac](https://github.com/thealexbarney/libhac)
