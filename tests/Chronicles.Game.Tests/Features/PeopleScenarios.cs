using System.Text.Json;
using Chronicles.Game.People;

namespace Chronicles.Game.Tests.Features;

internal static class PeopleFixture
{
    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static PeopleSource Source(string id, PeopleSourceKind kind = PeopleSourceKind.Founding, long month = 0, long soulId = 0) =>
        new(id, kind, $"evidence:{id}", month, SoulId: soulId);

    internal static InnateProfile Innate(int value = 5000) => new(Enumerable.Repeat(value, 6).ToArray(), Enumerable.Repeat(value, 5).ToArray(), [Source("founding")]);

    internal static ConceptionContext Conception(long id = 30)
    {
        var model = GeneticModel.Provisional();
        ParentGenome Parent(long personId, int one, int two) => new(personId, personId + 100, "human",
            model.Genes.Select(g => new GenePair(g.Id, one, two, Source($"body.{personId}.{g.Id}", PeopleSourceKind.Genetic))).ToArray(), []);
        return new(id, 0, 1, "human", Parent(1, 2000, 8000), Parent(2, 4000, 6000), model,
            model.Genes.Select(g => new AlleleSelection(g.Id, 1, 0, 0)).ToArray());
    }

    internal static T RoundTrip<T>(T value)
    {
        var json = JsonSerializer.Serialize(value);
        var restored = JsonSerializer.Deserialize<T>(json) ?? throw new InvalidDataException("Missing module round trip.");
        Check(json == JsonSerializer.Serialize(restored), "module serialization must preserve deterministic state");
        return restored;
    }

    internal static void Invalid(Action action, string message)
    {
        var rejected = false;
        try { action(); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, message);
    }
}

public sealed class PeopleAptitudeScenarios : IFeatureScenario
{
    private static readonly string[] ExpectedCognitionNames = ["관측·주의", "작업 기억", "기억 인출", "인과 추론", "대안 비교", "장기 계획", "자기 점검", "사회 추론", "정서·도심 조절", "영역 숙련"];
    public string Name => "INV-006/015 people: six foundations, 24 talents, canonical ten cognition mappings and single cause effects";

