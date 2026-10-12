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
    public double RankingValue(Trader t, RankingMetric metric, ComparisonPeriod period, DailySnapshot? basis = null, DailySnapshot? finish = null)
    {
        if (metric == RankingMetric.Assets) return t.Equity(State.Stocks);
        if (metric == RankingMetric.Cash) return t.Cash;
        // Scalar current-period metrics need neither a financial statement nor
        // a temporary trader snapshot. Preserve the generation boundary.
        if (period is ComparisonPeriod.All or ComparisonPeriod.Season)
        {
            var currentStart = period == ComparisonPeriod.All ? t.OpeningSnapshot : t.SeasonSnapshot;
            if (currentStart.Generation != t.Generation) currentStart = t.OpeningSnapshot;
            return metric switch
            {
                RankingMetric.Volume => t.TradedVolume - currentStart.Volume,
                RankingMetric.Turnover => t.TradedTurnover - currentStart.Turnover,
                RankingMetric.NetIncome => t.Financials(State.Stocks).NetIncome - t.NetContribution - currentStart.NetIncome,
                _ => t.ReturnIndex / Math.Max(1e-12, currentStart.ReturnIndex) - 1
            };
        }
        var start = period == ComparisonPeriod.All ? t.OpeningSnapshot : period == ComparisonPeriod.Season ? t.SeasonSnapshot
            : FindRankingSnapshot(basis ?? PeriodSummary(period).Start, t.Id) ?? t.OpeningSnapshot;
        var end = period == ComparisonPeriod.Custom ? FindRankingSnapshot(finish ?? PeriodSummary(period).End, t.Id) ?? CaptureTrader(t) : CaptureTrader(t);
        // A slot's successor is a distinct institution. Period comparisons never
        // subtract the old generation's trading totals from the new generation.
        if (start.Generation != end.Generation) start = end.Generation == t.Generation ? t.OpeningSnapshot : new TraderSnapshot { Id = end.Id, Generation = end.Generation, ReturnIndex = 1 };
        return metric switch
        {
            RankingMetric.Assets => t.Equity(State.Stocks),
            RankingMetric.Cash => t.Cash,
            RankingMetric.Volume => end.Volume - start.Volume,
            RankingMetric.Turnover => end.Turnover - start.Turnover,
            RankingMetric.NetIncome => end.NetIncome - start.NetIncome,
            _ => end.ReturnIndex / Math.Max(1e-12, start.ReturnIndex) - 1
        };
    }

    static TraderSnapshot? FindRankingSnapshot(DailySnapshot source, int id)
    {
        foreach (var row in source.Institutions)
            if (row.Id == id) return row;
        return null;
    }
}
