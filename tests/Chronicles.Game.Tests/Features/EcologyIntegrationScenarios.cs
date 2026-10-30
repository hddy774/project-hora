using System.Globalization;
using System.Text.Json;
using Chronicles.Game.Ecology;
using Chronicles.Game.Inventory;

namespace Chronicles.Game.Tests;

internal sealed class EcologyFixture
{
    private int command;
    public GameWorld World { get; set; }
    public EcologyState Data { get; private set; }
    public long First { get; }
    public long Second { get; }
    public EcologyFixture()
    {
        World = GameWorld.Create(42, new Dictionary<string, long> { ["birth.enabled"] = 0, ["ecology.disasterChance"] = 0,
            ["ecology.spoilage"] = 0, ["ecology.foodUse"] = 0, ["ecology.regrowth"] = 8 });
        var state = World.Checkpoint(); First = state.People[0].Id; Second = state.People[1].Id;
        state.People[1].Action = ActionKind.Wait; state.People[1].Due = 10000;
        World = GameWorld.Restore(state); Send("possess", First);
        Data = EcologyEngine.Initialize(Context());
    }
    public EcologyContext Context(EcologyRules? rules = null)
    {
        var s = World.Checkpoint();
        return new(s.Month, s.Revision, s.People, s.Groups, s.Places, s.Items, s.Inventory, s.Events,
            s.Laws["ecology.foodUse"], rules ?? new());
    }
    public void Send(string kind, long actor, long target = 0, long value = 0, string data = "")
    {
        var result = World.Execute(new($"ecology-functional:{++command}", actor, kind, target, value, data));
        Check(result.Accepted, kind + ": " + result.Message);
    }
    public void Sample(EcologyRules? rules = null)
    {
        var before = World.StateHash(); Data = EcologyEngine.Settle(Context(rules), Data);
        Check(World.StateHash() == before, "ecology projection cannot mint property, money, people or natural resources");
    }
    public long SplitFood(long owner, int quantity)
    {
        var original = World.Checkpoint().Items.First(i => i.Owner == owner && i.Definition == "food");
        Send("split", owner, original.Id, quantity);
        return World.Checkpoint().Items.Single(i => i.Owner == owner && i.Definition == "food" && i.Quantity == quantity).Id;
    }
    public void SaveDomain()
    { var state = World.Checkpoint(); state.Ecology = Data; World = GameWorld.Restore(state); }
    public static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}

public sealed class EcologyActualMarketScenario : IFeatureScenario
{
    public string Name => "V021-E02 ecology: actual offers and funded trades produce local price evidence without extra money or goods";
    public void Run()
    {
        var f = new EcologyFixture(); var item = f.SplitFood(f.First, 6);
        f.Send("offer", f.First, item, 12);
        var contract = f.World.Checkpoint().Inventory.Contracts.Single(c => c.Item == item).Id;
        var before = f.World.Checkpoint();
        f.Send("observe", 0); f.Send("trade", f.Second, contract);
        var paid = f.World.Checkpoint();
        EcologyFixture.Check(paid.People.Sum(p => p.Money) == before.People.Sum(p => p.Money) &&
            paid.Items.Sum(i => (long)i.Quantity) == before.Items.Sum(i => (long)i.Quantity), "the real trade alone transfers funded existing stock");
        f.Sample(); var trade = f.Data.Trades.Single(t => t.Contract == contract);
        EcologyFixture.Check(trade.Quantity == 6 && trade.Price == 12 && trade.Quality == 5000 && trade.Month == 0,
            "history uses inventory's typed actual paid quantity, price, quality and time");
        var price = f.Data.Markets.Single(m => m.Place == 0 && m.Definition == "food");
        EcologyFixture.Check(price.TradedQuantity == 6 && price.Turnover == 12 && price.PaidPerUnitMilli == 2000 &&
            price.Sources.Any(s => s.StartsWith("public-paid-contract:", StringComparison.Ordinal)), "regional prices cite actual counterparties' paid contract");
        var serialized = JsonSerializer.Serialize(f.Data); f.Sample();
        EcologyFixture.Check(JsonSerializer.Serialize(f.Data) == serialized, "sampling twice cannot duplicate paid demand or settlement");
        f.SaveDomain(); var loaded = GameCodec.Decode(GameCodec.Encode(f.World));
        EcologyFixture.Check(loaded.StateHash() == f.World.StateHash(), "prices, receipt cursors and market evidence survive real world save");
    }
}

