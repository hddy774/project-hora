using System.Text.Json;
using Chronicles.Game.Combat;
using Chronicles.Game.Cultivation;

namespace Chronicles.Game.Tests;

internal static class CombatFixture
{
    public static GameWorld Fresh(int path = 0, bool busy = true)
    {
        var world = GameWorld.Create(42, new Dictionary<string, long>
        {
            ["birth.enabled"] = 0, ["ecology.foodUse"] = 0, ["ecology.disasterChance"] = 0,
            ["combat.hitMinimum"] = 10000, ["combat.hitMaximum"] = 10000
        });
        var state = world.Checkpoint();
        WorldScenarioFixtures.SetCultivation(state, state.People[0], Realm.GoldenCore, 5, path: $"path.3.{path}");
        WorldScenarioFixtures.SetCultivation(state, state.People[1], Realm.GoldenCore, 5);
        foreach (var p in state.People) { p.Action = ActionKind.Idle; p.Due = 0; p.Personality = Enumerable.Repeat(100, 14).ToArray(); }
        if (busy) { state.People[1].Action = ActionKind.Guard; state.People[1].Due = 1000; }
        return GameWorld.Restore(state);
    }
    public static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
    public static void Send(GameWorld world, string id, long actor, string kind, long target = 0, long value = 0, string data = "")
    { var result = world.Execute(new(id, actor, kind, target, value, data)); Check(result.Accepted, kind + ": " + result.Message); }
    public static void Reject(GameWorld world, string id, long actor, string kind, long target = 0)
    { var hash = world.StateHash(); Check(!world.Execute(new(id, actor, kind, target)).Accepted && world.StateHash() == hash, "rejected " + kind + " mutated authority"); }
    public static long Item(GameState state, long owner, string definition)
    {
        var item = new Item { Id = state.NextId++, Owner = owner, Definition = definition, Quantity = 1, Quality = 5000,
            Place = -1, Source = "실제 교전 기능 검사의 명시 초기 실물" };
        state.Items.Add(item);
        if (!state.Materials.TryGetValue(definition, out var material)) state.Materials.Add(definition, material = new());
        material.Opening++; return item.Id;
    }
    public static CombatExchange Attack(GameWorld world, string id = "actual-combat")
    { var s = world.Checkpoint(); Send(world, id, s.People[0].Id, "attack", s.People[1].Id); return world.Checkpoint().Combat.Exchanges.Last(); }
}

public sealed class CombatAxesMechanicsScenario : IFeatureScenario
{
    public string Name => "combat mechanics: each canonical axis changes practical hit damage escape coordination or finite exertion";
    public void Run()
    {
        var basis = CombatAxes.Uniform(10000) with { MaintenanceCost = 5000 };
        var rules = new CombatRules(20, 4, 0, 10000, 1, 1, 2, 1);
        var coordination = CombatMechanics.Coordination(basis, 3);
        var chance = CombatMechanics.HitChance(basis, basis, coordination, rules);
        var damage = CombatMechanics.Damage(basis, basis, basis.OutputPenetration, CombatMechanics.Sustainability(basis, 5000), rules);
        CombatFixture.Check(CombatMechanics.Damage(basis, basis, 20000, CombatMechanics.Sustainability(basis, 5000), rules) > damage, "output is real damage");
        var reserve = basis with { ReserveEndurance = 20000 };
        CombatFixture.Check(CombatMechanics.Exertion(reserve, 5000, rules) < CombatMechanics.Exertion(basis, 5000, rules) &&
            CombatMechanics.Sustainability(reserve, 5000) > CombatMechanics.Sustainability(basis, 5000), "reserve sustains attacks and reduces paid exhaustion");
        CombatFixture.Check(CombatMechanics.HitChance(basis with { ControlPrecision = 20000 }, basis, coordination, rules) > chance, "precision affects actual hit");
        CombatFixture.Check(CombatMechanics.Damage(basis, basis with { ProtectionRecovery = 20000 }, basis.OutputPenetration,
            CombatMechanics.Sustainability(basis, 5000), rules) < damage, "protection absorbs actual damage");
        CombatFixture.Check(CombatMechanics.HitChance(basis with { MovementSpace = 20000 }, basis, coordination, rules) > chance &&
            CombatMechanics.EscapeChance(basis with { MovementSpace = 20000 }, basis) > CombatMechanics.EscapeChance(basis, basis), "movement changes approach and escape");
        CombatFixture.Check(CombatMechanics.HitChance(basis with { SensingConcealment = 20000 }, basis, coordination, rules) > chance &&
            CombatMechanics.EscapeChance(basis with { SensingConcealment = 20000 }, basis) > CombatMechanics.EscapeChance(basis, basis), "detection changes initiative and pursuit");
        CombatFixture.Check(CombatMechanics.Coordination(basis with { CoordinationLoad = 20000 }, 3) > coordination, "coordination handles actual controlled roles");
        var expensive = basis with { MaintenanceCost = 10000 };
        CombatFixture.Check(CombatMechanics.Exertion(expensive, 5000, rules) > CombatMechanics.Exertion(basis, 5000, rules) &&
            CombatMechanics.Sustainability(expensive, 5000) < CombatMechanics.Sustainability(basis, 5000), "maintenance consumes finite resources and lowers sustainability");
    }
}

