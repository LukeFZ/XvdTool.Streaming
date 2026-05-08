#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using LibXboxOne.XVC2.SerializedModel;

namespace LibXboxOne.XVC2;

public partial class Msixvc2File
{
    private readonly Dictionary<Guid, byte[]> _storedKeyMaterial = [];

    public void SubmitKeyMaterial(Guid keyId, byte[] keyMaterial)
    {
        _storedKeyMaterial[keyId] = keyMaterial;
    }

    private void DecryptContent(ReadOnlySpan<byte> encrypted, ReadOnlySpan<byte> iv, Span<byte> decrypted, int keyId,
        byte[]? encryptionKeyMaterial, byte[]? wrappedKey, PackagingIV? wrapIV, PackagingKeyPurpose purpose)
    {
        var key = _package.Keys[keyId];
        var keySource = key.Sources.First(x => x.SourcePurpose == purpose); 

        if (wrappedKey != null)
        {
            var wrapKey = DeriveWrappingKey(keySource, keySource.WrapAlgorithm);
            encryptionKeyMaterial = wrapKey.UnwrapKeyMaterial(wrappedKey, wrapIV);
        }

        if (encryptionKeyMaterial != null)
        {
            var encryptionKey = PackagingEncryptionKey.Create(keySource.Algorithm, encryptionKeyMaterial);
            encryptionKey.Decrypt(encrypted, PackagingIV.FromBytes(iv[..PackagingIV.Size]), decrypted);
        }
    }

    private IPackagingEncryptionKey DeriveWrappingKey(PackageKeySource keySource, PackagingEncryptionAlgorithm algorithm)
    {
        // Is this really how it's supposed to be done? since .PackageData is only used here

        var wrapKeyMaterial = _storedKeyMaterial[keySource.SourceKeyId];
        var derivationKey = PackagingDerivationKey.Create(keySource.DerivationAlgorithm, wrapKeyMaterial);
        var derivedKey = derivationKey.DeriveKey(PackagingKeyPurpose.PackageData, keySource.WrapAlgorithm, keySource.KdfContext);

        var wrappingKey = derivedKey.UnwrapKeyMaterial(keySource.WrappedKey, keySource.WrapIV);
        return PackagingEncryptionKey.Create(algorithm, wrappingKey);
    }
}