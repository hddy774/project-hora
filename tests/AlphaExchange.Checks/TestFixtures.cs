using AlphaExchange.Core;

static class TestFixtures
{
    // A smaller funded ledger isolates edge cases from the production balance.
    public static GameEngine SmallMarket(uint seed)
    {
        var rules = MarketRules.Default();
        rules.MarketCapitalization = CompanyCatalog.Companies.Sum(c => (long)c.InitialPrice * 1000);
        rules.InvestorCapital = 10_000_000; rules.RetailCapital = 100_000;
        rules.BankCapital = 1_000_000_000; rules.GovernmentCapital = 100_000_000; rules.RealEconomyCapital = 10_000_000_000;
        var game = new GameEngine(seed, marketRules: rules);
        // Undo only opening allocations so each trade case funds its own holdings.
        foreach (var trader in game.Participants)
        {
            long value = 0;
            for (int i = 0; i < GameEngine.StockCount; i++)
            {
                value += game.State.Stocks[i].Value(trader.Shares[i]);
                game.State.Bank.ShareInventory[i] += trader.Shares[i];
                trader.Shares[i] = 0; trader.AverageCost[i] = 0;
                if (trader.Development is { } d) d.PositionOpenedHours[i] = -1;
            }
            trader.Cash += value; game.State.Bank.Cash -= value; trader.BuyCashFlow -= value;
            if (!trader.IsRetail)
                trader.OpeningSnapshot = trader.SeasonSnapshot = new TraderSnapshot { Id = trader.Id, Generation = 1, Cash = trader.Cash, Equity = trader.Cash, ReturnIndex = 1 };
        }
        game.State.Government.Policy = new GovernmentPolicy();
        game.State.Government.History = [game.State.Government.Policy];
        game.State.CashFlows.Clear(); game.State.Journal.Clear();
        game.CancelAllOrders();
        game.State.OpeningSnapshot = game.State.SeasonSnapshot = game.CaptureSnapshot();
        game.State.DailyHistory = [game.State.OpeningSnapshot];
        return GameEngine.Deserialize(game.Serialize());
    }

    public static void AtHour(GameEngine game, long hour)
    {
        game.State.CompletedHours = hour; game.State.CompletedMinutes = hour * 60; game.State.MinuteActors.Clear();
    }

    public static void BuyFromBank(GameEngine game, int owner, int stock, int quantity)
    {
        int price = game.State.Stocks[stock].Price;
        if (game.SubmitOrder(0, stock, false, price, quantity) is not null || game.SubmitOrder(owner, stock, true, price, quantity) is not null)
            throw new Exception("Fixture must acquire bank inventory with real cash");
    }
}
