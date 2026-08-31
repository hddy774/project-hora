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

public sealed class ReincarnationStore
{
    private readonly List<LifeLineageEntry>[] _lineage;
    private readonly List<ReincarnationJourneyState>[] _journeys;
    private readonly List<ReincarnationTraceState>[] _traces;
    private readonly HistoricalPeakState[] _historicalPeaks;
    private readonly long[] _authorityRevisions;
    private readonly int[] _firstTrackedLifeNumbers;
    private readonly bool[] _completeLineage;
    private readonly long[] _snapshotCacheRevisions;
    private readonly IReadOnlyList<LifeLineageSnapshot>?[] _lineageSnapshotCache;
    private readonly IReadOnlyList<ReincarnationTraceSnapshot>?[] _traceSnapshotCache;

    public ReincarnationStore(int population)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        Population = population;
        _lineage = new List<LifeLineageEntry>[population];
        _journeys = new List<ReincarnationJourneyState>[population];
        _traces = new List<ReincarnationTraceState>[population];
        _historicalPeaks = new HistoricalPeakState[population];
        _authorityRevisions = new long[population];
        _firstTrackedLifeNumbers = new int[population];
        _completeLineage = new bool[population];
        _snapshotCacheRevisions = new long[population];
        Array.Fill(_snapshotCacheRevisions, -1);
        _lineageSnapshotCache = new IReadOnlyList<LifeLineageSnapshot>?[population];
        _traceSnapshotCache = new IReadOnlyList<ReincarnationTraceSnapshot>?[population];
        for (var index = 0; index < population; index++)
        {
            _lineage[index] = [];
            _journeys[index] = [];
            _traces[index] = [];
        }
    }

    public int Population { get; }

    public void InitializeFirstLife(
        int agentIndex,
        SoulIdentityId soulId,
        BodyIdentityId bodyId,
        PersonIdentityId personId,
        string personName,
        long birthTick,
        RealmStage realm,
        byte realmLayer,
        long combatPower) => InitializeTrackedLife(
            agentIndex,
            lifeNumber: 1,
            soulId,
            bodyId,
            personId,
            personName,
            birthTick,
            realm,
            realmLayer,
            combatPower,
            birthTick,
            completeLineage: true);

    public void MigrateLegacyCurrentLife(
        int agentIndex,
        int lifeNumber,
        SoulIdentityId soulId,
        BodyIdentityId bodyId,
        PersonIdentityId personId,
        string personName,
        long birthTick,
        RealmStage realm,
        byte realmLayer,
        long combatPower,
        long currentTick,
        bool inReincarnationRiver,
        long? journeyStartedTick,
        LifeEndKind endKind)
    {
        InitializeTrackedLife(
            agentIndex,
            lifeNumber,
            soulId,
            bodyId,
            personId,
            personName,
            birthTick,
            realm,
            realmLayer,
            combatPower,
            inReincarnationRiver ? journeyStartedTick ?? currentTick : currentTick,
            completeLineage: lifeNumber == 1);
        if (!inReincarnationRiver)
        {
            return;
        }

        if (journeyStartedTick is null || journeyStartedTick < birthTick || journeyStartedTick > currentTick)
        {
            throw new InvalidDataException("기존 윤회 시작 시점이 잘못되었습니다.");
        }

        BeginJourneyCore(
            agentIndex,
            journeyStartedTick.Value,
            endKind,
            realm,
            candidates: default,
            hasEvidence: false,
            migrationTick: currentTick);
    }

    public void UpdatePeak(
        int agentIndex,
        RealmStage realm,
        byte realmLayer,
        long combatPower,
        long tick)
    {
        ValidateAgent(agentIndex);
        if (_lineage[agentIndex].Count == 0)
        {
            throw new InvalidOperationException("초기화되지 않은 생의 최고 기록을 갱신할 수 없습니다.");
        }

        var current = _lineage[agentIndex][^1];
        if (current.DeathTick is not null)
        {
            return;
        }

        var candidate = new HistoricalPeakState(
            realm,
            realmLayer,
            combatPower,
            tick,
            current.LifeNumber,
            current.PersonId);
        var personalPeak = IsHigherPeak(candidate, current.PersonalPeak)
            ? candidate
            : current.PersonalPeak;
        var historicalPeak = IsHigherPeak(candidate, _historicalPeaks[agentIndex])
            ? candidate
            : _historicalPeaks[agentIndex];
        if (personalPeak == current.PersonalPeak && historicalPeak == _historicalPeaks[agentIndex])
        {
            return;
        }

        _lineage[agentIndex][^1] = current with { PersonalPeak = personalPeak };
        _historicalPeaks[agentIndex] = historicalPeak;
        _authorityRevisions[agentIndex] = checked(_authorityRevisions[agentIndex] + 1);
    }

    public ReincarnationJourneyState BeginJourney(
        int agentIndex,
        long deathTick,
        LifeEndKind deathKind,
        RealmStage sourceRealm,
        ReadOnlySpan<ReincarnationTraceCandidate> candidates) => BeginJourneyCore(
            agentIndex,
            deathTick,
            deathKind,
            sourceRealm,
            candidates,
            hasEvidence: true,
            migrationTick: null);

    public ReincarnationStageAdvanceResult AdvanceStage(int agentIndex, long tick)
    {
        ValidateAgent(agentIndex);
        var journeyIndex = GetActiveJourneyIndex(agentIndex);
        var current = _journeys[agentIndex][journeyIndex];
        if (tick != current.NextTransitionTick)
        {
            throw new InvalidOperationException("윤회 단계는 예정된 경계 tick에서만 전이할 수 있습니다.");
        }

        if (current.Stage == ReincarnationStage.PreparingRebirth)
        {
            return new ReincarnationStageAdvanceResult(
                current.Stage,
                current.Stage,
                current.RebirthReadyTick,
                CrossingCompleted: false,
                ReadyForRebirth: true);
        }

        var nextStage = ReincarnationRules.GetNextStage(current.Stage);
        var nextTick = ReincarnationRules.GetStageBoundaryTick(
            current.StartedTick,
            current.SourceRealm,
            nextStage);
        var revision = checked(_authorityRevisions[agentIndex] + 1);
        _authorityRevisions[agentIndex] = revision;
        var updated = current with
        {
            Stage = nextStage,
            StageStartedTick = tick,
            NextTransitionTick = nextTick,
            AuthorityRevision = revision,
        };
        _journeys[agentIndex][journeyIndex] = updated;
        return new ReincarnationStageAdvanceResult(
            current.Stage,
            nextStage,
            nextTick,
            CrossingCompleted: nextStage == ReincarnationStage.CrossingComplete,
            ReadyForRebirth: false);
    }

    public void CompleteRebirth(
        int agentIndex,
        long tick,
        SoulIdentityId soulId,
        BodyIdentityId newBodyId,
        PersonIdentityId newPersonId,
        string newPersonName,
        RealmStage initialRealm,
        byte initialRealmLayer,
        long initialCombatPower)
    {
        EnsureReadyForRebirth(agentIndex, tick, soulId);
        var journeyIndex = GetActiveJourneyIndex(agentIndex);
        var journey = _journeys[agentIndex][journeyIndex];

        var previous = _lineage[agentIndex][^1];
        var lifeNumber = checked(previous.LifeNumber + 1);
        var revision = checked(_authorityRevisions[agentIndex] + 1);
        _authorityRevisions[agentIndex] = revision;
        _journeys[agentIndex][journeyIndex] = journey with
        {
            CompletedTick = tick,
            AuthorityRevision = revision,
        };
        for (var traceIndex = 0; traceIndex < _traces[agentIndex].Count; traceIndex++)
        {
            var trace = _traces[agentIndex][traceIndex];
            if (trace.JourneyId != journey.Id)
            {
                continue;
            }

            var accessible = trace.HasEvidence
                ? Math.Min(trace.PreservedHundredths, trace.PendingAccessibleHundredths)
                : 0;
            _traces[agentIndex][traceIndex] = trace with
            {
                AccessibleHundredths = checked((ushort)accessible),
                IntegratedHundredths = 0,
                AccessLevel = ReincarnationRules.GetAccessLevel(accessible),
                AuthorityRevision = revision,
            };
        }

        var peak = new HistoricalPeakState(
            initialRealm,
            initialRealmLayer,
            initialCombatPower,
            tick,
            lifeNumber,
            newPersonId);
        _lineage[agentIndex].Add(new LifeLineageEntry(
            agentIndex,
            ReincarnationRules.CreateLifeRecordId(agentIndex, lifeNumber),
            previous.Id,
            lifeNumber,
            soulId,
            newBodyId,
            newPersonId,
            newPersonName,
            tick,
            DeathTick: null,
            LifeEndKind.None,
            peak,
            JourneyId: null));
        if (IsHigherPeak(peak, _historicalPeaks[agentIndex]))
        {
            _historicalPeaks[agentIndex] = peak;
        }
    }

    internal void EnsureReadyForRebirth(
        int agentIndex,
        long tick,
        SoulIdentityId soulId)
    {
        ValidateAgent(agentIndex);
        var journey = _journeys[agentIndex][GetActiveJourneyIndex(agentIndex)];
        var traceCount = _traces[agentIndex].Count(trace => trace.JourneyId == journey.Id);
        if (journey.Stage != ReincarnationStage.PreparingRebirth
            || tick != journey.RebirthReadyTick
            || journey.SoulId != soulId
            || traceCount != ReincarnationRules.TraceDomainCount)
        {
            throw new InvalidOperationException("새 생을 시작할 준비가 완료되지 않았습니다.");
        }
    }

    public ReincarnationJourneyState? GetCurrentJourney(int agentIndex)
    {
        ValidateAgent(agentIndex);
        return _journeys[agentIndex].LastOrDefault(journey => journey.CompletedTick is null);
    }

    public ReincarnationAgentSnapshot GetSnapshot(int agentIndex, long currentTick)
    {
        ValidateAgent(agentIndex);
        if (_lineage[agentIndex].Count == 0)
        {
            throw new InvalidOperationException("초기화되지 않은 윤회 상태입니다.");
        }

        if (_snapshotCacheRevisions[agentIndex] != _authorityRevisions[agentIndex]
            || _lineageSnapshotCache[agentIndex] is null
            || _traceSnapshotCache[agentIndex] is null)
        {
            _lineageSnapshotCache[agentIndex] = Array.AsReadOnly(_lineage[agentIndex]
                .Select(entry => new LifeLineageSnapshot(
                    entry.Id,
                    entry.PredecessorId,
                    entry.LifeNumber,
                    entry.PersonName,
                    entry.SoulId,
                    entry.BodyId,
                    entry.PersonId,
                    new SimulationTick(entry.BirthTick),
                    entry.DeathTick is { } death ? new SimulationTick(death) : null,
                    entry.EndKind,
                    ToSnapshot(entry.PersonalPeak),
                    entry.JourneyId))
                .ToArray());
            _traceSnapshotCache[agentIndex] = Array.AsReadOnly(_traces[agentIndex]
                .Select(trace => new ReincarnationTraceSnapshot(
                    trace.JourneyId,
                    trace.SourceLifeNumber,
                    trace.Domain,
                    trace.PreservedHundredths,
                    trace.PendingAccessibleHundredths,
                    trace.AccessibleHundredths,
                    trace.IntegratedHundredths,
                    trace.PreservationLevel,
                    trace.AccessLevel,
                    trace.HasEvidence,
                    trace.AuthorityRevision))
                .ToArray());
            _snapshotCacheRevisions[agentIndex] = _authorityRevisions[agentIndex];
        }
        var lineages = _lineageSnapshotCache[agentIndex]!;
        var traces = _traceSnapshotCache[agentIndex]!;
        var current = GetCurrentJourney(agentIndex);
        var currentSnapshot = current is null
            ? null
            : new ReincarnationJourneySnapshot(
                current.Id,
                current.SourceLifeNumber,
                current.SourceBodyId,
                current.SourcePersonId,
                current.SourceRealm,
                current.Stage,
                new SimulationTick(current.StartedTick),
                new SimulationTick(current.StageStartedTick),
                new SimulationTick(current.NextTransitionTick),
                new SimulationTick(current.RebirthReadyTick),
                current.RequiredYears,
                ReincarnationRules.GetProgressBasisPoints(
                    current.StartedTick,
                    currentTick,
                    current.SourceRealm),
                current.CompletedTick is { } completed ? new SimulationTick(completed) : null,
                current.AuthorityRevision,
                current.RequiresReview);
        return new ReincarnationAgentSnapshot(
            _lineage[agentIndex][^1].SoulId,
            _authorityRevisions[agentIndex],
            _firstTrackedLifeNumbers[agentIndex],
            _completeLineage[agentIndex],
            RequiresReview: true,
            ToSnapshot(_historicalPeaks[agentIndex]),
            currentSnapshot,
            lineages,
            traces);
    }

    public ReincarnationStoreCheckpoint CreateCheckpoint() => new(
        ReincarnationRules.FormulaRevision,
        RequiresReview: true,
        Population,
        _authorityRevisions.ToArray(),
        _firstTrackedLifeNumbers.ToArray(),
        _completeLineage.ToArray(),
        _lineage.SelectMany(entries => entries).Select(Clone).ToArray(),
        _journeys.SelectMany(entries => entries).Select(Clone).ToArray(),
        _traces.SelectMany(entries => entries).ToArray(),
        _historicalPeaks.ToArray());

    public static ReincarnationStore Restore(
        ReincarnationStoreCheckpoint checkpoint,
        long? maximumTick = null)
    {
        if (!IsValidCheckpoint(checkpoint, maximumTick))
        {
            throw new InvalidDataException("윤회 체크포인트가 잘못되었습니다.");
        }

        var store = new ReincarnationStore(checkpoint.Population);
        Array.Copy(checkpoint.AuthorityRevisions, store._authorityRevisions, checkpoint.Population);
        Array.Copy(checkpoint.FirstTrackedLifeNumbers, store._firstTrackedLifeNumbers, checkpoint.Population);
        Array.Copy(checkpoint.CompleteLineage, store._completeLineage, checkpoint.Population);
        Array.Copy(checkpoint.HistoricalPeaks, store._historicalPeaks, checkpoint.Population);
        foreach (var entry in checkpoint.Lineage)
        {
            store._lineage[entry.AgentIndex].Add(Clone(entry));
        }
        foreach (var journey in checkpoint.Journeys)
        {
            store._journeys[journey.AgentIndex].Add(Clone(journey));
        }
        foreach (var trace in checkpoint.Traces)
        {
            store._traces[trace.AgentIndex].Add(trace);
        }
        return store;
    }

    public static bool IsValidCheckpoint(
        ReincarnationStoreCheckpoint? checkpoint,
        long? maximumTick = null)
    {
        if (checkpoint is not { } value
            || maximumTick is < 0
            || value.FormulaRevision != ReincarnationRules.FormulaRevision
            || !value.RequiresReview
            || value.Population <= 0
            || value.AuthorityRevisions is not { } revisions
            || revisions.Length != value.Population
            || revisions.Any(revision => revision < 1)
            || value.FirstTrackedLifeNumbers is not { } firstTracked
            || firstTracked.Length != value.Population
            || firstTracked.Any(life => life < 1)
            || value.CompleteLineage is not { } complete
            || complete.Length != value.Population
            || value.HistoricalPeaks is not { } peaks
            || peaks.Length != value.Population
            || value.Lineage is null
            || value.Journeys is null
            || value.Traces is null)
        {
            return false;
        }

        var lineageByAgent = new List<LifeLineageEntry>?[value.Population];
        var journeysByAgent = new List<ReincarnationJourneyState>?[value.Population];
        var tracesByAgent = new List<ReincarnationTraceState>?[value.Population];
        foreach (var entry in value.Lineage)
        {
            if ((uint)entry.AgentIndex >= (uint)value.Population)
            {
                return false;
            }
            (lineageByAgent[entry.AgentIndex] ??= []).Add(entry);
        }
        foreach (var journey in value.Journeys)
        {
            if ((uint)journey.AgentIndex >= (uint)value.Population)
            {
                return false;
            }
            (journeysByAgent[journey.AgentIndex] ??= []).Add(journey);
        }
        foreach (var trace in value.Traces)
        {
            if ((uint)trace.AgentIndex >= (uint)value.Population)
            {
                return false;
            }
            (tracesByAgent[trace.AgentIndex] ??= []).Add(trace);
        }

        for (var agent = 0; agent < value.Population; agent++)
        {
            if (!IsValidAgentState(
                    agent,
                    firstTracked[agent],
                    complete[agent],
                    revisions[agent],
                    peaks[agent],
                    lineageByAgent[agent]?.ToArray() ?? [],
                    journeysByAgent[agent]?.ToArray() ?? [],
                    tracesByAgent[agent]?.ToArray() ?? [],
                    maximumTick))
            {
                return false;
            }
        }

        return true;
    }

    public ulong ComputeStateHash()
    {
        var hash = 14695981039346656037UL;
        Add(ref hash, ReincarnationRules.FormulaRevision);
        Add(ref hash, 1);
        Add(ref hash, unchecked((ulong)Population));
        foreach (var value in _authorityRevisions)
        {
            Add(ref hash, unchecked((ulong)value));
        }
        foreach (var value in _firstTrackedLifeNumbers)
        {
            Add(ref hash, unchecked((ulong)value));
        }
        foreach (var value in _completeLineage)
        {
            Add(ref hash, value ? 1UL : 0UL);
        }
        foreach (var peak in _historicalPeaks)
        {
            AddPeakHash(ref hash, peak);
        }
        for (var agent = 0; agent < Population; agent++)
        {
            foreach (var entry in _lineage[agent])
            {
                Add(ref hash, entry.Id.Value);
                Add(ref hash, entry.PredecessorId?.Value ?? 0);
                Add(ref hash, unchecked((ulong)entry.LifeNumber));
                Add(ref hash, entry.SoulId.Value);
                Add(ref hash, entry.BodyId.Value);
                Add(ref hash, entry.PersonId.Value);
                AddString(ref hash, entry.PersonName);
                Add(ref hash, unchecked((ulong)entry.BirthTick));
                Add(ref hash, unchecked((ulong)(entry.DeathTick ?? -1) + 1));
                Add(ref hash, (byte)entry.EndKind);
                AddPeakHash(ref hash, entry.PersonalPeak);
                Add(ref hash, entry.JourneyId?.Value ?? 0);
            }
            foreach (var journey in _journeys[agent])
            {
                Add(ref hash, journey.Id.Value);
                Add(ref hash, journey.SourceLifeRecordId.Value);
                Add(ref hash, unchecked((ulong)journey.SourceLifeNumber));
                Add(ref hash, journey.SoulId.Value);
                Add(ref hash, journey.SourceBodyId.Value);
                Add(ref hash, journey.SourcePersonId.Value);
                Add(ref hash, (byte)journey.SourceRealm);
                Add(ref hash, (byte)journey.Stage);
                Add(ref hash, unchecked((ulong)journey.StartedTick));
                Add(ref hash, unchecked((ulong)journey.StageStartedTick));
                Add(ref hash, unchecked((ulong)journey.NextTransitionTick));
                Add(ref hash, unchecked((ulong)journey.RebirthReadyTick));
                Add(ref hash, unchecked((ulong)journey.RequiredYears));
                Add(ref hash, unchecked((ulong)(journey.CompletedTick ?? -1) + 1));
                Add(ref hash, unchecked((ulong)journey.AuthorityRevision));
                Add(ref hash, journey.RequiresReview ? 1UL : 0UL);
            }
            foreach (var trace in _traces[agent])
            {
                Add(ref hash, trace.JourneyId.Value);
                Add(ref hash, unchecked((ulong)trace.SourceLifeNumber));
                Add(ref hash, (byte)trace.Domain);
                Add(ref hash, trace.PreservedHundredths);
                Add(ref hash, trace.PendingAccessibleHundredths);
                Add(ref hash, trace.AccessibleHundredths);
                Add(ref hash, trace.IntegratedHundredths);
                Add(ref hash, (byte)trace.PreservationLevel);
                Add(ref hash, (byte)trace.AccessLevel);
                Add(ref hash, trace.HasEvidence ? 1UL : 0UL);
                Add(ref hash, unchecked((ulong)trace.AuthorityRevision));
            }
        }
        return hash;
    }

    private void InitializeTrackedLife(
        int agentIndex,
        int lifeNumber,
        SoulIdentityId soulId,
        BodyIdentityId bodyId,
        PersonIdentityId personId,
        string personName,
        long birthTick,
        RealmStage realm,
        byte realmLayer,
        long combatPower,
        long peakTick,
        bool completeLineage)
    {
        ValidateAgent(agentIndex);
        if (_lineage[agentIndex].Count != 0
            || lifeNumber < 1
            || string.IsNullOrWhiteSpace(personName)
            || birthTick < 0
            || peakTick < birthTick
            || !RealmRules.IsValidMinorStage(realm, realmLayer)
            || combatPower < ReincarnationRules.MinimumHistoricalCombatPower)
        {
            throw new InvalidDataException("첫 추적 생의 윤회 상태가 잘못되었습니다.");
        }

        var peak = new HistoricalPeakState(
            realm,
            realmLayer,
            combatPower,
            peakTick,
            lifeNumber,
            personId);
        _lineage[agentIndex].Add(new LifeLineageEntry(
            agentIndex,
            ReincarnationRules.CreateLifeRecordId(agentIndex, lifeNumber),
            PredecessorId: null,
            lifeNumber,
            soulId,
            bodyId,
            personId,
            personName,
            birthTick,
            DeathTick: null,
            LifeEndKind.None,
            peak,
            JourneyId: null));
        _historicalPeaks[agentIndex] = peak;
        _firstTrackedLifeNumbers[agentIndex] = lifeNumber;
        _completeLineage[agentIndex] = completeLineage;
        _authorityRevisions[agentIndex] = 1;
    }

    private ReincarnationJourneyState BeginJourneyCore(
        int agentIndex,
        long deathTick,
        LifeEndKind deathKind,
        RealmStage sourceRealm,
        ReadOnlySpan<ReincarnationTraceCandidate> candidates,
        bool hasEvidence,
        long? migrationTick)
    {
        ValidateAgent(agentIndex);
        var lineage = _lineage[agentIndex];
        if (lineage.Count == 0
            || lineage[^1].DeathTick is not null
            || GetCurrentJourney(agentIndex) is not null
            || deathKind == LifeEndKind.None
            || deathTick < lineage[^1].BirthTick
            || (hasEvidence && !IsValidCandidates(candidates)))
        {
            throw new InvalidOperationException("윤회 여정을 시작할 수 없는 상태입니다.");
        }

        var currentLife = lineage[^1];
        var journeyId = ReincarnationRules.CreateJourneyId(agentIndex, currentLife.LifeNumber);
        var revision = checked(_authorityRevisions[agentIndex] + 1);
        _authorityRevisions[agentIndex] = revision;
        lineage[^1] = currentLife with
        {
            DeathTick = deathTick,
            EndKind = deathKind,
            JourneyId = journeyId,
        };

        var stageAtTick = migrationTick is { } migrated
            ? ReincarnationRules.GetStageAt(deathTick, migrated, sourceRealm)
            : ReincarnationStage.Entering;
        var readyTick = ReincarnationRules.GetRebirthReadyTick(deathTick, sourceRealm);
        var nextTick = ReincarnationRules.GetStageBoundaryTick(deathTick, sourceRealm, stageAtTick);
        if (migrationTick is { } migration && nextTick <= migration)
        {
            nextTick = checked(migration + 1);
        }
        var journey = new ReincarnationJourneyState(
            agentIndex,
            journeyId,
            currentLife.Id,
            currentLife.LifeNumber,
            currentLife.SoulId,
            currentLife.BodyId,
            currentLife.PersonId,
            sourceRealm,
            stageAtTick,
            deathTick,
            ReincarnationRules.GetStageStartedTick(deathTick, sourceRealm, stageAtTick),
            nextTick,
            readyTick,
            ReincarnationRules.GetTotalDurationYears(sourceRealm),
            CompletedTick: null,
            revision,
            RequiresReview: true);
        _journeys[agentIndex].Add(journey);
        foreach (var domain in ReincarnationRules.AllTraceDomains)
        {
            var candidate = hasEvidence
                ? GetCandidate(candidates, domain)
                : new ReincarnationTraceCandidate(domain, 0, 0);
            var preserved = candidate.PreservationHundredths;
            var pendingAccessible = checked((ushort)Math.Min(
                preserved,
                candidate.AccessibleHundredths));
            _traces[agentIndex].Add(new ReincarnationTraceState(
                agentIndex,
                journeyId,
                currentLife.LifeNumber,
                domain,
                preserved,
                pendingAccessible,
                AccessibleHundredths: 0,
                IntegratedHundredths: 0,
                hasEvidence
                    ? ReincarnationRules.GetPreservationLevel(preserved)
                    : TracePreservationLevel.Lost,
                TraceAccessLevel.Sealed,
                hasEvidence,
                revision));
        }
        return journey;
    }

    private static bool IsValidCandidates(ReadOnlySpan<ReincarnationTraceCandidate> candidates)
    {
        if (candidates.Length != ReincarnationRules.TraceDomainCount)
        {
            return false;
        }
        Span<bool> found = stackalloc bool[ReincarnationRules.TraceDomainCount];
        foreach (var candidate in candidates)
        {
            if (!Enum.IsDefined(candidate.Domain)
                || candidate.PreservationHundredths > QualityValue.MaximumHundredths
                || candidate.AccessibleHundredths > QualityValue.MaximumHundredths
                || found[(int)candidate.Domain])
            {
                return false;
            }
            found[(int)candidate.Domain] = true;
        }
        return true;
    }

    private static ReincarnationTraceCandidate GetCandidate(
        ReadOnlySpan<ReincarnationTraceCandidate> candidates,
        ReincarnationTraceDomain domain)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.Domain == domain)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("윤회 흔적 입력 영역이 누락되었습니다.");
    }

    private int GetActiveJourneyIndex(int agentIndex)
    {
        for (var index = _journeys[agentIndex].Count - 1; index >= 0; index--)
        {
            if (_journeys[agentIndex][index].CompletedTick is null)
            {
                return index;
            }
        }
        throw new InvalidOperationException("진행 중인 윤회 여정이 없습니다.");
    }

    private void ValidateAgent(int agentIndex)
    {
        if ((uint)agentIndex >= (uint)Population)
        {
            throw new ArgumentOutOfRangeException(nameof(agentIndex));
        }
    }

    private static bool IsHigherPeak(HistoricalPeakState candidate, HistoricalPeakState current)
    {
        var realm = candidate.Realm.CompareTo(current.Realm);
        if (realm != 0)
        {
            return realm > 0;
        }
        var floor = candidate.RealmLayer.CompareTo(current.RealmLayer);
        if (floor != 0)
        {
            return floor > 0;
        }
        return candidate.CombatPower > current.CombatPower;
    }

    private static HistoricalPeakSnapshot ToSnapshot(HistoricalPeakState peak) => new(
        peak.Realm,
        peak.RealmLayer,
        peak.CombatPower,
        new SimulationTick(peak.AchievedTick),
        peak.LifeNumber,
        peak.PersonId);

    private static LifeLineageEntry Clone(LifeLineageEntry entry) => entry;
    private static ReincarnationJourneyState Clone(ReincarnationJourneyState state) => state with { };

    private static bool IsValidAgentState(
        int agent,
        int firstTracked,
        bool complete,
        long revision,
        HistoricalPeakState historicalPeak,
        LifeLineageEntry[] lineage,
        ReincarnationJourneyState[] journeys,
        ReincarnationTraceState[] traces,
        long? maximumTick)
    {
        if (lineage.Length == 0
            || (complete && firstTracked != 1)
            || lineage[0].LifeNumber != firstTracked
            || lineage[0].PredecessorId is not null)
        {
            return false;
        }

        var lineageById = new Dictionary<LifeRecordId, LifeLineageEntry>(lineage.Length);
        var lineageByLifeNumber = new Dictionary<int, LifeLineageEntry>(lineage.Length);
        var expectedJourneyIds = new HashSet<ReincarnationJourneyId>();
        for (var index = 0; index < lineage.Length; index++)
        {
            var entry = lineage[index];
            var expectedLife = checked(firstTracked + index);
            if (entry.AgentIndex != agent
                || entry.LifeNumber != expectedLife
                || entry.Id != ReincarnationRules.CreateLifeRecordId(agent, expectedLife)
                || entry.SoulId != PersonCausalRules.CreateSoulId(agent)
                || entry.BodyId != PersonCausalRules.CreateBodyId(agent, expectedLife)
                || entry.PersonId != PersonCausalRules.CreatePersonId(agent, expectedLife)
                || string.IsNullOrWhiteSpace(entry.PersonName)
                || entry.BirthTick < 0
                || !IsValidPeak(entry.PersonalPeak, entry.LifeNumber, entry.PersonId)
                || entry.PersonalPeak.AchievedTick < entry.BirthTick
                || (entry.DeathTick is { } peakBoundary
                    && entry.PersonalPeak.AchievedTick > peakBoundary)
                || (maximumTick is { } worldTick
                    && (entry.BirthTick > worldTick
                        || entry.PersonalPeak.AchievedTick > worldTick
                        || entry.DeathTick is { } deathAfterWorld && deathAfterWorld > worldTick))
                || (index > 0 && entry.PredecessorId != lineage[index - 1].Id)
                || (entry.DeathTick is null) != (entry.EndKind == LifeEndKind.None)
                || (entry.DeathTick is null) != (entry.JourneyId is null)
                || !Enum.IsDefined(entry.EndKind)
                || (entry.DeathTick is { } death && death < entry.BirthTick)
                || !lineageById.TryAdd(entry.Id, entry)
                || !lineageByLifeNumber.TryAdd(entry.LifeNumber, entry))
            {
                return false;
            }

            if (entry.JourneyId is { } journeyId && !expectedJourneyIds.Add(journeyId))
            {
                return false;
            }
        }

        if (!IsValidPeak(historicalPeak, historicalPeak.LifeNumber, historicalPeak.PersonId)
            || (maximumTick is { } historicalWorldTick
                && historicalPeak.AchievedTick > historicalWorldTick))
        {
            return false;
        }
        var expectedHistorical = lineage[0].PersonalPeak;
        foreach (var entry in lineage.Skip(1))
        {
            if (IsHigherPeak(entry.PersonalPeak, expectedHistorical))
            {
                expectedHistorical = entry.PersonalPeak;
            }
        }
        if (historicalPeak != expectedHistorical)
        {
            return false;
        }

        var activeCount = 0;
        var journeyById = new Dictionary<ReincarnationJourneyId, ReincarnationJourneyState>(
            journeys.Length);
        foreach (var journey in journeys)
        {
            if (!lineageById.TryGetValue(journey.SourceLifeRecordId, out var source)
                || journey.AgentIndex != agent
                || journey.Id != ReincarnationRules.CreateJourneyId(agent, source.LifeNumber)
                || journey.SourceLifeNumber != source.LifeNumber
                || journey.SoulId != source.SoulId
                || journey.SourceBodyId != source.BodyId
                || journey.SourcePersonId != source.PersonId
                || journey.SourceRealm != source.PersonalPeak.Realm
                || source.JourneyId != journey.Id
                || !Enum.IsDefined(journey.SourceRealm)
                || journey.Stage == ReincarnationStage.None
                || !Enum.IsDefined(journey.Stage)
                || journey.StartedTick != source.DeathTick
                || journey.StageStartedTick < journey.StartedTick
                || journey.RebirthReadyTick != ReincarnationRules.GetRebirthReadyTick(
                    journey.StartedTick, journey.SourceRealm)
                || journey.RequiredYears != ReincarnationRules.GetTotalDurationYears(journey.SourceRealm)
                || journey.StageStartedTick != ReincarnationRules.GetStageStartedTick(
                    journey.StartedTick, journey.SourceRealm, journey.Stage)
                || journey.NextTransitionTick != ReincarnationRules.GetStageBoundaryTick(
                    journey.StartedTick, journey.SourceRealm, journey.Stage)
                || journey.AuthorityRevision < 1
                || journey.AuthorityRevision > revision
                || !journey.RequiresReview
                || !journeyById.TryAdd(journey.Id, journey)
                || (maximumTick is { } journeyWorldTick
                    && (journey.StartedTick > journeyWorldTick
                        || journey.StageStartedTick > journeyWorldTick
                        || journey.CompletedTick is { } completedAfterWorld
                            && completedAfterWorld > journeyWorldTick)))
            {
                return false;
            }
            if (journey.CompletedTick is null)
            {
                activeCount++;
                if (maximumTick is { } activeWorldTick
                    && (journey.NextTransitionTick <= activeWorldTick
                        || journey.Stage != ReincarnationRules.GetStageAt(
                            journey.StartedTick,
                            activeWorldTick,
                            journey.SourceRealm)))
                {
                    return false;
                }
            }
            else if (journey.CompletedTick != journey.RebirthReadyTick
                || journey.Stage != ReincarnationStage.PreparingRebirth)
            {
                return false;
            }
            else
            {
                if (!lineageByLifeNumber.TryGetValue(
                        journey.SourceLifeNumber + 1,
                        out var nextLife)
                    || nextLife.PredecessorId != source.Id
                    || nextLife.BirthTick != journey.CompletedTick)
                {
                    return false;
                }
            }
        }
        if (journeyById.Count != expectedJourneyIds.Count
            || expectedJourneyIds.Any(id => !journeyById.ContainsKey(id))
            || activeCount > 1
            || (activeCount == 1) != (lineage[^1].DeathTick is not null)
            || (activeCount == 0) != (lineage[^1].DeathTick is null))
        {
            return false;
        }

        var traceCounts = new Dictionary<ReincarnationJourneyId, int>(journeys.Length);
        var traceDomainMasks = new Dictionary<ReincarnationJourneyId, int>(journeys.Length);
        foreach (var trace in traces)
        {
            if (!journeyById.TryGetValue(trace.JourneyId, out var journey))
            {
                return false;
            }

            var domainBit = 1 << (int)trace.Domain;
            traceDomainMasks.TryGetValue(trace.JourneyId, out var domainMask);
            if (trace.AgentIndex != agent
                || trace.SourceLifeNumber != journey.SourceLifeNumber
                || !Enum.IsDefined(trace.Domain)
                || (domainMask & domainBit) != 0
                || trace.PreservedHundredths > QualityValue.MaximumHundredths
                || trace.PendingAccessibleHundredths > trace.PreservedHundredths
                || trace.AccessibleHundredths > trace.PreservedHundredths
                || trace.IntegratedHundredths > trace.AccessibleHundredths
                || !Enum.IsDefined(trace.PreservationLevel)
                || !Enum.IsDefined(trace.AccessLevel)
                || trace.AuthorityRevision < 1
                || trace.AuthorityRevision > revision
                || (trace.HasEvidence
                    && trace.PreservationLevel
                        != ReincarnationRules.GetPreservationLevel(trace.PreservedHundredths))
                || trace.AccessLevel != ReincarnationRules.GetAccessLevel(
                    trace.AccessibleHundredths)
                || (journey.CompletedTick is null && trace.AccessibleHundredths != 0)
                || (journey.CompletedTick is not null
                    && trace.AccessibleHundredths != trace.PendingAccessibleHundredths)
                || (!trace.HasEvidence
                    && (trace.PreservedHundredths != 0
                        || trace.PendingAccessibleHundredths != 0
                        || trace.AccessibleHundredths != 0
                        || trace.IntegratedHundredths != 0)))
            {
                return false;
            }

            traceDomainMasks[trace.JourneyId] = domainMask | domainBit;
            traceCounts.TryGetValue(trace.JourneyId, out var traceCount);
            traceCounts[trace.JourneyId] = checked(traceCount + 1);
        }

        var completeDomainMask = (1 << ReincarnationRules.TraceDomainCount) - 1;
        foreach (var journey in journeys)
        {
            if (!traceCounts.TryGetValue(journey.Id, out var count)
                || count != ReincarnationRules.TraceDomainCount
                || traceDomainMasks[journey.Id] != completeDomainMask)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsValidPeak(
        HistoricalPeakState peak,
        int lifeNumber,
        PersonIdentityId personId) =>
        Enum.IsDefined(peak.Realm)
        && RealmRules.IsValidMinorStage(peak.Realm, peak.RealmLayer)
        && peak.CombatPower >= ReincarnationRules.MinimumHistoricalCombatPower
        && peak.AchievedTick >= 0
        && peak.LifeNumber == lifeNumber
        && peak.PersonId == personId;

    private static void AddPeakHash(ref ulong hash, HistoricalPeakState peak)
    {
        Add(ref hash, (byte)peak.Realm);
        Add(ref hash, peak.RealmLayer);
        Add(ref hash, unchecked((ulong)peak.CombatPower));
        Add(ref hash, unchecked((ulong)peak.AchievedTick));
        Add(ref hash, unchecked((ulong)peak.LifeNumber));
        Add(ref hash, peak.PersonId.Value);
    }

    private static void Add(ref ulong hash, ulong value)
    {
        hash ^= value;
        hash *= 1099511628211UL;
    }

    private static void AddString(ref ulong hash, string value)
    {
        foreach (var character in value)
        {
            Add(ref hash, character);
        }
    }
}
