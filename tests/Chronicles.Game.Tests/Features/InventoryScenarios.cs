using System.Text.Json;
using Chronicles.Game.Inventory;

namespace Chronicles.Game.Tests;

internal static class InventoryFixture
{
    internal static InventoryState Create() => new()
    {
        NextId = 5,
        People = [new() { Id = 1, SoulId = 3, Place = 0, Money = 1000 }, new() { Id = 2, SoulId = 4, Place = 0, Money = 1000 }]
    };
    internal static long Add(InventoryState state, string definition, int count = 1, long owner = 1, int quality = 5000,
        ItemDefect defects = ItemDefect.None, string source = "fixture origin")
    {
        var item = new Item { Id = state.NextId++, Owner = owner, Definition = definition, Quantity = count, Quality = quality,
            Defects = defects, Place = 0, Source = source }; state.Items.Add(item);
        if (!state.Materials.TryGetValue(definition, out var flow)) state.Materials.Add(definition, flow = new());
        flow.Opening += count; return item.Id;
    }
    internal static long Accept(ref InventoryState state, InventoryCommand command)
    {
        var before = JsonSerializer.Serialize(state); var result = InventoryEngine.Apply(state, command);
        Check(JsonSerializer.Serialize(state) == before, "Accepted command mutated its input.");
        Check(result.Accepted, command.GetType().Name + ": " + result.Message); state = result.State; return result.Subject;
    }
    internal static void Reject(InventoryState state, InventoryCommand command)
    {
        var before = JsonSerializer.Serialize(state); var result = InventoryEngine.Apply(state, command);
        Check(!result.Accepted && JsonSerializer.Serialize(state) == before && ReferenceEquals(result.State, state), "Rejected command changed authority.");
    }
    internal static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    internal static void RoundTrip(ref InventoryState state)
    {
        var before = JsonSerializer.Serialize(state); state = JsonSerializer.Deserialize<InventoryState>(before)!;
        InventoryEngine.Validate(state); Check(before == JsonSerializer.Serialize(state), "Inventory save roundtrip changed state.");
    }
    internal static Item Item(InventoryState state, long id) => state.Items.Single(i => i.Id == id);
    internal static Person Person(InventoryState state, long id) => state.People.Single(p => p.Id == id);
    internal static long Money(InventoryState state) => state.People.Sum(p => p.Money) + state.Ledger.Contracts.Sum(c => c.Escrow);
}

public sealed class InventoryStackScenario : IFeatureScenario
{
    public string Name => "INV-016 inventory split/merge preserves hidden state, binding and provenance";
    public void Run()
    {
        var state = InventoryFixture.Create();
        var id = InventoryFixture.Add(state, "herb", 10, quality: 4567, defects: ItemDefect.Contamination | ItemDefect.MissingPages);
        InventoryFixture.Item(state, id).BoundSoul = 3;
        state.Ledger.Metadata.Add(new() { Item = id, Custodian = 1, UseRights = [2], Provenance = ["source:7", "harvest:9"] });
        var split = InventoryFixture.Accept(ref state, new SplitInventoryItem("split", 1, id, 4));
        var child = InventoryFixture.Item(state, split);
        InventoryFixture.Check(child.Quantity == 4 && child.Source == "fixture origin" && child.Quality == 4567 && child.BoundSoul == 3 &&
            child.Defects == (ItemDefect.Contamination | ItemDefect.MissingPages) && state.Ledger.Metadata.Single(m => m.Item == split).Provenance is ["source:7", "harvest:9"], "Split lost physical state.");
        InventoryFixture.RoundTrip(ref state);
        InventoryFixture.Accept(ref state, new MergeStacks("merge", 1, id, split));
        InventoryFixture.Check(InventoryFixture.Item(state, id).Quantity == 10, "Merge lost units.");
        split = InventoryFixture.Accept(ref state, new SplitInventoryItem("split2", 1, id, 3));
        InventoryFixture.Item(state, split).Quality++;
        InventoryFixture.Reject(state, new MergeStacks("bad-quality", 1, id, split));
        InventoryFixture.Item(state, split).Quality--; InventoryFixture.Item(state, split).Source = "different origin";
        InventoryFixture.Reject(state, new MergeStacks("bad-source", 1, id, split));
    }
}

