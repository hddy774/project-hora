namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    Trader BankAccount()
    {
        var t = State.Bank.MarketAccount;
        t.Id = 0; t.IsRetail = false; t.Cash = State.Bank.Cash; t.Shares = State.Bank.ShareInventory;
        int count = State.Stocks.Count;
        if (t.AverageCost.Length != count || t.ReservedShares.Length != count || t.ShortShares.Length != count || t.ReservedCovers.Length != count)
        {
            t.AverageCost = Resize(t.AverageCost, count); t.ReservedShares = Resize(t.ReservedShares, count);
            t.ShortShares = new long[count]; t.ShortAveragePrice = new double[count]; t.ReservedCovers = new long[count];
        }
        return t;
    }
}
