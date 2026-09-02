using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private static bool IsValidPersonCausalCheckpoint(
        int agentIndex,
        AgentCheckpoint agent,
        long maximumTick)
    {
        if (agent.Identity is not { } identity
            || agent.Decision is not { } decision
            || identity.SoulId != PersonCausalRules.CreateSoulId(agentIndex)
            || identity.BodyId != PersonCausalRules.CreateBodyId(agentIndex, agent.LifeNumber)
            || identity.PersonId != PersonCausalRules.CreatePersonId(agentIndex, agent.LifeNumber)
            || !Enum.IsDefined(identity.LifeState)
            || (identity.LifeState == PersonLifeState.ReincarnationRiver)
                != (agent.Activity == AgentActivity.Reincarnating)
            || decision.AuthorityRevision < 1
            || decision.Latest.AuthorityRevision != decision.AuthorityRevision
            || decision.Latest.Tick < 0
            || decision.Latest.Tick > maximumTick
            || decision.Latest.PrimaryGoal != AgentGoalKind.ReachRankOne
            || !Enum.IsDefined(decision.Latest.SupportingGoal)
            || !Enum.IsDefined(decision.Latest.SelectedActivity)
            || !Enum.IsDefined(decision.Latest.SelectedReason)
            || !Enum.IsDefined(decision.Latest.Resolution)
            || (decision.Latest.AlternativeActivity is { } alternative
                && !Enum.IsDefined(alternative)))
        {
            return false;
        }

        return true;
    }

    private static bool IsValidLifeAuthorityLink(
        AgentCheckpoint agent,
        PersonLifeAuthorityCheckpoint life)
    {
        if (agent.Identity is not { } identity
            || life.LatestTransition.PersonId != identity.PersonId
            || life.LatestTransition.BodyId != identity.BodyId
            || life.LatestTransition.CurrentPhase != life.LifePhase
            || life.LatestTransition.CurrentBodyState != life.BodyState
            || life.LatestTransition.CurrentSoulState != life.SoulState)
        {
            return false;
        }

        return identity.LifeState switch
        {
            PersonLifeState.Living => life.LifePhase == CurrentLifePhase.Living
                && life.BodyState == BodyLifeState.Living
                && life.SoulState == SoulEmbodimentState.Embodied,
            PersonLifeState.ReincarnationRiver =>
                life.LifePhase == CurrentLifePhase.ReincarnationHandoff
                && life.BodyState is BodyLifeState.Dead or BodyLifeState.Destroyed
                && life.SoulState == SoulEmbodimentState.Crossing,
            _ => false,
        };
    }

    private static bool IsValidCausalRuntimeLink(WorldCheckpoint checkpoint)
    {
        var causal = checkpoint.Causal!;
        for (var agentIndex = 0; agentIndex < checkpoint.Agents.Length; agentIndex++)
        {
            var agent = checkpoint.Agents[agentIndex];
            if (agent.Identity is not { } identity || agent.Decision is not { } decision)
            {
                return false;
            }

            var path = causal.CurrentPaths[agentIndex];
            if (path.AuthorityRevision != decision.Latest.AuthorityRevision
                || path.Goal != decision.Latest.SupportingGoal
                || path.Action != decision.Latest.SelectedActivity
                || path.AlternativeAction != decision.Latest.AlternativeActivity
                || path.Reason != decision.Latest.SelectedReason
                || (path.Resolution != AgentCausalRules.MapResolution(decision.Latest.Resolution)
                    && !path.IsWaiting
                    && !path.WasInterrupted)
                || path.StartedTick != decision.Latest.Tick
                || path.StartedTick > checkpoint.Tick
                || path.GoalId != AgentCausalRules.CreateGoalId(identity.PersonId, path.Goal)
                || path.PlanId != AgentCausalRules.CreatePlanId(identity.PersonId, path.AuthorityRevision)
                || path.TaskId != AgentCausalRules.CreateTaskId(identity.PersonId, path.AuthorityRevision)
                || path.ActionId != AgentCausalRules.CreateActionId(identity.PersonId, path.AuthorityRevision)
                || path.ThreadId != AgentCausalRules.CreateThreadId(identity.PersonId)
                || path.EventDefinitionId != AgentCausalRules.CreateEventDefinitionId(path.Action)
                || path.EventInstanceId != AgentCausalRules.CreateEventInstanceId(
                    identity.PersonId,
                    path.AuthorityRevision)
                || path.ResultId != AgentCausalRules.CreateResultId(identity.PersonId, path.AuthorityRevision)
                || path.EventRecordId != AgentCausalRules.CreateEventRecordId(
                    identity.PersonId,
                    path.AuthorityRevision)
                || (path.Resolution == CausalResolutionCode.Committed
                    ? path.FailureScope != CausalFailureScope.None
                    : path.FailureScope == CausalFailureScope.None))
            {
                return false;
            }

            var factStart = agentIndex * AgentCausalRules.PerceivedFactCapacity;
            if (causal.PerceivedFactCounts[agentIndex] != AgentCausalRules.PerceivedFactCapacity)
            {
                return false;
            }

            for (var offset = 0; offset < AgentCausalRules.PerceivedFactCapacity; offset++)
            {
                var fact = causal.PerceivedFacts[factStart + offset];
                var expectedKind = (PerceivedFactKind)offset;
                if (fact.Kind != expectedKind
                    || fact.Id != AgentCausalRules.CreateFactId(identity.PersonId, expectedKind)
                    || fact.ObserverPersonId != identity.PersonId
                    || fact.SubjectPersonId != identity.PersonId
                    || fact.ObservedTick < 0
                    || fact.ObservedTick > checkpoint.Tick
                    || fact.SourceRecordId != path.EventRecordId)
                {
                    return false;
                }
            }

            var memoryStart = agentIndex * AgentCausalRules.MemoryCapacityPerAgent;
            var populated = 0;
            for (var offset = 0; offset < AgentCausalRules.MemoryCapacityPerAgent; offset++)
            {
                var memory = causal.Memories[memoryStart + offset];
                if (memory.Id.Value == 0)
                {
                    continue;
                }

                populated++;
                if (memory.LifeNumber != agent.LifeNumber
                    || memory.FirstTick < agent.BirthTick
                    || memory.LastReinforcedTick < memory.FirstTick
                    || memory.LastReinforcedTick > checkpoint.Tick
                    || memory.StrengthHundredths is 0 or > 10_000
                    || memory.ReinforcementCount == 0
                    || memory.SourceResultId.Value == 0
                    || memory.SourceRecordId.Value == 0)
                {
                    return false;
                }
            }

            if (populated != causal.MemoryCounts[agentIndex])
            {
                return false;
            }

            var claim = causal.LatestKnowledgeClaims[agentIndex];
            if (claim.Id.Value != 0
                && (claim.LifeNumber != agent.LifeNumber
                    || claim.RecipientPersonId != identity.PersonId
                    || claim.TeachingProvenanceId.Value == 0
                    || claim.SourceResultId.Value == 0
                    || claim.CreatedTick < agent.BirthTick
                    || claim.CreatedTick > checkpoint.Tick
                    || claim.AuthorityRevision <= 0))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidTeachingCheckpoint(
        int owner,
        AgentCheckpoint agent,
        WorldCheckpoint checkpoint,
        IReadOnlyDictionary<ulong, RelationStateCheckpoint> relationById)
    {
        if (agent.Teaching is not { } teaching
            || teaching.AuthorityRevision < 0
            || teaching.LearningAuthorityRevision < 0
            || teaching.AuthorityRevision == long.MaxValue
            || teaching.LearningAuthorityRevision == long.MaxValue
            || teaching.Latest.AuthorityRevision != teaching.AuthorityRevision
            || teaching.LatestLearning.AuthorityRevision != teaching.LearningAuthorityRevision
            || !IsValidTeachingRevisionParity(
                teaching.AuthorityRevision,
                teaching.Latest.IsActive)
            || !IsValidTeachingRevisionParity(
                teaching.LearningAuthorityRevision,
                teaching.LatestLearning.IsActive)
            || (agent.Activity == AgentActivity.Teaching) != teaching.Latest.IsActive
            || teaching.TechniqueMisunderstandingPoints > QualityValue.MaximumHundredths
            || teaching.SpellMisunderstandingPoints > QualityValue.MaximumHundredths
            || !TeachingRules.IsValidTrace(
                teaching.Latest,
                checkpoint.Agents.Length,
                checkpoint.Tick)
            || !TeachingRules.IsValidTrace(
                teaching.LatestLearning,
                checkpoint.Agents.Length,
                checkpoint.Tick))
        {
            return false;
        }

        return IsValidTeachingTraceOwner(
                owner,
                agent,
                teaching.Latest,
                checkpoint,
                relationById,
                ownerIsTeacher: true)
            && IsValidTeachingTraceOwner(
                owner,
                agent,
                teaching.LatestLearning,
                checkpoint,
                relationById,
                ownerIsTeacher: false);
    }

    private static bool IsValidTeachingRevisionParity(
        long revision,
        bool isActive) => revision == 0
        ? !isActive
        : isActive
            ? (revision & 1L) == 1L
            : (revision & 1L) == 0L;

    private static string DescribeTeachingCheckpoint(AgentCheckpoint agent)
    {
        if (agent.Teaching is not { } teaching)
        {
            return "Teaching 없음";
        }

        var outgoing = teaching.Latest;
        var incoming = teaching.LatestLearning;
        return $"activity={agent.Activity}, person={agent.Identity?.PersonId.Value ?? 0}, "
            + $"outRev={teaching.AuthorityRevision}/{outgoing.AuthorityRevision}, "
            + $"outActive={outgoing.IsActive}, outLearner={outgoing.LearnerAgent}, "
            + $"outTeacherPerson={outgoing.TeacherPersonId.Value}, "
            + $"outLearnerPerson={outgoing.LearnerPersonId.Value}, "
            + $"inRev={teaching.LearningAuthorityRevision}/{incoming.AuthorityRevision}, "
            + $"inActive={incoming.IsActive}, inLearner={incoming.LearnerAgent}, "
            + $"inTeacherPerson={incoming.TeacherPersonId.Value}, "
            + $"inLearnerPerson={incoming.LearnerPersonId.Value}";
    }

    private static bool IsValidTeachingTraceOwner(
        int owner,
        AgentCheckpoint ownerCheckpoint,
        TeachingTraceState trace,
        WorldCheckpoint checkpoint,
        IReadOnlyDictionary<ulong, RelationStateCheckpoint> relationById,
        bool ownerIsTeacher)
    {
        if (trace.AuthorityRevision == 0)
        {
            return true;
        }

        var teacher = ownerIsTeacher
            ? owner
            : RelationRules.GetOtherAgent(trace.RelationId, trace.LearnerAgent);
        relationById.TryGetValue(trace.RelationId.Value, out var relationState);
        if (teacher < 0
            || teacher >= checkpoint.Agents.Length
            || trace.LearnerAgent == teacher
            || trace.RelationId != RelationRules.CreateId(teacher, trace.LearnerAgent)
            || trace.TargetKnowledgeRef != TeachingRules.CreateKnowledgeRef(
                trace.TeacherPersonId,
                trace.TargetKind,
                trace.SourceQuality)
            || trace.ProvenanceId != TeachingRules.CreateProvenanceId(
                trace.RelationId,
                trace.TargetKnowledgeRef,
                trace.StartedTick)
            || !IsValidHistoricalPersonId(
                trace.TeacherPersonId,
                teacher,
                checkpoint.Agents[teacher].LifeNumber)
            || !IsValidHistoricalPersonId(
                trace.LearnerPersonId,
                trace.LearnerAgent,
                checkpoint.Agents[trace.LearnerAgent].LifeNumber)
            || relationState is null
            || (ownerIsTeacher
                ? trace.TeacherPersonId != ownerCheckpoint.Identity!.Value.PersonId
                : trace.LearnerAgent != owner
                    || trace.LearnerPersonId != ownerCheckpoint.Identity!.Value.PersonId))
        {
            return false;
        }

        if (!trace.IsActive)
        {
            return true;
        }

        if (!IsValidActiveTeachingRuntimeState(
                trace,
                teacher,
                relationState,
                checkpoint))
        {
            return false;
        }

        var counterpart = ownerIsTeacher
            ? checkpoint.Agents[trace.LearnerAgent].Teaching?.LatestLearning
            : checkpoint.Agents[teacher].Teaching?.Latest;
        return checkpoint.Agents[teacher].Activity == AgentActivity.Teaching
            && checkpoint.Agents[teacher].Identity is { } teacherIdentity
            && trace.TeacherPersonId == teacherIdentity.PersonId
            && checkpoint.Agents[trace.LearnerAgent].Identity is { } learnerIdentity
            && trace.LearnerPersonId == learnerIdentity.PersonId
            && counterpart is { } matching
            && AreMatchingTeachingContexts(trace, matching)
            && (relationState.Roles & RelationRole.MasterDisciple) != 0
            && relationState.MasterAgent == teacher;
    }

    private static bool IsValidActiveTeachingRuntimeState(
        TeachingTraceState trace,
        int teacher,
        RelationStateCheckpoint relation,
        WorldCheckpoint checkpoint)
    {
        if (trace.AuthorityRevision == long.MaxValue
            || trace.SessionsCompleted == int.MaxValue
            || trace.LastResult != TeachingResultKind.None
            || trace.LastResultTick != 0
            || trace.LastGain != 0
            || checkpoint.Agents[trace.LearnerAgent].Teaching is not { } learnerTeaching)
        {
            return false;
        }

        var expectedQuality = trace.TargetKind == TeachingTargetKind.Technique
            ? checkpoint.Agents[teacher].TechniqueQuality
            : checkpoint.Agents[teacher].SpellQuality;
        var expectedMisunderstanding = trace.TargetKind == TeachingTargetKind.Technique
            ? learnerTeaching.TechniqueMisunderstandingPoints
            : learnerTeaching.SpellMisunderstandingPoints;
        var storedKnowledgeRef = trace.TargetKind == TeachingTargetKind.Technique
            ? relation.TechniqueKnowledgeRef
            : relation.SpellKnowledgeRef;
        var expectedSessions = storedKnowledgeRef == trace.TargetKnowledgeRef
            ? trace.TargetKind == TeachingTargetKind.Technique
                ? relation.TechniqueTeachingSessions
                : relation.SpellTeachingSessions
            : 0;
        var expectedTransmission = storedKnowledgeRef == trace.TargetKnowledgeRef
            && (trace.TargetKind == TeachingTargetKind.Technique
                ? relation.TechniqueTransmissionEstablished
                : relation.SpellTransmissionEstablished);
        return trace.SourceQuality == expectedQuality
            && trace.MisunderstandingPoints == expectedMisunderstanding
            && trace.SessionsCompleted == expectedSessions
            && trace.TransmissionEstablished == expectedTransmission;
    }

    private static bool AreMatchingTeachingContexts(
        TeachingTraceState left,
        TeachingTraceState right) =>
        left.IsActive
        && right.IsActive
        && left.LearnerAgent == right.LearnerAgent
        && left.TeacherPersonId == right.TeacherPersonId
        && left.LearnerPersonId == right.LearnerPersonId
        && left.RelationId == right.RelationId
        && left.TargetKind == right.TargetKind
        && left.SourceQuality == right.SourceQuality
        && left.TargetKnowledgeRef == right.TargetKnowledgeRef
        && left.ProvenanceId == right.ProvenanceId
        && left.StartedTick == right.StartedTick
        && left.LastResultTick == right.LastResultTick
        && left.LastResult == right.LastResult
        && left.LastGain == right.LastGain
        && left.SessionsCompleted == right.SessionsCompleted
        && left.MisunderstandingPoints == right.MisunderstandingPoints
        && left.TransmissionEstablished == right.TransmissionEstablished;

    private static bool IsValidHistoricalPersonId(
        PersonIdentityId personId,
        int agent,
        int currentLifeNumber)
    {
        var encodedAgent = (uint)personId.Value;
        var lifeNumber = personId.Value >> 32;
        return encodedAgent == checked((uint)agent + 1U)
            && lifeNumber >= 1
            && lifeNumber <= (ulong)currentLifeNumber;
    }

    private static bool IsValidWorldEvent(
        WorldEvent entry,
        long maximumSequence,
        long maximumTick,
        int population) =>
        entry.Sequence is >= 1
        && entry.Sequence <= maximumSequence
        && entry.Tick.Value >= 0
        && entry.Tick.Value <= maximumTick
        && Enum.IsDefined(entry.Kind)
        && entry.PrimaryAgent.Value >= 0
        && entry.PrimaryAgent.Value < population
        && (entry.SecondaryAgent is not { } secondary
            || (secondary.Value >= 0 && secondary.Value < population));

    private static bool IsStrictlyIncreasingSequence(WorldEvent[] events)
    {
        for (var index = 1; index < events.Length; index++)
        {
            if (events[index - 1].Sequence >= events[index].Sequence)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidWorldCenturyChronicle(
        WorldCenturyChronicleCheckpoint? checkpoint,
        long maximumTick,
        int population)
    {
        if (checkpoint is not { } value
            || value.Sequence < 0
            || value.Entries is null
            || value.Current is null)
        {
            return false;
        }

        var currentYear = GameCalendar.GetYear(new SimulationTick(maximumTick));
        var expectedCenturyIndex = currentYear / WorldCenturyChronicle.YearsPerVolume;
        if (value.Sequence != value.Entries.Length
            || value.Entries.LongLength != expectedCenturyIndex)
        {
            return false;
        }

        for (var index = 0; index < value.Entries.Length; index++)
        {
            var entry = value.Entries[index];
            var expectedStartYear = checked((long)index * WorldCenturyChronicle.YearsPerVolume);
            if (entry is null
                || entry.Sequence != index + 1L
                || entry.CenturyNumber != index + 1L
                || entry.StartYear != expectedStartYear
                || entry.EndYear != expectedStartYear + WorldCenturyChronicle.YearsPerVolume - 1
                || !IsValidCenturyRealmCounts(
                    entry.RealmBreakthroughCounts,
                    entry.RealmBreakthroughUniqueCounts,
                    population)
                || !IsValidPositionChangeTotals(entry.CombatPowerPositionChanges)
                || !IsValidPositionChangeTotals(entry.RealmPositionChanges)
                || !IsValidPositionChangeTotals(entry.AptitudePositionChanges)
                || !IsValidPositionChangeTotals(entry.WealthPositionChanges)
                || !IsValidChronicleLeader(entry.CombatPowerLeader, population)
                || !IsValidChronicleLeader(entry.RealmLeader, population)
                || !IsValidChronicleLeader(entry.AptitudeLeader, population)
                || !IsValidChronicleLeader(entry.WealthLeader, population)
                || entry.DeathCount < 0
                || entry.UniqueDeathCount is < 0 || entry.UniqueDeathCount > entry.DeathCount
                || entry.ReincarnationEntryCount < 0
                || entry.UniqueReincarnationEntryCount is < 0
                    || entry.UniqueReincarnationEntryCount > entry.ReincarnationEntryCount
                || entry.RebirthCount < 0
                || entry.UniqueRebirthCount is < 0 || entry.UniqueRebirthCount > entry.RebirthCount
                || entry.UniqueDeathCount > population
                || entry.UniqueReincarnationEntryCount > population
                || entry.UniqueRebirthCount > population
                || entry.ReincarnationRiverResidentsAtEnd is < 0
                    || entry.ReincarnationRiverResidentsAtEnd > population
                || entry.CombatsCompleted < 0
                || entry.MonsterHunts < 0
                || entry.MissionsCompleted < 0
                || entry.SpiritStonesMined < 0
                || entry.TreasuresCrafted < 0
                || entry.PillsCrafted < 0
                || entry.SecretRealmsDiscovered < 0
                || entry.UniqueSecretRealmDiscoverers is < 0
                    || entry.UniqueSecretRealmDiscoverers > entry.SecretRealmsDiscovered
                || entry.UniqueSecretRealmDiscoverers > population)
            {
                return false;
            }
        }

        var current = value.Current;
        var wordCount = checked((population + 63) / 64);
        if (current.CenturyIndex != expectedCenturyIndex
            || !IsValidCenturyRealmCounts(
                current.RealmBreakthroughCounts,
                current.RealmBreakthroughUniqueCounts,
                population)
            || current.RealmBreakthroughAgentBits is not { Length: var realmBitCount }
            || realmBitCount != RealmRules.All.Count
            || current.RealmBreakthroughAgentBits.Any(bits =>
                !IsValidAgentBits(bits, wordCount, population))
            || !IsValidPositionChangeTotals(current.CombatPowerPositionChanges)
            || !IsValidPositionChangeTotals(current.RealmPositionChanges)
            || !IsValidPositionChangeTotals(current.AptitudePositionChanges)
            || !IsValidPositionChangeTotals(current.WealthPositionChanges)
            || !IsValidRankingCache(current.PreviousCombatPowerTop, population)
            || !IsValidRankingCache(current.PreviousRealmTop, population)
            || !IsValidRankingCache(current.PreviousAptitudeTop, population)
            || !IsValidRankingCache(current.PreviousWealthTop, population)
            || !IsValidChronicleLeader(current.CombatPowerLeader, population)
            || !IsValidChronicleLeader(current.RealmLeader, population)
            || !IsValidChronicleLeader(current.AptitudeLeader, population)
            || !IsValidChronicleLeader(current.WealthLeader, population)
            || current.LastRankingSampleYear is < 0
            || current.LastRankingSampleYear > currentYear
            || current.DeathCount < 0
            || current.UniqueDeathCount is < 0 || current.UniqueDeathCount > current.DeathCount
            || !IsValidAgentBits(current.DeathAgentBits, wordCount, population)
            || current.ReincarnationEntryCount < 0
            || current.UniqueReincarnationEntryCount is < 0
                || current.UniqueReincarnationEntryCount > current.ReincarnationEntryCount
            || !IsValidAgentBits(current.ReincarnationEntryAgentBits, wordCount, population)
            || current.RebirthCount < 0
            || current.UniqueRebirthCount is < 0 || current.UniqueRebirthCount > current.RebirthCount
            || !IsValidAgentBits(current.RebirthAgentBits, wordCount, population)
            || current.ReincarnationRiverResidents is < 0
                || current.ReincarnationRiverResidents > population
            || current.CombatsCompleted < 0
            || current.MonsterHunts < 0
            || current.MissionsCompleted < 0
            || current.SpiritStonesMined < 0
            || current.TreasuresCrafted < 0
            || current.PillsCrafted < 0
            || current.SecretRealmsDiscovered < 0
            || current.UniqueSecretRealmDiscoverers is < 0
                || current.UniqueSecretRealmDiscoverers > current.SecretRealmsDiscovered
            || current.UniqueSecretRealmDiscoverers > population
            || (current.SecretRealmDiscovererBits is null
                ? current.UniqueSecretRealmDiscoverers != 0
                : !IsValidAgentBits(
                    current.SecretRealmDiscovererBits,
                    wordCount,
                    population)))
        {
            return false;
        }

        for (var realm = 0; realm < RealmRules.All.Count; realm++)
        {
            if (WorldCenturyChronicle.CountSetBits(current.RealmBreakthroughAgentBits[realm])
                    != current.RealmBreakthroughUniqueCounts[realm])
            {
                return false;
            }
        }

        return WorldCenturyChronicle.CountSetBits(current.DeathAgentBits)
                == current.UniqueDeathCount
            && WorldCenturyChronicle.CountSetBits(current.ReincarnationEntryAgentBits)
                == current.UniqueReincarnationEntryCount
            && WorldCenturyChronicle.CountSetBits(current.RebirthAgentBits)
                == current.UniqueRebirthCount
            && (current.SecretRealmDiscovererBits is null
                || WorldCenturyChronicle.CountSetBits(current.SecretRealmDiscovererBits)
                    == current.UniqueSecretRealmDiscoverers);
    }

    private static bool IsValidCenturyRealmCounts(
        int[]? counts,
        int[]? uniqueCounts,
        int population) =>
        counts is { Length: var countLength }
        && countLength == RealmRules.All.Count
        && uniqueCounts is { Length: var uniqueLength }
        && uniqueLength == RealmRules.All.Count
        && counts.All(value => value >= 0)
        && uniqueCounts.All(value => value is >= 0 && value <= population)
        && counts.Zip(uniqueCounts).All(pair => pair.First >= pair.Second);

    private static bool IsValidPositionChangeTotals(long[]? values) =>
        values is { Length: WorldCenturyChronicle.RankingTierCount }
        && values.All(value => value >= 0);

    private static bool IsValidChronicleLeader(WorldChronicleLeader leader, int population) =>
        leader.AgentId.Value is >= 0
        && leader.AgentId.Value < population
        && !string.IsNullOrWhiteSpace(leader.Name);

    private static bool IsValidDecisionRanking(
        DecisionRankingCheckpoint checkpoint,
        int population) =>
        checkpoint.LastProcessedMonth >= -1
        && IsValidRanksByAgent(checkpoint.CombatRanksByAgent, population)
        && IsValidRanksByAgent(checkpoint.RealmRanksByAgent, population)
        && IsValidRanksByAgent(checkpoint.AptitudeRanksByAgent, population)
        && IsValidRanksByAgent(checkpoint.WealthRanksByAgent, population);

    private static bool IsValidRanksByAgent(int[]? ranks, int population) =>
        ranks is { Length: var length }
        && length == population
        && ranks.All(rank => rank is >= 1 && rank <= population)
        && ranks.Distinct().Count() == population;

    private static bool IsValidRankingCache(int[]? agents, int population) =>
        agents is { Length: > 0 and <= WorldCenturyChronicle.RankingCacheSize }
        && agents.All(agent => agent is >= 0 && agent < population)
        && agents.Distinct().Count() == agents.Length;

    private static bool IsValidAgentBits(ulong[]? bits, int wordCount, int population)
    {
        if (bits is not { Length: var length } || length != wordCount)
        {
            return false;
        }

        var usedBits = population & 63;
        if (usedBits == 0)
        {
            return true;
        }

        var validMask = (1UL << usedBits) - 1;
        return (bits[^1] & ~validMask) == 0;
    }

    private static bool IsValidRankingHistory(
        RankingHistoryCheckpoint? checkpoint,
        long maximumTick,
        int population,
        bool requirePositionChangeTotals)
    {
        if (checkpoint?.Frames is not { Length: > 0 and <= 64 } frames)
        {
            return false;
        }

        var expectedLength = Math.Min(RankingHistory.MaximumTrackedRank, population);
        var previousTick = -1L;
        long[]? previousCombatTotals = null;
        long[]? previousRealmTotals = null;
        long[]? previousWealthTotals = null;
        foreach (var frame in frames)
        {
            if (frame.Tick <= previousTick
                || frame.Tick > maximumTick
                || frame.CombatPowerTop is not { Length: var combatLength }
                || combatLength != expectedLength
                || frame.RealmTop is not { Length: var realmLength }
                || realmLength != expectedLength
                || frame.WealthTop is not { Length: var wealthLength }
                || wealthLength != expectedLength
                || !IsValidRankingFrame(frame.CombatPowerTop, population)
                || !IsValidRankingFrame(frame.RealmTop, population)
                || !IsValidRankingFrame(frame.WealthTop, population)
                || !IsValidPositionChangeTotals(
                    frame.CombatPowerPositionChangeTotals,
                    previousCombatTotals,
                    requirePositionChangeTotals)
                || !IsValidPositionChangeTotals(
                    frame.RealmPositionChangeTotals,
                    previousRealmTotals,
                    requirePositionChangeTotals)
                || !IsValidPositionChangeTotals(
                    frame.WealthPositionChangeTotals,
                    previousWealthTotals,
                    requirePositionChangeTotals))
            {
                return false;
            }

            previousTick = frame.Tick;
            previousCombatTotals = frame.CombatPowerPositionChangeTotals;
            previousRealmTotals = frame.RealmPositionChangeTotals;
            previousWealthTotals = frame.WealthPositionChangeTotals;
        }

        return true;
    }

    private static bool IsValidRankingFrame(int[] ranking, int population)
    {
        var seen = new HashSet<int>();
        return ranking.All(agent => agent >= 0 && agent < population && seen.Add(agent));
    }

    private static bool IsValidPositionChangeTotals(
        long[]? totals,
        long[]? previousTotals,
        bool required)
    {
        if (totals is null)
        {
            return !required;
        }
        if (totals.Length != 7 || totals.Any(value => value < 0))
        {
            return false;
        }

        return previousTotals is null
            || totals.Zip(previousTotals).All(pair => pair.First >= pair.Second);
    }
}
