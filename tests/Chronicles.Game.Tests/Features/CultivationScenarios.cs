using System.Text.Json;
using Chronicles.Game.Cultivation;

namespace Chronicles.Game.Tests.Features;

internal static class CultivationFixture
{
    internal static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    internal static CultivationActor Actor(Realm realm = Realm.Qi) => new()
    { PersonId = 1, BodyId = 2, SoulId = 3, Realm = realm, Layer = 1, Health = 100, Status = LifeStatus.Alive };
    internal static CultivationContext Context(long month = 0, CultivationPolicy? policy = null) => new()
    {
        Month = month, Policy = policy ?? new(), LawRevision = 1,
        Site = new() { PlaceId = 7, HasUseRight = true, HasReservedSeat = true, SeatCapacity = 2,
            Available = Elements.Uniform(1000000), FormationOperational = true, FormationQuality = 5000,
            CelestialWindow = true, ObservedRealmConditions = true, RealPassageAccessible = true },
        Knowledge = Catalog.Paths.Select(p => new KnowledgeEvidence(p.Id, p.Id + ".edition", 1, 5000, 5000, 0, true, false, "수용시험 학습 입력"))
            .Append(new("entry", "entry.edition", 1, 5000, 5000, 0, true, false, "실제 입문 학습 입력")).ToArray()
    };
    internal static CultivationState State(CultivationActor actor, CultivationPolicy? policy = null, bool complete = false, int variant = 0)
    {
        policy ??= new();
        var state = CultivationProfiles.ForNewBody(actor.BodyId, actor.SoulId, "수용시험 출생 입력");
        if (actor.Realm == Realm.Mortal) return state with { EntryWork = complete ? policy.RequiredWork(Realm.Mortal) : 0 };
        var structure = CultivationProfiles.Empty(actor.Realm);
        if (complete) structure = Full(structure, policy);
        if (structure is TribulationStructure t && complete) structure = t with { CompletedTrials = 1023 };
        var path = $"path.{(int)actor.Realm}.{variant}";
        return state with
        {
            Structure = structure, ActivePath = path,
            RootGrowth = actor.Realm > Realm.Qi || complete ? Elements.Uniform(policy.RequiredWork(Realm.Qi) / 5) : default,
            Formations = [new(actor.Realm, path, 5000, CultivationProfiles.FormationKeys(actor.Realm).Select(k => new FormationFactor(k, 5000)).ToArray(),
                0, 7, 1, null, null, "수용시험 초기 구조")]
        };
    }
    internal static RealmStructure Full(RealmStructure structure, CultivationPolicy policy) => structure.WithMeasures(
        Enumerable.Range(0, structure.Measures.Count).Select(i => Target(policy.RequiredWork(structure.Realm), structure.Measures.Count, i)).ToArray());
    internal static long Target(long work, int count, int index) => work / count + (index < work % count ? 1 : 0);
    internal static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    internal static string Json<T>(T value) => JsonSerializer.Serialize(value);

    internal sealed class Harness
    {
        private int sequence;
        internal CultivationActor Actor { get; set; }
        internal CultivationState State { get; set; }
        internal CultivationContext Context { get; set; }
        internal Harness(Realm realm = Realm.Qi, bool complete = false, CultivationPolicy? policy = null, int variant = 0)
        {
            Actor = CultivationFixture.Actor(realm) with { Layer = complete && realm != Realm.Mortal ? 9 : 1,
                GreatPerfection = complete && realm != Realm.Mortal };
            Context = CultivationFixture.Context(policy: policy); State = CultivationFixture.State(Actor, Context.Policy, complete, variant);
        }
        internal string Id() => "cultivation.test." + ++sequence;
        internal CultivationResult Run(Func<string, long, CultivationCommand> make, long? month = null, int sample = 9999)
        {
            if (month.HasValue) Context = Context with { Month = month.Value };
            var result = CultivationEngine.Execute(State, Actor, make(Id(), State.Revision), Context, sample);
            Check(result.Accepted, string.Join("; ", result.Blockers.Select(b => b.Code + ": " + b.Detail)));
            Commit(result); return result;
        }
        internal void Commit(CultivationResult result)
        {
            Check(result.Flow is null || result.Flow.Conserved, "elemental energy conservation");
            State = result.State;
            Actor = Actor with { Realm = result.Actor.Realm ?? Actor.Realm, Layer = result.Actor.Layer ?? Actor.Layer,
                GreatPerfection = result.Actor.GreatPerfection ?? Actor.GreatPerfection,
                Health = Math.Clamp(Actor.Health + result.Actor.HealthChange, 0, 100), MartialStage = result.Actor.MartialStage ?? Actor.MartialStage,
                Status = result.Actor.Death is null ? Actor.Status : LifeStatus.Dead };
        }
        internal CultivationResult Reject(CultivationCommand command)
        {
            var before = Json(State);
            var result = CultivationEngine.Execute(State, Actor, command, Context, 9999);
            Check(!result.Accepted && Json(result.State) == before && result.SupplyDebit.Total == 0 && result.Supports.Count == 0, "rejection is atomic");
            return result;
        }
    }
}

