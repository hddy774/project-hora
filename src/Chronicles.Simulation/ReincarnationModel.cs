using Chronicles.Core;

namespace Chronicles.Simulation;

public enum ReincarnationStage : byte
{
    None,
    Entering,
    Crossing,
    LateCrossing,
    CrossingComplete,
    PreparingRebirth,
}

public enum ReincarnationTraceDomain : byte
{
    AutobiographicalMemory,
    RelationshipAndSocialMemory,
    CultivationPrinciplesAndRealmKnowledge,
    TechniqueSpellPillAndCraftingKnowledge,
    WorldFactionAndEnvironmentKnowledge,
    EmbodiedAndActionTrace,
}

public enum LifeEndKind : byte
{
    None,
    Lifespan,
    Combat,
}

public enum TracePreservationLevel : byte
{
    Lost,
    Fragment,
    Partial,
    Intact,
}

public enum TraceAccessLevel : byte
{
    Sealed,
    Latent,
    Partial,
    Immediate,
}

public readonly record struct LifeRecordId(ulong Value)
{
    public override string ToString() => $"L{Value:X16}";
}

public readonly record struct ReincarnationJourneyId(ulong Value)
{
    public override string ToString() => $"R{Value:X16}";
}

public readonly record struct HistoricalPeakState(
    RealmStage Realm,
    byte RealmLayer,
    long CombatPower,
    long AchievedTick,
    int LifeNumber,
    PersonIdentityId PersonId);

public readonly record struct LifeLineageEntry(
    int AgentIndex,
    LifeRecordId Id,
    LifeRecordId? PredecessorId,
    int LifeNumber,
    SoulIdentityId SoulId,
    BodyIdentityId BodyId,
    PersonIdentityId PersonId,
    string PersonName,
    long BirthTick,
    long? DeathTick,
    LifeEndKind EndKind,
    HistoricalPeakState PersonalPeak,
    ReincarnationJourneyId? JourneyId);

public readonly record struct ReincarnationTraceCandidate(
    ReincarnationTraceDomain Domain,
    ushort PreservationHundredths,
    ushort AccessibleHundredths);

public readonly record struct ReincarnationTraceState(
    int AgentIndex,
    ReincarnationJourneyId JourneyId,
    int SourceLifeNumber,
    ReincarnationTraceDomain Domain,
    ushort PreservedHundredths,
    ushort PendingAccessibleHundredths,
    ushort AccessibleHundredths,
    ushort IntegratedHundredths,
    TracePreservationLevel PreservationLevel,
    TraceAccessLevel AccessLevel,
    bool HasEvidence,
    long AuthorityRevision);

public sealed record ReincarnationJourneyState(
    int AgentIndex,
    ReincarnationJourneyId Id,
    LifeRecordId SourceLifeRecordId,
    int SourceLifeNumber,
    SoulIdentityId SoulId,
    BodyIdentityId SourceBodyId,
    PersonIdentityId SourcePersonId,
    RealmStage SourceRealm,
    ReincarnationStage Stage,
    long StartedTick,
    long StageStartedTick,
    long NextTransitionTick,
    long RebirthReadyTick,
    int RequiredYears,
    long? CompletedTick,
    long AuthorityRevision,
    bool RequiresReview);

public readonly record struct ReincarnationStageAdvanceResult(
    ReincarnationStage PreviousStage,
    ReincarnationStage CurrentStage,
    long NextTransitionTick,
    bool CrossingCompleted,
    bool ReadyForRebirth);

public sealed record ReincarnationStoreCheckpoint(
    int FormulaRevision,
    bool RequiresReview,
    int Population,
    long[] AuthorityRevisions,
    int[] FirstTrackedLifeNumbers,
    bool[] CompleteLineage,
    LifeLineageEntry[] Lineage,
    ReincarnationJourneyState[] Journeys,
    ReincarnationTraceState[] Traces,
    HistoricalPeakState[] HistoricalPeaks);

public readonly record struct HistoricalPeakSnapshot(
    RealmStage Realm,
    byte RealmLayer,
    long CombatPower,
    SimulationTick AchievedTick,
    int LifeNumber,
    PersonIdentityId PersonId);

