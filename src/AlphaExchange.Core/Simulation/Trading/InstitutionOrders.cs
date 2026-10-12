namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    bool ExecuteRiskExit(Trader trader, int index, bool margin)
    {
        var stock = State.Stocks[index]; var signal = institutionSignals[index];
        if (!stock.Active) return false;
        if (trader.ShortShares[index] > 0 && (margin || !State.Government.Policy.ShortSellingAllowed || signal.Score > 0 ||
            Math.Abs(stock.Price / Math.Max(1, trader.ShortAveragePrice[index]) - 1) >= Rules.Investment.ShortExitReturn))
        {
            // Liability exits must be allowed above a valuation-based entry cap.
            int price = asks[index].Count > 0 ? asks[index][0].Price : Quote(stock.Price * 1.035, true);
            int quantity = (int)Math.Min(trader.ShortShares[index] - trader.ReservedCovers[index], 1_000_000);
            while (quantity > 0)
            {
                if (SubmitOrder(trader.Id, index, true, price, quantity, 1, cover: true) is null) return true;
                quantity /= 2;
            }
        }
        if (margin && trader.Shares[index] > trader.ReservedShares[index])
        {
            int price = CompetitivePrice(trader, index, false, true);
            int quantity = (int)Math.Min(trader.Shares[index] - trader.ReservedShares[index], 1_000_000);
            return quantity > 0 && SubmitOrder(trader.Id, index, false, price, quantity, 1) is null;
        }
        return false;
    }

    bool PlacePortfolioOrder(Trader trader, int index, bool margin)
    {
        var stock = State.Stocks[index]; var plan = decisionPlans[index]!; var d = trader.Development!;
        long gross = Math.Max(1, trader.GrossAssets(State.Stocks));
        double weight = (double)stock.Value(trader.Shares[index]) / gross;
        bool buy = plan.TargetWeight > weight;
        if (margin && buy) return false;
        bool campaign = index == d.CampaignIndex;
        bool aggressive = Math.Abs(plan.TargetWeight - weight) > .035 || Math.Abs(institutionSignals[index].Score) > Rules.Investment.TakeLiquidityScore ||
            campaign && d.CampaignPhase is CampaignPhase.AttractFollowers or CampaignPhase.Distribute or CampaignPhase.Retreat;
        int price = CompetitivePrice(trader, index, buy, aggressive);
        long desiredValue = (long)(gross * Math.Min(Math.Abs(plan.TargetWeight - weight), Rules.Investment.OrderRatio + trader.Abilities.Execution / 100.0 * Rules.Investment.OrderSkillRatio));
        int quantity = (int)Math.Min(1_000_000, desiredValue / price);
        if (buy) quantity = Math.Min(quantity, PlannedBuyCapacity(trader, index, price));
        else quantity = (int)Math.Min(quantity, trader.Shares[index] - trader.ReservedShares[index]);
        if (quantity > 0)
        {
            if (SubmitOrder(trader.Id, index, buy, price, quantity, 1) is null)
            {
                if (aggressive) d.AggressiveOrders++;
                return true;
            }
        }
        if (!buy && trader.Shares[index] == 0 && institutionSignals[index].Score < Rules.Investment.ShortScore && RetailPressure(index) < -.2)
            return PlaceHedge(trader, index, gross);
        return false;
    }

    int PlannedBuyCapacity(Trader trader, int index, int price)
    {
        var d = trader.Development!; var plan = decisionPlans[index]!;
        long gross = Math.Max(1, trader.GrossAssets(State.Stocks));
        long sector = 0, pendingStock = 0, pendingSector = 0;
        for (int i = 0; i < StockCount; i++)
            if (signalSectors[i] == signalSectors[index]) sector += State.Stocks[i].Value(trader.Shares[i]);
        if (ownedOrders.TryGetValue(trader.Id, out var orders))
            foreach (var order in orders)
            {
                if (!order.Buy || order.Cover) continue;
                long amount = (long)order.Price * order.Remaining;
                if (order.StockIndex == index) pendingStock += amount;
                if (signalSectors[order.StockIndex] == signalSectors[index]) pendingSector += amount;
            }
        double stockBudget = gross * Math.Min(plan.TargetWeight, plan.MaximumWeight) - State.Stocks[index].Value(trader.Shares[index]) - pendingStock;
        double sectorBudget = gross * institutionSignals[index].SectorLimit - sector - pendingSector;
        long free = Math.Max(0, AvailableCash(trader) - (long)Math.Ceiling(gross * d.TargetCashRatio));
        long budget = (long)Math.Max(0, Math.Min(free, Math.Min(stockBudget, sectorBudget)));
        return (int)Math.Min(1_000_000, budget / (price + ExchangeFee(price)));
    }

    int CompetitivePrice(Trader trader, int index, bool buy, bool aggressive)
    {
        var stock = State.Stocks[index]; var plan = decisionPlans[index]!;
        int tick = stock.Price < 1000 ? 1 : 10;
        double limitMove = aggressive ? .018 + trader.Risk * .018 : .003;
        int limit = Quote(stock.Price * (1 + (buy ? limitMove : -limitMove)), buy);
        var opposite = buy ? asks[index] : bids[index];
        if (aggressive && opposite.Count > 0)
        {
            int touch = opposite[0].Price;
            // Sweep at most a narrow funded band; a stale far-away quote is not
            // accepted merely because it is currently the best price.
            if (buy ? touch <= limit : touch >= limit)
                limit = buy ? Math.Min(limit, touch + tick * 2) : Math.Max(limit, touch - tick * 2);
        }
        else
        {
            var same = buy ? bids[index] : asks[index];
            int improved = same.Count > 0 ? same[0].Price + (buy ? tick : -tick) :
                Quote(stock.Price * (1 + (buy ? -.001 : .001)), buy);
            limit = buy ? Math.Min(limit, improved) : Math.Max(limit, improved);
        }
        if (buy) limit = Math.Min(limit, Quote(stock.FairValue * 1.18, true));
        return Math.Clamp(buy ? Math.Min(limit, plan.UpperPrice) : Math.Max(limit, plan.LowerPrice), 1, 10_000_000);
    }

    bool PlaceHedge(Trader trader, int index, long gross)
    {
        if (!State.Government.Policy.ShortSellingAllowed || trader.ShortShares[index] > 0) return false;
        int price = CompetitivePrice(trader, index, false, true);
        long equity = Math.Max(0, trader.Equity(State.Stocks));
        long budget = (long)Math.Max(0, equity * Rules.Investment.MaximumShortRatio - trader.ShortLiability(State.Stocks));
        int quantity = (int)Math.Min(1000, Math.Min(budget / price, Math.Max(0, AvailableCash(trader) - gross * Rules.Investment.MinimumCash) / price));
        return quantity > 0 && SubmitOrder(trader.Id, index, false, price, quantity, 1, shortSale: true) is null;
    }

    void PlaceLiquidityOrders(Trader trader, int index, bool margin)
    {
        var stock = State.Stocks[index];
        if (!stock.Active || margin) return;
        int buyPrice = CompetitivePrice(trader, index, true, false);
        int buyQuantity = Math.Min(100, PlannedBuyCapacity(trader, index, buyPrice));
        if (buyQuantity > 0) SubmitOrder(trader.Id, index, true, buyPrice, buyQuantity, 1);
        int sellPrice = Math.Max(buyPrice + (stock.Price < 1000 ? 1 : 10), CompetitivePrice(trader, index, false, false));
        int sellQuantity = (int)Math.Min(100, trader.Shares[index] - trader.ReservedShares[index]);
        if (sellQuantity > 0 && sellPrice <= 10_000_000) SubmitOrder(trader.Id, index, false, sellPrice, sellQuantity, 1);
    }
}
