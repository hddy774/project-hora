using System.Text.Json;
using Chronicles.Game.Cultivation;
using Chronicles.Game.Knowledge;
using Chronicles.Game.People;

namespace Chronicles.Game.Tests.Features;

// Fixtures declare explicit current conditions. Every lifecycle, learning and recovery transition below
// runs through public world commands/time; no private hooks or manual people learning credits are used.
internal static class PeoplePublicFixture
{
    internal static GameWorld Fresh(Dictionary<string, long>? overrides = null)
    {
        var laws = new Dictionary<string, long> { ["birth.chance"] = 0, ["ecology.disasterChance"] = 0 };
        if (overrides is not null)
            foreach (var (key, value) in overrides) laws[key] = value;
        return GameWorld.Create(42, laws);
    }

    internal static void Accept(GameWorld world, GameCommand command)
    {
        var result = world.Execute(command); PeopleFixture.Check(result.Accepted, $"{command.Kind}: {result.Message}");
    }

    internal static GameWorld Configure(GameWorld world, Action<GameState> configure)
    {
        var snapshot = world.Checkpoint(); configure(snapshot); return GameWorld.Restore(snapshot);
    }

    internal static CultivationPolicy Policy(GameState state) => new()
    {
        Revision = state.LawRevision, EntryMonths = (int)state.Laws["cultivation.entry"], QiMonths = (int)state.Laws["cultivation.qi"],
        FoundationMonths = (int)state.Laws["cultivation.foundation"], UpperDurationPercent = (int)state.Laws["cultivation.upper"],
        BaseRisk = (int)state.Laws["cultivation.breakthroughRisk"], TrialRisk = (int)state.Laws["cultivation.breakthroughRisk"],
        MartialMonthsPerStage = (int)state.Laws["martial.period"], TrialHealthCost = (int)state.Laws["hazard.trialDamage"],
        QiLifespanMonths = state.Laws["life.qi"], FoundationLifespanMonths = state.Laws["life.foundation"],
        GoldenCoreLifespanMonths = state.Laws["life.core"], HalfImmortalLifespanMonths = state.Laws["founder.lifespan"]
    };

    internal static void CurrentRealm(GameState state, Person person, Realm realm, bool complete = false)
    {
        var policy = Policy(state);
        var actor = new CultivationActor { PersonId = person.Id, BodyId = person.BodyId, SoulId = person.SoulId, Realm = realm,
            Layer = complete ? 9 : 1, GreatPerfection = complete, Health = person.Health, Status = person.Status };
        var metadata = CultivationFixture.State(actor, policy, complete);
        state.Cultivation[person.BodyId] = metadata;
        person.Realm = realm; person.Layer = actor.Layer; person.GreatPerfection = complete;
        person.Action = ActionKind.Idle; person.Due = 0; person.ActionTarget = 0; person.ActionData = "";
        person.Irreversible = false; person.PendingLayer = 0; person.StructureDamage = 0;
        person.Work = metadata.Structure?.Measures.Sum() ?? metadata.EntryWork;
        person.Formations = metadata.Formations.Select(f => new Formation(f.Realm, f.Path, f.Quality, 0, f.Month, f.Source)).ToList();
        person.Components = new int[7]; person.Processes = new int[4];
    }

    internal static Pregnancy Conceive(GameWorld world, string id = "people.actual-conception")
    {
        var state = world.Checkpoint(); var mother = state.People.Single(p => p.Gestates); var father = state.People.Single(p => !p.Gestates);
        Accept(world, new(id, mother.Id, "conceive", father.Id));
        return world.Checkpoint().Pregnancies.Single();
    }

    internal static Person Birth(GameWorld world, long pregnancyId)
    {
        var pregnancy = world.Checkpoint().Pregnancies.Single(p => p.Id == pregnancyId);
        world.AdvanceTo(pregnancy.Due);
        var state = world.Checkpoint(); pregnancy = state.Pregnancies.Single(p => p.Id == pregnancyId);
        return state.People.Single(p => p.Id == pregnancy.Child);
    }
}

public sealed class PeopleWorldBirthIntegrationScenarios : IFeatureScenario
{
    public string Name => "INV-004/006/011/014 people public: frozen conception, actual birth soul and production-save continuation";

