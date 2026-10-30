using Chronicles.Game.Currency;

namespace Chronicles.Game.Tests.Features;

internal static class CurrencyWorldFixture
{
    internal static (GameWorld World, long Actor, long Item) WithStones(int quantity = 5, int quality = 5000)
    {
        var world = InventoryWorldFixture.Create(61); var snapshot = world.Checkpoint(); var actor = snapshot.People[0].Id;
        InventoryFixture.Check(snapshot.Currency.Initialized, "Public Create did not initialize actual currency origins.");
        InventoryWorldFixture.Add(snapshot, actor, CurrencyPolicy.StoneDefinition, quantity);
        var item = snapshot.Items.Last(); item.Quality = quality;
        world = GameWorld.Restore(snapshot); InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("currency-control", actor, "possess"));
        return (world, actor, item.Id);
    }
    internal static long Quantity(GameState state, long actor) => state.Items.Where(i => i.Owner == actor && i.Definition == CurrencyPolicy.StoneDefinition).Sum(i => (long)i.Quantity);
    internal static void RejectedSave(GameState snapshot)
    {
        var rejected = false;
        try { GameWorld.Restore(snapshot); } catch (InvalidDataException) { rejected = true; }
        InventoryFixture.Check(rejected, "Source-free or duplicated currency creation was accepted.");
    }
    internal static void Law(GameWorld world, long actor, long rate)
    {
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("currency-observe:" + rate, 0, "observe"));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("currency-rate:" + rate, 0, "law", value: rate, data: "currency.stoneValue"));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("currency-control:" + rate, actor, "possess"));
    }
}

public sealed class CurrencyGenesisScenario : IFeatureScenario
{
    public string Name => "currency public: exact two founder opening origins never grant money again on save or repeat initialization";
    public void Run()
    {
        var world = InventoryWorldFixture.Create(63); var state = world.Checkpoint();
        InventoryFixture.Check(state.People.Count == 2 && state.Currency.Initialized && state.Currency.Receipts.Count == 2 &&
            state.Currency.Receipts.All(r => r.Kind == CurrencyOriginKind.Genesis && r.Amount == state.Laws["founder.initialMoney"]) &&
            state.Currency.Receipts.Select(r => r.Person).Order().SequenceEqual(state.People.Select(p => p.Id).Order()) &&
            state.Currency.Receipts.Sum(r => r.Amount) == state.CreatedMoney, "Genesis created a ghost origin or re-granted opening money.");
        var before = world.StateHash();
        typeof(GameWorld).GetMethod("InitializeCurrency", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(world, null);
        InventoryFixture.Check(before == world.StateHash(), "Repeated currency initialization changed balances, IDs or source receipts.");
        var restored = GameCodec.Decode(GameCodec.Encode(world));
        InventoryFixture.Check(restored.StateHash() == before && restored.Checkpoint().Currency.Receipts.Count == 2, "Currency save continuation duplicated initial money.");
    }
}

public sealed class CurrencyBurnAndReplayScenario : IFeatureScenario
{
    public string Name => "currency public: exact physical burn credits once, accepts functional zero quality, and freezes rate evidence";
    public void Run()
    {
        var (world, actor, item) = CurrencyWorldFixture.WithStones(5, 0); var before = world.Checkpoint();
        var command = InventoryWorldFixture.Command("redeem-two", actor, "redeemCurrency", item, 2);
        InventoryWorldFixture.Accept(world, command); var state = world.Checkpoint();
        var receipt = state.Currency.Receipts.Single(r => r.Kind == CurrencyOriginKind.PhysicalStone);
        InventoryFixture.Check(CurrencyWorldFixture.Quantity(state, actor) == 3 && state.Materials[CurrencyPolicy.StoneDefinition].Consumption == 2 &&
            state.People.Single(p => p.Id == actor).Money == before.People.Single(p => p.Id == actor).Money + 2 && state.CreatedMoney == before.CreatedMoney + 2 &&
            receipt.OriginalItem == item && receipt.ConsumedItem != item && receipt.Quantity == 2 && receipt.Rate == 1 && receipt.Amount == 2 && receipt.Quality == 0 &&
            state.Inventory.Events.Any(e => e.Sequence == receipt.ConsumptionSequence && e.Kind == "physical-consumption" && e.Subject == receipt.ConsumedItem),
            "Balance credit retained the same physical tokens or lost actual zero-quality valuation/burn evidence.");
        var hash = world.StateHash(); InventoryWorldFixture.Accept(world, command); InventoryFixture.Check(hash == world.StateHash(), "Same currency command minted twice.");
        InventoryWorldFixture.Reject(world, command with { Value = 1 });
        world = GameCodec.Decode(GameCodec.Encode(world)); hash = world.StateHash(); InventoryWorldFixture.Accept(world, command);
        InventoryFixture.Check(hash == world.StateHash(), "Currency replay after save burned or credited twice.");
        CurrencyWorldFixture.Law(world, actor, 2);
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("redeem-new-rate", actor, "redeemCurrency", item, 1)); state = world.Checkpoint();
        var later = state.Currency.Receipts.Last();
        InventoryFixture.Check(later.Rate == 2 && later.Amount == 2 && later.LawRevision > receipt.LawRevision &&
            System.Text.Json.JsonSerializer.Serialize(state.Currency.Receipts.Single(r => r.Id == receipt.Id)) == System.Text.Json.JsonSerializer.Serialize(receipt) && CurrencyWorldFixture.Quantity(state, actor) == 2 &&
            state.CreatedMoney == before.CreatedMoney + 4, "New law retroactively changed earlier money or created a balance without burning another token.");
        Invariants.Validate(state);
    }
}

