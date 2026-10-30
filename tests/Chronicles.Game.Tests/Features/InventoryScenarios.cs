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
    public string Name => "inventory: INV-016 inventory split/merge preserves hidden state, binding and provenance";
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
    public string Name => "inventory: INV-016 ownership, custody, use, equipment and remote access remain independent";
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
    public string Name => "inventory: INV-016 recursive container capacity, cycle rejection and broken-container overflow preserve stock";
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
    public string Name => "inventory: INV-005/015/016 equipment replacement is atomic across occupied slots and return capacity";
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
    public string Name => "inventory: INV-017 care, teaching, production and defense share person and facility labor";
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
    public string Name => "inventory: INV-017 real production inputs, stage cancellation, failure, use and save settlement";
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
    public string Name => "inventory: V021-I01/K03-K08 all 64 resources have executable producers and consumers";
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
    public string Name => "inventory: INV-017/018 auctions fund deposits, reject reuse, refund death and settle equal deadlines once";
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
        var settled = state.Ledger.Contracts.Single(c => c.Id == auction);
        InventoryFixture.Check(settled.PaidAmount == 700 && settled.PaidQuantity == 1 && settled.PaidQuality == 5000 && settled.EndedMonth == 2,
            "Actual auction payment was lost when escrow cleared.");
        InventoryFixture.Item(state, first).Quality = 1234;
        InventoryFixture.Check(settled.PaidQuality == 5000, "Later buyer inventory rewrote sale-time quality.");
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
    public string Name => "inventory: V021-E03 real cargo preserves in-transit title, labor, fees, arrival and failure recovery location";
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
    public string Name => "inventory: V021-K06 array rights, actual upkeep, effect expiry, and stale world candidate rejection";
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

public sealed class InventoryProfessionalScenario : IFeatureScenario
{
    public string Name => "inventory: V021-K07 real medicine, appraisal and exploration consume labor and produce personal evidence";
    public void Run()
    {
        var state = InventoryFixture.Create(); InventoryFixture.Add(state, "herb", 2); InventoryFixture.Person(state, 2).Health = 40;
        InventoryFixture.Reject(state, new TreatPerson("no-consent", 1, 2, 1));
        var job = InventoryFixture.Accept(ref state, new TreatPerson("treatment", 1, 2, 1, true));
        InventoryFixture.Check(InventoryFixture.Person(state, 2).Health == 40, "Reserved treatment applied before work.");
        InventoryFixture.Accept(ref state, new BeginProduction("treat-begin", 1, job)); state.Month = 1;
        InventoryFixture.Accept(ref state, new FinishProduction("treat-finish", 1, job));
        InventoryFixture.Check(InventoryFixture.Person(state, 2).Health > 40 && state.Materials["herb"].Consumption == 2, "Medicine did not use real herbs.");
        var tool = InventoryFixture.Add(state, "tool", quality: 5678, defects: ItemDefect.Contamination | ItemDefect.MissingPages);
        job = InventoryFixture.Accept(ref state, new AppraisePhysical("appraise", 1, tool, 2));
        InventoryFixture.Accept(ref state, new BeginProduction("appraise-begin", 1, job)); state.Month = 2;
        InventoryFixture.Accept(ref state, new FinishProduction("appraise-finish", 1, job));
        var appraisal = state.Ledger.Appraisals.Single();
        InventoryFixture.Check(appraisal.Appraiser == 1 && appraisal.EstimatedQuality == 5000 && appraisal.DetectedDefects == ItemDefect.MissingPages,
            "Unskilled appraisal revealed perfect hidden state.");
        var sourceId = state.NextId++;
        state.Ledger.Sources.Add(new() { Id = sourceId, Definition = "resource.00.0", Activity = "source.mining", Place = 0,
            Capacity = 20, Remaining = 12, Origin = "observed finite mine" }); InventoryFixture.Add(state, "food");
        job = InventoryFixture.Accept(ref state, new ExploreNaturalSource("explore", 1, sourceId, 3));
        InventoryFixture.Accept(ref state, new BeginProduction("explore-begin", 1, job)); state.Month = 3;
        InventoryFixture.Accept(ref state, new FinishProduction("explore-finish", 1, job));
        InventoryFixture.Check(state.Ledger.Observations.Single().Remaining == 12 && state.Ledger.Sources.Single().Remaining == 12 &&
            state.Materials["food"].Consumption == 1, "Exploration created resources or lacked a real activity cost.");
        InventoryFixture.RoundTrip(ref state);
    }
}