public sealed class CultivationProfilesScenario : IFeatureScenario
{
    public string Name => "INV-004/011/023 10 realm structures·30 paths·genesis provenance";
    public void Run()
    {
        var policy = new CultivationPolicy();
        CultivationFixture.Check(CultivationProfiles.Paths.Count == 30 && CultivationProfiles.Paths.GroupBy(p => p.Definition.Realm).All(g => g.Count() == 3), "three paths per realm");
        CultivationFixture.Check(CultivationProfiles.Paths.Select(p => p.Definition.Id).Distinct().Count() == 30, "stable catalog identity");
        for (var realm = Realm.Qi; realm <= Realm.HalfImmortal; realm++)
        {
            var actor = CultivationFixture.Actor(realm);
            var state = CultivationFixture.State(actor, policy);
            var restored = CultivationFixture.RoundTrip(state);
            CultivationFixture.Check(restored.Structure!.GetType() == state.Structure!.GetType(), "polymorphic structure survives save");
            CultivationFixture.Check(CultivationFixture.Json(state) == CultivationFixture.Json(restored), "metadata save round trip");
            CultivationInvariants.Validate(restored, actor, policy);
        }
        CultivationFixture.Check(CultivationProfiles.FormationKeys(Realm.Foundation).Count == 10 &&
            CultivationProfiles.FormationKeys(Realm.GoldenCore).Count == 7 && new GoldenCoreStructure().Measures.Count == 4, "frozen factors versus actual growth");
        var founder = CultivationProfiles.ForFounder(2, 3, 0, policy);
        var founderActor = CultivationFixture.Actor(Realm.HalfImmortal) with { Layer = 5 };
        CultivationInvariants.Validate(founder, founderActor, policy);
        CultivationFixture.Check(founder.Formations.Count == 1 && founder.Formations[0].Source == "창세 조건" &&
            founder.Formations[0].Parent is null && founder.Formations[0].Knowledge is null && founder.Conversions.Count == 0, "no fictitious pre-genesis achievements/mastery");
        CultivationFixture.Check(CultivationEngine.Preflight(founder, founderActor, "path.10.0", CultivationFixture.Context()).Any(b => b.Code == "ascension"), "Half Immortal is not automatic ascension");
    }
}