    public void Run()
    {
        var innate = PeopleFixture.Innate();
        var context = new AptitudeContext(innate, TalentDevelopment.Empty(), 10_000, 10_000, [], new());
        var serialized = JsonSerializer.Serialize(context);
        var result = Aptitudes.Assess(context);
        PeopleFixture.Check(result.Talents.Length == 24 && result.Cognition.Length == 10, "complete derivation catalogs");
        PeopleFixture.Check(result.Talents.All(t => t.Innate == 5000 && t.Learned == 0 && t.Understanding == 0 && t.CurrentExpression == 3000), "innate potential is separate from learning and expression");
        PeopleFixture.Check(AptitudeCatalog.Cognition.Select(c => c.Name).SequenceEqual(ExpectedCognitionNames), "canonical cognition labels and order");
        PeopleFixture.Check(AptitudeCatalog.Talents.All(t => t.Inputs.Sum(i => i.BasisPoints) == 10_000 && t.LearningPath.Length > 0) &&
            AptitudeCatalog.Cognition.All(c => c.Inputs.Sum(i => i.BasisPoints) == 10_000 && c.PracticeTalents.Length > 0), "every derived value has an explicit source and learning path");
        var learned = TalentDevelopment.Empty(); learned.Learned[9] = 10_000;
        var trained = Aptitudes.Assess(context with { Development = learned });
        PeopleFixture.Check(trained.Talents[9].Innate == 5000 && trained.Talents[9].CurrentExpression > result.Talents[9].CurrentExpression &&
            trained.Talents[8] == result.Talents[8], "education changes its actual domain without rewriting innate potential");
        var fatigued = Aptitudes.Assess(context with { PhysicalCondition = 5000 });
        PeopleFixture.Check(fatigued.Talents[9].CurrentExpression == 1500 && fatigued.Talents[11] == result.Talents[11], "typed current condition affects the relevant expression");
        PeopleFixture.Check(serialized == JsonSerializer.Serialize(context) && JsonSerializer.Serialize(result) == JsonSerializer.Serialize(Aptitudes.Assess(context)), "repeated assessment is pure and deterministic");

        var target = new EffectTarget(EffectDomain.Talent, 7); var genetic = PeopleFixture.Source("same-cause", PeopleSourceKind.Genetic);
        PeopleEffect Effect(string id, PeopleSourceKind kind, EffectRole role = EffectRole.Contribution) => new(id, target, EffectOperation.Add, 500, genetic with { Kind = kind }, role);
        var effects = PeopleEffects.Resolve([Effect("inheritance", PeopleSourceKind.Genetic), Effect("trace", PeopleSourceKind.SoulTrace), Effect("trait", PeopleSourceKind.Trait), Effect("marker", PeopleSourceKind.Trait, EffectRole.DerivedMarker)]);
        PeopleFixture.Check(effects.Applied.Length == 1 && effects.Apply(target, 5000) == 5500, "shared inheritance, trace and trait cause applies once");
        var included = PeopleEffects.Resolve([Effect("baseline", PeopleSourceKind.Genetic, EffectRole.IncludedInBaseline), Effect("derived", PeopleSourceKind.Trait)]);
        PeopleFixture.Check(included.Apply(target, 5000) == 5000, "baseline-included cause cannot feed back through a trait");
        PeopleFixture.Invalid(() => PeopleEffects.Resolve([Effect("a", PeopleSourceKind.Genetic), Effect("b", PeopleSourceKind.Trait) with { Amount = 900 }]), "conflicting representations require explicit source reconciliation");
        var stack = PeopleEffects.Resolve([Effect("weak", PeopleSourceKind.Genetic) with { Source = genetic with { CauseId = "a" }, ExclusiveGroup = "meridian" },
            Effect("strong", PeopleSourceKind.Trait) with { Amount = 1000, Source = genetic with { CauseId = "b" }, ExclusiveGroup = "meridian" }]);
        PeopleFixture.Check(stack.Apply(target, 5000) == 6000, "exclusive effects use the strongest applicable contribution");
        PeopleFixture.RoundTrip(context); PeopleFixture.RoundTrip(effects);
    }
}

public sealed class PeopleTraitScenarios : IFeatureScenario
{
    public string Name => "INV-008/015 people: forty traits, lifecycle, conflicts, expiry, history and observed potential boundaries";

