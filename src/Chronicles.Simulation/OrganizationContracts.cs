using Chronicles.Core;

namespace Chronicles.Simulation;

/// <summary>
/// 조직 종류·역할·명령 결과·checkpoint DTO와 승인 창설 규칙의 공용 계약이다.
/// 권위 저장소 구현과 분리해 Queries, Persistence, 테스트가 안정 계약만 검색하게 한다.
/// </summary>
public enum OrganizationKind : byte
{
    Sect,
    Clan,
    Dynasty,
}

public enum OrganizationStatus : byte
{
    Active,
    Vacant,
    Declining,
    Dissolved,
    Merged,
}

public enum OrganizationRole : byte
{
    Member,
    Founder,
    Leader,
    Elder,
    VassalRepresentative,
    Deputy,
    Treasurer,
}

public enum OrganizationRightKind : byte
{
    Headquarters,
    Residence,
    Territory,
    SpiritVein,
}

public enum OrganizationDiplomacyKind : byte
{
    Neutral,
    Allied,
    Hostile,
    Vassal,
    Overlord,
}

public enum OrganizationHistoryKind : byte
{
    Founded,
    MemberJoined,
    MemberDeparted,
    LeaderSucceeded,
    LeadershipVacant,
    DeclineStarted,
    Dissolved,
    Split,
    Merged,
    AssetTransferred,
    DiplomacyChanged,
    SoulReconnected,
    RoleChanged,
    LoyaltyChanged,
    TreasuryAdjusted,
    RightTransferred,
}

public enum OrganizationCommandStatus : byte
{
    Applied,
    InvalidEvidence,
    GateNotMet,
    KindCapacityReached,
    MembershipConflict,
    MemberCapacityReached,
    OrganizationNotFound,
    OrganizationInactive,
    InvalidTransition,
    InsufficientAssets,
    PermissionDenied,
    Duplicate,
    LoyaltyBroken,
    Overflow,
}

public readonly record struct OrganizationMembershipState(
    int AgentIndex,
    PersonIdentityId PersonId,
    SoulIdentityId SoulId,
    OrganizationRole Role,
    long JoinedTick,
    int LoyaltyHundredths,
    bool Active);

public readonly record struct OrganizationRightState(
    OrganizationRightKind Kind,
    AtlasNodeId LocationNodeId,
    long GrantedTick);

public readonly record struct OrganizationDiplomacyState(
    OrganizationId OtherOrganizationId,
    OrganizationDiplomacyKind Kind,
    long ChangedTick);

public readonly record struct OrganizationStrategyState(
    int GrowthVsStabilityHundredths,
    int CooperationVsDominanceHundredths,
    int RiskToleranceHundredths);

public readonly record struct OrganizationKindProfile(
    int LivingGenealogyCount,
    int ControlledProvinceCount,
    int VassalOrganizationCount,
    int AverageSupportHundredths,
    bool HasFoundingClan,
    bool HasValidRuler,
    bool IsFormalized);

public readonly record struct OrganizationHistoryEntry(
    long Sequence,
    long Tick,
    OrganizationHistoryKind Kind,
    int PrimaryAgentIndex,
    OrganizationId? OtherOrganizationId,
    long Value);

public sealed record OrganizationAuthorityCheckpoint(
    OrganizationId Id,
    OrganizationKind Kind,
    OrganizationStatus Status,
    string Name,
    int FounderAgentIndex,
    PersonIdentityId FounderPersonId,
    int LeaderAgentIndex,
    PersonIdentityId LeaderPersonId,
    long FoundedTick,
    long StatusChangedTick,
    long TreasurySpiritStones,
    long ReputationHundredths,
    OrganizationMembershipState[] Members,
    OrganizationRightState[] Rights,
    OrganizationDiplomacyState[] Diplomacy,
    OrganizationHistoryEntry[] History,
    OrganizationStrategyState Strategy = default,
    OrganizationKindProfile Profile = default);

public sealed record OrganizationStoreCheckpoint(
    long NextOrganizationId,
    long HistorySequence,
    long AuthorityRevision,
    OrganizationAuthorityCheckpoint[] Organizations,
    long LastMaintenanceMonth = -1,
    int EvaluationCursor = 0,
    long[]? FoundingReserves = null,
    int[]? PreparationSponsors = null);

public sealed record OrganizationMemberSnapshot(
    AgentId AgentId,
    string Name,
    OrganizationRole Role,
    int LoyaltyHundredths,
    bool Active);

