namespace Chronicles.Game.People;

public enum TraitCategory { Constitution, Bloodline, SoulTrace, Temperament, DaoHeart, Habit, Relationship, Fortune, DaoScar, Status }
public enum TraitLayer { Body, Bloodline, Soul, Experience, Relationship, Institution }
public enum TraitPurpose { Mechanical, DerivedMarker, Preference, Relationship, Fortune, HistoricalTitle, AuthorityReference }
public enum TraitStatus { Latent, Active, Suppressed, Released, Expired }
public enum TraitStacking { UniqueDefinition, Strongest, PerRelatedEntity }
public enum TraitChangeKind { Acquired, Awakened, Suppressed, Released, Expired }
public enum TraitFact
{
    BodyPrototype, BloodlinePrototype, MeridianFit, EarthAffinity, ColdAdapted, ElementBiased,
    BloodlineAwakened, BloodlineResonating, BloodlineHarmonized, BloodlineConflicting,
    TraceAccessible, SoulStable, RebirthEvidence, SoulDamaged, CalmExperience, ResolveExperience,
    CuriosityExperience, CompetitionExperience, SurvivalValue, DaoValue, ProtectionValue, PromiseValue,
    RoutineExperience, PlanningExperience, AdaptationExperience, StorageExperience,
    TeacherDebt, ActualOath, ActualGrievance, ActualFamilyRelationship,
    GoodFortuneEvent, BadFortuneEvent, EscapeOpportunity, EncounterResonance,
    MeridianDamage, FoundationDamage, ElementImbalance, CoreDefect,
    ActualSectFounded, ActualDynastyFounded, ActualLandAuthority, ActualExile,
    Resolved, AuthorityRevoked
}
public enum GoalPreference { Survival, SeekingDao, Family, Curiosity, Cooperation, Trust, Risk, Ambition, Patience, Honour, Autonomy, Discipline, Empathy, Competition }
public enum FortuneEventKind { Discovery, Adversity, Escape, Encounter }
public sealed record PreferenceEffect(GoalPreference Preference, int Amount);
public sealed record TraitEffectTemplate(EffectTarget Target, EffectOperation Operation, long Amount, string ExclusiveGroup = "");
public sealed record FortuneEffect(FortuneEventKind EventKind, int ModifierBasisPoints);
public sealed record TraitDefinition(string Id, string Name, TraitCategory Category, TraitLayer Layer, TraitPurpose Purpose,
    TraitFact Acquisition, TraitFact Awakening, TraitFact Release, TraitStacking Stacking, string[] Conflicts,
    TraitEffectTemplate[] Effects, PreferenceEffect[] Preferences, FortuneEffect[] Fortune, bool RequiresRelatedEntity,
    bool MayInherit, bool RequiresReview = true, string PolicyId = "PC-TEMP-V021-TRAITS-01");
public sealed record TraitHistory(string EventId, TraitChangeKind Kind, long Month, PeopleSource Source);
public sealed record PersonTrait(long Id, long PersonId, string DefinitionId, TraitStatus Status, int Strength,
    long AcquiredMonth, long? AwakenedMonth, long? ExpiresMonth, long RelatedEntityId, PeopleSource Source, TraitHistory[] History);
public sealed record TraitLedger(PersonTrait[] Traits)
{
    public static TraitLedger Empty() => new([]);
}
public sealed record TraitAcquireContext(long Id, long PersonId, string DefinitionId, string EventId, PeopleSource Source,
    TraitFact[] Facts, int Strength = 10_000, long? ExpiresMonth = null, long RelatedEntityId = 0);
public sealed record TraitTransitionContext(long TraitId, string EventId, PeopleSource Source, TraitFact[] Facts);
public sealed record TraitChange(bool Accepted, string Code, TraitLedger State);
public sealed record TraitObservation(long ObserverId, long SubjectId, string DefinitionId, TraitStatus Status, long ObservedMonth,
    long AcquiredMonth, long? ExpiresMonth, string EvidenceId);
public sealed record TraitQueryContext(long ObserverId, long SubjectId, long Month, bool ObserverMode, TraitObservation[] Observations);
public sealed record KnownTrait(string DefinitionId, TraitStatus Status, long ObservedMonth, string EvidenceId, PeopleSource? ActualSource);

