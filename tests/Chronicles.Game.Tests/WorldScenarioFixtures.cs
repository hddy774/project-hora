using Chronicles.Game;
using Chronicles.Game.Cultivation;

namespace Chronicles.Game.Tests;

/// <summary>Explicit functional-test initial conditions. Production restore never repairs inconsistent authority.</summary>
public static class WorldScenarioFixtures
{
    public static void SetCultivation(GameState snapshot, Person actor, Realm realm, int layer = 1,
        bool greatPerfection = false, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot); ArgumentNullException.ThrowIfNull(actor);
        if (realm is < Realm.Mortal or > Realm.HalfImmortal || layer is < 1 or > 9 ||
            (greatPerfection && (realm == Realm.Mortal || layer != 9))) throw new ArgumentException("Explicit fixture realm/layer/completion mismatch.");
        var policy = Policy(snapshot);
        var metadata = CultivationProfiles.ForNewBody(actor.BodyId, actor.SoulId, "기능 경계 시험의 명시 초기 영근 조건");
        if (realm != Realm.Mortal)
        {
            path ??= $"path.{(int)realm}.0";
            if (CultivationProfiles.Find(path)?.Definition.Realm != realm) throw new ArgumentException("Fixture path must match its declared realm.", nameof(path));
            var structure = CultivationProfiles.Empty(realm); var count = structure.Measures.Count;
            var required = policy.RequiredWork(realm);
            var values = Enumerable.Range(0, count).Select(i =>
            {
                var target = required / count + (i < required % count ? 1 : 0);
                return greatPerfection ? target : (target * (layer - 1) + 7) / 8;
            }).ToArray();
            structure = structure.WithMeasures(values);
            if (structure is TribulationStructure trials)
                structure = trials with { CompletedTrials = greatPerfection ? 1023 : (1 << layer) - 1 };
            var rootRequired = policy.RequiredWork(Realm.Qi) / 5;
            var rootGrowth = realm > Realm.Qi || greatPerfection ? rootRequired : (rootRequired * (layer - 1) + 7) / 8;
            metadata = metadata with { Structure = structure, ActivePath = path, RootGrowth = Elements.Uniform(rootGrowth),
                GrowthBasis = new(snapshot.LawRevision, required, rootRequired, policy.RequiredWork(Realm.Mortal)),
                Formations = [new(realm, path, 5000, CultivationProfiles.FormationKeys(realm).Select(key => new FormationFactor(key, 5000)).ToArray(),
                    snapshot.Month, actor.Place, snapshot.LawRevision, null, null, "기능 경계 시험의 명시 현재 구조·과거 사건 주장 없음")] };
        }
        snapshot.Cultivation[actor.BodyId] = metadata;
        actor.Realm = realm; actor.Layer = layer; actor.GreatPerfection = greatPerfection;
        actor.Work = 0; actor.MartialWork = 0; actor.PendingLayer = 0; actor.Irreversible = false;
        actor.StructureDamage = 0; actor.BondStability = 100; actor.Processes = new int[4];
        actor.Components = realm == Realm.GoldenCore ? Enumerable.Repeat(5000, 7).ToArray() : new int[7];
        actor.Formations.Clear();
    }

    public static CultivationPolicy Policy(GameState snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new()
        {
            Revision = snapshot.LawRevision, EntryMonths = (int)snapshot.Laws["cultivation.entry"], QiMonths = (int)snapshot.Laws["cultivation.qi"],
            FoundationMonths = (int)snapshot.Laws["cultivation.foundation"], UpperDurationPercent = (int)snapshot.Laws["cultivation.upper"],
            BaseRisk = (int)snapshot.Laws["cultivation.breakthroughRisk"], TrialRisk = (int)snapshot.Laws["cultivation.breakthroughRisk"],
            MartialMonthsPerStage = (int)snapshot.Laws["martial.period"], TrialHealthCost = (int)snapshot.Laws["hazard.trialDamage"],
            QiLifespanMonths = snapshot.Laws["life.qi"], FoundationLifespanMonths = snapshot.Laws["life.foundation"],
            GoldenCoreLifespanMonths = snapshot.Laws["life.core"], HalfImmortalLifespanMonths = snapshot.Laws["founder.lifespan"],
            UpperLifespanPercent = (int)snapshot.Laws["life.upperMultiplier"]
        };
    }
}
