namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    void CapitalizeMarket()
    {
        var stocks = State.Stocks; long target = MarketRules.MarketCapitalization;
        var shares = stocks.Select(s => target / stocks.Count / s.Price).ToArray();
        long remainder = target - stocks.Select((s, i) => (long)s.Price * shares[i]).Sum(); bool resolved = remainder == 0;
        for (int i = 0; i < stocks.Count && !resolved; i++) for (int j = i + 1; j < stocks.Count && !resolved; j++)
            {
                long a = stocks[i].Price, b = stocks[j].Price;
                static (long g, long x, long y) Egcd(long a, long b)
                { if (b == 0) return (a, 1, 0); var (g, x, y) = Egcd(b, a % b); return (g, y, x - a / b * y); }
                var (g, x, y) = Egcd(a, b); if (remainder % g != 0) continue;
                long dx = x * (remainder / g), dy = y * (remainder / g);
                long shift = (long)Math.Round(-dx / (double)(b / g)); dx += shift * (b / g); dy -= shift * (a / g);
                if (shares[i] + dx < 1 || shares[j] + dy < 1) continue;
                shares[i] += dx; shares[j] += dy; resolved = true;
            }
        if (!resolved) throw new InvalidDataException("초기 주식 수로 시가총액을 표현할 수 없습니다.");
        for (int i = 0; i < stocks.Count; i++)
        { State.Bank.ShareInventory[i] = shares[i]; stocks[i].TreasuryShares = shares[i]; stocks[i].TotalShares = checked(shares[i] * 2); }
        if (stocks.Sum(s => s.MarketCap) != target) throw new InvalidDataException("초기 시가총액 불일치");
    }
    void ConfigureNewCapital()
    {
        var w = MarketRules;
        State.Bank.Cash = w.BankCapital;
        State.Government.Cash = w.GovernmentCapital;
        State.RealEconomy.Cash = w.RealEconomyCapital;
    }

    void AllocateOpeningShares()
    {
        // Funded opening allocations supply the investor-only order book. Cash
        // moves to the underwriter; the bank never places autonomous quotes.
        foreach (var trader in Participants)
        {
            long budget = trader.Cash * (trader.IsRetail ? 9 : 45) / 100;
            int start = (trader.Id * 7) % StockCount;
            int positions = trader.IsRetail ? 3 : 6;
            for (int n = 0; n < positions; n++)
            {
                int i = (start + n * 2) % StockCount;
                var stock = State.Stocks[i];
                long quantity = Math.Min(State.Bank.ShareInventory[i], budget / positions / stock.Price);
                if (quantity == 0) continue;
                long amount = checked(quantity * stock.Price);
                TransferCash(trader, State.Bank, amount, "opening-subscription");
                trader.Shares[i] += quantity;
                trader.AverageCost[i] = stock.Price;
                trader.BuyCashFlow += amount;
                State.Bank.ShareInventory[i] -= quantity;
                RecordPositionOpen(trader, i, false, true);
            }
        }
    }
    void AllocateListingShares(int index)
    {
        var stock = State.Stocks[index];
        foreach (var trader in Participants)
        {
            long budget = AvailableCash(trader) / (trader.IsRetail ? 200 : 50);
            long quantity = Math.Min(State.Bank.ShareInventory[index] - State.Bank.ReservedLending[index], budget / stock.Price);
            if (quantity <= 0) continue;
            long amount = checked(quantity * stock.Price);
            TransferCash(trader, State.Bank, amount, "ipo-public-subscription");
            trader.AverageCost[index] = (trader.AverageCost[index] * trader.Shares[index] + amount) / (trader.Shares[index] + quantity);
            trader.Shares[index] += quantity;
            trader.BuyCashFlow += amount;
            State.Bank.ShareInventory[index] -= quantity;
            RecordPositionOpen(trader, index, false, true);
        }
    }

}