public sealed class InventoryCustodyScenario : IFeatureScenario
{
    public string Name => "INV-016 ownership, custody, use, equipment and remote access remain independent";
    public void Run()
    {
        var state = InventoryFixture.Create(); var tool = InventoryFixture.Add(state, "tool");
        InventoryFixture.Accept(ref state, new GrantUse("grant", 1, tool, 2, true));
        InventoryFixture.Accept(ref state, new SetCustodian("custody", 1, tool, 2));
        InventoryFixture.Accept(ref state, new EquipItem("borrow", 2, tool));
        InventoryFixture.Check(InventoryFixture.Item(state, tool).Owner == 1 && InventoryFixture.Person(state, 2).Equipment[8] == tool, "Borrowing changed title.");
        InventoryFixture.Reject(state, new OfferItem("double-use", 1, tool, 5, 10));
        InventoryFixture.Accept(ref state, new UnequipItem("return", 2, tool, ReturnToGround: true));
        InventoryFixture.Accept(ref state, new TransferOwnership("title", 1, tool, 2));
        InventoryFixture.Check(state.Ledger.Metadata.Single(m => m.Item == tool).Custodian == 2, "Title rewrote custody.");
        var food = InventoryFixture.Add(state, "food", 2);
        InventoryFixture.Accept(ref state, new TransferOwnership("food-title", 1, food, 2));
        InventoryFixture.Check(state.Ledger.Metadata.Single(m => m.Item == food).Custodian == 1, "Title transfer silently handed over custody.");
        InventoryFixture.Accept(ref state, new SetCustodian("physical-handover", 2, food, 2));
        InventoryFixture.Person(state, 2).Place = 1;
        InventoryFixture.Reject(state, new ConsumeItem("remote-consume", 1, food));
        InventoryFixture.RoundTrip(ref state);
    }
}

public sealed class InventoryContainerScenario : IFeatureScenario
{
    public string Name => "INV-016 recursive container capacity, cycle rejection and broken-container overflow preserve stock";
    public void Run()
    {
        var state = InventoryFixture.Create(); var outer = InventoryFixture.Add(state, "bag"); var inner = InventoryFixture.Add(state, "bag");
        var ore = InventoryFixture.Add(state, "ore", 8); var extra = InventoryFixture.Add(state, "ore", 2);
        state.Ledger.Metadata.Add(new() { Item = outer, Custodian = 1, Capacity = 10 });
        state.Ledger.Metadata.Add(new() { Item = inner, Custodian = 1, Capacity = 8 });
        InventoryFixture.Accept(ref state, new StoreItem("fill", 1, ore, inner));
        InventoryFixture.Accept(ref state, new StoreItem("nest", 1, inner, outer));
        InventoryFixture.Reject(state, new StoreItem("cycle", 1, outer, inner));
        InventoryFixture.Reject(state, new StoreItem("recursive-overflow", 1, extra, outer));
        InventoryFixture.Accept(ref state, new DamageItem("break", 1, inner, 100));
        InventoryFixture.Check(InventoryFixture.Item(state, ore).Container == 0 && InventoryFixture.Item(state, ore).Place == 0 &&
            state.Items.Where(i => i.Definition == "ore").Sum(i => i.Quantity) == 10, "Broken storage lost stock or teleported it.");
        InventoryFixture.RoundTrip(ref state);
    }
}

