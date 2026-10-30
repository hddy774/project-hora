using Chronicles.Game.Cultivation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Chronicles.Game.Tests.Features;

internal static class ActionAuthorityFixture
{
    internal static (GameWorld World, long Actor) Create()
    {
        var world = GameWorld.Create(42, new Dictionary<string, long> { ["birth.enabled"] = 0, ["ecology.foodUse"] = 0,
            ["ecology.disasterChance"] = 0, ["cultivation.breakthroughRisk"] = 0 });
        var state = world.Checkpoint(); var actor = state.People[0].Id;
        state.People[1].Action = ActionKind.Wait; state.People[1].Due = 100;
        world = GameWorld.Restore(state); CultivationAdapterFixture.Accepted(world, actor, "possess");
        return (world, actor);
    }

    internal static void Reject(GameState snapshot, string message)
    {
        var rejected = false;
        try { GameWorld.Restore(snapshot); }
        catch (InvalidDataException) { rejected = true; }
        CultivationFixture.Check(rejected, message);
        // Deliberately valid wire checksum: rejection must come from saved authority, not damaged bytes.
        var payload = JsonSerializer.Serialize(snapshot);
        var checksum = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("0\n" + payload)));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { Format = "SEONRO-021", Schema = 2, Hash = checksum, Payload = payload, Sequence = 0L });
        try { GameCodec.Decode(bytes); }
        catch (InvalidDataException exception)
        {
            CultivationFixture.Check(!exception.Message.Contains("체크섬", StringComparison.Ordinal), "corrupt authority has a valid authenticated envelope");
            return;
        }
        throw new InvalidOperationException("Checksummed save accepted: " + message);
    }

    internal static void Finish(GameWorld world, long actor)
    {
        var due = world.Checkpoint().People.Single(p => p.Id == actor).Due;
        var restored = GameCodec.Decode(GameCodec.Encode(world));
        world.AdvanceTo(due); restored.AdvanceTo(due);
        CultivationFixture.Check(world.StateHash() == restored.StateHash(), "actual owned pending action resumes deterministically");
    }
}

public sealed class ActionAuthorityCorruptScenario : IFeatureScenario
{
    public string Name => "INV-004/017/021 H10 action owner: unbacked typed work and forged markers are rejected before completion";
    public void Run()
    {
        var (world, actor) = ActionAuthorityFixture.Create(); var initial = world.StateHash();
        foreach (var kind in new[] { ActionKind.Gather, ActionKind.Farm, ActionKind.Craft, ActionKind.Study, ActionKind.Teach,
            ActionKind.Breakthrough, ActionKind.Trial })
        {
            var state = world.Checkpoint(); var person = state.People.Single(p => p.Id == actor);
            person.Action = kind; person.Due = world.Month + 1; person.ActionTarget = 0;
            person.ActionData = kind == ActionKind.Craft ? "pill" : "";
            if (kind == ActionKind.Study) person.ActionTarget = state.Items.First(i => i.Owner == actor && i.Definition == "path.1.0").Id;
            ActionAuthorityFixture.Reject(state, $"raw {kind} cannot reach an unowned legacy completion");
        }
        foreach (var (kind, marker) in new[] { (ActionKind.Heal, "inventory:repair"), (ActionKind.Explore, "knowledge:{}"),
            (ActionKind.Wait, "people:{}"), (ActionKind.Guard, "politics:policy:99"), (ActionKind.Guard, "combat:1"),
            (ActionKind.Heal, "InVeNtOrY:repair"), (ActionKind.Heal, "cultivation.unknown") })
        {
            var state = world.Checkpoint(); var person = state.People.Single(p => p.Id == actor);
            person.Action = kind; person.Due = world.Month + 1; person.ActionData = marker; person.ActionTarget = 999;
            ActionAuthorityFixture.Reject(state, $"marker {marker} cannot fall through to a generic action");
        }
        foreach (var alias in new[] { "1", "6", "Gather", "Farm", "Craft", "BreakThrough", "Trial", "ActionKind.Craft" })
        {
            var result = world.Execute(CultivationAdapterFixture.Command(actor, alias, "pill"));
            CultivationFixture.Check(!result.Accepted && world.StateHash() == initial, "enum spelling/number cannot bypass a typed command source");
        }
        foreach (var marker in new[] { "inventory:repair", "knowledge:{}", "people:{}", "politics:policy:99", "combat:1", "cultivation.unknown" })
        {
            var result = world.Execute(CultivationAdapterFixture.Command(actor, "wait", marker));
            CultivationFixture.Check(!result.Accepted && world.StateHash() == initial, "public marker forgery is an ordinary atomic rejection");
        }
    }
}

