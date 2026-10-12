namespace AlphaExchange.Core;

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
        Span<ShareValuation> prices = State.Stocks.Count <= 128
            ? stackalloc ShareValuation[State.Stocks.Count] : new ShareValuation[State.Stocks.Count];
        for (int i = 0; i < prices.Length; i++) prices[i] = new ShareValuation(State.Stocks[i]);
        long[] institutionShares = new long[State.Stocks.Count];
        foreach (var t in State.Bots)
        { long equity = t.AccumulateFinancials(result.Institutions, prices); if (updateReturns) UpdateInvestorReturn(t, equity); for (int i = 0; i < State.Stocks.Count; i++) institutionShares[i] += t.Shares[i]; }
        foreach (var t in State.Retail)
        { long equity = t.AccumulateFinancials(result.Retail, prices); if (updateReturns) UpdateInvestorReturn(t, equity); }
        result.InstitutionTradingIncome = result.Institutions.NetIncome - result.Institutions.WageIncome + result.Institutions.Consumption + State.BankruptcyTotals.ClosedInstitutionTradingIncome;
        result.RetailTradingIncome = result.Retail.NetIncome - result.Retail.WageIncome + result.Retail.Consumption + State.BankruptcyTotals.ClosedRetailTradingIncome;
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
