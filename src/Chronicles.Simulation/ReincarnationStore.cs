using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class ReincarnationStore
{
    private readonly List<LifeLineageEntry>[] _lineage;
    private readonly List<ReincarnationJourneyState>?[] _journeys;
    private readonly List<ReincarnationTraceState>?[] _traces;
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
        _journeys = new List<ReincarnationJourneyState>?[population];
        _traces = new List<ReincarnationTraceState>?[population];
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
            migrationTick: currentTick,
            soulDamage: SoulDamageLevel.None,
            causalRecordId: default);
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
        ReadOnlySpan<ReincarnationTraceCandidate> candidates,
        SoulDamageLevel soulDamage = SoulDamageLevel.None,
        SimulationEventRecordId causalRecordId = default) => BeginJourneyCore(
            agentIndex,
            deathTick,
            deathKind,
            sourceRealm,
            candidates,
            hasEvidence: true,
            migrationTick: null,
            soulDamage: soulDamage,
            causalRecordId: causalRecordId);

    public BirthCandidateStatus PrepareBirthCandidate(
        int agentIndex,
        long tick,
        PersonIdentityId? parentOnePersonId,
        PersonIdentityId? parentTwoPersonId,
        bool allowIndependentManifestation)
    {
        ValidateAgent(agentIndex);
        var journeyIndex = GetActiveJourneyIndex(agentIndex);
        var journeys = GetJourneys(agentIndex);
        var journey = journeys[journeyIndex];
        if (journey.Stage != ReincarnationStage.PreparingRebirth
            || tick != journey.RebirthReadyTick)
        {
            throw new InvalidOperationException("환생 준비 경계에서만 출생 후보를 확정할 수 있습니다.");
        }

        var hasParent = parentOnePersonId is { Value: not 0 };
        var status = hasParent
            ? BirthCandidateStatus.Located
            : allowIndependentManifestation
                ? BirthCandidateStatus.IndependentManifestation
                : BirthCandidateStatus.UnavailableLongWait;
        var revision = checked(_authorityRevisions[agentIndex] + 1);
        _authorityRevisions[agentIndex] = revision;
        journeys[journeyIndex] = journey with
        {
            BirthCandidateStatus = status,
            ParentOnePersonId = hasParent ? parentOnePersonId : null,
            ParentTwoPersonId = hasParent ? parentTwoPersonId : null,
            AuthorityRevision = revision,
        };
        return status;
    }

    public long DeferForBirthCandidate(int agentIndex, long tick)
    {
        ValidateAgent(agentIndex);
        var journeyIndex = GetActiveJourneyIndex(agentIndex);
        var journeys = GetJourneys(agentIndex);
        var journey = journeys[journeyIndex];
        if (journey.Stage != ReincarnationStage.PreparingRebirth
            || tick != journey.RebirthReadyTick
            || journey.BirthCandidateStatus != BirthCandidateStatus.UnavailableLongWait)
        {
            throw new InvalidOperationException("출생 후보 부재 상태에서만 환생을 연기할 수 있습니다.");
        }

        var nextTick = checked(tick + GameCalendar.TicksPerYear);
        var revision = checked(_authorityRevisions[agentIndex] + 1);
        _authorityRevisions[agentIndex] = revision;
        journeys[journeyIndex] = journey with
        {
            NextTransitionTick = nextTick,
            RebirthReadyTick = nextTick,
            BirthCandidateWaitYears = checked(journey.BirthCandidateWaitYears + 1),
            BirthCandidateStatus = BirthCandidateStatus.Searching,
            AuthorityRevision = revision,
        };
        return nextTick;
    }

    public ReincarnationStageAdvanceResult AdvanceStage(
        int agentIndex,
        long tick,
        SimulationEventRecordId causalRecordId = default)
    {
        ValidateAgent(agentIndex);
        var journeyIndex = GetActiveJourneyIndex(agentIndex);
        var journeys = GetJourneys(agentIndex);
        var current = journeys[journeyIndex];
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
            CrossingCausalRecordId = nextStage == ReincarnationStage.CrossingComplete
                ? causalRecordId
                : current.CrossingCausalRecordId,
            AuthorityRevision = revision,
        };
        journeys[journeyIndex] = updated;
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
        long initialCombatPower,
        SimulationEventRecordId rebirthCausalRecordId = default)
    {
        EnsureReadyForRebirth(agentIndex, tick, soulId);
        var journeyIndex = GetActiveJourneyIndex(agentIndex);
        var journeys = GetJourneys(agentIndex);
        var traces = GetTraces(agentIndex);
        var journey = journeys[journeyIndex];

        var previous = _lineage[agentIndex][^1];
        var lifeNumber = checked(previous.LifeNumber + 1);
        var revision = checked(_authorityRevisions[agentIndex] + 1);
        _authorityRevisions[agentIndex] = revision;
        journeys[journeyIndex] = journey with
        {
            CompletedTick = tick,
            BirthCandidateStatus = BirthCandidateStatus.Completed,
            RebirthCausalRecordId = rebirthCausalRecordId,
            AuthorityRevision = revision,
        };
        for (var traceIndex = 0; traceIndex < traces.Count; traceIndex++)
        {
            var trace = traces[traceIndex];
            if (trace.JourneyId != journey.Id)
            {
                continue;
            }

            var accessible = trace.HasEvidence
                ? Math.Min(trace.PreservedHundredths, trace.PendingAccessibleHundredths)
                : 0;
            traces[traceIndex] = trace with
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
            JourneyId: null,
            ParentOnePersonId: journey.ParentOnePersonId,
            ParentTwoPersonId: journey.ParentTwoPersonId,
            BirthStatus: journey.BirthCandidateStatus,
            FamilyId: ReincarnationRules.CreateFamilyId(
                journey.ParentOnePersonId,
                journey.ParentTwoPersonId)));
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
        var journey = GetJourneys(agentIndex)[GetActiveJourneyIndex(agentIndex)];
        var traceCount = GetTraces(agentIndex).Count(trace => trace.JourneyId == journey.Id);
        if (journey.Stage != ReincarnationStage.PreparingRebirth
            || tick != journey.RebirthReadyTick
            || journey.SoulId != soulId
            || journey.BirthCandidateStatus is not (BirthCandidateStatus.Located
                or BirthCandidateStatus.IndependentManifestation)
            || traceCount != ReincarnationRules.TraceDomainCount)
        {
            throw new InvalidOperationException("새 생을 시작할 준비가 완료되지 않았습니다.");
        }
    }

    public ReincarnationJourneyState? GetCurrentJourney(int agentIndex)
    {
        ValidateAgent(agentIndex);
        return _journeys[agentIndex]?.LastOrDefault(journey => journey.CompletedTick is null);
    }

    public FamilyLineageId GetCurrentFamilyId(int agentIndex)
    {
        ValidateAgent(agentIndex);
        return _lineage[agentIndex].Count == 0
            ? default
            : _lineage[agentIndex][^1].FamilyId;
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
                    entry.JourneyId,
                    entry.ParentOnePersonId,
                    entry.ParentTwoPersonId,
                    entry.BirthStatus,
                    entry.FamilyId))
                .ToArray());
            var traceSnapshots = _traces[agentIndex] is { } traceStates
                ? traceStates.Select(trace => new ReincarnationTraceSnapshot(
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
                    trace.AuthorityRevision,
                    trace.EvidenceStatus)).ToArray()
                : [];
            _traceSnapshotCache[agentIndex] = Array.AsReadOnly(traceSnapshots);
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
                current.RequiresReview,
                current.SoulDamage,
                current.BirthCandidateStatus,
                current.BirthCandidateWaitYears,
                current.ParentOnePersonId,
                current.ParentTwoPersonId,
                current.RootCausalRecordId,
                current.EntryCausalRecordId,
                current.CrossingCausalRecordId,
                current.RebirthCausalRecordId);
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
}
