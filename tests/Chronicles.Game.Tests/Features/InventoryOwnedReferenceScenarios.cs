using Chronicles.Game.Inventory;

namespace Chronicles.Game.Tests.Features;

public sealed class InventoryOwnedWorldReferenceScenario : IFeatureScenario
{
    private readonly bool projected;
    private readonly bool typedCopy;
    public InventoryOwnedWorldReferenceScenario() { }
    internal InventoryOwnedWorldReferenceScenario(bool projected, bool typedCopy = false) { this.projected = projected; this.typedCopy = typedCopy; }
    // Captured before changing the private transaction path, at 751e349.
    private const string Baseline = "2cef8af882dcd34fec03884184a9fef5c67b8232e77a0e14c022cb84c986eec6";
    public string Name => "inventory owned reference: 기존 공개 제작·보관·거래·저장·명령 재전송 결과 보존";

    public void Run() => Capture().AssertFixed(Baseline);
    internal InventoryReferenceTrace Capture()
    {
        var world = InventoryWorldFixture.Create(73, new Dictionary<string, long> { ["combat.hitMinimum"] = 10000, ["combat.hitMaximum"] = 10000 });
        var state = world.Checkpoint(); var actor = state.People[0].Id; var other = state.People[1].Id;
        InventoryWorldFixture.Add(state, actor, "ore", 5); InventoryWorldFixture.Add(state, actor, "cloth", 2);
        InventoryWorldFixture.Add(state, actor, "bag", 1); InventoryWorldFixture.Add(state, actor, "herb", 4);
        world = GameWorld.Restore(state); var trace = new InventoryWorldTranscript(world, projected, typedCopy);
        void Apply(string id, long person, string kind, long target = 0, long value = 0, string data = "", bool expected = true) =>
            trace.Command(new(id, person, kind, target, value, data), expected);
        long Own(string definition) => world.Checkpoint().Items.Last(i => i.Owner == actor && i.Definition == definition).Id;

        Apply("owned-control", actor, "possess");
        Apply("owned-no-materials", actor, "craft", data: "pill", expected: false);
        Apply("owned-craft", actor, "craft", data: "armor"); trace.Save();
        Apply("owned-craft", actor, "craft", data: "armor");
        Apply("owned-craft", actor, "craft", data: "tool", expected: false);
        trace.Advance(world.Checkpoint().People.Single(p => p.Id == actor).Due);
        InventoryFixture.Check(world.Checkpoint().Inventory.Jobs.Single(j => j.Actor == actor && j.Recipe == "armor").Status == "completed", "기준 제작이 실물을 완성하지 못했습니다.");
        var armor = Own("armor"); Apply("owned-equip", actor, "equip", armor); Apply("owned-bind", actor, "bind", armor);
        Apply("owned-opponent", other, "possess");
        if (world.Checkpoint().People.Single(p => p.Id == other).Action != ActionKind.Idle) Apply("owned-ready", other, "cancel");
        Apply("owned-hit", other, "attack", actor);
        var damage = world.Checkpoint().Items.Single(i => i.Id == armor).Damage;
        InventoryFixture.Check(damage > 0, "실제 타격이 기준 장비에 손상을 남기지 않았습니다.");
        Apply("owned-owner", actor, "possess"); trace.Advance(world.Checkpoint().People.Single(p => p.Id == actor).Due);
        Apply("owned-repair", actor, "repair", armor); trace.Save(); trace.Advance(world.Checkpoint().People.Single(p => p.Id == actor).Due);
        InventoryFixture.Check(world.Checkpoint().Items.Single(i => i.Id == armor).Damage < damage, "기준 수리가 실제 손상을 줄이지 않았습니다.");
        var herbs = Own("herb"); var bag = Own("bag");
        Apply("owned-store", actor, "store", herbs, bag); Apply("owned-split", actor, "split", herbs, 2);
        var split = Own("herb"); Apply("owned-merge", actor, "merge", herbs, split);
        Apply("owned-take", actor, "store", herbs); Apply("owned-offer", actor, "offer", herbs, 5);
        var sale = world.Checkpoint().Inventory.Contracts.Single(c => c.Item == herbs && c.Status == "open").Id;
        Apply("owned-withdraw", actor, "withdraw", sale); Apply("owned-resale", actor, "offer", herbs, 6);
        sale = world.Checkpoint().Inventory.Contracts.Single(c => c.Item == herbs && c.Status == "open").Id;
        Apply("owned-buyer", other, "possess"); Apply("owned-buy", other, "trade", sale); Apply("owned-buy", other, "trade", sale);
        Apply("owned-auction-owner", actor, "possess"); var tool = Own("tool"); Apply("owned-auction", actor, "offer", tool, 5, "auction");
        var auction = world.Checkpoint().Inventory.Contracts.Single(c => c.Item == tool && c.Status == "open");
        Apply("owned-bidder", other, "possess"); Apply("owned-bid", other, "bid", auction.Id, 7);
        Apply("owned-unfunded", other, "bid", auction.Id, long.MaxValue, expected: false);
        Apply("owned-seller-again", actor, "possess"); Apply("owned-no-withdraw", actor, "withdraw", auction.Id, expected: false);
        trace.Save(); trace.Advance(auction.Due);
        state = world.Checkpoint(); InventoryFixture.Check(state.Inventory.Contracts.Single(c => c.Id == auction.Id).Status == "completed" &&
            state.Items.Single(i => i.Id == tool).Owner == other && state.Items.Single(i => i.Id == herbs).Owner == other,
            "실제 매매 또는 경매가 수취인에게 실물을 인도하지 못했습니다.");
        trace.Command(new("owned-stale-revision", actor, "split", herbs, 1, ExpectedRevision: world.Revision - 1), false);
        trace.Save(); return trace.Complete("manual");
    }
}