    public void Run()
    {
        var world = PeoplePublicFixture.Fresh(); var state = world.Checkpoint();
        PeopleFixture.Check(state.PeopleDomain.Profiles.Length == 2 && state.PeopleDomain.Genomes.Length == 2 &&
            state.PeopleDomain.Souls.All(s => s.Foundation is not null) && state.People.All(p => p.Realm == Realm.HalfImmortal),
            "public creation initializes exactly two founder bodies and independent soul foundations");
        var pregnancy = PeoplePublicFixture.Conceive(world); state = world.Checkpoint();
        var captured = state.PeopleDomain.Inheritance.Conceptions.Single(); var original = JsonSerializer.Serialize(captured);
        PeopleFixture.Check(pregnancy.Aptitudes[5] == 0 && captured.BodyFoundations.Length == 5 && state.Souls.Count == 2,
            "public conception freezes body-only genetic evidence while its soul remains unassigned");
        var before = world.StateHash(); var randomBefore = state.RandomState;
        PeoplePublicFixture.Accept(world, new("people.actual-conception", pregnancy.Mother, "conceive", pregnancy.Father));
        PeopleFixture.Check(before == world.StateHash() && randomBefore == world.Checkpoint().RandomState,
            "actual command retry cannot reroll alleles, consume RNG or allocate another pregnancy");
        PeoplePublicFixture.Accept(world, new("people.change-gestation", 0, "law", Value: 120, Data: "birth.gestation"));
        PeoplePublicFixture.Accept(world, new("people.disable-future-birth", 0, "law", Value: 0, Data: "birth.enabled"));
        var restored = GameCodec.Decode(GameCodec.Encode(world));
        var child = PeoplePublicFixture.Birth(world, pregnancy.Id); var other = PeoplePublicFixture.Birth(restored, pregnancy.Id);
        state = world.Checkpoint();
        PeopleFixture.Check(child.Aptitudes.Take(5).SequenceEqual(captured.BodyFoundations) && child.Roots.SequenceEqual(captured.ElementRoots) &&
            child.Aptitudes[5] == state.PeopleDomain.Souls.Single(s => s.SoulId == child.SoulId).Foundation!.Value,
            "actual birth keeps its original five body foundations and assigns the actual soul's separate foundation");
        PeopleFixture.Check(child.ParentOne == captured.ParentOne.PersonId && child.ParentTwo == captured.ParentTwo.PersonId &&
            state.PeopleDomain.Inheritance.Births.Single().PersonId == child.Id && child.Born == pregnancy.Due,
            "birth links the biological parent DAG and frozen conception despite later law changes");
        var profile = state.PeopleDomain.Profiles.Single(p => p.PersonId == child.Id);
        PeopleFixture.Check(child.Skills.Count == 0 && child.Understanding.Count == 0 && child.Realm == Realm.Mortal && child.Formations.Count == 0 &&
            profile.Learning.Length == 0 && profile.Development.Learned.All(v => v == 0), "neither parents nor assigned soul restore free proficiency or cultivation");
        PeopleFixture.Check(world.StateHash() == restored.StateHash() && child.Aptitudes.SequenceEqual(other.Aptitudes) &&
            original == JsonSerializer.Serialize(state.PeopleDomain.Inheritance.Conceptions.Single()),
            "pending-pregnancy production save continues to the same body, soul and source history");
        var detail = world.PeopleDetail(child.Id)!; before = world.StateHash(); detail.Innate!.Foundations[0] = 1;
        PeopleFixture.Check(before == world.StateHash(), "observer detail is a detached pure projection");
        var saved = GameCodec.Decode(GameCodec.Encode(world));
        PeopleFixture.Check(saved.StateHash() == world.StateHash() && saved.Checkpoint().PeopleDomain.SettledThroughMonth == pregnancy.Due,
            "strict production save validates the complete people domain and automatic monthly cursor");
    }
}

public sealed class PeopleWorldReproductionPolicyScenarios : IFeatureScenario
{
    public string Name => "INV-011/012 people public: both-parent Golden Core boundary and explicit martial-law override";