public sealed class ActionAuthorityProductionScenario : IFeatureScenario
{
    public string Name => "INV-004/017/018/021 H10 action owner: real consumed production resumes and cancellation never recreates inputs";
    public void Run()
    {
        var (world, actor) = ActionAuthorityFixture.Create();
        foreach (var definition in new[] { "herb", "wood" })
        {
            CultivationAdapterFixture.Accepted(world, actor, "gather", definition);
            ActionAuthorityFixture.Finish(world, actor);
        }
        CultivationAdapterFixture.Accepted(world, actor, "craft", "pill");
        var state = world.Checkpoint(); var person = state.People.Single(p => p.Id == actor);
        var job = state.Inventory.Jobs.Single(j => j.Id == person.ActionTarget);
        CultivationFixture.Check(job.Status == "processing" && job.Consumed.Count > 0 && job.KnowledgeActivity > 0,
            "actual physical inputs and one real knowledge practice back the shared production action");
        var consumption = state.Materials.ToDictionary(p => p.Key, p => p.Value.Consumption, StringComparer.Ordinal);
        var counts = state.Items.GroupBy(i => i.Definition).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity), StringComparer.Ordinal);
        var orphan = GameCodec.Decode(GameCodec.Encode(world)).Checkpoint(); var changed = orphan.People.Single(p => p.Id == actor);
        changed.Action = ActionKind.Idle; changed.Due = 0; changed.ActionTarget = 0; changed.ActionData = "";
        ActionAuthorityFixture.Reject(orphan, "a real active production job cannot lose its actor action");
        var wrongTime = GameCodec.Decode(GameCodec.Encode(world)).Checkpoint();
        wrongTime.People.Single(p => p.Id == actor).Due++;
        ActionAuthorityFixture.Reject(wrongTime, "production cannot change its adopted finish time through a raw action");
        var resumed = GameCodec.Decode(GameCodec.Encode(world));
        var cancel = CultivationAdapterFixture.Command(actor, "cancel");
        CultivationFixture.Check(world.Execute(cancel).Accepted && resumed.Execute(cancel).Accepted && world.StateHash() == resumed.StateHash(),
            "real pending production cancels through the same owner after save");
        state = world.Checkpoint();
        CultivationFixture.Check(state.Inventory.Jobs.Single(j => j.Id == job.Id).Status == "cancelled" &&
            state.Materials.All(p => p.Value.Consumption == consumption.GetValueOrDefault(p.Key)) &&
            state.Items.GroupBy(i => i.Definition).All(g => counts.GetValueOrDefault(g.Key) == g.Sum(i => i.Quantity)),
            "owner validation and cancellation never create replacements for already consumed materials");
    }
}