public sealed class CultivationBaselineScenario : IFeatureScenario
{
    public string Name => "INV-001/006/023 neutral 50.00 Qi whole-course functional formula";
    public void Run()
    {
        var h = new CultivationFixture.Harness();
        var innate = h.Actor.InnateRoots;
        long debit = 0;
        // A fixed 600-month formula assertion, explicitly not a performance or growth-distribution run.
        for (var month = 1; month <= 600; month++)
        {
            var r = h.Run((id, rev) => new PracticeCultivation(id, rev, "path.1.0", Elements.Uniform(1)), month);
            debit += r.SupplyDebit.Total;
            CultivationFixture.Check(r.Flow!.Assimilated.Total == 5000 && r.Flow.Conserved, "one neutral flow traverses four processes without repeating the duration");
            if (month < 600) CultivationFixture.Check(!h.Actor.GreatPerfection, "no early whole completion");
        }
        CultivationFixture.Check(h.Actor.GreatPerfection && h.Actor.Layer == 9 && h.State.RootGrowth.Minimum == 600000 && debit == 3000000, "whole Qi is 50 years");
        CultivationFixture.Check(h.Actor.InnateRoots == innate, "acquired root growth does not rewrite inherited innate quality");
        CultivationInvariants.Validate(h.State, h.Actor, h.Context.Policy);
        h.State = h.State with { RootGrowth = new(600000, 0, 0, 0, 0) };
        h.Actor = h.Actor with { GreatPerfection = false };
        var inspect = CultivationQueries.Inspect(h.State, h.Actor, h.Context.Policy);
        CultivationFixture.Check(inspect.Roots[0].Layer == 9 && inspect.Roots[1].Layer == 1 && !inspect.Roots[1].Complete, "representative ninth layer is not five-root completion");
        h.Reject(new PrepareCultivation(h.Id(), h.State.Revision, "path.2.0"));
        var gate = new CultivationFixture.Harness();
        gate.Actor = gate.Actor with { Circulation = 2999, Layer = 3 };
        gate.State = gate.State with { RootGrowth = Elements.Uniform(224999) };
        var blocked = gate.Run((id, rev) => new PracticeCultivation(id, rev, "path.1.0", Elements.Uniform(1)), 1);
        CultivationFixture.Check(gate.Actor.Layer == 3 && blocked.Blockers.Any(b => b.Code == "gate-3-4"), "3-to-4 control bottleneck");
        gate.Actor = gate.Actor with { Circulation = 5000, Body = 3999, Layer = 6 };
        gate.State = gate.State with { RootGrowth = Elements.Uniform(449999) };
        blocked = gate.Run((id, rev) => new PracticeCultivation(id, rev, "path.1.0", Elements.Uniform(1)), 2);
        CultivationFixture.Check(gate.Actor.Layer == 6 && blocked.Blockers.Any(b => b.Code == "gate-6-7"), "6-to-7 body bottleneck");
    }
}

public sealed class CultivationProcessScenario : IFeatureScenario
{
    public string Name => "INV-004/006/022 four-process energy·actual supply·crowding·rights";
    public void Run()
    {
        var h = new CultivationFixture.Harness();
        h.Actor = h.Actor with { Refinement = 1000, Circulation = 500, Assimilation = 500 };
        h.Context = h.Context with { Site = h.Context.Site with { Turbidity = 2500 } };
        var result = h.Run((id, rev) => new PracticeCultivation(id, rev, "path.1.0", Elements.Uniform(1)), 1);
        var flow = result.Flow!;
        CultivationFixture.Check(flow.Absorbed.Total == 5000 && flow.Refined.Total == 750 && flow.Circulated.Total == 500 && flow.Assimilated.Total == 500, "causal process capacities");
        CultivationFixture.Check(flow.HeldAfter.Total == 4250 && flow.Purged.Total == 250 && flow.Conserved && h.State.Damage > 0, "backlog and real over-absorption damage");
        h.State = CultivationFixture.RoundTrip(h.State);
        h.Context = h.Context with { Site = h.Context.Site with { Available = default } };
        result = h.Run((id, rev) => new PracticeCultivation(id, rev, "path.1.0", Elements.Uniform(1)), 2);
        CultivationFixture.Check(result.SupplyDebit.Total == 0 && result.Flow!.HeldBefore.Total == 4250 && result.Flow.Conserved, "held energy can be processed once after depletion");
        var crowd = new CultivationFixture.Harness();
        crowd.Context = crowd.Context with { Site = crowd.Context.Site with { Claimants = 5, Available = Elements.Uniform(1000) } };
        result = crowd.Run((id, rev) => new PracticeCultivation(id, rev, "path.1.0", Elements.Uniform(1)), 1);
        CultivationFixture.Check(result.SupplyDebit == Elements.Uniform(200), "fair actual elemental allocation");
        crowd.Context = crowd.Context with { Site = crowd.Context.Site with { HasUseRight = false }, Month = 2 };
        CultivationFixture.Check(crowd.Reject(new PracticeCultivation(crowd.Id(), crowd.State.Revision, "path.1.0", Elements.Uniform(1))).Blockers.Any(b => b.Code == "right"), "denied right");
        crowd.Context = crowd.Context with { Site = crowd.Context.Site with { HasUseRight = true, HasReservedSeat = false, OccupiedSeats = 2, SeatCapacity = 2 } };
        CultivationFixture.Check(crowd.Reject(new PracticeCultivation(crowd.Id(), crowd.State.Revision, "path.1.0", Elements.Uniform(1))).Blockers.Any(b => b.Code == "seat"), "occupied seats");
        h.Reject(new PracticeCultivation(h.Id(), h.State.Revision - 1, "path.1.0", Elements.Uniform(1)));
    }
}

