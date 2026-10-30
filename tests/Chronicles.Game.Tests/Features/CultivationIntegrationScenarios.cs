using System.Globalization;
using System.Text.Json;
using Chronicles.Game.Cultivation;
using Chronicles.Game.Knowledge;

namespace Chronicles.Game.Tests.Features;

/// <summary>Only public world commands, candidate snapshots, time and save APIs are used.</summary>
internal static class CultivationAdapterFixture
{
    private static int sequence;
    internal static (GameWorld World, long Person) Create()
    {
        var world = GameWorld.Create(42, new Dictionary<string, long> { ["birth.enabled"] = 0,
            ["cultivation.breakthroughRisk"] = 0, ["ecology.disasterChance"] = 0, ["ecology.foodUse"] = 0 });
        return (world, world.Checkpoint().People[0].Id);
    }
    internal static Person Person(GameWorld world, long id) => world.Checkpoint().People.Single(p => p.Id == id);
    internal static GameWorld CurrentRealm(GameWorld world, long id, Realm realm, bool complete)
    {
        var state = world.Checkpoint(); var person = state.People.Single(p => p.Id == id);
        WorldScenarioFixtures.SetCultivation(state, person, realm, complete ? 9 : 1, complete);
        person.Action = ActionKind.Idle; person.Due = 0; person.ActionTarget = 0; person.ActionData = "";
        return GameWorld.Restore(state);
    }
    internal static GameCommand Command(long person, string kind, string data = "", long target = 0) =>
        new("public.cultivation." + (++sequence).ToString(CultureInfo.InvariantCulture), person, kind, target, Data: data);
    internal static void Accepted(GameWorld world, long person, string kind, string data = "", long target = 0)
    {
        var result = world.Execute(Command(person, kind, data, target)); CultivationFixture.Check(result.Accepted, result.Message);
    }
    internal static (GameWorld World, long Pill) Pill(GameWorld world, long person)
    {
        var state = world.Checkpoint(); var id = state.NextId++;
        state.Items.Add(new() { Id = id, Owner = person, Definition = "pill", Quantity = 3, Quality = 5000,
            Place = -1, Source = "기능 검사의 명시 생산 초기조건·실제 품목 원장" });
        if (!state.Materials.TryGetValue("pill", out var material)) state.Materials.Add("pill", material = new());
        material.Production += 3;
        return (GameWorld.Restore(state), id);
    }
}

public sealed class CultivationWorldSourceScenario : IFeatureScenario
{
    public string Name => "INV-004/006/018 public cultivation·actual time/source debit/mastery/save/query purity";
    public void Run()
    {
        var (world, id) = CultivationAdapterFixture.Create();
        world = CultivationAdapterFixture.CurrentRealm(world, id, Realm.Qi, false);
        CultivationAdapterFixture.Accepted(world, id, "possess");
        var state = world.Checkpoint(); var person = state.People.Single(p => p.Id == id);
        var quantum = 5000 / state.Laws["cultivation.sourceCost"];
        var before = state.Places[person.Place].Qi * quantum + state.CultivationSupply.GetValueOrDefault(person.Place).Total;
        var innate = person.Roots.ToArray(); var mastery = state.Knowledge.Mastery.Count;
        CultivationAdapterFixture.Accepted(world, id, "cultivate", "path.1.0");
        person = CultivationAdapterFixture.Person(world, id);
        CultivationFixture.Check(world.Checkpoint().Cultivation[person.BodyId].RootGrowth.Total == 0 && person.ActionTarget > 0,
            "public scheduling is not completed work or mastery");
        var restored = GameCodec.Decode(GameCodec.Encode(world));
        world.AdvanceTo(1); restored.AdvanceTo(1);
        CultivationFixture.Check(world.StateHash() == restored.StateHash(), "saved pending action continues deterministically through actual scheduler");
        state = world.Checkpoint(); person = state.People.Single(p => p.Id == id);
        var after = state.Places[person.Place].Qi * quantum + state.CultivationSupply.GetValueOrDefault(person.Place).Total;
        CultivationFixture.Check(before - after == person.Processes[0] && person.Processes[3] > 0 &&
            state.Cultivation[person.BodyId].RootGrowth.Total == person.Processes[3], "real elemental parcels plus opened stock balance exact absorption");
        CultivationFixture.Check(person.Roots.SequenceEqual(innate) && state.Knowledge.Mastery.Count > mastery &&
            state.Knowledge.Activities.Any(a => a.PracticeOutcome?.Successful == true && a.ActorId == person.Id), "performed public action earns real practice without innate mutation");
        CultivationFixture.Check(person.Action == ActionKind.Idle, "an ordinary player month completes once instead of silently repeating");
        var hash = world.StateHash(); world.View(id); world.Checkpoint();
        CultivationFixture.Check(world.StateHash() == hash && GameCodec.Decode(GameCodec.Encode(world)).StateHash() == hash,
            "public observation and save are pure");
    }
}

