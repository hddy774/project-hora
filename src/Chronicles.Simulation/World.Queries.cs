using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private static readonly long[] CombatPowerBucketBounds =
    [
        10_000,
        100_000,
        1_000_000,
        10_000_000,
        100_000_000,
        1_000_000_000,
        10_000_000_000,
    ];

    public IReadOnlyList<LifeChronicleEntry> GetAgentChronicle(AgentId agentId)
    {
        var index = ValidateAgent(agentId);
        return _agents.Chronicles[index].ToArray();
    }

    public IReadOnlyList<PersonalLogEntry> GetAgentPersonalLog(AgentId agentId)
    {
        var index = ValidateAgent(agentId);
        return _agents.PersonalLogs[index].ToArray();
    }

    public IReadOnlyList<AgentSnapshot> BrowseAgents(int offset, int limit = 500)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        if (offset >= Population)
        {
            return Array.Empty<AgentSnapshot>();
        }

        var count = Math.Min(limit, Population - offset);
        var results = new AgentSnapshot[count];
        for (var resultIndex = 0; resultIndex < count; resultIndex++)
        {
            results[resultIndex] = GetAgentSnapshot(new AgentId(offset + resultIndex));
        }

        return results;
    }

    public IReadOnlyList<AgentSnapshot> SearchAgents(string? query, int limit = 1_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        var normalized = query?.Trim() ?? string.Empty;
        var results = new List<AgentSnapshot>(Math.Min(limit, Population));

        if (TryParseAgentId(normalized, out var exactAgent))
        {
            results.Add(GetAgentSnapshot(exactAgent));
            return results;
        }

        for (var index = 0; index < Population && results.Count < limit; index++)
        {
            if (normalized.Length == 0
                || _agents.Names[index].Contains(normalized, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(GetAgentSnapshot(new AgentId(index)));
            }
        }

        return results;
    }

    public WorldStatistics CreateStatistics()
    {
        var realmCounts = new int[RealmRules.All.Count];
        var powerCounts = new int[CombatPowerBucketBounds.Length + 1];
        long totalWins = 0;
        long totalLosses = 0;
        long totalSpiritStones = 0;
        for (var index = 0; index < Population; index++)
        {
            realmCounts[(int)_agents.RealmStages[index]]++;
            var power = _agents.Power[index].Total;
            var bucket = 0;
            while (bucket < CombatPowerBucketBounds.Length
                && power >= CombatPowerBucketBounds[bucket])
            {
                bucket++;
            }

            powerCounts[bucket]++;
            totalWins = checked(totalWins + _agents.Wins[index]);
            totalLosses = checked(totalLosses + _agents.Losses[index]);
            totalSpiritStones = checked(totalSpiritStones + _agents.SpiritStones[index]);
        }

        var realmDistribution = new RealmDistributionEntry[realmCounts.Length];
        for (var realm = 0; realm < realmCounts.Length; realm++)
        {
            realmDistribution[realm] = new RealmDistributionEntry(
                (RealmStage)realm,
                realmCounts[realm]);
        }

        var powerDistribution = new CombatPowerDistributionEntry[powerCounts.Length];
        for (var bucket = 0; bucket < powerCounts.Length; bucket++)
        {
            powerDistribution[bucket] = new CombatPowerDistributionEntry(
                bucket == 0 ? 0 : CombatPowerBucketBounds[bucket - 1],
                bucket < CombatPowerBucketBounds.Length ? CombatPowerBucketBounds[bucket] : null,
                powerCounts[bucket]);
        }

        return new WorldStatistics(
            realmDistribution,
            powerDistribution,
            new RankingMovementStatistics(
                new SimulationTick(_rankingMovementFromTick),
                new SimulationTick(_lastRankingRefreshTick),
                _rankingRisenCount,
                _rankingFallenCount,
                _rankingUnchangedCount,
                _rankingTotalAbsoluteMovement),
            _totalDeaths,
            totalWins,
            totalLosses,
            _totalReincarnations,
            _totalBreakthroughs,
            _totalMissionsCompleted,
            _totalTreasuresCrafted,
            _totalPillsCrafted,
            _totalMaterialsSold,
            totalSpiritStones);
    }

    private bool TryParseAgentId(string query, out AgentId agentId)
    {
        var numeric = query.StartsWith('A') ? query[1..] : query;
        if (int.TryParse(numeric, out var displayId)
            && displayId >= 1
            && displayId <= Population)
        {
            agentId = new AgentId(displayId - 1);
            return true;
        }

        agentId = default;
        return false;
    }
}
