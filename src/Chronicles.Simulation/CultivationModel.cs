namespace Chronicles.Simulation;

public enum RealmStage : byte
{
    QiRefining,
    FoundationEstablishment,
    GoldenCore,
    NascentSoul,
    SpiritTransformation,
    VoidRefinement,
    BodyIntegration,
    TribulationCrossing,
    Mahayana,
    HalfImmortal,
}

public enum MinorStage : byte
{
    Floor1 = 1,
    Floor2,
    Floor3,
    Floor4,
    Floor5,
    Floor6,
    Floor7,
    Floor8,
    Floor9,
}

// 기존 0~6 wire 값은 schema 7 저장을 읽기 위해 유지한다.
public enum CultivationState : byte
{
    RealmCultivation = 0,
    PreparingBreakthrough = 1,
    PreflightReady = 2,
    AttemptingBreakthrough = 3,
    Bottleneck = 4,
    Stabilizing = 5,
    Completed = 6,
    PreCultivation = 7,
    EntryTraining = 8,
    Stable = 9,
}

public enum CultivationLifecyclePhase : byte
{
    PreCultivation,
    EntryTraining,
    RealmCultivation,
    Bottleneck,
    BreakthroughPreparation,
    Attempt,
    Stabilizing,
    Stable,
}

public enum CultivationBottleneckCause : byte
{
    None,
    Knowledge,
    Body,
    SpiritualRoot,
    Resources,
    Stability,
    EntryGate,
    RealmComponent,
    Method,
}

public enum BreakthroughClass : byte
{
    None,
    Entry,
    NormalFloor,
    EarlyMiddleBottleneck,
    MiddleLateBottleneck,
    MajorRealm,
    FloorNineCompletion,
    GreatPerfectionCompletion,
}

public enum BreakthroughOutcome : byte
{
    None,
    Blocked,
    Success,
    Failure,
}

public enum CarryoverPolicy : byte
{
    NoCarryover,
    FullCarryover,
    LimitedCarryover,
}

public enum FoundationRoute : byte
{
    None,
    Human,
    Earth,
    Heaven,
}

public enum CultivationValueAvailability : byte
{
    Actual,
    NotCalculated,
    NotApplicable,
    Unavailable,
    Unapproved,
    Blocked,
}

public enum CultivationFormulaKind : byte
{
    None,
    BaselineDuration,
    CultivationSpeed,
    ProgressAccumulation,
    BottleneckState,
    BreakthroughReadiness,
    BreakthroughResult,
    LifespanIncrease,
    InjuryRecovery,
}

public readonly record struct CultivationFormulaDefinition(
    CultivationFormulaKind Kind,
    string StableId,
    int Revision,
    string Purpose,
    bool RequiresReview);

public static class CultivationFormulaRegistry
{
    private static readonly CultivationFormulaDefinition[] Definitions =
    [
        new(CultivationFormulaKind.BaselineDuration, "F-CULT-001", 1,
            "경지 기준 수행 기간", false),
        new(CultivationFormulaKind.CultivationSpeed, "F-CULT-002", 1,
            "수행 속도", true),
        new(CultivationFormulaKind.ProgressAccumulation, "F-CULT-003", 1,
            "수행 진행 누적", false),
        new(CultivationFormulaKind.BottleneckState, "F-CULT-004", 1,
            "병목 상태와 차단 원인", true),
        new(CultivationFormulaKind.BreakthroughReadiness, "F-CULT-005", 1,
            "돌파 준비도와 Preflight", true),
        new(CultivationFormulaKind.BreakthroughResult, "F-CULT-006", 1,
            "돌파 판정과 Typed Result", true),
        new(CultivationFormulaKind.LifespanIncrease, "F-CULT-007", 1,
            "경지 돌파 수명 증가", false),
        new(CultivationFormulaKind.InjuryRecovery, "F-CULT-008", 1,
            "부상과 회복 변화량", true),
    ];

    public static IReadOnlyList<CultivationFormulaDefinition> All => Definitions;

    public static CultivationFormulaDefinition Get(CultivationFormulaKind kind)
    {
        if (kind == CultivationFormulaKind.None || !Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        return Definitions[(int)kind - 1];
    }
}

public readonly record struct CultivationGateSnapshot(
    int GateNumber,
    int CoreChannelHundredths,
    int SupportCompositeHundredths,
    int TemperingBonusHundredths,
    int GateScoreHundredths,
    bool CorePassed,
    bool ScorePassed,
    bool Passed);

public readonly record struct RealmComponentSnapshot(
    string StableId,
    string KoreanName,
    int ValueHundredths,
    int LimitHundredths);

public readonly record struct CultivationTraceState(
    long Tick,
    int FormulaRevision,
    BreakthroughClass AttemptClass,
    BreakthroughOutcome Outcome,
    RealmStage Realm,
    byte SourceFloor,
    byte DestinationFloor,
    int WorkBefore,
    int WorkGain,
    int WorkAfter,
    int RequiredWork,
    int CarryoverWork,
    int BaseChanceBasisPoints,
    int ModifierBasisPoints,
    int FinalChanceBasisPoints,
    int RollBasisPoints,
    CultivationBottleneckCause Blocker,
    CultivationFormulaKind FormulaKind = CultivationFormulaKind.None,
    long AuthorityRevisionBefore = 0,
    long AuthorityRevisionAfter = 0)
{
    public static CultivationTraceState Empty => new(
        0,
        CultivationRules.FormulaRevision,
        BreakthroughClass.None,
        BreakthroughOutcome.None,
        RealmStage.QiRefining,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        -1,
        CultivationBottleneckCause.None);
}

public sealed record CultivationReadProjection(
    CultivationLifecyclePhase Phase,
    int Floor,
    int WorkBefore,
    int LastGain,
    int WorkAfter,
    int RequiredWork,
    int PendingCarryoverWork,
    int InnateAptitudeCompositeHundredths,
    decimal BaselineCultivationYears,
    bool FloorNineCompleted,
    bool GreatPerfectionAchieved,
    FoundationRoute FoundationRoute,
    IReadOnlyList<CultivationGateSnapshot> EntryGates,
    IReadOnlyList<RealmComponentSnapshot> Components,
    CultivationTraceState LatestTrace,
    long AuthorityRevision);

public readonly record struct CultivationCheckpoint(
    int PendingCarryoverWork,
    int LastWorkBefore,
    int LastWorkGain,
    bool FloorNineCompleted,
    bool GreatPerfectionAchieved,
    byte EntryGatePassMask,
    FoundationRoute FoundationRoute,
    ushort[] ComponentValues,
    ushort[] ComponentLimits,
    ushort[] FormationValues,
    CultivationTraceState LatestTrace,
    ushort[]? EntryGateTempering = null,
    long AuthorityRevision = 0);

public readonly record struct ProvisionalBalanceDefinition(
    string StableId,
    int Revision,
    string Value,
    string Basis,
    bool RequiresReview);