    public void Run()
    {
        PeopleFixture.Check(TraitCatalog.All.Count == 40 && TraitCatalog.All.GroupBy(t => t.Category).Count() == 10 && TraitCatalog.All.GroupBy(t => t.Category).All(g => g.Count() == 4), "ten categories with four actual definitions each");
        var source = PeopleFixture.Source("meridian", PeopleSourceKind.Genetic);
        var acquire = new TraitAcquireContext(100, 1, "body.meridian", "acquire", source, [TraitFact.BodyPrototype], ExpiresMonth: 5);
        var result = Traits.Acquire(TraitLedger.Empty(), acquire);
        PeopleFixture.Check(result.Accepted && Traits.ActiveEffects(result.State, 1, 0).Length == 0, "acquisition records latent potential");
        var missing = Traits.Transition(result.State, new(100, "missing", source, []), TraitChangeKind.Awakened);
        PeopleFixture.Check(!missing.Accepted && ReferenceEquals(missing.State, result.State), "failed awakening is atomic");
        result = Traits.Transition(result.State, new(100, "awaken", source, [TraitFact.MeridianFit]), TraitChangeKind.Awakened);
        PeopleFixture.Check(result.Accepted && Traits.ActiveEffects(result.State, 1, 4).Length == 1 && Traits.ActiveEffects(result.State, 1, 5).Length == 0, "active effects expire at exact simulation month");
        PeopleFixture.Check(!Traits.Acquire(result.State, acquire with { Id = 101, EventId = "duplicate" }).Accepted, "unique trait stacking is enforced");
        var expired = Traits.Transition(result.State, new(100, "expire", source with { Month = 5 }, []), TraitChangeKind.Expired);
        PeopleFixture.Check(expired.Accepted && expired.State.Traits.Single().History.Length == 3, "expiration keeps acquired and awakened history");
        var state = TraitLedger.Empty();
        foreach (var pair in new[] { (Id: 200L, Definition: "bloodline.harmony", Fact: TraitFact.BloodlineHarmonized), (Id: 201L, Definition: "bloodline.conflict", Fact: TraitFact.BloodlineConflicting) })
        {
            var a = Traits.Acquire(state, new(pair.Id, 1, pair.Definition, $"a{pair.Id}", source, [pair.Fact]));
            PeopleFixture.Check(a.Accepted, "bloodline trait acquisition"); state = a.State;
        }
        state = Traits.Transition(state, new(200, "harmony", source, [TraitFact.BloodlineHarmonized]), TraitChangeKind.Awakened).State;
        PeopleFixture.Check(!Traits.Transition(state, new(201, "conflict", source, [TraitFact.BloodlineConflicting]), TraitChangeKind.Awakened).Accepted, "contradictory bloodline structures cannot be active together");
        PeopleFixture.Check(Traits.ActiveEffects(state, 1, 0).Length == 0, "derived structure marker does not create another mechanical effect");
        state = Traits.Transition(state, new(200, "release", source, [TraitFact.Resolved]), TraitChangeKind.Released).State;
        PeopleFixture.Check(Traits.Transition(state, new(201, "conflict", source, [TraitFact.BloodlineConflicting]), TraitChangeKind.Awakened).Accepted, "release removes active conflict but preserves history");

        var projectionContext = new TraitQueryContext(2, 1, 4, false, [new(2, 1, "body.meridian", TraitStatus.Latent, 0, 0, null, "heard-at-0")]);
        var seen = JsonSerializer.Serialize(Traits.Project(result.State, projectionContext));
        PeopleFixture.Check(seen == JsonSerializer.Serialize(Traits.Project(expired.State, projectionContext)), "same observed snapshot cannot leak hidden trait changes");
        PeopleFixture.Check(Traits.Project(result.State, projectionContext with { Observations = [] }).Length == 0, "self or other latent potential requires actual observed evidence");
        var founder = Traits.Acquire(TraitLedger.Empty(), new(300, 1, "status.dynasty-founder", "found", source, [TraitFact.ActualDynastyFounded], RelatedEntityId: 9));
        founder = Traits.Transition(founder.State, new(300, "activate-title", source, [TraitFact.ActualDynastyFounded]), TraitChangeKind.Awakened);
        PeopleFixture.Check(founder.Accepted && Traits.ActiveEffects(founder.State, 1, 0).Length == 0 && TraitCatalog.Get("status.dynasty-founder").Purpose == TraitPurpose.HistoricalTitle, "historical founder title grants no treasury or authority");
        PeopleFixture.Check(!Traits.Acquire(TraitLedger.Empty(), new(400, 1, "fortune.good", "fortune", source, [TraitFact.GoodFortuneEvent])).Accepted, "fortune requires a bounded actual event and expiry");
        PeopleFixture.RoundTrip(expired.State);
    }
}

public sealed class PeopleInheritanceScenarios : IFeatureScenario
{
    public string Name => "INV-004/011/012/014 people: frozen allele inheritance, prenatal body evidence and birth-time soul assignment";

