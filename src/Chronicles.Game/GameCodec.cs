using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Buffers;
using System.Buffers.Binary;

namespace Chronicles.Game;

public static class GameCodec
{
    private const string Magic = "SEONRO-021";
    // 중간 규모부터 기존 이진 봉투를 써서 JSON 문자열의 이중 escaping과 UTF16 임시 사본을 줄인다.
    internal const int JsonPayloadLimit = 16 * 1024 * 1024;
    private const int BinaryHeaderSize = 64;
    private static ReadOnlySpan<byte> BinaryMagic => "SEONRO-021B\0"u8;
    private sealed record Envelope(string Format, int Schema, string Hash, string Payload, long Sequence);
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = false, MaxDepth = 64 };

    internal static GameState Clone(GameState value)
    {
        var copy = JsonSerializer.Deserialize<GameState>(JsonSerializer.SerializeToUtf8Bytes(value, Options), Options)
            ?? throw new InvalidDataException("빈 세계입니다.");
        // JSON collection construction must not inherit the host's linguistic comparer.
        copy.Laws = new(copy.Laws, StringComparer.Ordinal);
        copy.Materials = new(copy.Materials, StringComparer.Ordinal);
        copy.Commands = new(copy.Commands, StringComparer.Ordinal);
        copy.CommandSignatures = new(copy.CommandSignatures, StringComparer.Ordinal);
        copy.Inventory.Receipts = new(copy.Inventory.Receipts, StringComparer.Ordinal);
        foreach (var p in copy.People)
        {
            p.Skills = new(p.Skills, StringComparer.Ordinal);
            p.Understanding = new(p.Understanding, StringComparer.Ordinal);
            p.Beliefs = new(p.Beliefs, StringComparer.Ordinal);
            p.Habits = new(p.Habits, StringComparer.Ordinal);
        }
        foreach (var g in copy.Groups)
        {
            g.Policies = new(g.Policies, StringComparer.Ordinal);
            g.PendingPolicies = new(g.PendingPolicies, StringComparer.Ordinal);
        }
        return copy;
    }
    public static string Hash(GameState value)
    {
        using var sink = new CanonicalHashSink();
        JsonSerializer.Serialize(sink, value, Options);
        return sink.CompleteHash();
    }
    internal static string CommandHash(GameCommand value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, Options)));

    public static byte[] Encode(GameWorld world) => Encode(world, 0);

    internal static byte[] Encode(GameWorld world, long sequence) => Encode(world, sequence, JsonPayloadLimit);

    // The explicit limit is an internal functional-test seam; it is not saved or exposed as an app setting.
    internal static byte[] Encode(GameWorld world, long sequence, int jsonPayloadLimit)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        ArgumentOutOfRangeException.ThrowIfNegative(jsonPayloadLimit);
        var snapshot = world.Checkpoint(); Invariants.Validate(snapshot);
        var payload = JsonSerializer.SerializeToUtf8Bytes(snapshot, Options);
        var hash = PayloadHash(payload, sequence);
        if (payload.Length <= jsonPayloadLimit)
            return EncodeJsonEnvelope(payload, Convert.ToHexStringLower(hash), sequence);
        if (payload.Length > Array.MaxLength - BinaryHeaderSize)
            throw new InvalidDataException("저장 본문이 지원하는 바이트 배열 크기를 넘었습니다.");
        // 12-byte magic, LE int32 version, LE int64 sequence/length, 32-byte logical SHA256, raw canonical JSON.
        var bytes = new byte[BinaryHeaderSize + payload.Length];
        BinaryMagic.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12, 4), 3);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(16, 8), sequence);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(24, 8), payload.Length);
        hash.CopyTo(bytes, 32);
        payload.CopyTo(bytes, BinaryHeaderSize);
        return bytes;
    }

    // schema2 문자열의 원래 escape를 유지하면서 전체 UTF16 사본 대신 작은 UTF8 조각을 쓴다.
    private static byte[] EncodeJsonEnvelope(ReadOnlySpan<byte> payload, string hash, long sequence)
    {
        var output = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(output, new JsonWriterOptions
        {
            Encoder = Options.Encoder,
            Indented = Options.WriteIndented,
            MaxDepth = Options.MaxDepth
        });
        writer.WriteStartObject();
        writer.WriteString("Format", Magic);
        writer.WriteNumber("Schema", 2);
        writer.WriteString("Hash", hash);
        writer.WritePropertyName("Payload");

        const int segmentBytes = 16 * 1024;
        int offset = 0;
        do
        {
            int count = Math.Min(segmentBytes, payload.Length - offset);
            writer.WriteStringValueSegment(payload.Slice(offset, count), offset + count == payload.Length);
            offset += count;
        }
        while (offset < payload.Length);

        writer.WriteNumber("Sequence", sequence);
        writer.WriteEndObject();
        writer.Flush();
        return output.WrittenSpan.ToArray();
    }

    private static byte[] PayloadHash(ReadOnlySpan<byte> payload, long sequence)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(sequence.ToString(CultureInfo.InvariantCulture) + "\n"));
        hash.AppendData(payload);
        return hash.GetHashAndReset();
    }

    public static GameWorld Decode(ReadOnlySpan<byte> bytes) => DecodeEnvelope(bytes).World;

    internal static (GameWorld World, long Sequence) DecodeEnvelope(ReadOnlySpan<byte> bytes)
    {
        try
        {
            if (bytes.StartsWith(BinaryMagic)) return DecodeBinary(bytes);
            var envelope = JsonSerializer.Deserialize<Envelope>(bytes, Options) ?? throw new InvalidDataException("빈 저장입니다.");
            if (envelope.Format != Magic || envelope.Schema != 2 || envelope.Sequence < 0)
                throw new InvalidDataException("0.2.1 저장 형식이 아닙니다. 원본은 보존됩니다.");
            var payload = Encoding.UTF8.GetBytes(envelope.Payload);
            var actual = Convert.ToHexStringLower(PayloadHash(payload, envelope.Sequence));
            if (!string.Equals(actual, envelope.Hash, StringComparison.Ordinal)) throw new InvalidDataException("저장 체크섬이 다릅니다.");
            return (GameWorld.Restore(JsonSerializer.Deserialize<GameState>(payload.AsSpan(), Options) ??
                throw new InvalidDataException("빈 세계입니다.")), envelope.Sequence);
        }
        catch (JsonException e) { throw new InvalidDataException("저장 JSON이 손상되었습니다.", e); }
        catch (Exception e) when (e is ArgumentException or NullReferenceException or OverflowException or NotSupportedException)
        { throw new InvalidDataException("저장의 자료 구조가 올바르지 않습니다.", e); }
    }

    private static (GameWorld World, long Sequence) DecodeBinary(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < BinaryHeaderSize || BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(12, 4)) != 3)
            throw new InvalidDataException("대용량 저장 헤더 또는 버전이 올바르지 않습니다.");
        var sequence = BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(16, 8));
        var length = BinaryPrimitives.ReadInt64LittleEndian(bytes.Slice(24, 8));
        if (sequence < 0 || length <= 0 || length > Array.MaxLength - BinaryHeaderSize || length != bytes.Length - BinaryHeaderSize)
            throw new InvalidDataException("대용량 저장 순번 또는 본문 길이가 올바르지 않습니다.");
        var payload = bytes[BinaryHeaderSize..];
        if (!CryptographicOperations.FixedTimeEquals(PayloadHash(payload, sequence), bytes.Slice(32, 32)))
            throw new InvalidDataException("저장 체크섬이 다릅니다.");
        return (GameWorld.Restore(JsonSerializer.Deserialize<GameState>(payload, Options) ??
            throw new InvalidDataException("빈 세계입니다.")), sequence);
    }
}
