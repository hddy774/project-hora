using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private const int EventCapacity = 512;
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
    private readonly Queue<WorldEvent> _events = new(EventCapacity);
    private long _eventSequence;
    private long _chronicleSequence;
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
    private long _lastRankingRefreshTick;
    private long _rankingMovementFromTick;
    private int _rankingRisenCount;
    private int _rankingFallenCount;
    private int _rankingUnchangedCount;
    private long _rankingTotalAbsoluteMovement;

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

        for (var index = 0; index < population; index++)
        {
            _agents.Initialize(index, _random);
            AddChronicle(index, ChronicleEventKind.Birth);
            _agentByRank[index] = index;
            _agentByRealmRank[index] = index;
            _agentByAptitudeRank[index] = index;
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
            var alreadyScheduled = ResolveActivity(agentIndex);
            TryResolveBottleneckAfterAction(agentIndex, resolvedActivity);
            if (alreadyScheduled)
            {
                continue;
            }

            SelectNextActivity(agentIndex);
            _agents.RecalculatePower(agentIndex);
            var nextTick = checked(dueTick + CalculateActivityDuration(agentIndex));
            ScheduleAgent(agentIndex, nextTick);
        }

        Tick = targetTick;
        RefreshRankings(recordLeaderChange: true);
    }

    public AgentSnapshot GetAgentSnapshot(AgentId agentId)
    {
        var index = ValidateAgent(agentId);
        return new AgentSnapshot(
            agentId,
            _agents.Names[index],
            _rankByAgent[index],
            _realmRankByAgent[index],
            _aptitudeRankByAgent[index],
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
            new QualityValue(_agents.SpellQualities[index]),
            _agents.SpellMasteries[index],
            _agents.CombatExperience[index],
            _agents.Wins[index],
            _agents.Losses[index],
            _agents.Health[index],
            _agents.MaximumHealth[index],
            _agents.Qi[index],
            _agents.MaximumQi[index],
            _agents.Activities[index],
            new SimulationTick(_agents.NextDecisionTicks[index]),
            _agents.Opponents[index] < 0 ? null : new AgentId(_agents.Opponents[index]),
            new SimulationTick(_agents.LastCombatTicks[index]),
            _agents.ChallengesIssued[index],
            _agents.ActionsTaken[index],
            _agents.LastActionMonths[index] < 0
                ? null
                : GameCalendar.GetDate(new SimulationTick(_agents.LastActionTicks[index])),
            _agents.SpiritStones[index],
            _agents.GrowthPills[index],
            _agents.BreakthroughPills[index],
            _agents.MissionsCompleted[index],
            GetActiveMissionSnapshot(index),
            _agents.GetInventoryItemCount(index),
            GetEquippedTreasureSnapshots(index),
            _agents.Power[index]);
    }

    public WorldSnapshot CreateSnapshot(int rankingCount = 1_000, int eventCount = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rankingCount);
        ArgumentOutOfRangeException.ThrowIfNegative(eventCount);
        var actualRankingCount = Math.Min(rankingCount, Population);
        var combatPowerRankings = CreateRankingEntries(
            RankingCategory.CombatPower,
            _agentByRank,
            actualRankingCount);
        var realmRankings = CreateRankingEntries(
            RankingCategory.Realm,
            _agentByRealmRank,
            actualRankingCount);
        var aptitudeRankings = CreateRankingEntries(
            RankingCategory.Aptitude,
            _agentByAptitudeRank,
            actualRankingCount);
        var statistics = CreateStatistics();
        var market = _market.CreateSnapshot();

        var recentEvents = _events.Skip(Math.Max(0, _events.Count - eventCount)).ToArray();
        var agentNames = new Dictionary<int, string>((actualRankingCount * 3) + (recentEvents.Length * 2));
        foreach (var ranking in combatPowerRankings.Concat(realmRankings).Concat(aptitudeRankings))
        {
            agentNames[ranking.AgentId.Value] = ranking.Name;
        }

        foreach (var worldEvent in recentEvents)
        {
            agentNames[worldEvent.PrimaryAgent.Value] = _agents.Names[worldEvent.PrimaryAgent.Value];
            if (worldEvent.SecondaryAgent is { } secondaryAgent)
            {
                agentNames[secondaryAgent.Value] = _agents.Names[secondaryAgent.Value];
            }
        }

        return new WorldSnapshot(
            Tick,
            Population,
            combatPowerRankings,
            realmRankings,
            aptitudeRankings,
            statistics,
            market,
            agentNames,
            recentEvents,
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
                RankingCategory.Realm =>
                    ((((long)(int)_agents.RealmStages[agentIndex] * RealmRules.MinorStageCount)
                        + _agents.RealmLayers[agentIndex]) * 1_000_000_000L)
                    + _agents.CultivationProgress[agentIndex],
                RankingCategory.Aptitude => aptitudeScore,
                _ => throw new ArgumentOutOfRangeException(nameof(category)),
            };

            entries[offset] = new RankingEntry(
                category,
                offset + 1,
                new AgentId(agentIndex),
                _agents.Names[agentIndex],
                _agents.RealmStages[agentIndex],
                _agents.RealmLayers[agentIndex],
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
        Add(ref hash, (ulong)_lastRankingRefreshTick);
        Add(ref hash, (ulong)_rankingMovementFromTick);
        Add(ref hash, (ulong)_rankingRisenCount);
        Add(ref hash, (ulong)_rankingFallenCount);
        Add(ref hash, (ulong)_rankingUnchangedCount);
        Add(ref hash, (ulong)_rankingTotalAbsoluteMovement);
        var market = _market.CreateCheckpoint();
        Add(ref hash, (ulong)market.TreasurySpiritStones);
        Add(ref hash, (ulong)market.TreasureQualityHundredths);
        for (var goodId = 0; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            Add(ref hash, (ulong)market.Stock[goodId]);
            Add(ref hash, (ulong)market.Prices[goodId]);
            Add(ref hash, (ulong)market.Supply[goodId]);
            Add(ref hash, (ulong)market.Demand[goodId]);
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
            Add(ref hash, (ulong)_agents.BirthTicks[index]);
            Add(ref hash, (ulong)_agents.LifeNumbers[index]);
            Add(ref hash, _agents.BottleneckFailures[index]);
            Add(ref hash, (byte)_agents.RealmStages[index]);
            Add(ref hash, _agents.RealmLayers[index]);
            Add(ref hash, (ulong)_agents.CultivationProgress[index]);
            Add(ref hash, (byte)_agents.CultivationStates[index]);
            Add(ref hash, (byte)_agents.CultivationBottleneckCauses[index]);
            Add(ref hash, _agents.TechniqueQualities[index]);
            Add(ref hash, _agents.TechniqueMasteries[index]);
            Add(ref hash, _agents.SpellQualities[index]);
            Add(ref hash, _agents.SpellMasteries[index]);
            Add(ref hash, (ulong)_agents.CombatExperience[index]);
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
            Add(ref hash, _agents.CombatActions[index]);
            Add(ref hash, _agents.Guarding[index] ? 1UL : 0UL);
            Add(ref hash, (ulong)_agents.ChallengesIssued[index]);
            Add(ref hash, (ulong)_agents.SpiritStones[index]);
            Add(ref hash, (ulong)_agents.GrowthPills[index]);
            Add(ref hash, (ulong)_agents.BreakthroughPills[index]);
            Add(ref hash, (ulong)_agents.MissionsCompleted[index]);
            Add(ref hash, _agents.ActiveMissionMaterials[index]);
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
        }

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
            default:
                throw new InvalidOperationException("알 수 없는 AI 행동입니다.");
        }
    }

    private void ResolveCultivation(int index)
    {
        switch (_agents.CultivationStates[index])
        {
            case CultivationState.Completed:
                _agents.CultivationProgress[index] = GetCultivationRequirement(index);
                return;
            case CultivationState.Stabilizing:
                _agents.CultivationStates[index] = RealmRules.IsFinalStage(
                    _agents.RealmStages[index],
                    _agents.RealmLayers[index])
                    ? CultivationState.Completed
                    : CultivationState.Progressing;
                return;
            case CultivationState.PreparingBreakthrough:
                _agents.CultivationStates[index] = CultivationState.PreflightReady;
                return;
            case CultivationState.PreflightReady:
                ResolveBreakthroughAttempt(index);
                return;
            case CultivationState.Bottleneck:
                return;
            case CultivationState.AttemptingBreakthrough:
                throw new InvalidOperationException("돌파 시도 상태는 하나의 행동 안에서 종료되어야 합니다.");
            case CultivationState.Progressing:
                AccumulateCultivationWork(index);
                return;
            default:
                throw new InvalidOperationException("알 수 없는 수행 상태입니다.");
        }
    }

    private void AccumulateCultivationWork(int index)
    {
        var spiritualRoot = GetQualityEffect(_agents.SpiritualRoots[index]);
        var comprehension = GetQualityEffect(_agents.Comprehensions[index]);
        var mind = GetQualityEffect(_agents.Minds[index]);
        var soul = GetQualityEffect(_agents.Souls[index]);
        var technique = GetQualityEffect(_agents.TechniqueQualities[index]);
        var rateBasisPoints = Math.Clamp(
            10_000
            + ((spiritualRoot - 50) * 40)
            + ((comprehension - 50) * 40)
            + ((mind - 50) * 20)
            + ((soul - 50) * 10)
            + ((technique - 50) * 10),
            3_000,
            18_000);
        var actionTicks = CalculateActivityDuration(index);
        var gain = Math.Max(1, checked((int)(actionTicks * rateBasisPoints / 10_000)));
        if (_agents.GrowthPills[index] > 0)
        {
            _agents.GrowthPills[index]--;
            gain = checked(gain + Math.Max(1, gain / 2));
        }

        var requirement = GetCultivationRequirement(index);
        _agents.CultivationProgress[index] = Math.Min(
            requirement,
            checked(_agents.CultivationProgress[index] + gain));
        if (_agents.CultivationProgress[index] < requirement)
        {
            return;
        }

        _agents.CultivationStates[index] = RealmRules.IsFinalStage(
            _agents.RealmStages[index],
            _agents.RealmLayers[index])
            ? CultivationState.Completed
            : CultivationState.PreparingBreakthrough;
    }

    private void ResolveBreakthroughAttempt(int index)
    {
        var preflightFailure = EvaluateBreakthroughPreflight(index);
        if (preflightFailure != CultivationBottleneckCause.None)
        {
            _agents.CultivationStates[index] = CultivationState.Bottleneck;
            _agents.CultivationBottleneckCauses[index] = preflightFailure;
            return;
        }

        _agents.CultivationStates[index] = CultivationState.AttemptingBreakthrough;
        var spiritualRoot = GetQualityEffect(_agents.SpiritualRoots[index]);
        var comprehension = GetQualityEffect(_agents.Comprehensions[index]);
        var mind = GetQualityEffect(_agents.Minds[index]);
        var soul = GetQualityEffect(_agents.Souls[index]);
        var stagePenalty = ((int)_agents.RealmStages[index] * 550) + (_agents.RealmLayers[index] * 80);
        var constitution = GetQualityEffect(_agents.Constitutions[index]);
        var breakthroughPillBonus = 0;
        if (_agents.BreakthroughPills[index] > 0)
        {
            _agents.BreakthroughPills[index]--;
            breakthroughPillBonus = 1_500;
        }

        var chance = Math.Clamp(
            4_500
            + (comprehension * 30)
            + (spiritualRoot * 20)
            + (constitution * 10)
            + (mind * 10)
            + (soul * 5)
            + breakthroughPillBonus
            - stagePenalty,
            1_200,
            9_500);

        if (_random.Chance(chance))
        {
            _agents.CultivationProgress[index] = 0;
            var previousRealm = _agents.RealmStages[index];
            var previousMinorStage = (MinorStage)_agents.RealmLayers[index];
            var bottleneckFailures = _agents.BottleneckFailures[index];
            AdvanceRealm(index);
            _agents.CultivationStates[index] = CultivationState.Stabilizing;
            _agents.CultivationBottleneckCauses[index] = CultivationBottleneckCause.None;
            if (bottleneckFailures > 0)
            {
                AddChronicle(index, ChronicleEventKind.BottleneckBreakthrough, bottleneckFailures);
                AddEvent(WorldEventKind.BottleneckBreakthrough, index, bottleneckFailures);
            }

            if (_agents.RealmStages[index] != previousRealm)
            {
                AddChronicle(index, ChronicleEventKind.MajorRealmBreakthrough);
                AddEvent(
                    WorldEventKind.MajorRealmBreakthrough,
                    index,
                    EncodeRealm(_agents.RealmStages[index], _agents.RealmLayers[index]));
            }
            else if ((MinorStage)_agents.RealmLayers[index] != previousMinorStage)
            {
                AddChronicle(index, ChronicleEventKind.MinorStageBreakthrough);
                AddEvent(
                    WorldEventKind.MinorStageBreakthrough,
                    index,
                    EncodeRealm(_agents.RealmStages[index], _agents.RealmLayers[index]));
            }

            _agents.BottleneckFailures[index] = 0;
            _totalBreakthroughs++;
            return;
        }

        if (_agents.BottleneckFailures[index] == 0)
        {
            AddChronicle(index, ChronicleEventKind.BreakthroughFailure, chance);
        }

        _agents.BottleneckFailures[index] = checked((ushort)(_agents.BottleneckFailures[index] + 1));
        _agents.CultivationStates[index] = CultivationState.Bottleneck;
        _agents.CultivationBottleneckCauses[index] = SelectFailureBottleneckCause(index);
        var requirement = GetCultivationRequirement(index);
        _agents.CultivationProgress[index] = requirement * 3 / 4;
        _agents.Health[index] = Math.Max(1, _agents.Health[index] - (_agents.MaximumHealth[index] / 8));
        AddEvent(WorldEventKind.BreakthroughFailed, index, chance);
    }

    private CultivationBottleneckCause EvaluateBreakthroughPreflight(int index)
    {
        if (_agents.CultivationProgress[index] < GetCultivationRequirement(index))
        {
            return CultivationBottleneckCause.Stability;
        }

        if (_agents.Health[index] * 2 < _agents.MaximumHealth[index])
        {
            return CultivationBottleneckCause.Body;
        }

        if (_agents.Qi[index] * 4 < _agents.MaximumQi[index])
        {
            return CultivationBottleneckCause.SpiritualRoot;
        }

        return CultivationBottleneckCause.None;
    }

    private CultivationBottleneckCause SelectFailureBottleneckCause(int index)
    {
        var cause = CultivationBottleneckCause.Knowledge;
        var score = (GetQualityEffect(_agents.Comprehensions[index])
            + GetQualityEffect(_agents.TechniqueQualities[index])) / 2;

        ConsiderBottleneckCause(
            CultivationBottleneckCause.Body,
            (GetQualityEffect(_agents.Bones[index])
                + GetQualityEffect(_agents.Constitutions[index])) / 2,
            ref cause,
            ref score);
        ConsiderBottleneckCause(
            CultivationBottleneckCause.SpiritualRoot,
            GetQualityEffect(_agents.SpiritualRoots[index]),
            ref cause,
            ref score);
        var breakthroughPillPrice = _market.GetPrice(EconomyRules.BreakthroughPillGoodId);
        var resourceScore = _agents.BreakthroughPills[index] > 0
            ? int.MaxValue
            : _agents.SpiritStones[index] >= breakthroughPillPrice ? 60 : 20;
        ConsiderBottleneckCause(
            CultivationBottleneckCause.Resources,
            resourceScore,
            ref cause,
            ref score);
        ConsiderBottleneckCause(
            CultivationBottleneckCause.Stability,
            (GetQualityEffect(_agents.Minds[index]) + GetQualityEffect(_agents.Souls[index])) / 2,
            ref cause,
            ref score);
        return cause;
    }

    private static void ConsiderBottleneckCause(
        CultivationBottleneckCause candidate,
        int candidateScore,
        ref CultivationBottleneckCause selected,
        ref int selectedScore)
    {
        if (candidateScore < selectedScore)
        {
            selected = candidate;
            selectedScore = candidateScore;
        }
    }

    private void ResolveTechniqueTraining(int index)
    {
        var gain = 4
            + (GetQualityEffect(_agents.Comprehensions[index]) / 8)
            + (GetQualityEffect(_agents.Bones[index]) / 12)
            + (GetQualityEffect(_agents.Minds[index]) / 20);
        var mastery = _agents.TechniqueMasteries[index] + gain;
        var currentQuality = new QualityValue(_agents.TechniqueQualities[index]);
        if (mastery >= 1_000 && currentQuality.Tier < QualityTier.Flawless)
        {
            var nextQuality = QualityRules.GetNextTierMinimum(currentQuality);
            _agents.TechniqueQualities[index] = nextQuality.Hundredths;
            mastery = 250;
            AddChronicle(index, ChronicleEventKind.TechniqueEnlightenment, nextQuality.Hundredths);
            AddEvent(WorldEventKind.TechniqueEnlightenment, index, nextQuality.Hundredths);
        }

        _agents.TechniqueMasteries[index] = (ushort)Math.Min(1_000, mastery);
    }

    private void ResolveSpellTraining(int index)
    {
        var gain = 4
            + (GetQualityEffect(_agents.Comprehensions[index]) / 7)
            + (GetQualityEffect(_agents.SpiritualRoots[index]) / 14)
            + (GetQualityEffect(_agents.Minds[index]) / 18);
        var mastery = _agents.SpellMasteries[index] + gain;
        var currentQuality = new QualityValue(_agents.SpellQualities[index]);
        if (mastery >= 1_000 && currentQuality.Tier < QualityTier.Flawless)
        {
            var nextQuality = QualityRules.GetNextTierMinimum(currentQuality);
            _agents.SpellQualities[index] = nextQuality.Hundredths;
            mastery = 200;
            AddChronicle(index, ChronicleEventKind.SpellEnlightenment, nextQuality.Hundredths);
            AddEvent(WorldEventKind.SpellEnlightenment, index, nextQuality.Hundredths);
        }

        _agents.SpellMasteries[index] = (ushort)Math.Min(1_000, mastery);
    }

    private void ResolveRecovery(int index)
    {
        var healthGain = 20 + (GetQualityEffect(_agents.Constitutions[index]) * 4);
        var qiGain = 15 + (GetQualityEffect(_agents.SpiritualRoots[index]) * 5);
        _agents.Health[index] = Math.Min(_agents.MaximumHealth[index], _agents.Health[index] + healthGain);
        _agents.Qi[index] = Math.Min(_agents.MaximumQi[index], _agents.Qi[index] + qiGain);
    }

    private void TryResolveBottleneckAfterAction(int index, AgentActivity resolvedActivity)
    {
        if (_agents.CultivationStates[index] != CultivationState.Bottleneck)
        {
            return;
        }

        var resolved = _agents.CultivationBottleneckCauses[index] switch
        {
            CultivationBottleneckCause.Knowledge =>
                resolvedActivity is AgentActivity.TrainingTechnique or AgentActivity.TrainingSpell,
            CultivationBottleneckCause.Body =>
                resolvedActivity == AgentActivity.Recovering
                && _agents.Health[index] * 100 >= _agents.MaximumHealth[index] * 80,
            CultivationBottleneckCause.SpiritualRoot =>
                resolvedActivity == AgentActivity.Recovering
                && _agents.Qi[index] * 100 >= _agents.MaximumQi[index] * 80,
            CultivationBottleneckCause.Resources => _agents.BreakthroughPills[index] > 0,
            CultivationBottleneckCause.Stability =>
                resolvedActivity == AgentActivity.Recovering
                && _agents.Health[index] * 100 >= _agents.MaximumHealth[index] * 80
                && _agents.Qi[index] * 100 >= _agents.MaximumQi[index] * 80,
            _ => false,
        };
        if (!resolved)
        {
            return;
        }

        _agents.CultivationProgress[index] = GetCultivationRequirement(index);
        _agents.CultivationStates[index] = CultivationState.PreparingBreakthrough;
        _agents.CultivationBottleneckCauses[index] = CultivationBottleneckCause.None;
    }

    private void SelectNextActivity(int index)
    {
        if (_agents.Activities[index] == AgentActivity.Fighting)
        {
            return;
        }

        if (_agents.Health[index] * 100 < _agents.MaximumHealth[index] * 70
            || _agents.Qi[index] * 100 < _agents.MaximumQi[index] * 35)
        {
            _agents.Activities[index] = AgentActivity.Recovering;
            return;
        }

        switch (_agents.CultivationStates[index])
        {
            case CultivationState.PreparingBreakthrough:
            case CultivationState.PreflightReady:
            case CultivationState.Stabilizing:
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
                        _agents.SpiritStones[index] >= _market.GetPrice(EconomyRules.BreakthroughPillGoodId)
                            ? AgentActivity.TradingInCity
                            : AgentActivity.DoingCityMission,
                    CultivationBottleneckCause.Stability => AgentActivity.Recovering,
                    _ => AgentActivity.Cultivating,
                };
                return;
        }

        var rank = _rankByAgent[index];
        var sinceLastCombat = Tick.Value - _agents.LastCombatTicks[index];
        var challengeChance = sinceLastCombat > 700 ? 58 : 34;
        if (rank > 1 && Tick.Value >= 60 && _random.NextInt(100) < challengeChance)
        {
            _agents.Activities[index] = AgentActivity.SeekingChallenge;
            return;
        }

        var roll = _random.NextInt(100);
        var activity = SelectNonCombatActivity(index, roll);
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
            AgentActivity.Cultivating => Math.Max(30, 145 - ((GetQualityEffect(_agents.SpiritualRoots[index]) + GetQualityEffect(_agents.Comprehensions[index])) / 2)),
            AgentActivity.TrainingTechnique => Math.Max(34, 150 - ((GetQualityEffect(_agents.Bones[index]) + GetQualityEffect(_agents.Comprehensions[index])) / 2)),
            AgentActivity.TrainingSpell => Math.Max(34, 150 - ((GetQualityEffect(_agents.SpiritualRoots[index]) + GetQualityEffect(_agents.Comprehensions[index])) / 2)),
            AgentActivity.Recovering => Math.Max(24, 120 - GetQualityEffect(_agents.Constitutions[index])),
            AgentActivity.SeekingChallenge => 20,
            AgentActivity.Fighting => CalculateCombatActionDuration(index),
            AgentActivity.Reincarnating => GameCalendar.TicksPerYear,
            AgentActivity.DoingCityMission => 80,
            AgentActivity.TradingInCity => 45,
            AgentActivity.CraftingTreasure => 110,
            AgentActivity.CraftingPill => 90,
            _ => throw new InvalidOperationException("알 수 없는 AI 행동입니다."),
        };
    }

    private int GetCultivationRequirement(int index)
    {
        return RealmRules.GetMinorStageRequiredWork(_agents.RealmStages[index]);
    }

    private void AdvanceRealm(int index)
    {
        if (_agents.RealmLayers[index] < RealmRules.MinorStageCount)
        {
            _agents.RealmLayers[index]++;
            return;
        }

        if (_agents.RealmStages[index] < RealmStage.HalfImmortal)
        {
            _agents.RealmStages[index]++;
            _agents.RealmLayers[index] = 1;
        }
    }

    private void RefreshRankings(bool recordLeaderChange)
    {
        var previousLeader = _agentByRank[0];
        if (recordLeaderChange)
        {
            _rankingRisenCount = 0;
            _rankingFallenCount = 0;
            _rankingUnchangedCount = 0;
            _rankingTotalAbsoluteMovement = 0;
        }

        Array.Sort(_agentByRank, CompareAgentsByPower);
        Array.Sort(_agentByRealmRank, CompareAgentsByRealm);
        Array.Sort(_agentByAptitudeRank, CompareAgentsByAptitude);
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
        }

        if (recordLeaderChange)
        {
            _rankingMovementFromTick = _lastRankingRefreshTick;
            _lastRankingRefreshTick = Tick.Value;
            _rankingUnchangedCount = Population - _rankingRisenCount - _rankingFallenCount;
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

    private void AddEvent(WorldEventKind kind, int primaryAgent, long value, int? secondaryAgent = null)
    {
        if (_events.Count == EventCapacity)
        {
            _events.Dequeue();
        }

        _events.Enqueue(new WorldEvent(
            ++_eventSequence,
            Tick,
            kind,
            new AgentId(primaryAgent),
            secondaryAgent is null ? null : new AgentId(secondaryAgent.Value),
            value));
    }

    private long GetAgeYears(int index) => GameCalendar.GetAgeYears(
        new SimulationTick(_agents.BirthTicks[index]),
        Tick);

    private static int GetQualityEffect(ushort rawQuality) =>
        QualityRules.GetEffectiveWhole(new QualityValue(rawQuality));

    private bool HasReachedMaximumLifespan(int index) =>
        GetAgeYears(index) >= RealmRules.GetProfile(_agents.RealmStages[index]).MaximumLifespanYears;

    private void BeginReincarnation(int index)
    {
        var opponent = _agents.Opponents[index];
        if (opponent >= 0 && _agents.Opponents[opponent] == index)
        {
            FinishCombat(opponent, index);
        }

        var age = checked((int)Math.Min(int.MaxValue, GetAgeYears(index)));
        _totalDeaths++;
        AddChronicle(index, ChronicleEventKind.Death, age);
        AddEvent(WorldEventKind.Death, index, age);
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
}
