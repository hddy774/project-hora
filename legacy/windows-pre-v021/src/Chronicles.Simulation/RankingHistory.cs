using Chronicles.Core;

namespace Chronicles.Simulation;

internal readonly record struct RankingHistoryFrame(
    SimulationTick Tick,
    int[] CombatPowerTop,
    int[] RealmTop,
    int[] WealthTop,
    long[] CombatPowerPositionChangeTotals,
    long[] RealmPositionChangeTotals,
    long[] WealthPositionChangeTotals);

internal sealed class RankingHistory
{
    public const int MaximumTrackedRank = 1_000;

    private static readonly int[] TierLimits = [1, 3, 5, 10, 100, 500, 1_000];

    private static readonly RankingMovementPeriod[] Periods =
    [
        RankingMovementPeriod.OneYear,
        RankingMovementPeriod.TenYears,
        RankingMovementPeriod.HundredYears,
        RankingMovementPeriod.ThousandYears,
        RankingMovementPeriod.TenThousandYears,
        RankingMovementPeriod.All,
    ];

    private readonly List<RankingHistoryFrame> _frames = [];

    public IReadOnlyList<RankingHistoryFrame> Frames => _frames;

    public static IReadOnlyList<RankingMovementPeriod> AllPeriods => Periods;

    public bool Record(
        SimulationTick tick,
        ReadOnlySpan<int> combatPowerRanking,
        ReadOnlySpan<int> realmRanking,
        ReadOnlySpan<int> wealthRanking,
        bool force = false)
    {
        if (!force
            && _frames.Count > 0
            && GameCalendar.GetYear(_frames[^1].Tick) >= GameCalendar.GetYear(tick))
        {
            return false;
        }

        var count = Math.Min(MaximumTrackedRank, combatPowerRanking.Length);
        var combatPowerTop = combatPowerRanking[..count].ToArray();
        var realmTop = realmRanking[..count].ToArray();
        var wealthTop = wealthRanking[..count].ToArray();
        var combatPowerChanges = CreateNextPositionChangeTotals(
            combatPowerTop,
            _frames.Count == 0 ? null : _frames[^1].CombatPowerTop,
            _frames.Count == 0 ? null : _frames[^1].CombatPowerPositionChangeTotals);
        var realmChanges = CreateNextPositionChangeTotals(
            realmTop,
            _frames.Count == 0 ? null : _frames[^1].RealmTop,
            _frames.Count == 0 ? null : _frames[^1].RealmPositionChangeTotals);
        var wealthChanges = CreateNextPositionChangeTotals(
            wealthTop,
            _frames.Count == 0 ? null : _frames[^1].WealthTop,
            _frames.Count == 0 ? null : _frames[^1].WealthPositionChangeTotals);
        _frames.Add(new RankingHistoryFrame(
            tick,
            combatPowerTop,
            realmTop,
            wealthTop,
            combatPowerChanges,
            realmChanges,
            wealthChanges));
        Prune(GameCalendar.GetYear(tick));
        return true;
    }

    public RankingHistoryFrame GetBaseline(RankingMovementPeriod period, SimulationTick currentTick)
    {
        if (_frames.Count == 0)
        {
            throw new InvalidOperationException("랭킹 이력 기준점이 없습니다.");
        }

        if (period == RankingMovementPeriod.All)
        {
            return _frames[0];
        }

        var targetTick = Math.Max(0, currentTick.Value - GetPeriodTicks(period));
        for (var index = _frames.Count - 1; index >= 0; index--)
        {
            if (_frames[index].Tick.Value <= targetTick)
            {
                return _frames[index];
            }
        }

        return _frames[0];
    }

    public RankingHistoryCheckpoint CreateCheckpoint() => new(
        _frames.Select(frame => new RankingHistoryFrameCheckpoint(
            frame.Tick.Value,
            frame.CombatPowerTop.ToArray(),
            frame.RealmTop.ToArray(),
            frame.WealthTop.ToArray(),
            frame.CombatPowerPositionChangeTotals.ToArray(),
            frame.RealmPositionChangeTotals.ToArray(),
            frame.WealthPositionChangeTotals.ToArray())).ToArray());

    public void Restore(RankingHistoryCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        _frames.Clear();
        foreach (var frame in checkpoint.Frames)
        {
            var combatPowerTop = frame.CombatPowerTop.ToArray();
            var realmTop = frame.RealmTop.ToArray();
            var wealthTop = frame.WealthTop.ToArray();
            _frames.Add(new RankingHistoryFrame(
                new SimulationTick(frame.Tick),
                combatPowerTop,
                realmTop,
                wealthTop,
                RestoreOrRebuildPositionChangeTotals(
                    frame.CombatPowerPositionChangeTotals,
                    combatPowerTop,
                    _frames.Count == 0 ? null : _frames[^1].CombatPowerTop,
                    _frames.Count == 0 ? null : _frames[^1].CombatPowerPositionChangeTotals),
                RestoreOrRebuildPositionChangeTotals(
                    frame.RealmPositionChangeTotals,
                    realmTop,
                    _frames.Count == 0 ? null : _frames[^1].RealmTop,
                    _frames.Count == 0 ? null : _frames[^1].RealmPositionChangeTotals),
                RestoreOrRebuildPositionChangeTotals(
                    frame.WealthPositionChangeTotals,
                    wealthTop,
                    _frames.Count == 0 ? null : _frames[^1].WealthTop,
                    _frames.Count == 0 ? null : _frames[^1].WealthPositionChangeTotals)));
        }
    }

