using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace AlphaExchange.Core;

public sealed partial class GameStore
{
    static byte[] Compress(string json)
        => Compress(Encoding.UTF8.GetBytes(json));
    static byte[] Compress(byte[] bytes)
    {
        using var destination = new MemoryStream();
        using (var compressor = new BrotliStream(destination, CompressionLevel.Fastest, true))
        { compressor.Write(bytes); }
        return destination.ToArray();
    }
    static byte[] Inflate(byte[] data, int maximum = 128 * 1024 * 1024)
    {
        using var source = new MemoryStream(data); using var decompressor = new BrotliStream(source, CompressionMode.Decompress);
        using var destination = new MemoryStream(); byte[] buffer = new byte[32768]; int length;
        while ((length = decompressor.Read(buffer)) > 0)
        { if (destination.Length + length > maximum) throw new InvalidDataException("저장 데이터 크기 제한"); destination.Write(buffer, 0, length); }
        return destination.ToArray();
    }
    static string Decode(byte[] data, int maximum = 128 * 1024 * 1024) => Encoding.UTF8.GetString(Inflate(data, maximum));
    static string Hash(string json) => Hash(Encoding.UTF8.GetBytes(json));
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    static byte[] CheckedInflate(byte[] bytes, string hash, int limit = 128 * 1024 * 1024)
    { byte[] json = Inflate(bytes, limit); if (Hash(json) != hash) throw new InvalidDataException("저장 체크섬 불일치"); return json; }
    static string CheckedDecode(byte[] bytes, string hash, int limit = 128 * 1024 * 1024) => Encoding.UTF8.GetString(CheckedInflate(bytes, hash, limit));
}