public sealed class InventoryPreservationScenario : IFeatureScenario
{
    public string Name => "inventory: INV-016/018 backpack removal preserves overflow, sources stay finite, known bundles reveal no truth";
    public void Run()
    {
        var state = InventoryFixture.Create(); state.Ledger.BaseCarryCapacity = 4;
        var bag = InventoryFixture.Add(state, "bag"); var ore = InventoryFixture.Add(state, "ore", 7);
        state.Ledger.Metadata.Add(new() { Item = bag, Custodian = 1, Capacity = 10 });
        InventoryFixture.Accept(ref state, new EquipItem("bag", 1, bag));
        InventoryFixture.Accept(ref state, new StoreItem("carry", 1, ore));
        InventoryFixture.Reject(state, new UnequipItem("no-room", 1, bag));
        InventoryFixture.Accept(ref state, new UnequipItem("ground-overflow", 1, bag, ReturnToGround: true));
        InventoryFixture.Check(InventoryFixture.Item(state, ore).Quantity == 7 && InventoryFixture.Item(state, ore).Place == 0, "Removing storage deleted overflow.");
        var sourceId = state.NextId++;
        state.Ledger.Sources.Add(new() { Id = sourceId, Definition = "resource.00.0", Activity = "source.mining", Place = 0,
            Capacity = 5, Remaining = 3, Regeneration = 0, Origin = "finite mine" });
        state.Month = 10; state.Ledger.Sources.Single().Capacity = 100;
        InventoryFixture.Accept(ref state, new SettleInventory("no-free-stock"));
        InventoryFixture.Check(state.Ledger.Sources.Single().Remaining == 3, "Capacity or zero regeneration minted resources.");
        var herbA = InventoryFixture.Add(state, "herb", 2, quality: 1000); var herbB = InventoryFixture.Add(state, "herb", 3, quality: 9000, defects: ItemDefect.Contamination);
        var before = JsonSerializer.Serialize(state);
        var bundles = InventoryKnowledgeQueries.GroupKnownItems([new(herbA, "known herb", 2, null, null, 1), new(herbB, "known herb", 3, null, null, 2)]);
        InventoryFixture.Check(bundles is [{ ObservedQuantity: 5, KnownQuality: null, KnownDefects: null }] && JsonSerializer.Serialize(state) == before,
            "Display grouping inferred hidden quality or merged real stacks.");
        InventoryFixture.RoundTrip(ref state);
    }
}

public sealed class InventoryCompetingAndDeathScenario : IFeatureScenario
{
    public string Name => "inventory: INV-017/018 competing bids and carrier/seller deaths preserve estates, escrow and cargo";
    public void Run()
    {
        var state = InventoryFixture.Create(); var third = state.NextId++;
        state.People.Add(new() { Id = third, SoulId = 99, Place = 0, Money = 1000 });
        var item = InventoryFixture.Add(state, "armor"); var money = InventoryFixture.Money(state);
        var auction = InventoryFixture.Accept(ref state, new OfferItem("offer", 1, item, 100, 2, true));
        InventoryFixture.Accept(ref state, new PlaceBid("first", 2, auction, 400));
        InventoryFixture.Accept(ref state, new PlaceBid("outbid", third, auction, 500));
        InventoryFixture.Check(InventoryFixture.Person(state, 2).Money == 1000 && InventoryFixture.Person(state, third).Money == 500,
            "Losing bidder was not refunded before replacement deposit.");
        InventoryFixture.Person(state, 1).Status = LifeStatus.Dead;
        InventoryFixture.Accept(ref state, new SettleInventory("seller-death"));
        InventoryFixture.Check(InventoryFixture.Person(state, third).Money == 1000 && InventoryFixture.Item(state, item).Owner == 1 &&
            InventoryFixture.Money(state) == money, "Seller death lost estate property or bidder deposit.");
        item = InventoryFixture.Add(state, "ore", 3, owner: 2);
        var transport = InventoryFixture.Accept(ref state, new BeginTransport("cargo", 2, item, third, 2, 1, 2, 10, true, true));
        InventoryFixture.Person(state, third).Place = 3; InventoryFixture.Person(state, third).Status = LifeStatus.Dead;
        InventoryFixture.Accept(ref state, new SettleInventory("carrier-death"));
        InventoryFixture.Check(InventoryFixture.Item(state, item).Owner == 2 && InventoryFixture.Item(state, item).Place == 3 &&
            state.Ledger.Contracts.Single(c => c.Id == transport).Status == "failed" && InventoryFixture.Money(state) == money,
            "Carrier death teleported cargo or consumed escrow.");
    }
}

