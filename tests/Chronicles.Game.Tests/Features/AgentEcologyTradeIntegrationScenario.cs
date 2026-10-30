namespace Chronicles.Game.Tests.Features;

public sealed class AgentEcologyTradeIntegrationScenario : IFeatureScenario
{
    public string Name => "INV-008/017/018 agent planning: 실제 정책 비축을 넘는 식량을 일반 거래 목표에서 출품하고 보유 실물만 인도한다";
    public void Run()
    {
        var world = InventoryWorldFixture.Create(109, new Dictionary<string, long>
        { ["founder.initialFood"] = 8, ["ecology.foodUse"] = 1 });
        var state = world.Checkpoint(); var seller = state.People[0]; var buyer = state.People[1];
        seller.Personality = new int[14]; seller.Personality[4] = 100; seller.Personality[5] = 100;
        buyer.Action = ActionKind.Guard; buyer.Due = 1000; world = GameWorld.Restore(state);
        world.AdvanceTo(1); state = world.Checkpoint(); seller = state.People.Single(p => p.Id == seller.Id);
        var offer = state.Inventory.Contracts.Single(c => c.Seller == seller.Id && c.Status == "open" && c.AdvertisedDefinition == "food");
        var target = state.Laws["ecology.foodUse"] * state.Laws["economy.foodReserveMonths"];
        var reserve = state.Items.Where(i => i.Owner == seller.Id && i.Definition == "food" && i.Reserved == 0).Sum(i => i.Quantity);
        AgentPlanningFixture.Check(seller.ActionData == "ecology:market" && seller.Goal == "실제 잉여품 교환" && reserve == target &&
            offer.AdvertisedQuantity == 7 - target && seller.DecisionCandidates.Any(c => c.Goal == "교역·교섭" && c.Score > 0),
            "Ordinary trade did not select actual policy surplus, or food acquisition incorrectly consumed the surplus branch.");
        var replay = GameCodec.Decode(GameCodec.Encode(world));
        var control = new GameCommand("policy-food-buyer", buyer.Id, "possess");
        var purchase = new GameCommand("policy-food-buy", buyer.Id, "trade", offer.Id);
        foreach (var command in new[] { control, purchase })
        {
            AgentPlanningFixture.Check(world.Execute(command).Accepted && replay.Execute(command).Accepted && world.StateHash() == replay.StateHash(),
                "Policy-surplus sale or saved physical handover diverged.");
        }
        var hash = world.StateHash(); AgentPlanningFixture.Check(world.Execute(purchase).Accepted && world.StateHash() == hash,
            "Replayed surplus purchase duplicated food or payment.");
        state = world.Checkpoint(); AgentPlanningFixture.Check(state.Items.Single(i => i.Id == offer.Item).Owner == buyer.Id &&
            state.Inventory.Contracts.Single(c => c.Id == offer.Id).Status == "completed", "Policy surplus was not the same actual physical asset.");
        Invariants.Validate(state);
    }
}
