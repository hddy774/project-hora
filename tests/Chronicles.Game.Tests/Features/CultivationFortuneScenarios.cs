using Chronicles.Game.Cultivation;
using Chronicles.Game.People;

namespace Chronicles.Game.Tests.Features;

internal static class CultivationFortuneFixture
{
    internal static (GameWorld World, long Person) Create(int risk = 0, int disaster = 0)
    {
        var world = GameWorld.Create(42, new Dictionary<string, long> { ["birth.enabled"] = 0, ["ecology.foodUse"] = 0,
            ["ecology.regrowth"] = 0, ["ecology.disasterChance"] = disaster, ["hazard.trialDamage"] = 0,
            ["cultivation.breakthroughRisk"] = risk });
        var state = world.Checkpoint(); var person = state.People[0];
        state.People[1].Action = ActionKind.Wait; state.People[1].Due = 100;
        world = GameWorld.Restore(state);
        CultivationAdapterFixture.Accepted(world, person.Id, "possess");
        return (world, person.Id);
    }

    internal static GameWorld Pending(GameState state, long id, int target, long due, bool removeEscape = false)
    {
        var person = state.People.Single(p => p.Id == id);
        var metadata = state.Cultivation[person.BodyId];
        state.Cultivation[person.BodyId] = metadata with { PendingTrial = new(target, false, due) };
        person.PendingLayer = target; person.Action = ActionKind.Trial; person.Due = due;
        person.ActionData = ""; person.ActionTarget = 0;
        state.RandomState = 1; // First existing resolution sample is 5165; no new RNG stream is introduced.
        if (removeEscape) state.PeopleDomain = state.PeopleDomain with
        { Traits = new(state.PeopleDomain.Traits.Traits.Where(t => t.PersonId != id || t.DefinitionId != "fortune.escape").ToArray()) };
        return GameWorld.Restore(state);
    }
}

public sealed class CultivationEscapeFortuneWorldScenario : IFeatureScenario
{
    public string Name => "INV-004/006/015/023 fortune public: real trial source changes one dated risk and preserves the existing RNG draw";
    public void Run()
    {
        var (world, id) = CultivationFortuneFixture.Create();
        var state = world.Checkpoint(); var person = state.People.Single(p => p.Id == id);
        WorldScenarioFixtures.SetCultivation(state, person, Realm.Tribulation);
        var metadata = state.Cultivation[person.BodyId];
        state.Cultivation[person.BodyId] = metadata with
        { Structure = ((TribulationStructure)metadata.Structure!) with { CompletedTrials = 0 }, PendingTrial = new(1, false, 1) };
        person.PendingLayer = 1; person.Action = ActionKind.Trial; person.Due = 1;
        world = GameWorld.Restore(state);
        world.AdvanceTo(1); state = world.Checkpoint();
        var passed = state.Events.Single(e => e.Kind == "겁난 통과" && e.Actor == id);
        var escape = state.PeopleDomain.Traits.Traits.Single(t => t.PersonId == id && t.DefinitionId == "fortune.escape");
        CultivationFixture.Check(escape.Status == TraitStatus.Active && escape.Source.CauseId == $"event:{passed.Id}" &&
            escape.AcquiredMonth == 1 && escape.ExpiresMonth == 4, "real canonical trial emits exactly one active source-backed three-month escape trait");

        state.Laws["cultivation.breakthroughRisk"] = 5400;
        var baseline = CultivationFortuneFixture.Pending(GameCodec.Decode(GameCodec.Encode(world)).Checkpoint(), id, 2, 2, true);
        var baselineState = baseline.Checkpoint(); baselineState.Laws["cultivation.breakthroughRisk"] = 5400;
        baseline = GameWorld.Restore(baselineState);
        var fortunate = CultivationFortuneFixture.Pending(state, id, 2, 2);
        var restored = GameCodec.Decode(GameCodec.Encode(fortunate));
        fortunate.AdvanceTo(2); restored.AdvanceTo(2); baseline.AdvanceTo(2);
        var after = fortunate.Checkpoint(); var without = baseline.Checkpoint();
        CultivationFixture.Check(after.People.Single(p => p.Id == id).Status == LifeStatus.Alive &&
            without.People.Single(p => p.Id == id).Status == LifeStatus.Dead && without.Souls.Single(s => s.Id == person.SoulId).Route == 8,
            "same actual sample passes the reduced risk and fails the baseline through shared life authority");
        CultivationFixture.Check(after.RandomState == 33554433 && without.RandomState == after.RandomState &&
            fortunate.StateHash() == restored.StateHash(), "only the existing single resolution draw is used and source-backed risk resumes exactly");

        // Start from the first actual success again; a trial due at the exact expiry cannot retain that benefit.
        var expiredState = GameCodec.Decode(GameCodec.Encode(world)).Checkpoint();
        expiredState.Laws["cultivation.breakthroughRisk"] = 5400;
        var expired = CultivationFortuneFixture.Pending(expiredState, id, 2, 4);
        expired.AdvanceTo(3); expiredState = expired.Checkpoint(); expiredState.RandomState = 1;
        expired = GameWorld.Restore(expiredState); expired.AdvanceTo(4);
        CultivationFixture.Check(expired.Checkpoint().People.Single(p => p.Id == id).Status == LifeStatus.Dead,
            "exact ExpiresMonth removes the fortune effect before actual trial resolution");
    }
}