public sealed class CultivationIrreversibilityScenario : IFeatureScenario
{
    public string Name => "INV-004/006/013/023 prepare/start/fracture·mandatory Nascent failure death";
    public void Run()
    {
        CultivationFixture.Harness Started()
        {
            var h = new CultivationFixture.Harness(Realm.GoldenCore, true);
            h.Run((id, rev) => new PrepareCultivation(id, rev, "path.4.0"), 0);
            CultivationFixture.Check(h.State.Attempt!.Phase == AttemptPhase.Prepared, "preparation is not an irreversible attempt");
            h.State = CultivationFixture.RoundTrip(h.State);
            h.Run((id, rev) => new StartCultivation(id, rev));
            h.Reject(new ResolveCultivation(h.Id(), h.State.Revision));
            return h;
        }
        var safe = Started();
        var cancelled = safe.Run((id, rev) => new CancelCultivation(id, rev));
        CultivationFixture.Check(cancelled.Actor.Death is null && safe.Actor.Realm == Realm.GoldenCore && safe.State.Attempt!.Phase == AttemptPhase.Cancelled, "pre-fracture safe cancellation");
        var failure = Started(); failure.Run((id, rev) => new FractureGoldenCore(id, rev), 1);
        var snapshot = CultivationFixture.Json(failure.State);
        var saved = CultivationFixture.RoundTrip(failure.State);
        CultivationFixture.Check(CultivationFixture.Json(saved) == snapshot && saved.Attempt!.Phase == AttemptPhase.Fractured, "save/pause is not abandoning formation");
        failure.State = saved;
        var failed = failure.Run((id, rev) => new ResolveCultivation(id, rev), 2, 0);
        CultivationFixture.Check(failed.Actor.Death == CultivationDeathCause.CoreFracture && failure.Actor.Status == LifeStatus.Dead && failure.Actor.Realm == Realm.GoldenCore, "failed formation dies with pre-cause Golden Core evidence");
        failure.Reject(new CancelCultivation(failure.Id(), failure.State.Revision));
        var abandon = Started(); abandon.Run((id, rev) => new FractureGoldenCore(id, rev), 1);
        var death = abandon.Run((id, rev) => new CancelCultivation(id, rev));
        CultivationFixture.Check(death.Actor.Death == CultivationDeathCause.CoreFracture, "actual abandonment after fracture is fatal");
        var success = Started(); success.Run((id, rev) => new FractureGoldenCore(id, rev), 1);
        success.Run((id, rev) => new ResolveCultivation(id, rev), 2);
        CultivationFixture.Check(success.Actor.Realm == Realm.NascentSoul && success.State.Structure is NascentSoulStructure && success.State.Attempt!.Phase == AttemptPhase.Stabilizing, "successful formation then separate stabilization");
        success.Reject(new RecoverCultivation(success.Id(), success.State.Revision));
        success.Reject(new CancelCultivation(success.Id(), success.State.Revision));
        for (var month = 3; month <= 5; month++) success.Run((id, rev) => new StabilizeCultivation(id, rev), month);
        CultivationFixture.Check(success.State.Attempt!.Phase == AttemptPhase.Completed, "stabilization consumes three actual monthly supplies");
        CultivationInvariants.Validate(success.State, success.Actor, success.Context.Policy);
    }
}