public static class TraitCatalog
{
    public static IReadOnlyList<TraitDefinition> All { get; } = Array.AsReadOnly(Build());
    public static TraitDefinition Get(string id) => All.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.Ordinal))
        ?? throw new InvalidDataException("Unknown trait definition.");

    private static TraitDefinition[] Build()
    {
        var result = new List<TraitDefinition>();
        void Add(string id, string name, TraitCategory category, TraitLayer layer, TraitPurpose purpose, TraitFact acquire,
            TraitFact? awaken = null, bool inherits = false, string[]? conflicts = null, TraitEffectTemplate[]? effects = null,
            PreferenceEffect[]? preferences = null, FortuneEffect[]? fortune = null, bool related = false,
            TraitStacking stacking = TraitStacking.UniqueDefinition) =>
            result.Add(new(id, name, category, layer, purpose, acquire, awaken ?? acquire,
                purpose == TraitPurpose.AuthorityReference ? TraitFact.AuthorityRevoked : TraitFact.Resolved,
                related && stacking == TraitStacking.UniqueDefinition ? TraitStacking.PerRelatedEntity : stacking,
                conflicts ?? [], effects ?? [], preferences ?? [], fortune ?? [], related, inherits));
        TraitEffectTemplate Talent(int index, int amount) => new(new(EffectDomain.Talent, index), EffectOperation.Add, amount);
        Add("body.meridian", "통맥영체", TraitCategory.Constitution, TraitLayer.Body, TraitPurpose.Mechanical, TraitFact.BodyPrototype, TraitFact.MeridianFit,
            inherits: true, effects: [Talent(7, 500)]);
        Add("body.earth", "후토영체", TraitCategory.Constitution, TraitLayer.Body, TraitPurpose.Mechanical, TraitFact.BodyPrototype, TraitFact.EarthAffinity,
            inherits: true, effects: [Talent(4, 500)]);
        Add("body.cold", "한맥지구", TraitCategory.Constitution, TraitLayer.Body, TraitPurpose.Mechanical, TraitFact.BodyPrototype, TraitFact.ColdAdapted,
            inherits: true, effects: [Talent(2, 400)]);
        Add("body.biased", "영기편성", TraitCategory.Constitution, TraitLayer.Body, TraitPurpose.DerivedMarker, TraitFact.ElementBiased, inherits: true);
        Add("bloodline.latent", "고맥잠장", TraitCategory.Bloodline, TraitLayer.Bloodline, TraitPurpose.DerivedMarker, TraitFact.BloodlinePrototype, TraitFact.BloodlineAwakened, inherits: true);
        Add("bloodline.resonance", "혈맥공명", TraitCategory.Bloodline, TraitLayer.Bloodline, TraitPurpose.Mechanical, TraitFact.BloodlinePrototype, TraitFact.BloodlineResonating,
            inherits: true, effects: [Talent(5, 300)], stacking: TraitStacking.Strongest);
        Add("bloodline.harmony", "이맥교융", TraitCategory.Bloodline, TraitLayer.Bloodline, TraitPurpose.DerivedMarker, TraitFact.BloodlineHarmonized,
            inherits: true, conflicts: ["bloodline.conflict"]);
        Add("bloodline.conflict", "혈맥상충", TraitCategory.Bloodline, TraitLayer.Bloodline, TraitPurpose.DerivedMarker, TraitFact.BloodlineConflicting,
            inherits: true, conflicts: ["bloodline.harmony"]);
        Add("soul.insight", "숙혜잔광", TraitCategory.SoulTrace, TraitLayer.Soul, TraitPurpose.DerivedMarker, TraitFact.TraceAccessible);
        Add("soul.stable", "혼기견고", TraitCategory.SoulTrace, TraitLayer.Soul, TraitPurpose.DerivedMarker, TraitFact.SoulStable);
        Add("soul.reborn", "윤회유흔", TraitCategory.SoulTrace, TraitLayer.Soul, TraitPurpose.DerivedMarker, TraitFact.RebirthEvidence);
        Add("soul.injured", "혼상미유", TraitCategory.SoulTrace, TraitLayer.Soul, TraitPurpose.DerivedMarker, TraitFact.SoulDamaged);
        Add("temperament.calm", "침정여수", TraitCategory.Temperament, TraitLayer.Experience, TraitPurpose.Preference, TraitFact.CalmExperience,
            preferences: [new(GoalPreference.Patience, 500)]);
        Add("temperament.resolve", "강직불굴", TraitCategory.Temperament, TraitLayer.Experience, TraitPurpose.Preference, TraitFact.ResolveExperience,
            preferences: [new(GoalPreference.Honour, 500)]);
        Add("temperament.curious", "호기무궁", TraitCategory.Temperament, TraitLayer.Experience, TraitPurpose.Preference, TraitFact.CuriosityExperience,
            preferences: [new(GoalPreference.Curiosity, 500)]);
        Add("temperament.competitive", "쟁승호투", TraitCategory.Temperament, TraitLayer.Experience, TraitPurpose.Preference, TraitFact.CompetitionExperience,
            preferences: [new(GoalPreference.Competition, 500)]);
        Add("dao.longevity", "장생일념", TraitCategory.DaoHeart, TraitLayer.Experience, TraitPurpose.Preference, TraitFact.SurvivalValue,
            preferences: [new(GoalPreference.Survival, 500)]);
        Add("dao.seeking", "구도불권", TraitCategory.DaoHeart, TraitLayer.Experience, TraitPurpose.Preference, TraitFact.DaoValue,
            preferences: [new(GoalPreference.SeekingDao, 500)]);
        Add("dao.protection", "수호지심", TraitCategory.DaoHeart, TraitLayer.Experience, TraitPurpose.Preference, TraitFact.ProtectionValue,
            preferences: [new(GoalPreference.Family, 250), new(GoalPreference.Empathy, 250)]);
        Add("dao.promise", "중낙수신", TraitCategory.DaoHeart, TraitLayer.Experience, TraitPurpose.Preference, TraitFact.PromiseValue,
            preferences: [new(GoalPreference.Trust, 500)]);
        Add("habit.routine", "일과불철", TraitCategory.Habit, TraitLayer.Experience, TraitPurpose.Preference, TraitFact.RoutineExperience,
            preferences: [new(GoalPreference.Discipline, 500)]);
        Add("habit.planning", "신행원려", TraitCategory.Habit, TraitLayer.Experience, TraitPurpose.Preference, TraitFact.PlanningExperience,
            preferences: [new(GoalPreference.Patience, 500)]);
        Add("habit.adaptable", "임기응변", TraitCategory.Habit, TraitLayer.Experience, TraitPurpose.Preference, TraitFact.AdaptationExperience,
            preferences: [new(GoalPreference.Autonomy, 500)]);
        Add("habit.storage", "적곡비황", TraitCategory.Habit, TraitLayer.Experience, TraitPurpose.Preference, TraitFact.StorageExperience,
            preferences: [new(GoalPreference.Survival, 500)]);
        Add("relation.teacher", "사은미보", TraitCategory.Relationship, TraitLayer.Relationship, TraitPurpose.Relationship, TraitFact.TeacherDebt, related: true);
        Add("relation.oath", "혈맹지서", TraitCategory.Relationship, TraitLayer.Relationship, TraitPurpose.Relationship, TraitFact.ActualOath, related: true);
        Add("relation.grievance", "구원미해", TraitCategory.Relationship, TraitLayer.Relationship, TraitPurpose.Relationship, TraitFact.ActualGrievance, related: true);
        Add("relation.family", "세교지연", TraitCategory.Relationship, TraitLayer.Relationship, TraitPurpose.Relationship, TraitFact.ActualFamilyRelationship, related: true);
        Add("fortune.good", "길운상조", TraitCategory.Fortune, TraitLayer.Experience, TraitPurpose.Fortune, TraitFact.GoodFortuneEvent,
            fortune: [new(FortuneEventKind.Discovery, 500)], stacking: TraitStacking.Strongest);
        Add("fortune.bad", "액운미산", TraitCategory.Fortune, TraitLayer.Experience, TraitPurpose.Fortune, TraitFact.BadFortuneEvent,
            fortune: [new(FortuneEventKind.Adversity, 500)], stacking: TraitStacking.Strongest);
        Add("fortune.escape", "봉흉유로", TraitCategory.Fortune, TraitLayer.Experience, TraitPurpose.Fortune, TraitFact.EscapeOpportunity,
            fortune: [new(FortuneEventKind.Escape, 500)], stacking: TraitStacking.Strongest);
        Add("fortune.encounter", "기연상인", TraitCategory.Fortune, TraitLayer.Experience, TraitPurpose.Fortune, TraitFact.EncounterResonance,
            fortune: [new(FortuneEventKind.Encounter, 500)], related: true, stacking: TraitStacking.Strongest);
        Add("scar.meridian", "경맥구상", TraitCategory.DaoScar, TraitLayer.Body, TraitPurpose.DerivedMarker, TraitFact.MeridianDamage);
        Add("scar.foundation", "도기불온", TraitCategory.DaoScar, TraitLayer.Body, TraitPurpose.DerivedMarker, TraitFact.FoundationDamage);
        Add("scar.elements", "오행실형", TraitCategory.DaoScar, TraitLayer.Body, TraitPurpose.DerivedMarker, TraitFact.ElementImbalance);
        Add("scar.core", "금단유하", TraitCategory.DaoScar, TraitLayer.Body, TraitPurpose.DerivedMarker, TraitFact.CoreDefect);
        Add("status.sect-founder", "창종지조", TraitCategory.Status, TraitLayer.Institution, TraitPurpose.HistoricalTitle, TraitFact.ActualSectFounded, related: true);
        Add("status.dynasty-founder", "개국지군", TraitCategory.Status, TraitLayer.Institution, TraitPurpose.HistoricalTitle, TraitFact.ActualDynastyFounded, related: true);
        Add("status.landlord", "봉지지주", TraitCategory.Status, TraitLayer.Institution, TraitPurpose.AuthorityReference, TraitFact.ActualLandAuthority, related: true);
        Add("status.exile", "유망종실", TraitCategory.Status, TraitLayer.Institution, TraitPurpose.HistoricalTitle, TraitFact.ActualExile, related: true);
        return result.ToArray();
    }
}

