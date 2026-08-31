using Chronicles.Core;

namespace Chronicles.Simulation;

public readonly record struct RelationId(ulong Value)
{
    public override string ToString() => $"R{Value:X16}";
}

[Flags]
public enum RelationRole : byte
{
    None = 0,
    MasterDisciple = 1 << 0,
    Friendship = 1 << 1,
    Romance = 1 << 2,
    Companion = 1 << 3,
    PersistentRivalry = 1 << 4,
}

public enum RelationInteractionKind : byte
{
    SharedCultivation,
    SharedMission,
    SharedJourney,
    SharedHunt,
    CombatOutcome,
}

public enum RelationHistoryKind : byte
{
    RelationCreated,
    RoleFormed,
    RoleEndedAtLifeBoundary,
    TeachingTransmissionEstablished,
    TeachingTransmissionUpdated,
}

public readonly record struct RelationMetrics(
    int FavorDisfavor,
    int TrustDistrust,
    int RespectContempt,
    int Gratitude,
    int Grievance,
    int Vigilance);

public readonly record struct RelationHistoryEntry(
    long Sequence,
    long Tick,
    RelationHistoryKind Kind,
    RelationRole ChangedRoles,
    PersonIdentityId LeftPersonId,
    PersonIdentityId RightPersonId,
    TeachingTargetKind? TeachingTarget = null,
    TeachingKnowledgeRef KnowledgeRef = default,
    TeachingProvenanceId ProvenanceId = default);

public sealed record RelationStateCheckpoint(
    RelationId Id,
    int LeftAgent,
    int RightAgent,
    PersonIdentityId LeftPersonId,
    PersonIdentityId RightPersonId,
    RelationMetrics LeftTowardRight,
    RelationMetrics RightTowardLeft,
    RelationRole Roles,
    int MasterAgent,
    ushort CooperativeInteractions,
    ushort JourneyInteractions,
    ushort CombatInteractions,
    RelationHistoryEntry[] History,
    int TechniqueTeachingSessions = 0,
    int SpellTeachingSessions = 0,
    bool TechniqueTransmissionEstablished = false,
    bool SpellTransmissionEstablished = false,
    TeachingKnowledgeRef TechniqueKnowledgeRef = default,
    TeachingKnowledgeRef SpellKnowledgeRef = default);

public sealed record RelationStoreCheckpoint(
    long HistorySequence,
    RelationStateCheckpoint[] Relations);

public sealed record RelationSnapshot(
    RelationId Id,
    AgentId OtherAgentId,
    string OtherName,
    PersonIdentityId OtherPersonId,
    RelationMetrics TowardOther,
    RelationMetrics OtherTowardSelf,
    RelationRole Roles,
    bool IsMaster,
    bool IsDisciple,
    IReadOnlyList<RelationHistoryEntry> ImportantHistory);

public static class RelationRules
{
    public const int MetricMinimum = -10_000;
    public const int MetricMaximum = 10_000;
    public const int NonNegativeMetricMaximum = 10_000;
    public const int MaximumRelationsPerAgent = 8;
    public const int CandidateProbeCount = 8;
    public const int SocialInteractionActionInterval = 4;
    public const int DirectTeachingCultivationSpeedBonusBasisPoints = 0;
    public const string ProvisionalReviewId = "PC-TEMP-RELATION-BALANCE-113";

    internal const int FriendshipInteractionThreshold = 4;
    internal const int CompanionInteractionThreshold = 3;
    internal const int RomanceInteractionThreshold = 12;
    internal const int RivalryInteractionThreshold = 3;
    internal const int FriendshipAxisThreshold = 500;
    internal const int RomanceAxisThreshold = 1_500;

