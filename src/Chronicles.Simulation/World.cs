using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    public const int WorldLogCapacity = 100;
    public const int PersonalLogCapacity = PersonalLogRingStore.Capacity;
    private readonly DeterministicRandom _random;
    private readonly AgentStore _agents;
    private readonly AgentCausalStore _causal;
    private readonly SparseRelationStore _relations;
    private readonly ReincarnationStore _reincarnation;
    private readonly SpatialAuthorityStore _atlas;
    private readonly OrganizationStore _organizations;
    private readonly ContentAuthorityStore _content;
    private readonly KnowledgeAuthorityStore _knowledge;
    private readonly LawAuthorityStore _laws;
    private readonly WorldGenerationCatalog _generation;
    private readonly CityMarket _market;
    private readonly WorldBalanceConfiguration _balance;
    private readonly AgentDecisionSchedule _decisionSchedule;
    private readonly CultivationParallelCoordinator _cultivationParallel;
    private readonly int[] _agentByRank;
    private readonly int[] _rankByAgent;
    private readonly int[] _agentByRealmRank;
    private readonly int[] _realmRankByAgent;
    private readonly int[] _agentByAptitudeRank;
    private readonly int[] _aptitudeRankByAgent;
    private readonly int[] _agentByWealthRank;
    private readonly int[] _wealthRankByAgent;
    private readonly RankingSortWorkspace _rankingWorkspace;
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
    private long _lastMonthlyBoundaryMonth = -1;
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

    private World(
        ulong seed,
        int population,
        WorldBalanceConfiguration balance,
        WorldGenerationConfiguration generation)
    {
        WorldBalanceRules.Validate(balance);
        WorldGenerationRules.Validate(generation);
        Seed = seed;
        _balance = balance;
        _generation = WorldGenerationCatalog.Create(seed, generation);
        _random = new DeterministicRandom(seed);
        _agents = new AgentStore(population);
        _decisionSchedule = new AgentDecisionSchedule(population);
        _cultivationParallel = new CultivationParallelCoordinator(population);
        _causal = new AgentCausalStore(population);
        _relations = new SparseRelationStore(population);
        _reincarnation = new ReincarnationStore(population);
        _atlas = new SpatialAuthorityStore(population);
        _organizations = new OrganizationStore(population);
        _content = ContentAuthorityStore.CreateInitial(_generation);
        _knowledge = KnowledgeAuthorityStore.CreateInitial(
            seed,
            population,
            _content,
            _generation);
        _laws = LawAuthorityStore.CreateReviewCatalog(population);
        _market = new CityMarket(population);
        _agentByRank = new int[population];
        _rankByAgent = new int[population];
        _agentByRealmRank = new int[population];
        _realmRankByAgent = new int[population];
        _agentByAptitudeRank = new int[population];
        _aptitudeRankByAgent = new int[population];
        _agentByWealthRank = new int[population];
        _wealthRankByAgent = new int[population];
        _rankingWorkspace = new RankingSortWorkspace(population);
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
            _decisionSchedule.Schedule(index, _agents.NextDecisionTicks[index]);
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
    public int TotalKnowledgeEditions => _knowledge.EditionCount;
    public int TotalLawCandidates => _laws.CandidateCount;
    public WorldBalanceConfiguration Balance => _balance;
    public WorldGenerationConfiguration Generation => _generation.Configuration;

    public static World CreateDefault(ulong seed) =>
        new(
            seed,
            ProductInfo.DefaultPopulation,
            WorldBalanceConfiguration.Default,
            WorldGenerationConfiguration.Default);

    public static World Create(ulong seed, int population) =>
        new(
            seed,
            population,
            WorldBalanceConfiguration.Default,
            WorldGenerationConfiguration.Default);

    public static World Create(
        ulong seed,
        int population,
        WorldBalanceConfiguration balance) =>
        new(seed, population, balance, WorldGenerationConfiguration.Default);

    public static World Create(
        ulong seed,
        int population,
        WorldBalanceConfiguration balance,
        WorldGenerationConfiguration generation) =>
        new(seed, population, balance, generation);

    private bool ResolveActivity(
        int index,
        long actionMonth,
        CultivationWorkResult? precomputedCultivation = null)
    {
        switch (_agents.Activities[index])
        {
            case AgentActivity.Cultivating:
                ResolveCultivation(index, actionMonth, precomputedCultivation);
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
            && HasReachedAge(index, CultivationRules.EntryAgeYears)
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
        initialRank ?? _rankByAgent[index]);

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

    private void AddPersonalActionLog(int index, AgentActivity activity, long currentMonth)
    {
        var log = _agents.PersonalLogs[index];
        if (log.TryExtendLatestAction(
                activity,
                _agents.LifeNumbers[index],
                Tick,
                currentMonth))
        {
            return;
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
        Tick.Value >= GetMaximumLifespanTick(index);

    private bool HasReachedAge(int index, int ageYears) =>
        Tick.Value >= checked(
            _agents.BirthTicks[index] + ((long)ageYears * GameCalendar.TicksPerYear));

    private long GetMaximumLifespanTick(int index) => checked(
        _agents.BirthTicks[index]
        + ((long)RealmRules.GetProfile(_agents.RealmStages[index]).MaximumLifespanYears
            * GameCalendar.TicksPerYear));

    private void AddChronicle(int index, ChronicleEventKind kind, int value = 0)
    {
        var entry = new LifeChronicleEntry(
            ++_chronicleSequence,
            Tick,
            _agents.LifeNumbers[index],
            GetAgeYears(index),
            _agents.Names[index],
            kind,
            _agents.RealmStages[index],
            (MinorStage)_agents.RealmLayers[index],
            value);
        _agents.Chronicles[index].Add(entry);
        _agents.PersonalCenturyChronicles.Record(index, entry);
        AddPersonalChronicleLog(index, kind, value);
    }

    private static int EncodeRealm(RealmStage stage, int minorStage) =>
        ((int)stage * 10) + minorStage;

    private int ValidateAgent(AgentId agentId)
    {
        if ((uint)agentId.Value >= (uint)Population)
        {
            throw new ArgumentOutOfRangeException(nameof(agentId));
        }

        return agentId.Value;
    }

}
