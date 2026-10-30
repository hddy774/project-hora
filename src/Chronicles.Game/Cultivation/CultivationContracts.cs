using System.Text.Json.Serialization;

namespace Chronicles.Game.Cultivation;

/// <summary>Integer elemental energy, in cultivation units. 5,000 units is one neutral month's flow.</summary>
public readonly record struct Elements(long Metal, long Wood, long Water, long Fire, long Earth)
{
    [JsonIgnore] public long Total => checked(Metal + Wood + Water + Fire + Earth);
    [JsonIgnore] public long Minimum => Math.Min(Math.Min(Metal, Wood), Math.Min(Math.Min(Water, Fire), Earth));
    [JsonIgnore] public long Maximum => Math.Max(Math.Max(Metal, Wood), Math.Max(Math.Max(Water, Fire), Earth));
    [JsonIgnore] public bool Nonnegative => Minimum >= 0;
    public long At(int index) => index switch { 0 => Metal, 1 => Wood, 2 => Water, 3 => Fire, 4 => Earth,
        _ => throw new ArgumentOutOfRangeException(nameof(index)) };
    public static Elements Uniform(long value) => new(value, value, value, value, value);
    public static Elements From(IReadOnlyList<int> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count != 5) throw new ArgumentException("Exactly five roots are required.", nameof(values));
        return new(values[0], values[1], values[2], values[3], values[4]);
    }
    public static Elements Map(Elements value, Func<long, long> map) =>
        new(map(value.Metal), map(value.Wood), map(value.Water), map(value.Fire), map(value.Earth));
    public static Elements Zip(Elements a, Elements b, Func<long, long, long> map) =>
        new(map(a.Metal, b.Metal), map(a.Wood, b.Wood), map(a.Water, b.Water), map(a.Fire, b.Fire), map(a.Earth, b.Earth));
    public static Elements operator +(Elements a, Elements b) => Zip(a, b, (x, y) => checked(x + y));
    public static Elements operator -(Elements a, Elements b) => Zip(a, b, (x, y) => checked(x - y));
    public static Elements Limit(Elements a, Elements b) => Zip(a, b, Math.Min);
    public static Elements Allocate(long total, Elements weights)
    {
        if (total < 0 || !weights.Nonnegative || weights.Total == 0) throw new ArgumentOutOfRangeException(nameof(total));
        // Give rounding remainders to the largest fractional shares, then stable element order.
        var values = Enumerable.Range(0, 5).Select(i => total * weights.At(i) / weights.Total).ToArray();
        var remaining = total - values.Sum();
        foreach (var i in Enumerable.Range(0, 5).OrderByDescending(i => total * weights.At(i) % weights.Total).ThenBy(i => i).Take((int)remaining))
            values[i]++;
        return new(values[0], values[1], values[2], values[3], values[4]);
    }
}

/// <summary>A snapshot of shared Person authority. The domain never stores or mutates a second Person.</summary>
public sealed record CultivationActor
{
    public long PersonId { get; init; }
    public long BodyId { get; init; }
    public long SoulId { get; init; }
    public Realm Realm { get; init; }
    public int Layer { get; init; } = 1;
    public bool GreatPerfection { get; init; }
    public LifeStatus Status { get; init; }
    public int Health { get; init; } = 100;
    public int MartialStage { get; init; }
    public Elements InnateRoots { get; init; } = Elements.Uniform(5000);
    public int Absorption { get; init; } = 5000;
    public int Refinement { get; init; } = 5000;
    public int Circulation { get; init; } = 5000;
    public int Assimilation { get; init; } = 5000;
    public int Body { get; init; } = 5000;
    public int Mind { get; init; } = 5000;
}

public sealed record KnowledgeEvidence(string WorkId, string EditionId, long Revision, int Understanding,
    int Practice, int Error, bool Memorized, bool CanConsult, string Source)
{
    [JsonIgnore] public bool Executable => Understanding > 0 && (Memorized || CanConsult);
}

/// <summary>Available supply is an actual remaining ledger balance, never a monthly display estimate.</summary>
public sealed record CultivationSite
{
    public int PlaceId { get; init; }
    public Elements Available { get; init; }
    public int Claimants { get; init; } = 1;
    public int OccupiedSeats { get; init; }
    public int SeatCapacity { get; init; } = 1;
    public bool HasUseRight { get; init; }
    public bool HasReservedSeat { get; init; }
    public int Turbidity { get; init; }
    public int FormationQuality { get; init; }
    public bool FormationOperational { get; init; }
    public bool CelestialWindow { get; init; }
    public bool ObservedRealmConditions { get; init; }
    public bool RealPassageAccessible { get; init; }
}

