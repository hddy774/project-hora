using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chronicles.Game.Inventory;

namespace Chronicles.Game.Tests.Features;

public sealed class InventoryOwnedWorldReferenceScenario : IFeatureScenario
{
    private readonly bool projected;
    public InventoryOwnedWorldReferenceScenario() { }
    internal InventoryOwnedWorldReferenceScenario(bool projected) { this.projected = projected; }
    // Captured before changing the private transaction path, at 751e349.
    private const string Baseline = "2cef8af882dcd34fec03884184a9fef5c67b8232e77a0e14c022cb84c986eec6";
    public string Name => "inventory owned reference: 기존 공개 제작·보관·거래·저장·명령 재전송 결과 보존";

    public void Run()
    {
        var world = InventoryWorldFixture.Create(73, new Dictionary<string, long> { ["combat.hitMinimum"] = 10000, ["combat.hitMaximum"] = 10000 });
        var state = world.Checkpoint(); var actor = state.People[0].Id; var other = state.People[1].Id;
        InventoryWorldFixture.Add(state, actor, "ore", 5); InventoryWorldFixture.Add(state, actor, "cloth", 2);
        InventoryWorldFixture.Add(state, actor, "bag", 1); InventoryWorldFixture.Add(state, actor, "herb", 4);
        world = GameWorld.Restore(state); var trace = new InventoryWorldTranscript(world, projected);
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
        trace.Save(); trace.Assert(Baseline);
    }
}

public sealed class InventoryOwnedEcologyReferenceScenario : IFeatureScenario
{
    private readonly bool projected;
    public InventoryOwnedEcologyReferenceScenario() { }
    internal InventoryOwnedEcologyReferenceScenario(bool projected) { this.projected = projected; }
    // The low-food decision must really execute the private split+offer caller.
    private const string Baseline = "8e88d149188b0d0c8a65334ef1827eb0675bcdd3a81602a57bb6e6d48d36f561";
    public string Name => "inventory owned reference: 실제 생계 AI의 분할·판매 후보와 저장 결과 보존";
    public void Run()
    {
        var world = InventoryWorldFixture.Create(109, new Dictionary<string, long> { ["founder.initialFood"] = 8, ["ecology.foodUse"] = 1 });
        var state = world.Checkpoint(); var seller = state.People[0].Id; var buyer = state.People[1].Id;
        state.People[1].Action = ActionKind.Guard; state.People[1].Due = 1000; world = GameWorld.Restore(state);
        var trace = new InventoryWorldTranscript(world, projected); var food = state.Items.Single(i => i.Owner == seller && i.Definition == "food").Id;
        trace.Advance(1); state = world.Checkpoint();
        var offer = state.Inventory.Contracts.Single(c => c.Seller == seller && c.Status == "open" && c.AdvertisedDefinition == "food");
        InventoryFixture.Check(offer.Item != food && offer.AdvertisedQuantity > 0 && state.People[0].ActionData == "ecology:market" &&
            state.Items.Single(i => i.Id == food).Quantity == 3, "실제 생계 AI가 원래 식량을 보존한 채 잉여분을 분할·판매하지 않았습니다.");
        trace.Save(); trace.Command(new("owned-ecology-buyer", buyer, "possess"));
        trace.Command(new("owned-ecology-buy", buyer, "trade", offer.Id)); trace.Save();
        InventoryFixture.Check(world.Checkpoint().Items.Single(i => i.Id == offer.Item).Owner == buyer, "생계 AI 매물을 실제 구매자에게 전달하지 못했습니다.");
        trace.Assert(Baseline);
    }
}

internal sealed class InventoryWorldTranscript
{
    private readonly GameWorld world;
    private readonly bool projected;
    private readonly List<string> rows;
    private GameWorld? saved;
    internal InventoryWorldTranscript(GameWorld world, bool projected = false)
    {
        this.world = world; this.projected = projected; rows = [world.StateHash()];
        InventoryFixture.Check(world.InventoryProjectionEnabled, "일반 세계의 기본 경로가 채택된 Person 투영이 아닙니다.");
        if (!projected) world.UseStrictInventoryForVerification();
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
        InventoryFixture.Check(restored.InventoryProjectionEnabled, "저장 재개가 채택된 기본 Person 투영 경로를 사용하지 않습니다.");
        if (!projected) restored.UseStrictInventoryForVerification();
        saved = restored;
        rows.Add(Convert.ToHexStringLower(SHA256.HashData(encoded)));
    }
    internal void Assert(string expected)
    {
        Invariants.Validate(world.Checkpoint());
        InventoryFixture.Check(projected ? world.InventoryProjectionSnapshots > 0 : world.InventoryProjectionSnapshots == 0,
            "공개 기준선이 선택된 기본 투영/strict reference factory를 실행하지 않았습니다.");
        var actual = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", rows))));
        InventoryFixture.Check(actual == expected, "기존 공개 경로와 명령·월 경계·저장 해시가 달라졌습니다: " + actual);
    }
}
