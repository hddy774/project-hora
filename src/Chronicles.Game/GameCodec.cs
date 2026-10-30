using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;

namespace Chronicles.Game;

public static class GameCodec
{
    private const string Magic = "SEONRO-021";
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
    public static string Hash(GameState value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, Options)));
    internal static string CommandHash(GameCommand value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, Options)));

    public static byte[] Encode(GameWorld world) => Encode(world, 0);

    internal static byte[] Encode(GameWorld world, long sequence)
    {
        ArgumentNullException.ThrowIfNull(world);
        var snapshot = world.Checkpoint(); Invariants.Validate(snapshot);
        var payload = JsonSerializer.Serialize(snapshot, Options);
        return JsonSerializer.SerializeToUtf8Bytes(new Envelope(Magic, 2, PayloadHash(payload, sequence), payload, sequence), Options);
    }

    private static string PayloadHash(string payload, long sequence) => Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes(sequence.ToString(CultureInfo.InvariantCulture) + "\n" + payload)));

    public static GameWorld Decode(ReadOnlySpan<byte> bytes) => DecodeEnvelope(bytes).World;

    internal static (GameWorld World, long Sequence) DecodeEnvelope(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(bytes, Options) ?? throw new InvalidDataException("빈 저장입니다.");
            if (envelope.Format != Magic || envelope.Schema != 2 || envelope.Sequence < 0)
                throw new InvalidDataException("0.2.1 저장 형식이 아닙니다. 원본은 보존됩니다.");
            var actual = PayloadHash(envelope.Payload, envelope.Sequence);
            if (!string.Equals(actual, envelope.Hash, StringComparison.Ordinal)) throw new InvalidDataException("저장 체크섬이 다릅니다.");
            return (GameWorld.Restore(JsonSerializer.Deserialize<GameState>(envelope.Payload, Options) ??
                throw new InvalidDataException("빈 세계입니다.")), envelope.Sequence);
        }
        catch (JsonException e) { throw new InvalidDataException("저장 JSON이 손상되었습니다.", e); }
        catch (Exception e) when (e is ArgumentException or NullReferenceException or OverflowException or NotSupportedException)
        { throw new InvalidDataException("저장의 자료 구조가 올바르지 않습니다.", e); }
    }

}
