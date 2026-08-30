using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private const int EventCapacity = 512;
    private readonly DeterministicRandom _random;
    private readonly AgentStore _agents;
    private readonly PriorityQueue<int, (long Tick, int Agent)> _decisionSchedule = new();
    private readonly int[] _agentByRank;
    private readonly int[] _rankByAgent;
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

    private World(ulong seed, int population)
    {
        Seed = seed;
        _random = new DeterministicRandom(seed);
        _agents = new AgentStore(population);
        _agentByRank = new int[population];
        _rankByAgent = new int[population];

        for (var index = 0; index < population; index++)
        {
            _agents.Initialize(index, _random);
            AddChronicle(index, ChronicleEventKind.Birth);
            _agentByRank[index] = index;
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
            if (_agents.Activities[agentIndex] != AgentActivity.Reincarnating
                && HasReachedMaximumLifespan(agentIndex))
            {
                BeginReincarnation(agentIndex);
                continue;
            }

            var alreadyScheduled = ResolveActivity(agentIndex);
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
            _agents.TargetRanks[index],
            GetAgeYears(index),
            RealmRules.GetProfile(_agents.RealmStages[index]).MaximumLifespanYears,
            _agents.LifeNumbers[index],
            _agents.RealmStages[index],
            _agents.RealmLayers[index],
            _agents.CultivationProgress[index],
            GetCultivationRequirement(index),
            _agents.GetAptitudes(index),
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
            new SimulationTick(_agents.NextDecisionTicks[index]),
            _agents.Opponents[index] < 0 ? null : new AgentId(_agents.Opponents[index]),
            new SimulationTick(_agents.LastCombatTicks[index]),
            _agents.ChallengesIssued[index],
            _agents.Power[index]);
    }

    public WorldSnapshot CreateSnapshot(int rankingCount = 100, int eventCount = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rankingCount);
        ArgumentOutOfRangeException.ThrowIfNegative(eventCount);
        var actualRankingCount = Math.Min(rankingCount, Population);
        var rankings = new RankingEntry[actualRankingCount];
        for (var offset = 0; offset < actualRankingCount; offset++)
        {
            var agentIndex = _agentByRank[offset];
            rankings[offset] = new RankingEntry(
                offset + 1,
                new AgentId(agentIndex),
                _agents.Names[agentIndex],
                _agents.RealmStages[agentIndex],
                _agents.RealmLayers[agentIndex],
                _agents.Activities[agentIndex],
                _agents.Power[agentIndex]);
        }

        var recentEvents = _events.Skip(Math.Max(0, _events.Count - eventCount)).ToArray();
        var agentNames = new Dictionary<int, string>(actualRankingCount + (recentEvents.Length * 2));
        foreach (var ranking in rankings)
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
            rankings,
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
            Add(ref hash, _agents.TechniqueGrades[index]);
            Add(ref hash, _agents.TechniqueMasteries[index]);
            Add(ref hash, _agents.SpellGrades[index]);
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
            Add(ref hash, (ulong)(_agents.Opponents[index] + 1));
            Add(ref hash, (ulong)(_agents.LastOpponents[index] + 1));
            Add(ref hash, (ulong)_agents.LastCombatTicks[index]);
            Add(ref hash, (ulong)_agents.CombatStartedTicks[index]);
            Add(ref hash, _agents.CombatActions[index]);
            Add(ref hash, _agents.Guarding[index] ? 1UL : 0UL);
            Add(ref hash, (ulong)_agents.ChallengesIssued[index]);
            Add(ref hash, (ulong)_rankByAgent[index]);
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
            default:
                throw new InvalidOperationException("알 수 없는 AI 행동입니다.");
        }
    }

    private void ResolveCultivation(int index)
    {
        if (RealmRules.IsFinalStage(_agents.RealmStages[index], _agents.RealmLayers[index]))
        {
            _agents.CultivationProgress[index] = GetCultivationRequirement(index);
            return;
        }

        var rateBasisPoints = Math.Clamp(
            10_000
            + ((_agents.SpiritualRoots[index] - 50) * 40)
            + ((_agents.Comprehensions[index] - 50) * 40)
            + ((_agents.Minds[index] - 50) * 20)
            + ((_agents.Souls[index] - 50) * 10)
            + ((_agents.TechniqueGrades[index] - 1) * 300),
            3_000,
            18_000);
        var actionTicks = CalculateActivityDuration(index);
        var gain = Math.Max(1, checked((int)(actionTicks * rateBasisPoints / 10_000)));
        _agents.CultivationProgress[index] = checked(_agents.CultivationProgress[index] + gain);

        var requirement = GetCultivationRequirement(index);
        if (_agents.CultivationProgress[index] < requirement)
        {
            return;
        }

        var stagePenalty = ((int)_agents.RealmStages[index] * 550) + (_agents.RealmLayers[index] * 80);
        var chance = Math.Clamp(
            4_500
            + (_agents.Comprehensions[index] * 30)
            + (_agents.SpiritualRoots[index] * 20)
            + (_agents.Constitutions[index] * 10)
            + (_agents.Minds[index] * 10)
            + (_agents.Souls[index] * 5)
            - stagePenalty,
            1_200,
            9_500);

        if (_random.Chance(chance))
        {
            _agents.CultivationProgress[index] -= requirement;
            var previousRealm = _agents.RealmStages[index];
            var previousMinorStage = (MinorStage)_agents.RealmLayers[index];
            var bottleneckFailures = _agents.BottleneckFailures[index];
            AdvanceRealm(index);
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

        _agents.BottleneckFailures[index]++;
        _agents.CultivationProgress[index] = requirement * 3 / 4;
        _agents.Health[index] = Math.Max(1, _agents.Health[index] - (_agents.MaximumHealth[index] / 8));
        AddEvent(WorldEventKind.BreakthroughFailed, index, chance);
    }

    private void ResolveTechniqueTraining(int index)
    {
        var gain = 4
            + (_agents.Comprehensions[index] / 8)
            + (_agents.Bones[index] / 12)
            + (_agents.Minds[index] / 20);
        var mastery = _agents.TechniqueMasteries[index] + gain;
        if (mastery >= 1_000 && _agents.TechniqueGrades[index] < 5)
        {
            _agents.TechniqueGrades[index]++;
            mastery = 250;
            AddChronicle(index, ChronicleEventKind.TechniqueEnlightenment, _agents.TechniqueGrades[index]);
            AddEvent(WorldEventKind.TechniqueEnlightenment, index, _agents.TechniqueGrades[index]);
        }

        _agents.TechniqueMasteries[index] = (ushort)Math.Min(1_000, mastery);
    }

    private void ResolveSpellTraining(int index)
    {
        var gain = 4
            + (_agents.Comprehensions[index] / 7)
            + (_agents.SpiritualRoots[index] / 14)
            + (_agents.Minds[index] / 18);
        var mastery = _agents.SpellMasteries[index] + gain;
        if (mastery >= 1_000 && _agents.SpellGrades[index] < 5)
        {
            _agents.SpellGrades[index]++;
            mastery = 200;
            AddChronicle(index, ChronicleEventKind.SpellEnlightenment, _agents.SpellGrades[index]);
            AddEvent(WorldEventKind.SpellEnlightenment, index, _agents.SpellGrades[index]);
        }

        _agents.SpellMasteries[index] = (ushort)Math.Min(1_000, mastery);
    }

    private void ResolveRecovery(int index)
    {
        var healthGain = 20 + (_agents.Constitutions[index] * 4);
        var qiGain = 15 + (_agents.SpiritualRoots[index] * 5);
        _agents.Health[index] = Math.Min(_agents.MaximumHealth[index], _agents.Health[index] + healthGain);
        _agents.Qi[index] = Math.Min(_agents.MaximumQi[index], _agents.Qi[index] + qiGain);
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

        var rank = _rankByAgent[index];
        var sinceLastCombat = Tick.Value - _agents.LastCombatTicks[index];
        var challengeChance = sinceLastCombat > 700 ? 58 : 34;
        if (rank > 1 && Tick.Value >= 60 && _random.NextInt(100) < challengeChance)
        {
            _agents.Activities[index] = AgentActivity.SeekingChallenge;
            return;
        }

        var roll = _random.NextInt(100);
        _agents.Activities[index] = roll switch
        {
            < 55 => AgentActivity.Cultivating,
            < 80 => AgentActivity.TrainingTechnique,
            _ => AgentActivity.TrainingSpell,
        };
    }

    private long CalculateActivityDuration(int index)
    {
        return _agents.Activities[index] switch
        {
            AgentActivity.Cultivating => Math.Max(30, 145 - ((_agents.SpiritualRoots[index] + _agents.Comprehensions[index]) / 2)),
            AgentActivity.TrainingTechnique => Math.Max(34, 150 - ((_agents.Bones[index] + _agents.Comprehensions[index]) / 2)),
            AgentActivity.TrainingSpell => Math.Max(34, 150 - ((_agents.SpiritualRoots[index] + _agents.Comprehensions[index]) / 2)),
            AgentActivity.Recovering => Math.Max(24, 120 - _agents.Constitutions[index]),
            AgentActivity.SeekingChallenge => 20,
            AgentActivity.Fighting => CalculateCombatActionDuration(index),
            AgentActivity.Reincarnating => GameCalendar.TicksPerYear,
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
        Array.Sort(_agentByRank, CompareAgentsByPower);
        for (var offset = 0; offset < _agentByRank.Length; offset++)
        {
            _rankByAgent[_agentByRank[offset]] = offset + 1;
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

    private void AddEvent(WorldEventKind kind, int primaryAgent, int value, int? secondaryAgent = null)
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
        _agents.NextDecisionTicks[index] = tick;
        _decisionSchedule.Enqueue(index, (tick, index));
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
