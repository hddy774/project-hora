using System.Reflection;
using System.Runtime.ExceptionServices;
using Chronicles.Game.Cultivation;

namespace Chronicles.Game.Tests.Features;

// These scenarios invoke the real partial adapter hooks explicitly. Public dispatch/scheduler wiring is verified
// by the root integration workstream; an unhooked public command is never counted as an adapter success here.
internal static class CultivationAdapterFixture
{
    internal static object? Call(GameWorld world, string name, params object?[] values)
    {
        var method = typeof(GameWorld).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(m => m.Name == name && m.GetParameters().Length == values.Length);
        try { return method.Invoke(world, values); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    internal static GameState Live(GameWorld world) => (GameState)typeof(GameWorld)
        .GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
    internal static (GameWorld World, long Person) Create()
    {
        var world = GameWorld.Create(42, new Dictionary<string, long> { ["birth.enabled"] = 0,
            ["cultivation.breakthroughRisk"] = 0, ["ecology.disasterChance"] = 0, ["ecology.foodUse"] = 0 });
        Call(world, "InitializeInventory"); Call(world, "InitializePeopleDomain"); Call(world, "InitializeKnowledge");
        Call(world, "InitializeCultivation");
        return (world, Live(world).People[0].Id);
    }
    internal static Person Person(GameWorld world, long id) => Live(world).People.Single(p => p.Id == id);
    internal static void CurrentRealm(GameWorld world, long id, Realm realm, bool complete)
    {
        var p = Person(world, id); p.Realm = realm; p.Layer = complete ? 9 : 1; p.GreatPerfection = complete;
        p.Action = ActionKind.Idle; p.Due = 0; p.Irreversible = false; p.ActionTarget = 0; p.ActionData = "";
        p.Formations.Clear(); p.Work = 0; p.MartialWork = 0;
        var policy = (CultivationPolicy)Call(world, "CultivationPolicyForWorld")!;
        Live(world).Cultivation[p.BodyId] = CultivationFixture.State(new() { PersonId = p.Id, BodyId = p.BodyId,
            SoulId = p.SoulId, Realm = realm }, policy, complete);
    }
    internal static string? Command(GameWorld world, long person, string kind, string data = "", long target = 0)
    {
        var p = Person(world, person);
        object?[] args = [new GameCommand($"adapter:{Live(world).NextId}:{world.Month}:{kind}", person, kind, target, Data: data), p, null];
        CultivationFixture.Check((bool)Call(world, "TryCultivationCommand", args)!, "adapter recognizes cultivation command");
        return (string?)args[2];
    }
    internal static void Accepted(GameWorld world, long person, string kind, string data = "", long target = 0)
    {
        var error = Command(world, person, kind, data, target); CultivationFixture.Check(error is null, error ?? "adapter accepted");
    }
    internal static void Complete(GameWorld world, long person, long month)
    {
        Live(world).Month = month;
        var p = Person(world, person);
        CultivationFixture.Check(p.Action != ActionKind.Idle && p.Due <= month, "scheduled real action boundary reached");
        CultivationFixture.Check((bool)Call(world, "CompleteCultivation", p)!, "real cultivation completion handled");
    }
}

public sealed class CultivationWorldSourceScenario : IFeatureScenario
{
    public string Name => "INV-004/006/018 cultivation adapter·exact elemental source debit·real practice·query purity";
    public void Run()
    {
        var (world, id) = CultivationAdapterFixture.Create();
        CultivationAdapterFixture.CurrentRealm(world, id, Realm.Qi, false);
        var live = CultivationAdapterFixture.Live(world); var p = CultivationAdapterFixture.Person(world, id);
        var quantum = 5000 / live.Laws["cultivation.sourceCost"];
        var before = live.Places[p.Place].Qi * quantum + live.CultivationSupply.GetValueOrDefault(p.Place).Total;
        var roots = p.Roots.ToArray(); var aptitudes = p.Aptitudes.ToArray(); var rng = live.RandomState;
        var mastery = live.Knowledge.Mastery.Count;
        CultivationAdapterFixture.Accepted(world, id, "cultivate", "path.1.0");
        CultivationFixture.Check(live.Cultivation[p.BodyId].RootGrowth.Total == 0 && p.ActionTarget > 0, "scheduling is not completed growth or mastery");
        CultivationAdapterFixture.Complete(world, id, 1);
        live = CultivationAdapterFixture.Live(world); p = CultivationAdapterFixture.Person(world, id);
        var after = live.Places[p.Place].Qi * quantum + live.CultivationSupply.GetValueOrDefault(p.Place).Total;
        CultivationFixture.Check(before - after == p.Processes[0] && p.Processes[3] > 0 && live.Cultivation[p.BodyId].RootGrowth.Total == p.Processes[3], "physical unopened parcels plus opened elemental stock balance exact absorption");
        CultivationFixture.Check(p.Roots.SequenceEqual(roots) && p.Aptitudes.SequenceEqual(aptitudes) && live.RandomState == rng, "practice changes acquired progress without innate mutation or RNG");
        CultivationFixture.Check(live.Knowledge.Mastery.Count > mastery && live.Knowledge.Activities.Any(a =>
            a.PracticeOutcome?.Successful == true && a.ActorId == p.Id), "canonical mastery is supported by the performed monthly process");
        var hash = world.StateHash();
        var combat = (CombatProjection)CultivationAdapterFixture.Call(world, "CultivationCombatFor", p)!;
        CultivationAdapterFixture.Call(world, "CultivationInspectionFor", p);
        CultivationAdapterFixture.Call(world, "CultivationPotentialFor", p);
        CultivationFixture.Check(combat.Axes.OutputPenetration > 0 && world.StateHash() == hash, "adapter queries are pure");
        var restored = GameCodec.Decode(GameCodec.Encode(world));
        CultivationFixture.Check(restored.StateHash() == hash, "opened elemental source and acquired progress survive save");
    }
}

public sealed class CultivationWorldFractureScenario : IFeatureScenario
{
    public string Name => "INV-004/006/013/023 cultivation adapter·scheduled fracture·restore·same life death authority";
    public void Run()
    {
        var (world, id) = CultivationAdapterFixture.Create();
        CultivationAdapterFixture.CurrentRealm(world, id, Realm.GoldenCore, true);
        CultivationAdapterFixture.Accepted(world, id, "breakthrough", "path.4.0");
        var p = CultivationAdapterFixture.Person(world, id); var live = CultivationAdapterFixture.Live(world);
        CultivationFixture.Check(live.Cultivation[p.BodyId].Attempt!.Phase == AttemptPhase.Prepared && !p.Irreversible, "prepare is reversible and scheduled");
        CultivationAdapterFixture.Complete(world, id, 1);
        CultivationFixture.Check(live.Cultivation[p.BodyId].Attempt!.Phase == AttemptPhase.Started && !p.Irreversible, "start precedes actual fracture");
        CultivationAdapterFixture.Complete(world, id, 2);
        CultivationFixture.Check(live.Cultivation[p.BodyId].Attempt!.Phase == AttemptPhase.Fractured && p.Irreversible && p.Realm == Realm.GoldenCore, "actual fracture boundary keeps pre-cause realm evidence");
        var restored = GameCodec.Decode(GameCodec.Encode(world));
        CultivationFixture.Check(restored.StateHash() == world.StateHash(), "pause/save does not abandon formation");
        CultivationAdapterFixture.Accepted(restored, id, "cancel");
        live = CultivationAdapterFixture.Live(restored); p = CultivationAdapterFixture.Person(restored, id);
        CultivationFixture.Check(p.Status == LifeStatus.Dead && live.DeathCount == 1 && live.Souls.Single(s => s.Id == p.SoulId).Route == 0,
            "actual post-fracture abandonment uses shared EndLife once, below Spirit does not auto-reincarnate");
        CultivationFixture.Check(live.Cultivation[p.BodyId].Attempt!.Phase == AttemptPhase.Failed, "irreversible metadata is failed, never recovered by cancellation");
    }
}

public sealed class CultivationWorldBoundaryScenario : IFeatureScenario
{
    public string Name => "INV-004/008/018/021 cultivation adapter·real rights·no phantom knowledge·common activity capacity";
    public void Run()
    {
        var (world, id) = CultivationAdapterFixture.Create();
        var p = CultivationAdapterFixture.Person(world, id); var live = CultivationAdapterFixture.Live(world);
        CultivationFixture.Check(live.Cultivation[p.BodyId].Formations.Count == 1 && p.Layer == 5 && !p.GreatPerfection && p.Skills.Count == 0, "founder is one measured genesis structure without fabricated past mastery");
        CultivationAdapterFixture.CurrentRealm(world, id, Realm.Qi, false);
        var other = live.People.Single(q => q.Id != id);
        live.Places[p.Place].Owner = other.Id;
        var before = world.StateHash();
        CultivationFixture.Check(CultivationAdapterFixture.Command(world, id, "cultivate", "path.1.0") is not null && world.StateHash() == before, "private site ownership is an actual access boundary");
        live.Places[p.Place].Owner = 0;
        live.Inventory.Labor.Add(new() { Id = live.NextId++, Person = id, Purpose = "actual transport reservation", Start = 0, End = 2, Units = 100 });
        before = world.StateHash();
        CultivationFixture.Check(CultivationAdapterFixture.Command(world, id, "martial") is not null && world.StateHash() == before, "root-free martial still shares actual monthly activity capacity");
        live.Inventory.Labor[^1].Active = false;
        var created = world.Execute(new("adapter.explicit-arrival", 0, "create", Target: 0, Data: "새 수행자"));
        CultivationFixture.Check(created.Accepted, created.Message);
        live = CultivationAdapterFixture.Live(world); var newcomer = live.People[^1];
        CultivationAdapterFixture.Call(world, "InitializePeoplePerson", newcomer, false);
        CultivationAdapterFixture.Call(world, "InitializeCultivationPerson", newcomer, false);
        var book = live.Items.First(i => i.Definition == "path.1.0");
        CultivationAdapterFixture.Call(world, "Transfer", book, newcomer.Id);
        CultivationAdapterFixture.Call(world, "SettleKnowledge");
        // Transfer changes the real physical book's owner; it does not create a personal memory record.
        var cultivationRevision = live.Cultivation[newcomer.BodyId].Revision;
        CultivationFixture.Check(CultivationAdapterFixture.Command(world, newcomer.Id, "cultivate", "path.1.0") is not null &&
            live.Cultivation[newcomer.BodyId].Revision == cultivationRevision, "book possession cannot manufacture an executable learned path");
    }
}

public sealed class CultivationWorldAidScenario : IFeatureScenario
{
    public string Name => "INV-004/016/017/018 cultivation adapter·one physical pill split/reservation/consume/cancel";
    public void Run()
    {
        var (world, id) = CultivationAdapterFixture.Create();
        CultivationAdapterFixture.CurrentRealm(world, id, Realm.Qi, true);
        var pill = (Item)CultivationAdapterFixture.Call(world, "AddItem", id, "pill", 3, 5000,
            "수용시험 물품 생산 fixture", "production")!;
        var live = CultivationAdapterFixture.Live(world); var p = CultivationAdapterFixture.Person(world, id);
        var opening = live.Materials["pill"].Consumption;
        CultivationAdapterFixture.Accepted(world, id, "breakthrough", "path.2.0", pill.Id);
        live = CultivationAdapterFixture.Live(world); p = CultivationAdapterFixture.Person(world, id);
        var attempt = live.Cultivation[p.BodyId].Attempt!; var physical = attempt.ReservedItems.Single();
        CultivationFixture.Check(physical != pill.Id && live.Items.Single(i => i.Id == physical).Quantity == 1 &&
            live.Items.Single(i => i.Id == physical).Reserved > 0 && live.Inventory.Reservations.Any(r => r.Active && r.Items.Contains(physical)), "one exact split physical item is backed by shared reservation");
        CultivationAdapterFixture.Accepted(world, id, "cancel");
        live = CultivationAdapterFixture.Live(world);
        CultivationFixture.Check(live.Items.Where(i => i.Definition == "pill").Sum(i => i.Quantity) == 3 &&
            live.Materials["pill"].Consumption == opening && live.Items.All(i => i.Reserved == 0), "preparation cancellation releases real pill without consuming it");
        CultivationAdapterFixture.Accepted(world, id, "breakthrough", "path.2.0", physical);
        CultivationAdapterFixture.Complete(world, id, 1);
        live = CultivationAdapterFixture.Live(world);
        CultivationFixture.Check(live.Items.Where(i => i.Definition == "pill").Sum(i => i.Quantity) == 2 &&
            live.Materials["pill"].Consumption == opening + 1 && live.Inventory.Reservations.All(r => !r.Active), "start consumes exactly one reserved pill and closes common reservation");
    }
}
