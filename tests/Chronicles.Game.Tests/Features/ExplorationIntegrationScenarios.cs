using System.Text.Json;
using Chronicles.Game.Exploration;

namespace Chronicles.Game.Tests;

internal sealed class ExplorationFixture
{
    private int command;
    public GameWorld World { get; set; }
    public long First { get; }
    public long Second { get; }
    public ExplorationFixture(Dictionary<string, long>? changes = null, ulong seed = 42)
    {
        var laws = new Dictionary<string, long>
        {
            ["birth.enabled"] = 0, ["ecology.foodUse"] = 0, ["ecology.disasterChance"] = 0,
            ["hazard.sealChance"] = 0, ["exploration.openMonths"] = 12, ["exploration.openPeriod"] = 12,
            ["exploration.risk"] = 0, ["exploration.entryMonths"] = 3
        };
        if (changes is not null) foreach (var pair in changes) laws[pair.Key] = pair.Value;
        World = GameWorld.Create(seed, laws); var state = World.Checkpoint(); First = state.People[0].Id; Second = state.People[1].Id;
        foreach (var p in state.People) { p.Place = 8; p.KnownPlaces = [8]; p.Action = ActionKind.Guard; p.Due = 10000; p.ActionData = ""; p.ActionTarget = 0; }
        World = GameWorld.Restore(state);
    }
    public static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    public CommandResult Send(string kind, long actor = 0, long target = 0, long value = 0, string data = "")
    {
        var result = World.Execute(new("secret-functional:" + ++command, actor, kind, target, value, data));
        Check(result.Accepted, kind + ": " + result.Message); return result;
    }
    public void Reject(string kind, long actor, long target = 0)
    { var hash = World.StateHash(); var result = World.Execute(new("secret-functional:" + ++command, actor, kind, target)); Check(!result.Accepted && World.StateHash() == hash, "rejected " + kind + " leaked costs RNG IDs or world mutation"); }
    public void Idle(long actor)
    {
        var state = World.Checkpoint(); var p = state.People.Single(p => p.Id == actor);
        p.Action = ActionKind.Idle; p.Due = 0; p.ActionData = ""; p.ActionTarget = 0;
        foreach (var row in state.Inventory.Labor.Where(r => r.Person == actor && r.Purpose == "world-action")) row.Active = false;
        World = GameWorld.Restore(state);
    }
    public void Survey(long actor)
    {
        Idle(actor); Send("possess", actor); Send("explore", actor); World.AdvanceTo(World.Checkpoint().People.Single(p => p.Id == actor).Due);
    }
    public void Freeze(long actor)
    {
        var s = World.Checkpoint(); var p = s.People.Single(p => p.Id == actor); p.Action = ActionKind.Guard; p.Due = 10000; p.ActionData = ""; p.ActionTarget = 0;
        World = GameWorld.Restore(s);
    }
    public void BothSurvey()
    {
        Survey(First); Freeze(First); Survey(Second); Idle(First); Idle(Second); Send("observe");
    }
    public SecretEntry Last(long actor) => World.Checkpoint().Exploration.Entries.Last(e => e.Person == actor);
    public int Food(long actor) => World.Checkpoint().Items.Where(i => i.Owner == actor && i.Definition == "food").Sum(i => i.Quantity);
}

public sealed class ExplorationDiscoveryScenario : IFeatureScenario
{
    public string Name => "비경 공개 명령: 실제 현지 탐색만 입구를 발견하고 일반 자연 cache·100지점과 권위가 분리된다";
    public void Run()
    {
        var f = new ExplorationFixture();
        ExplorationFixture.Check(f.World.Checkpoint().Places.Count == 100 && f.World.ExplorationDetail().Entrances.Count == 10,
            "비경 입구가 전략 지점이나 주민을 추가했다.");
        f.Idle(f.First); f.Send("possess", f.First); ExplorationFixture.Check(f.World.ExplorationDetail().Entrances.Count == 0, "방문만으로 숨은 입구를 공개했다.");
        f.Reject("enterSecret", f.First, 8); f.Survey(f.First); var personal = f.World.ExplorationDetail();
        ExplorationFixture.Check(personal.Entrances is [{ Place: 8, Observed: 1, OpenNow: null, ActualParticipants: null, ObservedStock: null }] &&
            f.World.Checkpoint().Places[8].DiscoveryRemaining == 0 && f.World.Checkpoint().Exploration.Sites.Single(s => s.Place == 8).Remaining == 3,
            "실제 탐색·개인 날짜·내부 미상 또는 일반 자연 cache와 별도 비경 원천이 잘못되었다.");
        var hash = f.World.StateHash(); f.World.ExplorationDetail(); f.World.View(f.First);
        ExplorationFixture.Check(f.World.StateHash() == hash, "입구 조회가 세계/RNG를 바꾸었다.");
        f.Send("possess", f.Second); ExplorationFixture.Check(f.World.ExplorationDetail().Entrances.Count == 0, "다른 인물에게 발견을 무료 전역 공유했다.");
    }
}

