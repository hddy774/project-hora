namespace Chronicles.Simulation;

/// <summary>
/// 희소 관계 권위의 checkpoint, 복원 및 손상 검증 작업집합이다.
/// 상호작용 hot path와 저장 검증의 검색·수정 범위를 분리한다.
/// </summary>
internal sealed partial class SparseRelationStore
{
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
            relation.SpellKnowledgeRef,
            relation.LatestInteractionResult)).ToArray(),
        _resultSequence);

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
            _resultSequence = checkpoint.ResultSequence,
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
                LatestInteractionResult = source.LatestInteractionResult,
            };
            relation.History.AddRange(source.History);
            var relationIndex = store._relations.Count;
            store._relations.Add(relation);
            store._indexById.Add(relation.Id.Value, relationIndex);
            store.AddAdjacency(relation.LeftAgent, relationIndex);
            store.AddAdjacency(relation.RightAgent, relationIndex);
            if ((relation.Roles & RelationRole.MasterDisciple) != 0)
            {
                store.IncrementMasterDiscipleCount(relation.LeftAgent);
                store.IncrementMasterDiscipleCount(relation.RightAgent);
            }
        }

        return store;
    }

    public static bool IsValidCheckpoint(
        int population,
        RelationStoreCheckpoint? checkpoint,
        long maximumTick = long.MaxValue)
    {
        if (checkpoint is not { HistorySequence: >= 0, ResultSequence: >= 0, Relations: not null } value
            || value.Relations.Length > checked(population * RelationRules.MaximumRelationsPerAgent / 2))
        {
            return false;
        }

        var degree = new int[population];
        var ids = new HashSet<ulong>();
        var historySequences = new HashSet<long>();
        long maximumHistorySequence = 0;
        long maximumResultSequence = 0;
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

            var latestResult = relation.LatestInteractionResult;
            if (latestResult.Id.Value != 0
                && (latestResult.Sequence <= 0
                    || latestResult.Sequence > value.ResultSequence
                    || latestResult.Id != RelationRules.CreateResultId(
                        relation.Id,
                        latestResult.Sequence)
                    || latestResult.RelationId != relation.Id
                    || latestResult.Tick < 0
                    || latestResult.Tick > maximumTick
                    || !Enum.IsDefined(latestResult.Status)
                    || (latestResult.InteractionKind is { } interactionKind
                        && !Enum.IsDefined(interactionKind))
                    || (latestResult.TeachingResult is { } teachingResult
                        && !Enum.IsDefined(teachingResult))
                    || latestResult.LeftPersonId.Value == 0
                    || latestResult.RightPersonId.Value == 0))
            {
                return false;
            }
            maximumResultSequence = Math.Max(maximumResultSequence, latestResult.Sequence);

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

        return maximumHistorySequence == value.HistorySequence
            && maximumResultSequence == value.ResultSequence;
    }

    private const RelationRole AllRoles = RelationRole.MasterDisciple
        | RelationRole.Friendship
        | RelationRole.Romance
        | RelationRole.Companion
        | RelationRole.PersistentRivalry;

}
