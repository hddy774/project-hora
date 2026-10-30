using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Chronicles.Game.Tests.Features;

internal sealed record InventoryReferenceSave(long Month, byte[] Bytes);
internal sealed record InventoryReferenceTrace(string Fixture, string Mode, string[] Rows, InventoryReferenceSave[] Saves,
    string Digest, long ProjectionSnapshots, long TypedSnapshots)
{
    internal void AssertFixed(string expected) => InventoryFixture.Check(Digest == expected,
        "고정 strict 기준과 명령·월 경계·저장 해시가 달라졌습니다: " + Digest);

    internal void AssertEquivalent(InventoryReferenceTrace other)
    {
        InventoryFixture.Check(Fixture == other.Fixture && Rows.SequenceEqual(other.Rows), "독립 복사 경로의 전체 명령 응답·거부·월 경계 전사가 다릅니다.");
        InventoryFixture.Check(Saves.Length == other.Saves.Length && Saves.Zip(other.Saves).All(pair => pair.First.Month == pair.Second.Month && pair.First.Bytes.SequenceEqual(pair.Second.Bytes)),
            "독립 복사 경로의 실제 저장 bytes가 다릅니다.");
    }

    internal void CaptureIfRequested()
    {
        var directory = Environment.GetEnvironmentVariable("CHRONICLES_INVENTORY_REFERENCE_CAPTURE");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        // This optional audit output never changes the fixed expected digest or bypasses any assertion.
        File.WriteAllText(Path.Combine(directory, Fixture + "-" + Mode + ".json"), JsonSerializer.Serialize(new
        {
            schema_version = 1, kind = "inventory-independent-reference-transcript", fixture = Fixture, mode = Mode,
            sourceAssembly = typeof(GameWorld).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            digest = Digest, rows = Rows, saves = Saves.Select(s => new { month = s.Month, bytes = s.Bytes.Length,
                sha256 = Convert.ToHexStringLower(SHA256.HashData(s.Bytes)) }), projectionSnapshots = ProjectionSnapshots, typedSnapshots = TypedSnapshots
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}

internal sealed class InventoryWorldTranscript
{
    private readonly GameWorld world;
    private readonly bool projected;
    private readonly bool typedCopy;
    private readonly List<string> rows;
    private readonly List<InventoryReferenceSave> saves = [];
    private GameWorld? saved;
    internal InventoryWorldTranscript(GameWorld world, bool projected = false, bool typedCopy = false)
    {
        if (typedCopy && !projected) throw new ArgumentException("Typed transcript requires the projected input contract.", nameof(typedCopy));
        this.world = world; this.projected = projected; this.typedCopy = typedCopy; rows = [world.StateHash()];
        InventoryFixture.Check(world.InventoryProjectionEnabled && !world.InventoryTypedCopyExperimentEnabled, "일반 세계의 기본 경로가 채택된 JSON Person 투영이 아닙니다.");
        if (typedCopy) world.EnableInventoryTypedCopyForExperiment(); else if (!projected) world.UseStrictInventoryForVerification();
    }
    internal void Command(GameCommand command, bool accepted = true)
    {
        var before = world.StateHash(); var result = world.Execute(command);
        InventoryFixture.Check(result.Accepted == accepted, command.Kind + ": " + result.Message);
        if (!accepted) InventoryFixture.Check(world.StateHash() == before, "거부된 공개 명령이 상태를 변경했습니다.");
        if (saved is not null) InventoryFixture.Check(saved.Execute(command) == result && saved.StateHash() == world.StateHash(), "저장 이후 실제 명령 응답 또는 상태가 갈라졌습니다.");
        rows.Add(JsonSerializer.Serialize(new { command, result, Hash = world.StateHash() }));
    }
    internal void Advance(long due)
    {
        while (world.Month < due)
        {
            world.AdvanceTo(world.Month + 1); saved?.AdvanceTo(world.Month);
            if (saved is not null) InventoryFixture.Check(world.StateHash() == saved.StateHash(), "저장 이후 실제 월 경계 결과가 갈라졌습니다.");
            rows.Add(world.StateHash());
        }
    }
    internal void Save()
    {
        var encoded = GameCodec.Encode(world); var restored = GameCodec.Decode(encoded);
        InventoryFixture.Check(world.StateHash() == restored.StateHash() && encoded.SequenceEqual(GameCodec.Encode(restored)), "저장 왕복이 실제 권위 상태를 변경했습니다.");
        InventoryFixture.Check(restored.InventoryProjectionEnabled && !restored.InventoryTypedCopyExperimentEnabled, "저장 재개가 채택된 기본 JSON Person 투영 경로를 사용하지 않습니다.");
        if (typedCopy) restored.EnableInventoryTypedCopyForExperiment(); else if (!projected) restored.UseStrictInventoryForVerification();
        saved = restored; saves.Add(new(world.Month, encoded)); rows.Add(Convert.ToHexStringLower(SHA256.HashData(encoded)));
    }
    internal InventoryReferenceTrace Complete(string fixture)
    {
        Invariants.Validate(world.Checkpoint());
        InventoryFixture.Check(projected ? world.InventoryProjectionSnapshots > 0 : world.InventoryProjectionSnapshots == 0, "선택된 strict/JSON factory를 실행하지 않았습니다.");
        InventoryFixture.Check(typedCopy ? world.InventoryTypedCopySnapshots > 0 : world.InventoryTypedCopySnapshots == 0, "선택된 typed/JSON factory를 실행하지 않았습니다.");
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", rows))));
        var trace = new InventoryReferenceTrace(fixture, !projected ? "strict" : typedCopy ? "typed15" : "json15", rows.ToArray(), saves.ToArray(), digest,
            world.InventoryProjectionSnapshots, world.InventoryTypedCopySnapshots);
        trace.CaptureIfRequested(); return trace;
    }
}