    public void Run()
    {
        var world = PeoplePublicFixture.Fresh(); var initial = world.Checkpoint(); var fatherId = initial.People.Single(p => !p.Gestates).Id;
        world = PeoplePublicFixture.Configure(world, state =>
        {
            var father = state.People.Single(p => p.Id == fatherId); PeoplePublicFixture.CurrentRealm(state, father, Realm.Foundation); father.Martial = 10;
        });
        var motherId = world.Checkpoint().People.Single(p => p.Gestates).Id; var before = world.StateHash();
        PeopleFixture.Check(!world.Execute(new("people.ineligible-father", motherId, "conceive", fatherId)).Accepted && world.StateHash() == before &&
            world.Checkpoint().Pregnancies.Count == 0, "father below Golden Core is rejected atomically even with tenth-stage martial power");
        var core = PeoplePublicFixture.Configure(world, state => PeoplePublicFixture.CurrentRealm(state,
            state.People.Single(p => p.Id == fatherId), Realm.GoldenCore));
        PeoplePublicFixture.Conceive(core);
        PeopleFixture.Check(core.Checkpoint().PeopleDomain.Inheritance.Conceptions.Length == 1, "exact Golden Core father satisfies the actual default boundary");
        PeoplePublicFixture.Accept(world, new("people.explicit-martial-law", 0, "law", Value: 1, Data: "birth.martialEquivalent"));
        PeoplePublicFixture.Conceive(world);
        PeopleFixture.Check(world.Checkpoint().Pregnancies.Count == 1 && world.Checkpoint().Laws["birth.bothParents"] == 1,
            "martial equivalence applies only after an explicit saved world-law edit and never removes the both-parent rule");
    }
}

public sealed class PeopleWorldLearningVisibilityScenarios : IFeatureScenario
{
    public string Name => "INV-008/015 people public: one real study credit and hidden-person source noninterference";

    public void Run()
    {
        var world = PeoplePublicFixture.Fresh(); var state = world.Checkpoint(); var actorId = state.People[0].Id; var otherId = state.People[1].Id;
        PeoplePublicFixture.Accept(world, new("people.possess", actorId, "possess"));
        state = world.Checkpoint(); var book = state.Items.First(i => i.Owner == actorId && i.Definition == "path.1.0");
        var oldUnderstanding = state.People.Single(p => p.Id == actorId).Understanding[book.Definition];
        var genome = JsonSerializer.Serialize(state.PeopleDomain.Genomes.Single(g => g.PersonId == actorId));
        var command = new GameCommand("people.study", actorId, "study", book.Id);
        PeoplePublicFixture.Accept(world, command); world.AdvanceTo(1); state = world.Checkpoint();
        var actor = state.People.Single(p => p.Id == actorId); var gained = actor.Understanding[book.Definition] - oldUnderstanding;
        var learning = state.PeopleDomain.Profiles.Single(p => p.PersonId == actorId).Learning;
        var activity = state.Knowledge.Activities.Single(a => a.ActorId == actorId && a.Kind == KnowledgeActivityKind.Study);
        PeopleFixture.Check(gained > 0 && learning.Length == 1 && learning[0].UnderstandingChange == gained && learning[0].LearnedChange == 0 &&
            learning[0].Kind == PeopleLearningKind.Reflection && learning[0].CompletedMonths == activity.DueMonth - activity.StartedMonth &&
            learning[0].EventId == $"knowledge-learning:{actor.BodyId}:{activity.Id}",
            "public study projects exactly one source-backed actual understanding gain with its real duration and kind");
        var self = world.PeopleDetail(actorId)!;
        PeopleFixture.Check(!self.Observer && self.Innate is null && self.GeneticOrigin is null && self.ActualSoul is null && self.ActualBody is null &&
            self.CurrentTalents[7].Understanding == gained && self.CurrentTalents.All(t => t.Learned == 0) && self.LearningHistory!.Length == 1,
            "self sees actual current expression without hidden innate/genetic/soul truth or reading-created mastery");
        PeopleFixture.Check(genome == JsonSerializer.Serialize(state.PeopleDomain.Genomes.Single(g => g.PersonId == actorId)), "education never rewrites the transmitted genome");
        var before = world.StateHash(); PeoplePublicFixture.Accept(world, command);
        PeopleFixture.Check(before == world.StateHash(), "study command retry after completion cannot credit learning twice");
        var personal = JsonSerializer.Serialize(world.PeopleDetail(otherId));
        var counterfactual = PeoplePublicFixture.Configure(world, snapshot =>
        {
            var other = snapshot.People.Single(p => p.Id == otherId); other.Aptitudes[0] = 10_000;
            snapshot.PeopleDomain = snapshot.PeopleDomain with
            {
                Profiles = snapshot.PeopleDomain.Profiles.Select(p => p.PersonId == otherId ? p with
                    { Innate = p.Innate with { Foundations = p.Innate.Foundations.Select((v, i) => i == 0 ? 10_000 : v).ToArray() } } : p).ToArray(),
                Genomes = snapshot.PeopleDomain.Genomes.Select(g => g.PersonId == otherId ? g with { Genes = g.Genes.Select(pair =>
                    pair.GeneId.StartsWith("body.0.", StringComparison.Ordinal) ? pair with { FirstAllele = 10_000, SecondAllele = 10_000 } : pair).ToArray() } : g).ToArray()
            };
            var source = new PeopleSource("counterfactual.hidden-latent", PeopleSourceKind.Genetic, "hidden declared body potential", snapshot.Month);
            var latent = Traits.Acquire(snapshot.PeopleDomain.Traits, new(snapshot.NextId++, otherId, "body.meridian", "counterfactual.latent", source, [TraitFact.BodyPrototype]));
            PeopleFixture.Check(latent.Accepted, "counterfactual latent trait fixture");
            snapshot.PeopleDomain = snapshot.PeopleDomain with { Traits = latent.State };
        });
        PeopleFixture.Check(personal == JsonSerializer.Serialize(counterfactual.PeopleDetail(otherId)),
            "different hidden innate genes and latent traits cannot alter unchanged personal observations");
        before = world.StateHash(); world.PeopleDetail(actorId); world.PeopleDetail(otherId);
        PeopleFixture.Check(before == world.StateHash() && GameCodec.Decode(GameCodec.Encode(world)).StateHash() == before,
            "personal views and production save cannot create another learning or observation event");
    }
}

