namespace AlphaExchange.Core;

public sealed record CompanyLiquidity(string Symbol, long Bids, long Asks, int BidInstitutions, int AskInstitutions,
    int HourlyBidders, int HourlySellers, int LowerLimit, int UpperLimit, long CompanyNews);

public sealed partial class GameEngine
{
    void AddParticipationStatistics(MarketStatistics result)
    {
        foreach (var trader in State.Bots)
        {
            var d = trader.Development!;
            result.SuccessfulInvestments += d.SuccessfulInvestments;
            result.CompletedInvestments += d.CompletedInvestments;
            if (StockAllocationShortfall(trader) > 0) result.AllocationRecoveryCount++;
            double cash = CashRatio(trader);
            result.CashDistribution[Math.Min(4, (int)(cash * 10))]++;
        }
        Span<bool> buyers = stackalloc bool[AiCount];
        Span<bool> sellers = stackalloc bool[AiCount];
        for (int i = 0; i < StockCount; i++)
        {
            var stock = State.Stocks[i];
            long buys = 0, sells = 0;
            buyers.Clear(); sellers.Clear();
            foreach (var order in bids[i]) { buys += order.Remaining; if (order.OwnerId is >= 1 and <= AiCount) buyers[order.OwnerId - 1] = true; }
            foreach (var order in asks[i]) { sells += order.Remaining; if (order.OwnerId is >= 1 and <= AiCount) sellers[order.OwnerId - 1] = true; }
            int buyerCount = 0, sellerCount = 0;
            for (int j = 0; j < AiCount; j++) { if (buyers[j]) buyerCount++; if (sellers[j]) sellerCount++; }
            var band = DailyPriceLimits.For(stock);
            result.CompanyBooks.Add(new(stock.Symbol, buys, sells, buyerCount, sellerCount,
                stock.HourlyBidParticipants, stock.HourlyAskParticipants, band.Lower, band.Upper, stock.CompanyNewsCount));
            if (buys > 0 && sells > 0)
            {
                result.TwoSidedCompanies++;
                result.AverageSpread += (asks[i][0].Price - bids[i][0].Price) / (double)stock.Price;
            }
        }
        result.AverageSpread /= Math.Max(1, result.TwoSidedCompanies);
    }
}
