namespace Chronicles.Game.Cultivation;

/// <summary>Invoke when restoring the owning world's candidate save, before accepting any mutation.</summary>
public static class CultivationInvariants
{
    public static void Validate(CultivationState state, CultivationActor actor, CultivationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(actor); ArgumentNullException.ThrowIfNull(policy);
        Require(state.BodyId == actor.BodyId && state.SoulId == actor.SoulId && state.BodyId > 0 && state.SoulId > 0, "body/soul ownership");
        Require(state.Revision >= 0 && state.EntryWork >= 0 && state.Martial.Work >= 0, "progress/revision");
        Require(state.EntryWork == 0 || (state.GrowthBasis is { EntryWork: > 0 } entryBasis && state.EntryWork <= entryBasis.EntryWork), "adopted sensing work");
        Require(state.RootGrowth.Nonnegative && state.RootGrowth.Maximum <= CultivationEngine.RootTarget(state, policy), "five acquired roots");
        if (state.GrowthBasis is { } basis)
            Require(basis.LawRevision > 0 && basis.LawRevision <= policy.Revision && basis.RealmWork is >= 0 and <= 100000000000000 &&
                basis.RootWork is >= 0 and <= 20000000000000 && basis.EntryWork is > 0 and <= 100000000000000, "adopted physical progress basis");
        Require(state.RootDamage.Nonnegative && state.RootDamage.Maximum <= 10000 && state.Damage is >= 0 and <= 10000 && state.Load is >= 0 and <= 10000, "damage/load");
        Require(state.Raw.Nonnegative && state.Refined.Nonnegative && state.Circulating.Nonnegative, "energy balance");
        Require(state.Martial.Injury is >= 0 and <= 10000 && actor.MartialStage is >= 0 and <= 10, "independent martial state");
        Require(actor.Layer is >= 1 and <= 9 && actor.Realm is >= Realm.Mortal and <= Realm.HalfImmortal, "realm/layer");
        Require(actor.InnateRoots.Nonnegative && actor.InnateRoots.Maximum <= 10000, "innate roots");
        if (actor.Realm == Realm.Mortal) Require(state.Structure is null && state.ActivePath.Length == 0, "mortal has no formed spiritual structure");
        else
        {
            Require(state.Structure?.Realm == actor.Realm, "one current realm structure");
            Require(state.GrowthBasis is { RealmWork: > 0, RootWork: > 0 }, "formed structure retains adopted progress basis");
            var measures = state.Structure!.Measures; var required = CultivationEngine.RealmTarget(state, policy);
            for (var i = 0; i < measures.Count; i++) Require(measures[i] >= 0 && measures[i] <= required / measures.Count + (i < required % measures.Count ? 1 : 0), "current structure progress");
            Require(CultivationProfiles.Find(state.ActivePath)?.Definition.Realm == actor.Realm, "current path realm");
            Require(state.Formations.Count > 0 && state.Formations[^1].Realm == actor.Realm, "current formation evidence");
            if (actor.GreatPerfection && actor.Status == LifeStatus.Alive)
                Require(CultivationEngine.Complete(state, actor, policy), "whole completion is not representative ninth layer");
        }
        Require(state.AuxiliaryPath.Length == 0 || (state.AuxiliaryPath != state.ActivePath && CultivationProfiles.Find(state.AuxiliaryPath)?.Definition.Realm == actor.Realm), "compatible auxiliary process");
        foreach (var formation in state.Formations)
        {
            Require(CultivationProfiles.Find(formation.Path)?.Definition.Realm == formation.Realm && formation.Quality is >= 0 and <= 10000, "formation path/quality");
            Require(formation.Factors.Count == CultivationProfiles.FormationKeys(formation.Realm).Count && formation.Factors.All(f => f.Value is >= 0 and <= 10000), "realm-specific frozen factors");
            Require(formation.Source.Length > 0 && formation.LawRevision > 0 && formation.LawRevision <= policy.Revision, "formation provenance");
            if (formation.Source == "창세 조건") Require(formation.Parent is null && formation.Knowledge is null, "genesis does not fabricate past breakthroughs/mastery");
        }
        if (state.PendingTrial is not null)
        {
            Require(actor.Realm == Realm.Tribulation && state.PendingTrial.TargetLayer is >= 1 and <= 9, "minor tribulation ownership");
            Require(!state.PendingTrial.Completion || state.PendingTrial.TargetLayer == 9, "completion tribulation distinct from ninth layer");
        }
        if (state.Structure is TribulationStructure trials) Require(trials.CompletedTrials is >= 0 and <= 1023, "ten actual tribulation encounters");
        if (state.Attempt is not null)
        {
            var attempt = state.Attempt;
            Require(attempt.Id.Length > 0 && attempt.LawRevision > 0 && attempt.LawRevision <= policy.Revision && attempt.Knowledge.Revision >= 0 && attempt.Risk is >= 0 and <= 10000, "attempt evidence");
            Require(attempt.TargetWork is > 0 and <= 100000000000000 && attempt.TargetRootWork is > 0 and <= 20000000000000 &&
                attempt.LifespanAward > 0, "prepared attempt retains target work and life award");
            Require(attempt.ReservedItems.Distinct().Count() == attempt.ReservedItems.Count, "single reservation per item");
            Require(attempt.Phase != AttemptPhase.Fractured || (actor.Realm == Realm.GoldenCore && attempt.TargetRealm == Realm.NascentSoul && attempt.Kind == AttemptKind.RealmBreakthrough), "irreversible core fracture");
        }
    }

    private static void Require(bool valid, string name)
    {
        if (!valid) throw new InvalidDataException("Cultivation invariant: " + name);
    }
}