public sealed class PeopleWorldTraceAndSplitScenarios : IFeatureScenario
{
    public string Name => "INV-014/024 people public: coherent ready ascension, source capture and costed independent split";

    public void Run()
    {
        var world = PeoplePublicFixture.Fresh(new() { ["founder.ascensionProtection"] = 0, ["birth.enabled"] = 0 });
        var id = world.Checkpoint().People[1].Id;
        world = PeoplePublicFixture.Configure(world, state =>
        {
            var person = state.People.Single(p => p.Id == id); PeoplePublicFixture.CurrentRealm(state, person, Realm.HalfImmortal, complete: true);
            person.Place = 99; person.KnownPlaces.Add(99); person.KnownPlaces.Sort();
        });
        var beforeState = world.Checkpoint(); var soulId = beforeState.People.Single(p => p.Id == id).SoulId;
        var command = new GameCommand("people.ascend", id, "ascend"); PeoplePublicFixture.Accept(world, command);
        var state = world.Checkpoint(); var person = state.People.Single(p => p.Id == id);
        var root = state.Souls.Single(s => s.Id == soulId); var child = state.Souls.Single(s => s.Parent == root.Id);
        var parentTrace = state.PeopleDomain.Souls.Single(s => s.SoulId == root.Id); var childTrace = state.PeopleDomain.Souls.Single(s => s.SoulId == child.Id);
        var capture = parentTrace.History.Single(h => h.Kind == SoulTraceActionKind.LifeCapture && h.RelatedIdentity == id);
        PeopleFixture.Check(person.Status == LifeStatus.Ascended && state.DeathCount == beforeState.DeathCount && root.Status == "ascended" &&
            child.Route == 13 && child.CurrentPerson == 0 && state.People.Count == 2 && state.Souls.Count == 3,
            "public ascension is a world exit and one waiting independent soul, without death or phantom population");
        PeopleFixture.Check(capture.DomainAmounts is { Length: 6 } && parentTrace.Values[3].HasEvidence &&
            parentTrace.Values[3].DefinitionIds.Any(r => r.StartsWith("knowledge:recipe:", StringComparison.Ordinal)),
            "life-end craft/technique knowledge traces refer to actual memory revisions even without practical mastery");
        for (var domain = 0; domain < 6; domain++)
            PeopleFixture.Check(parentTrace.Values[domain].Preserved + childTrace.Values[domain].Preserved ==
                capture.DomainAmounts![domain] * (100 - state.Laws["soul.splitCost"]) / 100,
                "recorded pre-split domain preservation equals retained parent plus child plus consumed cost");
        PeopleFixture.Check(childTrace.ParentSoulId == root.Id && childTrace.Foundation!.Value < parentTrace.Foundation!.Value &&
            childTrace.Values.All(v => v.Accessible == 0 && v.Integrated == 0), "split preserves independent identity and bounded foundations without free current-life knowledge");
        var before = world.StateHash(); PeoplePublicFixture.Accept(world, command);
        PeopleFixture.Check(before == world.StateHash() && GameCodec.Decode(GameCodec.Encode(world)).StateHash() == before,
            "ascension replay and production save cannot duplicate trace capture, split souls or property");
    }
}