    public void Run()
    {
        var context = PeopleFixture.Conception();
        var result = Inheritance.Capture(InheritanceState.Empty(), context);
        PeopleFixture.Check(result.Accepted && result.State.Conceptions.Single().BodyFoundations.All(v => v == 6000), "actual selected alleles determine body foundations");
        var json = JsonSerializer.Serialize(result.State);
        var replay = Inheritance.Capture(result.State, context with { Selections = context.Selections.Select(s => s with { ParentOneAllele = 0 }).ToArray() });
        PeopleFixture.Check(replay.Accepted && json == JsonSerializer.Serialize(replay.State), "conception replay cannot reroll after selection changed");
        var parentValue = result.State.Conceptions[0].ParentOne.Genes[0].FirstAllele;
        context.ParentOne.Genes[0] = context.ParentOne.Genes[0] with { FirstAllele = 10_000 };
        PeopleFixture.Check(result.State.Conceptions[0].ParentOne.Genes[0].FirstAllele == parentValue, "parent conception snapshot is insulated from later development");
        var prenatal = new DevelopmentRecord("nutrition", DevelopmentStage.Prenatal, new(HeritableTargetKind.BodyFoundation, 2), 500,
            PeopleFixture.Source("nutrition", PeopleSourceKind.Prenatal, 2));
        result = Inheritance.Develop(result.State, 30, prenatal);
        PeopleFixture.Check(result.Accepted && result.State.Conceptions[0].BodyFoundations[2] == 6500, "prenatal development has separate recorded evidence");
        var before = JsonSerializer.Serialize(result.State);
        PeopleFixture.Check(!Inheritance.Develop(result.State, 30, prenatal with { EventId = "unassigned", Source = PeopleFixture.Source("soul", PeopleSourceKind.SoulTrace, 2, 77) }).Accepted && before == JsonSerializer.Serialize(result.State), "unassigned prenatal soul has no effect and rejection is atomic");
        var birth = new BirthContext(40, 30, 3, 103, 77, 9, 9000, PeopleFixture.Source("soul77", PeopleSourceKind.SoulTrace, 9, 77));
        result = Inheritance.BindBirth(PeopleFixture.RoundTrip(result.State), birth);
        var newborn = result.State.Births.Single();
        PeopleFixture.Check(result.Accepted && newborn.Innate.Foundations[2] == 6500 && newborn.Innate.Foundations[5] == 9000 && result.State.Conceptions[0].Development.Length == 1, "soul foundation is assigned at birth without rewriting prenatal body history");
        before = JsonSerializer.Serialize(result.State);
        PeopleFixture.Check(Inheritance.BindBirth(result.State, birth with { SoulFoundation = 1 }).Accepted && before == JsonSerializer.Serialize(result.State), "birth retry preserves the actual one-time result");
        PeopleFixture.Check(!Inheritance.Develop(result.State, 30, prenatal with { EventId = "late" }).Accepted, "postnatal learning cannot rewrite the frozen prenatal record");
        var inherited = Inheritance.TransmissibleGenome(newborn, result.State.Conceptions[0]);
        PeopleFixture.Check(inherited.Genes.All(g => g.Source.SoulId == 0) && inherited.Genes.Length == 20, "child's transmissible genome excludes soul and education");
        var invalidMarker = context.ParentOne with { Markers = [new("free-technique", "soul.insight", context.Model.Genes[0].Id, 0, PeopleFixture.Source("not-biological"))] };
        PeopleFixture.Invalid(() => Inheritance.Capture(InheritanceState.Empty(), context with { ParentOne = invalidMarker }), "soul traces cannot be inherited as biological markers");
        var mutationContext = PeopleFixture.Conception(31);
        mutationContext.Selections[0] = mutationContext.Selections[0] with { MutationDelta = 1000, MutationSource = PeopleFixture.Source("mutation", PeopleSourceKind.Mutation) };
        var mutation = Inheritance.Capture(InheritanceState.Empty(), mutationContext).State.Conceptions.Single();
        PeopleFixture.Check(mutation.Genes.Single(g => g.GeneId == mutationContext.Selections[0].GeneId).ParentOneAllele == 9000, "defined mutation changes the transmitted unit rather than granting a recurring generation bonus");
        PeopleFixture.RoundTrip(result.State);
    }
}

public sealed class PeopleSoulTraceScenarios : IFeatureScenario
{
    public string Name => "INV-004/014/024 people: six trace domains, awakening, integration, crossing loss and conserved split branches";

