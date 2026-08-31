using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    public const int WorldLogCapacity = 100;
    public const int PersonalLogCapacity = 100;
    private readonly DeterministicRandom _random;
    private readonly AgentStore _agents;
    private readonly CityMarket _market;
    private readonly PriorityQueue<int, (long Tick, int Agent)> _decisionSchedule = new();
    private readonly int[] _agentByRank;
    private readonly int[] _rankByAgent;
    private readonly int[] _agentByRealmRank;
    private readonly int[] _realmRankByAgent;
    private readonly int[] _agentByAptitudeRank;
    private readonly int[] _aptitudeRankByAgent;
    private readonly int[] _agentByWealthRank;
    private readonly int[] _wealthRankByAgent;
    private readonly int[] _monsterPopulations;
    private readonly Queue<WorldEvent> _events = new(WorldLogCapacity);
    private readonly WorldCenturyChronicle _worldChronicle;
    private readonly RankingHistory _rankingHistory = new();
    private long _eventSequence;
    private long _chronicleSequence;
    private long _personalLogSequence;
    private int _totalBreakthroughs;
    private int _totalChallenges;
    private int _totalCombatsStarted;
    private int _totalCombatsCompleted;
    private int _activeCombats;
    private int _totalDeaths;
    private int _totalReincarnations;
    private int _totalMissionsCompleted;
    private int _totalTreasuresCrafted;
    private int _totalPillsCrafted;
    private int _totalMaterialsSold;
    private int _totalMonsterHunts;
    private int _totalMonsterVictories;
    private long _lastMonsterRegenerationMonth;
    private long _lastRankingRefreshTick;
    private long _rankingMovementFromTick;
    private int _rankingRisenCount;
    private int _rankingFallenCount;
    private int _rankingUnchangedCount;
    private long _rankingTotalAbsoluteMovement;
    private readonly int[] _eliteCombatChangedCounts = new int[4];
    private readonly int[] _eliteRealmChangedCounts = new int[4];

    internal event Action<WorldEventNotification>? ImportantWorldEventRecorded;
    internal event Action<WorldCenturyChronicleEntry>? WorldCenturyChronicleRecorded;

    private World(ulong seed, int population)
    {
        Seed = seed;
        _random = new DeterministicRandom(seed);
        _agents = new AgentStore(population);
        _market = new CityMarket(population);
        _agentByRank = new int[population];
        _rankByAgent = new int[population];
        _agentByRealmRank = new int[population];
        _realmRankByAgent = new int[population];
        _agentByAptitudeRank = new int[population];
        _aptitudeRankByAgent = new int[population];
        _agentByWealthRank = new int[population];
        _wealthRankByAgent = new int[population];
        _monsterPopulations = CreateInitialMonsterPopulations();
        _worldChronicle = new WorldCenturyChronicle(population, currentYear: 0);
        _worldChronicle.EntryRecorded += WorldChronicle_EntryRecorded;

        for (var index = 0; index < population; index++)
        {
            _agents.Initialize(index, _random);
            AddChronicle(index, ChronicleEventKind.Birth);
            _agentByRank[index] = index;
            _agentByRealmRank[index] = index;
            _agentByAptitudeRank[index] = index;
            _agentByWealthRank[index] = index;
            _decisionSchedule.Enqueue(index, (_agents.NextDecisionTicks[index], index));
        }

        RefreshRankings(recordLeaderChange: false);
    }

    public ulong Seed { get; }
    public SimulationTick Tick { get; private set; }
    public int Population => _agents.Count;
    public AgentId LeaderId => new(_agentByRank[0]);
    public int TotalBreakthroughs => _totalBreakthroughs;
    public int TotalChallenges => _totalChallenges;
    public int TotalCombatsStarted => _totalCombatsStarted;
    public int TotalCombatsCompleted => _totalCombatsCompleted;
    public int ActiveCombats => _activeCombats;
    public int TotalDeaths => _totalDeaths;
    public int TotalReincarnations => _totalReincarnations;
    public int TotalMissionsCompleted => _totalMissionsCompleted;
    public int TotalTreasuresCrafted => _totalTreasuresCrafted;
    public int TotalPillsCrafted => _totalPillsCrafted;
    public int TotalMaterialsSold => _totalMaterialsSold;
    public int TotalMonsterHunts => _totalMonsterHunts;
    public int TotalMonsterVictories => _totalMonsterVictories;

    public static World CreateDefault(ulong seed) =>
        new(seed, ProductInfo.DefaultPopulation);

    public static World Create(ulong seed, int population) =>
        new(seed, population);

    public void AdvanceTo(SimulationTick targetTick)
    {
        if (targetTick.Value < Tick.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(targetTick), "세계 시간을 되돌릴 수 없습니다.");
        }

        while (_decisionSchedule.TryPeek(out _, out var priority) && priority.Tick <= targetTick.Value)
        {
            var agentIndex = _decisionSchedule.Dequeue();
            var dueTick = priority.Tick;
            if (_agents.NextDecisionTicks[agentIndex] != dueTick)
            {
                continue;
            }

            Tick = new SimulationTick(dueTick);
            AdvanceCityIndustry(GameCalendar.GetMonthIndex(Tick));
            RegenerateMonsters(GameCalendar.GetMonthIndex(Tick));
            var actionMonth = GameCalendar.GetMonthIndex(Tick);
            if (_agents.LastActionMonths[agentIndex] >= actionMonth)
            {
                ScheduleAgent(
                    agentIndex,
                    GameCalendar.GetMonthStart(checked(_agents.LastActionMonths[agentIndex] + 1)).Value);
                continue;
            }

            _agents.LastActionMonths[agentIndex] = actionMonth;
            _agents.LastActionTicks[agentIndex] = dueTick;
            _agents.ActionsTaken[agentIndex] = checked(_agents.ActionsTaken[agentIndex] + 1);
            if (_agents.Activities[agentIndex] != AgentActivity.Reincarnating
                && HasReachedMaximumLifespan(agentIndex))
            {
                BeginReincarnation(agentIndex);
                continue;
            }

            var resolvedActivity = _agents.Activities[agentIndex];
            var powerInputsBefore = _agents.GetPowerInputSignature(agentIndex);
            var alreadyScheduled = ResolveActivity(agentIndex);
            TryResolveBottleneckAfterAction(agentIndex, resolvedActivity);
            AddPersonalActionLog(agentIndex, resolvedActivity);
            if (alreadyScheduled)
            {
                continue;
            }

            SelectNextActivity(agentIndex);
            if (powerInputsBefore != _agents.GetPowerInputSignature(agentIndex))
            {
                _agents.RecalculatePower(agentIndex);
            }
            var nextTick = checked(dueTick + CalculateActivityDuration(agentIndex));
            ScheduleAgent(agentIndex, nextTick);
        }

        Tick = targetTick;
        AdvanceCityIndustry(GameCalendar.GetMonthIndex(Tick));
        RegenerateMonsters(GameCalendar.GetMonthIndex(Tick));
        RefreshRankings(recordLeaderChange: true);
        _worldChronicle.AdvanceTo(Tick);
    }

    private void AdvanceCityIndustry(long currentMonth)
    {
        var pillsBefore = _market.PillsProduced;
        var treasuresBefore = _market.TreasuresProduced;
        _market.AdvanceIndustry(currentMonth);
        _totalPillsCrafted = checked(
            _totalPillsCrafted + (int)(_market.PillsProduced - pillsBefore));
        _totalTreasuresCrafted = checked(
            _totalTreasuresCrafted + (int)(_market.TreasuresProduced - treasuresBefore));
    }

    public AgentSnapshot GetAgentSnapshot(
        AgentId agentId,
        bool includeAptitudeDetails = false)
    {
        var index = ValidateAgent(agentId);
        var combatAptitudes = _agents.GetContextAptitudes(index, combat: true);
        var aptitudeBonuses = AptitudeCombatBonusFormula.Calculate(
            _agents.GetAptitudeRoleScores(index));
        return new AgentSnapshot(
            agentId,
            _agents.Names[index],
            _rankByAgent[index],
            _realmRankByAgent[index],
            _aptitudeRankByAgent[index],
            _wealthRankByAgent[index],
            _agents.TargetRanks[index],
            GetAgeYears(index),
            RealmRules.GetProfile(_agents.RealmStages[index]).MaximumLifespanYears,
            _agents.LifeNumbers[index],
            _agents.RealmStages[index],
            _agents.RealmLayers[index],
            _agents.CultivationProgress[index],
            GetCultivationRequirement(index),
            _agents.CultivationStates[index],
            _agents.CultivationBottleneckCauses[index],
            _agents.GetAptitudes(index),
            new QualityValue(_agents.TechniqueQualities[index]),
            _agents.TechniqueMasteries[index],
            _agents.TechniqueTrainingExperience[index],
            _agents.TechniqueUnderstanding[index],
            new QualityValue(_agents.SpellQualities[index]),
            _agents.SpellMasteries[index],
            _agents.SpellTrainingExperience[index],
            _agents.SpellUnderstanding[index],
            _agents.CombatExperience[index],
            CombatProgressionRules.GetMasteryHundredths(_agents.CombatExperience[index]),
            _agents.CombatUnderstanding[index],
            _agents.Wins[index],
            _agents.Losses[index],
            _agents.Health[index],
            _agents.MaximumHealth[index],
            _agents.Qi[index],
            _agents.MaximumQi[index],
            _agents.CombatAttributes[index],
            _agents.InjuryPoints[index],
            CombatInjuryRules.GetSeverity(_agents.InjuryPoints[index]),
            _agents.Activities[index],
            new SimulationTick(_agents.NextDecisionTicks[index]),
            _agents.Opponents[index] < 0 ? null : new AgentId(_agents.Opponents[index]),
            new SimulationTick(_agents.LastCombatTicks[index]),
            _agents.ChallengesIssued[index],
            _agents.ActionsTaken[index],
            _agents.LastActionMonths[index] < 0
                ? null
                : GameCalendar.GetDate(new SimulationTick(_agents.LastActionTicks[index])),
            _agents.LocationIds[index],
            _agents.TravelDestinationIds[index],
            _agents.MonsterHunts[index],
            _agents.MonsterVictories[index],
            _agents.SpiritStones[index],
            _agents.GrowthPills[index],
            _agents.BreakthroughPills[index],
            _agents.MissionsCompleted[index],
            GetActiveMissionSnapshot(index),
            _agents.GetInventoryItemCount(index),
            GetMaterialHoldingSnapshots(index),
            GetEquippedTreasureSnapshots(index),
            _agents.Power[index],
            CreateCultivationProjection(index),
            CombatPowerFormula.CalculateDetailed(
                _agents.RealmStages[index],
                _agents.RealmLayers[index],
                combatAptitudes,
                aptitudeBonuses,
                new QualityValue(_agents.TechniqueQualities[index]),
                _agents.TechniqueMasteries[index],
                _agents.TechniqueUnderstanding[index],
                new QualityValue(_agents.SpellQualities[index]),
                _agents.SpellMasteries[index],
                _agents.SpellUnderstanding[index],
                _agents.CombatExperience[index],
                CombatProgressionRules.GetMasteryHundredths(_agents.CombatExperience[index]),
                _agents.CombatUnderstanding[index],
                _agents.GetTreasurePower(index)),
            includeAptitudeDetails
                ? _agents.CreateAptitudeDetailSnapshot(index)
                : null);
    }

    public WorldSnapshot CreateSnapshot(
        int rankingCount = 1_000,
        int eventCount = 100,
        ObservationSections sections = ObservationSections.All,
        ushort? detailedMapLocationId = null,
        int detailedMapAgentOffset = 0,
        int worldChronicleCount = int.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rankingCount);
        ArgumentOutOfRangeException.ThrowIfNegative(eventCount);
        ArgumentOutOfRangeException.ThrowIfNegative(worldChronicleCount);
        var actualRankingCount = Math.Min(rankingCount, Population);
        var combatPowerRankings = (sections & ObservationSections.CombatPowerRanking) != 0
            ? CreateRankingEntries(
                RankingCategory.CombatPower,
                _agentByRank,
                actualRankingCount)
            : Array.Empty<RankingEntry>();
        var realmRankings = (sections & ObservationSections.RealmRanking) != 0
            ? CreateRankingEntries(
                RankingCategory.Realm,
                _agentByRealmRank,
                actualRankingCount)
            : Array.Empty<RankingEntry>();
        var aptitudeRankings = (sections & ObservationSections.AptitudeRanking) != 0
            ? CreateRankingEntries(
                RankingCategory.Aptitude,
                _agentByAptitudeRank,
                actualRankingCount)
            : Array.Empty<RankingEntry>();
        var wealthRankings = (sections & ObservationSections.WealthRanking) != 0
            ? CreateRankingEntries(
                RankingCategory.Wealth,
                _agentByWealthRank,
                actualRankingCount)
            : Array.Empty<RankingEntry>();
        var requestedStatistics = sections & ObservationSections.Statistics;
        var statistics = requestedStatistics != ObservationSections.None
            ? CreateStatistics(requestedStatistics)
            : WorldStatistics.Empty;
        var market = (sections & ObservationSections.Market) != 0
            ? _market.CreateSnapshot((sections & ObservationSections.MarketGoods) != 0)
            : CityMarketSnapshot.Empty;
        var map = (sections & ObservationSections.Map) != 0
            ? CreateMapSnapshot(detailedMapLocationId, detailedMapAgentOffset)
            : WorldMapSnapshot.Empty;

        var recentEvents = (sections & ObservationSections.WorldLog) != 0
            ? _events.Skip(Math.Max(0, _events.Count - eventCount)).ToArray()
            : Array.Empty<WorldEvent>();
        var permanentWorldChronicle = (sections & ObservationSections.WorldChronicle) != 0
            ? _worldChronicle.GetTail(worldChronicleCount)
            : Array.Empty<WorldCenturyChronicleEntry>();
        var agentNames = new Dictionary<int, string>(actualRankingCount * 4);
        foreach (var ranking in combatPowerRankings
                     .Concat(realmRankings)
                     .Concat(aptitudeRankings)
                     .Concat(wealthRankings))
        {
            agentNames[ranking.AgentId.Value] = ranking.Name;
        }

        return new WorldSnapshot(
            Tick,
            Population,
            combatPowerRankings,
            realmRankings,
            aptitudeRankings,
            wealthRankings,
            statistics,
            market,
            map,
            agentNames,
            recentEvents,
            permanentWorldChronicle,
            _worldChronicle.Count,
            _totalBreakthroughs,
            _totalChallenges,
            _totalCombatsStarted,
            _totalCombatsCompleted,
            _activeCombats,
            _totalDeaths,
            _totalReincarnations,
            LeaderId);
    }

    private RankingEntry[] CreateRankingEntries(
        RankingCategory category,
        int[] orderedAgents,
        int count)
    {
        var entries = new RankingEntry[count];
        for (var offset = 0; offset < count; offset++)
        {
            var agentIndex = orderedAgents[offset];
            var aptitudeScore = _agents.GetAptitudes(agentIndex).RankingScore;
            var score = category switch
            {
                RankingCategory.CombatPower => _agents.Power[agentIndex].Total,
                RankingCategory.Realm when HasEnteredCultivation(agentIndex) =>
                    ((((long)(int)_agents.RealmStages[agentIndex] * RealmRules.MinorStageCount)
                        + _agents.RealmLayers[agentIndex]) * 1_000_000_000L)
                    + _agents.CultivationProgress[agentIndex],
                RankingCategory.Realm => _agents.CultivationProgress[agentIndex],
                RankingCategory.Aptitude => aptitudeScore,
                RankingCategory.Wealth => _agents.SpiritStones[agentIndex],
                _ => throw new ArgumentOutOfRangeException(nameof(category)),
            };

            entries[offset] = new RankingEntry(
                category,
                offset + 1,
                new AgentId(agentIndex),
                _agents.Names[agentIndex],
                HasEnteredCultivation(agentIndex),
                _agents.RealmStages[agentIndex],
                _agents.RealmLayers[agentIndex],
                _agents.GreatPerfectionAchieved[agentIndex],
                _agents.Activities[agentIndex],
                _agents.Power[agentIndex],
                aptitudeScore,
                score);
        }

        return entries;
    }

    public ulong ComputeStateHash()
    {
        var hash = 14695981039346656037UL;
        Add(ref hash, Seed);
        Add(ref hash, (ulong)Tick.Value);
        Add(ref hash, _random.State);
        Add(ref hash, (ulong)_eventSequence);
        Add(ref hash, (ulong)_chronicleSequence);
        Add(ref hash, (ulong)_personalLogSequence);
        Add(ref hash, (ulong)_totalBreakthroughs);
        Add(ref hash, (ulong)_totalChallenges);
        Add(ref hash, (ulong)_totalCombatsStarted);
        Add(ref hash, (ulong)_totalCombatsCompleted);
        Add(ref hash, (ulong)_activeCombats);
        Add(ref hash, (ulong)_totalDeaths);
        Add(ref hash, (ulong)_totalReincarnations);
        Add(ref hash, (ulong)_totalMissionsCompleted);
        Add(ref hash, (ulong)_totalTreasuresCrafted);
        Add(ref hash, (ulong)_totalPillsCrafted);
        Add(ref hash, (ulong)_totalMaterialsSold);
        Add(ref hash, (ulong)_totalMonsterHunts);
        Add(ref hash, (ulong)_totalMonsterVictories);
        Add(ref hash, (ulong)_lastRankingRefreshTick);
        Add(ref hash, (ulong)_rankingMovementFromTick);
        Add(ref hash, (ulong)_rankingRisenCount);
        Add(ref hash, (ulong)_rankingFallenCount);
        Add(ref hash, (ulong)_rankingUnchangedCount);
        Add(ref hash, (ulong)_rankingTotalAbsoluteMovement);
        for (var tier = 0; tier < _eliteCombatChangedCounts.Length; tier++)
        {
            Add(ref hash, (ulong)_eliteCombatChangedCounts[tier]);
            Add(ref hash, (ulong)_eliteRealmChangedCounts[tier]);
        }
        foreach (var frame in _rankingHistory.Frames)
        {
            Add(ref hash, (ulong)frame.Tick.Value);
            for (var index = 0; index < frame.CombatPowerTop.Length; index++)
            {
                Add(ref hash, (ulong)frame.CombatPowerTop[index]);
                Add(ref hash, (ulong)frame.RealmTop[index]);
                Add(ref hash, (ulong)frame.WealthTop[index]);
            }
            for (var tier = 0; tier < frame.CombatPowerPositionChangeTotals.Length; tier++)
            {
                Add(ref hash, (ulong)frame.CombatPowerPositionChangeTotals[tier]);
                Add(ref hash, (ulong)frame.RealmPositionChangeTotals[tier]);
                Add(ref hash, (ulong)frame.WealthPositionChangeTotals[tier]);
            }
        }
        var market = _market.CreateCheckpoint();
        Add(ref hash, (ulong)market.TreasurySpiritStones);
        Add(ref hash, (ulong)market.PurchaseTransactions);
        Add(ref hash, (ulong)market.SaleTransactions);
        Add(ref hash, (ulong)market.MissionDeliveries);
        Add(ref hash, (ulong)market.MaterialsProcessed);
        Add(ref hash, (ulong)market.PillsProduced);
        Add(ref hash, (ulong)market.TreasuresProduced);
        Add(ref hash, (ulong)market.TechniqueBooksProduced);
        Add(ref hash, (ulong)market.SpellBooksProduced);
        Add(ref hash, (ulong)market.TreasureQualityHundredths);
        Add(ref hash, (ulong)market.LastIndustryMonth);
        Add(ref hash, (ulong)market.SpiritStonesMined);
        Add(ref hash, (ulong)market.RemainingSpiritStoneOre);
        Add(ref hash, (ulong)market.MiningMissionCount);
        Add(ref hash, (ulong)market.SpiritStoneReserveTarget);
        for (var goodId = 0; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            Add(ref hash, (ulong)market.Stock[goodId]);
            Add(ref hash, (ulong)market.Prices[goodId]);
            Add(ref hash, (ulong)market.Supply[goodId]);
            Add(ref hash, (ulong)market.Demand[goodId]);
            Add(ref hash, (ulong)(market.QualityHundredths?[goodId] ?? 0));
        }
        foreach (var order in market.ProductionQueue ?? Array.Empty<CityProductionCheckpoint>())
        {
            Add(ref hash, (ulong)order.GoodId);
            Add(ref hash, order.QualityHundredths);
            Add(ref hash, (ulong)order.CompletedMonths);
            Add(ref hash, (ulong)order.RequiredMonths);
            Add(ref hash, (ulong)order.MaterialCost);
            Add(ref hash, (ulong)order.SpiritStoneCost);
        }
        Add(ref hash, (ulong)_lastMonsterRegenerationMonth);
        foreach (var population in _monsterPopulations)
        {
            Add(ref hash, (ulong)population);
        }
        for (var index = 0; index < Population; index++)
        {
            Add(ref hash, (ulong)index);
            AddString(ref hash, _agents.Names[index]);
            Add(ref hash, _agents.SpiritualRoots[index]);
            Add(ref hash, _agents.Bones[index]);
            Add(ref hash, _agents.Constitutions[index]);
            Add(ref hash, _agents.Comprehensions[index]);
            Add(ref hash, _agents.Minds[index]);
            Add(ref hash, _agents.Souls[index]);
            for (var component = 0; component < AptitudeRules.TotalComponentCount; component++)
            {
                Add(
                    ref hash,
                    _agents.AptitudeComponents[
                        (index * AptitudeRules.TotalComponentCount) + component]);
            }
            Add(ref hash, (ulong)_agents.BirthTicks[index]);
            Add(ref hash, (ulong)_agents.LifeNumbers[index]);
            Add(ref hash, _agents.BottleneckFailures[index]);
            Add(ref hash, (byte)_agents.RealmStages[index]);
            Add(ref hash, _agents.RealmLayers[index]);
            Add(ref hash, (ulong)_agents.CultivationProgress[index]);
            Add(ref hash, (ulong)_agents.PendingCultivationWork[index]);
            Add(ref hash, (ulong)_agents.LastCultivationWorkBefore[index]);
            Add(ref hash, (ulong)_agents.LastCultivationWorkGain[index]);
            Add(ref hash, (byte)_agents.CultivationStates[index]);
            Add(ref hash, (byte)_agents.CultivationBottleneckCauses[index]);
            Add(ref hash, _agents.FloorNineCompleted[index] ? 1UL : 0UL);
            Add(ref hash, _agents.GreatPerfectionAchieved[index] ? 1UL : 0UL);
            Add(ref hash, _agents.EntryGatePassMasks[index]);
            for (var gate = 0; gate < CultivationRules.EntryGateSlotCount; gate++)
            {
                Add(ref hash, _agents.EntryGateTemperings[
                    (index * CultivationRules.EntryGateSlotCount) + gate]);
            }
            Add(ref hash, (byte)_agents.FoundationRoutes[index]);
            for (var component = 0; component < CultivationRules.RealmComponentSlotCount; component++)
            {
                var cultivationOffset = (index * CultivationRules.RealmComponentSlotCount) + component;
                Add(ref hash, _agents.CultivationComponentValues[cultivationOffset]);
                Add(ref hash, _agents.CultivationComponentLimits[cultivationOffset]);
            }

            for (var formation = 0; formation < CultivationRules.FormationSlotCount; formation++)
            {
                Add(ref hash, _agents.FormationValues[(index * CultivationRules.FormationSlotCount) + formation]);
            }

            var trace = _agents.CultivationTraces[index];
            Add(ref hash, (ulong)trace.Tick);
            Add(ref hash, (ulong)trace.FormulaRevision);
            Add(ref hash, (byte)trace.AttemptClass);
            Add(ref hash, (byte)trace.Outcome);
            Add(ref hash, (byte)trace.Realm);
            Add(ref hash, trace.SourceFloor);
            Add(ref hash, trace.DestinationFloor);
            Add(ref hash, (ulong)trace.WorkBefore);
            Add(ref hash, (ulong)trace.WorkGain);
            Add(ref hash, (ulong)trace.WorkAfter);
            Add(ref hash, (ulong)trace.RequiredWork);
            Add(ref hash, (ulong)trace.CarryoverWork);
            Add(ref hash, (ulong)trace.BaseChanceBasisPoints);
            Add(ref hash, unchecked((ulong)(long)trace.ModifierBasisPoints));
            Add(ref hash, (ulong)trace.FinalChanceBasisPoints);
            Add(ref hash, unchecked((ulong)(long)trace.RollBasisPoints));
            Add(ref hash, (byte)trace.Blocker);
            Add(ref hash, _agents.TechniqueQualities[index]);
            Add(ref hash, _agents.TechniqueMasteries[index]);
            Add(ref hash, (ulong)_agents.TechniqueTrainingExperience[index]);
            Add(ref hash, _agents.TechniqueUnderstanding[index]);
            Add(ref hash, _agents.SpellQualities[index]);
            Add(ref hash, _agents.SpellMasteries[index]);
            Add(ref hash, (ulong)_agents.SpellTrainingExperience[index]);
            Add(ref hash, _agents.SpellUnderstanding[index]);
            Add(ref hash, (ulong)_agents.CombatExperience[index]);
            Add(ref hash, _agents.CombatUnderstanding[index]);
            Add(ref hash, (ulong)_agents.Wins[index]);
            Add(ref hash, (ulong)_agents.Losses[index]);
            Add(ref hash, (ulong)_agents.Health[index]);
            Add(ref hash, (ulong)_agents.MaximumHealth[index]);
            Add(ref hash, (ulong)_agents.Qi[index]);
            Add(ref hash, (ulong)_agents.MaximumQi[index]);
            Add(ref hash, (byte)_agents.Activities[index]);
            Add(ref hash, (ulong)_agents.NextDecisionTicks[index]);
            Add(ref hash, (ulong)(_agents.LastActionMonths[index] + 1));
            Add(ref hash, (ulong)_agents.LastActionTicks[index]);
            Add(ref hash, (ulong)_agents.ActionsTaken[index]);
            Add(ref hash, (ulong)(_agents.Opponents[index] + 1));
            Add(ref hash, (ulong)(_agents.LastOpponents[index] + 1));
            Add(ref hash, (ulong)_agents.LastCombatTicks[index]);
            Add(ref hash, (ulong)_agents.CombatStartedTicks[index]);
            Add(ref hash, (ulong)_agents.CombatActions[index]);
            Add(ref hash, (ulong)(_agents.CombatTurnOwners[index] + 1));
            Add(ref hash, _agents.InjuryPoints[index]);
            Add(ref hash, _agents.Guarding[index] ? 1UL : 0UL);
            Add(ref hash, (ulong)_agents.ChallengesIssued[index]);
            Add(ref hash, (ulong)_agents.SpiritStones[index]);
            Add(ref hash, (ulong)_agents.GrowthPills[index]);
            Add(ref hash, (ulong)_agents.BreakthroughPills[index]);
            Add(ref hash, (ulong)_agents.MissionsCompleted[index]);
            Add(ref hash, _agents.ActiveMissionMaterials[index]);
            Add(ref hash, _agents.LocationIds[index]);
            Add(ref hash, _agents.TravelDestinationIds[index]);
            Add(ref hash, (ulong)_agents.MonsterHunts[index]);
            Add(ref hash, (ulong)_agents.MonsterVictories[index]);
            for (var material = 0; material < EconomyRules.MaterialCount; material++)
            {
                Add(ref hash, (ulong)_agents.MaterialInventory[(index * EconomyRules.MaterialCount) + material]);
            }

            for (var slot = 0; slot < EconomyRules.TreasureSlotCount; slot++)
            {
                Add(ref hash, _agents.EquippedTreasureQualities[(index * EconomyRules.TreasureSlotCount) + slot]);
            }
            Add(ref hash, (ulong)_rankByAgent[index]);
            Add(ref hash, (ulong)_realmRankByAgent[index]);
            Add(ref hash, (ulong)_aptitudeRankByAgent[index]);
            foreach (var entry in _agents.PersonalLogs[index])
            {
                Add(ref hash, (ulong)entry.Sequence);
                Add(ref hash, (ulong)entry.Tick.Value);
                Add(ref hash, (ulong)entry.LifeNumber);
                Add(ref hash, (byte)entry.Kind);
                Add(ref hash, (ulong)((int?)entry.Activity + 1 ?? 0));
                Add(ref hash, (ulong)((int?)entry.WorldEventKind + 1 ?? 0));
                Add(ref hash, (ulong)((int?)entry.ChronicleEventKind + 1 ?? 0));
                Add(ref hash, entry.IsPrimaryActor ? 1UL : 0UL);
                Add(ref hash, (ulong)(entry.RelatedAgent?.Value + 1 ?? 0));
                Add(ref hash, unchecked((ulong)entry.Value));
            }

            foreach (var entry in _agents.Chronicles[index])
            {
                Add(ref hash, (ulong)entry.Sequence);
                Add(ref hash, (ulong)entry.Tick.Value);
                Add(ref hash, (ulong)entry.LifeNumber);
                Add(ref hash, (ulong)entry.AgeYears);
                AddString(ref hash, entry.PersonName);
                Add(ref hash, (byte)entry.Kind);
                Add(ref hash, (byte)entry.RealmStage);
                Add(ref hash, (byte)entry.MinorStage);
                Add(ref hash, unchecked((ulong)(long)entry.Value));
            }
        }

        foreach (var worldEvent in _events)
        {
            Add(ref hash, (ulong)worldEvent.Sequence);
            Add(ref hash, (ulong)worldEvent.Tick.Value);
            Add(ref hash, (byte)worldEvent.Kind);
            Add(ref hash, (ulong)worldEvent.PrimaryAgent.Value);
            Add(ref hash, (ulong)(worldEvent.SecondaryAgent?.Value + 1 ?? 0));
            Add(ref hash, unchecked((ulong)(long)worldEvent.Value));
            AddString(ref hash, worldEvent.PrimaryName ?? string.Empty);
            AddString(ref hash, worldEvent.SecondaryName ?? string.Empty);
        }


        AddWorldCenturyChronicleHash(ref hash, _worldChronicle.CreateCheckpoint());

        return hash;
    }

    private bool ResolveActivity(int index)
    {
        switch (_agents.Activities[index])
        {
            case AgentActivity.Cultivating:
                ResolveCultivation(index);
                return false;
            case AgentActivity.TrainingTechnique:
                ResolveTechniqueTraining(index);
                return false;
            case AgentActivity.TrainingSpell:
                ResolveSpellTraining(index);
                return false;
            case AgentActivity.Recovering:
                ResolveRecovery(index);
                return false;
            case AgentActivity.SeekingChallenge:
                ResolveSeekingChallenge(index);
                return false;
            case AgentActivity.Fighting:
                return ResolveCombatAction(index);
            case AgentActivity.Reincarnating:
                CompleteRebirth(index);
                return true;
            case AgentActivity.DoingCityMission:
                ResolveCityMission(index);
                return false;
            case AgentActivity.TradingInCity:
                ResolveCityTrading(index);
                return false;
            case AgentActivity.CraftingTreasure:
                ResolveTreasureCrafting(index);
                return false;
            case AgentActivity.CraftingPill:
                ResolvePillCrafting(index);
                return false;
            case AgentActivity.Traveling:
                ResolveTravel(index);
                return false;
            case AgentActivity.HuntingMonster:
                return ResolveMonsterHunt(index);
            case AgentActivity.MiningSpiritStones:
                ResolveSpiritStoneMining(index);
                return false;
            default:
                throw new InvalidOperationException("알 수 없는 AI 행동입니다.");
        }
    }

    private void ResolveTechniqueTraining(int index)
    {
        var roleScores = _agents.GetAptitudeRoleScores(index);
        var comprehension = _agents.GetDerivedAptitude(
            index,
            AptitudeDerivedAbility.ComprehensionLearning);
        var bone = _agents.GetDerivedAptitude(index, AptitudeDerivedAbility.BoneCultivation);
        var effectBasisPoints = AptitudeCombatBonusFormula.GetTechniqueEffectBasisPoints(
            roleScores);
        var experienceGain = SkillProgressionRules.ApplyExperienceBonus(
            SkillProgressionRules.GetTechniqueExperienceGain(
                comprehension,
                bone),
            effectBasisPoints);
        _agents.TechniqueTrainingExperience[index] = Math.Min(
            SkillProgressionRules.MaximumExperience,
            checked(_agents.TechniqueTrainingExperience[index] + experienceGain));
        _agents.TechniqueMasteries[index] = SkillProgressionRules.GetMasteryHundredths(
            _agents.TechniqueTrainingExperience[index]);
        var chance = SkillProgressionRules.GetEnlightenmentChanceBasisPoints(
            comprehension,
            _agents.TechniqueUnderstanding[index]);
        if (_agents.TechniqueUnderstanding[index] < SkillProgressionRules.MaximumHundredths
            && _random.Chance(chance))
        {
            _agents.TechniqueUnderstanding[index] = SkillProgressionRules.ApplyUnderstandingGain(
                _agents.TechniqueUnderstanding[index],
                comprehension,
                effectBasisPoints);
            AddEvent(
                WorldEventKind.TechniqueEnlightenment,
                index,
                _agents.TechniqueUnderstanding[index]);
        }
    }

    private void ResolveSpellTraining(int index)
    {
        var roleScores = _agents.GetAptitudeRoleScores(index);
        var comprehension = _agents.GetDerivedAptitude(
            index,
            AptitudeDerivedAbility.ComprehensionLearning);
        var spiritualRoot = _agents.GetDerivedAptitude(
            index,
            AptitudeDerivedAbility.SpiritualRootCultivation);
        var effectBasisPoints = AptitudeCombatBonusFormula.GetSpellEffectBasisPoints(
            roleScores);
        var experienceGain = SkillProgressionRules.ApplyExperienceBonus(
            SkillProgressionRules.GetSpellExperienceGain(
                comprehension,
                spiritualRoot),
            effectBasisPoints);
        _agents.SpellTrainingExperience[index] = Math.Min(
            SkillProgressionRules.MaximumExperience,
            checked(_agents.SpellTrainingExperience[index] + experienceGain));
        _agents.SpellMasteries[index] = SkillProgressionRules.GetMasteryHundredths(
            _agents.SpellTrainingExperience[index]);
        var chance = SkillProgressionRules.GetEnlightenmentChanceBasisPoints(
            comprehension,
            _agents.SpellUnderstanding[index]);
        if (_agents.SpellUnderstanding[index] < SkillProgressionRules.MaximumHundredths
            && _random.Chance(chance))
        {
            _agents.SpellUnderstanding[index] = SkillProgressionRules.ApplyUnderstandingGain(
                _agents.SpellUnderstanding[index],
                comprehension,
                effectBasisPoints);
            AddEvent(
                WorldEventKind.SpellEnlightenment,
                index,
                _agents.SpellUnderstanding[index]);
        }
    }

    private void ResolveRecovery(int index)
    {
        var constitution = _agents.GetDerivedAptitude(
            index,
            AptitudeDerivedAbility.ConstitutionCultivation);
        var spiritualRoot = _agents.GetDerivedAptitude(
            index,
            AptitudeDerivedAbility.SpiritualRootCultivation);
        var healthGain = Math.Max(
            20 + (GetQualityEffect(constitution) * 4),
            _agents.MaximumHealth[index] / 20);
        var qiGain = Math.Max(
            15 + (GetQualityEffect(spiritualRoot) * 5),
            _agents.MaximumQi[index] / 18);
        _agents.Health[index] = Math.Min(_agents.MaximumHealth[index], _agents.Health[index] + healthGain);
        _agents.Qi[index] = Math.Min(_agents.MaximumQi[index], _agents.Qi[index] + qiGain);
        var injuryRecovery = 150 + (constitution / 10);
        _agents.InjuryPoints[index] = checked((ushort)Math.Max(
            0,
            _agents.InjuryPoints[index] - injuryRecovery));
    }

    private void SelectNextActivity(int index)
    {
        if (_agents.Activities[index] == AgentActivity.Fighting)
        {
            return;
        }

        if (_agents.Health[index] * 100 < _agents.MaximumHealth[index] * 70
            || _agents.Qi[index] * 100 < _agents.MaximumQi[index] * 35
            || _agents.InjuryPoints[index] >= 2_500)
        {
            _agents.Activities[index] = AgentActivity.Recovering;
            return;
        }

        switch (_agents.CultivationStates[index])
        {
            case CultivationState.PreparingBreakthrough:
            case CultivationState.PreflightReady:
            case CultivationState.Stabilizing:
            case CultivationState.Stable:
                _agents.Activities[index] = AgentActivity.Cultivating;
                return;
            case CultivationState.Bottleneck:
                _agents.Activities[index] = _agents.CultivationBottleneckCauses[index] switch
                {
                    CultivationBottleneckCause.Knowledge =>
                        _agents.TechniqueMasteries[index] <= _agents.SpellMasteries[index]
                            ? AgentActivity.TrainingTechnique
                            : AgentActivity.TrainingSpell,
                    CultivationBottleneckCause.Body => AgentActivity.Recovering,
                    CultivationBottleneckCause.SpiritualRoot => AgentActivity.Recovering,
                    CultivationBottleneckCause.Resources =>
                        _agents.SpiritStones[index] >= _market.GetPrice(EconomyRules.GetPillGoodId(
                            PillKind.Breakthrough,
                            _agents.RealmStages[index]))
                            ? AgentActivity.TradingInCity
                            : AgentActivity.DoingCityMission,
                    CultivationBottleneckCause.Stability => AgentActivity.Recovering,
                    CultivationBottleneckCause.EntryGate => AgentActivity.Cultivating,
                    CultivationBottleneckCause.RealmComponent => AgentActivity.Cultivating,
                    CultivationBottleneckCause.Method => AgentActivity.TrainingTechnique,
                    _ => AgentActivity.Cultivating,
                };
                return;
        }

        if (_agents.CultivationStates[index] is CultivationState.PreCultivation
            or CultivationState.EntryTraining)
        {
            _agents.Activities[index] = AgentActivity.Cultivating;
            return;
        }

        // 모든 AI가 상위 경지를 목표로 한다는 핵심 의사결정 계약이다.
        // 위치·시장·도전 RNG보다 먼저 최소 절반의 행동을 본경 수행에 확보한다.
        if (_agents.CultivationStates[index] == CultivationState.RealmCultivation
            && (_agents.ActionsTaken[index] & 1) == 0)
        {
            _agents.Activities[index] = AgentActivity.Cultivating;
            return;
        }

        var rank = _rankByAgent[index];
        var sinceLastCombat = Tick.Value - _agents.LastCombatTicks[index];
        var challengeChance = sinceLastCombat >= GameCalendar.TicksPerYear * 2
            ? 24
            : sinceLastCombat >= GameCalendar.TicksPerYear
                ? 8
                : 0;
        if (rank > 1
            && challengeChance > 0
            && GetAgeYears(index) >= CultivationRules.EntryAgeYears
            && Tick.Value >= 60
            && WorldMapRules.IsCity(_agents.LocationIds[index])
            && _random.NextInt(100) < challengeChance)
        {
            _agents.Activities[index] = AgentActivity.SeekingChallenge;
            return;
        }

        AgentActivity activity;
        if (!TrySelectMapActivity(index, out activity))
        {
            var roll = _random.NextInt(100);
            activity = SelectNonCombatActivity(index, roll);
        }
        if (_agents.CultivationStates[index] == CultivationState.Completed
            && activity == AgentActivity.Cultivating)
        {
            activity = AgentActivity.TrainingTechnique;
        }

        _agents.Activities[index] = activity;
    }

    private long CalculateActivityDuration(int index)
    {
        return _agents.Activities[index] switch
        {
            AgentActivity.Cultivating => Math.Max(30, 145 - ((GetDerivedQualityEffect(index, AptitudeDerivedAbility.SpiritualRootCultivation) + GetDerivedQualityEffect(index, AptitudeDerivedAbility.ComprehensionLearning)) / 2)),
            AgentActivity.TrainingTechnique => Math.Max(34, 150 - ((GetDerivedQualityEffect(index, AptitudeDerivedAbility.BoneCultivation) + GetDerivedQualityEffect(index, AptitudeDerivedAbility.ComprehensionLearning)) / 2)),
            AgentActivity.TrainingSpell => Math.Max(34, 150 - ((GetDerivedQualityEffect(index, AptitudeDerivedAbility.SpiritualRootCultivation) + GetDerivedQualityEffect(index, AptitudeDerivedAbility.ComprehensionLearning)) / 2)),
            AgentActivity.Recovering => Math.Max(24, 120 - GetDerivedQualityEffect(index, AptitudeDerivedAbility.ConstitutionCultivation)),
            AgentActivity.SeekingChallenge => 20,
            AgentActivity.Fighting => CalculateCombatActionDuration(index),
            AgentActivity.Reincarnating => GameCalendar.TicksPerYear,
            AgentActivity.DoingCityMission => 80,
            AgentActivity.TradingInCity => 45,
            AgentActivity.CraftingTreasure => 110,
            AgentActivity.CraftingPill => 90,
            AgentActivity.Traveling => CalculateTravelDuration(index),
            AgentActivity.HuntingMonster => CalculateMonsterHuntDuration(index),
            AgentActivity.MiningSpiritStones => 90,
            _ => throw new InvalidOperationException("알 수 없는 AI 행동입니다."),
        };
    }

    private void RefreshRankings(bool recordLeaderChange)
    {
        var previousLeader = _agentByRank[0];
        Span<int> previousCombatTop = stackalloc int[Math.Min(10, Population)];
        Span<int> previousRealmTop = stackalloc int[Math.Min(10, Population)];
        if (recordLeaderChange)
        {
            _agentByRank.AsSpan(0, previousCombatTop.Length).CopyTo(previousCombatTop);
            _agentByRealmRank.AsSpan(0, previousRealmTop.Length).CopyTo(previousRealmTop);
            _rankingRisenCount = 0;
            _rankingFallenCount = 0;
            _rankingUnchangedCount = 0;
            _rankingTotalAbsoluteMovement = 0;
        }

        Array.Sort(_agentByRank, CompareAgentsByPower);
        Array.Sort(_agentByRealmRank, CompareAgentsByRealm);
        Array.Sort(_agentByAptitudeRank, CompareAgentsByAptitude);
        Array.Sort(_agentByWealthRank, CompareAgentsByWealth);
        for (var offset = 0; offset < _agentByRank.Length; offset++)
        {
            var agent = _agentByRank[offset];
            var previousRank = _rankByAgent[agent];
            var currentRank = offset + 1;
            if (recordLeaderChange)
            {
                if (currentRank < previousRank)
                {
                    _rankingRisenCount++;
                }
                else if (currentRank > previousRank)
                {
                    _rankingFallenCount++;
                }

                _rankingTotalAbsoluteMovement += Math.Abs((long)currentRank - previousRank);
            }

            _rankByAgent[agent] = currentRank;
            _realmRankByAgent[_agentByRealmRank[offset]] = offset + 1;
            _aptitudeRankByAgent[_agentByAptitudeRank[offset]] = offset + 1;
            _wealthRankByAgent[_agentByWealthRank[offset]] = offset + 1;
        }

        if (recordLeaderChange)
        {
            ReadOnlySpan<int> eliteLimits = [1, 3, 5, 10];
            for (var tier = 0; tier < eliteLimits.Length; tier++)
            {
                var count = Math.Min(eliteLimits[tier], Population);
                var combatChanged = 0;
                var realmChanged = 0;
                for (var offset = 0; offset < count; offset++)
                {
                    if (_agentByRank[offset] != previousCombatTop[offset])
                    {
                        combatChanged++;
                    }
                    if (_agentByRealmRank[offset] != previousRealmTop[offset])
                    {
                        realmChanged++;
                    }
                }

                _eliteCombatChangedCounts[tier] = combatChanged;
                _eliteRealmChangedCounts[tier] = realmChanged;
            }

            _rankingMovementFromTick = _lastRankingRefreshTick;
            _lastRankingRefreshTick = Tick.Value;
            _rankingUnchangedCount = Population - _rankingRisenCount - _rankingFallenCount;
        }

        var rankingSampleRecorded = _rankingHistory.Record(
            Tick,
            _agentByRank,
            _agentByRealmRank,
            _agentByWealthRank,
            force: !recordLeaderChange && _rankingHistory.Frames.Count == 0);
        if (rankingSampleRecorded)
        {
            _worldChronicle.RecordRankingSample(
                Tick,
                _agentByRank,
                _agentByRealmRank,
                _agentByAptitudeRank,
                _agentByWealthRank,
                agent => _agents.Names[agent]);
        }

        var leader = _agentByRank[0];
        if (recordLeaderChange && leader != previousLeader)
        {
            AddEvent(WorldEventKind.NewLeader, leader, _agents.Power[leader].Total, previousLeader);
        }
    }

    private int CompareAgentsByPower(int left, int right)
    {
        var powerComparison = _agents.Power[right].Total.CompareTo(_agents.Power[left].Total);
        return powerComparison != 0 ? powerComparison : left.CompareTo(right);
    }

    private int CompareAgentsByRealm(int left, int right)
    {
        var entryComparison = HasEnteredCultivation(right).CompareTo(HasEnteredCultivation(left));
        if (entryComparison != 0)
        {
            return entryComparison;
        }

        var realmComparison = _agents.RealmStages[right].CompareTo(_agents.RealmStages[left]);
        if (realmComparison != 0)
        {
            return realmComparison;
        }

        var minorStageComparison = _agents.RealmLayers[right].CompareTo(_agents.RealmLayers[left]);
        if (minorStageComparison != 0)
        {
            return minorStageComparison;
        }

        var progressComparison = _agents.CultivationProgress[right].CompareTo(
            _agents.CultivationProgress[left]);
        return progressComparison != 0 ? progressComparison : left.CompareTo(right);
    }

    private int CompareAgentsByAptitude(int left, int right)
    {
        var aptitudeComparison = _agents.GetAptitudes(right).RankingScore.CompareTo(
            _agents.GetAptitudes(left).RankingScore);
        return aptitudeComparison != 0 ? aptitudeComparison : left.CompareTo(right);
    }

    private int CompareAgentsByWealth(int left, int right)
    {
        var wealthComparison = _agents.SpiritStones[right].CompareTo(_agents.SpiritStones[left]);
        return wealthComparison != 0 ? wealthComparison : left.CompareTo(right);
    }

    private void AddEvent(WorldEventKind kind, int primaryAgent, long value, int? secondaryAgent = null)
    {
        if (!IsChronicleMirroredWorldEvent(kind))
        {
            AddPersonalWorldEventLog(primaryAgent, kind, value, secondaryAgent, isPrimaryActor: true);
            if (secondaryAgent is { } secondary)
            {
                AddPersonalWorldEventLog(secondary, kind, value, primaryAgent, isPrimaryActor: false);
            }
        }

        _worldChronicle.RecordWorldEvent(Tick, kind, primaryAgent, value);

        if (!IsImportantWorldEvent(kind, primaryAgent, secondaryAgent, value))
        {
            return;
        }

        if (_events.Count == WorldLogCapacity)
        {
            _events.Dequeue();
        }

        var worldEvent = new WorldEvent(
            ++_eventSequence,
            Tick,
            kind,
            new AgentId(primaryAgent),
            secondaryAgent is null ? null : new AgentId(secondaryAgent.Value),
            value,
            _agents.Names[primaryAgent],
            secondaryAgent is { } namedSecondary ? _agents.Names[namedSecondary] : null);
        _events.Enqueue(worldEvent);
        const bool isPermanentChronicle = false;

        ImportantWorldEventRecorded?.Invoke(new WorldEventNotification(
            worldEvent,
            worldEvent.PrimaryName ?? _agents.Names[primaryAgent],
            worldEvent.SecondaryName,
            isPermanentChronicle));
    }

    private void WorldChronicle_EntryRecorded(WorldCenturyChronicleEntry entry) =>
        WorldCenturyChronicleRecorded?.Invoke(entry);

    private bool IsImportantWorldEvent(
        WorldEventKind kind,
        int primaryAgent,
        int? secondaryAgent,
        long value) => kind switch
    {
        WorldEventKind.NewLeader
            or WorldEventKind.BreakthroughSucceeded
            or WorldEventKind.MajorRealmBreakthrough
            or WorldEventKind.CultivationEntered
            or WorldEventKind.GreatPerfectionAchieved
            or WorldEventKind.Death
            or WorldEventKind.CombatDeath
            or WorldEventKind.ReincarnationEntered
            or WorldEventKind.Rebirth => true,
        WorldEventKind.BottleneckBreakthrough => _rankByAgent[primaryAgent] <= 100,
        WorldEventKind.TechniqueEnlightenment
            or WorldEventKind.SpellEnlightenment
            or WorldEventKind.TreasureCrafted
            or WorldEventKind.TreasurePurchased
            or WorldEventKind.TechniqueBookPurchased
            or WorldEventKind.SpellBookPurchased => value >= 9_000,
        // 전투 결과는 경쟁 관찰 게임의 핵심 세계 사건이므로 모두 유지하되,
        // 고정 용량 세계 로그가 오래된 결과를 자동으로 밀어낸다.
        WorldEventKind.CombatEnded => true,
        _ => false,
    };

    private static bool IsChronicleMirroredWorldEvent(WorldEventKind kind) => kind is
        WorldEventKind.BreakthroughFailed
        or WorldEventKind.MinorStageBreakthrough
        or WorldEventKind.BottleneckBreakthrough
        or WorldEventKind.MajorRealmBreakthrough
        or WorldEventKind.CultivationEntered
        or WorldEventKind.GreatPerfectionAchieved
        or WorldEventKind.TechniqueAcquired
        or WorldEventKind.SpellAcquired
        or WorldEventKind.Death
        or WorldEventKind.CombatDeath
        or WorldEventKind.ReincarnationEntered
        or WorldEventKind.Rebirth;

    private void AddPersonalActionLog(int index, AgentActivity activity)
    {
        var log = _agents.PersonalLogs[index];
        if (log.Count > 0)
        {
            var latest = log[^1];
            if (latest.Kind == PersonalLogKind.ActionCompleted
                && latest.Activity == activity
                && latest.LifeNumber == _agents.LifeNumbers[index])
            {
                var elapsedMonths = Math.Max(
                    1L,
                    GameCalendar.GetMonthIndex(Tick) - GameCalendar.GetMonthIndex(latest.Tick));
                log[^1] = latest with
                {
                    Tick = Tick,
                    Value = checked(Math.Max(1L, latest.Value) + elapsedMonths),
                };
                return;
            }
        }

        AddPersonalLog(
            index,
            PersonalLogKind.ActionCompleted,
            activity,
            worldEventKind: null,
            chronicleEventKind: null,
            isPrimaryActor: true,
            relatedAgent: null,
            value: 1);
    }

    private void AddPersonalWorldEventLog(
        int index,
        WorldEventKind kind,
        long value,
        int? relatedAgent,
        bool isPrimaryActor) =>
        AddPersonalLog(
            index,
            PersonalLogKind.WorldEvent,
            activity: null,
            worldEventKind: kind,
            chronicleEventKind: null,
            isPrimaryActor,
            relatedAgent,
            value);

    private void AddPersonalChronicleLog(int index, ChronicleEventKind kind, int value) =>
        AddPersonalLog(
            index,
            PersonalLogKind.Chronicle,
            activity: null,
            worldEventKind: null,
            chronicleEventKind: kind,
            isPrimaryActor: true,
            relatedAgent: null,
            value);

    private void AddPersonalLog(
        int index,
        PersonalLogKind kind,
        AgentActivity? activity,
        WorldEventKind? worldEventKind,
        ChronicleEventKind? chronicleEventKind,
        bool isPrimaryActor,
        int? relatedAgent,
        long value)
    {
        var log = _agents.PersonalLogs[index];
        if (log.Count == PersonalLogCapacity)
        {
            log.RemoveAt(0);
        }

        log.Add(new PersonalLogEntry(
            ++_personalLogSequence,
            Tick,
            _agents.LifeNumbers[index],
            kind,
            activity,
            worldEventKind,
            chronicleEventKind,
            isPrimaryActor,
            relatedAgent is null ? null : new AgentId(relatedAgent.Value),
            value));
    }

    private long GetAgeYears(int index) => GameCalendar.GetAgeYears(
        new SimulationTick(_agents.BirthTicks[index]),
        Tick);

    private static int GetQualityEffect(int rawQuality) =>
        QualityRules.GetEffectiveWhole(QualityValue.FromHundredths(rawQuality));

    private int GetDerivedQualityEffect(int index, AptitudeDerivedAbility ability) =>
        GetQualityEffect(_agents.GetDerivedAptitude(index, ability));

    private bool HasReachedMaximumLifespan(int index) =>
        GetAgeYears(index) >= RealmRules.GetProfile(_agents.RealmStages[index]).MaximumLifespanYears;

    private void BeginReincarnation(int index, bool combatDeath = false)
    {
        var opponent = _agents.Opponents[index];
        if (opponent >= 0 && _agents.Opponents[opponent] == index)
        {
            FinishCombat(opponent, index);
        }

        var age = checked((int)Math.Min(int.MaxValue, GetAgeYears(index)));
        _totalDeaths++;
        if (combatDeath)
        {
            AddChronicle(index, ChronicleEventKind.CombatDeath, age);
            AddEvent(WorldEventKind.CombatDeath, index, age);
        }
        else
        {
            AddChronicle(index, ChronicleEventKind.Death, age);
            AddEvent(WorldEventKind.Death, index, age);
        }
        AddChronicle(index, ChronicleEventKind.ReincarnationEntered);
        AddEvent(WorldEventKind.ReincarnationEntered, index, _agents.LifeNumbers[index]);
        _agents.Activities[index] = AgentActivity.Reincarnating;
        _agents.Opponents[index] = -1;
        _agents.Health[index] = 0;
        _agents.Qi[index] = 0;
        ScheduleAgent(index, checked(Tick.Value + GameCalendar.TicksPerYear));
    }

    private void CompleteRebirth(int index)
    {
        _agents.Rebirth(index, _random, Tick.Value);
        _totalReincarnations++;
        AddChronicle(index, ChronicleEventKind.Rebirth);
        AddEvent(WorldEventKind.Rebirth, index, _agents.LifeNumbers[index]);
        ScheduleAgent(index, _agents.NextDecisionTicks[index]);
    }

    private void AddChronicle(int index, ChronicleEventKind kind, int value = 0)
    {
        _agents.Chronicles[index].Add(new LifeChronicleEntry(
            ++_chronicleSequence,
            Tick,
            _agents.LifeNumbers[index],
            GetAgeYears(index),
            _agents.Names[index],
            kind,
            _agents.RealmStages[index],
            (MinorStage)_agents.RealmLayers[index],
            value));
        AddPersonalChronicleLog(index, kind, value);
    }

    private static int EncodeRealm(RealmStage stage, int minorStage) =>
        ((int)stage * 10) + minorStage;

    private void ScheduleAgent(int index, long tick)
    {
        var scheduledTick = new SimulationTick(tick);
        if (GameCalendar.GetMonthIndex(scheduledTick) <= _agents.LastActionMonths[index])
        {
            scheduledTick = GameCalendar.GetMonthStart(
                checked(_agents.LastActionMonths[index] + 1));
        }

        _agents.NextDecisionTicks[index] = scheduledTick.Value;
        _decisionSchedule.Enqueue(index, (scheduledTick.Value, index));
    }

    private int ValidateAgent(AgentId agentId)
    {
        if ((uint)agentId.Value >= (uint)Population)
        {
            throw new ArgumentOutOfRangeException(nameof(agentId));
        }

        return agentId.Value;
    }

    private static void Add(ref ulong hash, ulong value)
    {
        for (var shift = 0; shift < 64; shift += 8)
        {
            hash ^= (byte)(value >> shift);
            hash *= 1099511628211UL;
        }
    }

    private static void AddString(ref ulong hash, string value)
    {
        foreach (var character in value)
        {
            Add(ref hash, character);
        }

        Add(ref hash, 0xFFFFUL);
    }

    private static void AddWorldCenturyChronicleHash(
        ref ulong hash,
        WorldCenturyChronicleCheckpoint checkpoint)
    {
        Add(ref hash, (ulong)checkpoint.Sequence);
        foreach (var entry in checkpoint.Entries)
        {
            Add(ref hash, (ulong)entry.Sequence);
            Add(ref hash, (ulong)entry.CenturyNumber);
            Add(ref hash, (ulong)entry.StartYear);
            Add(ref hash, (ulong)entry.EndYear);
            AddArray(ref hash, entry.RealmBreakthroughCounts);
            AddArray(ref hash, entry.RealmBreakthroughUniqueCounts);
            AddArray(ref hash, entry.CombatPowerPositionChanges);
            AddArray(ref hash, entry.RealmPositionChanges);
            AddArray(ref hash, entry.AptitudePositionChanges);
            AddArray(ref hash, entry.WealthPositionChanges);
            AddLeader(ref hash, entry.CombatPowerLeader);
            AddLeader(ref hash, entry.RealmLeader);
            AddLeader(ref hash, entry.AptitudeLeader);
            AddLeader(ref hash, entry.WealthLeader);
            Add(ref hash, (ulong)entry.DeathCount);
            Add(ref hash, (ulong)entry.UniqueDeathCount);
            Add(ref hash, (ulong)entry.ReincarnationEntryCount);
            Add(ref hash, (ulong)entry.UniqueReincarnationEntryCount);
            Add(ref hash, (ulong)entry.RebirthCount);
            Add(ref hash, (ulong)entry.UniqueRebirthCount);
            Add(ref hash, (ulong)entry.ReincarnationRiverResidentsAtEnd);
            Add(ref hash, (ulong)entry.CombatsCompleted);
            Add(ref hash, (ulong)entry.MonsterHunts);
            Add(ref hash, (ulong)entry.MissionsCompleted);
            Add(ref hash, (ulong)entry.SpiritStonesMined);
            Add(ref hash, (ulong)entry.TreasuresCrafted);
            Add(ref hash, (ulong)entry.PillsCrafted);
        }

        var current = checkpoint.Current;
        Add(ref hash, (ulong)current.CenturyIndex);
        AddArray(ref hash, current.RealmBreakthroughCounts);
        AddArray(ref hash, current.RealmBreakthroughUniqueCounts);
        foreach (var bits in current.RealmBreakthroughAgentBits)
        {
            AddArray(ref hash, bits);
        }
        AddArray(ref hash, current.CombatPowerPositionChanges);
        AddArray(ref hash, current.RealmPositionChanges);
        AddArray(ref hash, current.AptitudePositionChanges);
        AddArray(ref hash, current.WealthPositionChanges);
        AddArray(ref hash, current.PreviousCombatPowerTop);
        AddArray(ref hash, current.PreviousRealmTop);
        AddArray(ref hash, current.PreviousAptitudeTop);
        AddArray(ref hash, current.PreviousWealthTop);
        AddLeader(ref hash, current.CombatPowerLeader);
        AddLeader(ref hash, current.RealmLeader);
        AddLeader(ref hash, current.AptitudeLeader);
        AddLeader(ref hash, current.WealthLeader);
        Add(ref hash, unchecked((ulong)current.LastRankingSampleYear));
        Add(ref hash, (ulong)current.DeathCount);
        Add(ref hash, (ulong)current.UniqueDeathCount);
        AddArray(ref hash, current.DeathAgentBits);
        Add(ref hash, (ulong)current.ReincarnationEntryCount);
        Add(ref hash, (ulong)current.UniqueReincarnationEntryCount);
        AddArray(ref hash, current.ReincarnationEntryAgentBits);
        Add(ref hash, (ulong)current.RebirthCount);
        Add(ref hash, (ulong)current.UniqueRebirthCount);
        AddArray(ref hash, current.RebirthAgentBits);
        Add(ref hash, (ulong)current.ReincarnationRiverResidents);
        Add(ref hash, (ulong)current.CombatsCompleted);
        Add(ref hash, (ulong)current.MonsterHunts);
        Add(ref hash, (ulong)current.MissionsCompleted);
        Add(ref hash, (ulong)current.SpiritStonesMined);
        Add(ref hash, (ulong)current.TreasuresCrafted);
        Add(ref hash, (ulong)current.PillsCrafted);
    }

    private static void AddLeader(ref ulong hash, WorldChronicleLeader leader)
    {
        Add(ref hash, (ulong)(leader.AgentId.Value + 1));
        AddString(ref hash, leader.Name ?? string.Empty);
    }

    private static void AddArray(ref ulong hash, IEnumerable<int> values)
    {
        foreach (var value in values)
        {
            Add(ref hash, unchecked((ulong)(long)value));
        }
    }

    private static void AddArray(ref ulong hash, IEnumerable<long> values)
    {
        foreach (var value in values)
        {
            Add(ref hash, unchecked((ulong)value));
        }
    }

    private static void AddArray(ref ulong hash, IEnumerable<ulong> values)
    {
        foreach (var value in values)
        {
            Add(ref hash, value);
        }
    }
}
