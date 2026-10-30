namespace Chronicles.Simulation;

public readonly record struct GoalId(ulong Value)
{
    public override string ToString() => $"G{Value:X16}";
}

public readonly record struct PlanId(ulong Value)
{
    public override string ToString() => $"P{Value:X16}";
}

public readonly record struct TaskId(ulong Value)
{
    public override string ToString() => $"T{Value:X16}";
}

public readonly record struct ActionId(ulong Value)
{
    public override string ToString() => $"A{Value:X16}";
}

public readonly record struct MemoryId(ulong Value)
{
    public override string ToString() => $"M{Value:X16}";
}

public readonly record struct PerceivedFactId(ulong Value)
{
    public override string ToString() => $"F{Value:X16}";
}

public readonly record struct KnowledgeClaimId(ulong Value)
{
    public override string ToString() => $"K{Value:X16}";
}

public readonly record struct SimulationEventDefinitionId(ulong Value)
{
    public override string ToString() => $"ED{Value:X16}";
}

public readonly record struct SimulationEventInstanceId(ulong Value)
{
    public override string ToString() => $"EI{Value:X16}";
}

public readonly record struct SimulationEventRecordId(ulong Value)
{
    public override string ToString() => $"ER{Value:X16}";
}

public readonly record struct SimulationResultId(ulong Value)
{
    public override string ToString() => $"RS{Value:X16}";
}

public readonly record struct SimulationCausalThreadId(ulong Value)
{
    public override string ToString() => $"CT{Value:X16}";
}

public enum CausalResolutionCode : byte
{
    None,
    Committed,
    Reject,
    Revalidate,
    Retry,
    Defer,
    AlternativeResult,
    Compensate,
    Blocked,
}

public enum CausalFailureScope : byte
{
    None,
    Action,
    Task,
    Plan,
    Goal,
}

public enum PerceivedFactKind : byte
{
    OwnLifeState,
    OwnHealthRatio,
    OwnCultivationState,
    CurrentLocation,
    ObservedCombatRank,
}

public enum AgentMemoryKind : byte
{
    Birth,
    Decision,
    Failure,
    Relationship,
    Teaching,
    Combat,
    Death,
    Rebirth,
}

public enum KnowledgeClaimKind : byte
{
    None,
    Technique,
    Spell,
}

public enum KnowledgeClaimStatus : byte
{
    None,
    Observed,
    Learned,
    Misunderstood,
    Corrected,
    Superseded,
}

public readonly record struct CausalObservationInput(
    long Tick,
    PersonIdentityId PersonId,
    int LifeNumber,
    PersonLifeState LifeState,
    int Health,
    int MaximumHealth,
    CultivationState CultivationState,
    ushort LocationId,
    int ObservedCombatRank);

public readonly record struct PerceivedFactState(
    PerceivedFactId Id,
    PerceivedFactKind Kind,
    PersonIdentityId ObserverPersonId,
    PersonIdentityId SubjectPersonId,
    long ObservedTick,
    long NumericValue,
    SimulationEventRecordId SourceRecordId);

public readonly record struct AgentCausalPathState(
    long AuthorityRevision,
    GoalId GoalId,
    PlanId PlanId,
    TaskId TaskId,
    ActionId ActionId,
    SimulationCausalThreadId ThreadId,
    SimulationEventDefinitionId EventDefinitionId,
    SimulationEventInstanceId EventInstanceId,
    SimulationResultId ResultId,
    SimulationEventRecordId EventRecordId,
    SimulationEventRecordId RootRecordId,
    SimulationEventRecordId? ParentRecordId,
    AgentGoalKind Goal,
    AgentActivity Action,
    AgentActivity? AlternativeAction,
    DecisionReasonCode Reason,
    CausalResolutionCode Resolution,
    long StartedTick,
    long CompletedTick,
    bool IsWaiting,
    bool WasInterrupted,
    CausalFailureScope FailureScope = CausalFailureScope.None);

