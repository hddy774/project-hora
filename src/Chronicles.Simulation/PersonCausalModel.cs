namespace Chronicles.Simulation;

public readonly record struct SoulIdentityId(ulong Value)
{
    public override string ToString() => $"S{Value:D8}";
}

public readonly record struct BodyIdentityId(ulong Value)
{
    public override string ToString() => $"B{Value:X16}";
}

public readonly record struct PersonIdentityId(ulong Value)
{
    public override string ToString() => $"P{Value:X16}";
}

public enum PersonLifeState : byte
{
    Living,
    ReincarnationRiver,
}

public enum AgentGoalKind : byte
{
    ReachRankOne,
    EnterCultivation,
    AdvanceRealm,
    CompleteBreakthrough,
    ResolveBottleneck,
    RecoverBodyAndQi,
    ImproveTechnique,
    ImproveSpell,
    AcquireResources,
    SeekStrongerOpponent,
    ExploreWorld,
    CompleteReincarnation,
}

public enum DecisionReasonCode : byte
{
    InitialLifeCultivation,
    ForcedRecovery,
    BreakthroughPipeline,
    BottleneckRemediation,
    CultivationEntry,
    CultivationPriority,
    ChallengeOpportunity,
    MapOpportunity,
    GrowthBalance,
    CombatChallenge,
    CombatResponse,
    CombatRecovery,
    ReincarnationHandoff,
}

public enum DecisionResolutionKind : byte
{
    Selected,
    Forced,
    Deferred,
    Blocked,
}

public readonly record struct PersonIdentitySnapshot(
    SoulIdentityId SoulId,
    BodyIdentityId BodyId,
    PersonIdentityId PersonId,
    PersonLifeState LifeState);

public readonly record struct AgentDecisionTraceState(
    long Tick,
    long AuthorityRevision,
    AgentGoalKind PrimaryGoal,
    AgentGoalKind SupportingGoal,
    AgentActivity SelectedActivity,
    DecisionReasonCode SelectedReason,
    AgentActivity? AlternativeActivity,
    DecisionResolutionKind Resolution);

public readonly record struct AgentDecisionSnapshot(
    string ContractId,
    int ContractRevision,
    bool RequiresReview,
    AgentDecisionTraceState Latest);

public readonly record struct PersonIdentityCheckpoint(
    SoulIdentityId SoulId,
    BodyIdentityId BodyId,
    PersonIdentityId PersonId,
    PersonLifeState LifeState);

public readonly record struct AgentDecisionCheckpoint(
    long AuthorityRevision,
    AgentDecisionTraceState Latest);

public static class PersonCausalRules
{
    public const string DecisionContractId = "PC-AI-DECISION-001";
    public const int DecisionContractRevision = 1;

    // 후보 Utility·전환 비용의 정밀 수치는 Alpha 1.1.3에서 아직 실측 전이다.
    public const string ProvisionalDecisionReviewId = "PC-TEMP-AI-UTILITY-113";

    public static SoulIdentityId CreateSoulId(int agentIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(agentIndex);
        return new SoulIdentityId(checked((ulong)agentIndex + 1));
    }

    public static BodyIdentityId CreateBodyId(int agentIndex, int lifeNumber) =>
        new(ComposeLifeIdentity(agentIndex, lifeNumber));

    public static PersonIdentityId CreatePersonId(int agentIndex, int lifeNumber) =>
        new(ComposeLifeIdentity(agentIndex, lifeNumber));

    private static ulong ComposeLifeIdentity(int agentIndex, int lifeNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(agentIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lifeNumber);
        return ((ulong)(uint)lifeNumber << 32) | checked((uint)agentIndex + 1U);
    }
}
