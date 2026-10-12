namespace AlphaExchange.Core;

public readonly record struct RetailMarketReaction(double BuyProbability, double Aggression);

public sealed partial class GameEngine
{
    // Retail reads only public tape, market breadth and news. No balance-sheet,
    // fair-value, institution plan or personal profit-taking signal is available.
    internal double RetailPressure(int index)
    {
        var stock = State.Stocks[index];
        int n = stock.History.Count;
        double fast = stock.Price / stock.History[Math.Max(0, n - 3)] - 1;
        double daily = stock.Price / stock.History[Math.Max(0, n - 24)] - 1;
        double market = marketTrend;
        return Math.Clamp(fast * 8 + daily * 3 + market * 2 + stock.Sentiment * 5, -1, 1);
    }

    public RetailMarketReaction RetailReaction(Trader trader, int index)
    {
        double pressure = RetailPressure(index);
        double probability = .5 + pressure * (MarketRules.RetailHerdWeight + trader.Risk * .08);
        double aggression = .006 + trader.Risk * .012 + Math.Abs(pressure) * .022;
        return new(Math.Clamp(probability, .05, .95), aggression);
    }

    internal int RetailLimitPrice(int index, bool buy, double aggression)
    {
        var stock = State.Stocks[index];
        int cap = Quote(stock.Price * (1 + (buy ? aggression : -aggression)), buy);
        var book = buy ? asks[index] : bids[index];
        if (book.Count > 0 && Next() < MarketRules.RetailTakeLiquidityProbability) return book[0].Price;
        return cap;
    }

    void DecideRetail(Trader trader)
    {
        CancelOrders(trader.Id);
        if (trader.WaitingForCapital || Next() < trader.Patience * .08) return;
        int index = (int)(Next() * StockCount);
        int other = (int)(Next() * StockCount);
        var a = State.Stocks[index]; var b = State.Stocks[other];
        double attentionA = Math.Abs(a.HourlyChange) + Math.Abs(a.Sentiment);
        double attentionB = Math.Abs(b.HourlyChange) + Math.Abs(b.Sentiment);
        if (attentionB > attentionA && Next() < .75) index = other;
        // A bearish holder checks an existing position so panic can supply stock.
        if (Next() < .45)
        {
            int first = (int)(Next() * StockCount);
            for (int j = 0; j < StockCount; j++)
            {
                int i = (first + j) % StockCount;
                if (trader.Shares[i] > 0 && RetailPressure(i) < 0) { index = i; break; }
            }
        }
        var stock = State.Stocks[index];
        if (!stock.Active) return;
        var reaction = RetailReaction(trader, index);
        bool buy = Next() < reaction.BuyProbability;
        int price = RetailLimitPrice(index, buy, reaction.Aggression);
        long available = buy ? MaxBuy(trader, index, price) : trader.Shares[index] - trader.ReservedShares[index];
        int lot = Math.Clamp((int)Math.Min(1000, trader.OpeningEquity / price / MarketRules.RetailTradeDivisor), 1, 1000);
        int quantity = (int)Math.Min(available, lot * (1 + (Next() < trader.Risk * .5 ? 1 : 0)));
        if (quantity > 0) SubmitOrder(trader.Id, index, buy, price, quantity, 1);
    }

    public string EmptyBookReason(int index, bool buy)
        => !State.Stocks[index].Active ? "거래 중단" : buy ? "현재 매수 대기 주문 없음" : "현재 매도 대기 주문 없음";
}