    public long GetPositionChangeCount(
        RankingMovementPeriod period,
        RankingCategory category,
        SimulationTick currentTick,
        ReadOnlySpan<int> currentRanking,
        int topCount)
    {
        var baseline = GetBaseline(period, currentTick);
        var latest = _frames[^1];
        var tierIndex = GetTierIndex(topCount);
        var baselineTotal = GetPositionChangeTotals(baseline, category)[tierIndex];
        var latestTotal = GetPositionChangeTotals(latest, category)[tierIndex];
        var latestRanking = GetRanking(latest, category);
        var liveChanges = CountPositionalChanges(currentRanking, latestRanking, topCount);
        return checked(latestTotal - baselineTotal + liveChanges);
    }

    public static long GetObservationIntervalCount(
        RankingHistoryFrame baseline,
        SimulationTick currentTick)
    {
        var fromYear = GameCalendar.GetYear(baseline.Tick);
        var toYear = GameCalendar.GetYear(currentTick);
        return Math.Max(1, toYear - fromYear);
    }

    public static int CountPositionalChanges(
        ReadOnlySpan<int> currentRanking,
        ReadOnlySpan<int> baselineRanking,
        int count)
    {
        var actualCount = Math.Min(count, Math.Min(currentRanking.Length, baselineRanking.Length));
        var changed = 0;
        for (var index = 0; index < actualCount; index++)
        {
            if (currentRanking[index] != baselineRanking[index])
            {
                changed++;
            }
        }

        return changed;
    }

    public static long GetPeriodTicks(RankingMovementPeriod period) => period switch
    {
        RankingMovementPeriod.OneYear => GameCalendar.TicksPerYear,
        RankingMovementPeriod.TenYears => 10 * GameCalendar.TicksPerYear,
        RankingMovementPeriod.HundredYears => 100 * GameCalendar.TicksPerYear,
        RankingMovementPeriod.ThousandYears => 1_000 * GameCalendar.TicksPerYear,
        RankingMovementPeriod.TenThousandYears => 10_000 * GameCalendar.TicksPerYear,
        RankingMovementPeriod.All => long.MaxValue,
        _ => throw new ArgumentOutOfRangeException(nameof(period)),
    };

    private static long[] CreateNextPositionChangeTotals(
        ReadOnlySpan<int> currentRanking,
        int[]? previousRanking,
        long[]? previousTotals)
    {
        var totals = previousTotals?.ToArray() ?? new long[TierLimits.Length];
        if (previousRanking is null)
        {
            return totals;
        }

        for (var tier = 0; tier < TierLimits.Length; tier++)
        {
            totals[tier] = checked(
                totals[tier] + CountPositionalChanges(
                    currentRanking,
                    previousRanking,
                    TierLimits[tier]));
        }

        return totals;
    }

    private static long[] RestoreOrRebuildPositionChangeTotals(
        long[]? persistedTotals,
        ReadOnlySpan<int> currentRanking,
        int[]? previousRanking,
        long[]? previousTotals) =>
        persistedTotals is { Length: 7 }
            ? persistedTotals.ToArray()
            : CreateNextPositionChangeTotals(currentRanking, previousRanking, previousTotals);

    private static int GetTierIndex(int topCount)
    {
        var index = Array.IndexOf(TierLimits, topCount);
        return index >= 0
            ? index
            : throw new ArgumentOutOfRangeException(nameof(topCount), topCount, "지원하지 않는 랭킹 구간입니다.");
    }

    private static int[] GetRanking(RankingHistoryFrame frame, RankingCategory category) => category switch
    {
        RankingCategory.CombatPower => frame.CombatPowerTop,
        RankingCategory.Realm => frame.RealmTop,
        RankingCategory.Wealth => frame.WealthTop,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "변동 이력을 제공하지 않는 랭킹입니다."),
    };

    private static long[] GetPositionChangeTotals(
        RankingHistoryFrame frame,
        RankingCategory category) => category switch
    {
        RankingCategory.CombatPower => frame.CombatPowerPositionChangeTotals,
        RankingCategory.Realm => frame.RealmPositionChangeTotals,
        RankingCategory.Wealth => frame.WealthPositionChangeTotals,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "변동 이력을 제공하지 않는 랭킹입니다."),
    };

    private void Prune(long currentYear)
    {
        if (_frames.Count <= 2)
        {
            return;
        }

        var keep = new bool[_frames.Count];
        keep[0] = true;
        keep[^1] = true;
        var retainedBuckets = new HashSet<(int Level, long Bucket)>();
        for (var index = _frames.Count - 2; index > 0; index--)
        {
            var age = Math.Max(0, currentYear - GameCalendar.GetYear(_frames[index].Tick));
            var bucket = GetRetentionBucket(age);
            if (bucket is { } value && retainedBuckets.Add(value))
            {
                keep[index] = true;
            }
        }

        for (var index = keep.Length - 1; index >= 0; index--)
        {
            if (!keep[index])
            {
                _frames.RemoveAt(index);
            }
        }
    }

    private static (int Level, long Bucket)? GetRetentionBucket(long ageYears)
    {
        if (ageYears <= 12)
        {
            return (0, ageYears);
        }
        if (ageYears <= 120)
        {
            return (1, ageYears / 10);
        }
        if (ageYears <= 1_200)
        {
            return (2, ageYears / 100);
        }
        if (ageYears <= 12_000)
        {
            return (3, ageYears / 1_000);
        }

        return null;
    }
}
