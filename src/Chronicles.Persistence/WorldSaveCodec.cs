using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Chronicles.Simulation;

namespace Chronicles.Persistence;

public static class WorldSaveCodec
{
    private const int ContainerVersion = 1;
    private const int ChecksumLength = 32;
    private const int MaximumPayloadBytes = 64 * 1024 * 1024;
    private const int MaximumJsonBytes = 128 * 1024 * 1024;
    private static readonly byte[] Magic = "CMIP"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static byte[] Encode(WorldCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var json = JsonSerializer.SerializeToUtf8Bytes(checkpoint, JsonOptions);
        using var payloadStream = new MemoryStream();
        using (var compression = new BrotliStream(payloadStream, CompressionLevel.Optimal, leaveOpen: true))
        {
            compression.Write(json);
        }

        var payload = payloadStream.ToArray();
        if (payload.Length > MaximumPayloadBytes)
        {
            throw new InvalidDataException("저장 payload가 허용 크기를 초과했습니다.");
        }

        var checksum = SHA256.HashData(payload);
        using var container = new MemoryStream(Magic.Length + 8 + ChecksumLength + payload.Length);
        using var writer = new BinaryWriter(container);
        writer.Write(Magic);
        writer.Write(ContainerVersion);
        writer.Write(payload.Length);
        writer.Write(checksum);
        writer.Write(payload);
        writer.Flush();
        return container.ToArray();
    }

    public static WorldCheckpoint Decode(ReadOnlySpan<byte> container)
    {
        var minimumLength = Magic.Length + 8 + ChecksumLength;
        if (container.Length < minimumLength)
        {
            throw new InvalidDataException("저장 파일이 너무 짧습니다.");
        }

        using var stream = new MemoryStream(container.ToArray(), writable: false);
        using var reader = new BinaryReader(stream);
        if (!reader.ReadBytes(Magic.Length).AsSpan().SequenceEqual(Magic))
        {
            throw new InvalidDataException("저장 파일 magic이 일치하지 않습니다.");
        }

        var version = reader.ReadInt32();
        if (version != ContainerVersion)
        {
            throw new InvalidDataException($"지원하지 않는 저장 container {version}입니다.");
        }

        var payloadLength = reader.ReadInt32();
        if (payloadLength < 0
            || payloadLength > MaximumPayloadBytes
            || payloadLength != stream.Length - stream.Position - ChecksumLength)
        {
            throw new InvalidDataException("저장 payload 길이가 잘못되었습니다.");
        }

        var expectedChecksum = reader.ReadBytes(ChecksumLength);
        var payload = reader.ReadBytes(payloadLength);
        var actualChecksum = SHA256.HashData(payload);
        if (!CryptographicOperations.FixedTimeEquals(expectedChecksum, actualChecksum))
        {
            throw new InvalidDataException("저장 파일 checksum이 일치하지 않습니다.");
        }

        var json = Decompress(payload);
        try
        {
            var checkpoint = JsonSerializer.Deserialize<WorldCheckpoint>(json, JsonOptions);
            return checkpoint ?? throw new InvalidDataException("세계 체크포인트를 읽을 수 없습니다.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("세계 체크포인트 JSON이 잘못되었습니다.", exception);
        }
    }

    private static byte[] Decompress(byte[] payload)
    {
        using var input = new MemoryStream(payload, writable: false);
        using var compression = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[81_920];
        while (true)
        {
            var count = compression.Read(buffer, 0, buffer.Length);
            if (count == 0)
            {
                break;
            }

            if (output.Length + count > MaximumJsonBytes)
            {
                throw new InvalidDataException("압축 해제된 저장 데이터가 허용 크기를 초과했습니다.");
            }

            output.Write(buffer, 0, count);
        }

        return output.ToArray();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        options.Converters.Add(new AgentIdJsonConverter());
        options.Converters.Add(new SimulationTickJsonConverter());
        return options;
    }
}