public sealed record LifeLineageSnapshot(
    LifeRecordId Id,
    LifeRecordId? PredecessorId,
    int LifeNumber,
    string PersonName,
    SoulIdentityId SoulId,
    BodyIdentityId BodyId,
    PersonIdentityId PersonId,
    SimulationTick BirthTick,
    SimulationTick? DeathTick,
    LifeEndKind EndKind,
    HistoricalPeakSnapshot PersonalPeak,
    ReincarnationJourneyId? JourneyId);

public readonly record struct ReincarnationTraceSnapshot(
    ReincarnationJourneyId JourneyId,
    int SourceLifeNumber,
    ReincarnationTraceDomain Domain,
    int PreservedHundredths,
    int PendingAccessibleHundredths,
    int AccessibleHundredths,
    int IntegratedHundredths,
    TracePreservationLevel PreservationLevel,
    TraceAccessLevel AccessLevel,
    bool HasEvidence,
    long AuthorityRevision);

public sealed record ReincarnationJourneySnapshot(
    ReincarnationJourneyId Id,
    int SourceLifeNumber,
    BodyIdentityId SourceBodyId,
    PersonIdentityId SourcePersonId,
    RealmStage SourceRealm,
    ReincarnationStage Stage,
    SimulationTick StartedTick,
    SimulationTick StageStartedTick,
    SimulationTick NextTransitionTick,
    SimulationTick RebirthReadyTick,
    int RequiredYears,
    int ProgressBasisPoints,
    SimulationTick? CompletedTick,
    long AuthorityRevision,
    bool RequiresReview);

public sealed record ReincarnationAgentSnapshot(
    SoulIdentityId SoulId,
    long AuthorityRevision,
    int FirstTrackedLifeNumber,
    bool IsCompleteLineage,
    bool RequiresReview,
    HistoricalPeakSnapshot HistoricalPeak,
    ReincarnationJourneySnapshot? CurrentJourney,
    IReadOnlyList<LifeLineageSnapshot> Lineage,
    IReadOnlyList<ReincarnationTraceSnapshot> Traces);

public static class ReincarnationRules
{
    public const int FormulaRevision = 1;
    public const int TraceDomainCount = 6;
    public const long MinimumHistoricalCombatPower = 100;
    public const string StageReviewId = "PC-TEMP-REINCARNATION-STAGES-113";
    public const string UpperRealmDurationReviewId = "PC-TEMP-REINCARNATION-UPPER-DURATION-113";
    public const string TraceFormulaReviewId = "PC-TEMP-REINCARNATION-TRACE-113";
    public const string BirthCandidateReviewId = "PC-TEMP-REINCARNATION-BIRTH-CANDIDATE-113";

    public static IReadOnlyList<ReincarnationTraceDomain> AllTraceDomains { get; } =
        Array.AsReadOnly(Enum.GetValues<ReincarnationTraceDomain>());

    public static int GetTotalDurationYears(RealmStage realm) => realm switch
    {
        RealmStage.QiRefining => 3,
        RealmStage.FoundationEstablishment => 5,
        RealmStage.GoldenCore => 8,
        RealmStage.NascentSoul => 13,
        RealmStage.SpiritTransformation => 21,
        RealmStage.VoidRefinement => 34,
        RealmStage.BodyIntegration => 55,
        RealmStage.TribulationCrossing => 89,
        RealmStage.Mahayana => 144,
        RealmStage.HalfImmortal => 233,
        _ => throw new ArgumentOutOfRangeException(nameof(realm)),
    };

    public static long GetTotalDurationTicks(RealmStage realm) => checked(
        (long)GetTotalDurationYears(realm) * GameCalendar.TicksPerYear);

    public static long GetRebirthReadyTick(long startedTick, RealmStage realm) => checked(
        startedTick + GetTotalDurationTicks(realm));