public sealed class CombatWorldCostScenario : IFeatureScenario
{
    public string Name => "combat public: actual canonical routes pay health gear and shared time and preserve busy defender work";
    public void Run()
    {
        var costs = new List<int>(); var damages = new List<int>();
        for (var route = 0; route < 3; route++)
        {
            var world = CombatFixture.Fresh(route); var state = world.Checkpoint(); var actor = state.People[0].Id;
            var weapon = CombatFixture.Item(state, actor, "weapon"); world = GameWorld.Restore(state);
            CombatFixture.Send(world, "equip-actual-weapon", actor, "equip", weapon);
            var before = world.Checkpoint(); var canonical = world.View(actor).Detail!.CombatProjection!.Axes;
            var exchange = CombatFixture.Attack(world); var after = world.Checkpoint();
            var person = exchange.Participants.Single(p => p.Person == actor);
            CombatFixture.Check(person.Exertion > 0 && after.People[0].Health == before.People[0].Health - person.Exertion &&
                after.Items.Single(i => i.Id == weapon).Damage == before.Items.Single(i => i.Id == weapon).Damage + 1,
                "actual action failed to debit health/wear existing equipment");
            CombatFixture.Check(after.People[0].Action == ActionKind.Guard && after.People[0].Due == after.Month + 1 &&
                after.People[1].Action == before.People[1].Action && after.People[1].Due == before.People[1].Due && exchange.Reaction.Choice == "busy",
                "a combat action duplicated labor or overwrote another real job");
            CombatFixture.Check(exchange.Strikes.Count == 1 && exchange.Strikes[0].Hit && exchange.Strikes[0].Damage > 0 &&
                person.Axes == canonical && person.Axes.MaintenanceCost > 0 && person.Axes.ReserveEndurance > 0,
                "live action did not use canonical current inputs");
            costs.Add(person.Exertion); damages.Add(exchange.Strikes[0].Damage); Invariants.Validate(after);
        }
        CombatFixture.Check(costs.Distinct().Count() > 1 && damages.Distinct().Count() > 1, "same-realm actual paths have no practical cost/damage difference");
    }
}

