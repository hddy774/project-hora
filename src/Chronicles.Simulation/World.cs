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
    private int _totalBreakthroughs;
    private int _totalChallenges;
    private int _totalCombatsStarted;
    private int _totalCombatsCompleted;
    private int _activeCombats;

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
        return new WorldSnapshot(
            Tick,
            Population,
            rankings,
            recentEvents,
            _totalBreakthroughs,
            _totalChallenges,
            _totalCombatsStarted,
            _totalCombatsCompleted,
            _activeCombats,
            LeaderId);
    }

    public ulong ComputeStateHash()
    {
        var hash = 14695981039346656037UL;
        Add(ref hash, (ulong)Tick.Value);
        Add(ref hash, _random.State);
        for (var index = 0; index < Population; index++)
        {
            Add(ref hash, (ulong)index);
            Add(ref hash, _agents.SpiritualRoots[index]);
            Add(ref hash, _agents.Bones[index]);
            Add(ref hash, _agents.Constitutions[index]);
            Add(ref hash, _agents.Comprehensions[index]);
            Add(ref hash, (byte)_agents.RealmStages[index]);
            Add(ref hash, _agents.RealmLayers[index]);
            Add(ref hash, (ulong)_agents.CultivationProgress[index]);
            Add(ref hash, _agents.TechniqueGrades[index]);
            Add(ref hash, _agents.TechniqueMasteries[index]);
            Add(ref hash, _agents.SpellGrades[index]);
            Add(ref hash, _agents.SpellMasteries[index]);
            Add(ref hash, (ulong)_agents.CombatExperience[index]);
            Add(ref hash, (ulong)_agents.Health[index]);
            Add(ref hash, (ulong)_agents.Qi[index]);
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
            default:
                throw new InvalidOperationException("알 수 없는 AI 행동입니다.");
        }
    }

    private void ResolveCultivation(int index)
    {
        var gain = 80
            + (_agents.SpiritualRoots[index] * 3)
            + (_agents.Comprehensions[index] * 2)
            + (_agents.TechniqueGrades[index] * 20);
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
            - stagePenalty,
            1_200,
            9_500);

        if (_random.Chance(chance))
        {
            _agents.CultivationProgress[index] -= requirement;
            AdvanceRealm(index);
            _totalBreakthroughs++;
            AddEvent(WorldEventKind.BreakthroughSucceeded, index, ((int)_agents.RealmStages[index] * 10) + _agents.RealmLayers[index]);
            return;
        }

        _agents.CultivationProgress[index] = requirement * 3 / 4;
        _agents.Health[index] = Math.Max(1, _agents.Health[index] - (_agents.MaximumHealth[index] / 8));
        AddEvent(WorldEventKind.BreakthroughFailed, index, chance);
    }

    private void ResolveTechniqueTraining(int index)
    {
        var gain = 4 + (_agents.Comprehensions[index] / 8) + (_agents.Bones[index] / 12);
        var mastery = _agents.TechniqueMasteries[index] + gain;
        if (mastery >= 1_000 && _agents.TechniqueGrades[index] < 5)
        {
            _agents.TechniqueGrades[index]++;
            mastery = 250;
        }

        _agents.TechniqueMasteries[index] = (ushort)Math.Min(1_000, mastery);
    }

    private void ResolveSpellTraining(int index)
    {
        var gain = 4 + (_agents.Comprehensions[index] / 7) + (_agents.SpiritualRoots[index] / 14);
        var mastery = _agents.SpellMasteries[index] + gain;
        if (mastery >= 1_000 && _agents.SpellGrades[index] < 5)
        {
            _agents.SpellGrades[index]++;
            mastery = 200;
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
            _ => throw new InvalidOperationException("알 수 없는 AI 행동입니다."),
        };
    }

    private int GetCultivationRequirement(int index)
    {
        var stage = (int)_agents.RealmStages[index] + 1;
        return checked(2_000 + (stage * stage * 1_000) + (_agents.RealmLayers[index] * stage * 500));
    }

    private void AdvanceRealm(int index)
    {
        if (_agents.RealmLayers[index] < 9)
        {
            _agents.RealmLayers[index]++;
            return;
        }

        if (_agents.RealmStages[index] < RealmStage.SpiritTransformation)
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
}