public sealed class InventoryEquipmentScenario : IFeatureScenario
{
    public string Name => "INV-005/015/016 equipment replacement is atomic across occupied slots and return capacity";
    public void Run()
    {
        var state = InventoryFixture.Create(); var main = InventoryFixture.Add(state, "weapon"); var off = InventoryFixture.Add(state, "equipment.1");
        var great = InventoryFixture.Add(state, "greatWeapon"); var bag = InventoryFixture.Add(state, "bag");
        state.Ledger.Metadata.Add(new() { Item = bag, Custodian = 1, Capacity = 1 });
        InventoryFixture.Accept(ref state, new EquipItem("main", 1, main)); InventoryFixture.Accept(ref state, new EquipItem("off", 1, off));
        InventoryFixture.Reject(state, new EquipItem("small-return", 1, great, bag));
        InventoryFixture.Check(InventoryFixture.Person(state, 1).Equipment[0] == main && InventoryFixture.Person(state, 1).Equipment[1] == off, "Failed swap partially displaced gear.");
        InventoryFixture.Accept(ref state, new EquipItem("both", 1, great, ReturnToGround: true));
        InventoryFixture.Check(InventoryFixture.Person(state, 1).Equipment.Values.Distinct().Single() == great, "Multi-slot item duplicated authority.");
        InventoryFixture.Accept(ref state, new BindItem("bind", 1, great, "actual formation:1"));
        InventoryFixture.Accept(ref state, new DamageItem("wear", 1, great, 40));
        InventoryFixture.Add(state, "ore");
        var job = InventoryFixture.Accept(ref state, new RepairItem("repair", 1, great, 1));
        InventoryFixture.Reject(state, new OfferItem("repair-sale", 1, great, 20, 10));
        InventoryFixture.Accept(ref state, new BeginProduction("repair-process", 1, job)); state.Month = 1;
        InventoryFixture.Accept(ref state, new FinishProduction("repair-finish", 1, job));
        InventoryFixture.Check(InventoryFixture.Item(state, great).BoundSoul == 3 && InventoryFixture.Item(state, great).Damage == 15 &&
            state.Ledger.Bindings.Single().Evidence == "actual formation:1", "Repair lost binding contract or changed its soul.");
        InventoryFixture.RoundTrip(ref state);
    }
}

public sealed class InventoryLaborScenario : IFeatureScenario
{
    public string Name => "INV-017 care, teaching, production and defense share person and facility labor";
    public void Run()
    {
        var state = InventoryFixture.Create();
        InventoryFixture.Accept(ref state, new ReserveLabor("care", 1, "care", 0, 3, 40));
        InventoryFixture.Accept(ref state, new ReserveLabor("teaching", 1, "teaching", 1, 2, 30, 99, 60));
        InventoryFixture.Reject(state, new ReserveLabor("defense", 1, "defense", 1, 3, 40));
        InventoryFixture.Reject(state, new ReserveLabor("facility", 2, "teaching", 1, 2, 40, 99, 60));
        InventoryFixture.Accept(ref state, new ReserveLabor("adjacent", 1, "defense", 3, 4, 100));
        InventoryFixture.Add(state, "herb", 3); InventoryFixture.Add(state, "wood");
        InventoryFixture.Reject(state, new StartProduction("overbook", 1, "pill", 1, 70));
        InventoryFixture.Check(state.Items.All(i => i.Reserved == 0), "Failed labor booking reserved ingredients.");
        InventoryFixture.RoundTrip(ref state);
    }
}

