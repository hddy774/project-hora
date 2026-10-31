using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chronicles.Game;

namespace Chronicles.Game.Tests;

public sealed class LegacySaveEnvelopeCompatibilityScenario : IFeatureScenario
{
    public string Name => "INV-004 대용량 저장: 기존 schema2 바이트·고정 원본·구버전 정책 보존";

    public void Run()
    {
        var world = GameWorld.Create(191);
        // Captured from the unmodified 9704e955 Release assembly before this codec change.
        string[] golden = ["efbb54d69fb462bc8b553c43989adf9c02f20e6831170bdeba6dc46ad9658f7a",
            "e4bd98ffc8c1e8c81c4db47c99282cb22afc32dadd6e55642acc73b65796f891"];
        long[] sequences = [0, 7];
        for (var i = 0; i < sequences.Length; i++)
        {
            var oldBytes = LargeSaveFixture.Legacy(world, sequences[i]);
            var actual = GameCodec.Encode(world, sequences[i]);
            LargeSaveFixture.Check(oldBytes.Length == 1887246 && Convert.ToHexStringLower(SHA256.HashData(oldBytes)) == golden[i],
                "독립 기존 encoder가 수정 전 원본과 달라졌습니다.");
            LargeSaveFixture.Check(actual.AsSpan().SequenceEqual(oldBytes), "작은 세계의 schema2 실제 바이트가 변경됐습니다.");
            var oldLoaded = GameCodec.DecodeEnvelope(oldBytes);
            LargeSaveFixture.Check(oldLoaded.Sequence == sequences[i] && oldLoaded.World.StateHash() == world.StateHash(), "기존 원본을 읽지 못합니다.");
        }
        LargeSaveFixture.Invalid(() => GameCodec.Decode(LargeSaveFixture.Legacy(world, 0, 1)));
        LargeSaveFixture.Invalid(() => GameCodec.Decode(LargeSaveFixture.Legacy(world, 0, 3)));
        var snapshot = world.Checkpoint(); snapshot.DomainSchema = 1;
        LargeSaveFixture.Invalid(() => GameCodec.Decode(LargeSaveFixture.WrapBinary(JsonSerializer.SerializeToUtf8Bytes(snapshot), 0)));
    }
}

public sealed class LargeSaveEnvelopeRoundTripScenario : IFeatureScenario
{
    public string Name => "INV-004 대용량 저장: UTF8 양형식·경계·순번·명령 재전송·RNG 저장 속행";

    public void Run()
    {
        var world = GameWorld.Create(191);
        var actor = world.Checkpoint().People[0].Id;
        var command = new GameCommand("large-save-possess", actor, "possess");
        LargeSaveFixture.Check(world.Execute(command).Accepted, "공개 조작 명령이 거부됐습니다.");
        var payload = JsonSerializer.SerializeToUtf8Bytes(world.Checkpoint(), LargeSaveFixture.Options);
        var binary = GameCodec.Encode(world, 7, 0);
        var boundaryJson = GameCodec.Encode(world, 7, payload.Length);
        var boundaryBinary = GameCodec.Encode(world, 7, payload.Length - 1);
        LargeSaveFixture.Check(boundaryJson[0] == '{' && binary.AsSpan().SequenceEqual(boundaryBinary), "엄밀한 본문 길이 경계를 선택하지 않았습니다.");
        LargeSaveFixture.Check(binary.AsSpan(64).SequenceEqual(payload), "canonical UTF8 원문을 그대로 보존하지 않았습니다.");
        LargeSaveFixture.Check(binary.AsSpan(32, 32).SequenceEqual(LargeSaveFixture.LogicalHash(payload, 7)), "기존 논리 체크섬과 다릅니다.");
        var legacy = GameCodec.DecodeEnvelope(boundaryJson); var large = GameCodec.DecodeEnvelope(binary);
        LargeSaveFixture.Check(legacy.Sequence == 7 && large.Sequence == 7 && large.World.StateHash() == world.StateHash(), "양형식 순번/hash 왕복 실패");
        LargeSaveFixture.Check(GameCodec.DecodeEnvelope(GameCodec.Encode(world, long.MaxValue, 0)).Sequence == long.MaxValue,
            "비음수 순번 상한을 잃었습니다.");
        var before = world.StateHash();
        LargeSaveFixture.Check(large.World.Execute(command) == legacy.World.Execute(command) && large.World.StateHash() == before,
            "실제 재전송이 권위 상태를 변경했습니다.");
        world.AdvanceTo(2); legacy.World.AdvanceTo(2); large.World.AdvanceTo(2);
        LargeSaveFixture.Check(world.StateHash() == legacy.World.StateHash() && world.StateHash() == large.World.StateHash() &&
            world.Checkpoint().RandomState == large.World.Checkpoint().RandomState, "저장 뒤 실제 진행/RNG가 달라졌습니다.");
        LargeSaveFixture.Check(GameCodec.Encode(legacy.World).AsSpan().SequenceEqual(GameCodec.Encode(large.World)), "속행 후 정상 작은 저장 바이트가 다릅니다.");
        var negativeRejected = false;
        try { GameCodec.Encode(world, -1, 0); } catch (ArgumentOutOfRangeException) { negativeRejected = true; }
        LargeSaveFixture.Check(negativeRejected, "음수 순번을 생성했습니다.");
    }
}