public sealed class ActionAuthorityKnowledgeScenario : IFeatureScenario
{
    public string Name => "INV-004/017/021/024 H10 action owner: actual study and cultivation practice keep their own adopted activities";
    public void Run()
    {
        var (world, actor) = ActionAuthorityFixture.Create();
        var book = world.Checkpoint().Items.First(i => i.Owner == actor && i.Definition == "path.1.0");
        CultivationAdapterFixture.Accepted(world, actor, "study", target: book.Id);
        var snapshot = world.Checkpoint(); var person = snapshot.People.Single(p => p.Id == actor);
        var forged = GameCodec.Decode(GameCodec.Encode(world)).Checkpoint();
        forged.People.Single(p => p.Id == actor).ActionData = "";
        ActionAuthorityFixture.Reject(forged, "study without its adopted source context is not generic study");
        forged = GameCodec.Decode(GameCodec.Encode(world)).Checkpoint();
        forged.People.Single(p => p.Id == actor).ActionTarget++;
        ActionAuthorityFixture.Reject(forged, "a knowledge action cannot select another activity or reservation");
        ActionAuthorityFixture.Finish(world, actor);
        world = CultivationAdapterFixture.CurrentRealm(world, actor, Realm.Qi, false);
        CultivationAdapterFixture.Accepted(world, actor, "cultivate", "path.1.0");
        snapshot = world.Checkpoint(); person = snapshot.People.Single(p => p.Id == actor);
        CultivationFixture.Check(person.ActionTarget > 0 && snapshot.Knowledge.Activities.Any(a => a.Id == person.ActionTarget),
            "current cultivation owns its separate adopted practice without a knowledge action marker");
        ActionAuthorityFixture.Finish(world, actor);
        snapshot = world.Checkpoint(); Invariants.Validate(snapshot);
        CultivationFixture.Check(snapshot.Cultivation[person.BodyId].RootGrowth.Total > 0, "real cultivation proceeds after owner validation");
    }
}

public sealed class ActionAuthorityCombatRetreatScenario : IFeatureScenario
{
    public string Name => "INV-004/017/021 H10 action owner: a real retreat keeps route provenance after paid combat occupancy ends";
    public void Run()
    {
        var world = CombatFixture.Fresh(busy: false);
        CombatFixture.Send(world, "h10-long-retreat", 0, "law", value: 3, data: "economy.travelDuration");
        var state = world.Checkpoint(); state.RandomState = 1;
        var attacker = state.People[0].Id; var defender = state.People[1];
        defender.Personality = new int[14]; defender.Health = 60; defender.KnownPlaces.Add(1);
        var destination = state.Places[1];
        defender.ObservedPlaces[1] = new(1, destination.Source, destination.Qi, "", state.Month);
        world = GameWorld.Restore(state);
        CombatFixture.Send(world, "h10-control-attacker", attacker, "possess");
        var receipt = CombatFixture.Attack(world, "h10-actual-retreat");
        CultivationFixture.Check(receipt.Reaction.Choice == "retreat" && world.Checkpoint().People[1].Due == 3,
            "fixed functional source exercises actual retreat with a longer route than combat occupancy");
        world.AdvanceTo(1); state = world.Checkpoint(); defender = state.People[1];
        CultivationFixture.Check(defender.Action == ActionKind.Travel && defender.ActionData == $"combat:{receipt.Id}" &&
            defender.Place == 0 && receipt.Participants.Single(p => p.Person == defender.Id).OccupiedUntil == 1,
            "paid combat time ended while the actual route remains in flight");
        Invariants.Validate(state);
        var resumed = GameCodec.Decode(GameCodec.Encode(world));
        var cancelled = GameCodec.Decode(GameCodec.Encode(world));
        CombatFixture.Send(cancelled, "h10-control-traveller", defender.Id, "possess");
        var health = cancelled.Checkpoint().People[1].Health;
        CombatFixture.Send(cancelled, "h10-route-cancel-after-paid-time", defender.Id, "cancel");
        CultivationFixture.Check(cancelled.Checkpoint().People[1].Action == ActionKind.Idle &&
            cancelled.Checkpoint().People[1].Place == 0 && cancelled.Checkpoint().People[1].Health == health,
            "ordinary remaining travel can cancel after paid occupancy without refunding the exchange");
        world.AdvanceTo(3); resumed.AdvanceTo(3);
        CultivationFixture.Check(world.Checkpoint().People[1].Place == 1 && world.StateHash() == resumed.StateHash(),
            "retained receipt supports actual saved arrival after combat occupancy expiry");
    }
}
