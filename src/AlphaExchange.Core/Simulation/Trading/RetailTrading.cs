namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    void DecideRetail(Trader t)
    {
        CancelOrders(t.Id);
        if (t.WaitingForCapital) return;
        if (Next() < t.Patience * .15) return;
        int index = (int)(Next() * State.Stocks.Count);
        // Follow whichever of two randomly observed quotes has attracted volume.
        int other = (int)(Next() * State.Stocks.Count);
        if (State.Stocks[other].DayVolume > State.Stocks[index].DayVolume && Next() < .65) index = other;
        if (t.Shares[index] == 0 && Next() < .35)
            for (int i = 0; i < t.Shares.Length; i++) if (t.Shares[i] > 0) { index = i; break; }
        var stock = State.Stocks[index]; if (!stock.Active) return;
        var reaction = RetailReaction(t, index);
        bool buy = Next() < reaction.BuyProbability;
        int limit = RetailLimitPrice(index, buy, reaction.Aggression);
        long available = buy ? MaxBuy(t, index, limit) : t.Shares[index] - t.ReservedShares[index];
        int lot = State.World is not null && t.OpeningEquity >= WorldRules.RetailCapital / 2 ? Math.Clamp((int)(t.OpeningEquity / limit / WorldRules.RetailTradeDivisor), 1, 1000) : 1;
        int quantity = (int)Math.Min(available, lot * (1 + (Next() < t.Risk * .25 ? 1 : 0)));
        if (quantity > 0) SubmitOrder(t.Id, index, buy, limit, quantity, 1);
    }
}
