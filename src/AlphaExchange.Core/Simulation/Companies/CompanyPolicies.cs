namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    public static readonly string[] CompanyStrategyNames = ["성장·연구", "가치·수익성", "방어·안정"];
    public static readonly string[] ManagementNames = ["설비 확장", "비용 효율", "현금·부채 관리"];
    public static readonly string[] AllocationNames = ["재투자", "자사주 확대", "현금 배당"];
    void ApplyCompanyPolicies(Stock stock, out double revenue, out double cost, out double investment, out double payout, out double reserve)
    {
        (revenue, cost, investment, reserve) = stock.CompanyStrategy switch
        { CompanyStrategy.Growth => (1.10, 1.06, .35, 1.2), CompanyStrategy.Defensive => (.93, .85, .10, 2.0), _ => (1.02, .97, .20, 1.5) };
        if (stock.Management == ManagementPolicy.Expansion) { revenue *= 1.03; cost *= 1.02; investment += .10; }
        if (stock.Management == ManagementPolicy.Efficiency) cost *= .94;
        if (stock.Management == ManagementPolicy.Liquidity) { investment *= .5; reserve = Math.Max(2, reserve); }
        payout = stock.Allocation switch { CapitalAllocation.Dividend => .45, CapitalAllocation.Buyback => .10, _ => .08 };
        if (stock.Allocation == CapitalAllocation.Reinvestment) investment += .15;
    }
    void SeekTreasuryOwnership(Stock stock)
    {
        if (!stock.Active || stock.TotalShares <= 0 || stock.Report.Equity <= 0 || (double)stock.TreasuryShares / stock.TotalShares >= stock.TreasuryTarget) return;
        ApplyCompanyPolicies(stock, out _, out _, out _, out _, out double reserve);
        long free = Math.Max(0, stock.Report.Cash - (long)(stock.BaseRevenue * reserve));
        long budget = (long)(free * (stock.Allocation == CapitalAllocation.Buyback ? .08 : .015));
        long need = Math.Max(0, (long)Math.Ceiling(stock.TotalShares * stock.TreasuryTarget) - stock.TreasuryShares);
        long quantity = Math.Min(need, budget / Math.Max(1, stock.Price)); if (quantity == 0) return;
        int index = SecurityIndex(stock.SecurityId); CancelOrders(0);
        long bankShares = Math.Max(0, State.Bank.ShareInventory[index] - State.Bank.ReservedLending[index] - Math.Max(10, stock.TotalShares / 20));
        long fromBank = Math.Min(quantity, bankShares);
        if (fromBank > 0 && Buyback(stock.SecurityId, 0, fromBank) is null) quantity -= fromBank;
        if (quantity == 0) return;
        // Voluntary board tender: obtain real holders' stock with company cash.
        foreach (var t in State.Bots)
        {
            if (quantity == 0) break;
            long offered = Math.Min(quantity, ExposureSellCapacity(t, index, stock.Price));
            if (offered > 0 && t.AverageCost[index] <= stock.Price && Buyback(stock.SecurityId, t.Id, offered) is null) quantity -= offered;
        }
    }
}
