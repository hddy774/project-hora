namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    void CancelInstitutionOrders(int ownerId, bool liquidity)
    {
        if (!ownedOrders.TryGetValue(ownerId, out var orders)) return;
        for (int i = orders.Count - 1; i >= 0; i--)
            if (orders[i].Liquidity == liquidity) CancelOrder(orders[i].Id);
    }

    void QuoteHourlyMarket()
    {
        foreach (var stock in State.Stocks) stock.HourlyBidParticipants = stock.HourlyAskParticipants = 0;
        // Rotate both the first participant and stock assignment. Three assigned
        // names per institution produce 20 independently funded participants/name.
        for (int n = 0; n < AiCount; n++)
        {
            var trader = State.Bots[(n + (int)(State.CompletedHours % AiCount)) % AiCount];
            if (trader.WaitingForCapital || trader.ShortCollateral(State.Stocks) > trader.Cash) continue;
            CancelInstitutionOrders(trader.Id, true);
            int core = (trader.Id - 1) % StockCount;
            QuoteAssignedStock(trader, core);
            for (int slot = 0; slot < 2; slot++)
            {
                int assigned = (trader.Id + slot * 5 + (int)(State.CompletedHours % StockCount)) % StockCount;
                if (assigned == core) assigned = (assigned + 1) % StockCount;
                QuoteAssignedStock(trader, assigned);
            }
        }
    }

    void QuoteAssignedStock(Trader trader, int index)
    {
        var stock = State.Stocks[index];
        if (!stock.Active) return;
        long gross = Math.Max(1, trader.GrossAssets(State.Stocks));
        int layer = (trader.Id + index + (int)(State.CompletedHours % 5)) % 5;
        double fairBias = Math.Clamp(stock.FairValue / stock.Price - 1, -.15, .15) * (.08 + trader.Abilities.Valuation * .001);
        double center = stock.Price * (1 + fairBias + stock.Sentiment * .008);
        double spread = .002 + State.Government.Policy.FeeBasisPoints / 10000.0 + layer * .0015;
        int bid = LimitedQuote(index, center * (1 - spread), false);
        int ask = LimitedQuote(index, Math.Max(center * (1 + spread), stock.FairValue * (.91 + trader.Risk * .05)), true);
        // Liquidity quotes rest in the book instead of consuming each other's
        // inventory. Strategic orders and retail decide when to cross the spread.
        int tick = stock.Price < 1000 ? 1 : 10;
        var band = DailyPriceLimits.For(stock);
        if (asks[index].Count > 0) bid = Math.Min(bid, Math.Max(band.Lower, asks[index][0].Price - tick));
        if (bids[index].Count > 0) ask = Math.Max(ask, Math.Min(band.Upper, bids[index][0].Price + tick));
        bid = Math.Max(1, bid); ask = Math.Max(1, ask);
        long budget = (long)(gross * .002);
        long stockCapacity = Math.Max(0, (long)(gross * .18) - stock.Value(trader.Shares[index]));
        long free = Math.Max(0, AvailableCash(trader) - (long)(gross * Rules.Investment.MinimumCash));
        long pending = 0, sector = 0;
        for (int i = 0; i < StockCount; i++)
            if (State.Stocks[i].Sector == stock.Sector) sector += State.Stocks[i].Value(trader.Shares[i]);
        if (ownedOrders.TryGetValue(trader.Id, out var orders))
            foreach (var order in orders)
                if (order.Buy && !order.Cover)
                {
                    long value = (long)order.Price * order.Remaining;
                    if (order.StockIndex == index) pending += value;
                    if (State.Stocks[order.StockIndex].Sector == stock.Sector) sector += value;
                }
        budget = Math.Max(0, Math.Min(budget, Math.Min(free, Math.Min(stockCapacity - pending, (long)(gross * .38) - sector))));
        bool exiting = trader.Development!.Plans.Any(p => p.SecurityId == stock.SecurityId && p.TargetWeight == 0) ||
            trader.Development.LastExitMinutes[index] >= 0 && State.CompletedMinutes - trader.Development.LastExitMinutes[index] < 1440;
        int buyQuantity = exiting ? 0 : (int)Math.Min(10_000, budget / (bid + ExchangeFee(bid)));
        if (band.Contains(bid) && buyQuantity > 0 && SubmitInstitutionOrder(trader.Id, index, true, bid, buyQuantity, 2, liquidity: true) is null) stock.HourlyBidParticipants++;
        int sellQuantity = (int)Math.Min(10_000, (long)(gross * .002) / ask);
        sellQuantity = Math.Min(sellQuantity, ExposureSellCapacity(trader, index, ask));
        if (band.Contains(ask) && ask > bid && sellQuantity > 0 && SubmitInstitutionOrder(trader.Id, index, false, ask, sellQuantity, 2, liquidity: true) is null) stock.HourlyAskParticipants++;
        stock.LastCompetitionHour = State.CompletedHours;
    }
}
