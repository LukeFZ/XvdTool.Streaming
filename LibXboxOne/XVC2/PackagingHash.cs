using System.Buffers.Text;

namespace LibXboxOne.XVC2;

public readonly record struct PackagingHash(PackagingHashAlgorithm Algorithm, byte[] Hash)
{
    public override string ToString() => $"{Algorithm.ToString().ToLower()}:{Base64Url.EncodeToString(Hash)}";
}