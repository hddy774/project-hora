using System.Text.Json;
using Chronicles.Game.Inventory;

namespace Chronicles.Game.Tests.Features;

internal static class EcologyRecoveryFixture
{
    internal static GameWorld Create(int food, int place = 0, long? localFood = null)
    {
        var world = InventoryWorldFixture.Create(42, new Dictionary<string, long>
        {
            ["founder.initialFood"] = food, ["economy.gatherYield"] = 1,
            ["inventory.extractionWork"] = 100, ["economy.craftDuration"] = 1,
            ["ai.beliefBudget"] = 128, ["ai.planBudget"] = 64
        });
        var state = world.Checkpoint();
        state.People[1].Action = ActionKind.Guard; state.People[1].Due = 1000;
        // An explicit current-location/low-stock boundary, without invented history, extra stock or learned knowledge.
        var person = state.People[0]; person.Place = place; person.KnownPlaces = [place]; person.ObservedPlaces.Clear();
        if (localFood is { } quantity)
        {
            state.Inventory.Sources.Single(s => s.Place == place && s.Definition == "food").Remaining = quantity;
            state.Places[place].Source = checked((int)state.Inventory.Sources.Where(s => s.Place == place).Sum(s => s.Remaining));
        }
        return GameWorld.Restore(state);
    }
    internal static Person Actor(GameWorld world) => world.Checkpoint().People[0];
    internal static long Food(GameWorld world, long actor) => world.Checkpoint().Items
        .Where(i => i.Owner == actor && i.Definition == "food" && i.Reserved == 0).Sum(i => (long)i.Quantity);
    internal static void Accept(GameWorld world, string id, string kind, long actor = 0, long target = 0, long value = 0, string data = "") =>
        InventoryWorldFixture.Accept(world, new(id, actor, kind, target, value, data));
    internal static void AdvanceAction(GameWorld world, long actor) =>
        world.AdvanceTo(world.Checkpoint().People.Single(p => p.Id == actor).Due);
    internal static GameWorld Save(GameWorld world)
    {
        var restored = GameCodec.Decode(GameCodec.Encode(world));
        Check(restored.StateHash() == world.StateHash(), "저장 왕복이 생계 권위를 바꿨습니다."); return restored;
    }
    internal static void AdvanceTogether(GameWorld world, GameWorld restored, long month)
    {
        while (world.Month < month)
        {
            world.AdvanceTo(world.Month + 1); restored.AdvanceTo(world.Month);
            Check(world.StateHash() == restored.StateHash() && world.Checkpoint().RandomState == restored.Checkpoint().RandomState,
                "회복의 실제 월 경계 상태·RNG가 저장 재개와 갈라졌습니다.");
        }
    }
    internal static void PureView(GameWorld world, long actor)
    {
        var before = world.StateHash(); var random = world.Checkpoint().RandomState;
        _ = world.View(actor); _ = world.View(actor);
        Check(world.StateHash() == before && world.Checkpoint().RandomState == random, "생계 조회가 상태나 RNG를 바꿨습니다.");
    }
    internal static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}