public sealed class CurrencyEligibilityAndCargoScenario : IFeatureScenario
{
    public string Name => "currency public: other crystals defects remote custody cargo and disabled conversion never burn or mint";
    public void Run()
    {
        var (world, actor, item) = CurrencyWorldFixture.WithStones(); var state = world.Checkpoint();
        var other = state.People[1].Id;
        var book = state.Items.First(i => i.Owner == actor && Catalog.Items[i.Definition].Kind == ItemKind.Book).Id;
        InventoryWorldFixture.Add(state, actor, "resource.02.1", 1); var crystal = state.Items.Last().Id;
        InventoryWorldFixture.Add(state, actor, CurrencyPolicy.StoneDefinition, 1); var defective = state.Items.Last(); defective.Defects = ItemDefect.Contamination;
        InventoryWorldFixture.Add(state, actor, CurrencyPolicy.StoneDefinition, 1); var broken = state.Items.Last(); broken.Damage = 100;
        InventoryWorldFixture.Add(state, other, CurrencyPolicy.StoneDefinition, 1); var foreign = state.Items.Last(); foreign.Place = 0;
        InventoryWorldFixture.Add(state, actor, CurrencyPolicy.StoneDefinition, 1); var remote = state.Items.Last();
        state.Inventory.Metadata.Add(new() { Item = remote.Id, Custodian = other, Provenance = [remote.Source] }); state.People.Single(p => p.Id == other).Place = 1;
        world = GameWorld.Restore(state);
        foreach (var target in new[] { book, crystal, defective.Id, broken.Id, remote.Id, foreign.Id })
            InventoryWorldFixture.Reject(world, InventoryWorldFixture.Command("invalid-stone:" + target, actor, "redeemCurrency", target, 1));
        CurrencyWorldFixture.Law(world, actor, 0); InventoryWorldFixture.Reject(world, InventoryWorldFixture.Command("disabled-stone", actor, "redeemCurrency", item, 1));
        CurrencyWorldFixture.Law(world, actor, 1);
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("currency-route", actor, "explore")); world.AdvanceTo(world.Checkpoint().People.Single(p => p.Id == actor).Due);
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("currency-cargo", actor, "transport", item, 1));
        InventoryWorldFixture.Reject(world, InventoryWorldFixture.Command("cargo-cannot-redeem", actor, "redeemCurrency", item, 1));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("currency-cargo-cancel", actor, "cancel"));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("released-stone", actor, "redeemCurrency", item, 1));
        state = world.Checkpoint(); InventoryFixture.Check(state.Currency.Receipts.Count(r => r.Kind == CurrencyOriginKind.PhysicalStone) == 1 &&
            state.Materials[CurrencyPolicy.StoneDefinition].Consumption == 1, "Rejected/disabled/cancelled obligations duplicated token burns or money origins.");
        Invariants.Validate(state);
    }
}