public sealed class InventoryCraftingScenario : IFeatureScenario
{
    public string Name => "INV-017 real production inputs, stage cancellation, failure, use and save settlement";
    public void Run()
    {
        var state = InventoryFixture.Create(); InventoryFixture.Add(state, "herb", 9); InventoryFixture.Add(state, "wood", 3);
        var job = InventoryFixture.Accept(ref state, new StartProduction("draft", 1, "pill", 1));
        InventoryFixture.Reject(state, new EquipItem("committed-equip", 1, state.Items.First(i => i.Reserved != 0).Id));
        InventoryFixture.Accept(ref state, new CancelProduction("cancel-draft", 1, job));
        InventoryFixture.Check(state.Items.Where(i => i.Definition == "herb").Sum(i => i.Quantity) == 9, "Unused cancellation destroyed stock.");
        job = InventoryFixture.Accept(ref state, new StartProduction("consume-job", 1, "pill", 1));
        InventoryFixture.Accept(ref state, new BeginProduction("boundary", 1, job));
        InventoryFixture.RoundTrip(ref state);
        InventoryFixture.Accept(ref state, new CancelProduction("cancel-consumed", 1, job));
        InventoryFixture.Check(state.Items.Where(i => i.Definition == "herb").Sum(i => i.Quantity) == 6 && state.Materials["herb"].Consumption == 3, "Consumed cancellation refunded stock.");
        job = InventoryFixture.Accept(ref state, new StartProduction("success-job", 1, "pill", 1));
        InventoryFixture.Accept(ref state, new BeginProduction("success-boundary", 1, job)); state.Month = 1;
        var pill = InventoryFixture.Accept(ref state, new FinishProduction("success", 1, job));
        InventoryFixture.Reject(state, new FinishProduction("success-again", 1, job));
        InventoryFixture.Person(state, 1).Health = 50;
        InventoryFixture.Accept(ref state, new ConsumeItem("heal", 1, pill));
        InventoryFixture.Check(InventoryFixture.Person(state, 1).Health > 50 && state.Items.All(i => i.Id != pill), "Actual pill was not consumed or had no effect.");
        job = InventoryFixture.Accept(ref state, new StartProduction("failed-job", 1, "pill", 2));
        InventoryFixture.Accept(ref state, new BeginProduction("failed-boundary", 1, job)); state.Month = 2;
        InventoryFixture.Accept(ref state, new FinishProduction("failed", 1, job, false));
        InventoryFixture.Check(state.Items.All(i => i.Definition != "pill") && state.Materials["herb"].Consumption == 9, "Failed production created output or refunded inputs.");
    }
}

public sealed class InventoryResourcesScenario : IFeatureScenario
{
    public string Name => "V021-I01/K03-K08 all 64 resources have executable producers and consumers";
    public void Run()
    {
        InventoryFixture.Check(InventoryDefinitions.Resources.Count == 64 && InventoryDefinitions.Resources.Select(r => r.Family).Distinct().Count() == 16, "Resource families incomplete.");
        foreach (var route in InventoryDefinitions.Resources)
        {
            var state = InventoryFixture.Create(); long job;
            if (route.Producer.StartsWith("source.", StringComparison.Ordinal))
            {
                var sourceId = state.NextId++;
                state.Ledger.Sources.Add(new() { Id = sourceId, Place = 0, Definition = route.Definition, Activity = route.Producer,
                    Remaining = 4, Capacity = 8, Regeneration = 0, Origin = "finite world deposit" });
                job = InventoryFixture.Accept(ref state, new ExtractResource("extract", 1, sourceId, 4, 1));
                InventoryFixture.Reject(state, new ExtractResource("over-extract", 1, sourceId, 1, 1, 1));
            }
            else
            {
                var recipe = InventoryDefinitions.Recipes[route.Producer];
                foreach (var input in recipe.Inputs) InventoryFixture.Add(state, input.Key, input.Value);
                job = InventoryFixture.Accept(ref state, new StartProduction("produce", 1, recipe.Id, 1));
            }
            InventoryFixture.Accept(ref state, new BeginProduction("begin-producer", 1, job)); state.Month = 1;
            var produced = InventoryFixture.Accept(ref state, new FinishProduction("finish-producer", 1, job));
            InventoryFixture.Check(InventoryFixture.Item(state, produced).Definition == route.Definition, "Producer returned another definition.");
            var consumer = InventoryDefinitions.Recipes[route.Consumer];
            foreach (var input in consumer.Inputs)
            {
                var stock = state.Items.Where(i => i.Definition == input.Key).Sum(i => i.Quantity);
                if (stock < input.Value) InventoryFixture.Add(state, input.Key, input.Value - stock);
            }
            job = InventoryFixture.Accept(ref state, new StartProduction("consumer", 1, consumer.Id, 2));
            InventoryFixture.Accept(ref state, new BeginProduction("begin-consumer", 1, job)); state.Month = 2;
            InventoryFixture.Accept(ref state, new FinishProduction("finish-consumer", 1, job));
            InventoryFixture.Check(state.Materials[route.Definition].Consumption > 0, "Consumer did not consume its advertised resource.");
            InventoryFixture.RoundTrip(ref state);
        }
    }
}