public sealed class EcologyNaturalFlowScenario : IFeatureScenario
{
    public string Name => "V021-O01 ecology: actual extraction, finite depletion and renewable recovery reconcile to physical source receipts";
    public void Run()
    {
        var f = new EcologyFixture(); var initial = f.Context();
        var source = initial.Inventory.Sources.Single(s => s.Place == 0 && s.Definition == "food");
        f.Send("gather", f.First, data: "food"); var due = f.World.Checkpoint().People.Single(p => p.Id == f.First).Due;
        f.World.AdvanceTo(due); f.Sample();
        var used = f.Data.NaturalFlows.Single(s => s.Place == 0 && s.Month == due);
        EcologyFixture.Check(used.Extraction > 0 && used.ObservedAdjustment == 0 && used.NaturalSources.Contains(source.Id) &&
            used.Opening + used.Regeneration - used.Extraction - used.Loss == used.Closing, "finite real extraction has a closed source balance: " + JsonSerializer.Serialize(used));
        EcologyFixture.Check(f.Data.Production.Any(p => p.Person == f.First && p.Definition == "food" && p.Quantity == used.Extraction), "physical extraction output is linked to the real completed job");
        f.World.AdvanceTo(due + 1); f.Sample(); var recovered = f.Data.NaturalFlows.Single(s => s.Place == 0 && s.Month == due + 1);
        EcologyFixture.Check(recovered.Regeneration > 0 && recovered.ObservedAdjustment == 0 && recovered.Closing > recovered.Opening &&
            f.Data.Changes.Any(c => c.Place == 0 && c.Kind == EcologicalChangeKind.Recovery), "renewal is recorded only after inventory actually restores renewable stock");
        EcologyFixture.Check(f.World.Checkpoint().People.Count == 2 && f.World.Checkpoint().Groups.All(g => g.Kind != GroupKind.Sect), "ecology progression creates no settlers or sects");
    }
}

public sealed class EcologyNoScheduledSettlementScenario : IFeatureScenario
{
    public string Name => "V021-M02/O04 ecology: time and population alone do not unlock markets, workshops, settlements or capacity";
    public void Run()
    {
        var f = new EcologyFixture(); f.World.AdvanceTo(2); f.Sample();
        EcologyFixture.Check(f.Data.Facilities.Count == 0 && f.Data.Markets.All(m => m.AskPerUnitMilli is null && m.PaidPerUnitMilli is null),
            "without actual consumption, listings or work no institution or reference price is fabricated");
        var initialCapacity = f.World.Checkpoint().Inventory.BaseCarryCapacity;
        var item = f.SplitFood(f.First, 4); f.Send("offer", f.First, item, 4);
        f.World.AdvanceTo(3); f.Sample();
        var market = f.Data.Facilities.Single(p => p.Kind == EcologicalFacilityKind.Market && p.Active);
        EcologyFixture.Check(market.Operators.SequenceEqual([f.First]) && market.Sources.Any(s => s.StartsWith("public-contract:", StringComparison.Ordinal)) &&
            f.World.Checkpoint().Inventory.BaseCarryCapacity == initialCapacity && f.World.Checkpoint().Places.Count == 100,
            "a real open-air market records its actual operator and offer, granting no capacity or new strategic node");
        f.Send("withdraw", f.First, f.World.Checkpoint().Inventory.Contracts.Single(c => c.Item == item).Id);
        f.World.AdvanceTo(4); f.Sample();
        EcologyFixture.Check(!f.Data.Facilities.Single(p => p.Kind == EcologicalFacilityKind.Market).Active &&
            f.Data.Changes.Any(c => c.Kind == EcologicalChangeKind.FacilityClosed), "withdrawn physical activity closes the descriptive institution and preserves its history");
    }
}

