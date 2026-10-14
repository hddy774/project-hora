namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    // Policy belongs to AI investment orders. The public matching primitive is
    // also used by funded underwriting/settlement and isolated ledger checks.
    internal string? SubmitInstitutionOrder(int ownerId, int index, bool buy, int price, int quantity,
        int lifetime = 1, bool shortSale = false, bool cover = false, bool liquidity = false)
        => SubmitOrder(ownerId, index, buy, price, quantity, lifetime, shortSale, cover, liquidity, enforceAllocation: true);

    public const double MaximumInstitutionCashRatio = .5;

    public double DesiredCashRatio(Trader trader, bool defensive = false)
    {
        var r = Rules.Investment;
        double cash = r.BaseCash + r.DispositionCash[(int)trader.Disposition] - trader.Risk * r.RiskCashEffect;
        if (defensive) cash += r.DefensiveCash * trader.Abilities.RiskManagement / 100.0;
        return Math.Clamp(cash, r.MinimumCash, Math.Min(MaximumInstitutionCashRatio, r.MaximumCash));
    }

    public long StockAllocationShortfall(Trader trader)
    {
        long holdings = trader.GrossAssets(State.Stocks) - trader.Cash;
        return Math.Max(0, (trader.Cash - holdings + 1) / 2);
    }

    // Reserve the worst possible cash/stock movement of every pending sale.
    // A later price change may create a shortfall; it never authorizes more sales.
    internal int ExposureSellCapacity(Trader trader, int index, int price)
    {
        if (trader.Id == 0 || trader.IsRetail) return (int)Math.Min(1_000_000, trader.Shares[index] - trader.ReservedShares[index]);
        decimal surplus = trader.GrossAssets(State.Stocks) - 2m * trader.Cash;
        if (ownedOrders.TryGetValue(trader.Id, out var orders))
            foreach (var order in orders)
                if (!order.Buy && !order.Short)
                    surplus -= (State.Stocks[order.StockIndex].MarkPrice + DailyPriceLimits.For(State.Stocks[order.StockIndex]).Upper) * order.Remaining;
        decimal unit = State.Stocks[index].MarkPrice + Math.Max(price, DailyPriceLimits.For(State.Stocks[index]).Upper);
        long allowed = (long)Math.Max(0, decimal.Floor(surplus / unit));
        return (int)Math.Min(1_000_000, Math.Min(allowed, trader.Shares[index] - trader.ReservedShares[index]));
    }

    long RemainingTurnoverBudget(Trader trader)
    {
        var d = trader.Development!;
        long gross = Math.Max(1, trader.GrossAssets(State.Stocks));
        long spent = d.TurnoverDay == State.CompletedHours / 24 ? d.DayTurnover : 0;
        long pending = 0;
        if (ownedOrders.TryGetValue(trader.Id, out var orders))
            foreach (var order in orders) pending += (long)order.Price * order.Remaining;
        double dailyRatio = .12 + trader.Risk * .18;
        long recovery = Math.Max(0, trader.Cash - (long)(gross * (trader.Development!.TargetCashRatio + .02)));
        long allowance = Math.Max(0, Math.Max((long)(gross * dailyRatio), recovery) - spent - pending);
        // Already completed recovery purchases must not be subtracted twice.
        // The stock floor has priority over the discretionary turnover budget.
        if (StockAllocationShortfall(trader) > 0)
            allowance = Math.Max(allowance, trader.Cash - (long)(gross * .45) - pending);
        return Math.Max(0, allowance);
    }

    void RecordInstitutionTurnover(Trader trader, long amount)
    {
        if (trader.Development is not { } d) return;
        long day = State.CompletedHours / 24;
        if (d.TurnoverDay != day) { d.TurnoverDay = day; d.DayTurnover = 0; }
        d.DayTurnover = checked(d.DayTurnover + amount);
    }

    int LimitedQuote(int index, double price, bool buy)
        => DailyPriceLimits.For(State.Stocks[index]).Clamp(Quote(price, buy));

    void ResetTradingDay()
    {
        foreach (var stock in State.Stocks)
        {
            stock.DayOpenPrice = stock.Price;
            stock.LimitReferencePrice = stock.MarkPrice;
            stock.DayVolume = stock.DayTurnover = 0;
        }
        foreach (var order in State.Orders)
            if (order.Remaining > 0 && !DailyPriceLimits.For(State.Stocks[order.StockIndex]).Contains(order.Price)) CancelOrder(order.Id);
    }
}