public readonly record struct AgentMemoryState(
    MemoryId Id,
    int LifeNumber,
    AgentMemoryKind Kind,
    long FirstTick,
    long LastReinforcedTick,
    ushort StrengthHundredths,
    ushort ReinforcementCount,
    AgentActivity? RelatedActivity,
    DecisionReasonCode? RelatedReason,
    SimulationResultId SourceResultId,
    SimulationEventRecordId SourceRecordId,
    bool IsImportant,
    PersonIdentityId RelatedPersonId = default,
    RelationId RelationId = default);

public readonly record struct KnowledgeClaimState(
    KnowledgeClaimId Id,
    int LifeNumber,
    KnowledgeClaimKind Kind,
    KnowledgeClaimStatus Status,
    PersonIdentityId SourcePersonId,
    PersonIdentityId RecipientPersonId,
    TeachingProvenanceId TeachingProvenanceId,
    SimulationResultId SourceResultId,
    long CreatedTick,
    long AuthorityRevision);

public sealed record AgentCausalSnapshot(
    string ContractId,
    int ContractRevision,
    long AuthorityRevision,
    AgentCausalPathState CurrentPath,
    IReadOnlyList<PerceivedFactState> PerceivedFacts,
    IReadOnlyList<AgentMemoryState> CurrentLifeMemories,
    KnowledgeClaimState? LatestKnowledgeClaim,
    bool RequiresReview);

public sealed record AgentCausalStoreCheckpoint(
    int ContractRevision,
    long AuthorityRevision,
    AgentCausalPathState[] CurrentPaths,
    long[] AgentAuthorityRevisions,
    long[] MemorySequences,
    PerceivedFactState[] PerceivedFacts,
    byte[] PerceivedFactCounts,
    AgentMemoryState[] Memories,
    byte[] MemoryCounts,
    byte[] NextMemorySlots,
    KnowledgeClaimState[] LatestKnowledgeClaims);

public static class AgentCausalRules
{
    public const string ContractId = "PC-CAUSAL-RUNTIME-001";
    public const int ContractRevision = 1;
    public const int PerceivedFactCapacity = 5;
    public const int MemoryCapacityPerAgent = 8;
    public const string MemoryPolicyReviewId = "PC-TEMP-MEMORY-POLICY-113";

    public static SimulationCausalThreadId CreateThreadId(PersonIdentityId personId) =>
        new(Stable(personId.Value, 0x5448_5245_4144UL));

    public static GoalId CreateGoalId(PersonIdentityId personId, AgentGoalKind goal) =>
        new(Stable(personId.Value, 0x4700UL | (uint)goal));

    public static PlanId CreatePlanId(PersonIdentityId personId, long revision) =>
        new(Stable(personId.Value, 0x5000_0000_0000_0000UL ^ checked((ulong)revision)));

    public static TaskId CreateTaskId(PersonIdentityId personId, long revision) =>
        new(Stable(personId.Value, 0x5400_0000_0000_0000UL ^ checked((ulong)revision)));

    public static ActionId CreateActionId(PersonIdentityId personId, long revision) =>
        new(Stable(personId.Value, 0x4100_0000_0000_0000UL ^ checked((ulong)revision)));

    public static SimulationEventDefinitionId CreateEventDefinitionId(AgentActivity activity) =>
        new(Stable(0x4556_454E_5400UL, checked((uint)activity + 1U)));

    public static SimulationEventInstanceId CreateEventInstanceId(
        PersonIdentityId personId,
        long revision) => new(Stable(personId.Value, 0x4900_0000_0000_0000UL ^ checked((ulong)revision)));

    public static SimulationResultId CreateResultId(PersonIdentityId personId, long revision) =>
        new(Stable(personId.Value, 0x5200_0000_0000_0000UL ^ checked((ulong)revision)));

    public static SimulationEventRecordId CreateEventRecordId(
        PersonIdentityId personId,
        long revision) => new(Stable(personId.Value, 0x4500_0000_0000_0000UL ^ checked((ulong)revision)));

    public static PerceivedFactId CreateFactId(
        PersonIdentityId personId,
        PerceivedFactKind kind) => new(Stable(personId.Value, 0x4600UL | (uint)kind));

    public static MemoryId CreateMemoryId(PersonIdentityId personId, long sequence) =>
        new(Stable(personId.Value, 0x4D00_0000_0000_0000UL ^ checked((ulong)sequence)));

