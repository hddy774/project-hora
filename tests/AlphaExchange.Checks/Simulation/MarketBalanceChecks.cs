using AlphaExchange.Core;
using System.Text.Json.Nodes;

static class MarketBalanceChecks
{
    public static void Run(Action<bool, string> check, Action<GameEngine> validate)
    {
        PriceLimits(check, validate);
        ExposureAndCompetition(check, validate);
        InvestmentResults(check, validate);
        CompatibleSave(check, validate);
    }

    static void PriceLimits(Action<bool, string> check, Action<GameEngine> validate)
    {
        var game = TestFixtures.SmallMarket(1111);
        var stock = game.State.Stocks[0];
        var band = DailyPriceLimits.For(stock);
        check(band == new PriceBand(36680, 68120), "Daily price band is exactly 70–130% of the reference");
        foreach (int price in new[] { band.Lower - 1, band.Upper + 1 })
            check(game.SubmitOrder(0, 0, false, price, 1) is not null && game.SubmitOrder(1, 0, true, price, 1) is not null,
                "Both sides reject an out-of-band price before reserving funds");
        check(game.SubmitOrder(0, 0, false, band.Upper, 1) is null && game.SubmitOrder(1, 0, true, band.Upper, 1) is null && stock.Price == band.Upper,
            "A genuine trade may reach the upper limit");
        check(DailyPriceLimits.For(stock) == band, "Intraday trades never move the reference limit");
        check(game.SubmitOrder(0, 0, false, band.Lower, 1) is null && game.SubmitOrder(2, 0, true, band.Lower, 1) is null && stock.Price == band.Lower,
            "A genuine trade may reach the lower limit");
        check(game.SubmitOrder(101, 0, true, band.Lower, 1, 24) is null, "A funded quote may cross midnight");
        stock.Price = band.Upper;
        TestFixtures.AtHour(game, 23);
        game.AdvanceHour();
        check(stock.LimitReferencePrice == stock.MarkPrice && game.State.Orders.All(o => DailyPriceLimits.For(game.State.Stocks[o.StockIndex]).Contains(o.Price)),
            "Midnight resets the reference and removes obsolete out-of-band quotes");
        game.CancelAllOrders();
        decimal before = stock.LimitReferencePrice;
        check(game.SplitShares(stock.SecurityId, 2) is null && stock.LimitReferencePrice == before / 2,
            "Split adjusts the exact reference together with price and quantity");
        long dividend = game.PayDividend(stock.SecurityId, stock.OutstandingShares * 2);
        check(dividend > 0 && stock.LimitReferencePrice == before / 2 - 2, "Ex-dividend adjustment preserves the matching band");
        foreach (decimal reference in new[] { 1m, 999m, 1000m, 1428.57m, 9_999_999m })
        {
            var probe = new Stock { LimitReferencePrice = reference };
            var bounds = DailyPriceLimits.For(probe);
            check(bounds.Lower >= reference * .7m && bounds.Upper <= reference * 1.3m && bounds.Lower <= bounds.Upper,
                "Tick rounding stays inside both limits: " + reference);
        }
        validate(game);
    }

    static void ExposureAndCompetition(Action<bool, string> check, Action<GameEngine> validate)
    {
        var game = new GameEngine(1112);
        check(game.State.Bots.All(t => t.OpeningEquity == 10_000_000_000 && t.Equity(game.State.Stocks) == t.OpeningEquity && game.CashRatio(t) <= .5),
            "Every institution starts with 100억 of funded assets and at least 50% stock");
        var trader = game.Owner(1);
        trader.Disposition = Disposition.Cautious;
        double cautious = game.DesiredCashRatio(trader);
        trader.Disposition = Disposition.Aggressive;
        check(game.DesiredCashRatio(trader) < cautious, "Aggressive personality has a lower cash target");
        int capacity = game.ExposureSellCapacity(trader, 0, game.State.Stocks[0].Price);
        if (capacity < trader.Shares[0]) check(game.SubmitInstitutionOrder(1, 0, false, game.State.Stocks[0].Price, capacity + 1) is not null,
            "AI cannot submit sales exceeding the funded stock floor");
        int coveredHours = 0;
        for (int minute = 0; minute < 3 * 1440; minute++)
        {
            var references = game.State.Stocks.Select(s => s.LimitReferencePrice).ToArray();
            long previous = game.State.TotalMatches;
            game.AdvanceMinute();
            foreach (var trade in game.State.Tape.Take((int)Math.Min(game.State.TotalMatches - previous, game.State.Tape.Count)))
                check(trade.Price >= references[trade.StockIndex] * .7m && trade.Price <= references[trade.StockIndex] * 1.3m,
                    "Every live trade respects its day's band");
            if (game.State.Minute != 0) continue;
            check(game.State.Stocks.All(s => s.LastCompetitionHour == game.State.CompletedHours && (s.HourlyBidParticipants > 0 || s.Price == DailyPriceLimits.For(s).Lower) && (s.HourlyAskParticipants > 0 || s.Price == DailyPriceLimits.For(s).Upper)),
                "Hourly funded competition H" + game.State.CompletedHours + ": " + string.Join(",", game.State.Stocks.Where(s => s.HourlyBidParticipants == 0 || s.HourlyAskParticipants == 0).Select(s => $"{s.Symbol}:{s.HourlyBidParticipants}/{s.HourlyAskParticipants} p{s.Price} {DailyPriceLimits.For(s)}")));
            if (game.State.Stocks.Select((s, i) => game.Orders(i, true).Count > 0 && game.Orders(i, false).Count > 0).All(v => v)) coveredHours++;
            check(game.State.Bots.All(t => t.Development!.Plans.Count == 0 || t.Development.Plans.Sum(p => p.TargetWeight) >= .5 - 1e-8),
                "Pessimistic plans still allocate at least half the portfolio to stock");
        }
        check(coveredHours >= 60, "Distributed hourly competition sustains two-sided books in at least 60 of 72 samples");
        check(game.State.Stocks.All(s => s.CompanyNewsCount >= 3), "Every company publishes its own recurring news");
        foreach (var stock in game.State.Stocks) stock.Sentiment = 0;
        var prices = game.State.Stocks.Select(s => s.Price).ToArray();
        game.ApplyMarketEvent(MarketEventKind.Finance, 0, .05);
        check(game.State.Stocks[0].Sentiment > 0 && game.State.Stocks[10].Sentiment < 0 && prices.SequenceEqual(game.State.Stocks.Select(s => s.Price)),
            "Rate easing affects a borrower and a bank differently without writing prices");
        var stats = game.Statistics();
        check(stats.CashDistribution.Sum() == 100 && stats.CompanyBooks.Count == 15 && stats.CompanyBooks.Sum(s => s.Bids) == stats.BidQuantity,
            "Chart/table datasets reconcile with institutions and the live book");
        validate(game);
    }

