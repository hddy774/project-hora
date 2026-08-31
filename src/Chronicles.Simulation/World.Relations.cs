using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private void ObserveSocialInteraction(int agent, AgentActivity completedActivity)
    {
        if (_agents.LifeStates[agent] != PersonLifeState.Living
            || !TryGetRelationInteractionKind(completedActivity, out var kind)
            || Population < 2
            || _agents.ActionsTaken[agent] % RelationRules.SocialInteractionActionInterval != 0)
        {
            return;
        }

        var existingCount = _relations.GetRelationCount(agent);
        if (existingCount > 0)
        {
            var existingStart = _agents.ActionsTaken[agent] % existingCount;
            for (var offset = 0; offset < existingCount; offset++)
            {
                var adjacencyIndex = (existingStart + offset) % existingCount;
                var existing = _relations.GetRelatedAgent(agent, adjacencyIndex);
                if (_agents.LifeStates[existing] != PersonLifeState.Living
                    || _agents.LocationIds[existing] != _agents.LocationIds[agent]
                    || !CanShareInteraction(kind, _agents.Activities[existing]))
                {
                    continue;
                }

                _relations.ApplyMutualInteraction(
                    agent,
                    existing,
                    _agents.PersonIdentityIds[agent],
                    _agents.PersonIdentityIds[existing],
                    kind,
                    Tick.Value);
                return;
            }
        }

        if (existingCount >= RelationRules.MaximumRelationsPerAgent)
        {
            return;
        }

        var candidateRange = Population - 1;
        var start = (int)(((long)agent * 31L
            + ((long)_agents.ActionsTaken[agent] * 17L)) % candidateRange);
        for (var probe = 0; probe < RelationRules.CandidateProbeCount; probe++)
        {
            var distance = 1 + ((start + (probe * 7_919)) % candidateRange);
            var candidate = (agent + distance) % Population;
            if (_agents.LifeStates[candidate] != PersonLifeState.Living
                || _agents.LocationIds[candidate] != _agents.LocationIds[agent]
                || !CanShareInteraction(kind, _agents.Activities[candidate]))
            {
                continue;
            }

            if (_relations.ApplyMutualInteraction(
                    agent,
                    candidate,
                    _agents.PersonIdentityIds[agent],
                    _agents.PersonIdentityIds[candidate],
                    kind,
                    Tick.Value))
            {
                return;
            }
        }
    }

    private RelationSnapshot[] CreateRelationSnapshots(int agent)
    {
        var states = _relations.GetForAgent(agent);
        if (states.Length == 0)
        {
            return Array.Empty<RelationSnapshot>();
        }

        var snapshots = new RelationSnapshot[states.Length];
        for (var index = 0; index < states.Length; index++)
        {
            var state = states[index];
            snapshots[index] = new RelationSnapshot(
                state.Id,
                new AgentId(state.OtherAgent),
                _agents.Names[state.OtherAgent],
                state.OtherPersonId,
                state.TowardOther,
                state.OtherTowardSelf,
                state.Roles,
                IsMaster: state.MasterAgent == agent,
                IsDisciple: state.MasterAgent == state.OtherAgent,
                state.History);
        }

        return snapshots;
    }

    private static bool TryGetRelationInteractionKind(
        AgentActivity activity,
        out RelationInteractionKind kind)
    {
        switch (activity)
        {
            case AgentActivity.Cultivating:
            case AgentActivity.TrainingTechnique:
            case AgentActivity.TrainingSpell:
                kind = RelationInteractionKind.SharedCultivation;
                return true;
            case AgentActivity.DoingCityMission:
            case AgentActivity.MiningSpiritStones:
                kind = RelationInteractionKind.SharedMission;
                return true;
            case AgentActivity.Traveling:
                kind = RelationInteractionKind.SharedJourney;
                return true;
            case AgentActivity.HuntingMonster:
                kind = RelationInteractionKind.SharedHunt;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static bool CanShareInteraction(
        RelationInteractionKind kind,
        AgentActivity candidateActivity) => kind switch
    {
        RelationInteractionKind.SharedCultivation => candidateActivity is
            AgentActivity.Cultivating
            or AgentActivity.TrainingTechnique
            or AgentActivity.TrainingSpell,
        RelationInteractionKind.SharedMission => candidateActivity is
            AgentActivity.DoingCityMission
            or AgentActivity.MiningSpiritStones,
        RelationInteractionKind.SharedJourney => candidateActivity == AgentActivity.Traveling,
        RelationInteractionKind.SharedHunt => candidateActivity == AgentActivity.HuntingMonster,
        _ => false,
    };
}
