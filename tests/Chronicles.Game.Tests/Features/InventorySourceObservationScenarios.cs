using Chronicles.Game.Inventory;

namespace Chronicles.Game.Tests.Features;

internal static class FoodObservationFixture
{
    internal static GameWorld Create()
    {
        var state = InventoryWorldFixture.Create(131).Checkpoint();
        state.People[1].Action = ActionKind.Guard; state.People[1].Due = 1000;
        // A zero-stock, ungoverned fixture source distinguishes first-unknown selection without changing natural allocation.
        state.Inventory.Sources.Insert(0, new() { Id = state.NextId++, Place = 0, Definition = "ore", Activity = "source.mining",
            Remaining = 0, Capacity = 0, EcologyWeight = 0, Origin = "명시적 빈 유한 원천·선택 순서 검사" });
        return GameWorld.Restore(state);
    }
}

public sealed class InventoryFoodObservationScenario : IFeatureScenario
{
    public string Name => "inventory source observation: 공개 food 선택은 정확한 현지 원천을 실제 비용·한 달·저장 재개로 관측";
    public void Run()
    {
        var world = FoodObservationFixture.Create(); var opening = world.Checkpoint(); var actor = opening.People[0].Id;
        var source = opening.Inventory.Sources.Single(s => s.Place == 0 && s.Definition == "food");
        var initialFood = opening.Items.Where(i => i.Owner == actor && i.Definition == "food").Sum(i => i.Quantity);
        InventoryWorldFixture.Accept(world, new("food-observe-control", actor, "possess"));
        var request = new GameCommand("food-observe-paid", actor, "exploreSource", Data: "food"); InventoryWorldFixture.Accept(world, request);
        var state = world.Checkpoint(); var job = state.Inventory.Jobs.Single(j => j.Actor == actor && j.Status == "processing");
        InventoryFixture.Check(job.Recipe == "exploration" && job.Target == source.Id && job.Due == world.Month + 1 &&
            state.Items.Where(i => i.Owner == actor && i.Definition == "food").Sum(i => i.Quantity) == initialFood - 1 &&
            state.Materials["food"].Consumption == opening.Materials["food"].Consumption + 1 &&
            !state.Inventory.Observations.Any(o => o.Observer == actor && o.Source == source.Id), "유료 관측이 다른 원천을 택하거나 시간·입력을 생략했습니다.");
        var resumed = GameCodec.Decode(GameCodec.Encode(world)); world.AdvanceTo(job.Due); resumed.AdvanceTo(job.Due); state = world.Checkpoint();
        var observed = InventoryKnowledgeQueries.KnownSources(state.Inventory, actor).Single(o => o.Source == source.Id);
        InventoryFixture.Check(world.StateHash() == resumed.StateHash() && observed.Definition == "food" && observed.Activity == source.Activity &&
            observed.Month == job.Due && observed.Remaining == source.Remaining && state.Inventory.Sources.Single(s => s.Id == source.Id).Remaining == source.Remaining,
            "실제 완료·개인 관측·유한 원천·저장 속행이 다릅니다.");
        var before = world.StateHash(); InventoryWorldFixture.Accept(world, request); InventoryFixture.Check(world.StateHash() == before, "관측 재전송이 다시 소비했습니다.");
        // A new paid refresh may select a known source, using the same physical process rather than free information.
        InventoryWorldFixture.Accept(world, new("food-observe-refresh", actor, "exploreSource", source.Id, Data: "food"));
        state = world.Checkpoint(); job = state.Inventory.Jobs.Last(j => j.Actor == actor);
        InventoryFixture.Check(state.Materials["food"].Consumption == opening.Materials["food"].Consumption + 2 && job.Due == world.Month + 1,
            "알려진 원천의 재관측이 실제 비용이나 기간을 생략했습니다.");
        world.AdvanceTo(job.Due); Invariants.Validate(world.Checkpoint());
    }
}

public sealed class InventoryLegacyObservationScenario : IFeatureScenario
{
    public string Name => "inventory source observation: 빈 Data는 기존 첫 미관측 선택을 유지하고 명시 정의는 fallback하지 않음";
    public void Run()
    {
        var world = FoodObservationFixture.Create(); var state = world.Checkpoint(); var actor = state.People[0].Id;
        var ore = state.Inventory.Sources.First(s => s.Place == 0); var remote = state.Inventory.Sources.First(s => s.Place == 1 && s.Definition == "food");
        InventoryFixture.Check(ore.Definition == "ore", "검사 원천 순서가 실제로 다르지 않습니다.");
        InventoryWorldFixture.Accept(world, new("legacy-observe-control", actor, "possess"));
        InventoryWorldFixture.Reject(world, new("observe-target-mismatch", actor, "exploreSource", ore.Id, Data: "food"));
        InventoryWorldFixture.Reject(world, new("observe-remote", actor, "exploreSource", remote.Id, Data: "food"));
        InventoryWorldFixture.Reject(world, new("observe-missing-definition", actor, "exploreSource", Data: "missing-definition"));
        InventoryWorldFixture.Reject(world, new("observe-missing-id", actor, "exploreSource", long.MaxValue, Data: "food"));
        InventoryWorldFixture.Accept(world, new("legacy-observe-first", actor, "exploreSource"));
        state = world.Checkpoint(); InventoryFixture.Check(state.Inventory.Jobs.Single(j => j.Actor == actor).Target == ore.Id,
            "빈 Data의 기존 첫 미관측 원천 선택이 바뀌었습니다.");
        Invariants.Validate(state);
    }
}

public sealed class InventoryObservationDenialScenario : IFeatureScenario
{
    public string Name => "inventory source observation: 현지 정의 부재·실물 비용 부족은 원천·돈·시간·ID·관측 무변화 거부";
    public void Run()
    {
        var world = FoodObservationFixture.Create(); var state = world.Checkpoint(); var actor = state.People[0].Id;
        state.Inventory.Sources.RemoveAll(s => s.Place == 0 && s.Definition == "food");
        state.Places[0].Source = checked((int)state.Inventory.Sources.Where(s => s.Place == 0).Sum(s => s.Remaining));
        world = GameWorld.Restore(state); InventoryWorldFixture.Accept(world, new("missing-food-control", actor, "possess"));
        InventoryWorldFixture.Reject(world, new("missing-local-food", actor, "exploreSource", Data: "food"));

        world = FoodObservationFixture.Create(); state = world.Checkpoint();
        foreach (var item in state.Items.Where(i => i.Owner == actor && i.Definition == "food").ToArray())
        { state.Materials["food"].Loss += item.Quantity; state.Items.Remove(item); state.Inventory.Metadata.RemoveAll(m => m.Item == item.Id); }
        world = GameWorld.Restore(state); InventoryWorldFixture.Accept(world, new("unpaid-food-control", actor, "possess"));
        InventoryWorldFixture.Reject(world, new("unpaid-food-observe", actor, "exploreSource", Data: "food"));
        Invariants.Validate(world.Checkpoint());
    }
}
