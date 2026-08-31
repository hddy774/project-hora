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
        _market = new CityMarket(checkpoint.Market, checkpoint.SchemaVersion);
        _agentByRank = new int[checkpoint.Agents.Length];
        _rankByAgent = new int[checkpoint.Agents.Length];
        _agentByRealmRank = new int[checkpoint.Agents.Length];
        _realmRankByAgent = new int[checkpoint.Agents.Length];
        _agentByAptitudeRank = new int[checkpoint.Agents.Length];
        _aptitudeRankByAgent = new int[checkpoint.Agents.Length];
        _agentByWealthRank = new int[checkpoint.Agents.Length];
        _wealthRankByAgent = new int[checkpoint.Agents.Length];
        _worldChronicle = new WorldCenturyChronicle(
            checkpoint.Agents.Length,
            GameCalendar.GetYear(Tick));
        _worldChronicle.EntryRecorded += WorldChronicle_EntryRecorded;
        _monsterPopulations = checkpoint.Map?.MonsterPopulations is
            { Length: WorldMapRules.LocationCount * WorldMapRules.MonstersPerWildBiome } populations
                ? populations.ToArray()
                : CreateInitialMonsterPopulations();
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
        _lastMonsterRegenerationMonth = checkpoint.Map?.LastRegenerationMonth ?? 0;
        _lastRankingRefreshTick = checkpoint.LastRankingRefreshTick;
        _rankingMovementFromTick = checkpoint.RankingMovementFromTick;
        _rankingRisenCount = checkpoint.RankingRisenCount;
        _rankingFallenCount = checkpoint.RankingFallenCount;
        _rankingUnchangedCount = checkpoint.RankingUnchangedCount;
        _rankingTotalAbsoluteMovement = checkpoint.RankingTotalAbsoluteMovement;
        if (checkpoint.EliteCombatChangedCounts is { Length: 4 } eliteCombat)
        {
            Array.Copy(eliteCombat, _eliteCombatChangedCounts, 4);
        }
        if (checkpoint.EliteRealmChangedCounts is { Length: 4 } eliteRealm)
        {
            Array.Copy(eliteRealm, _eliteRealmChangedCounts, 4);
        }

        for (var index = 0; index < checkpoint.Agents.Length; index++)
        {
            RestoreAgent(index, checkpoint.Agents[index], checkpoint.SchemaVersion);
            _agentByRank[index] = index;
            _agentByRealmRank[index] = index;
            _agentByAptitudeRank[index] = index;
            _agentByWealthRank[index] = index;
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
            _events.Enqueue(checkpoint.SchemaVersion >= 15
                ? worldEvent
                : worldEvent with
                {
                    PrimaryName = _agents.Names[worldEvent.PrimaryAgent.Value],
                    SecondaryName = worldEvent.SecondaryAgent is { } secondary
                        ? _agents.Names[secondary.Value]
                        : null,
                });
        }

        RefreshRankings(recordLeaderChange: false);
        if (checkpoint.SchemaVersion >= 15 && checkpoint.RankingHistory is { } rankingHistory)
        {
            _rankingHistory.Restore(rankingHistory);
        }
        if (checkpoint.SchemaVersion >= 17 && checkpoint.CenturyChronicle is { } centuryChronicle)
        {
            _worldChronicle = WorldCenturyChronicle.Restore(Population, centuryChronicle);
            _worldChronicle.EntryRecorded += WorldChronicle_EntryRecorded;
        }
        else
        {
            _worldChronicle.RestoreLegacyRiverResidents(
                _agents.Activities.Count(activity => activity == AgentActivity.Reincarnating));
        }
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
                _totalMonsterVictories,
                _monsterPopulations.ToArray(),
                _lastMonsterRegenerationMonth),
            _eliteCombatChangedCounts.ToArray(),
            _eliteRealmChangedCounts.ToArray(),
            Array.Empty<WorldEvent>(),
            _rankingHistory.CreateCheckpoint(),
            _worldChronicle.CreateCheckpoint());
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
        _agents.ActiveMissionMaterials[index] = schemaVersion >= 14
            || checkpoint.ActiveMissionMaterial == byte.MaxValue
                ? checkpoint.ActiveMissionMaterial
                : checkpoint.ActiveMissionMaterial < 10
                    || checkpoint.ActiveMissionMaterial is >= 20 and < 25
                        ? (byte)MaterialId.TreasureMaterial
                        : (byte)MaterialId.PillMaterial;
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
        _agents.PersonalLogs[index].AddRange(checkpoint.PersonalLog.Where(entry =>
            entry.ChronicleEventKind is not ChronicleEventKind.TechniqueEnlightenment
                and not ChronicleEventKind.SpellEnlightenment));
        _agents.Chronicles[index].AddRange(checkpoint.Chronicle.Where(entry =>
            entry.Kind is not ChronicleEventKind.TechniqueEnlightenment
                and not ChronicleEventKind.SpellEnlightenment));
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
                || map.TotalMonsterVictories > map.TotalMonsterHunts
                || (checkpoint.SchemaVersion >= 14
                    && (map.MonsterPopulations is not
                            { Length: WorldMapRules.LocationCount * WorldMapRules.MonstersPerWildBiome }
                        || map.MonsterPopulations.Any(value =>
                            value is < 0 or > WorldMapRules.MaximumMonsterPopulation)
                        || map.LastRegenerationMonth < 0
                        || map.LastRegenerationMonth > GameCalendar.GetMonthIndex(
                            new SimulationTick(checkpoint.Tick))))))
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

        if (checkpoint.SchemaVersion >= 14
            && (checkpoint.EliteCombatChangedCounts is not { Length: 4 }
                || checkpoint.EliteRealmChangedCounts is not { Length: 4 }
                || checkpoint.EliteCombatChangedCounts.Any(value => value is < 0 or > 10)
                || checkpoint.EliteRealmChangedCounts.Any(value => value is < 0 or > 10)))
        {
            throw new InvalidDataException("상위권 순위 변동 통계가 잘못되었습니다.");
        }

        if (checkpoint.SchemaVersion >= 15
            && (checkpoint.Events.Any(entry => string.IsNullOrWhiteSpace(entry.PrimaryName))
                || !IsValidRankingHistory(
                    checkpoint.RankingHistory,
                    checkpoint.Tick,
                    checkpoint.Agents.Length,
                    requirePositionChangeTotals: checkpoint.SchemaVersion >= 16)
                || (checkpoint.SchemaVersion < 17
                    && (checkpoint.WorldChronicle is null
                        || checkpoint.WorldChronicle.Any(entry => !IsValidWorldEvent(
                            entry,
                            checkpoint.EventSequence,
                            checkpoint.Tick,
                            checkpoint.Agents.Length))
                        || checkpoint.WorldChronicle.Any(entry =>
                            string.IsNullOrWhiteSpace(entry.PrimaryName))
                        || !IsStrictlyIncreasingSequence(checkpoint.WorldChronicle)))
                || (checkpoint.SchemaVersion >= 17
                    && (checkpoint.WorldChronicle is not { Length: 0 }
                        || !IsValidWorldCenturyChronicle(
                            checkpoint.CenturyChronicle,
                            checkpoint.Tick,
                            checkpoint.Agents.Length)))))
        {
            throw new InvalidDataException("세계 연대기 또는 기간별 랭킹 이력이 잘못되었습니다.");
        }

        var marketGoodCount = checkpoint.Market?.Stock?.Length ?? -1;
        var validMarketGoodCount = marketGoodCount == EconomyRules.MarketGoodCount
            || (checkpoint.SchemaVersion < 14
                && marketGoodCount == EconomyRules.LegacyMarketGoodCount);
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
            || checkpoint.Market.Stock is not { Length: var stockLength }
            || !validMarketGoodCount
            || checkpoint.Market.Prices is not { Length: var priceLength }
            || priceLength != stockLength
            || checkpoint.Market.Supply is not { Length: var supplyLength }
            || supplyLength != stockLength
            || checkpoint.Market.Demand is not { Length: var demandLength }
            || demandLength != stockLength
            || checkpoint.Market.Stock.Any(value => value < 0)
            || checkpoint.Market.Prices.Any(value => value < 1)
            || checkpoint.Market.Supply.Any(value => value < 0)
            || checkpoint.Market.Demand.Any(value => value < 0)
            || checkpoint.Market.TreasureQualityHundredths < 0
            || (checkpoint.SchemaVersion < 14
                && marketGoodCount == EconomyRules.LegacyMarketGoodCount
                && checkpoint.Market.TreasureQualityHundredths
                    > (long)checkpoint.Market.Stock[32] * QualityValue.MaximumHundredths)
            || (checkpoint.SchemaVersion >= 14
                && (checkpoint.Market.QualityHundredths is not { Length: EconomyRules.MarketGoodCount }
                    || checkpoint.Market.QualityHundredths.Any(value => value < 0)
                    || checkpoint.Market.RemainingSpiritStoneOre < 0
                    || checkpoint.Market.SpiritStonesMined < 0
                    || checkpoint.Market.MiningMissionCount < 0
                    || checkpoint.Market.LastIndustryMonth < 0
                    || checkpoint.Market.ProductionQueue is null
                    || checkpoint.Market.ProductionQueue.Length > 4
                    || checkpoint.Market.ProductionQueue.Any(order =>
                        order.GoodId < EconomyRules.RealmGoodStartId
                        || order.GoodId >= EconomyRules.MarketGoodCount
                        || order.CompletedMonths < 0
                        || order.RequiredMonths <= 0
                        || order.CompletedMonths >= order.RequiredMonths
                        || order.MaterialCost < 0
                        || order.SpiritStoneCost <= 0))))
        {
            throw new InvalidDataException("세계 체크포인트의 도시 시장 상태가 잘못되었습니다.");
        }

        for (var index = 0; index < checkpoint.Agents.Length; index++)
        {
            var agent = checkpoint.Agents[index];
            var validMaterialCount = agent.MaterialInventory?.Length == EconomyRules.MaterialCount
                || (checkpoint.SchemaVersion < 14
                    && agent.MaterialInventory?.Length == EconomyRules.LegacyMaterialCount);
            var validTreasureCount = agent.EquippedTreasureQualities?.Length
                    == EconomyRules.TreasureSlotCount
                || (checkpoint.SchemaVersion < 14
                    && agent.EquippedTreasureQualities?.Length
                        == EconomyRules.LegacyTreasureSlotCount);
            var validMissionMaterialCount = agent.MaterialInventory?.Length
                ?? EconomyRules.MaterialCount;
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
                    && agent.ActiveMissionMaterial >= validMissionMaterialCount)
                || agent.MaterialInventory is not { Length: var materialLength }
                || !validMaterialCount
                || agent.MaterialInventory.Any(count => count < 0)
                || agent.EquippedTreasureQualities is not { Length: var treasureLength }
                || !validTreasureCount
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
                || entry.PillsCrafted < 0)
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
            || current.PillsCrafted < 0)
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
                == current.UniqueRebirthCount;
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