public sealed class CultivationKnowledgeScenario : IFeatureScenario
{
    public string Name => "INV-004/023/024 adopted knowledge revision·actual support reservation·no book mastery";
    public void Run()
    {
        var h = new CultivationFixture.Harness(Realm.Qi, true);
        h.Context = h.Context with { Knowledge = [], Supports = [new(50, "path.2.0", 1, 10000)] };
        h.Reject(new PrepareCultivation(h.Id(), h.State.Revision, "path.2.0"));
        h.Context = CultivationFixture.Context();
        h.Context = h.Context with { Supports = [new(51, "pill", 1, 5000)] };
        var prepared = h.Run((id, rev) => new PrepareCultivation(id, rev, "path.2.0", [51]));
        CultivationFixture.Check(prepared.Supports.Single().Operation == SupportOperation.Reserve && prepared.SupplyDebit.Total == 0, "preparation reserves physical aid without consuming supply");
        h.Reject(new StartCultivation(h.Id(), h.State.Revision));
        h.Context = h.Context with { Supports = [new(51, "pill", 1, 5000, h.State.Attempt!.Id)],
            Knowledge = h.Context.Knowledge.Select(k => k with { Revision = 2, Error = 10000 }).ToArray() };
        var started = h.Run((id, rev) => new StartCultivation(id, rev));
        CultivationFixture.Check(started.Supports.Single().Operation == SupportOperation.Consume, "physical aid consumed once at start");
        h.Run((id, rev) => new ResolveCultivation(id, rev), 1);
        CultivationFixture.Check(h.State.Formations[^1].Knowledge!.Revision == 1 && h.State.Formations[^1].Knowledge!.Error == 0, "new edition does not retroactively rewrite adopted knowledge");
        var earth = new CultivationFixture.Harness(Realm.Qi, true);
        earth.Context = earth.Context with { Site = earth.Context.Site with { FormationOperational = false } };
        earth.Reject(new PrepareCultivation(earth.Id(), earth.State.Revision, "path.2.1"));
        var heaven = new CultivationFixture.Harness(Realm.Qi, true);
        heaven.Context = heaven.Context with { Site = heaven.Context.Site with { CelestialWindow = false } };
        heaven.Reject(new PrepareCultivation(heaven.Id(), heaven.State.Revision, "path.2.2"));
        var released = new CultivationFixture.Harness(Realm.Qi, true);
        released.Context = released.Context with { Supports = [new(51, "pill", 1, 5000)] };
        released.Run((id, rev) => new PrepareCultivation(id, rev, "path.2.0", [51]));
        var cancellation = released.Run((id, rev) => new CancelCultivation(id, rev));
        CultivationFixture.Check(cancellation.Supports.Single().Operation == SupportOperation.Release, "prepared cancellation releases aid");
    }
}

public sealed class CultivationTransitionsScenario : IFeatureScenario
{
    public string Name => "INV-004/014/023 all 30 paths form their unique realm structure once";
    public void Run()
    {
        foreach (var path in CultivationProfiles.Paths)
        {
            var h = new CultivationFixture.Harness(path.Definition.Realm - 1, true);
            var oldQuality = h.State.Formations.Count == 0 ? 0 : h.State.Formations[^1].Quality;
            var oldCount = h.State.Formations.Count;
            h.Run((id, rev) => new PrepareCultivation(id, rev, path.Definition.Id));
            h.Run((id, rev) => new StartCultivation(id, rev));
            var due = 1;
            if (h.Actor.Realm == Realm.GoldenCore) { h.Run((id, rev) => new FractureGoldenCore(id, rev), 1); due = 2; }
            h.Run((id, rev) => new ResolveCultivation(id, rev), due);
            CultivationFixture.Check(h.Actor.Realm == path.Definition.Realm && h.State.Structure!.GetType() == CultivationProfiles.Empty(path.Definition.Realm).GetType(), "realm-specific structure formed");
            CultivationFixture.Check(h.State.Formations.Count == oldCount + 1 && h.State.Formations[^1].Path == path.Definition.Id &&
                h.State.Formations[^1].Parent!.Quality == oldQuality && h.State.Structure!.Total == 0, "frozen parent evidence transfers once into new formation, old growth not duplicated");
            if (oldCount > 0) CultivationFixture.Check(h.State.Formations[oldCount - 1].Quality == oldQuality, "old formation quality is immutable");
            CultivationInvariants.Validate(CultivationFixture.RoundTrip(h.State), h.Actor, h.Context.Policy);
        }
    }
}