public sealed class EcologyReservationRecoveryScenario : IFeatureScenario
{
    public string Name => "ecology recovery: 실제 전량 예약 실패 뒤 유료 식량 재관측·알려진 이동·저장·제어 경계";
    public void Run()
    {
        var world = EcologyRecoveryFixture.Create(3, localFood: 3); var first = EcologyRecoveryFixture.Actor(world).Id;
        var opening = world.Checkpoint(); var second = opening.People[1].Id;
        var source = opening.Inventory.Sources.Single(s => s.Place == 0 && s.Definition == "food");
        EcologyRecoveryFixture.Accept(world, "recovery-control", "possess", first);
        EcologyRecoveryFixture.Accept(world, "recovery-first-observation", "exploreSource", first, data: "food");
        EcologyRecoveryFixture.AdvanceAction(world, first);
        EcologyRecoveryFixture.Accept(world, "recovery-observe", "observe");
        EcologyRecoveryFixture.Accept(world, "recovery-gather-yield", "law", value: 3, data: "economy.gatherYield");
        EcologyRecoveryFixture.Accept(world, "recovery-second-ready", "cancel", second);
        EcologyRecoveryFixture.Accept(world, "recovery-competing-job", "gather", second, source.Id, data: "food");
        var competitor = world.Checkpoint().Inventory.Jobs.Single(j => j.Actor == second && j.Source == source.Id);
        EcologyRecoveryFixture.Check(competitor.OutputQuantity == 3 && competitor.Due == 4, "현지 식량 전량을 실제 3개월 작업이 예약하지 않았습니다.");
        EcologyRecoveryFixture.Accept(world, "recovery-life-consumption", "law", value: 1, data: "ecology.foodUse");
        var controlled = EcologyRecoveryFixture.Save(world);
        EcologyRecoveryFixture.Accept(controlled, "recovery-player-control", "possess", first);
        var restored = EcologyRecoveryFixture.Save(world); EcologyRecoveryFixture.AdvanceTogether(world, restored, 2);
        var state = world.Checkpoint(); var actor = state.People.Single(p => p.Id == first);
        var failure = actor.Beliefs.GetValueOrDefault("ecology:failed:0:food:gather");
        var survey = state.Inventory.Jobs.LastOrDefault(j => j.Actor == first && j.Recipe == "exploration");
        EcologyRecoveryFixture.Check(failure is not null && failure.Value == "Finite source has insufficient unreserved stock." &&
            failure.Source.Contains("actual-rejection:gather", StringComparison.Ordinal) && failure.Expires <= world.Month + 3 &&
            !actor.Beliefs.ContainsKey("depleted:0"), "실제 예약 거부를 짧은 증거 대신 영구 고갈로 추정했습니다.");
        EcologyRecoveryFixture.Check(survey is { Status: "processing", Due: 3 } && survey.Target == source.Id &&
            survey.Consumed.Any(c => c.Definition == "food" && c.Quantity == 1) && EcologyRecoveryFixture.Food(world, first) == 0 &&
            state.Inventory.Sources.Single(s => s.Id == source.Id).Remaining == 3 &&
            state.Inventory.Jobs.Single(j => j.Id == competitor.Id).Status == "processing",
            "회복이 다른 재료의 합계를 식량으로 쓰거나 관측 비용·노동·경합 중 원천을 무시했습니다.");
        controlled.AdvanceTo(2);
        EcologyRecoveryFixture.Check(controlled.Checkpoint().People.Single(p => p.Id == first).Action == ActionKind.Idle &&
            controlled.Checkpoint().Inventory.Jobs.Count(j => j.Actor == first) == 1 && EcologyRecoveryFixture.Food(controlled, first) == 1,
            "직접 제어 중인 인물에게 AI 회복이나 무료 비용이 적용됐습니다.");
        EcologyRecoveryFixture.PureView(world, first); EcologyRecoveryFixture.AdvanceTogether(world, restored, 3);
        state = world.Checkpoint(); actor = state.People.Single(p => p.Id == first);
        var observation = InventoryKnowledgeQueries.KnownSources(state.Inventory, first).Single(o => o.Source == source.Id);
        EcologyRecoveryFixture.Check(observation.Month == 3 && observation.Remaining == 3 &&
            state.Inventory.Jobs.Single(j => j.Id == competitor.Id).Status == "processing" &&
            actor.Action == ActionKind.Travel && actor.KnownPlaces.Contains(checked((int)actor.ActionTarget)) &&
            state.Places[actor.Place].Neighbors.Contains(checked((int)actor.ActionTarget)),
            "남아 있지만 예약된 식량을 다시 무한 채집하거나 알려지지 않은 곳으로 이동했습니다.");
        EcologyRecoveryFixture.Check(state.Materials["food"].Extraction == 0 && state.Materials["food"].Production == 0,
            "실패 회복만으로 식량을 지급했습니다."); Invariants.Validate(state);
    }
}