public sealed class InventoryAuctionScenario : IFeatureScenario
{
    public string Name => "INV-017/018 auctions fund deposits, reject reuse, refund death and settle equal deadlines once";
    public void Run()
    {
        var state = InventoryFixture.Create(); var first = InventoryFixture.Add(state, "weapon"); var second = InventoryFixture.Add(state, "tool");
        var total = InventoryFixture.Money(state);
        var auction = InventoryFixture.Accept(ref state, new OfferItem("offer", 1, first, 100, 2, true));
        var unsold = InventoryFixture.Accept(ref state, new OfferItem("unsold", 1, second, 50, 2, true));
        InventoryFixture.Reject(state, new EquipItem("reserved", 1, first));
        InventoryFixture.Reject(state, new PlaceBid("unfunded", 2, auction, 1001));
        InventoryFixture.Accept(ref state, new PlaceBid("bid", 2, auction, 600));
        InventoryFixture.Accept(ref state, new PlaceBid("raise-own", 2, auction, 700));
        InventoryFixture.Check(InventoryFixture.Money(state) == total && InventoryFixture.Person(state, 2).Money == 300, "Rebid duplicated its own deposit.");
        InventoryFixture.Reject(state, new WithdrawOffer("withdraw-funded", 1, auction));
        InventoryFixture.RoundTrip(ref state); state.Month = 2;
        InventoryFixture.Accept(ref state, new SettleInventory("settle"));
        InventoryFixture.Check(InventoryFixture.Item(state, first).Owner == 2 && state.Ledger.Contracts.Single(c => c.Id == unsold).Status == "expired" &&
            InventoryFixture.Money(state) == total, "Simultaneous auction settlement lost property or money.");
        var before = JsonSerializer.Serialize(state); var replay = InventoryEngine.Apply(state, new SettleInventory("settle"));
        InventoryFixture.Check(replay.Accepted && replay.Replayed && JsonSerializer.Serialize(replay.State) == before, "Replay duplicated settlement.");
        InventoryFixture.Reject(state, new SettleInventory("bid"));
        var dying = InventoryFixture.Add(state, "armor");
        auction = InventoryFixture.Accept(ref state, new OfferItem("death-offer", 1, dying, 100, 4, true));
        InventoryFixture.Accept(ref state, new PlaceBid("death-bid", 2, auction, 200));
        InventoryFixture.Person(state, 2).Status = LifeStatus.Dead;
        InventoryFixture.Accept(ref state, new SettleInventory("death"));
        InventoryFixture.Check(InventoryFixture.Item(state, dying).Owner == 1 && InventoryFixture.Money(state) == total &&
            state.Ledger.Contracts.Single(c => c.Id == auction).Status == "cancelled", "Death failed to refund estate or release property.");
    }
}