public sealed class ExplorationPaidProgressScenario : IFeatureScenario
{
    public string Name => "비경 공개 명령: 실제 도구·식량과 채택 법칙·한 번의 위험을 저장하고 월별 진척으로 유한 보상을 선점한다";
    public void Run()
    {
        var f = new ExplorationFixture(); f.Survey(f.First); var food = f.Food(f.First);
        var extraction = f.World.Checkpoint().Materials["herb"].Extraction;
        var command = new GameCommand("single-paid-entry", f.First, "enterSecret", 8); ExplorationFixture.Check(f.World.Execute(command).Accepted, "실제 입장 거절");
        var entry = f.Last(f.First); var hash = f.World.StateHash();
        ExplorationFixture.Check(f.Food(f.First) == food - 2 && entry.Progress == 0 && entry.RewardQuantity == 0 &&
            f.World.Checkpoint().Items.Single(i => i.Id == entry.Tool).Reserved == entry.ToolReservation,
            "준비 식량/실제 도구/입장 시간 없이 보상을 받았다.");
        ExplorationFixture.Check(f.World.Execute(command).Accepted && f.World.StateHash() == hash, "중복 입장이 소비·RNG·자리를 다시 예약했다.");
        f.Reject("craft", f.First); f.Send("observe");
        f.Send("law", value: 10, data: "exploration.entryMonths"); f.Send("law", value: 10000, data: "exploration.risk");
        f.Send("law", value: 100, data: "exploration.toolWear"); f.Send("possess", f.First);
        var loaded = GameCodec.Decode(GameCodec.Encode(f.World)); var began = f.World.Month;
        f.World.AdvanceTo(began + 1); loaded.AdvanceTo(began + 1);
        ExplorationFixture.Check(f.Last(f.First).Progress == 1 && f.Last(f.First).Roll == entry.Roll && f.Last(f.First).Status == SecretEntryStatus.Active,
            "월별 실제 진척이나 고정 위험을 소급 변경했다.");
        f.World.AdvanceTo(began + 3); loaded.AdvanceTo(began + 3); var finished = f.Last(f.First);
        ExplorationFixture.Check(f.World.StateHash() == loaded.StateHash() && finished.Status == SecretEntryStatus.Completed && finished.RewardQuantity == 3 &&
            finished.Rules.Months == 3 && finished.Rules.Risk == 0 && f.World.Checkpoint().Materials["herb"].Extraction == extraction + 3 &&
            f.World.Checkpoint().Exploration.Sites.Single(s => s.Place == 8).Remaining == 0 &&
            f.World.Checkpoint().Items.Single(i => i.Id == finished.Tool).Reserved == 0,
            "실제 원천·채택 법칙·도구 해제·생산 저장의 단일 선점이 잘못되었다.");
        Invariants.Validate(f.World.Checkpoint());
    }
}

public sealed class ExplorationCapacityReturnScenario : IFeatureScenario
{
    public string Name => "비경 공개 명령: 실제 입장 자리를 경쟁하고 중도 귀환도 한 달·도구·이미 쓴 식량을 보존한다";
    public void Run()
    {
        var f = new ExplorationFixture(); f.BothSurvey(); var food = f.Food(f.First);
        f.Send("enterSecret", f.First, 8); f.Reject("enterSecret", f.Second, 8); var began = f.World.Month;
        f.Send("cancel", f.First); var returning = f.Last(f.First);
        ExplorationFixture.Check(returning.Status == SecretEntryStatus.Returning && returning.Due == began + 1 && f.Food(f.First) == food - 2,
            "취소가 실제 귀환 시간이나 준비 소비를 환급했다.");
        f.Reject("enterSecret", f.Second, 8); f.Freeze(f.Second); f.Send("possess", f.First); var copy = GameCodec.Decode(GameCodec.Encode(f.World));
        f.World.AdvanceTo(began + 1); copy.AdvanceTo(began + 1);
        ExplorationFixture.Check(f.World.StateHash() == copy.StateHash() && f.Last(f.First).Status == SecretEntryStatus.Returned && f.Last(f.First).RewardQuantity == 0 &&
            !f.World.Checkpoint().Inventory.Reservations.Single(r => r.Id == returning.ToolReservation).Active,
            "실제 귀환·예약 해제·중도 저장이 일치하지 않는다.");
        f.Idle(f.Second); f.Send("observe"); f.Send("enterSecret", f.Second, 8);
        ExplorationFixture.Check(f.Last(f.Second).Status == SecretEntryStatus.Active, "귀환한 실제 자리의 재사용이 막혔다.");
    }
}