public sealed class InventoryEquipmentDefinitionsScenario : IFeatureScenario
{
    public string Name => "inventory: V021-I03 all 12 equipment categories occupy slots and multi-slot contributions apply once";
    public void Run()
    {
        var state = InventoryFixture.Create();
        for (var slot = 0; slot < 12; slot++)
        {
            var item = InventoryFixture.Add(state, "equipment." + slot.ToString(System.Globalization.CultureInfo.InvariantCulture));
            InventoryFixture.Accept(ref state, new EquipItem("slot:" + slot.ToString(System.Globalization.CultureInfo.InvariantCulture), 1, item));
        }
        InventoryFixture.Check(InventoryFixture.Person(state, 1).Equipment.Count == 12 && InventoryEngine.EquipmentContributions(state, 1).Count == 12,
            "Equipment categories have missing occupancy.");
        var great = InventoryFixture.Add(state, "greatWeapon");
        InventoryFixture.Accept(ref state, new EquipItem("two-hands", 1, great, ReturnToGround: true));
        InventoryFixture.Check(InventoryEngine.EquipmentContributions(state, 1).Count(e => e.Item == great) == 1 &&
            InventoryEngine.EquipmentContributions(state, 1).Single(e => e.Item == great).Slots.Count == 2, "Multi-slot equipment contributed twice.");
        InventoryFixture.RoundTrip(ref state);
    }
}

public sealed class InventoryTimingAndArrayDamageScenario : IFeatureScenario
{
    public string Name => "inventory: INV-017 production cannot bank idle time and broken arrays immediately stop contributing";
    public void Run()
    {
        var state = InventoryFixture.Create(); InventoryFixture.Add(state, "herb", 3); InventoryFixture.Add(state, "wood");
        InventoryFixture.Reject(state, new StartProduction("too-little-work", 1, "pill", 1, 1));
        var job = InventoryFixture.Accept(ref state, new StartProduction("long-work", 1, "pill", 2, 50));
        state.Month = 1; InventoryFixture.Reject(state, new BeginProduction("late-begin", 1, job));
        InventoryFixture.Accept(ref state, new CancelProduction("release-late", 1, job));
        var item = InventoryFixture.Add(state, "array"); InventoryFixture.Add(state, "resource.02.0");
        var array = InventoryFixture.Accept(ref state, new InstallArray("install", 1, item, true));
        job = InventoryFixture.Accept(ref state, new MaintainArray("fuel", 1, array, 2));
        InventoryFixture.Accept(ref state, new BeginProduction("fuel-begin", 1, job)); state.Month = 2;
        InventoryFixture.Accept(ref state, new FinishProduction("fuel-finish", 1, job));
        InventoryFixture.Check(InventoryEngine.ActiveEffects(state, 0, "qi.place").Count == 1, "Paid array failed to activate.");
        InventoryFixture.Accept(ref state, new DamageItem("break-array", 1, item, 100));
        InventoryFixture.Check(InventoryEngine.ActiveEffects(state, 0, "qi.place").Count == 0 && !state.Ledger.Arrays.Single().Active,
            "Destroyed array kept its live contribution until a later settlement.");
        InventoryFixture.Add(state, "resource.02.0");
        InventoryFixture.Reject(state, new MaintainArray("cannot-fuel-broken-frame", 1, array, 3));
    }
}


public sealed class InventoryOrdinalSaveScenario : IFeatureScenario
{
    public string Name => "inventory: INV-004/006 inventory candidate copying preserves ordinal knowledge and receipt identity";
    public void Run()
    {
        var state = InventoryFixture.Create(); var item = InventoryFixture.Add(state, "food", 2);
        InventoryFixture.Person(state, 1).Skills["é"] = 2;
        InventoryFixture.Person(state, 1).Skills["e\u0301"] = 3;
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("fr-FR");
            InventoryFixture.Accept(ref state, new SplitInventoryItem("é", 1, item, 1));
            InventoryFixture.Accept(ref state, new MergeStacks("e\u0301", 1, item, state.Items.Single(i => i.Id != item).Id));
            InventoryFixture.Check(state.Ledger.Receipts.Count == 2 && InventoryFixture.Person(state, 1).Skills.Count == 2,
                "Culture collapsed distinct stable keys during candidate copying.");
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }
}