public sealed class CultivationWorldFractureScenario : IFeatureScenario
{
    public string Name => "INV-004/006/013/023 public scheduled fracture·restore·same life death authority";
    public void Run()
    {
        var (world, id) = CultivationAdapterFixture.Create();
        world = CultivationAdapterFixture.CurrentRealm(world, id, Realm.GoldenCore, true);
        CultivationAdapterFixture.Accepted(world, id, "possess");
        CultivationAdapterFixture.Accepted(world, id, "breakthrough", "path.4.0");
        var person = CultivationAdapterFixture.Person(world, id);
        CultivationFixture.Check(world.Checkpoint().Cultivation[person.BodyId].Attempt!.Phase == AttemptPhase.Prepared && !person.Irreversible,
            "public preparation is reversible");
        world.AdvanceTo(1); person = CultivationAdapterFixture.Person(world, id);
        CultivationFixture.Check(world.Checkpoint().Cultivation[person.BodyId].Attempt!.Phase == AttemptPhase.Started && !person.Irreversible,
            "one real scheduled month reaches start before fracture");
        world.AdvanceTo(2); person = CultivationAdapterFixture.Person(world, id);
        CultivationFixture.Check(world.Checkpoint().Cultivation[person.BodyId].Attempt!.Phase == AttemptPhase.Fractured && person.Irreversible,
            "second actual boundary fractures while retaining Golden Core identity");
        var restored = GameCodec.Decode(GameCodec.Encode(world));
        CultivationFixture.Check(restored.StateHash() == world.StateHash(), "save/pause is not abandonment");
        var cancellation = CultivationAdapterFixture.Command(id, "cancel");
        CultivationFixture.Check(restored.Execute(cancellation).Accepted, "actual cancellation accepted");
        var after = restored.StateHash(); CultivationFixture.Check(restored.Execute(cancellation).Accepted && restored.StateHash() == after,
            "replayed public cancellation records no second death");
        var state = restored.Checkpoint(); person = state.People.Single(p => p.Id == id);
        CultivationFixture.Check(person.Status == LifeStatus.Dead && state.DeathCount == 1 &&
            state.Souls.Single(s => s.Id == person.SoulId).Route == 0 && state.Cultivation[person.BodyId].Attempt!.Phase == AttemptPhase.Failed,
            "shared life authority handles fatal abandonment once without below-Spirit auto-reincarnation");
    }
}