public sealed class EcologyTinyBeliefRecoveryScenario : IFeatureScenario
{
    public string Name => "ecology recovery: 믿음 예산1의 실제 채집 예약 거부와 즉시 퇴출은 무예외·권위·저장 결정론 유지";
    public void Run()
    {
        var world = EcologyRecoveryFixture.Create(0, localFood: 3); var opening = world.Checkpoint();
        var actor = opening.People[0].Id; var other = opening.People[1].Id;
        var source = opening.Inventory.Sources.Single(s => s.Place == 0 && s.Definition == "food");
        EcologyRecoveryFixture.Accept(world, "tiny-food-yield", "law", value: 3, data: "economy.gatherYield");
        EcologyRecoveryFixture.Accept(world, "tiny-food-other-ready", "cancel", other);
        EcologyRecoveryFixture.Accept(world, "tiny-food-actual-reservation", "gather", other, source.Id, data: "food");
        EcologyRecoveryFixture.Accept(world, "tiny-food-life", "law", value: 1, data: "ecology.foodUse");
        EcologyRecoveryFixture.Accept(world, "tiny-food-cognition", "law", value: 1, data: "ai.beliefBudget");
        var before = world.StateHash(); var random = world.Checkpoint().RandomState;
        var denied = world.Execute(new("tiny-food-public-rejection", actor, "gather", source.Id, Data: "food"));
        EcologyRecoveryFixture.Check(!denied.Accepted && denied.Message == "Finite source has insufficient unreserved stock." &&
            world.StateHash() == before && world.Checkpoint().RandomState == random,
            "법적 예산1 경계의 실제 채집 거부가 예약·시간·RNG 권위를 바꿨습니다.");
        var restored = EcologyRecoveryFixture.Save(world); EcologyRecoveryFixture.AdvanceTogether(world, restored, 1);
        var state = world.Checkpoint(); var person = state.People.Single(p => p.Id == actor);
        var cognitiveLoad = person.Beliefs.Count + person.ObservedPeople.Count + person.ObservedPlaces.Count +
            state.Inventory.Observations.Count(o => o.Observer == actor) + world.PeopleCognitiveLoad(person);
        EcologyRecoveryFixture.Check(cognitiveLoad == 1 && state.Laws["ai.beliefBudget"] == 1 &&
            person.Beliefs.ContainsKey("ecology:lastFailure") && !person.Beliefs.ContainsKey("ecology:failed:0:food:gather") &&
            person.Action == ActionKind.Explore && person.ActionData != "inventory:exploration" &&
            state.Inventory.Jobs.Single(j => j.Actor == other).Status == "processing" &&
            !state.Inventory.Jobs.Any(j => j.Actor == actor) && state.Inventory.Sources.Single(s => s.Id == source.Id).Remaining == 3 &&
            EcologyRecoveryFixture.Food(world, actor) == 0,
            "즉시 퇴출된 실패를 강제로 되살리거나 예산·실물 비용·타인의 실제 예약을 우회했습니다.");
        EcologyRecoveryFixture.PureView(world, actor); Invariants.Validate(state);
    }
}