public sealed class CultivationAdversityFortuneWorldScenario : IFeatureScenario
{
    public string Name => "INV-004/006/015/023 fortune public: actual disaster modifies adopted breakthrough risk once without quality or resource grants";
    public void Run()
    {
        var (world, id) = CultivationFortuneFixture.Create(5000, 10000);
        world.AdvanceTo(12);
        var state = world.Checkpoint(); var person = state.People.Single(p => p.Id == id);
        var disaster = state.Events.Single(e => e.Kind == "재해" && e.Target == person.Place);
        var adversity = state.PeopleDomain.Traits.Traits.Single(t => t.PersonId == id && t.DefinitionId == "fortune.bad");
        CultivationFixture.Check(adversity.Source.CauseId == $"event:{disaster.Id}" && adversity.ExpiresMonth == 18 &&
            adversity.Status == TraitStatus.Active, "actual witnessed disaster creates a bounded six-month adversity source");
        WorldScenarioFixtures.SetCultivation(state, person, Realm.Qi, 9, true);
        person.Health = 100; person.Action = ActionKind.Idle; person.Due = 0;
        world = GameWorld.Restore(state);
        var baselineState = world.Checkpoint();
        baselineState.PeopleDomain = baselineState.PeopleDomain with
        { Traits = new(baselineState.PeopleDomain.Traits.Traits.Where(t => t.Id != adversity.Id).ToArray()) };
        var baseline = GameWorld.Restore(baselineState);
        var before = world.Checkpoint();
        CultivationAdapterFixture.Accepted(world, id, "breakthrough", "path.2.0");
        CultivationAdapterFixture.Accepted(baseline, id, "breakthrough", "path.2.0");
        state = world.Checkpoint(); baselineState = baseline.Checkpoint();
        var attempt = state.Cultivation[person.BodyId].Attempt!; var ordinary = baselineState.Cultivation[person.BodyId].Attempt!;
        CultivationFixture.Check(attempt.Risk == 5500 && ordinary.Risk == 5000 &&
            attempt.FormationQuality == ordinary.FormationQuality && state.RandomState == before.RandomState &&
            state.NextId == baselineState.NextId && state.Items.Count == before.Items.Count && state.CreatedMoney == before.CreatedMoney,
            "one real source adds only 500bps risk, preserving formation quality, assets and preview RNG");
        var expired = GameWorld.Restore(before); expired.AdvanceTo(18);
        CultivationAdapterFixture.Accepted(expired, id, "breakthrough", "path.2.0");
        CultivationFixture.Check(expired.Checkpoint().Cultivation[person.BodyId].Attempt!.Risk == ordinary.Risk,
            "a future actual preparation after source expiry cannot retain the previous adversity modifier");
        CultivationAdapterFixture.Accepted(world, id, "cultivationstart");
        CultivationAdapterFixture.Accepted(baseline, id, "cultivationstart");
        var source = world.Checkpoint(); var plain = baseline.Checkpoint(); source.RandomState = 1; plain.RandomState = 1;
        world = GameWorld.Restore(source); baseline = GameWorld.Restore(plain);
        var restored = GameCodec.Decode(GameCodec.Encode(world));
        world.AdvanceTo(13); restored.AdvanceTo(13); baseline.AdvanceTo(13);
        state = world.Checkpoint(); baselineState = baseline.Checkpoint();
        CultivationFixture.Check(state.Cultivation[person.BodyId].Attempt!.Phase == AttemptPhase.Recovering &&
            baselineState.People.Single(p => p.Id == id).Realm == Realm.Foundation &&
            state.RandomState == baselineState.RandomState && state.RandomState == 33554433 && world.StateHash() == restored.StateHash(),
            "adopted risk changes the real success/failure outcome with the same single RNG draw after save");
    }
}