public sealed class CultivationRecoveryScenario : IFeatureScenario
{
    public string Name => "INV-004/015/023 regression·real recovery·costed route conversion·retreat boundaries";
    public void Run()
    {
        var h = new CultivationFixture.Harness(Realm.GoldenCore, true);
        var frozen = CultivationFixture.Json(h.State.Formations);
        h.Run((id, rev) => new DamageCultivation(id, rev, CultivationDamageCause.Injury, 3000));
        var regressed = h.State.Structure!.Total;
        CultivationFixture.Check(!h.Actor.GreatPerfection && h.Actor.Layer < 9 && CultivationFixture.Json(h.State.Formations) == frozen, "current injury leaves frozen formation quality unchanged");
        h.Context = h.Context with { Site = h.Context.Site with { Available = default }, Month = 1 };
        h.Reject(new RecoverCultivation(h.Id(), h.State.Revision));
        h.Context = CultivationFixture.Context(1);
        h.Run((id, rev) => new RecoverCultivation(id, rev));
        CultivationFixture.Check(h.State.Damage == 2500 && h.State.Structure!.Total == regressed && !h.Actor.GreatPerfection, "healing does not automatically regrow lost progress");
        var route = new CultivationFixture.Harness(Realm.GoldenCore, true);
        route.Run((id, rev) => new PrepareCultivation(id, rev, "path.3.1", Conversion: true));
        route.Run((id, rev) => new StartCultivation(id, rev));
        CultivationFixture.Check(route.State.Structure!.Total < route.Context.Policy.RequiredWork(Realm.GoldenCore) && route.State.ActivePath == "path.3.0", "conversion has real partial shutdown and loss");
        route.Run((id, rev) => new ResolveCultivation(id, rev), 3);
        CultivationFixture.Check(route.State.ActivePath == "path.3.1" && route.State.Formations.Single().Path == "path.3.0" && route.State.Conversions.Count == 1, "current conversion does not edit historical method or award formation twice");
        var segment = CultivationQueries.NextRetreatSegment(route.State, 3, 100, [new(3, "실제 돌봄 배정")]);
        CultivationFixture.Check(segment.EndMonth == 3 && !segment.RequestedEndReached, "retreat stops at actual care boundary");
        for (var month = 4; month <= 6; month++) route.Run((id, rev) => new StabilizeCultivation(id, rev), month);
        route.Run((id, rev) => new ConfigureAuxiliaryCultivation(id, rev, "path.3.2"));
        var practiced = route.Run((id, rev) => new PracticeCultivation(id, rev, "path.3.1", Elements.Uniform(1)), 7);
        CultivationFixture.Check(practiced.SupplyDebit.Total == 15000 && route.State.AuxiliaryPath == "path.3.2", "auxiliary process pays real additional flow instead of stacking mutually exclusive rewards");
    }
}