public sealed class EcologyFoodInformationRecoveryScenario : IFeatureScenario
{
    public string Name => "ecology recovery: 식량 관측 전 숨은 잔량은 선택에 영향 없고 비용 없는 조사·총자원 추정 금지";
    public void Run()
    {
        var a = EcologyRecoveryFixture.Create(2, localFood: 3); var initial = a.Checkpoint(); var actor = initial.People[0].Id;
        var offset = initial.Inventory.Sources.First(s => s.Place == 0 && s.Definition != "food" && s.Remaining >= 3);
        offset.Remaining -= 3; initial.Places[0].Source -= 3; a = GameWorld.Restore(initial);
        var hidden = a.Checkpoint();
        hidden.Inventory.Sources.Single(s => s.Place == 0 && s.Definition == "food").Remaining = 0;
        var otherSource = hidden.Inventory.Sources.Single(s => s.Id == offset.Id);
        otherSource.Remaining += 3;
        var b = GameWorld.Restore(hidden);
        foreach (var world in new[] { a, b })
        {
            EcologyRecoveryFixture.Accept(world, "food-information-consumption", "law", value: 1, data: "ecology.foodUse");
            EcologyRecoveryFixture.Accept(world, "food-information-control", "possess", actor);
            EcologyRecoveryFixture.Check(world.View(actor).ResourceSources.Count == 0, "식량 정보가 유료 관측 전에 노출됐습니다.");
            EcologyRecoveryFixture.PureView(world, actor);
        }
        EcologyRecoveryFixture.Check(JsonSerializer.Serialize(a.View(actor)) == JsonSerializer.Serialize(b.View(actor)),
            "같은 총량·개인 정보인데 숨은 식량 구성으로 플레이 조회가 달라졌습니다.");
        foreach (var world in new[] { a, b })
        {
            EcologyRecoveryFixture.Accept(world, "food-information-observe", "observe"); world.AdvanceTo(1);
        }
        var beforeA = a.Checkpoint(); var beforeB = b.Checkpoint();
        var first = beforeA.People[0]; var second = beforeB.People[0];
        EcologyRecoveryFixture.Check(first.Action == ActionKind.Explore && first.ActionData == "inventory:exploration" &&
            first.ActionTarget == second.ActionTarget && first.Due == second.Due && first.Reason == second.Reason &&
            EcologyRecoveryFixture.Food(a, actor) == 0 && EcologyRecoveryFixture.Food(b, actor) == 0 &&
            beforeA.Inventory.Observations.All(o => o.Observer != actor) && beforeB.Inventory.Observations.All(o => o.Observer != actor) &&
            beforeA.RandomState == beforeB.RandomState, "AI가 실제 관측 전에 식량 잔량을 읽거나 관측을 무료 완료했습니다.");
        var resumed = EcologyRecoveryFixture.Save(a); EcologyRecoveryFixture.AdvanceTogether(a, resumed, 2); b.AdvanceTo(2);
        var paidA = InventoryKnowledgeQueries.KnownSources(a.Checkpoint().Inventory, actor).Single(o => o.Definition == "food" && o.Place == 0);
        var paidB = InventoryKnowledgeQueries.KnownSources(b.Checkpoint().Inventory, actor).Single(o => o.Definition == "food" && o.Place == 0);
        EcologyRecoveryFixture.Check(paidA.Remaining == 3 && paidB.Remaining == 0 && paidA.Month == 2 && paidB.Month == 2,
            "시간·실물을 지불한 개인 관측이 실제 식량의 차이를 확인하지 못했습니다.");
        var empty = EcologyRecoveryFixture.Create(0, localFood: 0); actor = EcologyRecoveryFixture.Actor(empty).Id;
        EcologyRecoveryFixture.Accept(empty, "food-information-empty-law", "law", value: 1, data: "ecology.foodUse"); empty.AdvanceTo(1);
        var state = empty.Checkpoint();
        EcologyRecoveryFixture.Check(!state.Inventory.Jobs.Any(j => j.Actor == actor && j.Recipe == "exploration") &&
            state.Inventory.Observations.All(o => o.Observer != actor) && state.Materials.GetValueOrDefault("food")?.Extraction is null or 0 &&
            state.Materials.GetValueOrDefault("food")?.Production is null or 0 && EcologyRecoveryFixture.Actor(empty).Action == ActionKind.Explore &&
            EcologyRecoveryFixture.Actor(empty).ActionData != "inventory:exploration", "관측 실물 비용을 면제하거나 막힌 생계를 무한 즉시 재시도했습니다.");
        Invariants.Validate(state);

        var stranger = EcologyRecoveryFixture.Create(0); var boundary = stranger.Checkpoint();
        boundary.People[0].Action = ActionKind.Guard; boundary.People[0].Due = 1000;
        boundary.Inventory.Sources.Single(s => s.Place == 4 && s.Definition == "food").Remaining = 0;
        boundary.Places[4].Source = checked((int)boundary.Inventory.Sources.Where(s => s.Place == 4).Sum(s => s.Remaining));
        stranger = GameWorld.Restore(boundary);
        EcologyRecoveryFixture.Accept(stranger, "food-information-stranger", "create", target: 4, data: "아직 농사를 배우지 않은 실제 편입자");
        actor = stranger.Checkpoint().People.Last().Id;
        EcologyRecoveryFixture.Accept(stranger, "food-information-stranger-law", "law", value: 1, data: "ecology.foodUse"); stranger.AdvanceTo(1);
        var uninformed = stranger.Checkpoint().People.Single(p => p.Id == actor);
        EcologyRecoveryFixture.Check(uninformed.KnownPeople.Count == 0 && uninformed.Understanding.GetValueOrDefault("recipe:farm.food") == 0 &&
            uninformed.Action == ActionKind.Explore && uninformed.ActionData != "inventory:exploration" &&
            !stranger.Checkpoint().Inventory.Jobs.Any(j => j.Actor == actor && j.Recipe == "farm.food"),
            "관측 비용도 농사 출처도 없는 편입자에게 무료 기술·출력을 주거나 실제 이동로 조사를 막았습니다.");
        Invariants.Validate(stranger.Checkpoint());
    }
}