public static class Traits
{
    public static TraitChange Acquire(TraitLedger state, TraitAcquireContext context)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(context);
        context.Source.Validate(); ArgumentException.ThrowIfNullOrWhiteSpace(context.EventId);
        PeopleChecks.Score(context.Strength, nameof(context.Strength));
        var definition = TraitCatalog.Get(context.DefinitionId);
        if (context.Id <= 0 || context.PersonId <= 0 || context.RelatedEntityId < 0) return new(false, "invalid identity", state);
        var existing = state.Traits.FirstOrDefault(t => t.Id == context.Id);
        if (existing is not null)
            return new(existing.PersonId == context.PersonId && existing.DefinitionId == context.DefinitionId && existing.History[0].EventId == context.EventId,
                "existing trait identity", state);
        if (!context.Facts.Contains(definition.Acquisition)) return new(false, "missing acquisition evidence", state);
        if (definition.RequiresRelatedEntity && context.RelatedEntityId == 0) return new(false, "missing actual relationship or institution", state);
        if (definition.Purpose == TraitPurpose.Fortune && context.ExpiresMonth is null) return new(false, "fortune requires an expiry", state);
        if (context.ExpiresMonth <= context.Source.Month) return new(false, "invalid expiry", state);
        if (state.Traits.Any(t => t.PersonId == context.PersonId && t.DefinitionId == context.DefinitionId &&
            t.Status is not (TraitStatus.Released or TraitStatus.Expired) &&
            (definition.Stacking == TraitStacking.UniqueDefinition ||
                definition.Stacking == TraitStacking.PerRelatedEntity && t.RelatedEntityId == context.RelatedEntityId || t.Source.CauseId == context.Source.CauseId)))
            return new(false, "trait stacking conflict", state);
        var trait = new PersonTrait(context.Id, context.PersonId, context.DefinitionId, TraitStatus.Latent, context.Strength,
            context.Source.Month, null, context.ExpiresMonth, context.RelatedEntityId, context.Source,
            [new(context.EventId, TraitChangeKind.Acquired, context.Source.Month, context.Source)]);
        return new(true, "acquired latent trait", new([.. state.Traits, trait]));
    }

    public static TraitChange Transition(TraitLedger state, TraitTransitionContext context, TraitChangeKind kind)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(context);
        context.Source.Validate(); ArgumentException.ThrowIfNullOrWhiteSpace(context.EventId);
        var trait = state.Traits.FirstOrDefault(t => t.Id == context.TraitId);
        if (trait is null) return new(false, "unknown trait", state);
        var existing = trait.History.FirstOrDefault(h => h.EventId == context.EventId);
        if (existing is not null) return new(existing.Kind == kind, "existing transition", state);
        if (context.Source.Month < trait.History[^1].Month) return new(false, "transition before history", state);
        if (trait.Status is TraitStatus.Expired or TraitStatus.Released) return new(false, "trait ended", state);
        var definition = TraitCatalog.Get(trait.DefinitionId);
        var nextStatus = kind switch
        {
            TraitChangeKind.Awakened => TraitStatus.Active,
            TraitChangeKind.Suppressed => TraitStatus.Suppressed,
            TraitChangeKind.Released => TraitStatus.Released,
            TraitChangeKind.Expired => TraitStatus.Expired,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        if (kind == TraitChangeKind.Awakened)
        {
            if (trait.ExpiresMonth <= context.Source.Month) return new(false, "trait expired", state);
            if (!context.Facts.Contains(definition.Awakening)) return new(false, "missing awakening evidence", state);
            if (trait.Status == TraitStatus.Active) return new(false, "already active", state);
            if (state.Traits.Any(t => t.PersonId == trait.PersonId && IsActive(t, context.Source.Month) &&
                (definition.Conflicts.Contains(t.DefinitionId, StringComparer.Ordinal) ||
                    TraitCatalog.Get(t.DefinitionId).Conflicts.Contains(trait.DefinitionId, StringComparer.Ordinal))))
                return new(false, "conflicting active trait", state);
        }
        if (kind == TraitChangeKind.Released && !context.Facts.Contains(definition.Release)) return new(false, "missing release evidence", state);
        if (kind == TraitChangeKind.Expired && (trait.ExpiresMonth is null || trait.ExpiresMonth > context.Source.Month))
            return new(false, "expiry not reached", state);
        if (kind == TraitChangeKind.Suppressed && trait.Status != TraitStatus.Active) return new(false, "trait not active", state);
        var updated = trait with { Status = nextStatus,
            AwakenedMonth = kind == TraitChangeKind.Awakened ? context.Source.Month : trait.AwakenedMonth,
            History = [.. trait.History, new(context.EventId, kind, context.Source.Month, context.Source)] };
        return new(true, "transition applied", new(state.Traits.Select(t => t.Id == trait.Id ? updated : t).ToArray()));
    }

    public static bool IsActive(PersonTrait trait, long month) => trait.Status == TraitStatus.Active && trait.AcquiredMonth <= month &&
        trait.AwakenedMonth <= month && (trait.ExpiresMonth is null || month < trait.ExpiresMonth);

    public static PeopleEffect[] ActiveEffects(TraitLedger state, long personId, long month)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Active(state, personId, month).SelectMany(t => TraitCatalog.Get(t.DefinitionId).Effects.Select((e, i) =>
            new PeopleEffect($"trait.{t.Id}.{i}", e.Target, e.Operation, e.Amount * t.Strength / 10_000,
                t.Source, EffectRole.Contribution, e.ExclusiveGroup))).ToArray();
    }

    public static PreferenceEffect[] ActivePreferences(TraitLedger state, long personId, long month) =>
        Active(state, personId, month).SelectMany(t => TraitCatalog.Get(t.DefinitionId).Preferences.Select(e =>
            e with { Amount = e.Amount * t.Strength / 10_000 })).GroupBy(e => e.Preference)
            .OrderBy(g => g.Key).Select(g => new PreferenceEffect(g.Key, Math.Clamp(g.Sum(e => e.Amount), -10_000, 10_000))).ToArray();

    public static FortuneEffect[] ActiveFortune(TraitLedger state, long personId, long month, FortuneEventKind eventKind) =>
        Active(state, personId, month).SelectMany(t => TraitCatalog.Get(t.DefinitionId).Fortune
            .Where(e => e.EventKind == eventKind).Select(e => e with { ModifierBasisPoints = e.ModifierBasisPoints * t.Strength / 10_000 })).ToArray();

    public static KnownTrait[] Project(TraitLedger state, TraitQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(context);
        if (context.ObserverMode)
            return state.Traits.Where(t => t.PersonId == context.SubjectId && t.AcquiredMonth <= context.Month).OrderBy(t => t.Id)
                .Select(t => new KnownTrait(t.DefinitionId, t.ExpiresMonth <= context.Month ? TraitStatus.Expired : t.Status,
                    context.Month, "observer", t.Source)).ToArray();
        return context.Observations.Where(o => o.ObserverId == context.ObserverId && o.SubjectId == context.SubjectId &&
                o.ObservedMonth <= context.Month && o.AcquiredMonth <= context.Month)
            .OrderBy(o => o.DefinitionId, StringComparer.Ordinal).ThenByDescending(o => o.AcquiredMonth)
            .DistinctBy(o => o.DefinitionId, StringComparer.Ordinal)
            .Select(o => new KnownTrait(o.DefinitionId, o.ExpiresMonth <= context.Month ? TraitStatus.Expired : o.Status, o.ObservedMonth, o.EvidenceId, null)).ToArray();
    }

    private static IEnumerable<PersonTrait> Active(TraitLedger state, long personId, long month) =>
        state.Traits.Where(t => t.PersonId == personId && IsActive(t, month))
            .GroupBy(t => t.DefinitionId, StringComparer.Ordinal).SelectMany(g =>
                TraitCatalog.Get(g.Key).Stacking == TraitStacking.Strongest ? g.OrderByDescending(t => t.Strength).ThenBy(t => t.Id).Take(1) : g)
            .OrderBy(t => t.Id);
}