    public static KnowledgeClaimId CreateKnowledgeClaimId(
        PersonIdentityId recipient,
        TeachingProvenanceId provenanceId) => new(Stable(recipient.Value, provenanceId.Value));

    public static CausalResolutionCode MapResolution(DecisionResolutionKind resolution) =>
        resolution switch
        {
            DecisionResolutionKind.Selected or DecisionResolutionKind.Forced =>
                CausalResolutionCode.Committed,
            DecisionResolutionKind.Deferred => CausalResolutionCode.Defer,
            DecisionResolutionKind.Blocked => CausalResolutionCode.Blocked,
            _ => throw new ArgumentOutOfRangeException(nameof(resolution)),
        };

    private static ulong Stable(ulong left, ulong right)
    {
        var value = left ^ right ^ 0x9E37_79B9_7F4A_7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58_476D_1CE4_E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D0_49BB_1331_11EBUL;
        value ^= value >> 31;
        return value == 0 ? 1UL : value;
    }
}

internal sealed class AgentCausalStore
{
    private readonly int _population;
    private readonly AgentCausalPathState[] _currentPaths;
    private readonly long[] _agentAuthorityRevisions;
    private readonly long[] _memorySequences;
    private readonly PerceivedFactState[] _perceivedFacts;
    private readonly byte[] _perceivedFactCounts;
    private readonly AgentMemoryState[] _memories;
    private readonly byte[] _memoryCounts;
    private readonly byte[] _nextMemorySlots;
    private readonly KnowledgeClaimState[] _latestKnowledgeClaims;
    private long _authorityRevision;

