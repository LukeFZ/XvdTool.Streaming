using System;
using System.Diagnostics;
using System.Security.Cryptography;

namespace LibXboxOne.XVC2;

public interface IPackagingEncryptionKey
{
    void Encrypt(ReadOnlySpan<byte> input, PackagingIV iv, Span<byte> output);
    void Decrypt(ReadOnlySpan<byte> input, PackagingIV iv, Span<byte> output);
    byte[] WrapKeyMaterial(ReadOnlySpan<byte> input, PackagingIV? iv);
    byte[] UnwrapKeyMaterial(ReadOnlySpan<byte> input, PackagingIV? iv);
}

public static class PackagingEncryptionKey
{
    public static IPackagingEncryptionKey Create(PackagingEncryptionAlgorithm algorithm, ReadOnlySpan<byte> key)
    {
        return algorithm switch
        {
            PackagingEncryptionAlgorithm.Automatic => new PackagingEncryptionAesCbc(key),
            PackagingEncryptionAlgorithm.AES_256_CBC => new PackagingEncryptionAesCbc(key),
            PackagingEncryptionAlgorithm.AES_256_KW => new PackagingEncryptionAesKw(key),
            _ => throw new UnreachableException()
        };
    }
}

public class PackagingEncryptionAesCbc : IPackagingEncryptionKey
{
    private readonly Aes _aes;

    public PackagingEncryptionAesCbc(ReadOnlySpan<byte> key)
    {
        _aes = Aes.Create();
        _aes.Key = key.ToArray();
    }

    public void Encrypt(ReadOnlySpan<byte> input, PackagingIV iv, Span<byte> output)
    {
        _aes.EncryptCbc(input, iv.ToArray(), output, PaddingMode.None);
    }

    public void Decrypt(ReadOnlySpan<byte> input, PackagingIV iv, Span<byte> output)
    {
        _aes.DecryptCbc(input, iv.ToArray(), output, PaddingMode.None);
    }

    public byte[] WrapKeyMaterial(ReadOnlySpan<byte> input, PackagingIV? iv)
    {
        if (iv == null)
            throw new InvalidOperationException("IV required for CBC key wrapping");

        var output = new byte[input.Length];
        Encrypt(input, iv.Value, output);
        return output;
    }

    public byte[] UnwrapKeyMaterial(ReadOnlySpan<byte> input, PackagingIV? iv)
    {
        if (iv == null)
            throw new InvalidOperationException("IV required for CBC key unwrapping");

        var output = new byte[input.Length];
        Decrypt(input, iv.Value, output);
        return output;
    }
}

public class PackagingEncryptionAesKw : IPackagingEncryptionKey
{
    private readonly Aes _aes;

    public PackagingEncryptionAesKw(ReadOnlySpan<byte> key)
    {
        _aes = Aes.Create();
        _aes.Key = key.ToArray();
    }
    public void Encrypt(ReadOnlySpan<byte> input, PackagingIV iv, Span<byte> output)
    {
        throw new NotImplementedException();
    }

    public void Decrypt(ReadOnlySpan<byte> input, PackagingIV iv, Span<byte> output)
    {
        throw new NotImplementedException();
    }

    public byte[] WrapKeyMaterial(ReadOnlySpan<byte> input, PackagingIV? iv)
    {
        if (iv != null)
            throw new InvalidOperationException("No IV needed for AES-KW");

        return _aes.EncryptKeyWrapPadded(input);
    }

    public byte[] UnwrapKeyMaterial(ReadOnlySpan<byte> input, PackagingIV? iv)
    {
        if (iv != null)
            throw new InvalidOperationException("No IV needed for AES-KW");

        return _aes.DecryptKeyWrapPadded(input);
    }
}