    public static long GetStageBoundaryTick(
        long startedTick,
        RealmStage realm,
        ReincarnationStage stage)
    {
        var numerator = stage switch
        {
            ReincarnationStage.Entering => 1,
            ReincarnationStage.Crossing => 8,
            ReincarnationStage.LateCrossing => 10,
            ReincarnationStage.CrossingComplete => 11,
            ReincarnationStage.PreparingRebirth => 12,
            _ => throw new ArgumentOutOfRangeException(nameof(stage)),
        };
        var duration = GetTotalDurationTicks(realm);
        return checked(startedTick + ((duration * numerator) + 11) / 12);
    }

    public static ReincarnationStage GetNextStage(ReincarnationStage stage) => stage switch
    {
        ReincarnationStage.Entering => ReincarnationStage.Crossing,
        ReincarnationStage.Crossing => ReincarnationStage.LateCrossing,
        ReincarnationStage.LateCrossing => ReincarnationStage.CrossingComplete,
        ReincarnationStage.CrossingComplete => ReincarnationStage.PreparingRebirth,
        ReincarnationStage.PreparingRebirth => ReincarnationStage.None,
        _ => throw new ArgumentOutOfRangeException(nameof(stage)),
    };

    public static ReincarnationStage GetStageAt(
        long startedTick,
        long currentTick,
        RealmStage realm)
    {
        foreach (var stage in new[]
        {
            ReincarnationStage.Entering,
            ReincarnationStage.Crossing,
            ReincarnationStage.LateCrossing,
            ReincarnationStage.CrossingComplete,
            ReincarnationStage.PreparingRebirth,
        })
        {
            if (currentTick < GetStageBoundaryTick(startedTick, realm, stage))
            {
                return stage;
            }
        }

        return ReincarnationStage.PreparingRebirth;
    }

    public static long GetStageStartedTick(
        long startedTick,
        RealmStage realm,
        ReincarnationStage stage) => stage switch
    {
        ReincarnationStage.Entering => startedTick,
        ReincarnationStage.Crossing => GetStageBoundaryTick(
            startedTick, realm, ReincarnationStage.Entering),
        ReincarnationStage.LateCrossing => GetStageBoundaryTick(
            startedTick, realm, ReincarnationStage.Crossing),
        ReincarnationStage.CrossingComplete => GetStageBoundaryTick(
            startedTick, realm, ReincarnationStage.LateCrossing),
        ReincarnationStage.PreparingRebirth => GetStageBoundaryTick(
            startedTick, realm, ReincarnationStage.CrossingComplete),
        _ => throw new ArgumentOutOfRangeException(nameof(stage)),
    };

    public static int GetProgressBasisPoints(long startedTick, long tick, RealmStage realm)
    {
        var duration = GetTotalDurationTicks(realm);
        return checked((int)Math.Clamp((tick - startedTick) * 10_000 / duration, 0, 10_000));
    }

    public static TracePreservationLevel GetPreservationLevel(int value) => value switch
    {
        >= 8_000 => TracePreservationLevel.Intact,
        >= 5_500 => TracePreservationLevel.Partial,
        >= 3_000 => TracePreservationLevel.Fragment,
        _ => TracePreservationLevel.Lost,
    };

    public static TraceAccessLevel GetAccessLevel(int value) => value switch
    {
        >= 8_500 => TraceAccessLevel.Immediate,
        >= 6_000 => TraceAccessLevel.Partial,
        >= 3_000 => TraceAccessLevel.Latent,
        _ => TraceAccessLevel.Sealed,
    };

    public static LifeRecordId CreateLifeRecordId(int agentIndex, int lifeNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(agentIndex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lifeNumber);
        return new LifeRecordId(
            (checked((ulong)(uint)agentIndex + 1UL) << 32) | checked((uint)lifeNumber));
    }

    public static ReincarnationJourneyId CreateJourneyId(int agentIndex, int lifeNumber)
    {
        var life = CreateLifeRecordId(agentIndex, lifeNumber).Value;
        return new ReincarnationJourneyId(life ^ 0xA57E_0000_0000_0000UL);
    }

    public static bool IsUpperRealmDurationProvisional(RealmStage realm) =>
        realm >= RealmStage.VoidRefinement;
}