    public void Run()
    {
        var source = PeopleFixture.Source("previous-life", PeopleSourceKind.SoulTrace, soulId: 77);
        var soul = new SoulTraceProfile(77, 0, 77, 3, Enum.GetValues<SoulTraceDomain>().Select(d =>
            new SoulTraceValue(d, 6000, 1000, 0, true, [$"shared-memory.{(int)d}"], true, source)).ToArray(), []);
        soul.Validate(); var before = JsonSerializer.Serialize(soul);
        var awakening = new SoulTraceAction("dream", SoulTraceActionKind.Awakening, 1, source with { Month = 1 }, SoulTraceDomain.TechniqueSpellPillCraftKnowledge, 2000);
        var result = SoulTraces.Awaken(soul, awakening);
        PeopleFixture.Check(result.Accepted && result.State.Values[3].Accessible == 3000 && result.State.Values[3].Integrated == 0, "dream opens existing preserved clues only");
        result = SoulTraces.Integrate(result.State, new("reflection", SoulTraceActionKind.Integration, 2, source with { Month = 2 }, SoulTraceDomain.TechniqueSpellPillCraftKnowledge, 5000));
        PeopleFixture.Check(result.Accepted && result.State.Values[3].Integrated == 3000, "integration cannot exceed access");
        var projection = new TraceQueryContext(9, 3, 3, false, []);
        PeopleFixture.Check(SoulTraces.Project(soul, projection).Length == 0 && SoulTraces.Project(result.State, projection).Length == 0 && before == JsonSerializer.Serialize(soul), "hidden preservation and awakening cannot leak to another observer or mutate through queries");
        result = SoulTraces.ResetAtBirth(result.State, new("birth-80", SoulTraceActionKind.BirthReset, 3, source with { Month = 3 }, RelatedIdentity: 80));
        PeopleFixture.Check(result.Accepted && result.State.Values.All(v => v.Preserved == 6000 && v.Accessible == 0 && v.Integrated == 0), "new body does not automatically restore old realm, techniques or combat experience");
        var split = SoulTraces.Split(result.State, 78, new("split", SoulTraceActionKind.SplitSource, 4, source with { Month = 4 }, RelatedIdentity: 78,
            DomainAmounts: Enumerable.Repeat(5000, 6).ToArray()), 3000);
        PeopleFixture.Check(split.Accepted && split.Child is { SoulId: 78, ParentSoulId: 77, CurrentPersonId: 0 } && split.Child.Values.All(v => v.Accessible == 0 && v.Integrated == 0), "split child has independent identity and no present-life mastery");
        for (var domain = 0; domain < 6; domain++)
            PeopleFixture.Check(split.Source.Values[domain].Preserved + split.Child!.Values[domain].Preserved + split.Lost[domain] == 6000, "split preserves explicit allocation plus consumed cost per domain");
        var damageAction = new SoulTraceAction("crossing", SoulTraceActionKind.CrossingDamage, 5, source with { Month = 5 }, DomainAmounts: Enumerable.Repeat(5000, 6).ToArray());
        var damaged = SoulTraces.Damage(split.Child!, damageAction);
        PeopleFixture.Check(damaged.Accepted && damaged.State.Values.All(v => v.Preserved == 1050), "crossing damage applies once to actual preservation");
        PeopleFixture.Check(JsonSerializer.Serialize(SoulTraces.Damage(damaged.State, damageAction).State) == JsonSerializer.Serialize(damaged.State), "crossing retry cannot damage twice after save/load");
        var noEvidence = SoulTraceProfile.WithoutEvidence(79);
        PeopleFixture.Check(!noEvidence.Values[0].HasEvidence && !SoulTraces.Awaken(noEvidence, awakening).Accepted, "missing history differs from measured trace loss");
        PeopleFixture.RoundTrip(damaged.State).Validate(); PeopleFixture.RoundTrip(split.Source).Validate();
    }
}