public sealed class LargeSaveEnvelopeCorruptionScenario : IFeatureScenario
{
    public string Name => "INV-004 대용량 저장: prefix·버전·길이·순번·체크섬·잘림·후행 자료 엄밀 거부";

    public void Run()
    {
        var bytes = GameCodec.Encode(GameWorld.Create(191), 7, 0);
        void RejectMutation(Action<byte[]> mutate)
        {
            var corrupt = (byte[])bytes.Clone(); mutate(corrupt);
            LargeSaveFixture.Invalid(() => GameCodec.Decode(corrupt));
        }
        RejectMutation(x => x[0] ^= 1);
        foreach (var version in new[] { 0, 2, 4 }) RejectMutation(x => BinaryPrimitives.WriteInt32LittleEndian(x.AsSpan(12, 4), version));
        RejectMutation(x => BinaryPrimitives.WriteInt64LittleEndian(x.AsSpan(16, 8), -1));
        RejectMutation(x => BinaryPrimitives.WriteInt64LittleEndian(x.AsSpan(16, 8), 8));
        foreach (var length in new long[] { -1, 0, bytes.Length - 65, bytes.Length - 63, long.MaxValue })
            RejectMutation(x => BinaryPrimitives.WriteInt64LittleEndian(x.AsSpan(24, 8), length));
        RejectMutation(x => x[32] ^= 1); RejectMutation(x => x[64] ^= 1);
        foreach (var length in new[] { 0, 1, 11, 12, 15, 16, 24, 31, 63, 64, bytes.Length - 1 })
            LargeSaveFixture.Invalid(() => GameCodec.Decode(bytes.AsSpan(0, length)));
        var trailing = new byte[bytes.Length + 1]; bytes.CopyTo(trailing, 0);
        LargeSaveFixture.Invalid(() => GameCodec.Decode(trailing));
        // Even correctly checksummed bodies must pass JSON and canonical world validation.
        foreach (var payload in new[] { "null"u8.ToArray(), "{"u8.ToArray(), "[]"u8.ToArray() })
            LargeSaveFixture.Invalid(() => GameCodec.Decode(LargeSaveFixture.WrapBinary(payload, 7)));
    }
}

public sealed class LargeSaveAtomicRecoveryScenario : IFeatureScenario
{
    public string Name => "INV-004 대용량 저장: 실제 원자파일 혼합형식·pending 우선순번·backup·손상 원문 보존";

