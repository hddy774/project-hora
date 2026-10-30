using System.Text.Json.Nodes;
using Chronicles.Game;

namespace Chronicles.Game.Tests;

public sealed class HostRecoveryScenarios : IFeatureScenario
{
    public string Name => "INV-004/005 host recovery: committed pending saves, authenticated sequence and immutable old originals";

    public void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "seonro-recovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "world-0.seonro");
            var world = GameWorld.Create(42, new Dictionary<string, long> { ["birth.enabled"] = 0 });
            GameCodec.SaveAtomic(world, path); var previous = File.ReadAllBytes(path); var priorHash = world.StateHash();
            world.AdvanceTo(1); GameCodec.SaveAtomic(world, path); var current = File.ReadAllBytes(path);
            File.WriteAllBytes(path, previous); File.WriteAllBytes(path + ".pending", current);
            Require(GameCodec.Load(path).StateHash() == world.StateHash(), "최근 완전 pending이 이전 primary보다 우선");
            var session = new GameSession(directory);
            var recovered = session.Handle(new());
            Require(recovered.View.Month == 1 && recovered.Message.Contains("중단", StringComparison.Ordinal), "호스트 복구 사실 표시");
            File.WriteAllText(path + ".pending", "부분 파일");
            Require(GameCodec.Load(path).StateHash() == priorHash, "부분 pending은 유효 primary를 덮지 않음");
            File.WriteAllText(path, "손상"); File.WriteAllBytes(path + ".pending", current);
            Require(GameCodec.Load(path).StateHash() == world.StateHash(), "손상 primary에서 완전 pending 복구");
            File.WriteAllText(path + ".pending", "부분 파일");
            Require(GameCodec.Load(path).StateHash() == priorHash, "primary와 pending 손상 시 유효 backup 복구");
            var altered = JsonNode.Parse(current)!;
            altered["Sequence"] = altered["Sequence"]!.GetValue<long>() + 1;
            var rejected = false;
            try { GameCodec.Decode(System.Text.Encoding.UTF8.GetBytes(altered.ToJsonString())); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "호스트 저장 순서도 체크섬으로 검증");

            var oldDirectory = Path.Combine(directory, "old-format"); Directory.CreateDirectory(oldDirectory);
            var oldPath = Path.Combine(oldDirectory, "world-0.seonro"); var original = new byte[] { 0xff, 0, 0x12, 0x7f };
            File.WriteAllBytes(oldPath, original);
            var fresh = new GameSession(oldDirectory);
            Require(fresh.Handle(new()).Message.Contains("원본", StringComparison.Ordinal), "이전 파일을 숨기고 조용히 초기화하지 않음");
            fresh.Handle(new("save")); fresh.Handle(new("advance", Months: 1)); fresh.Handle(new("save")); fresh.Suspend();
            var retained = Directory.GetFiles(Path.Combine(oldDirectory, "preserved"));
            Require(retained.Length == 1 && File.ReadAllBytes(retained[0]).SequenceEqual(original), "반복 저장 후에도 이전 원본 영구 보존");
            Require(new GameSession(oldDirectory).Handle(new()).View.Month == 1, "새 저장 자체의 실제 호스트 재실행");
            fresh.Handle(new("command", new("preset-age", 0, "law", Value: 600, Data: "founder.age")));
            fresh.Handle(new("command", new("preset-money", 0, "law", Value: 300, Data: "founder.initialMoney")));
            var custom = fresh.Handle(new("new", Seed: 99, UseCurrentLaws: true));
            Require(custom.View.People.Count == 2 && custom.View.People.All(p => p.Age == 50 && p.Money == 300),
                "현재 법칙을 새 창세의 실제 초기 나이·자산에 적용");
            var defaults = fresh.Handle(new("new", Seed: 99));
            Require(defaults.View.People.Count == 2 && defaults.View.People.All(p => p.Age == 100 && p.Money == 1000),
                "명시적 기본 법칙 창세는 이전 프리셋과 분리");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