public sealed class CultivationTribulationScenario : IFeatureScenario
{
    public string Name => "INV-004/013/023 every minor-stage tribulation including entry/ninth/completion";
    public void Run()
    {
        var h = new CultivationFixture.Harness(Realm.Tribulation);
        h.State = h.State with { PendingTrial = new(1, false, 1) };
        h.Run((id, rev) => new ResolveMinorTribulation(id, rev), 1);
        var month = 2;
        for (var target = 2; target <= 9; target++)
        {
            var structure = h.State.Structure!; var required = h.Context.Policy.RequiredWork(Realm.Tribulation);
            h.State = h.State with { Structure = structure.WithMeasures(Enumerable.Range(0, structure.Measures.Count)
                .Select(i => (CultivationFixture.Target(required, structure.Measures.Count, i) * (target - 1) + 7) / 8).ToArray()) };
            h.Run((id, rev) => new PracticeCultivation(id, rev, "path.8.0", Elements.Uniform(1)), month++);
            CultivationFixture.Check(h.State.PendingTrial!.TargetLayer == target && h.Actor.Layer == target - 1, "cannot skip a minor encounter");
            h.State = CultivationFixture.RoundTrip(h.State);
            h.Run((id, rev) => new ResolveMinorTribulation(id, rev), month++);
            CultivationFixture.Check(h.Actor.Layer == target && !h.Actor.GreatPerfection, "ninth layer is not completion tribulation");
        }
        h.Run((id, rev) => new PracticeCultivation(id, rev, "path.8.0", Elements.Uniform(1)), month++);
        CultivationFixture.Check(h.State.PendingTrial!.Completion, "separate completion encounter queued");
        h.Run((id, rev) => new ResolveMinorTribulation(id, rev), month);
        CultivationFixture.Check(h.Actor.GreatPerfection && ((TribulationStructure)h.State.Structure!).CompletedTrials == 1023, "all ten actual encounters recorded");
        h.Reject(new ResolveMinorTribulation(h.Id(), h.State.Revision));
        CultivationInvariants.Validate(h.State, h.Actor, h.Context.Policy);
        var fail = new CultivationFixture.Harness(Realm.Tribulation);
        fail.State = fail.State with { PendingTrial = new(1, false, 1) };
        var death = fail.Run((id, rev) => new ResolveMinorTribulation(id, rev), 1, 0);
        CultivationFixture.Check(death.Actor.Death == CultivationDeathCause.Tribulation, "actual failed trial death");
        var collapse = new CultivationFixture.Harness(Realm.Tribulation);
        collapse.State = collapse.State with { PendingTrial = new(1, false, 1) }; collapse.Actor = collapse.Actor with { Health = 5 };
        death = collapse.Run((id, rev) => new ResolveMinorTribulation(id, rev), 1);
        CultivationFixture.Check(death.Actor.Death == CultivationDeathCause.PostTribulationCollapse, "post-trial collapse distinct from failed trial");
    }
}

public sealed class CultivationMartialScenario : IFeatureScenario
{
    public string Name => "INV-004/011/015 independent root-free ten-stage martial growth·600-year cap";
    public void Run()
    {
        var a = new CultivationFixture.Harness(Realm.Mortal); var b = new CultivationFixture.Harness(Realm.Mortal);
        a.Actor = a.Actor with { InnateRoots = default }; b.Actor = b.Actor with { InnateRoots = Elements.Uniform(10000) };
        var ra = a.Run((id, rev) => new TrainMartial(id, rev), 1); var rb = b.Run((id, rev) => new TrainMartial(id, rev), 1);
        CultivationFixture.Check(CultivationFixture.Json(ra.State) == CultivationFixture.Json(rb.State) && ra.Actor == rb.Actor && ra.SupplyDebit.Total == 0, "martial work ignores all spiritual roots/supply");
        for (var stage = 1; stage <= 10; stage++)
        {
            a.State = a.State with { Martial = a.State.Martial with { Work = a.Context.Policy.MartialMonthsPerStage * stage * a.Context.Policy.FlowPerMonth } };
            var r = a.Run((id, rev) => new BreakthroughMartial(id, rev));
            CultivationFixture.Check(a.Actor.MartialStage == stage && a.Actor.Realm == Realm.Mortal && a.State.RootGrowth.Total == 0 && r.Actor.LifespanFloorMonths <= 7200, "independent martial stages and life ceiling");
            if (stage == 10) CultivationFixture.Check(r.Actor.LifespanFloorMonths == 7200, "top martial base lifespan is exactly 600 years");
        }
        a.Reject(new TrainMartial(a.Id(), a.State.Revision));
        var core = new CultivationFixture.Harness(Realm.GoldenCore, true);
        var martialPower = CultivationQueries.Combat(a.State, a.Actor, a.Context.Policy, new()).DisplayPower;
        var corePower = CultivationQueries.Combat(core.State, core.Actor, core.Context.Policy, new()).DisplayPower;
        CultivationFixture.Check(martialPower > corePower / 2 && martialPower < corePower * 2, "top martial power is comparable to Golden Core, not spiritual reproduction qualification");
        CultivationInvariants.Validate(a.State, a.Actor, a.Context.Policy);
    }
}

