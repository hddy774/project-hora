namespace Chronicles.Game.People;

public enum GrowthPhase { Infant, Developing, Mature, Declining }
public enum AbsoluteAptitudeGrade { Ordinary, Promising, Outstanding, Exceptional, Unparalleled }
public enum EvaluationSeatStatus { Filled, VacantNoEligiblePerson, VacantBelowThreshold, VacantPopulation, DeferredTie }
public sealed record EvaluationScope(string Id, int? Region = null, string? SpeciesId = null, Realm? CultivationRealm = null,
    int? Layer = null, long MinimumAgeMonths = 0, long MaximumAgeMonths = long.MaxValue, GrowthPhase? Growth = null)
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id);
        if (Region < 0 || SpeciesId is "" || (CultivationRealm is { } realm && !Enum.IsDefined(realm)) ||
            Layer is < 1 or > 9 || MinimumAgeMonths < 0 || MaximumAgeMonths < MinimumAgeMonths ||
            (Growth is { } growth && !Enum.IsDefined(growth)))
            throw new InvalidDataException("Invalid evaluation region, species, realm, age or growth scope.");
    }
}
public sealed record EvaluationPerson(long PersonId, string SpeciesId, int Region, Realm CultivationRealm, int Layer, long BornMonth,
    GrowthPhase Growth, bool Alive, InnateProfile Innate);
public sealed record EvaluationPolicy(string PolicyId = "PC-TEMP-V021-EVALUATION-01", long Revision = 1,
    int GeniusMinimumAbsolute = 48_000, int CandidateMinimumAbsolute = 42_000, int MinimumPopulation = 2,
    bool IncludeInfants = false, bool IncludeDeclining = false, bool RequiresReview = true,
    string Rationale = "Absolute six-foundation thresholds, matched comparison population and explicit deferral of equal scores.")
{
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(PolicyId);
        if (Revision < 1 || GeniusMinimumAbsolute is < 0 or > 60_000 || CandidateMinimumAbsolute < 0 ||
            CandidateMinimumAbsolute > GeniusMinimumAbsolute || MinimumPopulation < 1)
            throw new InvalidDataException("Invalid evaluation policy.");
    }
}
public sealed record RelativeAptitude(long PersonId, int AbsoluteIndex, AbsoluteAptitudeGrade AbsoluteGrade, int RelativeRank,
    int Population, int PercentileBasisPoints, long AgeMonths, GrowthPhase Growth, bool TitleEligible);
public sealed record EvaluationSeat(EvaluationSeatStatus Status, long? PersonId, long[] Contenders, string Reason);
public sealed record AptitudeEvaluation(string EventId, EvaluationScope Scope, long Month, long LawRevision, string PolicyId,
    long PolicyRevision, RelativeAptitude[] Population, EvaluationSeat Genius, EvaluationSeat Candidate);
public sealed record EvaluationState(AptitudeEvaluation[] History)
{
    public static EvaluationState Empty() => new([]);
}
public sealed record EvaluationContext(string EventId, EvaluationScope Scope, long Month, long LawRevision, EvaluationPolicy Policy, EvaluationPerson[] People);
public sealed record EvaluationChange(bool Accepted, string Code, EvaluationState State);

/// <summary>Actual evaluations are observer data. AI and play must use Reputation claims instead.</summary>
public static class PeopleEvaluation
{
    public static AbsoluteAptitudeGrade Grade(int absoluteIndex) => absoluteIndex switch
    {
        < 0 or > 60_000 => throw new ArgumentOutOfRangeException(nameof(absoluteIndex)),
        < 18_000 => AbsoluteAptitudeGrade.Ordinary,
        < 30_000 => AbsoluteAptitudeGrade.Promising,
        < 42_000 => AbsoluteAptitudeGrade.Outstanding,
        < 54_000 => AbsoluteAptitudeGrade.Exceptional,
        _ => AbsoluteAptitudeGrade.Unparalleled
    };

