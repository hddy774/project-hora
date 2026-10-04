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
}
public sealed partial class GameEngine
{
    MarketStatistics? cachedStats;
    public MarketStatistics Statistics()
    {
        if (cachedStats is not null) return cachedStats;
        var result = new MarketStatistics();
        long[] institutionShares = new long[State.Stocks.Count];
        foreach (var t in State.Bots)
        { result.Institutions.Add(t.Financials(State.Stocks)); for (int i = 0; i < State.Stocks.Count; i++) institutionShares[i] += t.Shares[i]; }
        foreach (var t in State.Retail) result.Retail.Add(t.Financials(State.Stocks));
        foreach (var sector in State.Stocks.Select((s, i) => (s, i)).GroupBy(x => x.s.Sector))
        {
            long cap = sector.Sum(x => x.s.TotalShares * x.s.Price), opening = sector.Sum(x => x.s.TotalShares * x.s.DayOpenPrice);
            long inst = sector.Sum(x => institutionShares[x.i]), total = sector.Sum(x => x.s.TotalShares - State.Bank.ShareInventory[x.i]);
            result.Sectors.Add(new SectorStatistics(sector.Key, sector.Count(), cap, (double)cap / Math.Max(1, opening) - 1,
                sector.Sum(x => x.s.DayVolume), sector.Sum(x => x.s.DayTurnover), inst, total - inst));
        }
        result.Capitalization = result.Sectors.Sum(s => s.Capitalization);
        result.Change = (double)result.Capitalization / Math.Max(1, State.Stocks.Sum(s => s.TotalShares * s.DayOpenPrice)) - 1;
        result.DayVolume = State.Stocks.Sum(s => s.DayVolume); result.DayTurnover = State.Stocks.Sum(s => s.DayTurnover);
        result.BidQuantity = State.Orders.Where(o => o.Buy).Sum(o => (long)o.Remaining);
        result.AskQuantity = State.Orders.Where(o => !o.Buy).Sum(o => (long)o.Remaining);
        result.Rising = State.Stocks.Count(s => s.Change > 0); result.Falling = State.Stocks.Count(s => s.Change < 0); result.Unchanged = State.Stocks.Count - result.Rising - result.Falling;
        return cachedStats = result;
    }
}
