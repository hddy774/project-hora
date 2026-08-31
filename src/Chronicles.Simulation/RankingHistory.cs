using Chronicles.Core;

namespace Chronicles.Simulation;

internal readonly record struct RankingHistoryFrame(
    SimulationTick Tick,
    int[] CombatPowerTop,
    int[] RealmTop,
    int[] WealthTop);

internal sealed class RankingHistory
{
    public const int MaximumTrackedRank = 1_000;

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

    public void Record(
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
            return;
        }

        var count = Math.Min(MaximumTrackedRank, combatPowerRanking.Length);
        _frames.Add(new RankingHistoryFrame(
            tick,
            combatPowerRanking[..count].ToArray(),
            realmRanking[..count].ToArray(),
            wealthRanking[..count].ToArray()));
        Prune(GameCalendar.GetYear(tick));
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
            frame.WealthTop.ToArray())).ToArray());

    public void Restore(RankingHistoryCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        _frames.Clear();
        _frames.AddRange(checkpoint.Frames.Select(frame => new RankingHistoryFrame(
            new SimulationTick(frame.Tick),
            frame.CombatPowerTop.ToArray(),
            frame.RealmTop.ToArray(),
            frame.WealthTop.ToArray())));
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
