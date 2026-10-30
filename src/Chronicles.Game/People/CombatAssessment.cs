namespace Chronicles.Game.People;

public enum CombatAxis { OutputPenetration, ReserveEndurance, ControlPrecision, DefenseRecovery, MovementSpace, DetectionConcealment, CoordinationLoad, MaintenanceCost }
public enum CombatKnowledgeLevel { Actual, Measured, Estimated }
public enum PotentialAspect { Capacity, NextRealmCompatibility, RecoveryModification, AvailableRoutes, GrowthCostRisk, InformationReliability }
public enum PotentialRequirementStatus { Unknown, Missing, Available, Satisfied }
public sealed record StandardEncounter(string Id, int Participants, int DistanceMeters, string EnvironmentId, bool EquipmentIncluded,
    int DurationRounds, CombatKnowledgeLevel KnowledgeLevel);
public sealed record CombatFormation(long Id, long PersonId, Realm Realm, int Layer, string RouteId, long FormedMonth,
    long[] StructureAxes, PeopleEffect[] Contributions, PeopleSource Source);
public sealed record CombatAssessmentContext(CombatFormation Formation, int Health, int Settling, int[] Availability, int[] EnvironmentalFactors,
    PeopleEffect[] CurrentEffects, StandardEncounter Encounter);
public sealed record CombatAxisValue(CombatAxis Axis, string Name, long FormedStructure, long AvailableStructure, long Current);
public sealed record StandardCombatAssessment(long PersonId, Realm Realm, long FormationId, StandardEncounter Encounter, CombatAxisValue[] Axes,
    long DisplayPower, int SustainableFraction, EffectResolution Effects, string PolicyId = "PC-TEMP-V021-COMBAT-ASSESSMENT-01", long PolicyRevision = 1, bool RequiresReview = true);
public sealed record FormationTransfer(string EventId, long Month, CombatFormation Previous, long NewFormationId, int[] Retention,
    string[] CarriedCauses, PeopleSource Source);
public sealed record FormationState(CombatFormation Current, FormationTransfer[] History);
public sealed record FormationTransferContext(string EventId, long NewFormationId, Realm NextRealm, long Month, string RouteId,
    int[] Retention, long[] NewStructureAxes, string[] CarryCauses, PeopleEffect[] NewContributions, PeopleSource Source);
public sealed record FormationChange(bool Accepted, string Code, FormationState State);
public sealed record PotentialRequirement(string Id, PotentialAspect Aspect, PotentialRequirementStatus Status, string Description, PeopleSource? Source);
public sealed record ConditionalPotential(string Id, Realm NextRealm, PotentialRequirement[] Requirements, long? MinimumMonths, long? MaximumMonths,
    int Confidence, PeopleSource Source);
public sealed record PotentialObservation(long ObserverId, long SubjectId, ConditionalPotential Snapshot, long ObservedMonth, long ReceivedMonth);
public sealed record PotentialQueryContext(long ObserverId, long SubjectId, long Month, bool ObserverMode, PotentialObservation[] Observations);

public static class CombatAssessment
{
    public static IReadOnlyList<string> AxisNames { get; } = Array.AsReadOnly(new[] { "출력·관통", "비축·지속", "제어·정밀", "방호·복원", "이동·공간", "감지·은폐", "협응·부하", "유지 비용" });

