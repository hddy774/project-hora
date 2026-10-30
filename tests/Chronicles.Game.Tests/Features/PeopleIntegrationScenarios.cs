using System.Reflection;
using System.Text.Json;
using Chronicles.Game.People;

namespace Chronicles.Game.Tests.Features;

// These fixtures invoke the internal host hooks directly until the common lifecycle dispatch connects them.
// The hooked objects are the actual command-created world state, and save checks use the production GameCodec.
internal static class PeopleBridgeFixture
{
    internal static GameState State(GameWorld world) => (GameState)typeof(GameWorld).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;

    internal static object? Call(GameWorld world, string name, params object?[] args)
    {
        try { return typeof(GameWorld).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, args); }
        catch (TargetInvocationException error) when (error.InnerException is not null) { throw error.InnerException; }
    }

    internal static GameWorld Fresh()
    {
        var world = GameWorld.Create(42); Call(world, "InitializePeopleDomain"); return world;
    }

    internal static Pregnancy Conceive(GameWorld world)
    {
        var people = State(world).People;
        var result = world.Execute(new("people-actual-conception", people[1].Id, "conceive", people[0].Id));
        PeopleFixture.Check(result.Accepted, result.Message);
        var state = State(world); var pregnancy = state.Pregnancies.Single();
        Call(world, "FreezePeopleConception", pregnancy, state.People.Single(p => p.Id == pregnancy.Mother), state.People.Single(p => p.Id == pregnancy.Father));
        return pregnancy;
    }

    internal static Person Birth(GameWorld world, long pregnancyId)
    {
        var pregnancy = State(world).Pregnancies.Single(p => p.Id == pregnancyId);
        world.AdvanceTo(pregnancy.Due);
        var state = State(world); pregnancy = state.Pregnancies.Single(p => p.Id == pregnancyId);
        var child = state.People.Single(p => p.Id == pregnancy.Child);
        Call(world, "InitializePeopleBirth", child, pregnancy); return child;
    }
}

public sealed class PeopleWorldBirthIntegrationScenarios : IFeatureScenario
{
    public string Name => "INV-004/006/011/014 people bridge: actual conception command, frozen body genome, birth soul and GameCodec continuation";

    public void Run()
    {
        var world = PeopleBridgeFixture.Fresh(); var state = PeopleBridgeFixture.State(world);
        PeopleFixture.Check(state.PeopleDomain.Profiles.Length == 2 && state.PeopleDomain.Genomes.Length == 2 && state.PeopleDomain.Souls.All(s => s.Foundation is not null), "exact two declared founder profiles and independent soul foundations");
        var eligibility = (ReproductionEligibility)PeopleBridgeFixture.Call(world, "CheckPeopleReproduction", state.People[1], state.People[0])!;
        PeopleFixture.Check(eligibility.Eligible, "default both-Golden-Core policy admits actual consenting adult founders");
        state.People[0].Realm = Realm.Foundation; state.People[0].Martial = 10;
        eligibility = (ReproductionEligibility)PeopleBridgeFixture.Call(world, "CheckPeopleReproduction", state.People[1], state.People[0])!;
        PeopleFixture.Check(!eligibility.Eligible, "actual tenth martial stage does not silently override default father realm eligibility");
        state.People[0].Realm = Realm.HalfImmortal;
        var pregnancy = PeopleBridgeFixture.Conceive(world); state = PeopleBridgeFixture.State(world);
        var captured = state.PeopleDomain.Inheritance.Conceptions.Single(); var geneticJson = JsonSerializer.Serialize(captured);
        PeopleFixture.Check(pregnancy.Aptitudes[5] == 0 && captured.BodyFoundations.Length == 5 && state.Souls.Count == 2, "actual pregnancy keeps unassigned soul unresolved");
        var before = world.StateHash(); var randomBefore = state.RandomState;
        PeopleBridgeFixture.Call(world, "FreezePeopleConception", pregnancy, state.People[1], state.People[0]);
        PeopleFixture.Check(before == world.StateHash() && randomBefore == state.RandomState, "repeated frozen capture neither consumes global RNG nor rewrites inheritance");
        var restored = GameCodec.Decode(GameCodec.Encode(world));
        var child = PeopleBridgeFixture.Birth(world, pregnancy.Id); var other = PeopleBridgeFixture.Birth(restored, pregnancy.Id);
        state = PeopleBridgeFixture.State(world);
        PeopleFixture.Check(child.Aptitudes.Take(5).SequenceEqual(captured.BodyFoundations) && child.Roots.SequenceEqual(captured.ElementRoots) && child.Aptitudes[5] > 0,
            "newborn body is its frozen biological outcome, with a separately assigned soul foundation");
        PeopleFixture.Check(child.Skills.Count == 0 && state.PeopleDomain.Profiles.Single(p => p.PersonId == child.Id).Learning.Length == 0 &&
            state.PeopleDomain.Profiles.Single(p => p.PersonId == child.Id).Development.Learned.All(v => v == 0), "parents and soul grant no free acquired proficiency");
        PeopleFixture.Check(world.StateHash() == restored.StateHash() && child.Aptitudes.SequenceEqual(other.Aptitudes) && geneticJson == JsonSerializer.Serialize(state.PeopleDomain.Inheritance.Conceptions.Single()),
            "pending conception save resumes the same birth without parent/genetic reroll");
        PeopleBridgeFixture.Call(world, "ValidatePeopleDomain");
        var detail = world.PeopleDetail(child.Id)!; before = world.StateHash(); detail.Innate!.Foundations[0] = 1;
        PeopleFixture.Check(before == world.StateHash(), "observer detail is a detached pure projection");
        var saved = GameCodec.Decode(GameCodec.Encode(world)); PeopleBridgeFixture.Call(saved, "ValidatePeopleDomain");
        PeopleFixture.Check(saved.StateHash() == world.StateHash(), "production world save retains complete people state and source history");
    }
}

