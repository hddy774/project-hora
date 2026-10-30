namespace Chronicles.Game.People;

public enum SoulTraceDomain { AutobiographicalMemory, RelationshipSocialMemory, CultivationPrinciplesRealmKnowledge, TechniqueSpellPillCraftKnowledge, WorldFactionEnvironmentKnowledge, EmbodiedActionTrace }
public enum SoulTraceActionKind { Awakening, Integration, CrossingDamage, BirthReset, SplitSource, SplitChild }
public enum TracePreservationGrade { Lost, Fragment, Partial, Intact }
public enum TraceAccessGrade { Sealed, Latent, Partial, Immediate }
public sealed record SoulTraceValue(SoulTraceDomain Domain, int Preserved, int Accessible, int Integrated, bool HasEvidence,
    string[] DefinitionIds, bool MayContainMisconception, PeopleSource? Source)
{
    public TracePreservationGrade PreservationGrade => Preserved switch { >= 8000 => TracePreservationGrade.Intact, >= 5500 => TracePreservationGrade.Partial, >= 3000 => TracePreservationGrade.Fragment, _ => TracePreservationGrade.Lost };
    public TraceAccessGrade AccessGrade => Accessible switch { >= 8500 => TraceAccessGrade.Immediate, >= 6000 => TraceAccessGrade.Partial, >= 3000 => TraceAccessGrade.Latent, _ => TraceAccessGrade.Sealed };
}
public sealed record SoulTraceAction(string EventId, SoulTraceActionKind Kind, long Month, PeopleSource Source,
    SoulTraceDomain? Domain = null, int Amount = 0, long RelatedIdentity = 0, int[]? DomainAmounts = null);
public sealed record SoulTraceProfile(long SoulId, long ParentSoulId, long RootSoulId, long CurrentPersonId,
    SoulTraceValue[] Values, SoulTraceAction[] History)
{
    public static SoulTraceProfile WithoutEvidence(long soulId) => new(soulId, 0, soulId, 0,
        Enum.GetValues<SoulTraceDomain>().Select(d => new SoulTraceValue(d, 0, 0, 0, false, [], false, null)).ToArray(), []);

    public void Validate()
    {
        if (SoulId <= 0 || ParentSoulId < 0 || RootSoulId <= 0 || CurrentPersonId < 0 || ParentSoulId == SoulId ||
            Values.Length != 6 || Values.Select(v => v.Domain).Distinct().Count() != 6)
            throw new InvalidDataException("Invalid soul identity or six-domain trace structure.");
        foreach (var value in Values)
        {
            if (!Enum.IsDefined(value.Domain) || value.Integrated < 0 || value.Integrated > value.Accessible ||
                value.Accessible > value.Preserved || value.Preserved > 10_000 ||
                (!value.HasEvidence && (value.Preserved != 0 || value.DefinitionIds.Length > 0 || value.Source is not null)) ||
                (value.HasEvidence && value.Source is null))
                throw new InvalidDataException("Trace invariant: integrated <= accessible <= preserved; absent evidence is not measured loss.");
            value.Source?.Validate();
        }
        if (History.Select(a => a.EventId).Distinct(StringComparer.Ordinal).Count() != History.Length ||
            History.Zip(History.Skip(1)).Any(pair => pair.First.Month > pair.Second.Month))
            throw new InvalidDataException("Trace history must have unique events in simulation time order.");
        foreach (var action in History)
        {
            action.Source.Validate();
            if (action.Month != action.Source.Month || !Enum.IsDefined(action.Kind)) throw new InvalidDataException("Invalid trace event evidence.");
        }
    }
}
public sealed record SoulTraceChange(bool Accepted, string Code, SoulTraceProfile State);
public sealed record SoulTraceSplit(bool Accepted, string Code, SoulTraceProfile Source, SoulTraceProfile? Child, int[] Lost);
public sealed record SoulTraceHint(SoulTraceDomain Domain, string[] DefinitionIds, int Accessible, int Integrated, bool MayContainMisconception,
    string EvidenceId, long ObservedMonth);
public sealed record TraceObservation(long ObserverId, long SubjectId, SoulTraceDomain Domain, string[] DefinitionIds, int Accessible, int Integrated,
    bool MayContainMisconception, string EvidenceId, long ObservedMonth, long ReceivedMonth);
