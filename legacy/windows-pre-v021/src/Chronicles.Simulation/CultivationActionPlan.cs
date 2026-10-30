using Chronicles.Core;

namespace Chronicles.Simulation;

public enum CultivationPlanIntensity : byte
{
    Leisurely,
    Steady,
    ClosedDoor,
}

public enum CultivationResourcePolicy : byte
{
    None,
    SpiritStones,
    GrowthPills,
    Mixed,
}

public enum CultivationFormationSupport : byte
{
    None,
    ExistingAtLocation,
    SelfInstalled,
    ExternalAssistance,
}

public enum CultivationTeachingRole : byte
{
    None,
    Instructor,
    Learner,
}

/// <summary>
/// 수선자가 한 번 결정한 뒤 여러 달 동안 유지하는 권위 행동 계획이다.
/// 월별 성장 pulse는 유지하지만 목표·계획·행동 선택은 계획 경계에서만 다시 계산한다.
/// </summary>
public readonly record struct CultivationActionPlanState(
    long AuthorityRevision,
    long StartedTick,
    long PlannedEndTick,
    long PlannedMonths,
    long CompletedMonths,
    ushort LocationId,
    CultivationPlanIntensity Intensity,
    CultivationResourcePolicy ResourcePolicy,
    long SpiritStoneBudget,
    long SpiritStonesConsumed,
    int GrowthPillBudget,
    int GrowthPillsConsumed,
    CultivationFormationSupport FormationSupport,
    ushort FormationKnowledgeHundredths,
    ushort FormationInstallationExperienceHundredths,
    bool FormationInstallationSucceeded,
    CultivationTeachingRole TeachingRole,
    int TeachingPartnerAgent,
    int FrozenTeachingBonusBasisPoints,
    int FrozenConditionFactorBasisPoints,
    bool IsActive)
{
    public static CultivationActionPlanState Empty => new(
        AuthorityRevision: 0,
        StartedTick: 0,
        PlannedEndTick: 0,
        PlannedMonths: 0,
        CompletedMonths: 0,
        LocationId: 0,
        CultivationPlanIntensity.Steady,
        CultivationResourcePolicy.None,
        SpiritStoneBudget: 0,
        SpiritStonesConsumed: 0,
        GrowthPillBudget: 0,
        GrowthPillsConsumed: 0,
        CultivationFormationSupport.None,
        FormationKnowledgeHundredths: 0,
        FormationInstallationExperienceHundredths: 0,
        FormationInstallationSucceeded: false,
        CultivationTeachingRole.None,
        TeachingPartnerAgent: -1,
        FrozenTeachingBonusBasisPoints: CultivationPlanRules.NeutralFactorBasisPoints,
        FrozenConditionFactorBasisPoints: CultivationPlanRules.NeutralFactorBasisPoints,
        IsActive: false);
}

public sealed record CultivationActionPlanSnapshot(
    long AuthorityRevision,
    SimulationTick StartedTick,
    SimulationTick PlannedEndTick,
    long PlannedMonths,
    long CompletedMonths,
    ushort LocationId,
    CultivationPlanIntensity Intensity,
    CultivationResourcePolicy ResourcePolicy,
    long SpiritStoneBudget,
    long SpiritStonesConsumed,
    int GrowthPillBudget,
    int GrowthPillsConsumed,
    CultivationFormationSupport FormationSupport,
    int FormationKnowledgeHundredths,
    int FormationInstallationExperienceHundredths,
    bool FormationInstallationSucceeded,
    CultivationTeachingRole TeachingRole,
    AgentId? TeachingPartner,
    int TeachingBonusBasisPoints,
    int ConditionFactorBasisPoints,
    bool IsActive,
    bool RequiresReview);

public static class CultivationPlanRules
{
    public const int CurrentRevision = 1;
    public const int NeutralFactorBasisPoints = 10_000;
    public const int MinimumPlanMonths = 1;
    public const int MaximumPlanMonths = 120_000;
    public const int ExistingFormationFactorBasisPoints = 10_500;
    public const int SelfInstalledFormationFactorBasisPoints = 11_000;
    public const int ExternalFormationFactorBasisPoints = 11_500;
    public const int SpiritStoneFactorBasisPoints = 10_500;
    public const int GrowthPillFactorBasisPoints = 11_500;
    public const int MinimumSelfInstallationKnowledgeHundredths = 3_000;
    public const int BaseSelfInstallationChanceBasisPoints = 4_000;
    public const int MaximumTeachingBonusBasisPoints = 13_000;
    public const int InstructorExperiencePerMonth = 100;

    public static int GetDefaultDurationMonths(RealmStage realm, CultivationState state)
    {
        if (state is CultivationState.EntryTraining
            or CultivationState.PreparingBreakthrough
            or CultivationState.PreflightReady
            or CultivationState.AttemptingBreakthrough
            or CultivationState.Stabilizing)
        {
            return MinimumPlanMonths;
        }

        var lifespanYears = RealmRules.GetProfile(realm).MaximumLifespanYears;
        var provisionalMonths = checked(lifespanYears * GameCalendar.MonthsPerYear / 30);
        return checked((int)Math.Clamp(
            provisionalMonths,
            MinimumPlanMonths,
            MaximumPlanMonths));
    }

    public static int GetIntensityFactorBasisPoints(CultivationPlanIntensity intensity) =>
        intensity switch
        {
            CultivationPlanIntensity.Leisurely => 8_500,
            CultivationPlanIntensity.Steady => NeutralFactorBasisPoints,
            CultivationPlanIntensity.ClosedDoor => 11_500,
            _ => throw new ArgumentOutOfRangeException(nameof(intensity)),
        };

    public static int GetFormationFactorBasisPoints(CultivationFormationSupport support) =>
        support switch
        {
            CultivationFormationSupport.None => NeutralFactorBasisPoints,
            CultivationFormationSupport.ExistingAtLocation => ExistingFormationFactorBasisPoints,
            CultivationFormationSupport.SelfInstalled => SelfInstalledFormationFactorBasisPoints,
            CultivationFormationSupport.ExternalAssistance => ExternalFormationFactorBasisPoints,
            _ => throw new ArgumentOutOfRangeException(nameof(support)),
        };

    public static int CombineConditionFactors(
        CultivationPlanIntensity intensity,
        CultivationFormationSupport formation,
        int teachingBonusBasisPoints)
    {
        long factor = GetIntensityFactorBasisPoints(intensity);
        factor = factor * GetFormationFactorBasisPoints(formation) / NeutralFactorBasisPoints;
        factor = factor * Math.Clamp(
            teachingBonusBasisPoints,
            NeutralFactorBasisPoints,
            MaximumTeachingBonusBasisPoints) / NeutralFactorBasisPoints;
        return checked((int)Math.Clamp(factor, 5_000, 20_000));
    }

    public static int GetSelfInstallationChanceBasisPoints(
        int knowledgeHundredths,
        int experienceHundredths) => Math.Clamp(
            BaseSelfInstallationChanceBasisPoints
            + (Math.Max(0, knowledgeHundredths) / 2)
            + (Math.Max(0, experienceHundredths) / 4),
            0,
            9_500);

    public static int GetBodyConditionBasisPoints(int health, int maximumHealth)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(health);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumHealth);
        return checked((int)Math.Clamp(
            (long)health * NeutralFactorBasisPoints / maximumHealth,
            0L,
            NeutralFactorBasisPoints));
    }
}
