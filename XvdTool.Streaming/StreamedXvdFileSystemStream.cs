using System.Diagnostics;

namespace XvdTool.Streaming;

public class StreamedXvdFileSystemStream(long length, long driveOffset, long staticDataLength, long dynamicOffset, Stream baseStream) : Stream
{
    public override long Length { get; } = length;
    public override long Position { get; set; }
    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;

    private readonly long _driveOffset = driveOffset;
    private readonly long _staticDataLength = staticDataLength;
    private readonly long _dynamicOffset = dynamicOffset;
    private readonly Stream _fileStream = baseStream;

    public override int Read(byte[] buffer, int offset, int count)
        => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var readPosition = _driveOffset + Position;

        _fileStream.Position = readPosition;
        var count = _fileStream.Read(buffer);
        Position += count;
        return count;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => offset + Position,
            SeekOrigin.End => Length + offset,
            _ => throw new UnreachableException()
        };

        return Position;
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public override void Flush()
    {
        throw new NotImplementedException();
    }
}