public sealed class EcologyKnownFarmRecoveryScenario : IFeatureScenario
{
    public string Name => "ecology recovery: 실제 배운 농사·관측한 물과 종자·유료 노동으로 식량 생산하고 저장된 비축법칙 적용";
    public void Run()
    {
        var world = EcologyRecoveryFixture.Create(10, place: 4, localFood: 0); var actor = EcologyRecoveryFixture.Actor(world).Id;
        EcologyRecoveryFixture.Accept(world, "food-production-control", "possess", actor);
        foreach (var definition in new[] { "food", "resource.06.0", "resource.12.0" })
        {
            EcologyRecoveryFixture.Accept(world, "food-production-observe-" + definition, "exploreSource", actor, data: definition);
            EcologyRecoveryFixture.AdvanceAction(world, actor);
        }
        EcologyRecoveryFixture.Accept(world, "food-production-water", "gather", actor, data: "resource.06.0");
        EcologyRecoveryFixture.AdvanceAction(world, actor);
        var before = world.Checkpoint();
        EcologyRecoveryFixture.Check(before.Month == 4 && before.People[0].Understanding.GetValueOrDefault("recipe:farm.food") > 0 &&
            before.Items.Any(i => i.Owner == actor && i.Definition == "resource.06.0" && i.Quantity == 1) &&
            !before.Items.Any(i => i.Owner == actor && i.Definition == "resource.12.0"), "생산 준비가 실제 기존 지식·물 추출·미확보 종자 경계가 아닙니다.");
        EcologyRecoveryFixture.Accept(world, "food-production-observe", "observe");
        EcologyRecoveryFixture.Accept(world, "food-production-consumption", "law", value: 1, data: "ecology.foodUse");
        EcologyRecoveryFixture.Accept(world, "food-production-reserve", "law", value: 12, data: "economy.foodReserveMonths");
        EcologyRecoveryFixture.Check(world.EcologyFoodTarget(EcologyRecoveryFixture.Actor(world)) == 12, "AI 식량 목표가 저장된 비축 법칙과 다릅니다.");
        var resumed = EcologyRecoveryFixture.Save(world); EcologyRecoveryFixture.AdvanceTogether(world, resumed, 5);
        var refresh = world.Checkpoint().Inventory.Jobs.Last(j => j.Actor == actor && j.Recipe == "exploration");
        EcologyRecoveryFixture.Check(refresh.Status == "processing" && refresh.Due == 6 &&
            world.Checkpoint().Inventory.Sources.Single(s => s.Id == refresh.Target).Definition == "food",
            "낡은 고갈 관측을 영구 사실로 두거나 유료 식량 재확인을 생략했습니다.");
        EcologyRecoveryFixture.AdvanceTogether(world, resumed, 6);
        var seed = world.Checkpoint().Inventory.Jobs.SingleOrDefault(j => j.Actor == actor && j.OutputDefinition == "resource.12.0");
        EcologyRecoveryFixture.Check(seed is { Status: "processing", Due: 7 } && seed.OutputQuantity == 1,
            "생계 회복이 이미 개인 관측한 필요한 종자를 실제 채집하지 않았습니다.");
        EcologyRecoveryFixture.AdvanceTogether(world, resumed, 7);
        var processing = world.Checkpoint(); var farm = processing.Inventory.Jobs.SingleOrDefault(j => j.Actor == actor && j.Recipe == "farm.food");
        EcologyRecoveryFixture.Check(farm is { Status: "processing", Due: 8 } && farm.Consumed.Any(c => c.Definition == "resource.06.0" && c.Quantity == 1) &&
            farm.Consumed.Any(c => c.Definition == "resource.12.0" && c.Quantity == 1) &&
            processing.Materials["food"].Production == 0, "농사 시작이 종자·물·실제 노동을 소비하지 않거나 출력을 미리 지급했습니다.");
        EcologyRecoveryFixture.AdvanceTogether(world, resumed, 8); var completed = world.Checkpoint();
        EcologyRecoveryFixture.Check(completed.Inventory.Jobs.Single(j => j.Id == farm!.Id).Status == "completed" &&
            completed.Materials["food"].Production == 12 && completed.Ecology.Production.Any(p => p.Person == actor && p.Definition == "food" && p.Quantity == 12) &&
            completed.Knowledge.Activities.Any(a => a.ActorId == actor && a.WorkId == "recipe:farm.food" && a.Status == Knowledge.KnowledgeActivityStatus.Completed),
            "배운 공정의 실제 완료·식량 12개·지식 실습 영수증을 확인하지 못했습니다.");
        EcologyRecoveryFixture.PureView(world, actor); Invariants.Validate(completed);
    }
}
