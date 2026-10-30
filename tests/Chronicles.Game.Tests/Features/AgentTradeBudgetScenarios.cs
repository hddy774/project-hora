using Chronicles.Game.Currency;

namespace Chronicles.Game.Tests.Features;

public sealed class AgentTradeBudgetBoundaryScenario : IFeatureScenario
{
    public string Name => "INV-004/006/017/018/021 AI 거래 예산: 마지막 방문 뒤 생계 출품을 유예하고 충분한 예산에서만 실제 거래·환전을 선택한다";

    public void Run()
    {
        foreach (var budget in new[] { 1, 2 })
        foreach (var hasStone in new[] { false, true })
        {
            var (world, actor, _) = CurrencyPlanningFixture.Create(budget: budget);
            var opening = world.Checkpoint();
            if (hasStone) InventoryWorldFixture.Add(opening, actor, CurrencyPolicy.StoneDefinition, 1);
            world = GameWorld.Restore(opening);
            var resumed = GameCodec.Decode(GameCodec.Encode(world));
            var food = opening.Items.Where(i => i.Owner == actor && i.Definition == "food").Sum(i => i.Quantity);
            world.AdvanceTo(1); resumed.AdvanceTo(1);
            InventoryFixture.Check(world.StateHash() == resumed.StateHash(), "거래 예산 경계의 실제 명령·원장·RNG가 저장 재개와 달라졌습니다.");
            var state = world.Checkpoint(); var person = state.People.Single(p => p.Id == actor);
            var offers = state.Inventory.Contracts.Where(c => c.Seller == actor).ToArray();
            InventoryFixture.Check(person.PlanExpansions == budget && person.PlanExpansions <= state.Laws["ai.planBudget"],
                "일반 거래 후보의 실제 방문이 예산에 정확히 반영되지 않았습니다.");
            if (budget == 1)
            {
                InventoryFixture.Check(person.Action == ActionKind.Wait && offers.Length == 0 &&
                    state.Items.Where(i => i.Owner == actor && i.Definition == "food").Sum(i => i.Quantity) == food &&
                    !state.Items.Any(i => i.Owner == actor && i.Reserved != 0) &&
                    CurrencyWorldFixture.Quantity(state, actor) == (hasStone ? 1 : 0) && person.Money == opening.People[0].Money &&
                    state.CreatedMoney == opening.CreatedMoney && state.Currency.Receipts.All(r => r.Kind == CurrencyOriginKind.Genesis) &&
                    state.RandomState == opening.RandomState,
                    "소진된 예산 뒤 Eco fallback이 실물을 분할·예약·출품하거나 환전·추가 난수를 실행했습니다.");
            }
            else if (hasStone)
            {
                InventoryFixture.Check(person.Action == ActionKind.Guard && offers.Length == 0 && person.Money == 1 &&
                    CurrencyWorldFixture.Quantity(state, actor) == 0 && state.CreatedMoney == opening.CreatedMoney + 1 &&
                    state.Currency.Receipts.Count(r => r.Kind == CurrencyOriginKind.PhysicalStone) == 1,
                    "충분한 예산의 실제 환전이 생계 판매로 바뀌거나 실물 소비·원천을 보존하지 못했습니다.");
            }
            else
            {
                var offer = offers.Single(); var physical = state.Items.Single(i => i.Id == offer.Item);
                InventoryFixture.Check(person.Action == ActionKind.Guard && person.ActionData == "ecology:market" &&
                    offer.Status == "open" && physical.Definition == "food" && physical.Quantity == offer.AdvertisedQuantity &&
                    physical.Reserved == offer.Reservation && state.Inventory.Reservations.Single(r => r.Id == offer.Reservation).Active &&
                    state.Items.Where(i => i.Owner == actor && i.Definition == "food").Sum(i => i.Quantity) == food &&
                    person.Money == opening.People[0].Money && state.CreatedMoney == opening.CreatedMoney,
                    "충분한 예산의 일반 거래가 실제 유한 잉여 출품과 예약을 실행하지 못했습니다.");
            }
            Invariants.Validate(state);
        }
    }
}
