using System;
using System.Diagnostics;
using System.Security.Cryptography;

namespace LibXboxOne.XVC2;

public interface IPackagingDerivationKey
{
    IPackagingEncryptionKey DeriveKey(PackagingKeyPurpose purpose,
        PackagingEncryptionAlgorithm algorithm, ReadOnlySpan<byte> context);
}

public static class PackagingDerivationKey
{
    public static IPackagingDerivationKey Create(PackagingDerivationAlgorithm algorithm, ReadOnlySpan<byte> key)
    {
        return algorithm switch
        {
            PackagingDerivationAlgorithm.None => new PackagingDerivationKeyNone(),
            PackagingDerivationAlgorithm.SP800_108_HMAC_SHA256 => new PackagingDerivationKeySp800108(key.ToArray()),
            _ => throw new UnreachableException()
        };
    }
}

public class PackagingDerivationKeyNone : IPackagingDerivationKey
{
    public IPackagingEncryptionKey DeriveKey(PackagingKeyPurpose purpose, PackagingEncryptionAlgorithm algorithm,
        ReadOnlySpan<byte> context)
    {
        throw new NotImplementedException();
    }
}

public class PackagingDerivationKeySp800108(byte[] key) : IPackagingDerivationKey
{
    private readonly byte[] _key = key;

    public IPackagingEncryptionKey DeriveKey(PackagingKeyPurpose purpose,
        PackagingEncryptionAlgorithm algorithm, ReadOnlySpan<byte> context)
    {
        var label = GetLabel(purpose, algorithm);
        var keySize = GetKeySize(algorithm);

        var derivedKey = SP800108HmacCounterKdf.DeriveBytes(_key, HashAlgorithmName.SHA256, label, context, keySize);
        return PackagingEncryptionKey.Create(algorithm, derivedKey);
    }

    private static int GetKeySize(PackagingEncryptionAlgorithm encryptionAlgorithm)
    {
        if (encryptionAlgorithm == PackagingEncryptionAlgorithm.AES_256_CBC)
            return 32;

        throw new InvalidOperationException($"No key size defined for {encryptionAlgorithm}");
    }

    private static ReadOnlySpan<byte> GetLabel(PackagingKeyPurpose keyPurpose, PackagingEncryptionAlgorithm encryptionAlgorithm)
    {
        if (keyPurpose == PackagingKeyPurpose.PackageData &&
            encryptionAlgorithm == PackagingEncryptionAlgorithm.AES_256_CBC)
        {
            return "MSIXVC2:PackageData:AES_256_CBC"u8;
        }

        throw new InvalidOperationException($"No label defined for {keyPurpose} in {encryptionAlgorithm}");
    }
}