public sealed class EcologyPersonalInformationScenario : IFeatureScenario
{
    public string Name => "V021-E02/O03 ecology: actor history and plans exclude private NPC wealth, stock, quality and source truth";
    public void Run()
    {
        var f = new EcologyFixture(); var original = f.Context(); var first = EcologyEngine.Settle(original, f.Data);
        var alteredPeople = JsonSerializer.Deserialize<List<Person>>(JsonSerializer.Serialize(original.People))!;
        alteredPeople.Single(p => p.Id == f.Second).Money += 100;
        var alteredItems = JsonSerializer.Deserialize<List<Item>>(JsonSerializer.Serialize(original.Items))!;
        alteredItems.First(i => i.Owner == f.Second && i.Definition == "food").Quantity += 100;
        var changed = original with { People = alteredPeople, Items = alteredItems };
        var second = EcologyEngine.Settle(changed, f.Data);
        EcologyFixture.Check(JsonSerializer.Serialize(EcologyEngine.View(original, first, f.First)) == JsonSerializer.Serialize(EcologyEngine.View(changed, second, f.First)),
            "counterfactual private NPC changes do not enter the actor's public/personal ecology view");
        var input = new EcologyDecisionInput(f.First, 5, 0, 10, 6, 2, [],
            [new(111, "sale", f.Second, 0, "food", "actual public food", 4, 8, 8, 5, "public-contract:111")], [],
            [new(9, 0, "food", 0, 4, "personal-source:9:4")], [new(1, 10, 3, true, false)], [], new());
        var plan = EcologyPlanner.Decide(input)!;
        EcologyFixture.Check(plan.Kind == "trade" && plan.Target == 111 && plan.Price <= input.Money, "known affordable local public offer supports a real purchase");
        var poor = EcologyPlanner.Decide(input with { Money = 0 })!;
        EcologyFixture.Check(poor.Kind == "travel" && poor.Target == 1 && poor.Evidence.Contains("observed:3", StringComparison.Ordinal),
            "insufficient own funds adapts using dated personal source observations, not live resource truth");
        EcologyFixture.Check(EcologyPlanner.Decide(input with { Money = 0, Places = [] }) is null, "no actual capacity produces a genuine failed option, not a hidden supplier");
        var low = input with { Stock = [new(12, "food", 20, 2000, 1, true, false, true)], Listings = [], Sources = [], Places = [], FoodNeed = 1 };
        var high = low with { Stock = [new(12, "food", 20, 8000, 1, true, false, true)] };
        EcologyFixture.Check(EcologyPlanner.Decide(high)!.Price > EcologyPlanner.Decide(low)!.Price, "own observed quality affects a real asking price without consulting unappraised NPC quality");
    }
}

public sealed class EcologyBoundedHistoryScenario : IFeatureScenario
{
    public string Name => "V021-O01/O05 ecology: bounded source history and pure culture-independent isolated queries preserve world authority";
    public void Run()
    {
        var f = new EcologyFixture(); var rules = new EcologyRules { HistoryMonths = 2, MaximumChanges = 8 };
        for (var month = 1; month <= 3; month++) { f.World.AdvanceTo(month); f.Sample(rules); }
        EcologyFixture.Check(f.Data.NaturalFlows.All(p => p.Month >= 2) && f.Data.Demand.All(p => p.Month >= 2) && f.Data.Changes.Count <= 8,
            "functional history retention has an explicit bound");
        var before = JsonSerializer.Serialize(f.Data); var view = EcologyEngine.View(f.Context(rules), f.Data, null);
        if (view.Regions.Count > 0 && view.Regions[0].Sources is string[] sources && sources.Length > 0) sources[0] = "external mutation";
        EcologyFixture.Check(JsonSerializer.Serialize(f.Data) == before, "observation arrays do not expose mutable saved authority");
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR"); var alternate = JsonSerializer.Serialize(EcologyEngine.View(f.Context(rules), f.Data, null));
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            EcologyFixture.Check(alternate == JsonSerializer.Serialize(EcologyEngine.View(f.Context(rules), f.Data, null)), "query culture cannot change economics or saved evidence");
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }
}
