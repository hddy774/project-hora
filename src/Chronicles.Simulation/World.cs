using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    public const int WorldLogCapacity = 100;
    public const int PersonalLogCapacity = 100;
    private readonly DeterministicRandom _random;
    private readonly AgentStore _agents;
    private readonly AgentCausalStore _causal;
    private readonly SparseRelationStore _relations;
    private readonly ReincarnationStore _reincarnation;
    private readonly SpatialAuthorityStore _atlas;
    private readonly OrganizationStore _organizations;
    private readonly ContentAuthorityStore _content;
    private readonly CityMarket _market;
    private readonly WorldBalanceConfiguration _balance;
    private readonly PriorityQueue<int, (long Tick, int Agent)> _decisionSchedule = new();
    private readonly int[] _agentByRank;
    private readonly int[] _rankByAgent;
    private readonly int[] _agentByRealmRank;
    private readonly int[] _realmRankByAgent;
    private readonly int[] _agentByAptitudeRank;
    private readonly int[] _aptitudeRankByAgent;
    private readonly int[] _agentByWealthRank;
    private readonly int[] _wealthRankByAgent;
    private readonly RankingRevisionCache _combatPowerRankingCache = new();
    private readonly RankingRevisionCache _realmRankingCache = new();
    private readonly RankingRevisionCache _aptitudeRankingCache = new();
    private readonly RankingRevisionCache _wealthRankingCache = new();
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

    private World(ulong seed, int population, WorldBalanceConfiguration balance)
    {
        WorldBalanceRules.Validate(balance);
        Seed = seed;
        _balance = balance;
        _random = new DeterministicRandom(seed);
        _agents = new AgentStore(population);
        _causal = new AgentCausalStore(population);
        _relations = new SparseRelationStore(population);
        _reincarnation = new ReincarnationStore(population);
        _atlas = new SpatialAuthorityStore(population);
        _organizations = new OrganizationStore(population);
        _content = ContentAuthorityStore.CreateInitial(seed);
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
            _causal.StartLife(
                index,
                CreateCausalObservation(index, initialRank: index + 1),
                _agents.DecisionTraces[index],
                AgentMemoryKind.Birth);
            _reincarnation.InitializeFirstLife(
                index,
                _agents.SoulIdentityIds[index],
                _agents.BodyIdentityIds[index],
                _agents.PersonIdentityIds[index],
                _agents.Names[index],
                _agents.BirthTicks[index],
                _agents.RealmStages[index],
                _agents.RealmLayers[index],
                _agents.Power[index].Total);
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
    public int TotalRelations => _relations.Count;
    public int TotalOrganizations => _organizations.Count;
    public int TotalContentInstances => _content.InstanceCount;
    public WorldBalanceConfiguration Balance => _balance;

    public static World CreateDefault(ulong seed) =>
        new(seed, ProductInfo.DefaultPopulation, WorldBalanceConfiguration.Default);

    public static World Create(ulong seed, int population) =>
        new(seed, population, WorldBalanceConfiguration.Default);

    public static World Create(
        ulong seed,
        int population,
        WorldBalanceConfiguration balance) =>
        new(seed, population, balance);

    public void AdvanceTo(SimulationTick targetTick)
    {
        if (targetTick.Value < Tick.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(targetTick), "세계 시간을 되돌릴 수 없습니다.");
        }

        // 같은 월에 수십만 개의 인물 행동이 몰려도 도시 생산과 요수 재생은 한 번만
        // 확인한다. 이 값은 호출 단위 파생 캐시이므로 권위 상태나 저장 wire에 영향을
        // 주지 않는다.
        var environmentMonth = long.MinValue;
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
            if (actionMonth != environmentMonth)
            {
                AdvanceCityIndustry(actionMonth);
                RegenerateMonsters(actionMonth);
                AdvanceOrganizationSystem(actionMonth);
                environmentMonth = actionMonth;
            }
            if (_agents.LifeStates[agentIndex] == PersonLifeState.Living
                && HasReachedMaximumLifespan(agentIndex))
            {
                BeginReincarnation(agentIndex);
                continue;
            }

            if (_agents.Activities[agentIndex] == AgentActivity.Fighting)
            {
                var opponent = _agents.Opponents[agentIndex];
                if ((uint)opponent < (uint)Population
                    && _agents.LifeStates[opponent] == PersonLifeState.Living
                    && HasReachedMaximumLifespan(opponent))
                {
                    BeginReincarnation(opponent);
                    continue;
                }
            }

            if (_agents.Activities[agentIndex] == AgentActivity.Reincarnating)
            {
                ResolveReincarnationStage(agentIndex);
                continue;
            }

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
            if (_agents.Activities[agentIndex] == AgentActivity.Cultivating)
            {
                EnsureCultivationPlan(agentIndex);
            }
            var resolvedActivity = _agents.Activities[agentIndex];
            var tracksRealmRanking = resolvedActivity == AgentActivity.Cultivating;
            var enteredCultivationBefore = tracksRealmRanking && HasEnteredCultivation(agentIndex);
            var realmRankingScoreBefore = tracksRealmRanking
                ? GetRealmRankingScore(agentIndex)
                : 0;
            var tracksWealthRanking = CanChangeSpiritStoneBalance(resolvedActivity);
            var spiritStonesBefore = tracksWealthRanking
                ? _agents.SpiritStones[agentIndex]
                : 0;
            var tracksPowerInputs = CanChangePowerInputsWithoutInternalRecalculation(
                resolvedActivity);
            var powerInputsBefore = tracksPowerInputs
                ? _agents.GetPowerInputSignature(agentIndex)
                : default;
            var alreadyScheduled = ResolveActivity(agentIndex);
            TryResolveBottleneckAfterAction(agentIndex, resolvedActivity);
            MarkActionRankingChanges(
                agentIndex,
                tracksRealmRanking,
                enteredCultivationBefore,
                realmRankingScoreBefore,
                tracksWealthRanking,
                spiritStonesBefore);
            if (tracksPowerInputs
                && powerInputsBefore != _agents.GetPowerInputSignature(agentIndex))
            {
                RecalculatePowerAndUpdatePeak(agentIndex);
            }
            if (resolvedActivity == AgentActivity.Cultivating
                && TryContinueCultivationPlan(agentIndex))
            {
                var plan = _agents.CultivationPlans[agentIndex];
                if (plan.CompletedMonths <= 1 || plan.CompletedMonths % 12 == 0)
                {
                    AddPersonalActionLog(agentIndex, resolvedActivity);
                }
                continue;
            }

            ObserveSocialInteraction(agentIndex, resolvedActivity);
            AddPersonalActionLog(agentIndex, resolvedActivity);
            if (alreadyScheduled)
            {
                continue;
            }

            SelectNextActivity(agentIndex);
            var nextTick = checked(dueTick + CalculateActivityDuration(agentIndex));
            ScheduleAgent(agentIndex, nextTick);
        }

        Tick = targetTick;
        var targetMonth = GameCalendar.GetMonthIndex(Tick);
        if (targetMonth != environmentMonth)
        {
            AdvanceCityIndustry(targetMonth);
            RegenerateMonsters(targetMonth);
        }
        RefreshRankings(recordLeaderChange: true);
        _worldChronicle.AdvanceTo(Tick);
    }

    private void AdvanceCityIndustry(long currentMonth)
    {
        // 월드 생성 경로에서만 초기 수선자 수요를 투입한다. CityMarket 단위 계약과
        // 복원된 거래 원장은 외부에서 임의 수요가 추가되지 않는다.
        _market.SeedInitialCultivatorDemand();
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
        bool includeAptitudeDetails = false,
        bool includeRelations = false,
        bool includeTeaching = false,
        bool includeCausal = false,
        CultivationProjectionSections cultivationSections = CultivationProjectionSections.Pipeline)
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
            CreateCultivationProjection(index, cultivationSections),
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
            _agents.GetPersonIdentity(index),
            _agents.GetDecisionSnapshot(index),
            includeTeaching
                ? CreateTeachingSnapshot(index)
                : null,
            includeAptitudeDetails
                ? _agents.CreateAptitudeDetailSnapshot(index)
                : null,
            includeRelations
                ? CreateRelationSnapshots(index)
                : null,
            _agents.GetLifeAuthoritySnapshot(index),
            includeCausal
                ? _causal.GetSnapshot(index)
                : null,
            _atlas.GetKnowledgeSnapshot(index, _agents.LocationIds[index]),
            _atlas.GetRoute(index));
    }

    public WorldSnapshot CreateSnapshot(
        int rankingCount = 1_000,
        int eventCount = 100,
        ObservationSections sections = ObservationSections.All,
        ushort? detailedMapLocationId = null,
        int detailedMapAgentOffset = 0,
        int worldChronicleCount = int.MaxValue,
        int contentSecretRealmOffset = 0,
        int contentSecretRealmLimit = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rankingCount);
        ArgumentOutOfRangeException.ThrowIfNegative(eventCount);
        ArgumentOutOfRangeException.ThrowIfNegative(worldChronicleCount);
        ArgumentOutOfRangeException.ThrowIfNegative(contentSecretRealmOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(contentSecretRealmLimit);
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
        var organizations = (sections & ObservationSections.Organization) != 0
            ? _organizations.CreateSnapshot(_agents.Names)
            : WorldOrganizationSnapshot.Empty;
        var content = (sections & ObservationSections.Content) != 0
            ? _content.CreateSnapshot(
                ContentDiscovererRef.Observer,
                includeHidden: _balance.DeveloperModeEnabled,
                contentSecretRealmOffset,
                contentSecretRealmLimit)
            : WorldContentSnapshot.Empty;

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
            LeaderId,
            organizations,
            content);
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
                RankingCategory.Realm => GetRealmRankingScore(agentIndex),
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

    public ulong ComputeStateHash() => ComputeStateHashCore(includeContent: true);

    /// <summary>
    /// 구 슬롯의 저장 당시 hash 계약을 검증할 때만 사용합니다. 복원된 세계의 현재
    /// canonical hash는 항상 <see cref="ComputeStateHash"/>를 사용합니다.
    /// </summary>
    public ulong ComputeStateHashForCheckpointSchema(int checkpointSchema)
    {
        if (checkpointSchema is < 7 or > WorldCheckpoint.CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(checkpointSchema));
        }
        return ComputeStateHashCore(includeContent: checkpointSchema >= 27);
    }

    private ulong ComputeStateHashCore(bool includeContent)
    {
        var hash = 14695981039346656037UL;
        Add(ref hash, Seed);
        Add(ref hash, (ulong)Tick.Value);
        Add(ref hash, _random.State);
        Add(ref hash, _balance.DeveloperModeEnabled ? 1UL : 0UL);
        Add(ref hash, checked((ulong)_balance.CultivationWorkPercent));
        Add(ref hash, checked((ulong)_balance.CombatAggressionPercent));
        Add(ref hash, checked((ulong)_balance.EconomyActivityPercent));
        Add(ref hash, checked((ulong)_balance.MonsterDangerPercent));
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
        Add(ref hash, _reincarnation.ComputeStateHash());
        Add(ref hash, _causal.ComputeStateHash());
        Add(ref hash, (ulong)_totalMissionsCompleted);
        Add(ref hash, (ulong)_totalTreasuresCrafted);
        Add(ref hash, (ulong)_totalPillsCrafted);
        Add(ref hash, (ulong)_totalMaterialsSold);
        Add(ref hash, (ulong)_totalMonsterHunts);
        Add(ref hash, (ulong)_totalMonsterVictories);
        _atlas.AddStateHash(ref hash);
        _organizations.AddStateHash(ref hash);
        if (includeContent)
        {
            Add(ref hash, _content.ComputeStateHash());
        }
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
        Add(ref hash, (ulong)market.CurrentFlowBucket);
        Add(ref hash, (ulong)market.AuthorityRevision);
        Add(ref hash, (ulong)market.NextTradeSequence);
        for (var goodId = 0; goodId < EconomyRules.MarketGoodCount; goodId++)
        {
            Add(ref hash, (ulong)market.Stock[goodId]);
            Add(ref hash, (ulong)market.Prices[goodId]);
            Add(ref hash, (ulong)market.Supply[goodId]);
            Add(ref hash, (ulong)market.Demand[goodId]);
            Add(ref hash, (ulong)(market.QualityHundredths?[goodId] ?? 0));
            Add(ref hash, (ulong)(market.RecentSupply?[goodId] ?? 0));
            Add(ref hash, (ulong)(market.RecentDemand?[goodId] ?? 0));
        }
        foreach (var value in market.SupplyWindowBuckets ?? Array.Empty<long>())
        {
            Add(ref hash, (ulong)value);
        }
        foreach (var value in market.DemandWindowBuckets ?? Array.Empty<long>())
        {
            Add(ref hash, (ulong)value);
        }
        foreach (var source in market.LastSettlementSourceByAgent ?? Array.Empty<long>())
        {
            Add(ref hash, unchecked((ulong)source));
        }
        foreach (var trade in market.RecentTrades ?? Array.Empty<MarketTradeCheckpoint>())
        {
            Add(ref hash, (ulong)trade.Sequence);
            Add(ref hash, (ulong)trade.SourceResultId);
            Add(ref hash, (ulong)trade.Tick);
            Add(ref hash, (ulong)trade.AgentId);
            Add(ref hash, (byte)trade.Kind);
            Add(ref hash, (ulong)trade.GoodId);
            Add(ref hash, (ulong)trade.Count);
            Add(ref hash, (ulong)trade.UnitPrice);
            Add(ref hash, (ulong)trade.TotalSpiritStones);
            Add(ref hash, trade.QualityHundredths);
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
        if (market.Authority is { } marketAuthority)
        {
            Add(ref hash, (ulong)marketAuthority.NextOfferId);
            Add(ref hash, (ulong)marketAuthority.NextReservationId);
            Add(ref hash, (ulong)marketAuthority.LastExpiryMonth);
            foreach (var reserved in marketAuthority.ReservedStock)
            {
                Add(ref hash, (ulong)reserved);
            }
            foreach (var offer in marketAuthority.Offers)
            {
                Add(ref hash, (ulong)offer.OfferId);
                Add(ref hash, (ulong)offer.Revision);
                Add(ref hash, (ulong)offer.GoodId);
                Add(ref hash, (byte)offer.Channel);
                Add(ref hash, (byte)offer.Side);
                Add(ref hash, (ulong)offer.CreatedMonth);
                Add(ref hash, (ulong)offer.ExpiresMonth);
                Add(ref hash, (byte)offer.Status);
            }
            foreach (var reservation in marketAuthority.Reservations)
            {
                Add(ref hash, (ulong)reservation.ReservationId);
                Add(ref hash, (ulong)reservation.OfferId);
                Add(ref hash, (ulong)reservation.AgentIndex);
                Add(ref hash, (ulong)reservation.GoodId);
                Add(ref hash, (ulong)reservation.Count);
                Add(ref hash, (ulong)reservation.UnitPrice);
                Add(ref hash, (ulong)reservation.CreatedMonth);
                Add(ref hash, (ulong)reservation.ExpiresMonth);
                Add(ref hash, (byte)reservation.Status);
                Add(ref hash, (ulong)reservation.SourceResultId);
            }
        }
        Add(ref hash, (ulong)_lastMonsterRegenerationMonth);
        foreach (var population in _monsterPopulations)
        {
            Add(ref hash, (ulong)population);
        }
        var relations = _relations.CreateCheckpoint();
        Add(ref hash, (ulong)relations.HistorySequence);
        Add(ref hash, (ulong)relations.ResultSequence);
        Add(ref hash, (ulong)relations.Relations.Length);
        foreach (var relation in relations.Relations)
        {
            Add(ref hash, relation.Id.Value);
            Add(ref hash, (ulong)relation.LeftAgent);
            Add(ref hash, (ulong)relation.RightAgent);
            Add(ref hash, relation.LeftPersonId.Value);
            Add(ref hash, relation.RightPersonId.Value);
            AddRelationMetricsHash(ref hash, relation.LeftTowardRight);
            AddRelationMetricsHash(ref hash, relation.RightTowardLeft);
            Add(ref hash, (byte)relation.Roles);
            Add(ref hash, (ulong)(relation.MasterAgent + 1));
            Add(ref hash, relation.CooperativeInteractions);
            Add(ref hash, relation.JourneyInteractions);
            Add(ref hash, relation.CombatInteractions);
            Add(ref hash, (ulong)relation.TechniqueTeachingSessions);
            Add(ref hash, (ulong)relation.SpellTeachingSessions);
            Add(ref hash, relation.TechniqueTransmissionEstablished ? 1UL : 0UL);
            Add(ref hash, relation.SpellTransmissionEstablished ? 1UL : 0UL);
            Add(ref hash, relation.TechniqueKnowledgeRef.Value);
            Add(ref hash, relation.SpellKnowledgeRef.Value);
            var relationResult = relation.LatestInteractionResult;
            Add(ref hash, relationResult.Id.Value);
            Add(ref hash, (ulong)relationResult.Sequence);
            Add(ref hash, (ulong)relationResult.Tick);
            Add(ref hash, relationResult.RelationId.Value);
            Add(ref hash, (byte)relationResult.Status);
            Add(ref hash, relationResult.InteractionKind is { } interactionKind
                ? checked((ulong)(byte)interactionKind + 1UL)
                : 0UL);
            Add(ref hash, relationResult.TeachingResult is { } teachingResult
                ? checked((ulong)(byte)teachingResult + 1UL)
                : 0UL);
            Add(ref hash, relationResult.LeftPersonId.Value);
            Add(ref hash, relationResult.RightPersonId.Value);
            Add(ref hash, relationResult.SourceEventRecordId.Value);
            foreach (var history in relation.History)
            {
                Add(ref hash, (ulong)history.Sequence);
                Add(ref hash, (ulong)history.Tick);
                Add(ref hash, (byte)history.Kind);
                Add(ref hash, (byte)history.ChangedRoles);
                Add(ref hash, history.LeftPersonId.Value);
                Add(ref hash, history.RightPersonId.Value);
                Add(ref hash, history.TeachingTarget is { } target
                    ? checked((ulong)(byte)target + 1UL)
                    : 0UL);
                Add(ref hash, history.KnowledgeRef.Value);
                Add(ref hash, history.ProvenanceId.Value);
            }
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
            Add(ref hash, _agents.SoulIdentityIds[index].Value);
            Add(ref hash, _agents.BodyIdentityIds[index].Value);
            Add(ref hash, _agents.PersonIdentityIds[index].Value);
            Add(ref hash, (byte)_agents.LifeStates[index]);
            Add(ref hash, (byte)_agents.Genders[index]);
            Add(ref hash, (byte)_agents.SoulEmbodimentStates[index]);
            Add(ref hash, (byte)_agents.BodyLifeStates[index]);
            Add(ref hash, (byte)_agents.CurrentLifePhases[index]);
            Add(ref hash, (ulong)_agents.LifeAuthorityRevisions[index]);
            var appearance = _agents.AppearanceStates[index];
            Add(ref hash, appearance.DefinitionId.Value);
            Add(ref hash, appearance.InnateBeautyHundredths);
            Add(ref hash, appearance.HeightHundredths);
            Add(ref hash, appearance.BuildHundredths);
            Add(ref hash, appearance.FacialFeatureHundredths);
            Add(ref hash, appearance.ComplexionHundredths);
            Add(ref hash, appearance.HairHundredths);
            Add(ref hash, appearance.BearingHundredths);
            Add(ref hash, unchecked((ulong)(long)appearance.PermanentModifierHundredths));
            Add(ref hash, appearance.ScarSeverityHundredths);
            Add(ref hash, (ulong)appearance.AuthorityRevision);
            var lifeTransition = _agents.LatestLifeTransitions[index];
            Add(ref hash, (byte)lifeTransition.Status);
            Add(ref hash, (byte)lifeTransition.Reason);
            Add(ref hash, lifeTransition.PersonId.Value);
            Add(ref hash, lifeTransition.BodyId.Value);
            Add(ref hash, (byte)lifeTransition.PreviousPhase);
            Add(ref hash, (byte)lifeTransition.CurrentPhase);
            Add(ref hash, (byte)lifeTransition.PreviousBodyState);
            Add(ref hash, (byte)lifeTransition.CurrentBodyState);
            Add(ref hash, (byte)lifeTransition.PreviousSoulState);
            Add(ref hash, (byte)lifeTransition.CurrentSoulState);
            Add(ref hash, (ulong)lifeTransition.Tick);
            Add(ref hash, (ulong)lifeTransition.AuthorityRevision);
            var decision = _agents.DecisionTraces[index];
            Add(ref hash, (ulong)_agents.DecisionAuthorityRevisions[index]);
            Add(ref hash, (ulong)decision.Tick);
            Add(ref hash, (ulong)decision.AuthorityRevision);
            Add(ref hash, (byte)decision.PrimaryGoal);
            Add(ref hash, (byte)decision.SupportingGoal);
            Add(ref hash, (byte)decision.SelectedActivity);
            Add(ref hash, (byte)decision.SelectedReason);
            Add(ref hash, (ulong)((int?)decision.AlternativeActivity + 1 ?? 0));
            Add(ref hash, (byte)decision.Resolution);
            Add(ref hash, (ulong)_agents.TeachingAuthorityRevisions[index]);
            AddTeachingTraceHash(ref hash, _agents.TeachingTraces[index]);
            Add(ref hash, (ulong)_agents.LearningAuthorityRevisions[index]);
            AddTeachingTraceHash(ref hash, _agents.LearningTraces[index]);
            Add(ref hash, _agents.TechniqueMisunderstandingPoints[index]);
            Add(ref hash, _agents.SpellMisunderstandingPoints[index]);
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

            if (_agents.FoundationFormations.CreateCheckpoint(index) is { } foundationFormation)
            {
                Add(ref hash, 1UL);
                Add(ref hash, (ulong)foundationFormation.MethodRevision);
                Add(ref hash, (byte)foundationFormation.CompletedSteps);
                Add(ref hash, (byte)foundationFormation.Source.Route);
                Add(ref hash, (byte)foundationFormation.Source.Kind);
                Add(ref hash, foundationFormation.Source.LocationId);
                Add(ref hash, (ulong)foundationFormation.Source.SourceRevision);
                Add(ref hash, (ulong)foundationFormation.Source.FrozenTick);
                Add(ref hash, (ulong)foundationFormation.Source.SourceCount);
                Add(ref hash, (ulong)foundationFormation.Source.RemainingSupply);
                Add(ref hash, (byte)foundationFormation.Source.Availability);
            }
            else
            {
                Add(ref hash, 0UL);
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
            Add(ref hash, (byte)trace.FormulaKind);
            Add(ref hash, (ulong)trace.AuthorityRevisionBefore);
            Add(ref hash, (ulong)trace.AuthorityRevisionAfter);
            Add(ref hash, (ulong)trace.AttemptEffectRegistryRevision);
            Add(ref hash, (ulong)trace.CandidateEffectCount);
            Add(ref hash, (ulong)trace.SelectedEffectCount);
            Add(ref hash, (byte)trace.AttemptEffectAvailability);
            Add(ref hash, (byte)trace.FailureEffect);
            Add(ref hash, (byte)trace.FailureEffectAvailability);
            Add(ref hash, (byte)trace.RngDomain);
            Add(ref hash, trace.AttemptSnapshotFrozen ? 1UL : 0UL);
            Add(ref hash, (byte)trace.FoundationProcedureSteps);
            Add(ref hash, (ulong)trace.EnvironmentFactorBasisPoints);
            Add(ref hash, (byte)trace.EnvironmentAvailability);
            Add(ref hash, (byte)trace.LawUnderstandingAvailability);
            Add(ref hash, (byte)trace.LuckFateAvailability);
            Add(ref hash, (ulong)_agents.CultivationAuthorityRevisions[index]);
            var cultivationPlan = _agents.CultivationPlans[index];
            Add(ref hash, (ulong)cultivationPlan.AuthorityRevision);
            Add(ref hash, (ulong)cultivationPlan.StartedTick);
            Add(ref hash, (ulong)cultivationPlan.PlannedEndTick);
            Add(ref hash, (ulong)cultivationPlan.PlannedMonths);
            Add(ref hash, (ulong)cultivationPlan.CompletedMonths);
            Add(ref hash, cultivationPlan.LocationId);
            Add(ref hash, (byte)cultivationPlan.Intensity);
            Add(ref hash, (byte)cultivationPlan.ResourcePolicy);
            Add(ref hash, (ulong)cultivationPlan.SpiritStoneBudget);
            Add(ref hash, (ulong)cultivationPlan.SpiritStonesConsumed);
            Add(ref hash, (ulong)cultivationPlan.GrowthPillBudget);
            Add(ref hash, (ulong)cultivationPlan.GrowthPillsConsumed);
            Add(ref hash, (byte)cultivationPlan.FormationSupport);
            Add(ref hash, cultivationPlan.FormationKnowledgeHundredths);
            Add(ref hash, cultivationPlan.FormationInstallationExperienceHundredths);
            Add(ref hash, cultivationPlan.FormationInstallationSucceeded ? 1UL : 0UL);
            Add(ref hash, (byte)cultivationPlan.TeachingRole);
            Add(ref hash, unchecked((ulong)(long)cultivationPlan.TeachingPartnerAgent));
            Add(ref hash, (ulong)cultivationPlan.FrozenTeachingBonusBasisPoints);
            Add(ref hash, (ulong)cultivationPlan.FrozenConditionFactorBasisPoints);
            Add(ref hash, cultivationPlan.IsActive ? 1UL : 0UL);
            Add(ref hash, (ulong)_agents.CultivationExperience[index]);
            Add(ref hash, _agents.FormationInstallationExperience[index]);
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
                return ResolveSeekingChallenge(index);
            case AgentActivity.Fighting:
                return ResolveCombatAction(index);
            case AgentActivity.Reincarnating:
                ResolveReincarnationStage(index);
                return true;
            case AgentActivity.DoingCityMission:
                return ResolveCityMission(index);
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
                return ResolveSpiritStoneMining(index);
            case AgentActivity.Teaching:
                ResolveTeaching(index);
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
            checked(_agents.TechniqueTrainingExperience[index] + (experienceGain * 6L)));
        _agents.TechniqueMasteries[index] = SkillProgressionRules.GetMasteryHundredths(
            _agents.TechniqueTrainingExperience[index]);
        for (var month = 0; month < 6; month++)
        {
            var understanding = _agents.TechniqueUnderstanding[index];
            if (understanding >= SkillProgressionRules.MaximumHundredths)
            {
                break;
            }

            var chance = SkillProgressionRules.GetEnlightenmentChanceBasisPoints(
                comprehension,
                understanding);
            if (!_random.Chance(chance))
            {
                continue;
            }

            _agents.TechniqueUnderstanding[index] = SkillProgressionRules.ApplyUnderstandingGain(
                understanding,
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
            checked(_agents.SpellTrainingExperience[index] + (experienceGain * 6L)));
        _agents.SpellMasteries[index] = SkillProgressionRules.GetMasteryHundredths(
            _agents.SpellTrainingExperience[index]);
        for (var month = 0; month < 6; month++)
        {
            var understanding = _agents.SpellUnderstanding[index];
            if (understanding >= SkillProgressionRules.MaximumHundredths)
            {
                break;
            }

            var chance = SkillProgressionRules.GetEnlightenmentChanceBasisPoints(
                comprehension,
                understanding);
            if (!_random.Chance(chance))
            {
                continue;
            }

            _agents.SpellUnderstanding[index] = SkillProgressionRules.ApplyUnderstandingGain(
                understanding,
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
        _agents.Health[index] = checked((int)Math.Min(
            _agents.MaximumHealth[index],
            (long)_agents.Health[index] + healthGain));
        _agents.Qi[index] = checked((int)Math.Min(
            _agents.MaximumQi[index],
            (long)_agents.Qi[index] + qiGain));
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

        if ((long)_agents.Health[index] * 100L < (long)_agents.MaximumHealth[index] * 70L
            || (long)_agents.Qi[index] * 100L < (long)_agents.MaximumQi[index] * 35L
            || _agents.InjuryPoints[index] >= 2_500)
        {
            SetActivityDecision(
                index,
                AgentActivity.Recovering,
                AgentGoalKind.RecoverBodyAndQi,
                DecisionReasonCode.ForcedRecovery,
                DecisionResolutionKind.Forced);
            return;
        }

        switch (_agents.CultivationStates[index])
        {
            case CultivationState.PreparingBreakthrough:
            case CultivationState.PreflightReady:
            case CultivationState.Stabilizing:
            case CultivationState.Stable:
                SetActivityDecision(
                    index,
                    AgentActivity.Cultivating,
                    AgentGoalKind.CompleteBreakthrough,
                    DecisionReasonCode.BreakthroughPipeline,
                    DecisionResolutionKind.Forced);
                return;
            case CultivationState.Bottleneck:
                var bottleneckActivity = _agents.CultivationBottleneckCauses[index] switch
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
                SetActivityDecision(
                    index,
                    bottleneckActivity,
                    GetSupportingGoal(bottleneckActivity, bottleneck: true),
                    DecisionReasonCode.BottleneckRemediation,
                    DecisionResolutionKind.Forced);
                return;
        }

        if (_agents.CultivationStates[index] is CultivationState.PreCultivation
            or CultivationState.EntryTraining)
        {
            SetActivityDecision(
                index,
                AgentActivity.Cultivating,
                AgentGoalKind.EnterCultivation,
                DecisionReasonCode.CultivationEntry,
                DecisionResolutionKind.Forced);
            return;
        }

        // 모든 AI가 상위 경지를 목표로 한다는 핵심 의사결정 계약이다.
        // 위치·시장·도전 RNG보다 먼저 최소 절반의 행동을 본경 수행에 확보한다.
        if (_agents.CultivationStates[index] == CultivationState.RealmCultivation
            && (_agents.ActionsTaken[index] & 1) == 0)
        {
            SetActivityDecision(
                index,
                AgentActivity.Cultivating,
                AgentGoalKind.AdvanceRealm,
                DecisionReasonCode.CultivationPriority,
                DecisionResolutionKind.Selected);
            return;
        }

        var rank = _rankByAgent[index];
        var sinceLastCombat = Tick.Value - _agents.LastCombatTicks[index];
        var challengeChance = Population <= 2
            && sinceLastCombat >= GameCalendar.TicksPerYear
                ? 100
            : sinceLastCombat >= GameCalendar.TicksPerYear * 5
                ? 60
            : sinceLastCombat >= GameCalendar.TicksPerYear * 2
                ? 32
            : sinceLastCombat >= GameCalendar.TicksPerYear
                ? 12
                : 0;
        if (_balance.DeveloperModeEnabled && challengeChance > 0)
        {
            challengeChance = Math.Min(
                100,
                WorldBalanceRules.ApplyPercent(
                    challengeChance,
                    _balance.CombatAggressionPercent));
        }
        if (rank > 1
            && challengeChance > 0
            && GetAgeYears(index) >= CultivationRules.EntryAgeYears
            && Tick.Value >= 60
            && WorldMapRules.IsCity(_agents.LocationIds[index])
            && _random.NextInt(100) < challengeChance)
        {
            SetActivityDecision(
                index,
                AgentActivity.SeekingChallenge,
                AgentGoalKind.SeekStrongerOpponent,
                DecisionReasonCode.ChallengeOpportunity,
                DecisionResolutionKind.Selected,
                AgentActivity.Cultivating);
            return;
        }

        if (TryPrepareTeaching(index))
        {
            SetActivityDecision(
                index,
                AgentActivity.Teaching,
                AgentGoalKind.GuideDisciple,
                DecisionReasonCode.TeachingOpportunity,
                DecisionResolutionKind.Selected,
                AgentActivity.Cultivating);
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

        SetActivityDecision(
            index,
            activity,
            GetSupportingGoal(activity, bottleneck: false),
            TryIsMapActivity(activity)
                ? DecisionReasonCode.MapOpportunity
                : DecisionReasonCode.GrowthBalance,
            DecisionResolutionKind.Selected,
            activity == AgentActivity.Cultivating ? null : AgentActivity.Cultivating);
    }

    private void SetActivityDecision(
        int index,
        AgentActivity activity,
        AgentGoalKind supportingGoal,
        DecisionReasonCode reason,
        DecisionResolutionKind resolution,
        AgentActivity? alternativeActivity = null)
    {
        if (activity != AgentActivity.Cultivating)
        {
            FinishCultivationPlan(index);
        }
        _agents.Activities[index] = activity;
        _agents.RecordDecision(
            index,
            Tick.Value,
            supportingGoal,
            activity,
            reason,
            resolution,
            alternativeActivity);
        _causal.RecordDecision(
            index,
            CreateCausalObservation(index),
            _agents.DecisionTraces[index]);
    }

    private CausalObservationInput CreateCausalObservation(int index, int? initialRank = null) => new(
        Tick.Value,
        _agents.PersonIdentityIds[index],
        _agents.LifeNumbers[index],
        _agents.LifeStates[index],
        _agents.Health[index],
        _agents.MaximumHealth[index],
        _agents.CultivationStates[index],
        _agents.LocationIds[index],
        initialRank ?? checked(_rankByAgent[index] + 1));

    private static AgentGoalKind GetSupportingGoal(AgentActivity activity, bool bottleneck) =>
        activity switch
        {
            AgentActivity.Cultivating => bottleneck
                ? AgentGoalKind.ResolveBottleneck
                : AgentGoalKind.AdvanceRealm,
            AgentActivity.TrainingTechnique => AgentGoalKind.ImproveTechnique,
            AgentActivity.TrainingSpell => AgentGoalKind.ImproveSpell,
            AgentActivity.Recovering => AgentGoalKind.RecoverBodyAndQi,
            AgentActivity.SeekingChallenge or AgentActivity.Fighting =>
                AgentGoalKind.SeekStrongerOpponent,
            AgentActivity.DoingCityMission
                or AgentActivity.TradingInCity
                or AgentActivity.CraftingTreasure
                or AgentActivity.CraftingPill
                or AgentActivity.MiningSpiritStones => AgentGoalKind.AcquireResources,
            AgentActivity.Traveling or AgentActivity.HuntingMonster => AgentGoalKind.ExploreWorld,
            AgentActivity.Reincarnating => AgentGoalKind.CompleteReincarnation,
            AgentActivity.Teaching => AgentGoalKind.GuideDisciple,
            _ => AgentGoalKind.ReachRankOne,
        };

    private static bool TryIsMapActivity(AgentActivity activity) => activity is
        AgentActivity.Traveling or AgentActivity.HuntingMonster;

    private long CalculateActivityDuration(int index)
    {
        return _agents.Activities[index] switch
        {
            AgentActivity.Cultivating => Math.Max(30, 145 - ((GetDerivedQualityEffect(index, AptitudeDerivedAbility.SpiritualRootCultivation) + GetDerivedQualityEffect(index, AptitudeDerivedAbility.ComprehensionLearning)) / 2)),
            AgentActivity.TrainingTechnique => GetPlannedActivityDurationTicks(6),
            AgentActivity.TrainingSpell => GetPlannedActivityDurationTicks(6),
            AgentActivity.Recovering => Math.Max(24, 120 - GetDerivedQualityEffect(index, AptitudeDerivedAbility.ConstitutionCultivation)),
            AgentActivity.SeekingChallenge => 20,
            AgentActivity.Fighting => CalculateCombatActionDuration(index),
            AgentActivity.Reincarnating => GameCalendar.TicksPerYear,
            AgentActivity.DoingCityMission => GetPlannedActivityDurationTicks(6),
            AgentActivity.TradingInCity => 45,
            AgentActivity.CraftingTreasure => GetPlannedActivityDurationTicks(6),
            AgentActivity.CraftingPill => GetPlannedActivityDurationTicks(3),
            AgentActivity.Traveling => CalculateTravelDuration(index),
            AgentActivity.HuntingMonster => CalculateMonsterHuntDuration(index),
            AgentActivity.MiningSpiritStones => GetPlannedActivityDurationTicks(6),
            AgentActivity.Teaching => GetPlannedActivityDurationTicks(6),
            _ => throw new InvalidOperationException("알 수 없는 AI 행동입니다."),
        };
    }

    private long GetPlannedActivityDurationTicks(long months)
    {
        var boundedMonths = Math.Clamp(
            months,
            CultivationPlanRules.MinimumPlanMonths,
            CultivationPlanRules.MaximumPlanMonths);
        return checked(
            GameCalendar.GetMonthStart(checked(
                GameCalendar.GetMonthIndex(Tick) + boundedMonths)).Value
            - Tick.Value);
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

        var combatPowerDirty = _combatPowerRankingCache.IsDirty;
        var realmDirty = _realmRankingCache.IsDirty;
        var aptitudeDirty = _aptitudeRankingCache.IsDirty;
        var wealthDirty = _wealthRankingCache.IsDirty;

        if (combatPowerDirty)
        {
            Array.Sort(_agentByRank, CompareAgentsByPower);
            _combatPowerRankingCache.MarkSorted();
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
            }
        }

        if (realmDirty)
        {
            Array.Sort(_agentByRealmRank, CompareAgentsByRealm);
            _realmRankingCache.MarkSorted();
            UpdateInverseRanking(_agentByRealmRank, _realmRankByAgent);
        }

        if (aptitudeDirty)
        {
            Array.Sort(_agentByAptitudeRank, CompareAgentsByAptitude);
            _aptitudeRankingCache.MarkSorted();
            UpdateInverseRanking(_agentByAptitudeRank, _aptitudeRankByAgent);
        }

        if (wealthDirty)
        {
            Array.Sort(_agentByWealthRank, CompareAgentsByWealth);
            _wealthRankingCache.MarkSorted();
            UpdateInverseRanking(_agentByWealthRank, _wealthRankByAgent);
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
                    if (combatPowerDirty && _agentByRank[offset] != previousCombatTop[offset])
                    {
                        combatChanged++;
                    }
                    if (realmDirty && _agentByRealmRank[offset] != previousRealmTop[offset])
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

        var scoreComparison = GetRealmRankingScore(right).CompareTo(GetRealmRankingScore(left));
        return scoreComparison != 0 ? scoreComparison : left.CompareTo(right);
    }

    private long GetRealmRankingScore(int index)
    {
        if (!HasEnteredCultivation(index))
        {
            return _agents.CultivationProgress[index];
        }

        var stageAndFloor = ((long)(int)_agents.RealmStages[index] * RealmRules.MinorStageCount)
            + _agents.RealmLayers[index];
        var completionTier = _agents.GreatPerfectionAchieved[index]
            ? 2
            : _agents.FloorNineCompleted[index] ? 1 : 0;
        return checked((((stageAndFloor * 3) + completionTier) * 1_000_000_000L)
            + _agents.CultivationProgress[index]);
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

    private static void UpdateInverseRanking(int[] orderedAgents, int[] rankByAgent)
    {
        for (var offset = 0; offset < orderedAgents.Length; offset++)
        {
            rankByAgent[orderedAgents[offset]] = offset + 1;
        }
    }

    private void MarkActionRankingChanges(
        int index,
        bool tracksRealmRanking,
        bool enteredCultivationBefore,
        long realmRankingScoreBefore,
        bool tracksWealthRanking,
        long spiritStonesBefore)
    {
        if (tracksRealmRanking
            && (enteredCultivationBefore != HasEnteredCultivation(index)
                || realmRankingScoreBefore != GetRealmRankingScore(index)))
        {
            MarkRealmRankingDirty();
        }

        if (tracksWealthRanking && spiritStonesBefore != _agents.SpiritStones[index])
        {
            MarkWealthRankingDirty();
        }
    }

    private static bool CanChangeSpiritStoneBalance(AgentActivity activity) => activity is
        AgentActivity.Cultivating
        or AgentActivity.DoingCityMission
        or AgentActivity.TradingInCity
        or AgentActivity.CraftingTreasure
        or AgentActivity.CraftingPill
        or AgentActivity.MiningSpiritStones;

    private static bool CanChangePowerInputsWithoutInternalRecalculation(
        AgentActivity activity) => activity is
        AgentActivity.Cultivating
        or AgentActivity.TrainingTechnique
        or AgentActivity.TrainingSpell
        or AgentActivity.TradingInCity
        or AgentActivity.HuntingMonster;

    private void MarkCombatPowerRankingDirty() => _combatPowerRankingCache.MarkDirty();

    private void MarkRealmRankingDirty() => _realmRankingCache.MarkDirty();

    private void MarkAptitudeRankingDirty() => _aptitudeRankingCache.MarkDirty();

    private void MarkWealthRankingDirty() => _wealthRankingCache.MarkDirty();

    private void MarkAllRankingsDirty()
    {
        MarkCombatPowerRankingDirty();
        MarkRealmRankingDirty();
        MarkAptitudeRankingDirty();
        MarkWealthRankingDirty();
    }

    internal RankingCacheDiagnostics GetRankingCacheDiagnostics() => new(
        _combatPowerRankingCache.GetDiagnostics(),
        _realmRankingCache.GetDiagnostics(),
        _aptitudeRankingCache.GetDiagnostics(),
        _wealthRankingCache.GetDiagnostics());

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
            or WorldEventKind.ReincarnationCrossingCompleted
            or WorldEventKind.Rebirth
            or WorldEventKind.SecretRealmDiscovered => true,
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
        or WorldEventKind.FloorNineCompletion
        or WorldEventKind.GreatPerfectionAchieved
        or WorldEventKind.TechniqueAcquired
        or WorldEventKind.SpellAcquired
        or WorldEventKind.Death
        or WorldEventKind.CombatDeath
        or WorldEventKind.ReincarnationEntered
        or WorldEventKind.ReincarnationCrossingCompleted
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

    private long GetAgeYears(int index)
    {
        var ageTick = _agents.LifeStates[index] == PersonLifeState.ReincarnationRiver
            ? _reincarnation.GetCurrentJourney(index)?.StartedTick ?? Tick.Value
            : Tick.Value;
        return GameCalendar.GetAgeYears(
            new SimulationTick(_agents.BirthTicks[index]),
            new SimulationTick(ageTick));
    }

    private void RecalculatePowerAndUpdatePeak(int index)
    {
        var previousPower = _agents.Power[index].Total;
        _agents.RecalculatePower(index);
        if (previousPower != _agents.Power[index].Total)
        {
            MarkCombatPowerRankingDirty();
        }
        if (_agents.LifeStates[index] == PersonLifeState.Living)
        {
            _reincarnation.UpdatePeak(
                index,
                _agents.RealmStages[index],
                _agents.RealmLayers[index],
                _agents.Power[index].Total,
                Tick.Value);
        }
    }

    private static int GetQualityEffect(int rawQuality) =>
        QualityRules.GetEffectiveWhole(QualityValue.FromHundredths(rawQuality));

    private int GetDerivedQualityEffect(int index, AptitudeDerivedAbility ability) =>
        GetQualityEffect(_agents.GetDerivedAptitude(index, ability));

    private bool HasReachedMaximumLifespan(int index) =>
        GetAgeYears(index) >= RealmRules.GetProfile(_agents.RealmStages[index]).MaximumLifespanYears;

    private long GetMaximumLifespanTick(int index) => checked(
        _agents.BirthTicks[index]
        + ((long)RealmRules.GetProfile(_agents.RealmStages[index]).MaximumLifespanYears
            * GameCalendar.TicksPerYear));

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

    private void ScheduleAgent(int index, long tick, bool lifecycleBoundary = false)
    {
        if (_agents.LifeStates[index] == PersonLifeState.Living)
        {
            var maximumLifespanTick = GetMaximumLifespanTick(index);
            if (maximumLifespanTick <= tick)
            {
                tick = maximumLifespanTick;
                lifecycleBoundary = true;
            }
        }

        var scheduledTick = new SimulationTick(tick);
        if (!lifecycleBoundary
            && GameCalendar.GetMonthIndex(scheduledTick) <= _agents.LastActionMonths[index])
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

    private static void AddRelationMetricsHash(ref ulong hash, RelationMetrics metrics)
    {
        Add(ref hash, unchecked((ulong)(long)metrics.FavorDisfavor));
        Add(ref hash, unchecked((ulong)(long)metrics.TrustDistrust));
        Add(ref hash, unchecked((ulong)(long)metrics.RespectContempt));
        Add(ref hash, (ulong)metrics.Gratitude);
        Add(ref hash, (ulong)metrics.Grievance);
        Add(ref hash, (ulong)metrics.Vigilance);
    }

    private static void AddTeachingTraceHash(ref ulong hash, TeachingTraceState teaching)
    {
        Add(ref hash, (ulong)teaching.AuthorityRevision);
        Add(ref hash, teaching.IsActive ? 1UL : 0UL);
        Add(ref hash, (ulong)(teaching.LearnerAgent + 1));
        Add(ref hash, teaching.TeacherPersonId.Value);
        Add(ref hash, teaching.LearnerPersonId.Value);
        Add(ref hash, teaching.RelationId.Value);
        Add(ref hash, (byte)teaching.TargetKind);
        Add(ref hash, teaching.SourceQuality);
        Add(ref hash, teaching.TargetKnowledgeRef.Value);
        Add(ref hash, teaching.ProvenanceId.Value);
        Add(ref hash, (ulong)teaching.StartedTick);
        Add(ref hash, (ulong)teaching.LastResultTick);
        Add(ref hash, (byte)teaching.LastResult);
        Add(ref hash, (ulong)teaching.LastGain);
        Add(ref hash, (ulong)teaching.SessionsCompleted);
        Add(ref hash, teaching.MisunderstandingPoints);
        Add(ref hash, teaching.TransmissionEstablished ? 1UL : 0UL);
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
            Add(ref hash, (ulong)entry.SecretRealmsDiscovered);
            Add(ref hash, (ulong)entry.UniqueSecretRealmDiscoverers);
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
        Add(ref hash, (ulong)current.SecretRealmsDiscovered);
        Add(ref hash, (ulong)current.UniqueSecretRealmDiscoverers);
        AddArray(ref hash, current.SecretRealmDiscovererBits ?? []);
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