public sealed class PeopleWorldReincarnationScenarios : IFeatureScenario
{
    public string Name => "INV-004/014/024 people public: eligible natural death, waiting-soul birth, real awakening and taught integration";

    public void Run()
    {
        var world = PeoplePublicFixture.Fresh(new() { ["soul.crossing"] = 0, ["birth.maturity"] = 1 });
        var state = world.Checkpoint(); var father = state.People.Single(p => !p.Gestates); var mother = state.People.Single(p => p.Gestates);
        var foundation = state.PeopleDomain.Souls.Single(s => s.SoulId == father.SoulId).Foundation!;
        PeoplePublicFixture.Accept(world, new("people.control-mother", mother.Id, "possess"));
        var pregnancy = PeoplePublicFixture.Conceive(world);
        var frozen = JsonSerializer.Serialize(world.Checkpoint().PeopleDomain.Inheritance.Conceptions.Single());
        world = PeoplePublicFixture.Configure(world, snapshot => snapshot.People.Single(p => p.Id == father.Id).Lifespan = 1 - father.Born);
        world.AdvanceTo(1); state = world.Checkpoint();
        var dead = state.People.Single(p => p.Id == father.Id); var waiting = state.Souls.Single(s => s.Id == father.SoulId);
        var trace = state.PeopleDomain.Souls.Single(s => s.SoulId == father.SoulId);
        PeopleFixture.Check(dead.Status == LifeStatus.Dead && waiting.Route == 1 && waiting.Status == "waiting" && trace.CurrentPersonId == 0 &&
            trace.History.Count(h => h.Kind == SoulTraceActionKind.LifeCapture) == 1 && trace.Values.All(v => v.Accessible == 0 && v.Integrated == 0),
            "the actual lifespan boundary captures one qualified soul and distinct actual source domains");
        PeopleFixture.Check(trace.Values[2].Preserved > 0 && trace.Values[3].Preserved > 0 && trace.Values[5].Preserved == 0,
            "declared understanding is preserved while absent embodied mastery remains zero, rather than cloning one count into all domains");
        world = GameCodec.Decode(GameCodec.Encode(world));
        var child = PeoplePublicFixture.Birth(world, pregnancy.Id); state = world.Checkpoint();
        var newbornTrace = state.PeopleDomain.Souls.Single(s => s.SoulId == child.SoulId);
        PeopleFixture.Check(child.SoulId == father.SoulId && child.Id != father.Id && child.BodyId != father.BodyId && child.Aptitudes[5] == foundation.Value &&
            child.ParentOne == mother.Id && child.ParentTwo == father.Id && child.Skills.Count == 0 && child.Understanding.Count == 0 && child.Realm == Realm.Mortal,
            "waiting-soul birth preserves soul identity/foundation while forming a new biological child without ownership or mastery restoration");
        PeopleFixture.Check(newbornTrace.Values.All(v => v.Accessible == 0 && v.Integrated == 0) && frozen == JsonSerializer.Serialize(state.PeopleDomain.Inheritance.Conceptions.Single()),
            "actual birth resets trace access without rewriting its prenatal body-parent evidence");
        PeoplePublicFixture.Accept(world, new("people.release-mother", 0, "observe"));
        world.AdvanceTo(pregnancy.Due + 1); state = world.Checkpoint();
        var awakened = state.PeopleDomain.Souls.Single(s => s.SoulId == child.SoulId);
        PeopleFixture.Check(awakened.Values[2].Accessible > 0 && awakened.Values[2].Integrated == 0 &&
            state.Knowledge.Memories.All(m => m.PersonId != child.Id) && state.Knowledge.Mastery.All(m => m.PersonId != child.Id),
            "one actual matured dream opens preserved clues without producing a knowledge memory or skill");
        PeoplePublicFixture.Accept(world, new("people.inspect-reborn-self", child.Id, "possess"));
        var self = world.PeopleDetail(child.Id)!;
        PeopleFixture.Check(self.ActualSoul is null && self.KnownTraces.All(t => t.DefinitionIds.Length == 0),
            "weak sealed dream fragments do not disclose every preserved technique, edition or past memory reference");
        PeoplePublicFixture.Accept(world, new("people.finish-self-inspection", 0, "observe"));
        var currentMother = state.People.Single(p => p.Id == mother.Id);
        if (currentMother.Action != ActionKind.Idle) PeoplePublicFixture.Accept(world, new("people.cancel-mother-work", mother.Id, "cancel"));
        PeoplePublicFixture.Accept(world, new("people.actual-parent-lesson", mother.Id, "teach", child.Id, 1, "path.1.0"));
        world = GameCodec.Decode(GameCodec.Encode(world)); world.AdvanceTo(world.Month + 1); state = world.Checkpoint();
        var learned = state.PeopleDomain.Profiles.Single(p => p.PersonId == child.Id);
        var integrated = state.PeopleDomain.Souls.Single(s => s.SoulId == child.SoulId);
        PeopleFixture.Check(learned.Learning.Length == 1 && learned.Learning[0].Kind == PeopleLearningKind.Teaching &&
            state.Knowledge.Memories.Any(m => m.PersonId == child.Id && m.WorkId == "path.1.0") &&
            state.Knowledge.Mastery.All(m => m.PersonId != child.Id) && integrated.Values[2].Integrated > 0 &&
            integrated.Values[2].Integrated <= integrated.Values[2].Accessible,
            "taught integration is backed by actual parent time and personal learning, without inherited mastery");
        PeopleFixture.Check(state.People.Single(p => p.Id == child.Id).Realm == Realm.Mortal && state.Items.All(i => i.Owner != child.Id),
            "old realm and physical possessions do not return through trace integration");
        PeopleFixture.Check(GameCodec.Decode(GameCodec.Encode(world)).StateHash() == world.StateHash(), "soul continuity and actual integration source history pass strict world save");
    }
}