public sealed record TraceQueryContext(long ObserverId, long SubjectId, long Month, bool ObserverMode, TraceObservation[] Observations);

public static class SoulTraces
{
    public static SoulTraceChange Awaken(SoulTraceProfile state, SoulTraceAction action) => Progress(state, action, SoulTraceActionKind.Awakening);
    public static SoulTraceChange Integrate(SoulTraceProfile state, SoulTraceAction action) => Progress(state, action, SoulTraceActionKind.Integration);

    private static SoulTraceChange Progress(SoulTraceProfile state, SoulTraceAction action, SoulTraceActionKind kind)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(action);
        state.Validate(); action.Source.Validate(); ArgumentException.ThrowIfNullOrWhiteSpace(action.EventId);
        var replay = Replay(state, action);
        if (replay is not null) return replay;
        if (action.Kind != kind || action.Month != action.Source.Month || action.Domain is not { } domain || !Enum.IsDefined(domain) ||
            action.Amount is < 1 or > 10_000 || (state.History.Length > 0 && action.Month < state.History[^1].Month))
            return new(false, "invalid trace progress event", state);
        var value = state.Values.Single(v => v.Domain == domain);
        if (!value.HasEvidence) return new(false, "no preserved evidence to awaken or integrate", state);
        var updated = kind == SoulTraceActionKind.Awakening
            ? value with { Accessible = Math.Min(value.Preserved, value.Accessible + action.Amount) }
            : value with { Integrated = Math.Min(value.Accessible, value.Integrated + action.Amount) };
        return new(true, "trace progress only; no skills, realm, property or authority restored", state with
        {
            Values = state.Values.Select(v => v.Domain == domain ? updated : v).ToArray(), History = [.. state.History, action]
        });
    }

    public static SoulTraceChange Damage(SoulTraceProfile state, SoulTraceAction action)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(action);
        state.Validate(); action.Source.Validate(); ArgumentException.ThrowIfNullOrWhiteSpace(action.EventId);
        var replay = Replay(state, action); if (replay is not null) return replay;
        if (action.Kind != SoulTraceActionKind.CrossingDamage || !ValidTime(state, action) || action.DomainAmounts is null)
            return new(false, "invalid crossing damage event", state);
        PeopleChecks.Scores(action.DomainAmounts, 6, nameof(action.DomainAmounts));
        var values = state.Values.Select(v =>
        {
            var preserved = v.Preserved * (10_000 - action.DomainAmounts[(int)v.Domain]) / 10_000;
            var accessible = Math.Min(v.Accessible, preserved);
            return v with { Preserved = preserved, Accessible = accessible, Integrated = Math.Min(v.Integrated, accessible) };
        }).ToArray();
        return new(true, "crossing loss recorded", state with { Values = values, History = [.. state.History, action with { DomainAmounts = action.DomainAmounts.ToArray() }] });
    }

    public static SoulTraceChange ResetAtBirth(SoulTraceProfile state, SoulTraceAction action)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(action);
        state.Validate(); action.Source.Validate(); ArgumentException.ThrowIfNullOrWhiteSpace(action.EventId);
        var replay = Replay(state, action); if (replay is not null) return replay;
        if (action.Kind != SoulTraceActionKind.BirthReset || !ValidTime(state, action) || action.RelatedIdentity <= 0 ||
            state.History.Any(h => h.Kind == SoulTraceActionKind.BirthReset && h.RelatedIdentity == action.RelatedIdentity))
            return new(false, "invalid or repeated current-life binding", state);
        return new(true, "new body starts without accessible or integrated mastery", state with
        {
            CurrentPersonId = action.RelatedIdentity,
            Values = state.Values.Select(v => v with { Accessible = 0, Integrated = 0, DefinitionIds = v.DefinitionIds.ToArray() }).ToArray(),
            History = [.. state.History, action]
        });
    }

    /// <summary>Cost is consumed first, then each domain's remaining preservation is divided; nothing is cloned.</summary>
    public static SoulTraceSplit Split(SoulTraceProfile state, long childSoulId, SoulTraceAction action, int costBasisPoints)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(action);
        state.Validate(); action.Source.Validate(); ArgumentException.ThrowIfNullOrWhiteSpace(action.EventId);
        PeopleChecks.Score(costBasisPoints, nameof(costBasisPoints));
        if (childSoulId <= 0 || childSoulId == state.SoulId || childSoulId == state.RootSoulId ||
            action.Kind != SoulTraceActionKind.SplitSource || !ValidTime(state, action) || action.DomainAmounts is null ||
            action.RelatedIdentity != childSoulId || state.History.Any(h => h.EventId == action.EventId || h.RelatedIdentity == childSoulId && h.Kind == SoulTraceActionKind.SplitSource))
            return new(false, "invalid or repeated split; preserve the returned child in the host soul ledger", state, null, new int[6]);
        PeopleChecks.Scores(action.DomainAmounts, 6, nameof(action.DomainAmounts));
        var parentValues = new List<SoulTraceValue>(); var childValues = new List<SoulTraceValue>(); var lost = new int[6];
        foreach (var value in state.Values.OrderBy(v => v.Domain))
        {
            var available = value.Preserved * (10_000 - costBasisPoints) / 10_000;
            var childAmount = available * action.DomainAmounts[(int)value.Domain] / 10_000;
            var parentAmount = available - childAmount;
            lost[(int)value.Domain] = value.Preserved - available;
            var access = Math.Min(value.Accessible, parentAmount);
            parentValues.Add(value with { Preserved = parentAmount, Accessible = access, Integrated = Math.Min(value.Integrated, access), DefinitionIds = value.DefinitionIds.ToArray() });
            childValues.Add(value with { Preserved = childAmount, Accessible = 0, Integrated = 0, DefinitionIds = value.DefinitionIds.ToArray() });
        }
        var frozenAction = action with { Amount = costBasisPoints, DomainAmounts = action.DomainAmounts.ToArray() };
        var parent = state with { Values = parentValues.ToArray(), History = [.. state.History, frozenAction] };
        var child = new SoulTraceProfile(childSoulId, state.SoulId, state.RootSoulId, 0, childValues.ToArray(),
            [frozenAction with { Kind = SoulTraceActionKind.SplitChild, RelatedIdentity = state.SoulId }]);
        parent.Validate(); child.Validate();
        return new(true, "independent soul branch; distributed preservation without present-life skills or property", parent, child, lost);
    }

    public static SoulTraceHint[] Project(SoulTraceProfile state, TraceQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(context);
        if (context.ObserverMode)
            return state.Values.OrderBy(v => v.Domain).Select(v => new SoulTraceHint(v.Domain, v.DefinitionIds.ToArray(), v.Accessible, v.Integrated,
                v.MayContainMisconception, v.HasEvidence ? v.Source!.Reference : "no evidence", context.Month)).ToArray();
        return context.Observations.Where(o => o.ObserverId == context.ObserverId && o.SubjectId == context.SubjectId &&
                o.ObservedMonth <= context.Month && o.ReceivedMonth <= context.Month)
            .OrderBy(o => o.Domain).ThenByDescending(o => o.ReceivedMonth).DistinctBy(o => o.Domain)
            .Select(o => new SoulTraceHint(o.Domain, o.DefinitionIds.ToArray(), o.Accessible, o.Integrated, o.MayContainMisconception, o.EvidenceId, o.ObservedMonth)).ToArray();
    }

    private static bool ValidTime(SoulTraceProfile state, SoulTraceAction action) => action.Month == action.Source.Month &&
        (state.History.Length == 0 || action.Month >= state.History[^1].Month);

    private static SoulTraceChange? Replay(SoulTraceProfile state, SoulTraceAction action)
    {
        var existing = state.History.FirstOrDefault(h => h.EventId == action.EventId);
        if (existing is null) return null;
        var same = existing.Kind == action.Kind && existing.Month == action.Month && existing.Source == action.Source &&
            existing.Domain == action.Domain && existing.Amount == action.Amount && existing.RelatedIdentity == action.RelatedIdentity &&
            (existing.DomainAmounts ?? []).SequenceEqual(action.DomainAmounts ?? []);
        return new(same, "trace event already resolved", state);
    }
}
