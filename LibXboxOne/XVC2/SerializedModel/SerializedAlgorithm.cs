namespace LibXboxOne.XVC2.SerializedModel;

public enum SerializedAlgorithm
{
    None = 0,
    Deflate = 1,
    Brotli = 2,
    AES_256_CBC = 256,
    AES_256_KW = 257,
    FastCDC = 512,
    Fixed = 513,
    SHA256 = 768,
    SHA384 = 769,
    SHA512 = 770,
    SP800_108_HMAC_SHA256 = 1024,
}