public sealed class CultivationWorldBoundaryScenario : IFeatureScenario
{
    public string Name => "INV-004/008/018/021 public cultivation rights·book ownership·common activity capacity";
    public void Run()
    {
        var (world, id) = CultivationAdapterFixture.Create();
        var person = CultivationAdapterFixture.Person(world, id);
        CultivationFixture.Check(world.Checkpoint().Cultivation[person.BodyId].Formations.Count == 1 &&
            person.Layer == 5 && !person.GreatPerfection && person.Skills.Count == 0, "public genesis declares one measured structure without past mastery");
        world = CultivationAdapterFixture.CurrentRealm(world, id, Realm.Qi, false);
        var state = world.Checkpoint(); person = state.People.Single(p => p.Id == id);
        state.Places[person.Place].Owner = state.People.Single(p => p.Id != id).Id;
        world = GameWorld.Restore(state); var before = world.StateHash();
        CultivationFixture.Check(!world.Execute(CultivationAdapterFixture.Command(id, "cultivate", "path.1.0")).Accepted && world.StateHash() == before,
            "public command rechecks private-site use rights atomically");
        state = world.Checkpoint(); state.Places[person.Place].Owner = 0;
        state.Inventory.Labor.Add(new() { Id = state.NextId++, Person = id, Purpose = "declared functional transport reservation", Start = 0, End = 2, Units = 100 });
        world = GameWorld.Restore(state); before = world.StateHash();
        CultivationFixture.Check(!world.Execute(CultivationAdapterFixture.Command(id, "martial")).Accepted && world.StateHash() == before,
            "root-free martial still shares real reserved monthly capacity");
        state = world.Checkpoint(); state.Inventory.Labor[^1].Active = false; world = GameWorld.Restore(state);
        CultivationAdapterFixture.Accepted(world, 0, "create", "새 수행자");
        state = world.Checkpoint(); var newcomer = state.People[^1];
        var book = state.Items.First(i => i.Definition == "path.1.0"); book.Owner = newcomer.Id;
        var authority = KnowledgeAuthority.Restore(state.Knowledge); var medium = state.Knowledge.Media.Single(m => m.ItemId == book.Id);
        var moved = authority.SynchronizeMedium(book.Id, newcomer.Id, newcomer.Place, medium.Quality, medium.MissingChapters,
            medium.Unreadable, false, state.Month, "fixture:real-title-transfer:" + book.Id.ToString(CultureInfo.InvariantCulture));
        CultivationFixture.Check(moved.Applied, moved.Message); state.Knowledge = authority.Checkpoint(); world = GameWorld.Restore(state);
        before = world.StateHash();
        CultivationFixture.Check(!world.Execute(CultivationAdapterFixture.Command(newcomer.Id, "cultivate", "path.1.0")).Accepted && world.StateHash() == before,
            "actual physical title does not grant personal memory or executable knowledge");
    }
}

public sealed class CultivationWorldAidScenario : IFeatureScenario
{
    public string Name => "INV-004/016/017/018 public pill reservation·cancel·consume exactly once";
    public void Run()
    {
        var (world, id) = CultivationAdapterFixture.Create();
        world = CultivationAdapterFixture.CurrentRealm(world, id, Realm.Qi, true);
        (world, var pill) = CultivationAdapterFixture.Pill(world, id);
        CultivationAdapterFixture.Accepted(world, id, "possess");
        var opening = world.Checkpoint().Materials["pill"].Consumption;
        CultivationAdapterFixture.Accepted(world, id, "breakthrough", "path.2.0", pill);
        var state = world.Checkpoint(); var person = state.People.Single(p => p.Id == id);
        var physical = state.Cultivation[person.BodyId].Attempt!.ReservedItems.Single();
        CultivationFixture.Check(physical != pill && state.Items.Single(i => i.Id == physical).Quantity == 1 &&
            state.Inventory.Reservations.Any(r => r.Active && r.Items.Contains(physical)), "public preparation splits one backed physical pill");
        world = GameCodec.Decode(GameCodec.Encode(world));
        CultivationAdapterFixture.Accepted(world, id, "cancel"); state = world.Checkpoint();
        CultivationFixture.Check(state.Items.Where(i => i.Definition == "pill").Sum(i => i.Quantity) == 3 &&
            state.Materials["pill"].Consumption == opening && state.Items.All(i => i.Reserved == 0), "public safe cancellation releases the reservation without consuming it");
        CultivationAdapterFixture.Accepted(world, id, "breakthrough", "path.2.0", physical);
        world.AdvanceTo(1); state = world.Checkpoint();
        CultivationFixture.Check(state.Items.Where(i => i.Definition == "pill").Sum(i => i.Quantity) == 2 &&
            state.Materials["pill"].Consumption == opening + 1 && state.Inventory.Reservations.All(r => !r.Active), "real start consumes one reserved pill once");
    }
}