    public static RelationId CreateId(int leftAgent, int rightAgent)
    {
        if (leftAgent == rightAgent)
        {
            throw new ArgumentException("자기 자신과 관계를 만들 수 없습니다.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(leftAgent);
        ArgumentOutOfRangeException.ThrowIfNegative(rightAgent);
        var left = Math.Min(leftAgent, rightAgent);
        var right = Math.Max(leftAgent, rightAgent);
        return new RelationId(
            ((ulong)checked((uint)left + 1U) << 32)
            | checked((uint)right + 1U));
    }

    internal static int GetOtherAgent(RelationId relationId, int agent)
    {
        var leftEncoded = (uint)(relationId.Value >> 32);
        var rightEncoded = (uint)relationId.Value;
        if (leftEncoded == 0 || rightEncoded == 0)
        {
            return -1;
        }

        var left = (long)leftEncoded - 1L;
        var right = (long)rightEncoded - 1L;
        if (left > int.MaxValue || right > int.MaxValue)
        {
            return -1;
        }

        return agent == (int)left
            ? (int)right
            : agent == (int)right
                ? (int)left
                : -1;
    }

    internal static RelationMetrics Apply(
        RelationMetrics value,
        RelationMetricDelta delta) => new(
        Math.Clamp(value.FavorDisfavor + delta.FavorDisfavor, MetricMinimum, MetricMaximum),
        Math.Clamp(value.TrustDistrust + delta.TrustDistrust, MetricMinimum, MetricMaximum),
        Math.Clamp(value.RespectContempt + delta.RespectContempt, MetricMinimum, MetricMaximum),
        Math.Clamp(value.Gratitude + delta.Gratitude, 0, NonNegativeMetricMaximum),
        Math.Clamp(value.Grievance + delta.Grievance, 0, NonNegativeMetricMaximum),
        Math.Clamp(value.Vigilance + delta.Vigilance, 0, NonNegativeMetricMaximum));

    internal static RelationMetricDelta GetMutualDelta(RelationInteractionKind kind) => kind switch
    {
        RelationInteractionKind.SharedCultivation => new(180, 140, 90, 20, -10, -15),
        RelationInteractionKind.SharedMission => new(220, 180, 110, 40, -15, -20),
        RelationInteractionKind.SharedJourney => new(240, 170, 120, 30, -10, 10),
        RelationInteractionKind.SharedHunt => new(260, 210, 160, 80, -20, 20),
        RelationInteractionKind.CombatOutcome => new(-60, -20, 100, 0, 100, 140),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    internal static RelationMetricDelta GetSecondParticipantDelta(
        RelationInteractionKind kind) => kind == RelationInteractionKind.CombatOutcome
            ? new RelationMetricDelta(-120, -60, 260, 0, 180, 340)
            : GetMutualDelta(kind);

    public static bool IsValidMetrics(RelationMetrics value) =>
        value.FavorDisfavor is >= MetricMinimum and <= MetricMaximum
        && value.TrustDistrust is >= MetricMinimum and <= MetricMaximum
        && value.RespectContempt is >= MetricMinimum and <= MetricMaximum
        && value.Gratitude is >= 0 and <= NonNegativeMetricMaximum
        && value.Grievance is >= 0 and <= NonNegativeMetricMaximum
        && value.Vigilance is >= 0 and <= NonNegativeMetricMaximum;
}

internal readonly record struct RelationMetricDelta(
    int FavorDisfavor,
    int TrustDistrust,
    int RespectContempt,
    int Gratitude,
    int Grievance,
    int Vigilance);

internal readonly record struct RelationReadState(
    RelationId Id,
    int OtherAgent,
    PersonIdentityId OtherPersonId,
    RelationMetrics TowardOther,
    RelationMetrics OtherTowardSelf,
    RelationRole Roles,
    int MasterAgent,
    RelationHistoryEntry[] History);

internal sealed class SparseRelationStore
{
    private sealed class RelationState
    {
        public required RelationId Id { get; init; }
        public required int LeftAgent { get; init; }
        public required int RightAgent { get; init; }
        public required PersonIdentityId LeftPersonId { get; set; }
        public required PersonIdentityId RightPersonId { get; set; }
        public RelationMetrics LeftTowardRight { get; set; }
        public RelationMetrics RightTowardLeft { get; set; }
        public RelationRole Roles { get; set; }
        public int MasterAgent { get; set; } = -1;
        public ushort CooperativeInteractions { get; set; }
        public ushort JourneyInteractions { get; set; }
        public ushort CombatInteractions { get; set; }
        public int TechniqueTeachingSessions { get; set; }
        public int SpellTeachingSessions { get; set; }
        public bool TechniqueTransmissionEstablished { get; set; }
        public bool SpellTransmissionEstablished { get; set; }
        public TeachingKnowledgeRef TechniqueKnowledgeRef { get; set; }
        public TeachingKnowledgeRef SpellKnowledgeRef { get; set; }
        public List<RelationHistoryEntry> History { get; } = new();
    }

    private readonly int _population;
    private readonly Dictionary<ulong, int> _indexById;
    private readonly List<RelationState> _relations;
    private readonly int[] _adjacency;
    private readonly byte[] _adjacencyCounts;
    private long _historySequence;

    public SparseRelationStore(int population)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        _population = population;
        _indexById = new Dictionary<ulong, int>(population * 2);
        _relations = new List<RelationState>(population * 2);
        _adjacency = new int[checked(population * RelationRules.MaximumRelationsPerAgent)];
        Array.Fill(_adjacency, -1);
        _adjacencyCounts = new byte[population];
    }

    public int Count => _relations.Count;

    public int GetRelationCount(int agent)
    {
        ValidateAgent(agent);
        return _adjacencyCounts[agent];
    }

    public int GetRelatedAgent(int agent, int adjacencyIndex)
    {
        ValidateAgent(agent);
        if ((uint)adjacencyIndex >= _adjacencyCounts[agent])
        {
            throw new ArgumentOutOfRangeException(nameof(adjacencyIndex));
        }

        var relationIndex = _adjacency[
            (agent * RelationRules.MaximumRelationsPerAgent) + adjacencyIndex];
        var relation = _relations[relationIndex];
        return relation.LeftAgent == agent ? relation.RightAgent : relation.LeftAgent;
    }

    public bool ApplyMutualInteraction(
        int leftAgent,
        int rightAgent,
        PersonIdentityId leftPersonId,
        PersonIdentityId rightPersonId,
        RelationInteractionKind kind,
        long tick)
    {
        if (!TryGetOrCreate(
                leftAgent,
                rightAgent,
                leftPersonId,
                rightPersonId,
                tick,
                out var relation))
        {
            return false;
        }

        SynchronizePersonIdentities(relation, leftAgent, leftPersonId, rightAgent, rightPersonId);
        var firstDelta = RelationRules.GetMutualDelta(kind);
        var secondDelta = RelationRules.GetSecondParticipantDelta(kind);
        if (relation.LeftAgent == leftAgent)
        {
            relation.LeftTowardRight = RelationRules.Apply(relation.LeftTowardRight, firstDelta);
            relation.RightTowardLeft = RelationRules.Apply(relation.RightTowardLeft, secondDelta);
        }
        else
        {
            relation.LeftTowardRight = RelationRules.Apply(relation.LeftTowardRight, secondDelta);
            relation.RightTowardLeft = RelationRules.Apply(relation.RightTowardLeft, firstDelta);
        }
        switch (kind)
        {
            case RelationInteractionKind.SharedCultivation:
            case RelationInteractionKind.SharedMission:
                relation.CooperativeInteractions = Increment(relation.CooperativeInteractions);
                break;
            case RelationInteractionKind.SharedJourney:
            case RelationInteractionKind.SharedHunt:
                relation.CooperativeInteractions = Increment(relation.CooperativeInteractions);
                relation.JourneyInteractions = Increment(relation.JourneyInteractions);
                break;
            case RelationInteractionKind.CombatOutcome:
                relation.CombatInteractions = Increment(relation.CombatInteractions);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        EvaluateRoles(relation, tick);
        return true;
    }

    public bool TryEstablishMasterDisciple(
        int teacher,
        int learner,
        PersonIdentityId teacherPersonId,
        PersonIdentityId learnerPersonId,
        long tick,
        out RelationId relationId)
    {
        if (!TryGetOrCreate(
                teacher,
                learner,
                teacherPersonId,
                learnerPersonId,
                tick,
                out var relation))
        {
            relationId = default;
            return false;
        }

        SynchronizePersonIdentities(
            relation,
            teacher,
            teacherPersonId,
            learner,
            learnerPersonId);
        if ((relation.Roles & RelationRole.MasterDisciple) != 0)
        {
            relationId = relation.Id;
            return relation.MasterAgent == teacher;
        }

        var teacherDelta = new RelationMetricDelta(100, 100, 50, 0, -20, -20);
        var learnerDelta = new RelationMetricDelta(100, 150, 300, 200, -20, -40);
        if (relation.LeftAgent == teacher)
        {
            relation.LeftTowardRight = RelationRules.Apply(relation.LeftTowardRight, teacherDelta);
            relation.RightTowardLeft = RelationRules.Apply(relation.RightTowardLeft, learnerDelta);
        }
        else
        {
            relation.LeftTowardRight = RelationRules.Apply(relation.LeftTowardRight, learnerDelta);
            relation.RightTowardLeft = RelationRules.Apply(relation.RightTowardLeft, teacherDelta);
        }

        relation.Roles |= RelationRole.MasterDisciple;
        relation.MasterAgent = teacher;
        AddHistory(
            relation,
            tick,
            RelationHistoryKind.RoleFormed,
            RelationRole.MasterDisciple);
        relationId = relation.Id;
        return true;
    }

    public bool TryGetPerspective(
        int agent,
        int other,
        out RelationMetrics towardOther,
        out RelationRole roles,
        out int masterAgent)
    {
        ValidateAgent(agent);
        ValidateAgent(other);
        var id = RelationRules.CreateId(agent, other);
        if (!_indexById.TryGetValue(id.Value, out var relationIndex))
        {
            towardOther = default;
            roles = RelationRole.None;
            masterAgent = -1;
            return false;
        }

        var relation = _relations[relationIndex];
        towardOther = relation.LeftAgent == agent
            ? relation.LeftTowardRight
            : relation.RightTowardLeft;
        roles = relation.Roles;
        masterAgent = relation.MasterAgent;
        return true;
    }

    public (int SessionsCompleted, bool TransmissionEstablished) GetTeachingContinuity(
        RelationId relationId,
        TeachingTargetKind targetKind,
        TeachingKnowledgeRef knowledgeRef)
    {
        if (!_indexById.TryGetValue(relationId.Value, out var relationIndex))
        {
            throw new InvalidOperationException("Teaching 관계가 존재하지 않습니다.");
        }

        var relation = _relations[relationIndex];
        return targetKind switch
        {
            TeachingTargetKind.Technique when relation.TechniqueKnowledgeRef == knowledgeRef => (
                relation.TechniqueTeachingSessions,
                relation.TechniqueTransmissionEstablished),
            TeachingTargetKind.Spell when relation.SpellKnowledgeRef == knowledgeRef => (
                relation.SpellTeachingSessions,
                relation.SpellTransmissionEstablished),
            TeachingTargetKind.Technique or TeachingTargetKind.Spell => (0, false),
            _ => throw new ArgumentOutOfRangeException(nameof(targetKind)),
        };
    }

    public void RecordTeachingResult(
        RelationId relationId,
        long tick,
        TeachingTargetKind targetKind,
        TeachingKnowledgeRef knowledgeRef,
        TeachingProvenanceId provenanceId,
        TeachingResultKind result)
    {
        if (!_indexById.TryGetValue(relationId.Value, out var relationIndex))
        {
            throw new InvalidOperationException("Teaching 관계가 존재하지 않습니다.");
        }

        if (result == TeachingResultKind.None)
        {
            throw new ArgumentOutOfRangeException(nameof(result));
        }

        if (!TeachingRules.IsTransmissionProgress(result))
        {
            return;
        }

        var relation = _relations[relationIndex];
        switch (targetKind)
        {
            case TeachingTargetKind.Technique:
                if (relation.TechniqueKnowledgeRef != knowledgeRef)
                {
                    relation.TechniqueTeachingSessions = 0;
                    relation.TechniqueTransmissionEstablished = false;
                    relation.TechniqueKnowledgeRef = knowledgeRef;
                }
                relation.TechniqueTeachingSessions = checked(
                    relation.TechniqueTeachingSessions + 1);
                relation.TechniqueTransmissionEstablished |=
                    result == TeachingResultKind.TransmissionEstablished;
                break;
            case TeachingTargetKind.Spell:
                if (relation.SpellKnowledgeRef != knowledgeRef)
                {
                    relation.SpellTeachingSessions = 0;
                    relation.SpellTransmissionEstablished = false;
                    relation.SpellKnowledgeRef = knowledgeRef;
                }
                relation.SpellTeachingSessions = checked(
                    relation.SpellTeachingSessions + 1);
                relation.SpellTransmissionEstablished |=
                    result == TeachingResultKind.TransmissionEstablished;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(targetKind));
        }

        if (result is TeachingResultKind.TransmissionEstablished
            or TeachingResultKind.TransmissionUpdated)
        {
            var kind = result == TeachingResultKind.TransmissionEstablished
                ? RelationHistoryKind.TeachingTransmissionEstablished
                : RelationHistoryKind.TeachingTransmissionUpdated;
            AddHistory(
                relation,
                tick,
                kind,
                RelationRole.None,
                targetKind,
                knowledgeRef,
                provenanceId);
        }
    }

    public void EndLife(int agent, long tick)
    {
        ValidateAgent(agent);
        var count = _adjacencyCounts[agent];
        var offset = agent * RelationRules.MaximumRelationsPerAgent;
        for (var adjacencyIndex = 0; adjacencyIndex < count; adjacencyIndex++)
        {
            var relation = _relations[_adjacency[offset + adjacencyIndex]];
            if (relation.Roles == RelationRole.None)
            {
                continue;
            }

            var ended = relation.Roles;
            relation.Roles = RelationRole.None;
            relation.MasterAgent = -1;
            AddHistory(relation, tick, RelationHistoryKind.RoleEndedAtLifeBoundary, ended);
        }
    }

    public void StartNewLife(int agent, PersonIdentityId personId)
    {
        ValidateAgent(agent);
        var count = _adjacencyCounts[agent];
        var offset = agent * RelationRules.MaximumRelationsPerAgent;
        for (var adjacencyIndex = 0; adjacencyIndex < count; adjacencyIndex++)
        {
            var relation = _relations[_adjacency[offset + adjacencyIndex]];
            if (relation.LeftAgent == agent)
            {
                relation.LeftPersonId = personId;
            }
            else
            {
                relation.RightPersonId = personId;
            }

            relation.LeftTowardRight = default;
            relation.RightTowardLeft = default;
            relation.CooperativeInteractions = 0;
            relation.JourneyInteractions = 0;
            relation.CombatInteractions = 0;
            relation.TechniqueTeachingSessions = 0;
            relation.SpellTeachingSessions = 0;
            relation.TechniqueTransmissionEstablished = false;
            relation.SpellTransmissionEstablished = false;
            relation.TechniqueKnowledgeRef = default;
            relation.SpellKnowledgeRef = default;
            relation.Roles = RelationRole.None;
            relation.MasterAgent = -1;
        }
    }

    public RelationReadState[] GetForAgent(int agent)
    {
        ValidateAgent(agent);
        var count = _adjacencyCounts[agent];
        if (count == 0)
        {
            return Array.Empty<RelationReadState>();
        }

        var result = new RelationReadState[count];
        var offset = agent * RelationRules.MaximumRelationsPerAgent;
        for (var adjacencyIndex = 0; adjacencyIndex < count; adjacencyIndex++)
        {
            var relation = _relations[_adjacency[offset + adjacencyIndex]];
            var isLeft = relation.LeftAgent == agent;
            result[adjacencyIndex] = new RelationReadState(
                relation.Id,
                isLeft ? relation.RightAgent : relation.LeftAgent,
                isLeft ? relation.RightPersonId : relation.LeftPersonId,
                isLeft ? relation.LeftTowardRight : relation.RightTowardLeft,
                isLeft ? relation.RightTowardLeft : relation.LeftTowardRight,
                relation.Roles,
                relation.MasterAgent,
                relation.History.ToArray());
        }

        Array.Sort(result, static (left, right) => left.OtherAgent.CompareTo(right.OtherAgent));
        return result;
    }

    public RelationStoreCheckpoint CreateCheckpoint() => new(
        _historySequence,
        _relations.Select(relation => new RelationStateCheckpoint(
            relation.Id,
            relation.LeftAgent,
            relation.RightAgent,
            relation.LeftPersonId,
            relation.RightPersonId,
            relation.LeftTowardRight,
            relation.RightTowardLeft,
            relation.Roles,
            relation.MasterAgent,
            relation.CooperativeInteractions,
            relation.JourneyInteractions,
            relation.CombatInteractions,
            relation.History.ToArray(),
            relation.TechniqueTeachingSessions,
            relation.SpellTeachingSessions,
            relation.TechniqueTransmissionEstablished,
            relation.SpellTransmissionEstablished,
            relation.TechniqueKnowledgeRef,
            relation.SpellKnowledgeRef)).ToArray());

    public static SparseRelationStore Restore(
        int population,
        RelationStoreCheckpoint checkpoint)
    {
        if (!IsValidCheckpoint(population, checkpoint))
        {
            throw new InvalidDataException("희소 관계 체크포인트가 잘못되었습니다.");
        }

        var store = new SparseRelationStore(population)
        {
            _historySequence = checkpoint.HistorySequence,
        };
        foreach (var source in checkpoint.Relations)
        {
            var relation = new RelationState
            {
                Id = source.Id,
                LeftAgent = source.LeftAgent,
                RightAgent = source.RightAgent,
                LeftPersonId = source.LeftPersonId,
                RightPersonId = source.RightPersonId,
                LeftTowardRight = source.LeftTowardRight,
                RightTowardLeft = source.RightTowardLeft,
                Roles = source.Roles,
                MasterAgent = source.MasterAgent,
                CooperativeInteractions = source.CooperativeInteractions,
                JourneyInteractions = source.JourneyInteractions,
                CombatInteractions = source.CombatInteractions,
                TechniqueTeachingSessions = source.TechniqueTeachingSessions,
                SpellTeachingSessions = source.SpellTeachingSessions,
                TechniqueTransmissionEstablished = source.TechniqueTransmissionEstablished,
                SpellTransmissionEstablished = source.SpellTransmissionEstablished,
                TechniqueKnowledgeRef = source.TechniqueKnowledgeRef,
                SpellKnowledgeRef = source.SpellKnowledgeRef,
            };
            relation.History.AddRange(source.History);
            var relationIndex = store._relations.Count;
            store._relations.Add(relation);
            store._indexById.Add(relation.Id.Value, relationIndex);
            store.AddAdjacency(relation.LeftAgent, relationIndex);
            store.AddAdjacency(relation.RightAgent, relationIndex);
        }

        return store;
    }

    public static bool IsValidCheckpoint(
        int population,
        RelationStoreCheckpoint? checkpoint,
        long maximumTick = long.MaxValue)
    {
        if (checkpoint is not { HistorySequence: >= 0, Relations: not null } value
            || value.Relations.Length > checked(population * RelationRules.MaximumRelationsPerAgent / 2))
        {
            return false;
        }

        var degree = new int[population];
        var ids = new HashSet<ulong>();
        var historySequences = new HashSet<long>();
        long maximumHistorySequence = 0;
        foreach (var relation in value.Relations)
        {
            if (relation.LeftAgent < 0
                || relation.LeftAgent >= population
                || relation.RightAgent <= relation.LeftAgent
                || relation.RightAgent >= population
                || relation.Id != RelationRules.CreateId(relation.LeftAgent, relation.RightAgent)
                || !ids.Add(relation.Id.Value)
                || ++degree[relation.LeftAgent] > RelationRules.MaximumRelationsPerAgent
                || ++degree[relation.RightAgent] > RelationRules.MaximumRelationsPerAgent
                || relation.LeftPersonId.Value == 0
                || relation.RightPersonId.Value == 0
                || !RelationRules.IsValidMetrics(relation.LeftTowardRight)
                || !RelationRules.IsValidMetrics(relation.RightTowardLeft)
                || (relation.Roles & ~AllRoles) != 0
                || (relation.MasterAgent != -1
                    && relation.MasterAgent != relation.LeftAgent
                    && relation.MasterAgent != relation.RightAgent)
                || ((relation.Roles & RelationRole.MasterDisciple) == 0
                    && relation.MasterAgent != -1)
                || ((relation.Roles & RelationRole.MasterDisciple) != 0
                    && relation.MasterAgent == -1)
                || relation.TechniqueTeachingSessions < 0
                || relation.SpellTeachingSessions < 0
                || relation.TechniqueTeachingSessions == int.MaxValue
                || relation.SpellTeachingSessions == int.MaxValue
                || (relation.TechniqueTransmissionEstablished
                    && relation.TechniqueTeachingSessions == 0)
                || (relation.SpellTransmissionEstablished
                    && relation.SpellTeachingSessions == 0)
                || (relation.TechniqueTeachingSessions > 0
                    && !relation.TechniqueTransmissionEstablished)
                || (relation.SpellTeachingSessions > 0
                    && !relation.SpellTransmissionEstablished)
                || (relation.TechniqueTeachingSessions == 0
                    ? relation.TechniqueKnowledgeRef.Value != 0
                    : relation.TechniqueKnowledgeRef.Value == 0)
                || (relation.SpellTeachingSessions == 0
                    ? relation.SpellKnowledgeRef.Value != 0
                    : relation.SpellKnowledgeRef.Value == 0)
                || relation.History is null)
            {
                return false;
            }

            long previousSequence = 0;
            foreach (var history in relation.History)
            {
                if (history.Sequence <= previousSequence
                    || history.Sequence > value.HistorySequence
                    || !historySequences.Add(history.Sequence)
                    || history.Tick < 0
                    || history.Tick > maximumTick
                    || !Enum.IsDefined(history.Kind)
                    || (history.ChangedRoles & ~AllRoles) != 0
                    || history.LeftPersonId.Value == 0
                    || history.RightPersonId.Value == 0
                    || !IsValidHistoryTeachingTrace(history))
                {
                    return false;
                }

                previousSequence = history.Sequence;
                maximumHistorySequence = Math.Max(maximumHistorySequence, history.Sequence);
            }
        }

        return maximumHistorySequence == value.HistorySequence;
    }

    private const RelationRole AllRoles = RelationRole.MasterDisciple
        | RelationRole.Friendship
        | RelationRole.Romance
        | RelationRole.Companion
        | RelationRole.PersistentRivalry;

    private bool TryGetOrCreate(
        int firstAgent,
        int secondAgent,
        PersonIdentityId firstPersonId,
        PersonIdentityId secondPersonId,
        long tick,
        out RelationState relation)
    {
        ValidateAgent(firstAgent);
        ValidateAgent(secondAgent);
        if (firstAgent == secondAgent)
        {
            relation = null!;
            return false;
        }

        var id = RelationRules.CreateId(firstAgent, secondAgent);
        if (_indexById.TryGetValue(id.Value, out var existingIndex))
        {
            relation = _relations[existingIndex];
            return true;
        }

        if (_adjacencyCounts[firstAgent] >= RelationRules.MaximumRelationsPerAgent
            || _adjacencyCounts[secondAgent] >= RelationRules.MaximumRelationsPerAgent)
        {
            relation = null!;
            return false;
        }

        var firstIsLeft = firstAgent < secondAgent;
        relation = new RelationState
        {
            Id = id,
            LeftAgent = Math.Min(firstAgent, secondAgent),
            RightAgent = Math.Max(firstAgent, secondAgent),
            LeftPersonId = firstIsLeft ? firstPersonId : secondPersonId,
            RightPersonId = firstIsLeft ? secondPersonId : firstPersonId,
        };
        var relationIndex = _relations.Count;
        _relations.Add(relation);
        _indexById.Add(id.Value, relationIndex);
        AddAdjacency(firstAgent, relationIndex);
        AddAdjacency(secondAgent, relationIndex);
        AddHistory(relation, tick, RelationHistoryKind.RelationCreated, RelationRole.None);
        return true;
    }

    private static void SynchronizePersonIdentities(
        RelationState relation,
        int firstAgent,
        PersonIdentityId firstPersonId,
        int secondAgent,
        PersonIdentityId secondPersonId)
    {
        if (relation.LeftAgent == firstAgent)
        {
            relation.LeftPersonId = firstPersonId;
            relation.RightPersonId = secondPersonId;
        }
        else
        {
            relation.LeftPersonId = secondPersonId;
            relation.RightPersonId = firstPersonId;
        }
    }

    private void EvaluateRoles(RelationState relation, long tick)
    {
        var formed = RelationRole.None;
        if (relation.CombatInteractions >= RelationRules.RivalryInteractionThreshold)
        {
            formed |= RelationRole.PersistentRivalry;
        }

        if (relation.CooperativeInteractions >= RelationRules.FriendshipInteractionThreshold
            && relation.LeftTowardRight.FavorDisfavor >= RelationRules.FriendshipAxisThreshold
            && relation.RightTowardLeft.FavorDisfavor >= RelationRules.FriendshipAxisThreshold
            && relation.LeftTowardRight.TrustDistrust >= RelationRules.FriendshipAxisThreshold
            && relation.RightTowardLeft.TrustDistrust >= RelationRules.FriendshipAxisThreshold)
        {
            formed |= RelationRole.Friendship;
        }

        if (relation.JourneyInteractions >= RelationRules.CompanionInteractionThreshold)
        {
            formed |= RelationRole.Companion;
        }

        if ((relation.Roles & RelationRole.Friendship) != 0
            && relation.CooperativeInteractions >= RelationRules.RomanceInteractionThreshold
            && relation.LeftTowardRight.FavorDisfavor >= RelationRules.RomanceAxisThreshold
            && relation.RightTowardLeft.FavorDisfavor >= RelationRules.RomanceAxisThreshold
            && relation.LeftTowardRight.TrustDistrust >= RelationRules.RomanceAxisThreshold
            && relation.RightTowardLeft.TrustDistrust >= RelationRules.RomanceAxisThreshold)
        {
            formed |= RelationRole.Romance;
        }

        formed &= ~relation.Roles;
        if (formed == RelationRole.None)
        {
            return;
        }

        relation.Roles |= formed;
        AddHistory(relation, tick, RelationHistoryKind.RoleFormed, formed);
    }

    private void AddHistory(
        RelationState relation,
        long tick,
        RelationHistoryKind kind,
        RelationRole changedRoles,
        TeachingTargetKind? teachingTarget = null,
        TeachingKnowledgeRef knowledgeRef = default,
        TeachingProvenanceId provenanceId = default)
    {
        relation.History.Add(new RelationHistoryEntry(
            ++_historySequence,
            tick,
            kind,
            changedRoles,
            relation.LeftPersonId,
            relation.RightPersonId,
            teachingTarget,
            knowledgeRef,
            provenanceId));
    }

    private static bool IsValidHistoryTeachingTrace(RelationHistoryEntry history)
    {
        var isTeachingHistory = history.Kind is
            RelationHistoryKind.TeachingTransmissionEstablished
            or RelationHistoryKind.TeachingTransmissionUpdated;
        return isTeachingHistory
            ? history.TeachingTarget is { } target
                && Enum.IsDefined(target)
                && history.KnowledgeRef.Value != 0
                && history.ProvenanceId.Value != 0
            : history.TeachingTarget is null
                && history.KnowledgeRef.Value == 0
                && history.ProvenanceId.Value == 0;
    }

    private void AddAdjacency(int agent, int relationIndex)
    {
        var count = _adjacencyCounts[agent];
        _adjacency[(agent * RelationRules.MaximumRelationsPerAgent) + count] = relationIndex;
        _adjacencyCounts[agent] = checked((byte)(count + 1));
    }

    private static ushort Increment(ushort value) => value == ushort.MaxValue
        ? value
        : checked((ushort)(value + 1));

    private void ValidateAgent(int agent)
    {
        if ((uint)agent >= (uint)_population)
        {
            throw new ArgumentOutOfRangeException(nameof(agent));
        }
    }
}