public sealed class CombatWorldSupportScenario : IFeatureScenario
{
    public string Name => "combat public: explicit local guard intent contributes once with real supporter health time and no fabricated allies";
    public void Run()
    {
        var world = CombatFixture.Fresh(); var first = world.Checkpoint().People[0].Id;
        CombatFixture.Send(world, "actual-helper-arrival", 0, "create", data: "자발적 경계자");
        var state = world.Checkpoint(); var helper = state.People[2];
        WorldScenarioFixtures.SetCultivation(state, helper, Realm.GoldenCore, 5); helper.Action = ActionKind.Guard; helper.Due = 1000;
        world = GameWorld.Restore(state);
        CombatFixture.Send(world, "observer-selects-explorer", first, "possess");
        CombatFixture.Send(world, "observe-real-helper", first, "explore"); world.AdvanceTo(1);
        CombatFixture.Send(world, "release-for-helper-command", 0, "observe"); state = world.Checkpoint();
        state.People[2].Action = ActionKind.Idle; state.People[2].Due = 0; world = GameWorld.Restore(state);
        var ordinary = GameWorld.Restore(world.Checkpoint());
        CombatFixture.Send(ordinary, "ordinary-guard", helper.Id, "guard");
        CombatFixture.Send(world, "explicit-help", helper.Id, "guard", first);
        var before = world.Checkpoint(); var assisted = CombatFixture.Attack(world); var solo = CombatFixture.Attack(ordinary);
        CombatFixture.Check(assisted.Strikes[0].Supporters.SequenceEqual([helper.Id]) && solo.Strikes[0].Supporters.Count == 0 &&
            assisted.Strikes[0].Damage > solo.Strikes[0].Damage, "ordinary nearby guards were conscripted or explicit help did not affect the exchange");
        var paid = world.Checkpoint().People.Single(p => p.Id == helper.Id);
        CombatFixture.Check(paid.Health < before.People.Single(p => p.Id == helper.Id).Health && paid.Due == 2 &&
            paid.ActionData.StartsWith("combat:", StringComparison.Ordinal), "support did not occupy real existing time and actual health");
        CombatFixture.Reject(world, "no-free-support-cancel", helper.Id, "cancel");
        CombatFixture.Check(world.Checkpoint().People.Count == 3, "combat created people");
    }
}

public sealed class CombatWorldReactionScenario : IFeatureScenario
{
    public string Name => "combat public: defender independently counters once or spends a real known route on retreat";
    public void Run()
    {
        var counter = CombatFixture.Fresh(busy: false); var initial = counter.Checkpoint(); var exchange = CombatFixture.Attack(counter);
        CombatFixture.Check(exchange.Reaction.Choice == "counter" && exchange.Strikes.Count == 2 &&
            counter.Checkpoint().People.All(p => p.Health < 100 && p.Due == 1), "actual independent response was recursive free or absent");
        var retreated = false;
        // Explicit bounded functional seeds exercise the real probabilistic route; no long-run or growth measurement.
        for (ulong seed = 1; seed <= 12 && !retreated; seed++)
        {
            var world = CombatFixture.Fresh(busy: false); var s = world.Checkpoint(); s.RandomState = seed;
            s.People[1].Personality = new int[14]; s.People[1].Health = 60;
            s.People[1].KnownPlaces.Add(1); var place = s.Places[1];
            s.People[1].ObservedPlaces[1] = new(1, place.Source, place.Qi, "", s.Month);
            world = GameWorld.Restore(s); var result = CombatFixture.Attack(world);
            if (result.Reaction.Choice != "retreat") continue;
            var defender = world.Checkpoint().People[1];
            CombatFixture.Check(defender.Place == 0 && defender.Action == ActionKind.Travel && defender.ActionTarget == 1 && defender.Health > 0,
                "retreat teleported the defender or ignored actual travel labor");
            CombatFixture.Reject(world, "no-same-tick-retreat-refund", defender.Id, "cancel");
            var loaded = GameCodec.Decode(GameCodec.Encode(world)); world.AdvanceTo(defender.Due); loaded.AdvanceTo(defender.Due);
            CombatFixture.Check(world.Checkpoint().People[1].Place == 1 && world.StateHash() == loaded.StateHash(), "retreat/save failed the actual travel scheduler");
            retreated = true;
        }
        CombatFixture.Check(retreated, "bounded actual route fixture did not exercise successful retreat");
        CombatFixture.Check(initial.Items.Count == counter.Checkpoint().Items.Count, "combat fabricated or deleted physical assets");
    }
}

