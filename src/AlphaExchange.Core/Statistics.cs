namespace AlphaExchange.Core;
public sealed record SectorStatistics(string Name, int Companies, long Capitalization, double Change, long Volume, long Turnover, long InstitutionShares, long RetailShares);
public sealed class MarketStatistics
{
    public FinancialStatement Institutions { get; set; } = new();
    public FinancialStatement Retail { get; set; } = new();
    public List<SectorStatistics> Sectors { get; set; } = [];
    public long Capitalization { get; set; }
    public double Change { get; set; }
    public long DayVolume { get; set; }
    public long DayTurnover { get; set; }
    public long BidQuantity { get; set; }
    public long AskQuantity { get; set; }
    public int Rising { get; set; }
    public int Falling { get; set; }
    public int Unchanged { get; set; }
    public double InstitutionTradingIncome { get; set; }
    public double RetailTradingIncome { get; set; }
}
public sealed partial class GameEngine
{
    MarketStatistics? cachedStats;
    public MarketStatistics Statistics()
    {
        if (cachedStats is not null) return cachedStats;
        return BuildStatistics(false);
    }
    MarketStatistics BuildStatistics(bool updateReturns)
    {
        var result = new MarketStatistics();
        long[] institutionShares = new long[State.Stocks.Count];
        foreach (var t in State.Bots)
        { long equity=t.AccumulateFinancials(result.Institutions,State.Stocks); if(updateReturns) UpdateInvestorReturn(t,equity); for (int i = 0; i < State.Stocks.Count; i++) institutionShares[i] += t.Shares[i]; }
        foreach (var t in State.Retail)
        { long equity=t.AccumulateFinancials(result.Retail,State.Stocks); if(updateReturns) UpdateInvestorReturn(t,equity); }
        result.InstitutionTradingIncome=result.Institutions.NetIncome-result.Institutions.WageIncome+result.Institutions.Consumption+State.BankruptcyTotals.ClosedInstitutionTradingIncome;
        result.RetailTradingIncome=result.Retail.NetIncome-result.Retail.WageIncome+result.Retail.Consumption+State.BankruptcyTotals.ClosedRetailTradingIncome;
        foreach (var sector in State.Stocks.Select((s, i) => (s, i)).Where(x => x.s.Active).GroupBy(x => x.s.Sector))
        {
            long cap = sector.Sum(x => x.s.MarketCap), opening = sector.Sum(x => x.s.OutstandingShares * x.s.DayOpenPrice);
            long inst = sector.Sum(x => institutionShares[x.i]), total = sector.Sum(x => x.s.OutstandingShares - x.s.FounderShares - State.Bank.ShareInventory[x.i]);
            result.Sectors.Add(new SectorStatistics(sector.Key, sector.Count(), cap, (double)cap / Math.Max(1, opening) - 1,
                sector.Sum(x => x.s.DayVolume), sector.Sum(x => x.s.DayTurnover), inst, total - inst));
        }
        result.Capitalization = result.Sectors.Sum(s => s.Capitalization);
        result.Change = (double)result.Capitalization / Math.Max(1, State.Stocks.Where(s => s.Active).Sum(s => s.OutstandingShares * s.DayOpenPrice)) - 1;
        result.DayVolume = State.Stocks.Sum(s => s.DayVolume); result.DayTurnover = State.Stocks.Sum(s => s.DayTurnover);
        result.BidQuantity = State.Orders.Where(o => o.Buy).Sum(o => (long)o.Remaining);
        result.AskQuantity = State.Orders.Where(o => !o.Buy).Sum(o => (long)o.Remaining);
        result.Rising = State.Stocks.Count(s => s.Active && s.Change > 0); result.Falling = State.Stocks.Count(s => s.Active && s.Change < 0); result.Unchanged = State.Stocks.Count(s => s.Active) - result.Rising - result.Falling;
        return cachedStats = result;
    }
}
