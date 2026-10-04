namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    TraderSnapshot CaptureTrader(Trader t) => new() { Id = t.Id, Equity = t.Equity(State.Stocks), Cash = t.Cash,
        NetIncome = t.Financials(State.Stocks).NetIncome, Volume = t.TradedVolume, Turnover = t.TradedTurnover };
    void InitializeHistory()
    {
        foreach (var t in Participants) { t.OpeningSnapshot = CaptureTrader(t); t.SeasonSnapshot = CaptureTrader(t); }
        State.OpeningSnapshot = State.SeasonSnapshot = CaptureSnapshot(); State.DailyHistory = [State.OpeningSnapshot];
    }
    public DailySnapshot CaptureSnapshot()
    {
        var stats = Statistics();
        return new DailySnapshot { Hour = State.CompletedHours, Capitalization = stats.Capitalization,
            Volume = State.Stocks.Sum(s => s.TotalVolume), Turnover = State.Stocks.Sum(s => s.TotalTurnover),
            InstitutionEquity = stats.Institutions.Equity, RetailEquity = stats.Retail.Equity,
            InstitutionCash = stats.Institutions.Cash, RetailCash = stats.Retail.Cash,
            Institutions = State.Bots.Select(CaptureTrader).ToList(), StockVolumes = State.Stocks.Select(s => s.TotalVolume).ToArray(),
            StockTurnovers = State.Stocks.Select(s => s.TotalTurnover).ToArray(), StockPrices = State.Stocks.Select(s => s.Price).ToArray() };
    }
    public PeriodStatistics Period(ComparisonPeriod period)
    {
        var end = CaptureSnapshot(); DailySnapshot start; bool partial = false;
        long target = period switch { ComparisonPeriod.TenDays => end.Hour - 240, ComparisonPeriod.FiveDays => end.Hour - 120,
            ComparisonPeriod.PreviousDay => end.Hour - 24, _ => 0 };
        if (period == ComparisonPeriod.All) start = State.OpeningSnapshot ?? end;
        else if (period == ComparisonPeriod.Season) start = State.SeasonSnapshot ?? end;
        else
        {
            start = State.DailyHistory.LastOrDefault(x => x.Hour <= Math.Max(0, target)) ?? State.DailyHistory.FirstOrDefault() ?? end;
            partial = target < start.Hour;
        }
        var points = State.DailyHistory.Where(x => x.Hour > start.Hour && x.Hour < end.Hour).Prepend(start).Append(end).ToList();
        // Lifetime totals use the original baseline; a bounded chart reports the gap.
        if (period == ComparisonPeriod.All && State.DailyHistory.Count > 0 && State.DailyHistory[0].Hour > start.Hour + 24) partial = true;
        return new PeriodStatistics(start, end, points, partial);
    }
    public double RankingValue(Trader t, RankingMetric metric, ComparisonPeriod period, DailySnapshot? basis = null)
    {
        var start = period == ComparisonPeriod.All ? t.OpeningSnapshot : period == ComparisonPeriod.Season ? t.SeasonSnapshot
            : (basis ?? Period(period).Start).Institutions.FirstOrDefault(x => x.Id == t.Id) ?? t.OpeningSnapshot;
        return metric switch { RankingMetric.Assets => t.Equity(State.Stocks), RankingMetric.Cash => t.Cash,
            RankingMetric.Volume => t.TradedVolume - start.Volume, RankingMetric.Turnover => t.TradedTurnover - start.Turnover,
            RankingMetric.NetIncome => t.Financials(State.Stocks).NetIncome - start.NetIncome,
            _ => (double)(t.Equity(State.Stocks) - start.Equity) / Math.Max(1, start.Equity) };
    }
}