    public AgentCausalStore(int population)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        _population = population;
        _currentPaths = new AgentCausalPathState[population];
        _agentAuthorityRevisions = new long[population];
        _memorySequences = new long[population];
        _perceivedFacts = new PerceivedFactState[checked(population * AgentCausalRules.PerceivedFactCapacity)];
        _perceivedFactCounts = new byte[population];
        _memories = new AgentMemoryState[checked(population * AgentCausalRules.MemoryCapacityPerAgent)];
        _memoryCounts = new byte[population];
        _nextMemorySlots = new byte[population];
        _latestKnowledgeClaims = new KnowledgeClaimState[population];
    }

    public long AuthorityRevision => _authorityRevision;

    public AgentCausalPathState GetCurrentPath(int agentIndex)
    {
        ValidateAgent(agentIndex);
        return _currentPaths[agentIndex];
    }

    public KnowledgeClaimId GetLatestKnowledgeClaimId(int agentIndex)
    {
        ValidateAgent(agentIndex);
        return _latestKnowledgeClaims[agentIndex].Id;
    }

    public void StartLife(
        int agentIndex,
        CausalObservationInput observation,
        AgentDecisionTraceState decision,
        AgentMemoryKind lifeBoundaryKind)
    {
        ValidateAgent(agentIndex);
        Array.Clear(
            _memories,
            agentIndex * AgentCausalRules.MemoryCapacityPerAgent,
            AgentCausalRules.MemoryCapacityPerAgent);
        _memoryCounts[agentIndex] = 0;
        _nextMemorySlots[agentIndex] = 0;
        _latestKnowledgeClaims[agentIndex] = default;
        RecordDecision(agentIndex, observation, decision, forceMemory: false);
        AddOrReinforceMemory(
            agentIndex,
            observation,
            lifeBoundaryKind,
            decision.SelectedActivity,
            decision.SelectedReason,
            _currentPaths[agentIndex].ResultId,
            _currentPaths[agentIndex].EventRecordId,
            isImportant: true);
    }

    public void RecordDecision(
        int agentIndex,
        CausalObservationInput observation,
        AgentDecisionTraceState decision,
        bool forceMemory = false)
    {
        ValidateAgent(agentIndex);
        if (observation.PersonId.Value == 0
            || observation.LifeNumber <= 0
            || decision.AuthorityRevision <= 0)
        {
            throw new ArgumentException("인과 판정 입력의 안정 ID와 revision이 유효해야 합니다.");
        }

        var previous = _currentPaths[agentIndex];
        var recordId = AgentCausalRules.CreateEventRecordId(
            observation.PersonId,
            decision.AuthorityRevision);
        var rootRecordId = previous.ThreadId.Value != 0
            && previous.ThreadId == AgentCausalRules.CreateThreadId(observation.PersonId)
            ? previous.RootRecordId
            : recordId;
        SimulationEventRecordId? parent =
            previous.EventRecordId.Value == 0 || rootRecordId == recordId
                ? null
                : previous.EventRecordId;
        var path = new AgentCausalPathState(
            decision.AuthorityRevision,
            AgentCausalRules.CreateGoalId(observation.PersonId, decision.SupportingGoal),
            AgentCausalRules.CreatePlanId(observation.PersonId, decision.AuthorityRevision),
            AgentCausalRules.CreateTaskId(observation.PersonId, decision.AuthorityRevision),
            AgentCausalRules.CreateActionId(observation.PersonId, decision.AuthorityRevision),
            AgentCausalRules.CreateThreadId(observation.PersonId),
            AgentCausalRules.CreateEventDefinitionId(decision.SelectedActivity),
            AgentCausalRules.CreateEventInstanceId(observation.PersonId, decision.AuthorityRevision),
            AgentCausalRules.CreateResultId(observation.PersonId, decision.AuthorityRevision),
            recordId,
            rootRecordId,
            parent,
            decision.SupportingGoal,
            decision.SelectedActivity,
            decision.AlternativeActivity,
            decision.SelectedReason,
            AgentCausalRules.MapResolution(decision.Resolution),
            decision.Tick,
            decision.Tick,
            IsWaiting: decision.Resolution == DecisionResolutionKind.Deferred,
            WasInterrupted: false);

        _currentPaths[agentIndex] = path;
        _agentAuthorityRevisions[agentIndex] = checked(_agentAuthorityRevisions[agentIndex] + 1);
        _authorityRevision = checked(_authorityRevision + 1);
        CapturePerceivedFacts(agentIndex, observation, recordId);

        var activityChanged = previous.ActionId.Value != 0 && previous.Action != path.Action;
        var meaningful = forceMemory
            || activityChanged
            || path.Resolution is not CausalResolutionCode.Committed
            || decision.AuthorityRevision % 12 == 0;
        if (meaningful)
        {
            AddOrReinforceMemory(
                agentIndex,
                observation,
                path.Resolution == CausalResolutionCode.Committed
                    ? AgentMemoryKind.Decision
                    : AgentMemoryKind.Failure,
                path.Action,
                path.Reason,
                path.ResultId,
                path.EventRecordId,
                isImportant: path.Resolution is CausalResolutionCode.Blocked
                    or CausalResolutionCode.Reject
                    or CausalResolutionCode.AlternativeResult);
        }
    }

    public void RecordResolution(
        int agentIndex,
        CausalObservationInput observation,
        CausalResolutionCode resolution,
        AgentMemoryKind memoryKind,
        bool interrupted,
        CausalFailureScope failureScope = CausalFailureScope.Action)
    {
        ValidateAgent(agentIndex);
        if (resolution == CausalResolutionCode.None)
        {
            throw new ArgumentOutOfRangeException(nameof(resolution));
        }

        var current = _currentPaths[agentIndex];
        if (current.ActionId.Value == 0)
        {
            return;
        }

        current = current with
        {
            Resolution = resolution,
            CompletedTick = observation.Tick,
            IsWaiting = resolution is CausalResolutionCode.Defer or CausalResolutionCode.Retry,
            WasInterrupted = interrupted,
            FailureScope = resolution == CausalResolutionCode.Committed
                ? CausalFailureScope.None
                : failureScope,
        };
        _currentPaths[agentIndex] = current;
        _agentAuthorityRevisions[agentIndex] = checked(_agentAuthorityRevisions[agentIndex] + 1);
        _authorityRevision = checked(_authorityRevision + 1);
        AddOrReinforceMemory(
            agentIndex,
            observation,
            memoryKind,
            current.Action,
            current.Reason,
            current.ResultId,
            current.EventRecordId,
            isImportant: true);
    }

    public void RecordInteractionMemory(
        int agentIndex,
        CausalObservationInput observation,
        AgentMemoryKind kind,
        PersonIdentityId relatedPersonId,
        RelationId relationId,
        bool isImportant = false)
    {
        ValidateAgent(agentIndex);
        var current = _currentPaths[agentIndex];
        if (current.ActionId.Value == 0)
        {
            return;
        }

        AddOrReinforceMemory(
            agentIndex,
            observation,
            kind,
            current.Action,
            current.Reason,
            current.ResultId,
            current.EventRecordId,
            isImportant,
            relatedPersonId,
            relationId);
        _agentAuthorityRevisions[agentIndex] = checked(_agentAuthorityRevisions[agentIndex] + 1);
        _authorityRevision = checked(_authorityRevision + 1);
    }

    public void RecordKnowledgeClaim(
        int agentIndex,
        int lifeNumber,
        PersonIdentityId sourcePersonId,
        PersonIdentityId recipientPersonId,
        TeachingTargetKind target,
        TeachingResultKind result,
        TeachingProvenanceId provenanceId,
        long tick)
    {
        ValidateAgent(agentIndex);
        if (provenanceId.Value == 0 || recipientPersonId.Value == 0)
        {
            return;
        }

        var previous = _latestKnowledgeClaims[agentIndex];
        var revision = checked(previous.AuthorityRevision + 1);
        var status = result switch
        {
            TeachingResultKind.Misunderstanding => KnowledgeClaimStatus.Misunderstood,
            TeachingResultKind.Correction => KnowledgeClaimStatus.Corrected,
            TeachingResultKind.Refused or TeachingResultKind.Interrupted => KnowledgeClaimStatus.Observed,
            _ => KnowledgeClaimStatus.Learned,
        };
        var sourceResult = _currentPaths[agentIndex].ResultId;
        _latestKnowledgeClaims[agentIndex] = new KnowledgeClaimState(
            AgentCausalRules.CreateKnowledgeClaimId(recipientPersonId, provenanceId),
            lifeNumber,
            target == TeachingTargetKind.Technique
                ? KnowledgeClaimKind.Technique
                : KnowledgeClaimKind.Spell,
            status,
            sourcePersonId,
            recipientPersonId,
            provenanceId,
            sourceResult,
            tick,
            revision);
        _agentAuthorityRevisions[agentIndex] = checked(_agentAuthorityRevisions[agentIndex] + 1);
        _authorityRevision = checked(_authorityRevision + 1);
    }

    public AgentCausalSnapshot GetSnapshot(int agentIndex)
    {
        ValidateAgent(agentIndex);
        var facts = new PerceivedFactState[_perceivedFactCounts[agentIndex]];
        Array.Copy(
            _perceivedFacts,
            agentIndex * AgentCausalRules.PerceivedFactCapacity,
            facts,
            0,
            facts.Length);
        var memories = new AgentMemoryState[_memoryCounts[agentIndex]];
        var start = agentIndex * AgentCausalRules.MemoryCapacityPerAgent;
        var firstLogicalSlot = memories.Length == AgentCausalRules.MemoryCapacityPerAgent
            ? _nextMemorySlots[agentIndex]
            : 0;
        for (var offset = 0; offset < memories.Length; offset++)
        {
            var logical = (firstLogicalSlot + offset) % AgentCausalRules.MemoryCapacityPerAgent;
            memories[offset] = _memories[start + logical];
        }

        var claim = _latestKnowledgeClaims[agentIndex];
        return new AgentCausalSnapshot(
            AgentCausalRules.ContractId,
            AgentCausalRules.ContractRevision,
            _agentAuthorityRevisions[agentIndex],
            _currentPaths[agentIndex],
            Array.AsReadOnly(facts),
            Array.AsReadOnly(memories),
            claim.Id.Value == 0 ? null : claim,
            RequiresReview: true);
    }

    public AgentCausalStoreCheckpoint CreateCheckpoint() => new(
        AgentCausalRules.ContractRevision,
        _authorityRevision,
        _currentPaths.ToArray(),
        _agentAuthorityRevisions.ToArray(),
        _memorySequences.ToArray(),
        _perceivedFacts.ToArray(),
        _perceivedFactCounts.ToArray(),
        _memories.ToArray(),
        _memoryCounts.ToArray(),
        _nextMemorySlots.ToArray(),
        _latestKnowledgeClaims.ToArray());

    public ulong ComputeStateHash()
    {
        var hash = 14695981039346656037UL;
        Add(ref hash, checked((ulong)_authorityRevision));
        for (var agent = 0; agent < _population; agent++)
        {
            Add(ref hash, checked((ulong)_agentAuthorityRevisions[agent]));
            Add(ref hash, checked((ulong)_memorySequences[agent]));
            var path = _currentPaths[agent];
            Add(ref hash, checked((ulong)path.AuthorityRevision));
            Add(ref hash, path.GoalId.Value);
            Add(ref hash, path.PlanId.Value);
            Add(ref hash, path.TaskId.Value);
            Add(ref hash, path.ActionId.Value);
            Add(ref hash, path.ThreadId.Value);
            Add(ref hash, path.EventDefinitionId.Value);
            Add(ref hash, path.EventInstanceId.Value);
            Add(ref hash, path.ResultId.Value);
            Add(ref hash, path.EventRecordId.Value);
            Add(ref hash, path.RootRecordId.Value);
            Add(ref hash, path.ParentRecordId?.Value ?? 0UL);
            Add(ref hash, (byte)path.Goal);
            Add(ref hash, (byte)path.Action);
            Add(ref hash, path.AlternativeAction is { } alternative
                ? checked((ulong)(byte)alternative + 1UL)
                : 0UL);
            Add(ref hash, (byte)path.Reason);
            Add(ref hash, (byte)path.Resolution);
            Add(ref hash, checked((ulong)path.StartedTick));
            Add(ref hash, checked((ulong)path.CompletedTick));
            Add(ref hash, path.IsWaiting ? 1UL : 0UL);
            Add(ref hash, path.WasInterrupted ? 1UL : 0UL);
            Add(ref hash, (byte)path.FailureScope);
            Add(ref hash, _perceivedFactCounts[agent]);
            var factStart = agent * AgentCausalRules.PerceivedFactCapacity;
            for (var offset = 0; offset < _perceivedFactCounts[agent]; offset++)
            {
                var fact = _perceivedFacts[factStart + offset];
                Add(ref hash, fact.Id.Value);
                Add(ref hash, (byte)fact.Kind);
                Add(ref hash, fact.ObserverPersonId.Value);
                Add(ref hash, fact.SubjectPersonId.Value);
                Add(ref hash, checked((ulong)fact.ObservedTick));
                Add(ref hash, unchecked((ulong)fact.NumericValue));
                Add(ref hash, fact.SourceRecordId.Value);
            }

            Add(ref hash, _memoryCounts[agent]);
            Add(ref hash, _nextMemorySlots[agent]);
            var memoryStart = agent * AgentCausalRules.MemoryCapacityPerAgent;
            for (var offset = 0; offset < AgentCausalRules.MemoryCapacityPerAgent; offset++)
            {
                var memory = _memories[memoryStart + offset];
                Add(ref hash, memory.Id.Value);
                Add(ref hash, checked((ulong)memory.LifeNumber));
                Add(ref hash, (byte)memory.Kind);
                Add(ref hash, checked((ulong)memory.FirstTick));
                Add(ref hash, checked((ulong)memory.LastReinforcedTick));
                Add(ref hash, memory.StrengthHundredths);
                Add(ref hash, memory.ReinforcementCount);
                Add(ref hash, memory.RelatedActivity is { } activity
                    ? checked((ulong)(byte)activity + 1UL)
                    : 0UL);
                Add(ref hash, memory.RelatedReason is { } reason
                    ? checked((ulong)(byte)reason + 1UL)
                    : 0UL);
                Add(ref hash, memory.SourceResultId.Value);
                Add(ref hash, memory.SourceRecordId.Value);
                Add(ref hash, memory.IsImportant ? 1UL : 0UL);
                Add(ref hash, memory.RelatedPersonId.Value);
                Add(ref hash, memory.RelationId.Value);
            }

            var claim = _latestKnowledgeClaims[agent];
            Add(ref hash, claim.Id.Value);
            Add(ref hash, checked((ulong)claim.LifeNumber));
            Add(ref hash, (byte)claim.Kind);
            Add(ref hash, (byte)claim.Status);
            Add(ref hash, claim.SourcePersonId.Value);
            Add(ref hash, claim.RecipientPersonId.Value);
            Add(ref hash, claim.TeachingProvenanceId.Value);
            Add(ref hash, claim.SourceResultId.Value);
            Add(ref hash, checked((ulong)claim.CreatedTick));
            Add(ref hash, checked((ulong)claim.AuthorityRevision));
        }

        return hash;
    }

    public static AgentCausalStore Restore(int population, AgentCausalStoreCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (!IsValidCheckpoint(population, checkpoint))
        {
            throw new InvalidDataException("인과 런타임 checkpoint가 손상되었습니다.");
        }

        var store = new AgentCausalStore(population)
        {
            _authorityRevision = checkpoint.AuthorityRevision,
        };
        Array.Copy(checkpoint.CurrentPaths, store._currentPaths, population);
        Array.Copy(checkpoint.AgentAuthorityRevisions, store._agentAuthorityRevisions, population);
        Array.Copy(checkpoint.MemorySequences, store._memorySequences, population);
        Array.Copy(checkpoint.PerceivedFacts, store._perceivedFacts, checkpoint.PerceivedFacts.Length);
        Array.Copy(checkpoint.PerceivedFactCounts, store._perceivedFactCounts, population);
        Array.Copy(checkpoint.Memories, store._memories, checkpoint.Memories.Length);
        Array.Copy(checkpoint.MemoryCounts, store._memoryCounts, population);
        Array.Copy(checkpoint.NextMemorySlots, store._nextMemorySlots, population);
        Array.Copy(checkpoint.LatestKnowledgeClaims, store._latestKnowledgeClaims, population);
        return store;
    }

    public static bool IsValidCheckpoint(int population, AgentCausalStoreCheckpoint? checkpoint)
    {
        if (checkpoint is null
            || checkpoint.ContractRevision != AgentCausalRules.ContractRevision
            || checkpoint.AuthorityRevision < 0
            || checkpoint.CurrentPaths.Length != population
            || checkpoint.AgentAuthorityRevisions.Length != population
            || checkpoint.MemorySequences.Length != population
            || checkpoint.PerceivedFacts.Length != population * AgentCausalRules.PerceivedFactCapacity
            || checkpoint.PerceivedFactCounts.Length != population
            || checkpoint.Memories.Length != population * AgentCausalRules.MemoryCapacityPerAgent
            || checkpoint.MemoryCounts.Length != population
            || checkpoint.NextMemorySlots.Length != population
            || checkpoint.LatestKnowledgeClaims.Length != population)
        {
            return false;
        }

        for (var agent = 0; agent < population; agent++)
        {
            if (checkpoint.AgentAuthorityRevisions[agent] < 0
                || checkpoint.MemorySequences[agent] < 0
                || checkpoint.PerceivedFactCounts[agent] > AgentCausalRules.PerceivedFactCapacity
                || checkpoint.MemoryCounts[agent] > AgentCausalRules.MemoryCapacityPerAgent
                || checkpoint.NextMemorySlots[agent] >= AgentCausalRules.MemoryCapacityPerAgent)
            {
                return false;
            }

            var path = checkpoint.CurrentPaths[agent];
            if (path.AuthorityRevision <= 0
                || path.GoalId.Value == 0
                || path.PlanId.Value == 0
                || path.TaskId.Value == 0
                || path.ActionId.Value == 0
                || path.ThreadId.Value == 0
                || path.EventDefinitionId.Value == 0
                || path.EventInstanceId.Value == 0
                || path.ResultId.Value == 0
                || path.EventRecordId.Value == 0
                || path.RootRecordId.Value == 0
                || !Enum.IsDefined(path.Resolution)
                || !Enum.IsDefined(path.FailureScope))
            {
                return false;
            }
        }

        return true;
    }

    private void CapturePerceivedFacts(
        int agentIndex,
        CausalObservationInput observation,
        SimulationEventRecordId sourceRecordId)
    {
        var start = agentIndex * AgentCausalRules.PerceivedFactCapacity;
        _perceivedFacts[start] = CreateFact(
            observation,
            PerceivedFactKind.OwnLifeState,
            (long)observation.LifeState,
            sourceRecordId);
        _perceivedFacts[start + 1] = CreateFact(
            observation,
            PerceivedFactKind.OwnHealthRatio,
            observation.MaximumHealth <= 0
                ? 0
                : Math.Clamp((long)observation.Health * 10_000L / observation.MaximumHealth, 0, 10_000),
            sourceRecordId);
        _perceivedFacts[start + 2] = CreateFact(
            observation,
            PerceivedFactKind.OwnCultivationState,
            (long)observation.CultivationState,
            sourceRecordId);
        _perceivedFacts[start + 3] = CreateFact(
            observation,
            PerceivedFactKind.CurrentLocation,
            observation.LocationId,
            sourceRecordId);
        _perceivedFacts[start + 4] = CreateFact(
            observation,
            PerceivedFactKind.ObservedCombatRank,
            observation.ObservedCombatRank,
            sourceRecordId);
        _perceivedFactCounts[agentIndex] = AgentCausalRules.PerceivedFactCapacity;
    }

    private static PerceivedFactState CreateFact(
        CausalObservationInput observation,
        PerceivedFactKind kind,
        long value,
        SimulationEventRecordId sourceRecordId) => new(
            AgentCausalRules.CreateFactId(observation.PersonId, kind),
            kind,
            observation.PersonId,
            observation.PersonId,
            observation.Tick,
            value,
            sourceRecordId);

    private void AddOrReinforceMemory(
        int agentIndex,
        CausalObservationInput observation,
        AgentMemoryKind kind,
        AgentActivity? activity,
        DecisionReasonCode? reason,
        SimulationResultId resultId,
        SimulationEventRecordId recordId,
        bool isImportant,
        PersonIdentityId relatedPersonId = default,
        RelationId relationId = default)
    {
        var start = agentIndex * AgentCausalRules.MemoryCapacityPerAgent;
        var count = _memoryCounts[agentIndex];
        for (var offset = 0; offset < count; offset++)
        {
            var slot = start + offset;
            var existing = _memories[slot];
            if (existing.LifeNumber != observation.LifeNumber
                || existing.Kind != kind
                || existing.RelatedActivity != activity
                || existing.RelatedReason != reason)
            {
                continue;
            }

            _memories[slot] = existing with
            {
                LastReinforcedTick = observation.Tick,
                StrengthHundredths = checked((ushort)Math.Min(10_000, existing.StrengthHundredths + 350)),
                ReinforcementCount = existing.ReinforcementCount == ushort.MaxValue
                    ? ushort.MaxValue
                    : checked((ushort)(existing.ReinforcementCount + 1)),
                SourceResultId = resultId,
                SourceRecordId = recordId,
                IsImportant = existing.IsImportant || isImportant,
                RelatedPersonId = relatedPersonId.Value == 0
                    ? existing.RelatedPersonId
                    : relatedPersonId,
                RelationId = relationId.Value == 0 ? existing.RelationId : relationId,
            };
            return;
        }

        var next = _nextMemorySlots[agentIndex];
        var sequence = checked(++_memorySequences[agentIndex]);
        _memories[start + next] = new AgentMemoryState(
            AgentCausalRules.CreateMemoryId(observation.PersonId, sequence),
            observation.LifeNumber,
            kind,
            observation.Tick,
            observation.Tick,
            StrengthHundredths: 1_000,
            ReinforcementCount: 1,
            activity,
            reason,
            resultId,
            recordId,
            isImportant,
            relatedPersonId,
            relationId);
        if (count < AgentCausalRules.MemoryCapacityPerAgent)
        {
            _memoryCounts[agentIndex] = checked((byte)(count + 1));
        }

        _nextMemorySlots[agentIndex] = checked((byte)((next + 1) % AgentCausalRules.MemoryCapacityPerAgent));
    }

    private void ValidateAgent(int agentIndex)
    {
        if ((uint)agentIndex >= (uint)_population)
        {
            throw new ArgumentOutOfRangeException(nameof(agentIndex));
        }
    }

    private static void Add(ref ulong hash, ulong value)
    {
        hash ^= value;
        hash *= 1099511628211UL;
    }
}
