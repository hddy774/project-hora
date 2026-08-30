using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private World(WorldCheckpoint checkpoint)
    {
        ValidateCheckpoint(checkpoint);
        Seed = checkpoint.Seed;
        Tick = new SimulationTick(checkpoint.Tick);
        _random = new DeterministicRandom(checkpoint.RandomState);
        _agents = new AgentStore(checkpoint.Agents.Length);
        _agentByRank = new int[checkpoint.Agents.Length];
        _rankByAgent = new int[checkpoint.Agents.Length];
        _eventSequence = checkpoint.EventSequence;
        _totalBreakthroughs = checkpoint.TotalBreakthroughs;
        _totalChallenges = checkpoint.TotalChallenges;
        _totalCombatsStarted = checkpoint.TotalCombatsStarted;
        _totalCombatsCompleted = checkpoint.TotalCombatsCompleted;
        _activeCombats = checkpoint.ActiveCombats;

        for (var index = 0; index < checkpoint.Agents.Length; index++)
        {
            RestoreAgent(index, checkpoint.Agents[index]);
            _agentByRank[index] = index;
            _agents.RecalculatePower(index);
            ScheduleAgent(index, _agents.NextDecisionTicks[index]);
        }

        foreach (var worldEvent in checkpoint.Events.TakeLast(EventCapacity))
        {
            _events.Enqueue(worldEvent);
        }

        RefreshRankings(recordLeaderChange: false);
    }

    public static World Restore(WorldCheckpoint checkpoint) => new(checkpoint);

    public WorldCheckpoint CreateCheckpoint()
    {
        var agents = new AgentCheckpoint[Population];
        for (var index = 0; index < Population; index++)
        {
            agents[index] = new AgentCheckpoint(
                _agents.Names[index],
                _agents.SpiritualRoots[index],
                _agents.Bones[index],
                _agents.Constitutions[index],
                _agents.Comprehensions[index],
                _agents.RealmStages[index],
                _agents.RealmLayers[index],
                _agents.CultivationProgress[index],
                _agents.TechniqueGrades[index],
                _agents.TechniqueMasteries[index],
                _agents.SpellGrades[index],
                _agents.SpellMasteries[index],
                _agents.CombatExperience[index],
                _agents.Wins[index],
                _agents.Losses[index],
                _agents.Health[index],
                _agents.MaximumHealth[index],
                _agents.Qi[index],
                _agents.MaximumQi[index],
                _agents.Activities[index],
                _agents.NextDecisionTicks[index],
                _agents.TargetRanks[index],
                _agents.Opponents[index],
                _agents.LastOpponents[index],
                _agents.LastCombatTicks[index],
                _agents.CombatStartedTicks[index],
                _agents.CombatActions[index],
                _agents.Guarding[index],
                _agents.ChallengesIssued[index]);
        }

        return new WorldCheckpoint(
            WorldCheckpoint.CurrentSchemaVersion,
            Seed,
            _random.State,
            Tick.Value,
            agents,
            _events.ToArray(),
            _eventSequence,
            _totalBreakthroughs,
            _totalChallenges,
            _totalCombatsStarted,
            _totalCombatsCompleted,
            _activeCombats);
    }

    private void RestoreAgent(int index, AgentCheckpoint checkpoint)
    {
        _agents.Names[index] = checkpoint.Name;
        _agents.SpiritualRoots[index] = checkpoint.SpiritualRoot;
        _agents.Bones[index] = checkpoint.Bone;
        _agents.Constitutions[index] = checkpoint.Constitution;
        _agents.Comprehensions[index] = checkpoint.Comprehension;
        _agents.RealmStages[index] = checkpoint.RealmStage;
        _agents.RealmLayers[index] = checkpoint.RealmLayer;
        _agents.CultivationProgress[index] = checkpoint.CultivationProgress;
        _agents.TechniqueGrades[index] = checkpoint.TechniqueGrade;
        _agents.TechniqueMasteries[index] = checkpoint.TechniqueMastery;
        _agents.SpellGrades[index] = checkpoint.SpellGrade;
        _agents.SpellMasteries[index] = checkpoint.SpellMastery;
        _agents.CombatExperience[index] = checkpoint.CombatExperience;
        _agents.Wins[index] = checkpoint.Wins;
        _agents.Losses[index] = checkpoint.Losses;
        _agents.Health[index] = checkpoint.Health;
        _agents.MaximumHealth[index] = checkpoint.MaximumHealth;
        _agents.Qi[index] = checkpoint.Qi;
        _agents.MaximumQi[index] = checkpoint.MaximumQi;
        _agents.Activities[index] = checkpoint.Activity;
        _agents.NextDecisionTicks[index] = checkpoint.NextDecisionTick;
        _agents.TargetRanks[index] = checkpoint.TargetRank;
        _agents.Opponents[index] = checkpoint.Opponent;
        _agents.LastOpponents[index] = checkpoint.LastOpponent;
        _agents.LastCombatTicks[index] = checkpoint.LastCombatTick;
        _agents.CombatStartedTicks[index] = checkpoint.CombatStartedTick;
        _agents.CombatActions[index] = checkpoint.CombatActions;
        _agents.Guarding[index] = checkpoint.Guarding;
        _agents.ChallengesIssued[index] = checkpoint.ChallengesIssued;
    }

    private static void ValidateCheckpoint(WorldCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (checkpoint.SchemaVersion != WorldCheckpoint.CurrentSchemaVersion)
        {
            throw new InvalidDataException($"지원하지 않는 세계 schema {checkpoint.SchemaVersion}입니다.");
        }

        if (checkpoint.Agents is not { Length: > 0 } || checkpoint.Tick < 0)
        {
            throw new InvalidDataException("세계 체크포인트의 인구 또는 시간이 잘못되었습니다.");
        }

        if (checkpoint.TotalCombatsStarted != checkpoint.TotalCombatsCompleted + checkpoint.ActiveCombats)
        {
            throw new InvalidDataException("세계 체크포인트의 전투 수가 일치하지 않습니다.");
        }

        for (var index = 0; index < checkpoint.Agents.Length; index++)
        {
            var agent = checkpoint.Agents[index];
            if (string.IsNullOrWhiteSpace(agent.Name)
                || agent.RealmLayer is < 1 or > RealmRules.MinorStageCount
                || agent.TargetRank != 1
                || agent.NextDecisionTick <= checkpoint.Tick
                || agent.Opponent < -1
                || agent.Opponent >= checkpoint.Agents.Length)
            {
                throw new InvalidDataException($"Agent {index} 체크포인트가 잘못되었습니다.");
            }

            if (agent.Activity == AgentActivity.Fighting
                && (agent.Opponent < 0 || checkpoint.Agents[agent.Opponent].Opponent != index))
            {
                throw new InvalidDataException($"Agent {index}의 전투 상대가 일치하지 않습니다.");
            }
        }
    }
}
