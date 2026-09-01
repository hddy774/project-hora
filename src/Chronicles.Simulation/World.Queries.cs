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

    public ReincarnationAgentSnapshot GetAgentReincarnationSnapshot(AgentId agentId)
    {
        var index = ValidateAgent(agentId);
        return _reincarnation.GetSnapshot(index, Tick.Value);
    }

    public IReadOnlyList<WorldCenturyChronicleEntry> GetWorldChronicle() =>
        _worldChronicle.GetAll();

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

    public WorldStatistics CreateStatistics() => CreateStatistics(ObservationSections.Statistics);

    private WorldStatistics CreateStatistics(ObservationSections sections)
    {
        var includeSummary = (sections & ObservationSections.StatisticsSummary) != 0;
        var includeRealmDistribution = (sections & ObservationSections.RealmDistribution) != 0;
        var includePowerDistribution = (sections & ObservationSections.CombatPowerDistribution) != 0;
        var realmCounts = includeRealmDistribution
            ? new int[RealmRules.All.Count + 1]
            : Array.Empty<int>();
        var powerCounts = includePowerDistribution
            ? new int[CombatPowerBucketBounds.Length + 1]
            : Array.Empty<int>();
        long totalWins = 0;
        long totalLosses = 0;
        long totalSpiritStones = 0;
        for (var index = 0; index < Population; index++)
        {
            if (includeRealmDistribution)
            {
                var realmBucket = HasEnteredCultivation(index)
                    ? (int)_agents.RealmStages[index] + 1
                    : 0;
                realmCounts[realmBucket]++;
            }

            if (includePowerDistribution)
            {
                var power = _agents.Power[index].Total;
                var bucket = 0;
                while (bucket < CombatPowerBucketBounds.Length
                    && power >= CombatPowerBucketBounds[bucket])
                {
                    bucket++;
                }

                powerCounts[bucket]++;
            }

            if (includeSummary)
            {
                totalWins = checked(totalWins + _agents.Wins[index]);
                totalLosses = checked(totalLosses + _agents.Losses[index]);
                totalSpiritStones = checked(totalSpiritStones + _agents.SpiritStones[index]);
            }
        }

        IReadOnlyList<RealmDistributionEntry> realmDistribution = Array.Empty<RealmDistributionEntry>();
        var realmSnapshots = new RealmDistributionEntry[realmCounts.Length];
        for (var realm = 0; realm < realmCounts.Length; realm++)
        {
            realmSnapshots[realm] = new RealmDistributionEntry(
                realm == 0 ? null : (RealmStage)(realm - 1),
                realmCounts[realm]);
        }

        if (realmSnapshots.Length > 0)
        {
            realmDistribution = realmSnapshots;
        }

        IReadOnlyList<CombatPowerDistributionEntry> powerDistribution = Array.Empty<CombatPowerDistributionEntry>();
        var powerSnapshots = new CombatPowerDistributionEntry[powerCounts.Length];
        for (var bucket = 0; bucket < powerCounts.Length; bucket++)
        {
            powerSnapshots[bucket] = new CombatPowerDistributionEntry(
                bucket == 0 ? 0 : CombatPowerBucketBounds[bucket - 1],
                bucket < CombatPowerBucketBounds.Length ? CombatPowerBucketBounds[bucket] : null,
                powerCounts[bucket]);
        }

        if (powerSnapshots.Length > 0)
        {
            powerDistribution = powerSnapshots;
        }

        if (includeSummary)
        {
            totalSpiritStones = checked(
                totalSpiritStones + _organizations.TotalFoundingReserves);
        }

        IReadOnlyList<EliteRankingMovementSnapshot> eliteMovements =
            Array.Empty<EliteRankingMovementSnapshot>();
        IReadOnlyList<RankingMovementWindowSnapshot> movementWindows =
            Array.Empty<RankingMovementWindowSnapshot>();
        if (includeSummary)
        {
            int[] limits = [1, 3, 5, 10];
            var entries = new EliteRankingMovementSnapshot[limits.Length];
            for (var tier = 0; tier < limits.Length; tier++)
            {
                var count = Math.Min(limits[tier], Population);
                var combatCutoffAgent = _agentByRank[count - 1];
                var realmCutoffAgent = _agentByRealmRank[count - 1];
                entries[tier] = new EliteRankingMovementSnapshot(
                    limits[tier],
                    _agents.Power[combatCutoffAgent].Total,
                    _agents.RealmStages[realmCutoffAgent],
                    _agents.RealmLayers[realmCutoffAgent],
                    _eliteCombatChangedCounts[tier],
                    _eliteRealmChangedCounts[tier],
                    _eliteCombatChangedCounts[tier] * 10_000 / count,
                    _eliteRealmChangedCounts[tier] * 10_000 / count);
            }

            eliteMovements = entries;
            movementWindows = CreateRankingMovementWindows();
        }

        return new WorldStatistics(
            realmDistribution,
            powerDistribution,
            includeSummary
                ? new RankingMovementStatistics(
                    new SimulationTick(_rankingMovementFromTick),
                    new SimulationTick(_lastRankingRefreshTick),
                    _rankingRisenCount,
                    _rankingFallenCount,
                    _rankingUnchangedCount,
                    _rankingTotalAbsoluteMovement)
                : default,
            includeSummary ? _totalDeaths : 0,
            totalWins,
            totalLosses,
            includeSummary ? _totalReincarnations : 0,
            includeSummary ? _totalBreakthroughs : 0,
            includeSummary ? _totalMissionsCompleted : 0,
            includeSummary ? _totalTreasuresCrafted : 0,
            includeSummary ? _totalPillsCrafted : 0,
            includeSummary ? _totalMaterialsSold : 0,
            totalSpiritStones,
            eliteMovements,
            movementWindows);
    }

    private RankingMovementWindowSnapshot[] CreateRankingMovementWindows()
    {
        ReadOnlySpan<int> tierLimits = [1, 3, 5, 10, 100, 500, 1_000];
        var results = new RankingMovementWindowSnapshot[
            RankingHistory.AllPeriods.Count * tierLimits.Length];
        var resultIndex = 0;
        foreach (var period in RankingHistory.AllPeriods)
        {
            var baseline = _rankingHistory.GetBaseline(period, Tick);
            var observationIntervals = RankingHistory.GetObservationIntervalCount(baseline, Tick);
            foreach (var tierLimit in tierLimits)
            {
                var comparedCount = Math.Min(
                    tierLimit,
                    Math.Min(Population, baseline.CombatPowerTop.Length));
                var cutoffOffset = Math.Max(0, Math.Min(tierLimit, Population) - 1);
                var combatCutoffAgent = _agentByRank[cutoffOffset];
                var realmCutoffAgent = _agentByRealmRank[cutoffOffset];
                var wealthCutoffAgent = _agentByWealthRank[cutoffOffset];
                var combatChanged = _rankingHistory.GetPositionChangeCount(
                    period,
                    RankingCategory.CombatPower,
                    Tick,
                    _agentByRank,
                    tierLimit);
                var realmChanged = _rankingHistory.GetPositionChangeCount(
                    period,
                    RankingCategory.Realm,
                    Tick,
                    _agentByRealmRank,
                    tierLimit);
                var wealthChanged = _rankingHistory.GetPositionChangeCount(
                    period,
                    RankingCategory.Wealth,
                    Tick,
                    _agentByWealthRank,
                    tierLimit);
                results[resultIndex++] = new RankingMovementWindowSnapshot(
                    period,
                    baseline.Tick,
                    Tick,
                    tierLimit,
                    comparedCount,
                    observationIntervals,
                    _agents.Power[combatCutoffAgent].Total,
                    HasEnteredCultivation(realmCutoffAgent),
                    _agents.RealmStages[realmCutoffAgent],
                    _agents.RealmLayers[realmCutoffAgent],
                    _agents.SpiritStones[wealthCutoffAgent],
                    combatChanged,
                    realmChanged,
                    wealthChanged,
                    GetChangeBasisPoints(combatChanged, comparedCount, observationIntervals),
                    GetChangeBasisPoints(realmChanged, comparedCount, observationIntervals),
                    GetChangeBasisPoints(wealthChanged, comparedCount, observationIntervals));
            }
        }

        return results;
    }

    private static int GetChangeBasisPoints(
        long changedCount,
        int comparedCount,
        long observationIntervals)
    {
        if (comparedCount == 0 || observationIntervals <= 0)
        {
            return 0;
        }

        var denominator = checked((long)comparedCount * observationIntervals);
        return checked((int)(changedCount * 10_000 / denominator));
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