public sealed record OrganizationSnapshot(
    OrganizationId Id,
    OrganizationKind Kind,
    OrganizationStatus Status,
    string Name,
    AgentId FounderAgentId,
    string FounderName,
    AgentId? LeaderAgentId,
    string LeaderName,
    SimulationTick FoundedTick,
    long TreasurySpiritStones,
    long ReputationHundredths,
    IReadOnlyList<OrganizationMemberSnapshot> Members,
    IReadOnlyList<OrganizationRightState> Rights,
    IReadOnlyList<OrganizationDiplomacyState> Diplomacy,
    IReadOnlyList<OrganizationHistoryEntry> History,
    OrganizationStrategyState Strategy = default,
    OrganizationKindProfile Profile = default);

public sealed record WorldOrganizationSnapshot(
    long AuthorityRevision,
    IReadOnlyList<OrganizationSnapshot> Organizations,
    int ActiveSects,
    int ActiveClans,
    int ActiveDynasties,
    bool CompositePowerRankingAvailable,
    string CompositePowerDataState,
    int PreparingSectPlans = 0,
    long TotalFoundingReserves = 0)
{
    public static WorldOrganizationSnapshot Empty { get; } = new(
        0,
        Array.Empty<OrganizationSnapshot>(),
        0,
        0,
        0,
        false,
        "DataUnavailable",
        0,
        0);
}

public sealed record OrganizationFoundingEvidence(
    OrganizationKind Kind,
    string Name,
    int FounderAgentIndex,
    PersonIdentityId FounderPersonId,
    SoulIdentityId FounderSoulId,
    RealmStage FounderRealm,
    long AvailableSpiritStones,
    int OrganizationOperationAptitudeHundredths,
    int FoundingAptitudeHundredths,
    int LeadershipHundredths,
    int AverageTrustHundredths,
    int AverageSupportHundredths,
    int ControlledProvinceCount,
    bool HasActualHeadquartersSpiritVeinRight,
    bool HasActualResidenceRight,
    bool HasFoundingClan,
    bool HasValidRuler,
    AtlasNodeId PrimaryLocationNodeId,
    int[] CandidateMemberAgentIndices,
    PersonIdentityId[] CandidateMemberPersonIds,
    SoulIdentityId[] CandidateMemberSoulIds,
    int LivingGenealogyCount,
    OrganizationId[] VassalOrganizationIds);

public readonly record struct OrganizationCommandResult(
    OrganizationCommandStatus Status,
    OrganizationId? OrganizationId,
    long SpiritStoneCost,
    string Message)
{
    public bool IsApplied => Status == OrganizationCommandStatus.Applied;
}

public static class OrganizationRules
{
    public const int SectFounderMinimumPledgedMembers = 8;
    public const int SectMinimumAverageTrustHundredths = 5_500;
    public const int SectMinimumOrganizationOperationHundredths = 5_000;
    public const int SectMinimumFoundingAptitudeHundredths = 5_000;
    public const long SectFoundingSpiritStoneCost = 10_000;
    public const int MaximumActiveSects = 80;
    public const int MaximumSectMembers = 100;
    public const int MaximumDirectSubsects = 10;
    public const int MaximumSectPreparationPlans = SectFounderMinimumPledgedMembers;
    public const int LoyaltyMaximumHundredths = 10_000;
    public const int LoyaltyActiveMinimumHundredths = 6_000;
    public const int LoyaltyStrainedMinimumHundredths = 3_000;
    public const int HistoryRetentionLimit = 100;
    // PC_TEMP_REVIEW ORG-020-02: 외부 인물 초청의 상호 신뢰 경계는 후속 밸런스 검토 대상이다.
    public const int SectJoinMinimumMutualTrustHundredths = 3_000;

    public const int ClanMinimumLivingGenealogy = 8;
    public const int ClanMinimumLeadershipHundredths = 5_000;
    public const long ClanFoundingSpiritStoneCost = 5_000;

    public const int DynastyMinimumControlledProvinces = 1;
    public const int DynastyMinimumVassalOrganizations = 3;
    public const long DynastyFoundingSpiritStoneCost = 100_000;
    public const int DynastyMinimumAverageSupportHundredths = 6_000;