public sealed record CultivationSupport(long ItemId, string Kind, int Quantity, int Quality, string ReservedBy = "");
public sealed record CultivationContext
{
    public long Month { get; init; }
    public long LawRevision { get; init; } = 1;
    public CultivationPolicy Policy { get; init; } = new();
    public CultivationSite Site { get; init; } = new();
    public IReadOnlyList<KnowledgeEvidence> Knowledge { get; init; } = [];
    public IReadOnlyList<CultivationSupport> Supports { get; init; } = [];
}

public enum TrainingFocus { Balanced, Absorption, Refinement, Circulation, Assimilation, Structure }
public enum AttemptKind { Entry, RealmBreakthrough, RouteConversion }
public enum AttemptPhase { Prepared, Started, Fractured, Stabilizing, Recovering, Completed, Cancelled, Failed }
public enum CultivationDeathCause { CoreFracture, Tribulation, PostTribulationCollapse, StructureCollapse, BondCollapse }
public enum CultivationDamageCause { OverAbsorption, WrongCirculation, Injury, Neglect, Turbidity, BondConflict }
public enum SupportOperation { Reserve, Consume, Release }

public abstract record CultivationCommand(string Id, long ExpectedRevision);
public sealed record PracticeCultivation(string Id, long ExpectedRevision, string PathId, Elements Allocation,
    TrainingFocus Focus = TrainingFocus.Balanced, int Intensity = 10000) : CultivationCommand(Id, ExpectedRevision);
public sealed record PrepareCultivation(string Id, long ExpectedRevision, string PathId,
    IReadOnlyList<long>? SupportItems = null, bool Conversion = false) : CultivationCommand(Id, ExpectedRevision);
public sealed record StartCultivation(string Id, long ExpectedRevision) : CultivationCommand(Id, ExpectedRevision);
public sealed record FractureGoldenCore(string Id, long ExpectedRevision) : CultivationCommand(Id, ExpectedRevision);
public sealed record ResolveCultivation(string Id, long ExpectedRevision) : CultivationCommand(Id, ExpectedRevision);
public sealed record CancelCultivation(string Id, long ExpectedRevision) : CultivationCommand(Id, ExpectedRevision);
public sealed record StabilizeCultivation(string Id, long ExpectedRevision) : CultivationCommand(Id, ExpectedRevision);
public sealed record RecoverCultivation(string Id, long ExpectedRevision, bool MedicalTreatment = false) : CultivationCommand(Id, ExpectedRevision);
public sealed record DamageCultivation(string Id, long ExpectedRevision, CultivationDamageCause Cause, int Severity) : CultivationCommand(Id, ExpectedRevision);
public sealed record ResolveMinorTribulation(string Id, long ExpectedRevision) : CultivationCommand(Id, ExpectedRevision);
public sealed record TrainMartial(string Id, long ExpectedRevision, int Intensity = 10000) : CultivationCommand(Id, ExpectedRevision);
public sealed record BreakthroughMartial(string Id, long ExpectedRevision) : CultivationCommand(Id, ExpectedRevision);
public sealed record ConfigureAuxiliaryCultivation(string Id, long ExpectedRevision, string PathId) : CultivationCommand(Id, ExpectedRevision);

public sealed record SupportDelta(long ItemId, SupportOperation Operation, string Reservation, int Quantity);
public sealed record CultivationNotice(string Kind, string Detail, string Source);
public sealed record CultivationBlocker(string Code, string Detail, string Remedy);
public sealed record ActorCultivationDelta(Realm? Realm = null, int? Layer = null, bool? GreatPerfection = null,
    int HealthChange = 0, long LifespanFloorMonths = 0, int? MartialStage = null,
    CultivationDeathCause? Death = null);
public sealed record ProcessFlow(Elements Absorbed, Elements Refined, Elements Circulated, Elements Assimilated,
    Elements Purged, Elements HeldBefore, Elements HeldAfter)
{
    [JsonIgnore] public bool Conserved => HeldBefore + Absorbed == HeldAfter + Assimilated + Purged;
}
public sealed record CultivationResult(bool Accepted, CultivationState State, ActorCultivationDelta Actor,
    Elements SupplyDebit, IReadOnlyList<SupportDelta> Supports, IReadOnlyList<CultivationNotice> Notices,
    IReadOnlyList<CultivationBlocker> Blockers, ProcessFlow? Flow = null);

public sealed record FormationFactor(string Key, int Value);
public sealed record ParentStructureEvidence(Realm Realm, int Quality, int Maturity, int Damage, string Path);
public sealed record CultivationFormation(Realm Realm, string Path, int Quality, IReadOnlyList<FormationFactor> Factors,
    long Month, int Place, long LawRevision, KnowledgeEvidence? Knowledge, ParentStructureEvidence? Parent, string Source);
