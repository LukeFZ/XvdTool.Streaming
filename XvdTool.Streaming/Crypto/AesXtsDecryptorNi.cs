namespace XvdTool.Streaming.Crypto;

// Everything in here and /Crypto taken from Thealexbarney's LibHac (https://github.dev/Thealexbarney/LibHac)
// Only changes are making .Decrypt take the iv instead of the constructor

public class AesXtsDecryptorNi
{
    private AesXtsCipherNi _cipher;

    public AesXtsDecryptorNi(ReadOnlySpan<byte> data, ReadOnlySpan<byte> tweak)
    {
        _cipher = new AesXtsCipherNi();
        _cipher.Initialize(data, tweak, true);
    }

    public int Transform(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> tweakIv)
    {
        return _cipher.Decrypt(input, output, tweakIv);
    }
}