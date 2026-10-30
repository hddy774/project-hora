using System.Text.Json;
using Chronicles.Game.Currency;
using Chronicles.Game.Inventory;

namespace Chronicles.Game.Tests.Features;

internal static class CurrencyPlanningFixture
{
    internal static (GameWorld World, long Actor, long Source) Create(long rate = 1, int budget = 64)
    {
        var world = InventoryWorldFixture.Create(101, new Dictionary<string, long> { ["currency.stoneValue"] = rate, ["ai.planBudget"] = budget });
        var state = world.Checkpoint(); var actor = state.People[0]; var other = state.People[1];
        other.Money += actor.Money; actor.Money = 0;
        actor.Place = 1; actor.KnownPlaces.Add(1); actor.KnownPlaces.Sort(); actor.ObservedPeople.Clear();
        actor.Personality = new int[14]; actor.Personality[4] = 100; actor.Personality[5] = 100;
        other.Place = 99; other.Action = ActionKind.Guard; other.Due = 1000;
        var source = state.Inventory.Sources.Single(s => s.Place == 1 && s.Definition == CurrencyPolicy.StoneDefinition).Id;
        return (GameWorld.Restore(state), actor.Id, source);
    }

    internal static void Discover(GameWorld world, long actor, long source)
    {
        InventoryWorldFixture.Accept(world, new("currency-plan-control", actor, "possess"));
        InventoryWorldFixture.Accept(world, new("currency-plan-discover", actor, "exploreSource", source));
        world.AdvanceTo(world.Checkpoint().People.Single(p => p.Id == actor).Due);
        InventoryFixture.Check(world.Checkpoint().Inventory.Observations.Any(o => o.Observer == actor && o.Source == source &&
            o.Definition == CurrencyPolicy.StoneDefinition && o.Evidence.Contains("job:", StringComparison.Ordinal)),
            "AI에 인계하기 전 실제 현지 탐사로 자기 영석 관측을 만들지 못했습니다.");
    }

    internal static void CheckRedemption(GameState state, long actor, long source, long openingMoney)
    {
        var receipt = state.Currency.Receipts.Single(r => r.Kind == CurrencyOriginKind.PhysicalStone);
        var person = state.People.Single(p => p.Id == actor);
        InventoryFixture.Check(receipt.Person == actor && receipt.Source.StartsWith($"extraction:{source}:job:", StringComparison.Ordinal) &&
            receipt.Quantity > 0 && receipt.Amount == receipt.Quantity * receipt.Rate && person.Money == receipt.Amount &&
            state.CreatedMoney == openingMoney + receipt.Amount && CurrencyWorldFixture.Quantity(state, actor) == 0 &&
            state.Materials[CurrencyPolicy.StoneDefinition].Consumption == receipt.Quantity &&
            state.Materials[CurrencyPolicy.StoneDefinition].Extraction == receipt.Quantity &&
            state.Inventory.Events.Count(e => e.Sequence == receipt.ConsumptionSequence && e.Actor == actor &&
                e.Subject == receipt.ConsumedItem && e.Kind == "physical-consumption" && e.Quantity == receipt.Quantity) == 1 &&
            person.Action == ActionKind.Guard && person.Due == state.Month + 1 && person.PlanExpansions <= state.Laws["ai.planBudget"],
            "AI 환전이 실제 채광·단일 실물 소비·잔액 원천·유한 행동 점유를 보존하지 못했습니다.");
        Invariants.Validate(state);
    }
}