public sealed record PathConversion(string From, string To, long Began, long Completed, int LostProgress, string Source);
public sealed record CultivationAttempt(string Id, AttemptKind Kind, AttemptPhase Phase, Realm TargetRealm, string Path,
    long PreparedMonth, long LawRevision, KnowledgeEvidence Knowledge, IReadOnlyList<long> ReservedItems,
    int FormationQuality, int Risk, long Due, int RecoveryRemaining, ParentStructureEvidence Parent);
public sealed record PendingTribulation(int TargetLayer, bool Completion, long Due);
public sealed record MartialProgress(long Work = 0, int Injury = 0, long LastMonth = -1);

/// <summary>Only domain-owned metadata is serialized. Shared identity, realm, layer, life and health stay on Person.</summary>
public sealed record CultivationState
{
    public long BodyId { get; init; }
    public long SoulId { get; init; }
    public long Revision { get; init; }
    public long LastTrainingMonth { get; init; } = -1;
    public string RootOrigin { get; init; } = "출생 유전 입력";
    public Elements RootGrowth { get; init; }
    public Elements RootDamage { get; init; }
    public Elements Raw { get; init; }
    public Elements Refined { get; init; }
    public Elements Circulating { get; init; }
    public RealmStructure? Structure { get; init; }
    public string ActivePath { get; init; } = "";
    public string AuxiliaryPath { get; init; } = "";
    public int Damage { get; init; }
    public int Load { get; init; }
    public long EntryWork { get; init; }
    public long EntryLastMonth { get; init; } = -1;
    public CultivationAttempt? Attempt { get; init; }
    public PendingTribulation? PendingTrial { get; init; }
    public IReadOnlyList<CultivationFormation> Formations { get; init; } = [];
    public IReadOnlyList<PathConversion> Conversions { get; init; } = [];
    public MartialProgress Martial { get; init; } = new();
}

/// <summary>Developer-tuned provisional defaults; month and basis-point units remain explicit at the boundary.</summary>
public sealed record CultivationPolicy
{
    public long Revision { get; init; } = 1;
    public long FlowPerMonth { get; init; } = 5000;
    public int EntryMonths { get; init; } = 36;
    public int QiMonths { get; init; } = 600;
    public int FoundationMonths { get; init; } = 1200;
    public int UpperDurationPercent { get; init; } = 150;
    public int MinimumUnderstanding { get; init; } = 1000;
    public int MinimumControl { get; init; } = 3000;
    public int UpperControl { get; init; } = 4000;
    public int CompletionControl { get; init; } = 4500;
    public int MinimumFormation { get; init; } = 2500;
    public int BaseRisk { get; init; } = 100;
    public int TrialRisk { get; init; } = 200;
    public int StabilizationMonths { get; init; } = 3;
    public int ConversionMonths { get; init; } = 3;
    public int RecoveryPerMonth { get; init; } = 500;
    public int TrialHealthCost { get; init; } = 5;
    public int FailureHealthCost { get; init; } = 20;
    public int FailureDamage { get; init; } = 2000;
    public int ConversionLoss { get; init; } = 1000;
    public int ReserveLimitMonths { get; init; } = 2;
    public int MartialMonthsPerStage { get; init; } = 60;
    public int MartialMaximumYears { get; init; } = 600;
    public long QiLifespanMonths { get; init; } = 1440;
    public long FoundationLifespanMonths { get; init; } = 3600;
    public long GoldenCoreLifespanMonths { get; init; } = 7200;
    public long HalfImmortalLifespanMonths { get; init; } = 120000;
    public int UpperLifespanPercent { get; init; } = 200;

    public long RequiredWork(Realm realm)
    {
        if (realm == Realm.Mortal) return EntryMonths * FlowPerMonth;
        if (realm == Realm.Qi) return QiMonths * FlowPerMonth;
        long months = FoundationMonths;
        for (var i = 2; i < (int)realm; i++) months = Math.Min(100000000, months * UpperDurationPercent / 100);
        return checked(months * FlowPerMonth);
    }
    public long Lifespan(Realm realm)
    {
        if (realm == Realm.Mortal) return 960;
        if (realm == Realm.Qi) return QiLifespanMonths;
        if (realm == Realm.Foundation) return FoundationLifespanMonths;
        if (realm == Realm.GoldenCore) return GoldenCoreLifespanMonths;
        if (realm == Realm.HalfImmortal) return HalfImmortalLifespanMonths;
        var lifespan = GoldenCoreLifespanMonths;
        for (var current = Realm.GoldenCore; current < realm; current++)
            lifespan = Math.Min(HalfImmortalLifespanMonths, lifespan * UpperLifespanPercent / 100);
        return lifespan;
    }
}