public sealed class CultivationWorldLifespanScenario : IFeatureScenario
{
    public string Name => "INV-003/004/023 public Nascent Soul formation follows actual upper-life law and aftercare";
    public void Run()
    {
        var (world, id) = CultivationAdapterFixture.Create();
        world = CultivationAdapterFixture.CurrentRealm(world, id, Realm.GoldenCore, true);
        var state = world.Checkpoint(); var person = state.People.Single(p => p.Id == id);
        state.Laws["life.upperMultiplier"] = 150; person.Lifespan = state.Laws["life.core"];
        world = GameWorld.Restore(state); CultivationAdapterFixture.Accepted(world, id, "possess");
        CultivationAdapterFixture.Accepted(world, id, "breakthrough", "path.4.0");
        world.AdvanceTo(3); state = world.Checkpoint(); person = state.People.Single(p => p.Id == id);
        CultivationFixture.Check(person.Realm == Realm.NascentSoul && person.Lifespan == state.Laws["life.core"] * 150 / 100 &&
            state.Cultivation[person.BodyId].Attempt!.Phase == AttemptPhase.Stabilizing, "actual successful formation applies 150 percent future life law then aftercare");
        world.AdvanceTo(6); state = world.Checkpoint();
        CultivationFixture.Check(state.Cultivation[person.BodyId].Attempt!.Phase == AttemptPhase.Completed &&
            state.People.Single(p => p.Id == id).Action == ActionKind.Idle, "three actual subsequent months stabilize the new structure");
    }
}

public sealed class CultivationWorldLawAdoptionScenario : IFeatureScenario
{
    public string Name => "INV-003/004/006/023 legal cultivation law edits preserve adopted work completion and active attempts";

    private static void Laws(GameWorld world, Dictionary<string, long> changes)
    {
        var result = world.Execute(CultivationAdapterFixture.Command(0, "lawBatch", JsonSerializer.Serialize(changes)));
        CultivationFixture.Check(result.Accepted, result.Message);
    }