public sealed class InventoryOwnedEcologyReferenceScenario : IFeatureScenario
{
    private readonly bool projected;
    private readonly bool typedCopy;
    public InventoryOwnedEcologyReferenceScenario() { }
    internal InventoryOwnedEcologyReferenceScenario(bool projected, bool typedCopy = false) { this.projected = projected; this.typedCopy = typedCopy; }
    // Current ordinary trade, with an explicit commercial personality, must execute private split+offer above reserve3.
    private const string Baseline = "8e88d149188b0d0c8a65334ef1827eb0675bcdd3a81602a57bb6e6d48d36f561";
    public string Name => "inventory owned reference: 명시 거래 성향과 비축3 경계의 실제 분할·판매·저장 결과 보존";
    public void Run() => Capture().AssertFixed(Baseline);
    internal InventoryReferenceTrace Capture()
    {
        var world = InventoryWorldFixture.Create(109, new Dictionary<string, long> { ["founder.initialFood"] = 8, ["ecology.foodUse"] = 1, ["economy.foodReserveMonths"] = 3 });
        var state = world.Checkpoint(); var seller = state.People[0].Id; var buyer = state.People[1].Id;
        state.People[0].Personality = new int[14]; state.People[0].Personality[4] = 100; state.People[0].Personality[5] = 100;
        state.People[1].Action = ActionKind.Guard; state.People[1].Due = 1000; world = GameWorld.Restore(state);
        var trace = new InventoryWorldTranscript(world, projected, typedCopy); var food = state.Items.Single(i => i.Owner == seller && i.Definition == "food").Id;
        trace.Advance(1); state = world.Checkpoint();
        var offer = state.Inventory.Contracts.Single(c => c.Seller == seller && c.Status == "open" && c.AdvertisedDefinition == "food");
        var target = state.Laws["ecology.foodUse"] * state.Laws["economy.foodReserveMonths"];
        InventoryFixture.Check(target == 3 && offer.Item != food && offer.AdvertisedQuantity == 7 - target && state.People[0].ActionData == "ecology:market" &&
            state.People[0].Goal == "실제 잉여품 교환" && state.People[0].DecisionCandidates.Any(c => c.Goal == "교역·교섭" && c.Score > 0) &&
            state.Items.Single(i => i.Id == food).Quantity == target, "명시 거래 성향의 일반 교역 목표가 실제 비축 경계와 잉여분을 분할·판매하지 않았습니다.");
        trace.Save(); trace.Command(new("owned-ecology-buyer", buyer, "possess"));
        trace.Command(new("owned-ecology-buy", buyer, "trade", offer.Id)); trace.Save();
        InventoryFixture.Check(world.Checkpoint().Items.Single(i => i.Id == offer.Item).Owner == buyer, "생계 AI 매물을 실제 구매자에게 전달하지 못했습니다.");
        return trace.Complete("policy-surplus");
    }
}
