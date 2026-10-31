using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class ReincarnationStore
{
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
                Add(ref hash, entry.ParentOnePersonId?.Value ?? 0);
                Add(ref hash, entry.ParentTwoPersonId?.Value ?? 0);
                Add(ref hash, (byte)entry.BirthStatus);
                Add(ref hash, entry.FamilyId.Value);
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
                Add(ref hash, (byte)journey.SoulDamage);
                Add(ref hash, (byte)journey.BirthCandidateStatus);
                Add(ref hash, unchecked((ulong)journey.BirthCandidateWaitYears));
                Add(ref hash, journey.ParentOnePersonId?.Value ?? 0);
                Add(ref hash, journey.ParentTwoPersonId?.Value ?? 0);
                Add(ref hash, journey.RootCausalRecordId.Value);
                Add(ref hash, journey.EntryCausalRecordId.Value);
                Add(ref hash, journey.CrossingCausalRecordId.Value);
                Add(ref hash, journey.RebirthCausalRecordId.Value);
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
                Add(ref hash, (byte)trace.EvidenceStatus);
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
        long? migrationTick,
        SoulDamageLevel soulDamage,
        SimulationEventRecordId causalRecordId)
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
            RequiresReview: true,
            SoulDamage: soulDamage,
            BirthCandidateStatus: BirthCandidateStatus.Searching,
            RootCausalRecordId: causalRecordId,
            EntryCausalRecordId: causalRecordId);
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
                revision,
                hasEvidence
                    ? PreservationEvidenceStatus.Available
                    : PreservationEvidenceStatus.UnavailableMigration));
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
                || !Enum.IsDefined(entry.BirthStatus)
                || (entry.ParentTwoPersonId is not null && entry.ParentOnePersonId is null)
                || entry.FamilyId != ReincarnationRules.CreateFamilyId(
                    entry.ParentOnePersonId,
                    entry.ParentTwoPersonId)
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
                || journey.BirthCandidateWaitYears < 0
                || journey.RebirthReadyTick != checked(
                    ReincarnationRules.GetRebirthReadyTick(
                        journey.StartedTick,
                        journey.SourceRealm)
                    + ((long)journey.BirthCandidateWaitYears * GameCalendar.TicksPerYear))
                || journey.RequiredYears != ReincarnationRules.GetTotalDurationYears(journey.SourceRealm)
                || journey.StageStartedTick != ReincarnationRules.GetStageStartedTick(
                    journey.StartedTick, journey.SourceRealm, journey.Stage)
                || journey.NextTransitionTick != (journey.Stage == ReincarnationStage.PreparingRebirth
                    ? journey.RebirthReadyTick
                    : ReincarnationRules.GetStageBoundaryTick(
                        journey.StartedTick,
                        journey.SourceRealm,
                        journey.Stage))
                || journey.AuthorityRevision < 1
                || journey.AuthorityRevision > revision
                || !journey.RequiresReview
                || !Enum.IsDefined(journey.SoulDamage)
                || !Enum.IsDefined(journey.BirthCandidateStatus)
                || (journey.ParentTwoPersonId is not null && journey.ParentOnePersonId is null)
                || (journey.BirthCandidateStatus == BirthCandidateStatus.Located
                    && journey.ParentOnePersonId is null)
                || (journey.BirthCandidateStatus != BirthCandidateStatus.Located
                    && journey.BirthCandidateStatus != BirthCandidateStatus.Completed
                    && (journey.ParentOnePersonId is not null
                        || journey.ParentTwoPersonId is not null))
                || ((journey.RootCausalRecordId.Value == 0)
                    != (journey.EntryCausalRecordId.Value == 0))
                || (journey.RootCausalRecordId.Value != 0
                    && journey.RootCausalRecordId != journey.EntryCausalRecordId)
                || (journey.CrossingCausalRecordId.Value != 0
                    && journey.Stage < ReincarnationStage.CrossingComplete)
                || (journey.RebirthCausalRecordId.Value != 0
                    && journey.CompletedTick is null)
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
                || journey.Stage != ReincarnationStage.PreparingRebirth
                || journey.BirthCandidateStatus != BirthCandidateStatus.Completed)
            {
                return false;
            }
            else
            {
                if (!lineageByLifeNumber.TryGetValue(
                        journey.SourceLifeNumber + 1,
                        out var nextLife)
                    || nextLife.PredecessorId != source.Id
                    || nextLife.BirthTick != journey.CompletedTick
                    || nextLife.ParentOnePersonId != journey.ParentOnePersonId
                    || nextLife.ParentTwoPersonId != journey.ParentTwoPersonId)
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
                || !Enum.IsDefined(trace.EvidenceStatus)
                || (trace.HasEvidence
                    && trace.EvidenceStatus is not (PreservationEvidenceStatus.Available
                        or PreservationEvidenceStatus.Unknown))
                || (!trace.HasEvidence
                    && trace.EvidenceStatus is not (PreservationEvidenceStatus.UnavailableMigration
                        or PreservationEvidenceStatus.Unknown))
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