public sealed class CurrencyAiMiningHandoffScenario : IFeatureScenario
{
    public string Name => "currency planning: 공개 채광을 인계한 AI의 환전과 저장·재전송은 실물 원천을 한 번만 소비";
    public void Run()
    {
        var (world, actor, source) = CurrencyPlanningFixture.Create(); CurrencyPlanningFixture.Discover(world, actor, source);
        var initial = world.Checkpoint(); var created = initial.CreatedMoney; var remaining = initial.Inventory.Sources.Single(s => s.Id == source).Remaining;
        InventoryWorldFixture.Accept(world, new("currency-plan-public-mine", actor, "mining", source, Data: CurrencyPolicy.StoneDefinition));
        var due = world.Checkpoint().People.Single(p => p.Id == actor).Due;
        InventoryFixture.Check(world.Checkpoint().CreatedMoney == created, "채광을 시작하자 실물 완성·소비 없이 돈이 생겼습니다.");
        InventoryWorldFixture.Accept(world, new("currency-plan-handoff", 0, "observe"));
        var resumed = GameCodec.Decode(GameCodec.Encode(world)); world.AdvanceTo(due); resumed.AdvanceTo(due);
        InventoryFixture.Check(world.StateHash() == resumed.StateHash(), "AI에 인계한 실제 채광·환전의 저장 이어짐이 달라졌습니다.");
        var state = world.Checkpoint(); CurrencyPlanningFixture.CheckRedemption(state, actor, source, created);
        var receipt = state.Currency.Receipts.Single(r => r.Kind == CurrencyOriginKind.PhysicalStone);
        InventoryFixture.Check(state.Inventory.Sources.Single(s => s.Id == source).Remaining == remaining - receipt.Quantity,
            "환전된 실물과 실제 유한 원천의 감소량이 다릅니다.");
        InventoryWorldFixture.Accept(world, new("currency-plan-player-return", actor, "possess"));
        var replay = new GameCommand(receipt.CommandId, actor, "redeemCurrency", receipt.OriginalItem, receipt.Quantity);
        InventoryWorldFixture.Accept(world, replay); var hash = world.StateHash(); InventoryWorldFixture.Accept(world, replay);
        InventoryFixture.Check(world.StateHash() == hash && world.Checkpoint().CreatedMoney == state.CreatedMoney &&
            world.Checkpoint().Currency.Receipts.Count(r => r.Kind == CurrencyOriginKind.PhysicalStone) == 1,
            "AI 환전 명령 재전송이 실물이나 화폐 출처를 복제했습니다.");
        InventoryWorldFixture.Reject(world, replay with { Value = replay.Value + 1 });
        Invariants.Validate(world.Checkpoint());
    }
}

public sealed class CurrencyAiObservedMiningScenario : IFeatureScenario
{
    public string Name => "currency planning: 자기 현지 관측으로 실제 채광을 예약하고 완료한 실물만 환전";
    public void Run()
    {
        var (world, actor, source) = CurrencyPlanningFixture.Create(); CurrencyPlanningFixture.Discover(world, actor, source);
        var before = world.Checkpoint(); var remaining = before.Inventory.Sources.Single(s => s.Id == source).Remaining;
        InventoryWorldFixture.Accept(world, new("currency-plan-known-source-handoff", 0, "observe")); world.AdvanceTo(world.Month + 1);
        var state = world.Checkpoint(); var job = state.Inventory.Jobs.Single(j => j.Actor == actor && j.Source == source);
        InventoryFixture.Check(job.Status == "processing" && job.OutputDefinition == CurrencyPolicy.StoneDefinition && job.OutputQuantity > 0 &&
            state.People.Single(p => p.Id == actor).Action == ActionKind.Gather &&
            state.Inventory.Sources.Single(s => s.Id == source).Remaining == remaining &&
            state.CreatedMoney == before.CreatedMoney && state.Currency.Receipts.All(r => r.Kind == CurrencyOriginKind.Genesis),
            "자기 원천 관측에서 실제 채광 시간·예약 없이 실물이나 잔액을 만들었습니다.");
        var resumed = GameCodec.Decode(GameCodec.Encode(world)); world.AdvanceTo(job.Due); resumed.AdvanceTo(job.Due);
        InventoryFixture.Check(world.StateHash() == resumed.StateHash(), "AI 채광 준비와 실제 환전의 저장 결과가 달라졌습니다.");
        state = world.Checkpoint(); CurrencyPlanningFixture.CheckRedemption(state, actor, source, before.CreatedMoney);
        InventoryFixture.Check(state.Currency.Receipts.Last().Source == $"extraction:{source}:job:{job.Id}",
            "환전 원천이 AI가 실제로 예약·완료한 채광 작업과 다릅니다.");
    }
}

