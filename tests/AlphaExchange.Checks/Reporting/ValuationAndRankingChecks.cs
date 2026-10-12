using AlphaExchange.Core;
using System.Text.Json;

static class ValuationAndRankingChecks
{
    public static void Run(Action<bool, string> check)
    {
        var game = new GameEngine(909);
        foreach (decimal fraction in new[] { 0m, .5m, -.5m, .123m })
        {
            foreach (var stock in game.State.Stocks) stock.FractionalMark = fraction;
            var prices = game.State.Stocks.Select(stock => new ShareValuation(stock)).ToArray();
            foreach (var trader in game.State.Bots.Take(8))
            {
                for (int i = 0; i < prices.Length; i++)
                {
                    trader.Shares[i] = i % 3 == 0 ? i + 1 : 0;
                    trader.ShortShares[i] = i % 4 == 0 ? i + 2 : 0;
                    trader.AverageCost[i] = 1234.5 + i;
                    trader.ShortAveragePrice[i] = 3456.5 + i;
                }
                var individual = new FinancialStatement();
                var bulk = new FinancialStatement();
                long a = trader.AccumulateFinancials(individual, game.State.Stocks);
                long b = trader.AccumulateFinancials(bulk, prices);
                check(a == b && JsonSerializer.Serialize(individual) == JsonSerializer.Serialize(bulk),
                    "Bulk valuation preserves all ledger fields and decimal rounding");
            }
        }
        check(new ShareValuation(new Stock { Price = 10, FractionalMark = .5m }).Value(1) == 10,
            "Fractional midpoint rounds to even");
        bool overflow = false;
        try { new ShareValuation(new Stock { Price = int.MaxValue }).Value(long.MaxValue); }
        catch (OverflowException) { overflow = true; }
        check(overflow, "Bulk valuation preserves checked integer overflow");

        game = new GameEngine(910);
        for (int hour = 0; hour < 25; hour++) game.AdvanceHour();
        var trader0 = game.State.Bots[0];
        trader0.Generation++;
        trader0.OpeningSnapshot = new TraderSnapshot { Id = trader0.Id, Generation = trader0.Generation, ReturnIndex = 1 };
        foreach (var period in Enum.GetValues<ComparisonPeriod>())
        {
            game.State.ComparisonFrom = 0;
            game.State.ComparisonTo = 24;
            var range = game.PeriodSummary(period);
            foreach (var metric in Enum.GetValues<RankingMetric>())
            {
                double OriginalValue(Trader trader)
                {
                    var start = period == ComparisonPeriod.All ? trader.OpeningSnapshot : period == ComparisonPeriod.Season ? trader.SeasonSnapshot
                        : range.Start.Institutions.FirstOrDefault(row => row.Id == trader.Id) ?? trader.OpeningSnapshot;
                    var live = new TraderSnapshot
                    {
                        Id = trader.Id,
                        Generation = trader.Generation,
                        Cash = trader.Cash,
                        Equity = trader.Equity(game.State.Stocks),
                        NetIncome = trader.Financials(game.State.Stocks).NetIncome - trader.NetContribution,
                        ReturnIndex = trader.ReturnIndex,
                        Volume = trader.TradedVolume,
                        Turnover = trader.TradedTurnover
                    };
                    var end = period == ComparisonPeriod.Custom ? range.End.Institutions.FirstOrDefault(row => row.Id == trader.Id) ?? live : live;
                    if (start.Generation != end.Generation) start = end.Generation == trader.Generation ? trader.OpeningSnapshot
                        : new TraderSnapshot { Id = end.Id, Generation = end.Generation, ReturnIndex = 1 };
                    return metric switch
                    {
                        RankingMetric.Assets => live.Equity,
                        RankingMetric.Cash => trader.Cash,
                        RankingMetric.Volume => end.Volume - start.Volume,
                        RankingMetric.Turnover => end.Turnover - start.Turnover,
                        RankingMetric.NetIncome => end.NetIncome - start.NetIncome,
                        _ => end.ReturnIndex / Math.Max(1e-12, start.ReturnIndex) - 1
                    };
                }
                var expected = game.State.Bots.Select(trader => (Trader: trader, Value: OriginalValue(trader)))
                    .OrderByDescending(row => row.Value).ThenBy(row => row.Trader.Id).ToArray();
                var rows = game.RankingRows(metric, period);
                for (int i = 0; i < rows.Count; i++)
                    check(rows[i].Trader == expected[i].Trader && rows[i].Value == expected[i].Value && rows[i].Rank == i + 1,
                        "Ranking preserves all metrics, periods, ties and generation boundaries");
            }
        }
        check(game.RankOf(-1) == 0, "Missing trader has no rank");
    }
}