    // PC_TEMP_REVIEW ORG-020-01: 평가 분산 주기와 공석 쇠퇴 경계는 다음 버전 검토안이다.
    public const int MonthlyCandidateProbeCount = RelationRules.CandidateProbeCount;
    public const int VacantToDecliningYears = 10;
    public const int DecliningToDissolvedYears = 50;
    public const string TemporaryMappingReviewId = "PC-TEMP-ORG-APTITUDE-MAPPING-020";
    public const string SuccessionReviewId = "PC-TEMP-ORG-SUCCESSION-020";
    public const string StrategyReviewId = "PC-TEMP-ORG-STRATEGY-020";
    public const string AiDecisionReviewId = "PC-TEMP-ORG-AI-DECISION-020";
    public const string FoundingContributionReviewId = "PC-TEMP-ORG-CONTRIBUTION-020";

    public static int GetSectPreparationCapacity(int population)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        return Math.Clamp(population / 1_000, 1, MaximumSectPreparationPlans);
    }

    public static OrganizationCommandStatus ValidateFounding(
        OrganizationFoundingEvidence evidence,
        int activeKindCount)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (!Enum.IsDefined(evidence.Kind)
            || string.IsNullOrWhiteSpace(evidence.Name)
            || evidence.FounderAgentIndex < 0
            || evidence.FounderPersonId.Value == 0
            || evidence.FounderSoulId.Value == 0
            || evidence.PrimaryLocationNodeId.Value == 0
            || evidence.CandidateMemberAgentIndices is null
            || evidence.CandidateMemberPersonIds is null
            || evidence.CandidateMemberSoulIds is null
            || evidence.VassalOrganizationIds is null
            || evidence.CandidateMemberAgentIndices.Length != evidence.CandidateMemberPersonIds.Length
            || evidence.CandidateMemberAgentIndices.Length != evidence.CandidateMemberSoulIds.Length
            || evidence.CandidateMemberAgentIndices.Distinct().Count()
                != evidence.CandidateMemberAgentIndices.Length)
        {
            return OrganizationCommandStatus.InvalidEvidence;
        }

        if (evidence.Kind == OrganizationKind.Sect && activeKindCount >= MaximumActiveSects)
        {
            return OrganizationCommandStatus.KindCapacityReached;
        }

        return evidence.Kind switch
        {
            OrganizationKind.Sect when evidence.FounderRealm < RealmStage.GoldenCore
                || evidence.CandidateMemberAgentIndices.Length < SectFounderMinimumPledgedMembers
                || evidence.AverageTrustHundredths < SectMinimumAverageTrustHundredths
                || evidence.OrganizationOperationAptitudeHundredths
                    < SectMinimumOrganizationOperationHundredths
                || evidence.FoundingAptitudeHundredths < SectMinimumFoundingAptitudeHundredths
                || evidence.AvailableSpiritStones < SectFoundingSpiritStoneCost
                || !evidence.HasActualHeadquartersSpiritVeinRight =>
                OrganizationCommandStatus.GateNotMet,
            OrganizationKind.Clan when evidence.FounderRealm < RealmStage.GoldenCore
                || evidence.LivingGenealogyCount < ClanMinimumLivingGenealogy
                || evidence.LeadershipHundredths < ClanMinimumLeadershipHundredths
                || evidence.AvailableSpiritStones < ClanFoundingSpiritStoneCost
                || !evidence.HasActualResidenceRight =>
                OrganizationCommandStatus.GateNotMet,
            OrganizationKind.Dynasty when evidence.ControlledProvinceCount
                    < DynastyMinimumControlledProvinces
                || !evidence.HasFoundingClan
                || !evidence.HasValidRuler
                || evidence.VassalOrganizationIds.Distinct().Count()
                    < DynastyMinimumVassalOrganizations
                || evidence.AvailableSpiritStones < DynastyFoundingSpiritStoneCost
                || evidence.AverageSupportHundredths
                    < DynastyMinimumAverageSupportHundredths =>
                OrganizationCommandStatus.GateNotMet,
            _ => OrganizationCommandStatus.Applied,
        };
    }

    public static long GetFoundingCost(OrganizationKind kind) => kind switch
    {
        OrganizationKind.Sect => SectFoundingSpiritStoneCost,
        OrganizationKind.Clan => ClanFoundingSpiritStoneCost,
        OrganizationKind.Dynasty => DynastyFoundingSpiritStoneCost,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static int GetMemberCapacity(OrganizationKind kind) => kind switch
    {
        OrganizationKind.Sect => MaximumSectMembers,
        OrganizationKind.Clan or OrganizationKind.Dynasty => int.MaxValue,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

