using Chronicles.Core;

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
    Environment,
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
    ActualZero,
    NotCalculated,
    NotApplicable,
    Unavailable,
    Unapproved,
    Blocked,
}

public enum CultivationAttemptEffectFamily : byte
{
    BreakthroughMethod,
    Resource,
    Insight,
    BodyLifeStability,
    TemporaryOpportunity,
    LuckFate,
    Surplus,
}

public enum CultivationFailureEffectKind : byte
{
    CleanFailure,
    AttemptResourceConsumed,
    FailureResourceLoss,
    PreparationOrWorkDisturbance,
    RealmComponentDisturbance,
    Injury,
    SoulOrMentalBurden,
    FatalBodyLoss,
    RetryBlock,
}

public enum CultivationRngDomain : byte
{
    NotUsed,
    BreakthroughSuccess,
    FailureSeverity,
}

public enum FoundationSourceKind : byte
{
    None,
    InternalBodyAndSpiritualRoot,
    LocationSpiritVeins,
    ManifestationEnvironmentWindow,
}

[Flags]
public enum FoundationProcedureStep : byte
{
    None = 0,
    AchievementConverged = 1 << 0,
    RouteBound = 1 << 1,
    SourceAligned = 1 << 2,
    FormationAttempted = 1 << 3,
    ResultHandedToStabilization = 1 << 4,
    Complete = AchievementConverged
        | RouteBound
        | SourceAligned
        | FormationAttempted
        | ResultHandedToStabilization,
}

public readonly record struct CultivationAttemptEffectDefinition(
    string StableId,
    int Revision,
    CultivationAttemptEffectFamily Family,
    BreakthroughClass ApplicableTransitionClass,
    int ModifierBasisPoints,
    string ConflictGroupKey,
    int ConflictPriority,
    int LocalCapBasisPoints)
{
    public RegistryStableId RegistryId =>
        AuthorityRegistry.CreateStableId("cmip.cultivation.attempt-effect", StableId);
}

public readonly record struct CultivationAttemptEffectCandidate(
    RegistryStableId EffectId,
    int Revision,
    bool Applicable);

public readonly record struct CultivationAttemptEffectResolution(
    int RegistryRevision,
    int CandidateCount,
    int SelectedCount,
    int RawModifierBasisPoints,
    CultivationValueAvailability Availability)
{
    public static CultivationAttemptEffectResolution NoApprovedEffects => new(
        CultivationAttemptEffectRegistry.CurrentRevision,
        0,
        0,
        0,
        CultivationValueAvailability.Unapproved);
}

public static class CultivationAttemptEffectRegistry
{
    public const int CurrentRevision = 1;

    // Alpha 1.1.3 정본의 승인된 Concrete Attempt Effect Catalog는 0개다.
    private static readonly CultivationAttemptEffectDefinition[] ApprovedDefinitions = [];

    public static IReadOnlyList<CultivationAttemptEffectDefinition> All => ApprovedDefinitions;

    public static CultivationAttemptEffectResolution Resolve(
        BreakthroughClass transitionClass,
        ReadOnlySpan<CultivationAttemptEffectCandidate> candidates)
    {
        if (transitionClass is BreakthroughClass.None
            or BreakthroughClass.Entry
            or BreakthroughClass.GreatPerfectionCompletion)
        {
            throw new ArgumentOutOfRangeException(nameof(transitionClass));
        }

        if (candidates.Length == 0)
        {
            return CultivationAttemptEffectResolution.NoApprovedEffects;
        }

        var selectedCount = 0;
        var rawModifier = 0;
        var availability = CultivationValueAvailability.ActualZero;
        for (var candidateIndex = 0; candidateIndex < candidates.Length; candidateIndex++)
        {
            var candidate = candidates[candidateIndex];
            if (!candidate.Applicable)
            {
                continue;
            }

            var definitionIndex = FindApprovedDefinition(candidate.EffectId, candidate.Revision);
            if (definitionIndex < 0)
            {
                availability = CultivationValueAvailability.Unapproved;
                continue;
            }

            var definition = ApprovedDefinitions[definitionIndex];
            if (definition.ApplicableTransitionClass != transitionClass)
            {
                continue;
            }

            var superseded = false;
            for (var otherIndex = 0; otherIndex < candidates.Length; otherIndex++)
            {
                if (otherIndex == candidateIndex || !candidates[otherIndex].Applicable)
                {
                    continue;
                }

                var otherDefinitionIndex = FindApprovedDefinition(
                    candidates[otherIndex].EffectId,
                    candidates[otherIndex].Revision);
                if (otherDefinitionIndex < 0)
                {
                    continue;
                }

                var other = ApprovedDefinitions[otherDefinitionIndex];
                if (other.ApplicableTransitionClass == transitionClass
                    && string.Equals(
                        other.ConflictGroupKey,
                        definition.ConflictGroupKey,
                        StringComparison.Ordinal)
                    && other.ConflictPriority > definition.ConflictPriority)
                {
                    superseded = true;
                    break;
                }
            }

            if (superseded)
            {
                continue;
            }

            selectedCount++;
            rawModifier = checked(rawModifier + Math.Clamp(
                definition.ModifierBasisPoints,
                -definition.LocalCapBasisPoints,
                definition.LocalCapBasisPoints));
            availability = rawModifier == 0
                ? CultivationValueAvailability.ActualZero
                : CultivationValueAvailability.Actual;
        }

        return new CultivationAttemptEffectResolution(
            CurrentRevision,
            candidates.Length,
            selectedCount,
            rawModifier,
            availability);
    }