    public static StandardCombatAssessment Assess(CombatAssessmentContext context)
    {
        ArgumentNullException.ThrowIfNull(context); Validate(context.Formation);
        PeopleChecks.Score(context.Health, nameof(context.Health)); PeopleChecks.Score(context.Settling, nameof(context.Settling));
        PeopleChecks.Scores(context.Availability, 8, nameof(context.Availability)); PeopleChecks.Scores(context.EnvironmentalFactors, 8, nameof(context.EnvironmentalFactors));
        var encounter = context.Encounter;
        ArgumentException.ThrowIfNullOrWhiteSpace(encounter.Id); ArgumentException.ThrowIfNullOrWhiteSpace(encounter.EnvironmentId);
        if (encounter.Participants < 1 || encounter.DistanceMeters < 0 || encounter.DurationRounds is < 1 or > 10_000 || !Enum.IsDefined(encounter.KnowledgeLevel))
            throw new InvalidDataException("A standard comparison must specify participants, distance, environment, equipment, duration and information level.");
        var effects = PeopleEffects.Resolve(context.Formation.Contributions.Concat(context.CurrentEffects)
            .Where(e => encounter.EquipmentIncluded || e.Source.Kind != PeopleSourceKind.Equipment));
        var axes = Enumerable.Range(0, 8).Select(index =>
        {
            var formed = context.Formation.StructureAxes[index];
            var available = index == (int)CombatAxis.MaintenanceCost ? formed :
                formed * context.Health / 10_000 * context.Settling / 10_000 * context.Availability[index] / 10_000;
            var current = effects.Apply(new(EffectDomain.Combat, index), available, maximum: 1_000_000_000_000);
            current = current * context.EnvironmentalFactors[index] / 10_000;
            return new CombatAxisValue((CombatAxis)index, AxisNames[index], formed, available, current);
        }).ToArray();
        var reserve = axes[(int)CombatAxis.ReserveEndurance].Current;
        var cost = axes[(int)CombatAxis.MaintenanceCost].Current;
        var sustainable = cost == 0 ? 10_000 : (int)Math.Min(10_000, reserve * 10_000 / Math.Max(1, cost * encounter.DurationRounds));
        var coordinationWeight = encounter.Participants > 1 ? 1500 : 500;
        var sensingWeight = encounter.Participants > 1 ? 500 : 1500;
        var weighted = checked(axes[0].Current * 3000 + axes[2].Current * 1500 + axes[3].Current * 2000 + axes[4].Current * 1500 +
            axes[5].Current * sensingWeight + axes[6].Current * coordinationWeight) / 10_000;
        var power = weighted * (5000 + sustainable / 2) / 10_000;
        return new(context.Formation.PersonId, context.Formation.Realm, context.Formation.Id, encounter, axes, power, sustainable, effects);
    }