public sealed class ExplorationFirstClaimScenario : IFeatureScenario
{
    public string Name => "비경 공개 명령: 동시 실제 입장과 같은 시점 완료는 유한 원천을 한 번만 선점하고 다시 입장해도 복제하지 않는다";
    public void Run()
    {
        var f = new ExplorationFixture(new() { ["exploration.capacity"] = 2 }); f.BothSurvey();
        f.Send("enterSecret", f.First, 8); f.Send("enterSecret", f.Second, 8); f.Send("possess", f.First); var end = f.World.Month + 3;
        var loaded = GameCodec.Decode(GameCodec.Encode(f.World)); f.World.AdvanceTo(end);
        for (var month = loaded.Month + 1; month <= end; month++) loaded.AdvanceTo(month);
        var state = f.World.Checkpoint(); var entries = state.Exploration.Entries;
        ExplorationFixture.Check(f.World.StateHash() == loaded.StateHash() && entries.Count(e => e.RewardQuantity == 3) == 1 &&
            entries.Sum(e => e.RewardQuantity) == 3 && entries.All(e => e.Status == SecretEntryStatus.Completed) &&
            state.Exploration.Sites.Single(s => s.Place == 8).Remaining == 0 && state.People.Count == 2,
            "동시 완료·분할 시간·저장이 사람이나 유한 보상을 복제했다.");
        f.Idle(f.First); f.Send("possess", f.First); f.Send("enterSecret", f.First, 8); f.World.AdvanceTo(f.World.Month + 3);
        ExplorationFixture.Check(f.Last(f.First).RewardQuantity == 0 && f.World.Checkpoint().Exploration.Entries.Sum(e => e.RewardQuantity) == 3,
            "실패/재입장이 이미 선점한 보상을 다시 발행했다.");
    }
}

public sealed class ExplorationRiskScenario : IFeatureScenario
{
    public string Name => "비경 공개 명령: 채택한 실제 위험 실패·부상은 저장과 이후 법칙 변경으로 재추첨되지 않는다";
    public void Run()
    {
        var exercised = false;
        for (ulong seed = 1; seed <= 8 && !exercised; seed++)
        {
            var f = new ExplorationFixture(new() { ["exploration.risk"] = 10000, ["exploration.injury"] = 15 }, seed); f.Survey(f.First);
            f.Send("enterSecret", f.First, 8); var before = f.Last(f.First);
            if (before.Roll >= before.Risk) continue;
            var health = f.World.Checkpoint().People.Single(p => p.Id == f.First).Health;
            f.Send("observe"); f.Send("law", value: 0, data: "exploration.risk"); f.Send("possess", f.First);
            var loaded = GameCodec.Decode(GameCodec.Encode(f.World)); f.World.AdvanceTo(f.World.Month + 1); loaded.AdvanceTo(loaded.Month + 1);
            var failed = f.Last(f.First);
            ExplorationFixture.Check(f.World.StateHash() == loaded.StateHash() && failed.Status == SecretEntryStatus.Failed && failed.Roll == before.Roll &&
                failed.Risk == before.Risk && failed.Injury == 15 && f.World.Checkpoint().People.Single(p => p.Id == f.First).Health == health - 15 &&
                f.World.Checkpoint().Exploration.Sites.Single(s => s.Place == 8).Remaining == 3 && failed.RewardQuantity == 0,
                "부상·실패·원천·채택 위험의 실제 결과를 소급 변경했다.");
            exercised = true;
        }
        ExplorationFixture.Check(exercised, "한정된 실제 위험 fixture가 실패 경계를 실행하지 못했다.");
    }
}

public sealed class ExplorationPrivacySpaceScenario : IFeatureScenario
{
    public string Name => "비경 공개 명령: 과거 개인 입구 기록은 숨은 참여·잔량을 읽지 않고 입구 밖 물리 행동을 차단한다";
    public void Run()
    {
        var f = new ExplorationFixture(); f.BothSurvey(); f.Send("possess", f.Second);
        var before = JsonSerializer.Serialize(f.World.ExplorationDetail()); f.Send("observe"); f.Send("enterSecret", f.First, 8); f.Send("possess", f.Second);
        ExplorationFixture.Check(JsonSerializer.Serialize(f.World.ExplorationDetail()) == before, "숨은 실제 입장/현재 수용 상태가 개인 과거 기록에 누출되었다.");
        f.Reject("attack", f.Second, f.First); f.Reject("requestTeaching", f.Second, f.First); f.Reject("guard", f.Second, f.First);
        f.Send("possess", f.First); var food = f.World.Checkpoint().Items.First(i => i.Owner == f.First && i.Definition == "food").Id;
        f.Reject("offer", f.First, food); f.Reject("travel", f.First, 7);
        var hash = f.World.StateHash(); f.World.ExplorationDetail(); f.World.View(f.First);
        ExplorationFixture.Check(f.World.StateHash() == hash && f.World.ExplorationDetail().OwnEntries.Count == 1, "입구/자기 진척 조회가 권위를 바꾸었다.");
    }
}

