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

    private static byte[] Encode(GameWorld world, long sequence)
    {
        ArgumentNullException.ThrowIfNull(world);
        var snapshot = world.Checkpoint(); Invariants.Validate(snapshot);
        var payload = JsonSerializer.Serialize(snapshot, Options);
        return JsonSerializer.SerializeToUtf8Bytes(new Envelope(Magic, 2, PayloadHash(payload, sequence), payload, sequence), Options);
    }

    private static string PayloadHash(string payload, long sequence) => Convert.ToHexStringLower(SHA256.HashData(
        Encoding.UTF8.GetBytes(sequence.ToString(CultureInfo.InvariantCulture) + "\n" + payload)));

    public static GameWorld Decode(ReadOnlySpan<byte> bytes) => DecodeEnvelope(bytes).World;

    private static (GameWorld World, long Sequence) DecodeEnvelope(ReadOnlySpan<byte> bytes)
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

    public static void SaveAtomic(GameWorld world, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temporary = full + ".pending";
        var sequence = 0L;
        foreach (var candidate in new[] { full, temporary, full + ".backup" })
        {
            if (!File.Exists(candidate)) continue;
            try { sequence = Math.Max(sequence, DecodeEnvelope(File.ReadAllBytes(candidate)).Sequence); }
            catch (InvalidDataException) { PreserveOriginal(candidate); }
        }
        var bytes = Encode(world, checked(sequence + 1));
        using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough))
        { output.Write(bytes); output.Flush(true); }
        // Validate the exact bytes before replacing a valid slot. Backup stays readable after interruption.
        Decode(File.ReadAllBytes(temporary));
        if (File.Exists(full))
        {
            try { Decode(File.ReadAllBytes(full)); File.Copy(full, full + ".backup", true); }
            catch (InvalidDataException) { PreserveOriginal(full); }
        }
        File.Move(temporary, full, true);
    }

    private static void PreserveOriginal(string path)
    {
        using var source = File.OpenRead(path);
        var hash = Convert.ToHexStringLower(SHA256.HashData(source));
        var directory = Path.Combine(Path.GetDirectoryName(path)!, "preserved");
        Directory.CreateDirectory(directory);
        var retained = Path.Combine(directory, Path.GetFileName(path) + "." + hash + ".original");
        if (!File.Exists(retained)) File.Copy(path, retained, false);
    }

    public static GameWorld Load(string path) => LoadWithSource(path).World;

    internal static (GameWorld World, string Source) LoadWithSource(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        GameWorld? selected = null; var sequence = -1L; var source = "";
        foreach (var candidate in new[] { (Path: path, Source: "primary"), (Path: path + ".pending", Source: "pending"),
                     (Path: path + ".backup", Source: "backup") })
        {
            try
            {
                var value = DecodeEnvelope(File.ReadAllBytes(candidate.Path));
                if (value.Sequence > sequence) { selected = value.World; sequence = value.Sequence; source = candidate.Source; }
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { }
        }
        return selected is null ? throw new InvalidDataException("복구할 수 있는 0.2.1 저장이 없습니다. 기존 파일은 보존됩니다.") : (selected, source);
    }
}