    /// <summary>NewStructureAxes are new realm contributions; retained old structure is added exactly once.</summary>
    public static FormationChange Transfer(FormationState state, FormationTransferContext context)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(context);
        Validate(state.Current); context.Source.Validate(); ArgumentException.ThrowIfNullOrWhiteSpace(context.EventId);
        var previous = state.History.FirstOrDefault(h => h.EventId == context.EventId);
        if (previous is not null) return new(previous.NewFormationId == context.NewFormationId, "formation transfer already resolved", state);
        PeopleChecks.Scores(context.Retention, 8, nameof(context.Retention)); ValidateAxes(context.NewStructureAxes);
        if (context.NewFormationId <= 0 || context.NewFormationId == state.Current.Id || !Enum.IsDefined(context.NextRealm) ||
            context.NextRealm != state.Current.Realm + 1 || context.Month < state.Current.FormedMonth || context.Source.Month != context.Month ||
            state.History.Any(h => h.Previous.Id == context.NewFormationId || h.NewFormationId == context.NewFormationId))
            return new(false, "invalid next-realm conversion", state);
        var currentEffects = PeopleEffects.Resolve(state.Current.Contributions).Applied;
        if (context.CarryCauses.Distinct(StringComparer.Ordinal).Count() != context.CarryCauses.Length ||
            context.CarryCauses.Any(cause => !currentEffects.Any(e => e.Source.CauseId == cause)) ||
            currentEffects.Any(e => context.CarryCauses.Contains(e.Source.CauseId, StringComparer.Ordinal) && e.Operation != EffectOperation.Add))
            return new(false, "transfer requires explicit additive structural causes", state);
        var carried = currentEffects.Where(e => context.CarryCauses.Contains(e.Source.CauseId, StringComparer.Ordinal)).Select(e => e with
        {
            Id = $"formation.{context.NewFormationId}.{e.Id}", Amount = e.Amount * context.Retention[e.Target.Index] / 10_000,
            Source = e.Source with { Kind = PeopleSourceKind.Formation, Reference = $"transferred formation {state.Current.Id}: {e.Source.Reference}", Month = context.Month, Revision = e.Source.Revision + 1 }
        }).ToArray();
        var contributions = carried.Concat(context.NewContributions).ToArray();
        PeopleEffects.Resolve(contributions);
        var structure = state.Current.StructureAxes.Select((value, index) => checked(value * context.Retention[index] / 10_000 + context.NewStructureAxes[index])).ToArray();
        var formation = new CombatFormation(context.NewFormationId, state.Current.PersonId, context.NextRealm, 1, context.RouteId, context.Month,
            structure, contributions, context.Source);
        Validate(formation);
        var transfer = new FormationTransfer(context.EventId, context.Month, state.Current, context.NewFormationId, context.Retention.ToArray(), context.CarryCauses.ToArray(), context.Source);
        return new(true, "previous formation retained as history; only converted current contributions remain active", new(formation, [.. state.History, transfer]));
    }

    public static ConditionalPotential[] ProjectPotential(ConditionalPotential[] actual, PotentialQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(actual); ArgumentNullException.ThrowIfNull(context);
        var selected = context.ObserverMode ? actual : context.Observations.Where(o => o.ObserverId == context.ObserverId && o.SubjectId == context.SubjectId &&
                o.ObservedMonth <= context.Month && o.ReceivedMonth <= context.Month).OrderByDescending(o => o.ReceivedMonth)
            .DistinctBy(o => o.Snapshot.Id, StringComparer.Ordinal).Select(o => o.Snapshot);
        return selected.OrderBy(p => p.Id, StringComparer.Ordinal).Select(p => p with { Requirements = p.Requirements.ToArray() }).ToArray();
    }

    public static void ValidatePotential(ConditionalPotential potential)
    {
        ArgumentNullException.ThrowIfNull(potential); potential.Source.Validate(); PeopleChecks.Score(potential.Confidence, nameof(potential.Confidence));
        ArgumentException.ThrowIfNullOrWhiteSpace(potential.Id);
        if (!Enum.IsDefined(potential.NextRealm) || potential.NextRealm == Realm.Mortal || potential.MinimumMonths < 0 || potential.MaximumMonths < potential.MinimumMonths ||
            (potential.MinimumMonths is null) != (potential.MaximumMonths is null) ||
            potential.Requirements.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != potential.Requirements.Length)
            throw new InvalidDataException("Conditional potential requires bounded known estimates and distinct prerequisites.");
        foreach (var requirement in potential.Requirements)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(requirement.Id); ArgumentException.ThrowIfNullOrWhiteSpace(requirement.Description);
            if (!Enum.IsDefined(requirement.Aspect) || !Enum.IsDefined(requirement.Status) ||
                requirement.Status != PotentialRequirementStatus.Unknown && requirement.Source is null)
                throw new InvalidDataException("Known potential prerequisites require actual supporting evidence.");
            requirement.Source?.Validate();
        }
    }

    public static void Validate(CombatFormation formation)
    {
        ArgumentNullException.ThrowIfNull(formation); formation.Source.Validate(); ValidateAxes(formation.StructureAxes);
        if (formation.Id <= 0 || formation.PersonId <= 0 || formation.Layer is < 1 or > 9 || formation.FormedMonth < 0 || !Enum.IsDefined(formation.Realm) ||
            formation.Contributions.Any(e => e.Target.Domain != EffectDomain.Combat)) throw new InvalidDataException("Invalid actual combat formation.");
        PeopleEffects.Resolve(formation.Contributions);
    }

    internal static void ValidateAxes(long[] axes)
    {
        ArgumentNullException.ThrowIfNull(axes);
        if (axes.Length != 8 || axes.Any(value => value is < 0 or > 1_000_000_000_000)) throw new InvalidDataException("Combat requires eight bounded actual axes.");
    }
}