public sealed class CombatWorldCognitionScenario : IFeatureScenario
{
    public string Name => "combat public: actual attacked witness and reaction obey a one-fact shared cognition budget";
    public void Run()
    {
        var world = CombatFixture.Fresh(busy: false);
        CombatFixture.Send(world, "tiny-real-cognition", 0, "law", value: 1, data: "ai.beliefBudget");
        CombatFixture.Attack(world);
        foreach (var person in world.Checkpoint().People)
            CombatFixture.Check(person.Beliefs.Count + person.ObservedPeople.Count + person.ObservedPlaces.Count +
                world.Checkpoint().Inventory.Observations.Count(o => o.Observer == person.Id) <= 1,
                "appending an actual attacked witness overflowed bounded saved cognition");
        Invariants.Validate(world.Checkpoint());
    }
}

public sealed class CombatWorldReplayScenario : IFeatureScenario
{
    public string Name => "combat public: command replay paid cooldown and saved continuation preserve actual receipts and query purity";
    public void Run()
    {
        var world = CombatFixture.Fresh(); var initial = world.Checkpoint(); var actor = initial.People[0].Id;
        CombatFixture.Send(world, "controlled-attacker", actor, "possess");
        var command = new GameCommand("one-actual-exchange", actor, "attack", initial.People[1].Id);
        CombatFixture.Check(world.Execute(command).Accepted, "real attack rejected"); var hash = world.StateHash();
        CombatFixture.Check(world.Execute(command).Accepted && world.StateHash() == hash && world.Checkpoint().Combat.Exchanges.Count == 1,
            "replayed command repeated physical costs RNG or damage");
        world.View(actor); world.View(initial.People[1].Id); CombatFixture.Check(hash == world.StateHash(), "combat query changed authority/RNG");
        CombatFixture.Reject(world, "paid-labor-not-refundable", actor, "cancel");
        CombatFixture.Reject(world, "no-second-action", actor, "attack", initial.People[1].Id);
        var loaded = GameCodec.Decode(GameCodec.Encode(world)); world.AdvanceTo(1); loaded.AdvanceTo(1);
        CombatFixture.Check(world.StateHash() == loaded.StateHash() && world.Checkpoint().People[0].Action == ActionKind.Idle,
            "saved paid combat did not finish once at its real decision boundary");
        var corrupt = loaded.Checkpoint(); var receipt = corrupt.Combat.Exchanges[0];
        corrupt.Combat.Exchanges[0] = receipt with { Participants = receipt.Participants.Select(p => p.Person == actor ? p with { Exertion = p.Exertion + 1 } : p).ToArray() };
        var rejected = false; try { GameWorld.Restore(corrupt); } catch (InvalidDataException) { rejected = true; }
        CombatFixture.Check(rejected, "corrupt saved combat health/source audit was accepted");
    }
}

public sealed class CombatWorldPrivacyScenario : IFeatureScenario
{
    public string Name => "combat public: hidden opponent formation is absent from actor queries and receipt inputs never enter public logs";
    public void Run()
    {
        var a = CombatFixture.Fresh(); var s = a.Checkpoint(); var actor = s.People[0].Id; var other = s.People[1].Id;
        CombatFixture.Send(a, "private-observer-to-play", actor, "possess"); s = a.Checkpoint();
        WorldScenarioFixtures.SetCultivation(s, s.People[1], Realm.GoldenCore, 5, path: "path.3.2"); var b = GameWorld.Restore(s);
        CombatFixture.Check(JsonSerializer.Serialize(a.View(other)) == JsonSerializer.Serialize(b.View(other)),
            "unobserved actual opponent formation changed personal query or recommendation");
        CombatFixture.Attack(a);
        var visible = JsonSerializer.Serialize(a.View(other));
        CombatFixture.Check(a.View(other).Detail?.CombatProjection is null && !visible.Contains("HitChance", StringComparison.Ordinal) &&
            a.View().Events.Where(e => e.Kind == "전투").All(e => e.Source.StartsWith("combat:", StringComparison.Ordinal) && !e.Source.Contains('{')),
            "actual hidden opponent axes or rolls leaked through a play projection/event source");
    }
}