public sealed class InventoryTransportScenario : IFeatureScenario
{
    public string Name => "V021-E03 real cargo preserves in-transit title, labor, fees, arrival and failure recovery location";
    public void Run()
    {
        var state = InventoryFixture.Create(); var cargo = InventoryFixture.Add(state, "food", 5); var total = InventoryFixture.Money(state);
        InventoryFixture.Reject(state, new BeginTransport("no-route", 1, cargo, 2, 2, 1, 2, 10, false, true));
        InventoryFixture.Reject(state, new BeginTransport("no-acceptance", 1, cargo, 2, 2, 1, 2, 10, true));
        var contract = InventoryFixture.Accept(ref state, new BeginTransport("start", 1, cargo, 2, 2, 1, 2, 10, true, true));
        InventoryFixture.Check(InventoryFixture.Item(state, cargo).Owner == 1 && state.Ledger.Metadata.Single(m => m.Item == cargo).Carrier == 2, "Cargo ownership changed before delivery.");
        InventoryFixture.Reject(state, new ConsumeItem("reserved-use", 1, cargo));
        InventoryFixture.Reject(state, new ReserveLabor("carrier-double", 2, "defense", 0, 2, 1));
        InventoryFixture.Reject(state, new FinishTransport("early", 2, contract, true));
        InventoryFixture.Person(state, 2).Place = 1; state.Month = 2;
        InventoryFixture.RoundTrip(ref state);
        InventoryFixture.Accept(ref state, new FinishTransport("arrive", 2, contract, true));
        InventoryFixture.Check(InventoryFixture.Item(state, cargo).Owner == 2 && InventoryFixture.Item(state, cargo).Place == 1 &&
            InventoryFixture.Money(state) == total, "Delivery did not settle real cargo and fee.");
        InventoryFixture.Reject(state, new FinishTransport("again", 2, contract, true));
        InventoryFixture.Person(state, 2).Place = 0; cargo = InventoryFixture.Add(state, "ore", 3);
        contract = InventoryFixture.Accept(ref state, new BeginTransport("second", 1, cargo, 2, 2, 1, 4, 10, true, true));
        InventoryFixture.Person(state, 2).Place = 2;
        InventoryFixture.Accept(ref state, new FinishTransport("failed", 1, contract, false));
        InventoryFixture.Check(InventoryFixture.Item(state, cargo).Owner == 1 && InventoryFixture.Item(state, cargo).Place == 2 &&
            InventoryFixture.Item(state, cargo).Reserved == 0 && InventoryFixture.Money(state) == total, "Failed transport teleported cargo or lost sender title/refund.");
    }
}

public sealed class InventoryArrayAndAdapterScenario : IFeatureScenario
{
    public string Name => "V021-K06 array rights, actual upkeep, effect expiry, and stale world candidate rejection";
    public void Run()
    {
        var state = InventoryFixture.Create(); var item = InventoryFixture.Add(state, "array"); InventoryFixture.Add(state, "resource.02.0");
        InventoryFixture.Reject(state, new InstallArray("no-right", 1, item, false));
        var array = InventoryFixture.Accept(ref state, new InstallArray("install", 1, item, true));
        InventoryFixture.Check(InventoryEngine.ActiveEffects(state, 0, "qi.place").Count == 0, "Unfunded array produced qi.");
        var job = InventoryFixture.Accept(ref state, new MaintainArray("upkeep", 1, array, 1));
        InventoryFixture.Reject(state, new DismantleArray("busy", 1, array));
        InventoryFixture.Accept(ref state, new BeginProduction("pay-fuel", 1, job)); state.Month = 1;
        InventoryFixture.Accept(ref state, new FinishProduction("paid", 1, job));
        var before = JsonSerializer.Serialize(state);
        InventoryFixture.Check(InventoryEngine.ActiveEffects(state, 0, "qi.place").Count == 1 && JsonSerializer.Serialize(state) == before, "Effect query generated state or duplicate contributions.");
        state.Month = 13; InventoryFixture.Accept(ref state, new SettleInventory("expire"));
        InventoryFixture.Check(InventoryEngine.ActiveEffects(state, 0, "qi.place").Count == 0, "Array kept supplying qi without upkeep.");
        InventoryFixture.Accept(ref state, new DismantleArray("remove", 1, array));
        InventoryFixture.Check(state.Items.Any(i => i.Id == item), "Dismantling deleted the actual array.");
        var world = GameWorld.Create(42).Checkpoint(); var candidate = InventoryState.FromWorld(world);
        var food = world.Items.First(i => i.Definition == "food");
        var result = InventoryEngine.Apply(candidate, new SplitInventoryItem("world-split", food.Owner, food.Id, 1));
        InventoryFixture.Check(result.Accepted, result.Message);
        var ledger = result.State.ApplyTo(world);
        InventoryFixture.Check(world.Items.Count == candidate.Items.Count + 1 && ledger.Receipts.ContainsKey("world-split"), "World adapter did not apply the accepted authority.");
        var rejected = false;
        try { result.State.ApplyTo(world); } catch (InvalidOperationException) { rejected = true; }
        InventoryFixture.Check(rejected, "Stale candidate overwrote newer world authority.");
        InventoryFixture.RoundTrip(ref state);
    }
}