    public void Run()
    {
        var (world, id) = CultivationAdapterFixture.Create();
        var opening = world.Checkpoint(); var metadata = JsonSerializer.Serialize(opening.Cultivation);
        var random = opening.RandomState;
        Laws(world, new() { ["cultivation.qi"] = 1, ["cultivation.foundation"] = 1, ["cultivation.upper"] = 100 });
        CultivationFixture.Check(JsonSerializer.Serialize(world.Checkpoint().Cultivation) == metadata &&
            world.Checkpoint().RandomState == random, "legal reduction preserves earned roots, current structure, formation and RNG");
        world = GameCodec.Decode(GameCodec.Encode(world));
        Laws(world, new() { ["cultivation.qi"] = 120000, ["cultivation.foundation"] = 120000, ["cultivation.upper"] = 500 });
        CultivationFixture.Check(JsonSerializer.Serialize(world.Checkpoint().Cultivation) == metadata,
            "legal increase does not invalidate the previously formed current structure");
        world = CultivationAdapterFixture.CurrentRealm(world, id, Realm.Qi, true);
        var complete = world.Checkpoint(); var person = complete.People.Single(p => p.Id == id);
        var completedWork = JsonSerializer.Serialize(complete.Cultivation[person.BodyId]);
        Laws(world, new() { ["cultivation.qi"] = 1 });
        Laws(world, new() { ["cultivation.qi"] = 120000 });
        person = CultivationAdapterFixture.Person(world, id);
        CultivationFixture.Check(person.GreatPerfection && person.Layer == 9 &&
            JsonSerializer.Serialize(world.Checkpoint().Cultivation[person.BodyId]) == completedWork,
            "completion remains tied to adopted physical units under both legal duration directions");
        var restored = GameCodec.Decode(GameCodec.Encode(world));
        world.AdvanceTo(1); restored.AdvanceTo(1);
        CultivationFixture.Check(restored.StateHash() == world.StateHash(), "edited-law save/resume remains deterministic");

        var attemptWorld = CultivationAdapterFixture.Create(); world = CultivationAdapterFixture.CurrentRealm(attemptWorld.World, attemptWorld.Person, Realm.Qi, true);
        id = attemptWorld.Person;
        var snapshot = world.Checkpoint(); person = snapshot.People.Single(p => p.Id == id); person.Lifespan = snapshot.Laws["life.qi"];
        world = GameWorld.Restore(snapshot);
        CultivationAdapterFixture.Accepted(world, id, "breakthrough", "path.2.0");
        snapshot = world.Checkpoint(); var attempt = snapshot.Cultivation[person.BodyId].Attempt!;
        var frozenAttempt = JsonSerializer.Serialize(attempt); var formations = JsonSerializer.Serialize(snapshot.Cultivation[person.BodyId].Formations);
        Laws(world, new() { ["cultivation.foundation"] = 1, ["cultivation.upper"] = 500, ["life.foundation"] = 4800 });
        snapshot = world.Checkpoint();
        CultivationFixture.Check(JsonSerializer.Serialize(snapshot.Cultivation[person.BodyId].Attempt) == frozenAttempt &&
            JsonSerializer.Serialize(snapshot.Cultivation[person.BodyId].Formations) == formations, "prepared law/knowledge/risk/target/life provenance remains frozen");
        world = GameCodec.Decode(GameCodec.Encode(world)); world.AdvanceTo(2);
        snapshot = world.Checkpoint(); person = snapshot.People.Single(p => p.Id == id);
        var formed = snapshot.Cultivation[person.BodyId];
        CultivationFixture.Check(person.Realm == Realm.Foundation && formed.GrowthBasis!.RealmWork == attempt.TargetWork &&
            formed.GrowthBasis.LawRevision == attempt.LawRevision && formed.Formations[^1].LawRevision == attempt.LawRevision &&
            person.Lifespan == attempt.LifespanAward && person.Lifespan == 3600,
            "ongoing formation adopts its prepared target and life award despite later laws");
        CultivationFixture.Check(GameCodec.Decode(GameCodec.Encode(world)).StateHash() == world.StateHash(), "formed old-revision structure saves under new live laws");

        var newWorld = GameWorld.Create(42, new Dictionary<string, long> { ["birth.enabled"] = 0,
            ["cultivation.foundation"] = 1, ["life.foundation"] = 4800, ["cultivation.breakthroughRisk"] = 0 });
        id = newWorld.Checkpoint().People[0].Id; newWorld = CultivationAdapterFixture.CurrentRealm(newWorld, id, Realm.Qi, true);
        CultivationAdapterFixture.Accepted(newWorld, id, "breakthrough", "path.2.0");
        snapshot = newWorld.Checkpoint(); person = snapshot.People.Single(p => p.Id == id);
        var adoptedNew = snapshot.Cultivation[person.BodyId].Attempt!;
        CultivationFixture.Check(adoptedNew.TargetWork == 5000 && adoptedNew.LifespanAward == 4800,
            "newly prepared structures use the new duration and life rules");
    }
}

public sealed class CultivationAiBoundaryScenario : IFeatureScenario
{
    public string Name => "INV-010/021 ordinary AI cultivation completion returns to the actual goal decision boundary";
    public void Run()
    {
        var world = GameWorld.Create(42, new Dictionary<string, long> { ["birth.enabled"] = 0,
            ["ecology.disasterChance"] = 0, ["ecology.foodUse"] = 0, ["cultivation.entry"] = 1 });
        var snapshot = world.Checkpoint(); var person = snapshot.People[0]; var id = person.Id;
        WorldScenarioFixtures.SetCultivation(snapshot, person, Realm.Mortal);
        snapshot.Cultivation[person.BodyId] = snapshot.Cultivation[person.BodyId] with
        { EntryWork = 4999, GrowthBasis = new(snapshot.LawRevision, 0, 0, 5000) };
        var decisions = person.Decisions.Count;
        world = GameWorld.Restore(snapshot);
        CultivationAdapterFixture.Accepted(world, id, "cultivate", "path.1.0");
        world.AdvanceTo(1); snapshot = world.Checkpoint(); person = snapshot.People.Single(p => p.Id == id);
        CultivationFixture.Check(snapshot.Cultivation[person.BodyId].EntryWork == 5000 &&
            person.Decisions.Count == decisions + 1 && person.Decisions[^1].Month == 1,
            "ready entry work yields to real Decide instead of silently preparing a new attempt");
    }
}
