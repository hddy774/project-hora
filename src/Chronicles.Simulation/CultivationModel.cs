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

public readonly record struct CultivationGateSnapshot(
    int GateNumber,
    int CoreChannelHundredths,
    int SupportCompositeHundredths,
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
    CultivationBottleneckCause Blocker)
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
    CultivationTraceState LatestTrace);

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
    CultivationTraceState LatestTrace);

public readonly record struct ProvisionalBalanceDefinition(
    string StableId,
    int Revision,
    string Value,
    string Basis,
    bool RequiresReview);
