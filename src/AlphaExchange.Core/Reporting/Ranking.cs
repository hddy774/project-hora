namespace AlphaExchange.Core;

public readonly record struct RankedTrader(Trader Trader, double Value, int Rank);

public sealed partial class GameEngine
{
    public List<RankedTrader> RankingRows(RankingMetric metric = RankingMetric.Return,
        ComparisonPeriod period = ComparisonPeriod.Season)
    {
        var range = period is ComparisonPeriod.All or ComparisonPeriod.Season ? null : PeriodSummary(period);
        var rows = new List<RankedTrader>(State.Bots.Count);
        foreach (var trader in State.Bots)
            rows.Add(new RankedTrader(trader, RankingValue(trader, metric, period, range?.Start,
                period == ComparisonPeriod.Custom ? range?.End : null), 0));
        rows.Sort(static (left, right) =>
        {
            int value = right.Value.CompareTo(left.Value);
            return value != 0 ? value : left.Trader.Id.CompareTo(right.Trader.Id);
        });
        for (int i = 0; i < rows.Count; i++) rows[i] = rows[i] with { Rank = i + 1 };
        return rows;
    }

    public List<Trader> Ranking(RankingMetric metric = RankingMetric.Return,
        ComparisonPeriod period = ComparisonPeriod.Season)
    {
        var rows = RankingRows(metric, period);
        var result = new List<Trader>(rows.Count);
        foreach (var row in rows) result.Add(row.Trader);
        return result;
    }

    public int RankOf(int id) => RankingRows().Find(row => row.Trader.Id == id).Rank;
}