    public void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "seonro-large-save-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var path = Path.Combine(directory, "world.save");
        try
        {
            var world = GameWorld.Create(191); var initialHash = world.StateHash();
            GameSaveStore.SaveAtomic(world, path);
            var original = File.ReadAllBytes(path);
            LargeSaveFixture.Check(original[0] == '{' && GameCodec.DecodeEnvelope(original).Sequence == 1, "기존 public 저장 경로가 변경됐습니다.");
            world.AdvanceTo(1); var laterHash = world.StateHash();
            GameSaveStore.SaveAtomic(world, path, 0);
            var binary = File.ReadAllBytes(path);
            LargeSaveFixture.Check(binary[0] == 'S' && GameCodec.DecodeEnvelope(binary).Sequence == 2 &&
                File.ReadAllBytes(path + ".backup").AsSpan().SequenceEqual(original), "큰 형식 원자저장 또는 기존 backup 바이트가 잘못됐습니다.");
            LargeSaveFixture.Check(GameSaveStore.Load(path).StateHash() == laterHash, "큰 형식 실제 파일 왕복 실패");
            // A valid later pending write wins over both formats regardless of the world's tick.
            File.WriteAllBytes(path + ".pending", GameCodec.Encode(GameWorld.Create(191), 3, 0));
            LargeSaveFixture.Check(GameSaveStore.Load(path).StateHash() == initialHash, "실제 pending 최고 순번 선택 실패");
            var badPrimary = (byte[])binary.Clone(); badPrimary[32] ^= 1;
            byte[] badPending = [1, 2, 3, 4, 5];
            File.WriteAllBytes(path, badPrimary); File.WriteAllBytes(path + ".pending", badPending);
            LargeSaveFixture.Check(GameSaveStore.Load(path).StateHash() == initialHash && File.ReadAllBytes(path).AsSpan().SequenceEqual(badPrimary),
                "손상 primary/pending 뒤 작은 backup 복구 또는 읽기 원문 보존 실패");
            GameSaveStore.SaveAtomic(world, path, 0);
            var preserved = Directory.GetFiles(Path.Combine(directory, "preserved"));
            LargeSaveFixture.Check(preserved.Any(file => File.ReadAllBytes(file).AsSpan().SequenceEqual(badPrimary)) &&
                preserved.Any(file => File.ReadAllBytes(file).AsSpan().SequenceEqual(badPending)), "재저장 전에 손상 파일 원문을 보존하지 않았습니다.");
            LargeSaveFixture.Check(GameCodec.DecodeEnvelope(File.ReadAllBytes(path)).Sequence == 2 && GameSaveStore.Load(path).StateHash() == laterHash,
                "유효 backup 기준 순번/상태 복구 후 원자저장 실패");
            world.AdvanceTo(2); GameSaveStore.SaveAtomic(world, path);
            LargeSaveFixture.Check(File.ReadAllBytes(path)[0] == '{' && File.ReadAllBytes(path + ".backup")[0] == 'S' &&
                GameCodec.DecodeEnvelope(File.ReadAllBytes(path)).Sequence == 3, "큰 backup에서 정상 작은 형식으로 이어 저장하지 못합니다.");
            File.WriteAllBytes(path + ".pending", GameCodec.Encode(world, long.MaxValue, 0));
            var paths = new[] { path, path + ".pending", path + ".backup" };
            var beforeFailure = paths.Select(File.ReadAllBytes).ToArray();
            var overflowRejected = false;
            try { GameSaveStore.SaveAtomic(world, path); } catch (OverflowException) { overflowRejected = true; }
            LargeSaveFixture.Check(overflowRejected && paths.Select(File.ReadAllBytes).Zip(beforeFailure)
                .All(pair => pair.First.AsSpan().SequenceEqual(pair.Second)), "순번이 넘친 저장 실패가 유효 primary/pending/backup을 덮었습니다.");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}

internal static class LargeSaveFixture
{
    internal static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = false, MaxDepth = 64 };
    private sealed record OldEnvelope(string Format, int Schema, string Hash, string Payload, long Sequence);

    internal static byte[] Legacy(GameWorld world, long sequence, int schema = 2)
    {
        var payload = JsonSerializer.Serialize(world.Checkpoint(), Options);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sequence.ToString(CultureInfo.InvariantCulture) + "\n" + payload)));
        return JsonSerializer.SerializeToUtf8Bytes(new OldEnvelope("SEONRO-021", schema, hash, payload, sequence), Options);
    }

    internal static byte[] LogicalHash(byte[] payload, long sequence) => SHA256.HashData(Encoding.UTF8.GetBytes(
        sequence.ToString(CultureInfo.InvariantCulture) + "\n" + Encoding.UTF8.GetString(payload)));

    internal static byte[] WrapBinary(byte[] payload, long sequence)
    {
        var bytes = new byte[64 + payload.Length]; "SEONRO-021B\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12, 4), 3);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(16, 8), sequence);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(24, 8), payload.Length);
        LogicalHash(payload, sequence).CopyTo(bytes, 32); payload.CopyTo(bytes, 64); return bytes;
    }

    internal static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    internal static void Invalid(Action action)
    {
        var rejected = false; try { action(); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "손상/지원하지 않는 저장이 InvalidDataException 없이 수락됐습니다.");
    }
}