public sealed class InventorySharedReservationScenario : IFeatureScenario
{
    public string Name => "inventory: INV-016/017 shared physical reservations preserve split provenance and exclude parallel use";
    public void Run()
    {
        var state = InventoryFixture.Create(); var paper = InventoryFixture.Add(state, "paper", 3, quality: 4321, defects: ItemDefect.Contamination);
        var reservation = InventoryFixture.Accept(ref state, new ReserveInventoryPhysical("reserve-blank", 1, "transcription", paper, 1));
        var physical = state.Ledger.Reservations.Single(r => r.Id == reservation).Items.Single();
        InventoryFixture.Check(InventoryFixture.Item(state, physical).Quantity == 1 && InventoryFixture.Item(state, physical).Quality == 4321 &&
            InventoryFixture.Item(state, physical).Defects == ItemDefect.Contamination && InventoryFixture.Item(state, physical).Source == "fixture origin", "Shared physical reservation lost real substrate state.");
        InventoryFixture.Reject(state, new ConsumeItem("cannot-reuse", 1, physical));
        InventoryFixture.Accept(ref state, new ReleaseInventoryAssets("release", 1, reservation));
        reservation = InventoryFixture.Accept(ref state, new ReserveInventoryMaterials("reserve-cost", 1, "knowledge-cost",
            new Dictionary<string, int>(StringComparer.Ordinal) { ["paper"] = 2 }));
        InventoryFixture.Accept(ref state, new ConsumeInventoryAssets("pay", 1, reservation));
        InventoryFixture.Check(state.Items.Where(i => i.Definition == "paper").Sum(i => i.Quantity) == 1 && state.Materials["paper"].Consumption == 2,
            "Shared cost consumption duplicated or refunded physical stock.");
        InventoryFixture.RoundTrip(ref state);
        var world = GameWorld.Create(42).Checkpoint(); var originalPerson = world.People[0]; var candidate = InventoryState.FromWorld(world);
        var food = world.Items.First(i => i.Owner == originalPerson.Id && i.Definition == "food");
        var result = InventoryEngine.Apply(candidate, new SplitInventoryItem("identity", originalPerson.Id, food.Id, 1));
        InventoryFixture.Check(result.Accepted, result.Message); result.State.ApplyTo(world);
        InventoryFixture.Check(ReferenceEquals(originalPerson, world.People[0]), "Applying accepted inventory invalidated the world's live actor reference.");
    }
}

public sealed class InventoryKnownSourceAndOfferScenario : IFeatureScenario
{
    public string Name => "inventory: known source and local offer projections never infer mutable private truth";
    public void Run()
    {
        var state = InventoryFixture.Create(); state.Month = 2;
        var source = state.NextId++; var legacy = state.NextId++;
        state.Ledger.Sources.Add(new() { Id = source, Place = 0, Definition = "resource.00.0", Activity = "source.mining", Remaining = 100, Capacity = 100, Origin = "changed actual source" });
        state.Ledger.Observations.Add(new(1, source, 0, 12, 1, "actual past exploration", "resource.03.0", "source.gathering"));
        state.Ledger.Observations.Add(new(2, source, 0, 100, 2, "other person's private exploration", "resource.00.0", "source.mining"));
        state.Ledger.Observations.Add(new(1, legacy, 0, 5, 2, "legacy incomplete snapshot"));
        var known = InventoryKnowledgeQueries.KnownSources(state.Ledger, 1);
        InventoryFixture.Check(known is [{ Definition: "resource.03.0", Remaining: 12, Month: 1 }], "Known source projection used another observer or current source truth.");
        var item = InventoryFixture.Add(state, "tool", quality: 1111);
        InventoryFixture.Accept(ref state, new OfferItem("advertise", 1, item, 5, 10));
        var beforeState = JsonSerializer.Serialize(state); var beforeView = JsonSerializer.Serialize(InventoryKnowledgeQueries.LocalOffers(state, 2));
        InventoryFixture.Check(InventoryKnowledgeQueries.LocalOffers(state, 2) is [{ Definition: "tool", Quantity: 1, Price: 5, OwnDeposit: 0 }], "Public advertisement did not retain its actual listing snapshot.");
        InventoryFixture.Check(JsonSerializer.Serialize(state) == beforeState, "Known projections changed authority.");
        InventoryFixture.Item(state, item).Quality = 9999; InventoryFixture.Item(state, item).Source = "unobserved private history";
        state.Ledger.Sources.Single().Remaining = 1;
        InventoryFixture.Check(beforeView == JsonSerializer.Serialize(InventoryKnowledgeQueries.LocalOffers(state, 2)) &&
            InventoryKnowledgeQueries.KnownSources(state.Ledger, 1).Single().Remaining == 12, "Private item/source changes leaked into authorized cached facts.");
        InventoryFixture.Person(state, 2).Place = 1;
        InventoryFixture.Check(InventoryKnowledgeQueries.LocalOffers(state, 2).Count == 0, "A remote market leaked into a local-only projection.");
    }
}
