namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    public IHistoryQuery? HistorySource { get; set; }
    public event Action<DailySnapshot>? HourRecorded;
    void RecordHour() { if (HourRecorded is not null) HourRecorded(CaptureSnapshot()); }
    TraderSnapshot CaptureTrader(Trader t)
    {
        var financials=t.Financials(State.Stocks);
        return new TraderSnapshot { Id=t.Id,Generation=t.Generation,Equity=financials.Equity,Cash=t.Cash,NetIncome=financials.NetIncome-t.NetContribution,
            ReturnIndex=t.ReturnIndex,NetContribution=t.NetContribution,Volume=t.TradedVolume,Turnover=t.TradedTurnover };
    }
    void InitializeHistory()
    {
        foreach (var t in State.Bots) { t.OpeningSnapshot = CaptureTrader(t); t.SeasonSnapshot = CaptureTrader(t); }
        State.OpeningSnapshot = State.SeasonSnapshot = CaptureSnapshot(); State.DailyHistory = [State.OpeningSnapshot];
    }
    public DailySnapshot CaptureSnapshot()
    {
        var stats = Statistics();
        return new DailySnapshot { CompanyBankruptcies=State.BankruptcyTotals.Companies,InstitutionBankruptcies=State.BankruptcyTotals.Institutions,RetailBankruptcies=State.BankruptcyTotals.Retail,
            CohortTradingBasis=6,InstitutionTradingIncome=stats.InstitutionTradingIncome,RetailTradingIncome=stats.RetailTradingIncome,
            CashFlows = new Dictionary<string, long>(State.CashFlows), SecurityIds = State.Stocks.Select(s => s.SecurityId).ToArray(),
            StockSectors = State.Stocks.Select(s => s.Sector).ToArray(), StockShares = State.Stocks.Select(s => s.Active ? s.OutstandingShares : 0).ToArray(),
            FloatShares = State.Stocks.Select(s => s.Active ? s.FloatShares : 0).ToArray(), SplitFactors = State.Stocks.Select(s => s.SplitFactor).ToArray(),
            MarkPrices = State.Stocks.Select(s => s.MarkPrice).ToArray(), PriceIndex = State.PriceIndex, TotalReturnIndex = State.TotalReturnIndex,
            CompanyEquity = State.Stocks.Sum(s => s.Report.Equity), CompanyProfit = State.Stocks.Sum(s => s.Report.NetIncome),
            Dividends = State.MarketDividends, GovernmentCash = State.Government.Cash, BankCash = State.Bank.Cash,
            ExchangeCash = State.FeePool, RealEconomyCash = State.RealEconomy.Cash,
            Hour = State.CompletedHours, Capitalization = stats.Capitalization,
            Volume = State.MatchedVolume, Turnover = State.MatchedTurnover,
            SectorVolumes=new(State.SectorVolumes),SectorTurnovers=new(State.SectorTurnovers),
            InstitutionEquity = stats.Institutions.Equity, RetailEquity = stats.Retail.Equity,
            InstitutionCash = stats.Institutions.Cash, RetailCash = stats.Retail.Cash,
            Institutions = State.Bots.Select(CaptureTrader).ToList(), StockVolumes = State.Stocks.Select(s => s.TotalVolume).ToArray(),
            StockTurnovers = State.Stocks.Select(s => s.TotalTurnover).ToArray(), StockPrices = State.Stocks.Select(s => s.Price).ToArray() };
    }
    PeriodStatistics? cachedPeriod;
    PeriodStatistics? cachedSummary;
    (GameState State,ComparisonPeriod Period,long Hour,long From,long To,long Transactions,long Events,IHistoryQuery? Source) periodKey,summaryKey;
    public PeriodStatistics PeriodSummary(ComparisonPeriod period) => Period(period,false);
    public PeriodStatistics Period(ComparisonPeriod period)
        => Period(period,true);
    PeriodStatistics Period(ComparisonPeriod period,bool chart)
    {
        var key=(State,period,State.CompletedHours,State.ComparisonFrom,State.ComparisonTo,State.NextTransactionId,State.NextCorporateEventId,HistorySource);
        if(chart && cachedPeriod is not null && periodKey==key) return cachedPeriod;
        if(!chart && cachedSummary is not null && summaryKey==key) return cachedSummary;
        if(chart)
        {
            var bounds=Period(period,false);
            var records=HistorySource?.Range(bounds.Start.Hour,bounds.End.Hour,500) ?? State.DailyHistory;
            if(records.LastOrDefault(r=>r.Hour==bounds.End.Hour) is {} recordedEnd) bounds.End.GapBefore=recordedEnd.GapBefore;
            var points=records.Where(x=>x.Hour>bounds.Start.Hour && x.Hour<bounds.End.Hour).Prepend(bounds.Start).Append(bounds.End).ToList();
            periodKey=key; return cachedPeriod=new PeriodStatistics(bounds.Start,bounds.End,points,bounds.Partial);
        }
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
        // Lifetime totals use the original baseline; a bounded chart reports the gap.
        if (period == ComparisonPeriod.All && HistorySource is null && State.DailyHistory.Count > 0 && State.DailyHistory[0].Hour > start.Hour + 24) partial = true;
        if (HistorySource is not null && !HistorySource.Covers(start.Hour, end.Hour)) partial = true;
        summaryKey=key; return cachedSummary=new PeriodStatistics(start,end,[start,end],partial);
    }
    public double RankingValue(Trader t, RankingMetric metric, ComparisonPeriod period, DailySnapshot? basis = null, DailySnapshot? finish = null)
    {
        var start = period == ComparisonPeriod.All ? t.OpeningSnapshot : period == ComparisonPeriod.Season ? t.SeasonSnapshot
            : (basis ?? PeriodSummary(period).Start).Institutions.FirstOrDefault(x => x.Id == t.Id) ?? t.OpeningSnapshot;
        var end = period == ComparisonPeriod.Custom ? (finish ?? PeriodSummary(period).End).Institutions.FirstOrDefault(x => x.Id == t.Id) ?? CaptureTrader(t) : CaptureTrader(t);
        // A slot's successor is a distinct institution. Period comparisons never
        // subtract the old generation's trading totals from the new generation.
        if(start.Generation!=end.Generation) start=end.Generation==t.Generation ? t.OpeningSnapshot : new TraderSnapshot { Id=end.Id,Generation=end.Generation,ReturnIndex=1 };
        return metric switch { RankingMetric.Assets => t.Equity(State.Stocks), RankingMetric.Cash => t.Cash,
            RankingMetric.Volume => end.Volume - start.Volume, RankingMetric.Turnover => end.Turnover - start.Turnover,
            RankingMetric.NetIncome => end.NetIncome - start.NetIncome,
            _ => end.ReturnIndex / Math.Max(1e-12, start.ReturnIndex) - 1 };
    }
}
