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
        _agentByRealmRank = new int[checkpoint.Agents.Length];
        _realmRankByAgent = new int[checkpoint.Agents.Length];
        _agentByAptitudeRank = new int[checkpoint.Agents.Length];
        _aptitudeRankByAgent = new int[checkpoint.Agents.Length];
        _eventSequence = checkpoint.EventSequence;
        _chronicleSequence = checkpoint.ChronicleSequence;
        _totalBreakthroughs = checkpoint.TotalBreakthroughs;
        _totalChallenges = checkpoint.TotalChallenges;
        _totalCombatsStarted = checkpoint.TotalCombatsStarted;
        _totalCombatsCompleted = checkpoint.TotalCombatsCompleted;
        _activeCombats = checkpoint.ActiveCombats;
        _totalDeaths = checkpoint.TotalDeaths;
        _totalReincarnations = checkpoint.TotalReincarnations;
        _totalMissionsCompleted = checkpoint.TotalMissionsCompleted;
        _totalTreasuresCrafted = checkpoint.TotalTreasuresCrafted;
        _totalPillsCrafted = checkpoint.TotalPillsCrafted;
        _totalMaterialsSold = checkpoint.TotalMaterialsSold;

        for (var index = 0; index < checkpoint.Agents.Length; index++)
        {
            RestoreAgent(index, checkpoint.Agents[index]);
            _agentByRank[index] = index;
            _agentByRealmRank[index] = index;
            _agentByAptitudeRank[index] = index;
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
                _agents.Minds[index],
                _agents.Souls[index],
                _agents.BirthTicks[index],
                _agents.LifeNumbers[index],
                _agents.BottleneckFailures[index],
                _agents.RealmStages[index],
                _agents.RealmLayers[index],
                _agents.CultivationProgress[index],
                _agents.TechniqueQualities[index],
                _agents.TechniqueMasteries[index],
                _agents.SpellQualities[index],
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
                _agents.ChallengesIssued[index],
                _agents.SpiritStones[index],
                _agents.GrowthPills[index],
                _agents.BreakthroughPills[index],
                _agents.MissionsCompleted[index],
                _agents.ActiveMissionMaterials[index],
                _agents.CopyMaterialInventory(index),
                _agents.CopyEquippedTreasureQualities(index),
                _agents.Chronicles[index].ToArray());
        }

        return new WorldCheckpoint(
            WorldCheckpoint.CurrentSchemaVersion,
            Seed,
            _random.State,
            Tick.Value,
            agents,
            _events.ToArray(),
            _eventSequence,
            _chronicleSequence,
            _totalBreakthroughs,
            _totalChallenges,
            _totalCombatsStarted,
            _totalCombatsCompleted,
            _activeCombats,
            _totalDeaths,
            _totalReincarnations,
            _totalMissionsCompleted,
            _totalTreasuresCrafted,
            _totalPillsCrafted,
            _totalMaterialsSold);
    }

    private void RestoreAgent(int index, AgentCheckpoint checkpoint)
    {
        _agents.Names[index] = checkpoint.Name;
        _agents.SpiritualRoots[index] = checkpoint.SpiritualRoot;
        _agents.Bones[index] = checkpoint.Bone;
        _agents.Constitutions[index] = checkpoint.Constitution;
        _agents.Comprehensions[index] = checkpoint.Comprehension;
        _agents.Minds[index] = checkpoint.Mind;
        _agents.Souls[index] = checkpoint.Soul;
        _agents.BirthTicks[index] = checkpoint.BirthTick;
        _agents.LifeNumbers[index] = checkpoint.LifeNumber;
        _agents.BottleneckFailures[index] = checkpoint.BottleneckFailures;
        _agents.RealmStages[index] = checkpoint.RealmStage;
        _agents.RealmLayers[index] = checkpoint.RealmLayer;
        _agents.CultivationProgress[index] = checkpoint.CultivationProgress;
        _agents.TechniqueQualities[index] = checkpoint.TechniqueQuality;
        _agents.TechniqueMasteries[index] = checkpoint.TechniqueMastery;
        _agents.SpellQualities[index] = checkpoint.SpellQuality;
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
        _agents.SpiritStones[index] = checkpoint.SpiritStones;
        _agents.GrowthPills[index] = checkpoint.GrowthPills;
        _agents.BreakthroughPills[index] = checkpoint.BreakthroughPills;
        _agents.MissionsCompleted[index] = checkpoint.MissionsCompleted;
        _agents.ActiveMissionMaterials[index] = checkpoint.ActiveMissionMaterial;
        _agents.RestoreEconomyState(
            index,
            checkpoint.MaterialInventory,
            checkpoint.EquippedTreasureQualities);
        _agents.Chronicles[index].AddRange(checkpoint.Chronicle);
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

        if (checkpoint.TotalMissionsCompleted < 0
            || checkpoint.TotalTreasuresCrafted < 0
            || checkpoint.TotalPillsCrafted < 0
            || checkpoint.TotalMaterialsSold < 0)
        {
            throw new InvalidDataException("세계 체크포인트의 경제 집계가 잘못되었습니다.");
        }

        for (var index = 0; index < checkpoint.Agents.Length; index++)
        {
            var agent = checkpoint.Agents[index];
            if (string.IsNullOrWhiteSpace(agent.Name)
                || agent.SpiritualRoot > QualityValue.MaximumHundredths
                || agent.Bone > QualityValue.MaximumHundredths
                || agent.Constitution > QualityValue.MaximumHundredths
                || agent.Comprehension > QualityValue.MaximumHundredths
                || agent.Mind > QualityValue.MaximumHundredths
                || agent.Soul > QualityValue.MaximumHundredths
                || agent.TechniqueQuality > QualityValue.MaximumHundredths
                || agent.SpellQuality > QualityValue.MaximumHundredths
                || agent.SpiritStones < 0
                || agent.GrowthPills < 0
                || agent.BreakthroughPills < 0
                || agent.MissionsCompleted < 0
                || (agent.ActiveMissionMaterial != byte.MaxValue
                    && agent.ActiveMissionMaterial >= EconomyRules.MaterialCount)
                || agent.MaterialInventory is not { Length: EconomyRules.MaterialCount }
                || agent.MaterialInventory.Any(count => count < 0)
                || agent.EquippedTreasureQualities is not { Length: EconomyRules.TreasureSlotCount }
                || agent.EquippedTreasureQualities.Any(quality =>
                    quality != EconomyRules.EmptyTreasureQuality
                    && quality > QualityValue.MaximumHundredths)
                || agent.RealmLayer is < 1 or > RealmRules.MinorStageCount
                || agent.BirthTick < 0
                || agent.BirthTick > checkpoint.Tick
                || agent.LifeNumber < 1
                || agent.Chronicle is not { Length: > 0 }
                || agent.TargetRank != 1
                || agent.NextDecisionTick <= checkpoint.Tick
                || agent.Opponent < -1
                || agent.Opponent >= checkpoint.Agents.Length)
            {
                throw new InvalidDataException($"Agent {index} 체크포인트가 잘못되었습니다.");
            }

            if (agent.Chronicle.Any(entry =>
                    entry.LifeNumber < 1
                    || entry.Tick.Value > checkpoint.Tick
                    || entry.Sequence < 1
                    || entry.Sequence > checkpoint.ChronicleSequence))
            {
                throw new InvalidDataException($"Agent {index}의 일대기가 잘못되었습니다.");
            }

            if (agent.Activity == AgentActivity.Fighting
                && (agent.Opponent < 0 || checkpoint.Agents[agent.Opponent].Opponent != index))
            {
                throw new InvalidDataException($"Agent {index}의 전투 상대가 일치하지 않습니다.");
            }
        }
    }
}