public sealed class CultivationCombatPotentialScenario : IFeatureScenario
{
    public string Name => "INV-006/008/015/023 canonical eight axes·single-source effects·conditional potential";
    public void Run()
    {
        var h = new CultivationFixture.Harness(Realm.GoldenCore, true);
        var before = CultivationFixture.Json(h.State); var scenario = new CombatSituation();
        var effect = new CombatSourceEffect("actual.shared.bloodline.7", 1, CombatAxes.Uniform(1000), CombatAxes.Uniform(10000));
        var once = CultivationQueries.Combat(h.State, h.Actor, h.Context.Policy, scenario, [effect]);
        var thrice = CultivationQueries.Combat(h.State, h.Actor, h.Context.Policy, scenario, [effect, effect, effect]);
        CultivationFixture.Check(once.Axes == thrice.Axes && thrice.Contributions.Count == 2 && CultivationQueries.AxisNames[7] == "유지 비용", "one cause across three aliases contributes once");
        var roots = CultivationQueries.Combat(h.State, h.Actor with { InnateRoots = Elements.Uniform(10000), Body = 10000 }, h.Context.Policy, scenario, [effect]);
        CultivationFixture.Check(roots.Axes == once.Axes, "innate aptitudes are not directly added to combat power");
        var history = h.State with { Formations = [h.State.Formations[0] with { Quality = 10000 }, .. h.State.Formations] };
        CultivationFixture.Check(CultivationQueries.Combat(history, h.Actor, h.Context.Policy, scenario, [effect]).Axes == once.Axes, "previous formation evidence is not added again to current power");
        var mobile = new CombatSituation { Moving = true };
        var prepared = new CombatSituation { PreparedFormation = true, DurationMonths = 3 };
        var human = new CultivationFixture.Harness(Realm.Foundation, true, variant: 0);
        var earth = new CultivationFixture.Harness(Realm.Foundation, true, variant: 1);
        CultivationFixture.Check(CultivationQueries.Combat(earth.State, earth.Actor, earth.Context.Policy, prepared).Axes.ReserveEndurance >
            CultivationQueries.Combat(human.State, human.Actor, human.Context.Policy, prepared).Axes.ReserveEndurance, "real prepared formation supports Earth route endurance");
        CultivationFixture.Check(CultivationQueries.Combat(earth.State, earth.Actor, earth.Context.Policy, mobile).Axes.ReserveEndurance <
            CultivationQueries.Combat(human.State, human.Actor, human.Context.Policy, mobile).Axes.ReserveEndurance, "moving/supply disruption changes route order");
        var knowledge = h.Context with { Knowledge = h.Context.Knowledge.Where(k => k.WorkId == "path.4.1").ToArray() };
        var potential = CultivationQueries.Potential(h.State, h.Actor, knowledge);
        CultivationFixture.Check(potential.KnownNextRoutes.Count == 1 && potential.KnownNextRoutes[0].Path == "path.4.1" &&
            potential.KnownNextRoutes[0].TargetRealm == Realm.NascentSoul && potential.KnownNextRoutes[0].ParentQualityCarried == 5000 / 6, "only learned next-realm routes, conditional one-time parent transformation");
        CultivationQueries.Inspect(h.State, h.Actor, h.Context.Policy);
        CultivationQueries.NextRetreatSegment(h.State, 0, 100);
        CultivationFixture.Check(CultivationFixture.Json(h.State) == before, "all queries consume no state or RNG");
        var invalid = h.State with { RootDamage = Elements.Uniform(10001) };
        var rejected = false; try { CultivationInvariants.Validate(invalid, h.Actor, h.Context.Policy); } catch (InvalidDataException) { rejected = true; }
        CultivationFixture.Check(rejected, "invalid saved damage rejected");
    }
}
