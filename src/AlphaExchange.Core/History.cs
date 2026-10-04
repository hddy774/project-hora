namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    public IHistoryQuery? HistorySource { get; set; }
    public event Action<DailySnapshot>? HourRecorded;
    void RecordHour() { if (HourRecorded is not null) HourRecorded(CaptureSnapshot()); }
    TraderSnapshot CaptureTrader(Trader t) => new() { Id = t.Id, Equity = t.Equity(State.Stocks), Cash = t.Cash,
        NetIncome = t.Financials(State.Stocks).NetIncome - t.NetContribution, ReturnIndex = t.ReturnIndex, NetContribution = t.NetContribution, Volume = t.TradedVolume, Turnover = t.TradedTurnover };
    void InitializeHistory()
    {
        foreach (var t in Participants) { t.OpeningSnapshot = CaptureTrader(t); t.SeasonSnapshot = CaptureTrader(t); }
        State.OpeningSnapshot = State.SeasonSnapshot = CaptureSnapshot(); State.DailyHistory = [State.OpeningSnapshot];
    }
    public DailySnapshot CaptureSnapshot()
    {
        var stats = Statistics();
        return new DailySnapshot { CashFlows = new Dictionary<string, long>(State.CashFlows), SecurityIds = State.Stocks.Select(s => s.SecurityId).ToArray(),
            StockSectors = State.Stocks.Select(s => s.Sector).ToArray(), StockShares = State.Stocks.Select(s => s.Active ? s.OutstandingShares : 0).ToArray(),
            FloatShares = State.Stocks.Select(s => s.Active ? s.FloatShares : 0).ToArray(), SplitFactors = State.Stocks.Select(s => s.SplitFactor).ToArray(),
            MarkPrices = State.Stocks.Select(s => s.MarkPrice).ToArray(), PriceIndex = State.PriceIndex, TotalReturnIndex = State.TotalReturnIndex,
            CompanyEquity = State.Stocks.Sum(s => s.Report.Equity), CompanyProfit = State.Stocks.Sum(s => s.Report.NetIncome),
            Dividends = State.MarketDividends, GovernmentCash = State.Government.Cash, BankCash = State.Bank.Cash,
            ExchangeCash = State.FeePool, RealEconomyCash = State.RealEconomy.Cash,
            Hour = State.CompletedHours, Capitalization = stats.Capitalization,
            Volume = State.Stocks.Sum(s => s.TotalVolume), Turnover = State.Stocks.Sum(s => s.TotalTurnover),
            InstitutionEquity = stats.Institutions.Equity, RetailEquity = stats.Retail.Equity,
            InstitutionCash = stats.Institutions.Cash, RetailCash = stats.Retail.Cash,
            Institutions = State.Bots.Select(CaptureTrader).ToList(), StockVolumes = State.Stocks.Select(s => s.TotalVolume).ToArray(),
            StockTurnovers = State.Stocks.Select(s => s.TotalTurnover).ToArray(), StockPrices = State.Stocks.Select(s => s.Price).ToArray() };
    }
    PeriodStatistics? cachedPeriod;
    (ComparisonPeriod Period,long Hour,long From,long To,long Transactions,long Events,IHistoryQuery? Source) periodKey;
    public PeriodStatistics Period(ComparisonPeriod period)
    {
        var key=(period,State.CompletedHours,State.ComparisonFrom,State.ComparisonTo,State.NextTransactionId,State.NextCorporateEventId,HistorySource);
        if(cachedPeriod is not null && periodKey==key) return cachedPeriod;
        var end = CaptureSnapshot(); DailySnapshot start; bool partial = false;
        if (period == ComparisonPeriod.Custom)
        {
            long targetEnd = State.ComparisonTo <= 0 ? end.Hour : Math.Min(State.ComparisonTo,end.Hour);
            end = HistorySource?.At(targetEnd) ?? State.DailyHistory.LastOrDefault(s => s.Hour <= targetEnd) ?? end;
            partial = end.Hour != targetEnd;
        }
        long target = period switch { ComparisonPeriod.TenDays => end.Hour - 240, ComparisonPeriod.FiveDays => end.Hour - 120,
            ComparisonPeriod.PreviousDay => end.Hour - 24, ComparisonPeriod.ThirtyDays => end.Hour - 720,
            ComparisonPeriod.NinetyDays => end.Hour - 2160, ComparisonPeriod.Year => end.Hour - 8640,
            ComparisonPeriod.Custom => Math.Clamp(State.ComparisonFrom,0,end.Hour), _ => 0 };
        if (period == ComparisonPeriod.All) start = State.OpeningSnapshot ?? end;
        else if (period == ComparisonPeriod.Season) start = State.SeasonSnapshot ?? end;
        else
        {
            start = HistorySource?.At(Math.Max(0, target)) ?? State.DailyHistory.LastOrDefault(x => x.Hour <= Math.Max(0, target)) ?? State.DailyHistory.FirstOrDefault() ?? end;
            partial |= Math.Max(0, target) != start.Hour;
        }
        var records = HistorySource?.Range(start.Hour, end.Hour, 500) ?? State.DailyHistory;
        if (records.LastOrDefault(r => r.Hour == end.Hour) is { } recordedEnd) end.GapBefore = recordedEnd.GapBefore;
        var points = records.Where(x => x.Hour > start.Hour && x.Hour < end.Hour).Prepend(start).Append(end).ToList();
        // Lifetime totals use the original baseline; a bounded chart reports the gap.
        if (period == ComparisonPeriod.All && HistorySource is null && State.DailyHistory.Count > 0 && State.DailyHistory[0].Hour > start.Hour + 24) partial = true;
        if (HistorySource is not null && !HistorySource.Covers(start.Hour, end.Hour)) partial = true;
        periodKey=key; return cachedPeriod=new PeriodStatistics(start, end, points, partial);
    }
    public double RankingValue(Trader t, RankingMetric metric, ComparisonPeriod period, DailySnapshot? basis = null, DailySnapshot? finish = null)
    {
        var start = period == ComparisonPeriod.All ? t.OpeningSnapshot : period == ComparisonPeriod.Season ? t.SeasonSnapshot
            : (basis ?? Period(period).Start).Institutions.FirstOrDefault(x => x.Id == t.Id) ?? t.OpeningSnapshot;
        var end = period == ComparisonPeriod.Custom ? (finish ?? Period(period).End).Institutions.FirstOrDefault(x => x.Id == t.Id) ?? CaptureTrader(t) : CaptureTrader(t);
        return metric switch { RankingMetric.Assets => t.Equity(State.Stocks), RankingMetric.Cash => t.Cash,
            RankingMetric.Volume => end.Volume - start.Volume, RankingMetric.Turnover => end.Turnover - start.Turnover,
            RankingMetric.NetIncome => end.NetIncome - start.NetIncome,
            _ => end.ReturnIndex / Math.Max(1e-12, start.ReturnIndex) - 1 };
    }
}