public sealed class CurrencyAiScopeAndNeedScenario : IFeatureScenario
{
    public string Name => "currency planning: 다른 인물의 돈·관측과 원격·예약·결손 영석은 목표를 만들지 않고 비활성·충분 잔액도 보존";
    public void Run()
    {
        var (world, actor, source) = CurrencyPlanningFixture.Create();
        InventoryWorldFixture.Accept(world, new("currency-plan-explicit-other", 0, "create", 99, Data: "정보 범위 밖 실제 인물"));
        var state = world.Checkpoint(); var other = state.People[1].Id; var outsider = state.People[2];
        outsider.Action = ActionKind.Guard; outsider.Due = 1000;
        InventoryWorldFixture.Add(state, actor, "resource.02.1", 1);
        InventoryWorldFixture.Add(state, actor, CurrencyPolicy.StoneDefinition, 1); state.Items.Last().Place = 2;
        InventoryWorldFixture.Add(state, actor, CurrencyPolicy.StoneDefinition, 1); state.Items.Last().Damage = 100;
        InventoryWorldFixture.Add(state, actor, CurrencyPolicy.StoneDefinition, 1); state.Items.Last().Defects = ItemDefect.Contamination;
        InventoryWorldFixture.Add(state, other, CurrencyPolicy.StoneDefinition, 1); state.Items.Last().Place = 1;
        InventoryWorldFixture.Add(state, actor, CurrencyPolicy.StoneDefinition, 1); var reserved = state.Items.Last().Id;
        world = GameWorld.Restore(state); InventoryWorldFixture.Accept(world, new("currency-plan-real-reservation", actor, "offer", reserved, 1, "auction"));
        var hidden = world.Checkpoint(); hidden.People[1].Money -= 17; hidden.People[2].Money += 17;
        hidden.Inventory.Observations.Add(new(other, source, 1, 12, hidden.Month, "검사에 명시한 다른 인물의 과거 관측", CurrencyPolicy.StoneDefinition, "source.mining"));
        var alternative = GameWorld.Restore(hidden); world.AdvanceTo(1); alternative.AdvanceTo(1);
        var left = world.Checkpoint(); var right = alternative.Checkpoint(); var p = left.People.Single(p => p.Id == actor); var q = right.People.Single(p => p.Id == actor);
        InventoryFixture.Check(JsonSerializer.Serialize(p.Decisions.Last()) == JsonSerializer.Serialize(q.Decisions.Last()) &&
            p.Action == q.Action && p.ActionData == q.ActionData && p.Due == q.Due &&
            left.Currency.Receipts.All(r => r.Kind == CurrencyOriginKind.Genesis) && right.Currency.Receipts.All(r => r.Kind == CurrencyOriginKind.Genesis) &&
            !left.Inventory.Jobs.Any(j => j.Actor == actor && j.Source == source) &&
            !right.Inventory.Jobs.Any(j => j.Actor == actor && j.Source == source),
            "AI가 타인 돈·관측·외부 실물이나 미발견 현지 원천으로 환전 목표를 선택했습니다.");
        Invariants.Validate(left); Invariants.Validate(right);

        foreach (var disabled in new[] { false, true })
        {
            var (guarded, person, _) = CurrencyPlanningFixture.Create(disabled ? 0 : 1); var snapshot = guarded.Checkpoint();
            InventoryWorldFixture.Add(snapshot, person, CurrencyPolicy.StoneDefinition, 2);
            if (!disabled) { var needed = snapshot.Laws["politics.foundCost"]; snapshot.People[0].Money += needed; snapshot.People[1].Money -= needed; }
            guarded = GameWorld.Restore(snapshot); guarded.AdvanceTo(1); var result = guarded.Checkpoint();
            InventoryFixture.Check(result.CreatedMoney == snapshot.CreatedMoney && CurrencyWorldFixture.Quantity(result, person) == 2 &&
                result.Currency.Receipts.All(r => r.Kind == CurrencyOriginKind.Genesis), "환전 비활성 또는 충분한 잔액인데 실물을 강제 전환했습니다.");
            Invariants.Validate(result);
        }

        var (limited, limitedActor, _) = CurrencyPlanningFixture.Create(budget: 1); var prepared = limited.Checkpoint();
        InventoryWorldFixture.Add(prepared, limitedActor, CurrencyPolicy.StoneDefinition, 1);
        limited = GameWorld.Restore(prepared); limited.AdvanceTo(1); var deferred = limited.Checkpoint();
        var waiting = deferred.People.Single(p => p.Id == limitedActor);
        InventoryFixture.Check(waiting.PlanExpansions == 1 && waiting.Action == ActionKind.Wait &&
            deferred.CreatedMoney == prepared.CreatedMoney && CurrencyWorldFixture.Quantity(deferred, limitedActor) == 1 &&
            deferred.Currency.Receipts.All(r => r.Kind == CurrencyOriginKind.Genesis),
            "환전 도우미가 실제 계획 탐색 예산을 넘어 실물이나 화폐를 소비·생성했습니다.");
        Invariants.Validate(deferred);
    }
}