    static void InvestmentResults(Action<bool, string> check, Action<GameEngine> validate)
    {
        var game = TestFixtures.SmallMarket(1113);
        var trader = game.Owner(1);
        var d = trader.Development!;
        TestFixtures.BuyFromBank(game, 1, 0, 2);
        int price = game.State.Stocks[0].Price + 1000;
        game.SubmitOrder(1, 0, false, price, 2);
        game.SubmitOrder(2, 0, true, price, 1);
        check(d.SuccessfulInvestments == 0 && d.CompletedInvestments == 0, "A partial sale never counts as another investment");
        game.SubmitOrder(3, 0, true, price, 1);
        check(d.SuccessfulInvestments == 1 && d.CompletedInvestments == 1, "Profitable full exit counts once after fees and tax");
        game.State.Government.Policy.FeeBasisPoints = 100;
        TestFixtures.BuyFromBank(game, 1, 0, 1);
        int tinyGain = game.State.Stocks[0].Price + 10;
        game.SubmitOrder(1, 0, false, tinyGain, 1);
        game.SubmitOrder(2, 0, true, tinyGain, 1);
        check(d.SuccessfulInvestments == 1 && d.CompletedInvestments == 2, "A gross gain smaller than costs is not a success");
        TestFixtures.BuyFromBank(game, 1, 0, 2);
        check(game.SplitShares(game.State.Stocks[0].SecurityId, 2) is null, "Outcome fixture splits real shares");
        int splitGain = game.State.Stocks[0].Price + 1000;
        game.SubmitOrder(1, 0, false, splitGain, 4);
        game.SubmitOrder(3, 0, true, splitGain, 4);
        check(d.SuccessfulInvestments == 2 && d.CompletedInvestments == 3, "An exact split preserves a single investment outcome");
        validate(game);
        var restored = GameEngine.Deserialize(game.Serialize());
        check(restored.Owner(1).Development!.SuccessfulInvestments == 2, "Investment counts survive a restart");
    }

    static void CompatibleSave(Action<bool, string> check, Action<GameEngine> validate)
    {
        var game = new GameEngine(1114);
        for (int i = 0; i < 77; i++) game.AdvanceMinute();
        var old = JsonNode.Parse(game.Serialize())!;
        old["Version"] = 9;
        old["Environment"]!["Rules"]!["Version"] = "market-1.10";
        foreach (var stock in old["Stocks"]!.AsArray())
            foreach (string field in new[] { "LimitReferencePrice", "NextCompanyNewsMinute", "CompanyNewsCount", "HourlyBidParticipants", "HourlyAskParticipants", "LastCompetitionHour" }) stock!.AsObject().Remove(field);
        foreach (var bot in old["Bots"]!.AsArray())
            foreach (string field in new[] { "InvestmentNet", "ShortInvestmentNet", "TrackedInvestments", "TrackedShortInvestments", "SuccessfulInvestments", "CompletedInvestments", "OutcomesSinceMinute", "TurnoverDay", "DayTurnover" }) bot!["Development"]!.AsObject().Remove(field);
        var migrated = GameEngine.Deserialize(old.ToJsonString());
        check(migrated.State.Version == 10 && migrated.State.RunId == game.State.RunId && migrated.State.CompletedMinutes == 77 &&
            migrated.State.Stocks.Select(s => s.Price).SequenceEqual(game.State.Stocks.Select(s => s.Price)) &&
            migrated.Participants.Select(t => t.Cash).SequenceEqual(game.Participants.Select(t => t.Cash)) &&
            migrated.Participants.SelectMany(t => t.Shares).SequenceEqual(game.Participants.SelectMany(t => t.Shares)),
            "v9 upgrades without changing identity, elapsed time, cash, price or holdings");
        check(migrated.State.Bots.All(t => t.Development!.OutcomesSinceMinute == 77 && !t.Development.TrackedInvestments.Any(x => x)),
            "Missing historical outcomes are explicitly untracked");
        validate(migrated);
        var copy = GameEngine.Deserialize(migrated.Serialize());
        for (int i = 0; i < 180; i++) { migrated.AdvanceMinute(); copy.AdvanceMinute(); }
        check(migrated.Serialize() == copy.Serialize(), "Migrated limit, news and liquidity schedules resume deterministically");
    }
}