public sealed class CurrencyMiningScenario : IFeatureScenario
{
    public string Name => "currency public: actual discovered mining source becomes spendable money only after physical token consumption";
    public void Run()
    {
        var world = InventoryWorldFixture.Create(67); var state = world.Checkpoint(); var actor = state.People[0].Id;
        state.People[0].Place = 1; state.People[0].KnownPlaces.Add(1); state.People[0].KnownPlaces.Sort(); world = GameWorld.Restore(state);
        var source = state.Inventory.Sources.Single(s => s.Place == 1 && s.Definition == CurrencyPolicy.StoneDefinition);
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("mining-control", actor, "possess"));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("discover-real-stone", actor, "exploreSource", source.Id));
        world.AdvanceTo(world.Checkpoint().People.Single(p => p.Id == actor).Due);
        var money = world.Checkpoint().CreatedMoney;
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("mine-real-stone", actor, "mining", source.Id, data: CurrencyPolicy.StoneDefinition));
        world.AdvanceTo(world.Checkpoint().People.Single(p => p.Id == actor).Due); state = world.Checkpoint();
        var token = state.Items.First(i => i.Owner == actor && i.Definition == CurrencyPolicy.StoneDefinition);
        InventoryFixture.Check(state.CreatedMoney == money && state.Inventory.Sources.Single(s => s.Id == source.Id).Remaining < source.Remaining &&
            state.Materials[CurrencyPolicy.StoneDefinition].Extraction == token.Quantity, "Mining already duplicated spendable money alongside its physical output.");
        var originalQuantity = token.Quantity;
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("redeem-mined-stone", actor, "redeemCurrency", token.Id, 1)); state = world.Checkpoint();
        InventoryFixture.Check(state.CreatedMoney == money + 1 && CurrencyWorldFixture.Quantity(state, actor) == originalQuantity - 1 &&
            state.Currency.Receipts.Last().Source.StartsWith("extraction:", StringComparison.Ordinal), "Mined money lacked the actual finite source and one consumed token.");
        var restored = GameCodec.Decode(GameCodec.Encode(world)); InventoryFixture.Check(world.StateHash() == restored.StateHash(), "Mined money provenance did not survive save.");
    }
}

public sealed class CurrencyEscrowAndDeathScenario : IFeatureScenario
{
    public string Name => "currency public: auction escrow seller death and estate transfers never count as new currency origins";
    public void Run()
    {
        var (world, seller, item) = CurrencyWorldFixture.WithStones(3); var state = world.Checkpoint(); var buyer = state.People[1].Id;
        var created = state.CreatedMoney;
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("currency-auction", seller, "offer", item, 5, "auction"));
        var contract = world.Checkpoint().Inventory.Contracts.Single(c => c.Item == item);
        InventoryWorldFixture.Reject(world, InventoryWorldFixture.Command("listed-cannot-redeem", seller, "redeemCurrency", item, 1));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("currency-buyer", buyer, "possess"));
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("currency-auction-bid", buyer, "bid", contract.Id, 7));
        state = world.Checkpoint(); var person = state.People.Single(p => p.Id == seller); person.Lifespan = state.Month - person.Born + 1; world = GameWorld.Restore(state);
        world.AdvanceTo(world.Month + 2); state = world.Checkpoint();
        InventoryFixture.Check(state.People.Single(p => p.Id == seller).Status == LifeStatus.Dead &&
            state.Inventory.Contracts.Single(c => c.Id == contract.Id).Escrow == 0 && state.Inventory.Contracts.Single(c => c.Id == contract.Id).Status == "cancelled" &&
            state.Items.Single(i => i.Id == item).Quantity == 3 && state.Items.Single(i => i.Id == item).Reserved == 0 &&
            state.CreatedMoney == created && state.Currency.Receipts.Count == 2, "Escrow refund/death inheritance minted money or consumed the seller estate's tokens.");
        InventoryWorldFixture.Reject(world, InventoryWorldFixture.Command("dead-cannot-redeem", seller, "redeemCurrency", item, 1));
        Invariants.Validate(state);
    }
}

public sealed class CurrencySourceCorruptionScenario : IFeatureScenario
{
    public string Name => "currency public: source-free balances repeated burns and disabled origin validation are rejected on restore";
    public void Run()
    {
        var (world, actor, item) = CurrencyWorldFixture.WithStones(2);
        InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("real-burn-before-corruption", actor, "redeemCurrency", item, 1));
        var state = world.Checkpoint(); state.People.Single(p => p.Id == actor).Money++; state.CreatedMoney++; CurrencyWorldFixture.RejectedSave(state);
        state = world.Checkpoint(); var receipt = state.Currency.Receipts.Single(r => r.Kind == CurrencyOriginKind.PhysicalStone);
        state.Currency.Receipts.Add(receipt with { Id = state.Currency.NextReceiptId++, CommandId = "forged-second-burn" });
        state.People.Single(p => p.Id == actor).Money += receipt.Amount; state.CreatedMoney += receipt.Amount; CurrencyWorldFixture.RejectedSave(state);
        state = world.Checkpoint(); state.Currency.Receipts.Add(receipt with { Id = state.Currency.NextReceiptId++, CommandId = "forged-missing-burn",
            ConsumptionSequence = state.Inventory.NextEventSequence + 100 });
        state.People.Single(p => p.Id == actor).Money += receipt.Amount; state.CreatedMoney += receipt.Amount; CurrencyWorldFixture.RejectedSave(state);
        state = world.Checkpoint(); state.Currency.Initialized = false; CurrencyWorldFixture.RejectedSave(state);
        Invariants.Validate(world.Checkpoint());
    }
}