public sealed class PeopleWorldLearningVisibilityScenarios : IFeatureScenario
{
    public string Name => "INV-008/015 people bridge: real study completion, self expression and hidden-person source noninterference";

    public void Run()
    {
        var world = PeopleBridgeFixture.Fresh(); var state = PeopleBridgeFixture.State(world); var actorId = state.People[0].Id; var otherId = state.People[1].Id;
        PeopleFixture.Check(world.Execute(new("people-possess", actorId, "possess")).Accepted, "actual possession");
        state = PeopleBridgeFixture.State(world);
        var book = state.Items.First(i => i.Owner == actorId && i.Definition == "path.1.0"); var oldUnderstanding = state.People[0].Understanding[book.Definition];
        PeopleFixture.Check(world.Execute(new("people-study", actorId, "study", book.Id)).Accepted, "actual study command");
        world.AdvanceTo(1); state = PeopleBridgeFixture.State(world); var actor = state.People.Single(p => p.Id == actorId);
        var gained = actor.Understanding[book.Definition] - oldUnderstanding;
        PeopleFixture.Check(gained > 0, "actual physical book study produced understanding");
        var genome = JsonSerializer.Serialize(state.PeopleDomain.Genomes.Single(g => g.PersonId == actorId));
        PeopleBridgeFixture.Call(world, "RecordPeopleLearning", actor, book.Definition, 0, gained, "actual-study-completion", 1);
        var self = world.PeopleDetail(actorId)!;
        PeopleFixture.Check(!self.Observer && self.Innate is null && self.GeneticOrigin is null && self.ActualSoul is null && self.ActualBody is null &&
            self.CurrentTalents[7].Understanding == gained && self.LearningHistory!.Length == 1, "self receives source-backed current expression without hidden innate/soul/genetic truth");
        PeopleFixture.Check(genome == JsonSerializer.Serialize(state.PeopleDomain.Genomes.Single(g => g.PersonId == actorId)), "study never changes transmitted genes");
        var known = world.PeopleDetail(otherId)!; var personalJson = JsonSerializer.Serialize(known);
        PeopleFixture.Check(known.Innate is null && known.CurrentTalents.Length == 0 && known.ActualEvaluations is null, "known NPC does not expose actual aptitude or classifications");
        state.PeopleDomain = state.PeopleDomain with { Profiles = state.PeopleDomain.Profiles.Select(p => p.PersonId == otherId
            ? p with { Innate = p.Innate with { Foundations = Enumerable.Repeat(10_000, 6).ToArray() } } : p).ToArray() };
        var hidden = state.People.Single(p => p.Id == otherId);
        PeopleBridgeFixture.Call(world, "RecordPeopleTrait", hidden, "body.meridian", PeopleFixture.Source("hidden-latent", PeopleSourceKind.Genetic, 1),
            new[] { TraitFact.BodyPrototype }, false, 0L, null);
        PeopleFixture.Check(personalJson == JsonSerializer.Serialize(world.PeopleDetail(otherId)), "hidden gifted genes and latent traits cannot change an observer's unchanged knowledge snapshot");
        var before = world.StateHash(); world.PeopleDetail(actorId); world.PeopleDetail(otherId);
        PeopleFixture.Check(before == world.StateHash(), "self and NPC detail cannot produce learning, dreams, trait awakening or claims");
        PeopleBridgeFixture.Call(world, "ValidatePeopleDomain");
    }
}

public sealed class PeopleWorldTraceAndSplitScenarios : IFeatureScenario
{
    public string Name => "INV-014/024 people bridge: actual life-end source measurements and independent split without free mastery or population";