    public static AptitudeEvaluation Assess(EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(context.EventId); context.Scope.Validate(); context.Policy.Validate();
        if (context.Month < 0 || context.LawRevision < 1 || context.People.Select(p => p.PersonId).Distinct().Count() != context.People.Length)
            throw new InvalidDataException("Evaluation requires one simulation-time snapshot per person.");
        foreach (var person in context.People)
        {
            if (person.PersonId <= 0 || person.Region < 0 || person.BornMonth > context.Month || person.Layer is < 1 or > 9 ||
                !Enum.IsDefined(person.CultivationRealm) || !Enum.IsDefined(person.Growth))
                throw new InvalidDataException("Invalid evaluation population.");
            person.Innate.Validate();
        }
        var scope = context.Scope; var policy = context.Policy;
        var people = context.People.Where(p => p.Alive && (scope.Region is null || p.Region == scope.Region) &&
                (scope.SpeciesId is null || p.SpeciesId == scope.SpeciesId) && (scope.CultivationRealm is null || p.CultivationRealm == scope.CultivationRealm) &&
                (scope.Layer is null || p.Layer == scope.Layer) && context.Month - p.BornMonth >= scope.MinimumAgeMonths &&
                context.Month - p.BornMonth <= scope.MaximumAgeMonths && (scope.Growth is null || p.Growth == scope.Growth))
            .Select(p => (Person: p, Score: Aptitudes.AbsoluteIndex(p.Innate)))
            .OrderByDescending(p => p.Score).ThenBy(p => p.Person.PersonId).ToArray();
        var population = people.Select(p =>
        {
            var rank = people.Count(other => other.Score > p.Score) + 1;
            var eligible = (policy.IncludeInfants || p.Person.Growth != GrowthPhase.Infant) && (policy.IncludeDeclining || p.Person.Growth != GrowthPhase.Declining);
            return new RelativeAptitude(p.Person.PersonId, p.Score, Grade(p.Score), rank, people.Length,
                people.Length <= 1 ? 10_000 : (people.Length - rank) * 10_000 / (people.Length - 1), context.Month - p.Person.BornMonth, p.Person.Growth, eligible);
        }).ToArray();
        var genius = Seat(population, policy.GeniusMinimumAbsolute, policy.MinimumPopulation, []);
        var reserved = genius.PersonId is { } holder ? new[] { holder } : genius.Contenders;
        var candidate = Seat(population, policy.CandidateMinimumAbsolute, policy.MinimumPopulation, reserved);
        return new(context.EventId, scope, context.Month, context.LawRevision, policy.PolicyId, policy.Revision, population, genius, candidate);
    }

    public static EvaluationChange Record(EvaluationState state, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(context);
        var existing = state.History.FirstOrDefault(e => e.EventId == context.EventId);
        if (existing is not null) return new(existing.Scope == context.Scope && existing.Month == context.Month && existing.LawRevision == context.LawRevision,
            "evaluation event already recorded", state);
        if (state.History.Any(e => e.Scope.Id == context.Scope.Id && e.Scope != context.Scope))
            return new(false, "scope identity has different comparison boundaries", state);
        if (state.History.Any(e => e.Scope.Id == context.Scope.Id && e.Month > context.Month)) return new(false, "cannot rewrite prior evaluation history", state);
        return new(true, "evaluation recorded at explicit simulation time", new([.. state.History, Assess(context)]));
    }

    public static AptitudeEvaluation? Latest(EvaluationState state, string scopeId, long month) =>
        state.History.Reverse().Where(e => e.Scope.Id == scopeId && e.Month <= month).OrderByDescending(e => e.Month).FirstOrDefault();

    private static EvaluationSeat Seat(RelativeAptitude[] population, int threshold, int minimumPopulation, long[] reserved)
    {
        if (population.Length < minimumPopulation) return new(EvaluationSeatStatus.VacantPopulation, null, [], "comparison population below policy minimum");
        var eligible = population.Where(p => p.TitleEligible && !reserved.Contains(p.PersonId)).ToArray();
        if (eligible.Length == 0) return new(EvaluationSeatStatus.VacantNoEligiblePerson, null, [], "no eligible person in the current scope");
        var qualifying = eligible.Where(p => p.AbsoluteIndex >= threshold).ToArray();
        if (qualifying.Length == 0) return new(EvaluationSeatStatus.VacantBelowThreshold, null, [], "no person meets the absolute threshold");
        var highest = qualifying.Max(p => p.AbsoluteIndex);
        var tied = qualifying.Where(p => p.AbsoluteIndex == highest).Select(p => p.PersonId).Order().ToArray();
        return tied.Length == 1 ? new(EvaluationSeatStatus.Filled, tied[0], [], "unique eligible highest absolute aptitude in this scope") :
            new(EvaluationSeatStatus.DeferredTie, null, tied, "equal absolute scores; title is deferred until a later evaluation");
    }
}