public sealed class PeopleWorldMonthlyTraitScenarios : IFeatureScenario
{
    public string Name => "INV-006/015 people public: actual environmental damage, timed recovery and derived-marker release";

    public void Run()
    {
        var world = PeoplePublicFixture.Fresh(new() { ["hazard.turbidity"] = 0, ["hazard.structureDamage"] = 2 });
        var personId = world.Checkpoint().People[0].Id;
        PeoplePublicFixture.Accept(world, new("people.control-practitioner", personId, "possess"));
        PeoplePublicFixture.Accept(world, new("people.exposed-practice", personId, "cultivate", Data: "path.10.0"));
        world.AdvanceTo(1); var state = world.Checkpoint(); var person = state.People.Single(p => p.Id == personId);
        var marker = state.PeopleDomain.Traits.Traits.Single(t => t.PersonId == personId && t.DefinitionId == "scar.foundation");
        PeopleFixture.Check(marker.Status == TraitStatus.Active && person.StructureDamage > 0 && state.Cultivation[person.BodyId].Damage > 0 &&
            Traits.ActiveEffects(state.PeopleDomain.Traits, personId, 1).Length == 0, "one real structural injury produces a marker without another mechanical penalty");
        var before = world.StateHash(); world.AdvanceTo(1);
        PeopleFixture.Check(before == world.StateHash(), "repeating the same logical boundary cannot produce another injury marker or trace event");
        PeoplePublicFixture.Accept(world, new("people.actual-recovery", personId, "cultivationrecover")); world.AdvanceTo(2); state = world.Checkpoint();
        marker = state.PeopleDomain.Traits.Traits.Single(t => t.Id == marker.Id);
        PeopleFixture.Check(state.People.Single(p => p.Id == personId).StructureDamage == 0 && marker.Status == TraitStatus.Released &&
            marker.History[^1].Kind == TraitChangeKind.Released && world.PeopleDetail(personId)!.KnownTraits.Any(t => t.DefinitionId == "scar.foundation" && t.Status == TraitStatus.Released),
            "actual paid-time recovery releases the injury marker and its self observation while retaining causal history");
        var restored = GameCodec.Decode(GameCodec.Encode(world));
        PeopleFixture.Check(restored.Checkpoint().PeopleDomain.SettledThroughMonth == 2 && restored.StateHash() == world.StateHash(),
            "automatic monthly cursor and source-backed release survive strict production save");
    }
}
