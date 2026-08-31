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
        _market = new CityMarket(checkpoint.Market);
        _agentByRank = new int[checkpoint.Agents.Length];
        _rankByAgent = new int[checkpoint.Agents.Length];
        _agentByRealmRank = new int[checkpoint.Agents.Length];
        _realmRankByAgent = new int[checkpoint.Agents.Length];
        _agentByAptitudeRank = new int[checkpoint.Agents.Length];
        _aptitudeRankByAgent = new int[checkpoint.Agents.Length];
        _eventSequence = checkpoint.EventSequence;
        _chronicleSequence = checkpoint.ChronicleSequence;
        _personalLogSequence = checkpoint.PersonalLogSequence;
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
        _totalMonsterHunts = checkpoint.Map?.TotalMonsterHunts ?? 0;
        _totalMonsterVictories = checkpoint.Map?.TotalMonsterVictories ?? 0;
        _lastRankingRefreshTick = checkpoint.LastRankingRefreshTick;
        _rankingMovementFromTick = checkpoint.RankingMovementFromTick;
        _rankingRisenCount = checkpoint.RankingRisenCount;
        _rankingFallenCount = checkpoint.RankingFallenCount;
        _rankingUnchangedCount = checkpoint.RankingUnchangedCount;
        _rankingTotalAbsoluteMovement = checkpoint.RankingTotalAbsoluteMovement;

        for (var index = 0; index < checkpoint.Agents.Length; index++)
        {
            RestoreAgent(index, checkpoint.Agents[index], checkpoint.SchemaVersion);
            _agentByRank[index] = index;
            _agentByRealmRank[index] = index;
            _agentByAptitudeRank[index] = index;
            _agents.RecalculatePower(index);
        }

        if (checkpoint.SchemaVersion < 10)
        {
            RepairLegacyCombatTurnAuthority();
        }

        for (var index = 0; index < checkpoint.Agents.Length; index++)
        {
            if (_agents.NextDecisionTicks[index] != long.MaxValue)
            {
                ScheduleAgent(index, _agents.NextDecisionTicks[index]);
            }
        }

        foreach (var worldEvent in checkpoint.Events.TakeLast(WorldLogCapacity))
        {
            _events.Enqueue(worldEvent);
        }

        RefreshRankings(recordLeaderChange: false);
    }

    public static World Restore(WorldCheckpoint checkpoint) => new(checkpoint);

    private void RepairLegacyCombatTurnAuthority()
    {
        for (var left = 0; left < Population; left++)
        {
            var right = _agents.Opponents[left];
            if (_agents.Activities[left] != AgentActivity.Fighting
                || right <= left
                || right >= Population
                || _agents.Activities[right] != AgentActivity.Fighting
                || _agents.Opponents[right] != left)
            {
                continue;
            }

            var leftTick = _agents.NextDecisionTicks[left];
            var rightTick = _agents.NextDecisionTicks[right];
            var owner = leftTick < rightTick || (leftTick == rightTick && left < right)
                ? left
                : right;
            var waiting = owner == left ? right : left;
            _agents.CombatTurnOwners[left] = owner;
            _agents.CombatTurnOwners[right] = owner;
            _agents.NextDecisionTicks[owner] = Math.Min(leftTick, rightTick);
            _agents.NextDecisionTicks[waiting] = long.MaxValue;
        }
    }

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
                _agents.CultivationStates[index],
                _agents.CultivationBottleneckCauses[index],
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
                _agents.LastActionMonths[index],
                _agents.LastActionTicks[index],
                _agents.ActionsTaken[index],
                _agents.TargetRanks[index],
                _agents.Opponents[index],
                _agents.LastOpponents[index],
                _agents.LastCombatTicks[index],
                _agents.CombatStartedTicks[index],
                checked((byte)Math.Min(byte.MaxValue, _agents.CombatActions[index])),
                _agents.Guarding[index],
                _agents.ChallengesIssued[index],
                _agents.SpiritStones[index],
                _agents.GrowthPills[index],
                _agents.BreakthroughPills[index],
                _agents.MissionsCompleted[index],
                _agents.ActiveMissionMaterials[index],
                _agents.CopyMaterialInventory(index),
                _agents.CopyEquippedTreasureQualities(index),
                _agents.PersonalLogs[index].ToArray(),
                _agents.Chronicles[index].ToArray(),
                new CultivationCheckpoint(
                    _agents.PendingCultivationWork[index],
                    _agents.LastCultivationWorkBefore[index],
                    _agents.LastCultivationWorkGain[index],
                    _agents.FloorNineCompleted[index],
                    _agents.GreatPerfectionAchieved[index],
                    _agents.EntryGatePassMasks[index],
                    _agents.FoundationRoutes[index],
                    _agents.CopyCultivationComponentValues(index),
                    _agents.CopyCultivationComponentLimits(index),
                    _agents.CopyFormationValues(index),
                    _agents.CultivationTraces[index],
                    _agents.CopyEntryGateTemperings(index)),
                new SkillProgressCheckpoint(
                    _agents.TechniqueTrainingExperience[index],
                    _agents.TechniqueUnderstanding[index],
                    _agents.SpellTrainingExperience[index],
                    _agents.SpellUnderstanding[index]),
                new CombatAuthorityCheckpoint(
                    _agents.CombatActions[index],
                    _agents.CombatTurnOwners[index],
                    _agents.InjuryPoints[index]),
                new MapAgentCheckpoint(
                    _agents.LocationIds[index],
                    _agents.TravelDestinationIds[index],
                    _agents.MonsterHunts[index],
                    _agents.MonsterVictories[index]),
                new CombatProgressCheckpoint(
                    _agents.CombatUnderstanding[index]));
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
            _personalLogSequence,
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
            _totalMaterialsSold,
            _lastRankingRefreshTick,
            _rankingMovementFromTick,
            _rankingRisenCount,
            _rankingFallenCount,
            _rankingUnchangedCount,
            _rankingTotalAbsoluteMovement,
            _market.CreateCheckpoint(),
            new WorldMapCheckpoint(
                _totalMonsterHunts,
                _totalMonsterVictories));
    }

    private void RestoreAgent(int index, AgentCheckpoint checkpoint, int schemaVersion)
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
        var migratedFloor = schemaVersion == 7
            ? MigrateLegacyFloor(checkpoint.RealmLayer)
            : checkpoint.RealmLayer;
        _agents.RealmLayers[index] = migratedFloor;
        _agents.CultivationProgress[index] = schemaVersion == 7
            ? MigrateLegacyCultivationWork(
                checkpoint.RealmStage,
                checkpoint.RealmLayer,
                migratedFloor,
                checkpoint.CultivationProgress)
            : checkpoint.CultivationProgress;
        _agents.CultivationStates[index] = checkpoint.CultivationState;
        _agents.CultivationBottleneckCauses[index] = checkpoint.CultivationBottleneckCause;
        var cultivation = checkpoint.Cultivation;
        if (cultivation is { } current)
        {
            _agents.PendingCultivationWork[index] = current.PendingCarryoverWork;
            _agents.LastCultivationWorkBefore[index] = current.LastWorkBefore;
            _agents.LastCultivationWorkGain[index] = current.LastWorkGain;
            _agents.FloorNineCompleted[index] = current.FloorNineCompleted;
            _agents.GreatPerfectionAchieved[index] = current.GreatPerfectionAchieved;
            _agents.EntryGatePassMasks[index] = current.EntryGatePassMask;
            _agents.FoundationRoutes[index] = current.FoundationRoute;
            _agents.RestoreCultivationArrays(
                index,
                current.ComponentValues,
                current.ComponentLimits,
                current.FormationValues,
                current.EntryGateTempering);
            _agents.CultivationTraces[index] = current.LatestTrace;
        }
        else
        {
            var legacyCompleted = checkpoint.CultivationState == CultivationState.Completed;
            _agents.FloorNineCompleted[index] = legacyCompleted;
            _agents.GreatPerfectionAchieved[index] = legacyCompleted;
            _agents.EntryGatePassMasks[index] = 0b111;
            _agents.CultivationTraces[index] = CultivationTraceState.Empty;
            InitializeRealmComponents(index);
        }
        _agents.TechniqueQualities[index] = checkpoint.TechniqueQuality;
        _agents.TechniqueMasteries[index] = schemaVersion < 9
            ? checked((ushort)Math.Min(10_000, checkpoint.TechniqueMastery * 10))
            : checkpoint.TechniqueMastery;
        _agents.SpellQualities[index] = checkpoint.SpellQuality;
        _agents.SpellMasteries[index] = schemaVersion < 9
            ? checked((ushort)Math.Min(10_000, checkpoint.SpellMastery * 10))
            : checkpoint.SpellMastery;
        if (checkpoint.SkillProgress is { } skillProgress)
        {
            _agents.TechniqueTrainingExperience[index] = skillProgress.TechniqueExperience;
            _agents.TechniqueUnderstanding[index] = skillProgress.TechniqueUnderstanding;
            _agents.SpellTrainingExperience[index] = skillProgress.SpellExperience;
            _agents.SpellUnderstanding[index] = skillProgress.SpellUnderstanding;
        }
        else
        {
            _agents.TechniqueTrainingExperience[index] = SkillProgressionRules.GetRequiredExperience(
                _agents.TechniqueMasteries[index]);
            _agents.TechniqueUnderstanding[index] = 0;
            _agents.SpellTrainingExperience[index] = SkillProgressionRules.GetRequiredExperience(
                _agents.SpellMasteries[index]);
            _agents.SpellUnderstanding[index] = 0;
        }
        _agents.CombatExperience[index] = checkpoint.CombatExperience;
        _agents.CombatUnderstanding[index] = schemaVersion >= 13
            ? checkpoint.CombatProgress!.Value.UnderstandingHundredths
            : (ushort)0;
        _agents.Wins[index] = checkpoint.Wins;
        _agents.Losses[index] = checkpoint.Losses;
        _agents.Health[index] = checkpoint.Health;
        _agents.MaximumHealth[index] = checkpoint.MaximumHealth;
        _agents.Qi[index] = checkpoint.Qi;
        _agents.MaximumQi[index] = checkpoint.MaximumQi;
        _agents.Activities[index] = checkpoint.Activity;
        _agents.NextDecisionTicks[index] = checkpoint.NextDecisionTick;
        _agents.LastActionMonths[index] = checkpoint.LastActionMonth;
        _agents.LastActionTicks[index] = checkpoint.LastActionTick;
        _agents.ActionsTaken[index] = checkpoint.ActionsTaken;
        _agents.TargetRanks[index] = checkpoint.TargetRank;
        _agents.Opponents[index] = checkpoint.Opponent;
        _agents.LastOpponents[index] = checkpoint.LastOpponent;
        _agents.LastCombatTicks[index] = checkpoint.LastCombatTick;
        _agents.CombatStartedTicks[index] = checkpoint.CombatStartedTick;
        if (checkpoint.CombatAuthority is { } combatAuthority)
        {
            _agents.CombatActions[index] = combatAuthority.ActionCount;
            _agents.CombatTurnOwners[index] = combatAuthority.TurnOwner;
            _agents.InjuryPoints[index] = combatAuthority.InjuryPoints;
        }
        else
        {
            _agents.CombatActions[index] = checkpoint.CombatActions;
            _agents.CombatTurnOwners[index] = checkpoint.Activity == AgentActivity.Fighting
                ? index
                : -1;
            _agents.InjuryPoints[index] = 0;
        }
        _agents.Guarding[index] = checkpoint.Guarding;
        _agents.ChallengesIssued[index] = checkpoint.ChallengesIssued;
        _agents.SpiritStones[index] = checkpoint.SpiritStones;
        _agents.GrowthPills[index] = checkpoint.GrowthPills;
        _agents.BreakthroughPills[index] = checkpoint.BreakthroughPills;
        _agents.MissionsCompleted[index] = checkpoint.MissionsCompleted;
        _agents.ActiveMissionMaterials[index] = checkpoint.ActiveMissionMaterial;
        if (checkpoint.Map is { } map)
        {
            _agents.LocationIds[index] = map.LocationId;
            _agents.TravelDestinationIds[index] = map.TravelDestinationId;
            _agents.MonsterHunts[index] = map.MonsterHunts;
            _agents.MonsterVictories[index] = map.MonsterVictories;
        }
        else
        {
            _agents.LocationIds[index] = WorldMapRules.CityLocationId;
            _agents.TravelDestinationIds[index] = WorldMapRules.CityLocationId;
            _agents.MonsterHunts[index] = 0;
            _agents.MonsterVictories[index] = 0;
        }
        _agents.RestoreEconomyState(
            index,
            checkpoint.MaterialInventory,
            checkpoint.EquippedTreasureQualities);
        _agents.PersonalLogs[index].AddRange(checkpoint.PersonalLog);
        _agents.Chronicles[index].AddRange(checkpoint.Chronicle);
    }

    private static void ValidateCheckpoint(WorldCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        if (checkpoint.SchemaVersion is < 7 or > WorldCheckpoint.CurrentSchemaVersion)
        {
            throw new InvalidDataException($"지원하지 않는 세계 schema {checkpoint.SchemaVersion}입니다.");
        }

        if (checkpoint.Agents is not { Length: > 0 } || checkpoint.Tick < 0)
        {
            throw new InvalidDataException("세계 체크포인트의 인구 또는 시간이 잘못되었습니다.");
        }

        if (checkpoint.Events is null
            || checkpoint.Events.Length > WorldLogCapacity
            || checkpoint.EventSequence < 0
            || checkpoint.ChronicleSequence < 0
            || checkpoint.PersonalLogSequence < 0
            || checkpoint.Events.Any(entry =>
                entry.Sequence < 1
                || entry.Sequence > checkpoint.EventSequence
                || entry.Tick.Value > checkpoint.Tick
                || entry.PrimaryAgent.Value < 0
                || entry.PrimaryAgent.Value >= checkpoint.Agents.Length
                || (entry.SecondaryAgent is { } secondary
                    && (secondary.Value < 0 || secondary.Value >= checkpoint.Agents.Length))))
        {
            throw new InvalidDataException("세계 로그 또는 로그 sequence가 잘못되었습니다.");
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

        if (checkpoint.SchemaVersion >= 11
            && (checkpoint.Map is not { } map
                || map.TotalMonsterHunts < 0
                || map.TotalMonsterVictories < 0
                || map.TotalMonsterVictories > map.TotalMonsterHunts))
        {
            throw new InvalidDataException("세계 체크포인트의 지도 집계가 잘못되었습니다.");
        }

        if (checkpoint.LastRankingRefreshTick < 0
            || checkpoint.LastRankingRefreshTick > checkpoint.Tick
            || checkpoint.RankingMovementFromTick < 0
            || checkpoint.RankingMovementFromTick > checkpoint.LastRankingRefreshTick
            || checkpoint.RankingRisenCount < 0
            || checkpoint.RankingFallenCount < 0
            || checkpoint.RankingUnchangedCount < 0
            || checkpoint.RankingRisenCount
                + checkpoint.RankingFallenCount
                + checkpoint.RankingUnchangedCount > checkpoint.Agents.Length
            || checkpoint.RankingTotalAbsoluteMovement < 0)
        {
            throw new InvalidDataException("세계 체크포인트의 순위 변동 통계가 잘못되었습니다.");
        }

        if (checkpoint.Market is null
            || checkpoint.Market.TreasurySpiritStones < 0
            || checkpoint.Market.PurchaseTransactions < 0
            || checkpoint.Market.SaleTransactions < 0
            || checkpoint.Market.MissionDeliveries < 0
            || checkpoint.Market.MaterialsProcessed < 0
            || checkpoint.Market.PillsProduced < 0
            || checkpoint.Market.TreasuresProduced < 0
            || checkpoint.Market.TechniqueBooksProduced < 0
            || checkpoint.Market.SpellBooksProduced < 0
            || checkpoint.Market.Stock is not { Length: EconomyRules.MarketGoodCount }
            || checkpoint.Market.Prices is not { Length: EconomyRules.MarketGoodCount }
            || checkpoint.Market.Supply is not { Length: EconomyRules.MarketGoodCount }
            || checkpoint.Market.Demand is not { Length: EconomyRules.MarketGoodCount }
            || checkpoint.Market.Stock.Any(value => value < 0)
            || checkpoint.Market.Prices.Any(value => value < 1)
            || checkpoint.Market.Supply.Any(value => value < 0)
            || checkpoint.Market.Demand.Any(value => value < 0)
            || checkpoint.Market.TreasureQualityHundredths < 0
            || checkpoint.Market.TreasureQualityHundredths
                > (long)checkpoint.Market.Stock[EconomyRules.TreasureGoodId]
                    * QualityValue.MaximumHundredths)
        {
            throw new InvalidDataException("세계 체크포인트의 도시 시장 상태가 잘못되었습니다.");
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
                || (checkpoint.SchemaVersion >= 9
                    && (agent.TechniqueMastery > SkillProgressionRules.MaximumHundredths
                        || agent.SpellMastery > SkillProgressionRules.MaximumHundredths))
                || agent.CombatExperience < 0
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
                || !RealmRules.IsValidMinorStage(agent.RealmStage, agent.RealmLayer)
                || !Enum.IsDefined(agent.CultivationState)
                || !Enum.IsDefined(agent.CultivationBottleneckCause)
                || agent.CultivationProgress < 0
                || agent.CultivationProgress > GetCheckpointCultivationRequirement(checkpoint.SchemaVersion, agent)
                || (agent.CultivationState == CultivationState.Bottleneck
                    && agent.CultivationBottleneckCause == CultivationBottleneckCause.None)
                || (agent.CultivationState != CultivationState.Bottleneck
                    && agent.CultivationBottleneckCause != CultivationBottleneckCause.None)
                || (agent.CultivationState == CultivationState.Completed
                    && !(agent.RealmStage == RealmStage.HalfImmortal
                        && (checkpoint.SchemaVersion == 7
                            ? agent.RealmLayer == 4
                            : agent.RealmLayer == RealmRules.MinorStageCount)))
                || agent.BirthTick < 0
                || agent.BirthTick > checkpoint.Tick
                || agent.LifeNumber < 1
                || agent.PersonalLog is null
                || agent.PersonalLog.Length > PersonalLogCapacity
                || agent.Chronicle is not { Length: > 0 }
                || agent.TargetRank != 1
                || agent.NextDecisionTick <= checkpoint.Tick
                || agent.LastActionMonth < -1
                || agent.LastActionMonth > GameCalendar.GetMonthIndex(new SimulationTick(checkpoint.Tick))
                || agent.LastActionTick < 0
                || agent.LastActionTick > checkpoint.Tick
                || agent.ActionsTaken < 0
                || (agent.LastActionMonth < 0 && (agent.LastActionTick != 0 || agent.ActionsTaken != 0))
                || (agent.LastActionMonth >= 0
                    && GameCalendar.GetMonthIndex(new SimulationTick(agent.LastActionTick)) != agent.LastActionMonth)
                || GameCalendar.GetMonthIndex(new SimulationTick(agent.NextDecisionTick)) <= agent.LastActionMonth
                || agent.Opponent < -1
                || agent.Opponent >= checkpoint.Agents.Length)
            {
                throw new InvalidDataException($"Agent {index} 체크포인트가 잘못되었습니다.");
            }

            if (checkpoint.SchemaVersion >= 8
                && !IsValidCultivationCheckpoint(agent.Cultivation, checkpoint.SchemaVersion))
            {
                throw new InvalidDataException($"Agent {index}의 수행 V2 체크포인트가 잘못되었습니다.");
            }

            if (checkpoint.SchemaVersion >= 9 && !IsValidSkillProgress(agent.SkillProgress))
            {
                throw new InvalidDataException($"Agent {index}의 공법·법술 성장 체크포인트가 잘못되었습니다.");
            }

            if (checkpoint.SchemaVersion >= 10
                && !IsValidCombatAuthority(agent.CombatAuthority, checkpoint.Agents.Length))
            {
                throw new InvalidDataException($"Agent {index}의 전투 권위 체크포인트가 잘못되었습니다.");
            }

            if (checkpoint.SchemaVersion >= 11 && !IsValidMapAgent(agent.Map))
            {
                throw new InvalidDataException($"Agent {index}의 지도 체크포인트가 잘못되었습니다.");
            }

            if (checkpoint.SchemaVersion >= 13 && !IsValidCombatProgress(agent.CombatProgress))
            {
                throw new InvalidDataException($"Agent {index}의 전투 성장 체크포인트가 잘못되었습니다.");
            }

            if (agent.PersonalLog.Any(entry =>
                    entry.Sequence < 1
                    || entry.Sequence > checkpoint.PersonalLogSequence
                    || entry.Tick.Value > checkpoint.Tick
                    || entry.LifeNumber < 1
                    || entry.LifeNumber > agent.LifeNumber
                    || !Enum.IsDefined(entry.Kind)
                    || (entry.Activity is { } activity && !Enum.IsDefined(activity))
                    || (entry.WorldEventKind is { } eventKind && !Enum.IsDefined(eventKind))
                    || (entry.ChronicleEventKind is { } chronicleKind && !Enum.IsDefined(chronicleKind))
                    || (entry.Kind == PersonalLogKind.ActionCompleted
                        && (entry.Activity is null
                            || entry.WorldEventKind is not null
                            || entry.ChronicleEventKind is not null))
                    || (entry.Kind == PersonalLogKind.WorldEvent
                        && (entry.Activity is not null
                            || entry.WorldEventKind is null
                            || entry.ChronicleEventKind is not null))
                    || (entry.Kind == PersonalLogKind.Chronicle
                        && (entry.Activity is not null
                            || entry.WorldEventKind is not null
                            || entry.ChronicleEventKind is null))
                    || (entry.RelatedAgent is { } related
                        && (related.Value < 0 || related.Value >= checkpoint.Agents.Length))))
            {
                throw new InvalidDataException($"Agent {index}의 개인 로그가 잘못되었습니다.");
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

    private static byte MigrateLegacyFloor(byte legacyFloor) => legacyFloor switch
    {
        1 => 1,
        2 => 4,
        3 => 7,
        4 => 9,
        _ => throw new InvalidDataException("기존 소경지 값이 잘못되었습니다."),
    };

    private static int MigrateLegacyCultivationWork(
        RealmStage realm,
        byte legacyFloor,
        byte destinationFloor,
        int progress)
    {
        var oldLifespanTicks = checked(RealmRules.GetProfile(realm).MaximumLifespanYears * GameCalendar.TicksPerYear);
        var oldRequirement = checked((int)((oldLifespanTicks + 6) / 12));
        var newRequirement = RealmRules.GetMinorStageRequiredWork(realm, destinationFloor);
        return oldRequirement == 0
            ? 0
            : checked((int)Math.Min(newRequirement, (long)progress * newRequirement / oldRequirement));
    }

    private static int GetCheckpointCultivationRequirement(int schemaVersion, AgentCheckpoint agent)
    {
        if (schemaVersion == 7)
        {
            var lifespanTicks = checked(RealmRules.GetProfile(agent.RealmStage).MaximumLifespanYears * GameCalendar.TicksPerYear);
            return checked((int)((lifespanTicks + 6) / 12));
        }

        return agent.CultivationState is CultivationState.PreCultivation or CultivationState.EntryTraining
            || (agent.CultivationState == CultivationState.Bottleneck
                && agent.CultivationBottleneckCause == CultivationBottleneckCause.EntryGate)
            ? CultivationRules.RequiredEntryWork
            : RealmRules.GetMinorStageRequiredWork(agent.RealmStage, agent.RealmLayer);
    }

    private static bool IsValidCultivationCheckpoint(
        CultivationCheckpoint? checkpoint,
        int schemaVersion)
    {
        if (checkpoint is not { } value
            || value.PendingCarryoverWork < 0
            || value.LastWorkBefore < 0
            || value.LastWorkGain < 0
            || value.EntryGatePassMask > 0b111
            || !Enum.IsDefined(value.FoundationRoute)
            || value.ComponentValues is not { Length: CultivationRules.RealmComponentSlotCount }
            || value.ComponentLimits is not { Length: CultivationRules.RealmComponentSlotCount }
            || value.FormationValues is not { Length: CultivationRules.FormationSlotCount }
            || (schemaVersion >= 12
                && value.EntryGateTempering is not { Length: CultivationRules.EntryGateSlotCount })
            || (value.EntryGateTempering is { } tempering
                && (tempering.Length != CultivationRules.EntryGateSlotCount
                    || tempering.Any(item => item > QualityValue.MaximumHundredths))))
        {
            return false;
        }

        for (var component = 0; component < CultivationRules.RealmComponentSlotCount; component++)
        {
            if (value.ComponentValues[component] > value.ComponentLimits[component]
                || value.ComponentLimits[component] > QualityValue.MaximumHundredths)
            {
                return false;
            }
        }

        return value.FormationValues.All(item => item <= QualityValue.MaximumHundredths)
            && value.LatestTrace.FormulaRevision is >= 0 and <= CultivationRules.FormulaRevision
            && Enum.IsDefined(value.LatestTrace.AttemptClass)
            && Enum.IsDefined(value.LatestTrace.Outcome)
            && Enum.IsDefined(value.LatestTrace.Blocker);
    }

    private static bool IsValidSkillProgress(SkillProgressCheckpoint? checkpoint) => checkpoint is { } value
        && value.TechniqueExperience is >= 0 and <= SkillProgressionRules.MaximumExperience
        && value.SpellExperience is >= 0 and <= SkillProgressionRules.MaximumExperience
        && value.TechniqueUnderstanding <= SkillProgressionRules.MaximumHundredths
        && value.SpellUnderstanding <= SkillProgressionRules.MaximumHundredths;

    private static bool IsValidCombatAuthority(CombatAuthorityCheckpoint? checkpoint, int population) =>
        checkpoint is { } value
        && value.ActionCount >= 0
        && value.TurnOwner >= -1
        && value.TurnOwner < population
        && value.InjuryPoints <= CombatInjuryRules.MaximumInjuryPoints;

    private static bool IsValidMapAgent(MapAgentCheckpoint? checkpoint) => checkpoint is { } value
        && value.LocationId < WorldMapRules.LocationCount
        && value.TravelDestinationId < WorldMapRules.LocationCount
        && value.MonsterHunts >= 0
        && value.MonsterVictories >= 0
        && value.MonsterVictories <= value.MonsterHunts;

    private static bool IsValidCombatProgress(CombatProgressCheckpoint? checkpoint) =>
        checkpoint is { } value
        && value.UnderstandingHundredths <= CombatProgressionRules.MaximumHundredths;
}