public sealed class ExplorationDeathBodyScenario : IFeatureScenario
{
    public string Name => "비경 공개 명령: 실제 사망은 입장·도구를 한 번 해제하며 새 출생의 몸에는 진척·관측을 복제하지 않는다";
    public void Run()
    {
        var f = new ExplorationFixture(new() { ["birth.enabled"] = 1, ["birth.gestation"] = 3, ["soul.crossing"] = 0 });
        f.Idle(f.Second); f.Send("conceive", f.Second, f.First); f.Freeze(f.Second); f.Survey(f.First); f.Send("enterSecret", f.First, 8);
        var entry = f.Last(f.First); var state = f.World.Checkpoint(); var soul = state.People[0].SoulId;
        state.People[0].Lifespan = state.Month + 1 - state.People[0].Born; f.World = GameWorld.Restore(state);
        var loaded = GameCodec.Decode(GameCodec.Encode(f.World)); f.World.AdvanceTo(3); loaded.AdvanceTo(3); state = f.World.Checkpoint();
        var next = state.People.Single(p => p.Id != f.First && p.SoulId == soul);
        ExplorationFixture.Check(f.World.StateHash() == loaded.StateHash() && f.Last(f.First).Status == SecretEntryStatus.Aborted &&
            !state.Inventory.Reservations.Single(r => r.Id == entry.ToolReservation).Active && state.Items.Single(i => i.Id == entry.Tool).Place == 8 &&
            next.BodyId != entry.Body && state.Exploration.Entries.All(e => e.Person != next.Id),
            "실제 사망·유산 입구 위치·새 몸의 진척 분리가 잘못되었다.");
        f.Send("possess", next.Id); ExplorationFixture.Check(f.World.ExplorationDetail().Entrances.Count == 0, "전생의 입구 관측을 새 인물에게 자동 지급했다.");
    }
}

public sealed class ExplorationPreparationGatesScenario : IFeatureScenario
{
    public string Name => "비경 공개 명령: 관측한 개방 시각과 실제 준비·계약·육신 자격을 재검사하고 거절은 원자적이다";
    public void Run()
    {
        var f = new ExplorationFixture(new() { ["exploration.openMonths"] = 1 }); f.Survey(f.First);
        f.Reject("enterSecret", f.First, 8); f.World.AdvanceTo(12);
        var item = f.World.Checkpoint().Items.First(i => i.Owner == f.First && i.Definition == "tool").Id;
        f.Send("offer", f.First, item, 1); f.Reject("enterSecret", f.First, 8);
        var contract = f.World.Checkpoint().Inventory.Contracts.Single(c => c.Item == item && c.Status == "open").Id;
        f.Send("withdraw", f.First, contract); f.Send("store", f.First, item); f.Send("enterSecret", f.First, 8);
        ExplorationFixture.Check(f.Last(f.First).Status == SecretEntryStatus.Active, "실제 개방·출품 종료 후 준비한 입장이 거절되었다.");
        var corrupt = f.World.Checkpoint(); corrupt.Exploration.Entries[0].Body++;
        var rejected = false; try { GameWorld.Restore(corrupt); } catch (InvalidDataException) { rejected = true; }
        ExplorationFixture.Check(rejected, "다른 몸의 위조된 입장 저장을 복원했다.");
        var rooted = new ExplorationFixture(new() { ["exploration.minimumRealm"] = 0 });
        rooted.Send("create", target: 8, data: "{\"name\":\"뿌리내린 조사자\",\"species\":\"spirit-pine\"}");
        var pine = rooted.World.Checkpoint().People.Single(p => p.Species == "spirit-pine").Id;
        rooted.Survey(pine); var hash = rooted.World.StateHash();
        var unavailable = rooted.World.Execute(new("actual-rooted-entry", pine, "enterSecret", 8));
        ExplorationFixture.Check(!unavailable.Accepted && unavailable.Message.Contains("육신", StringComparison.Ordinal) && rooted.World.StateHash() == hash,
            "실제 고정된 육신의 이동/도구 적합을 무시했거나 거절이 물품을 지급했다.");
    }
}
