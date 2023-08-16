using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System;

namespace XvdTool.Streaming;

public sealed class HttpFileStream : Stream
{
    public string Url { get; }

    private readonly HttpClient _httpClient = new();
    private long _contentLength = -1;

    private HttpFileStream(string url)
    {
        Url = url;
    }

    public static HttpFileStream Open(string url)
    {
        var stream = new HttpFileStream(url);
        stream.GetFileLength();

        return stream;
    }

    private void GetFileLength()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, Url);
        request.Headers.Range = new RangeHeaderValue(0, 0);

        var response = _httpClient.Send(request);
        response.EnsureSuccessStatusCode();

        var contentRange = response.Content.Headers.ContentRange;

        if (contentRange == null || contentRange.Unit == "none")
            throw new InvalidOperationException("URL does not support 'Range:' header.");

        if (contentRange.Unit != "bytes")
            throw new InvalidOperationException(
                $"URL supports 'Range:' header but uses invalid unit {contentRange.Unit}.");

        if (!contentRange.HasLength)
            throw new InvalidOperationException("URL supports 'Range:' header but did not respond with content length.");

        _contentLength = contentRange.Length!.Value;
    }

    private Stream GetRangeStream(int count)
    {
        var actualLength = Math.Min(Position + count, _contentLength);

        var header = new RangeHeaderValue(Position, actualLength);

#if DEBUG
        Console.WriteLine($"Sending request to read from {Position} to {actualLength} (0x{actualLength - Position:x8})");
#endif

        var request = new HttpRequestMessage(HttpMethod.Get, Url);
        request.Headers.Range = header;

#if DEBUG
        var stop = Stopwatch.StartNew();
#endif

        var response = _httpClient.Send(request);
        response.EnsureSuccessStatusCode();

#if DEBUG
        stop.Stop();
        Console.WriteLine($"Request took {stop.ElapsedMilliseconds} ms");
#endif

        Position = actualLength;

        return response.Content.ReadAsStream();
    }

    private async Task<Stream> GetRangeStreamAsync(int count, CancellationToken cancellationToken)
    {
        var actualLength = Math.Min(Position + count, _contentLength);

        var header = new RangeHeaderValue(Position, actualLength);

#if DEBUG
        Console.WriteLine($"Sending request to read from {Position} to {actualLength}");
#endif

        var request = new HttpRequestMessage(HttpMethod.Get, Url);
        request.Headers.Range = header;

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        Position = actualLength;

        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (Position >= _contentLength)
            return 0;

        using var responseStream = GetRangeStream(count);
        return responseStream.Read(buffer, offset, count);
    }

    public override int Read(Span<byte> buffer)
    {
        if (Position >= _contentLength)
            return 0;

        using var responseStream = GetRangeStream(buffer.Length);
        return responseStream.Read(buffer);
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (Position >= _contentLength)
            return 0;

        await using var responseStream = await GetRangeStreamAsync(count, cancellationToken);
        return await responseStream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (Position >= _contentLength)
            return 0;

        await using var responseStream = await GetRangeStreamAsync(buffer.Length, cancellationToken);
        return await responseStream.ReadAsync(buffer, cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => _contentLength - offset,
            _ => throw new UnreachableException()
        };

        if (newPosition >= _contentLength)
            throw new IOException("Cannot seek past file end");

        Position = newPosition;
        return Position;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _httpClient.Dispose();
        }

        base.Dispose(disposing);
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _contentLength;
    public override long Position { get; set; }

    #region Unimplemented Methods

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    public override void Flush()
    {
        throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    #endregion
}