    private static int FindApprovedDefinition(RegistryStableId id, int revision)
    {
        for (var index = 0; index < ApprovedDefinitions.Length; index++)
        {
            if (ApprovedDefinitions[index].RegistryId == id
                && ApprovedDefinitions[index].Revision == revision)
            {
                return index;
            }
        }

        return -1;
    }
}

public readonly record struct CultivationFailureRiskContract(
    string StableId,
    int Revision,
    CultivationFailureEffectKind Effect,
    int WorkLossBasisPoints,
    int ComponentLossHundredths,
    int InjuryPoints,
    bool FatalBodyLoss,
    long RetryBlockMonths);

public readonly record struct CultivationFailureResult(
    CultivationFailureEffectKind Effect,
    int WorkLost,
    int ComponentLostHundredths,
    int InjuryPoints,
    bool FatalBodyLoss,
    long RetryBlockMonths,
    CultivationValueAvailability Availability);

public static class CultivationFailureRiskRegistry
{
    public const int CurrentRevision = 1;
    public const string CleanFailureContractId = "cultivation.failure.clean";

    private static readonly CultivationFailureRiskContract CleanFailure = new(
        CleanFailureContractId,
        CurrentRevision,
        CultivationFailureEffectKind.CleanFailure,
        WorkLossBasisPoints: 0,
        ComponentLossHundredths: 0,
        InjuryPoints: 0,
        FatalBodyLoss: false,
        RetryBlockMonths: 0);

    public static CultivationFailureRiskContract Get(BreakthroughClass transitionClass)
    {
        if (transitionClass is BreakthroughClass.None
            or BreakthroughClass.Entry
            or BreakthroughClass.GreatPerfectionCompletion)
        {
            throw new ArgumentOutOfRangeException(nameof(transitionClass));
        }

        return CleanFailure;
    }

    public static CultivationFailureResult Resolve(BreakthroughClass transitionClass)
    {
        var contract = Get(transitionClass);
        return new CultivationFailureResult(
            contract.Effect,
            WorkLost: 0,
            contract.ComponentLossHundredths,
            contract.InjuryPoints,
            contract.FatalBodyLoss,
            contract.RetryBlockMonths,
            CultivationValueAvailability.ActualZero);
    }
}

public readonly record struct FoundationSourceSnapshot(
    FoundationRoute Route,
    FoundationSourceKind Kind,
    ushort LocationId,
    int SourceRevision,
    long FrozenTick,
    int SourceCount,
    long RemainingSupply,
    CultivationValueAvailability Availability);

public readonly record struct FoundationFormationCheckpoint(
    int MethodRevision,
    FoundationProcedureStep CompletedSteps,
    FoundationSourceSnapshot Source);

public sealed record FoundationFormationSnapshot(
    string MethodStableId,
    int MethodRevision,
    FoundationProcedureStep CompletedSteps,
    FoundationSourceSnapshot Source,
    IReadOnlyList<int> QualityComponentsHundredths,
    bool RequiresStabilization);

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
    long AuthorityRevisionAfter = 0,
    int AttemptEffectRegistryRevision = 0,
    int CandidateEffectCount = 0,
    int SelectedEffectCount = 0,
    CultivationValueAvailability AttemptEffectAvailability = CultivationValueAvailability.NotCalculated,
    CultivationFailureEffectKind FailureEffect = CultivationFailureEffectKind.CleanFailure,
    CultivationValueAvailability FailureEffectAvailability = CultivationValueAvailability.NotApplicable,
    CultivationRngDomain RngDomain = CultivationRngDomain.NotUsed,
    bool AttemptSnapshotFrozen = false,
    FoundationProcedureStep FoundationProcedureSteps = FoundationProcedureStep.None,
    int EnvironmentFactorBasisPoints = 0,
    CultivationValueAvailability EnvironmentAvailability = CultivationValueAvailability.NotCalculated,
    CultivationValueAvailability LawUnderstandingAvailability = CultivationValueAvailability.Unavailable,
    CultivationValueAvailability LuckFateAvailability = CultivationValueAvailability.Unavailable)
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
    long AuthorityRevision,
    FoundationFormationSnapshot? FoundationFormation = null,
    long CultivationExperience = 0,
    CultivationActionPlanSnapshot? ActionPlan = null);

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
    long AuthorityRevision = 0,
    FoundationFormationCheckpoint? FoundationFormation = null,
    CultivationActionPlanState? ActionPlan = null,
    long CultivationExperience = 0,
    ushort FormationInstallationExperience = 0);

public readonly record struct ProvisionalBalanceDefinition(
    string StableId,
    int Revision,
    string Value,
    string Basis,
    bool RequiresReview);