    public void Run()
    {
        var world = PeopleBridgeFixture.Fresh(); var state = PeopleBridgeFixture.State(world); var person = state.People[0];
        person.Skills["alchemy"] = 2000;
        PeopleBridgeFixture.Call(world, "EndLife", person, 1, "actual fixture lifespan ending", LifeStatus.Dead);
        PeopleBridgeFixture.Call(world, "EndPeopleLife", person);
        var trace = state.PeopleDomain.Souls.Single(s => s.SoulId == person.SoulId);
        PeopleFixture.Check(trace.Values[3].Preserved == 2000 && trace.Values[5].Preserved == 0 && trace.Values.All(v => v.HasEvidence && v.Accessible == 0 && v.Integrated == 0),
            "six domains measure different actual sources; technique understanding count does not synthesize every trace");
        var before = world.StateHash(); PeopleBridgeFixture.Call(world, "EndPeopleLife", person);
        PeopleFixture.Check(before == world.StateHash(), "same life end cannot capture memory twice");
        var ascender = state.People[1]; ascender.Place = 99; ascender.Action = ActionKind.Idle; state.Laws["founder.ascensionProtection"] = 0;
        ascender.Skills["alchemy"] = 4000;
        PeopleFixture.Check(PeopleBridgeFixture.Call(world, "Ascend", ascender) is null, "actual valid ascension created a waiting independent soul");
        PeopleBridgeFixture.Call(world, "EndPeopleLife", ascender);
        var root = state.Souls.Single(s => s.Id == ascender.SoulId); var child = state.Souls.Single(s => s.Parent == root.Id);
        var original = state.PeopleDomain.Souls.Single(s => s.SoulId == root.Id).Values.Select(v => v.Preserved).ToArray();
        PeopleBridgeFixture.Call(world, "SplitPeopleSoul", root, child);
        var parentTrace = state.PeopleDomain.Souls.Single(s => s.SoulId == root.Id); var childTrace = state.PeopleDomain.Souls.Single(s => s.SoulId == child.Id);
        PeopleFixture.Check(childTrace.ParentSoulId == root.Id && childTrace.CurrentPersonId == 0 && childTrace.Values.All(v => v.Accessible == 0 && v.Integrated == 0) &&
            state.People.Count == 2 && childTrace.Foundation!.Value < parentTrace.Foundation!.Value, "actual split is an independent, costed soul branch and does not spawn a body or copy mastery");
        for (var domain = 0; domain < 6; domain++)
            PeopleFixture.Check(parentTrace.Values[domain].Preserved + childTrace.Values[domain].Preserved == original[domain] * 70 / 100, "split allocation plus actual preservation cost is not duplication");
        PeopleBridgeFixture.Call(world, "ValidatePeopleDomain");
        var restored = GameCodec.Decode(GameCodec.Encode(world)); PeopleBridgeFixture.Call(restored, "ValidatePeopleDomain");
        PeopleFixture.Check(restored.StateHash() == world.StateHash(), "source capture and independent split survive the production save container");
    }
}

public sealed class PeopleWorldMonthlyTraitScenarios : IFeatureScenario
{
    public string Name => "INV-006/015 people bridge: monthly actual damage markers, release history and idempotent settlement";

    public void Run()
    {
        var world = PeopleBridgeFixture.Fresh(); var personId = PeopleBridgeFixture.State(world).People[0].Id;
        PeopleBridgeFixture.State(world).People[0].StructureDamage = 20;
        world.AdvanceTo(1); PeopleBridgeFixture.Call(world, "SettlePeopleDomain");
        var state = PeopleBridgeFixture.State(world); var marker = state.PeopleDomain.Traits.Traits.Single(t => t.PersonId == personId && t.DefinitionId == "scar.foundation");
        PeopleFixture.Check(marker.Status == TraitStatus.Active && state.People[0].StructureDamage == 20 && Traits.ActiveEffects(state.PeopleDomain.Traits, personId, 1).Length == 0,
            "derived injury marker reflects real damage without feeding another damage effect into it");
        var before = world.StateHash(); PeopleBridgeFixture.Call(world, "SettlePeopleDomain");
        PeopleFixture.Check(before == world.StateHash(), "repeated monthly settlement cannot acquire markers or award trace access again");
        state.People[0].StructureDamage = 0;
        world.AdvanceTo(2); PeopleBridgeFixture.Call(world, "SettlePeopleDomain");
        state = PeopleBridgeFixture.State(world); marker = state.PeopleDomain.Traits.Traits.Single(t => t.Id == marker.Id);
        PeopleFixture.Check(marker.Status == TraitStatus.Released && marker.History[^1].Kind == TraitChangeKind.Released, "actual recovery releases current injury while retaining causal history");
        PeopleBridgeFixture.Call(world, "ValidatePeopleDomain");
        var restored = GameCodec.Decode(GameCodec.Encode(world)); PeopleBridgeFixture.Call(restored, "ValidatePeopleDomain");
        PeopleFixture.Check(restored.Checkpoint().PeopleDomain.SettledThroughMonth == 2 && restored.StateHash() == world.StateHash(), "monthly cursor and release history round trip without duplicate effects");
    }
}
