using AlphaExchange.Core;
using System.Text.Json.Nodes;

static class EconomyChecks
{
    public static void Run(Action<bool, string> check, Action<GameEngine> validate, Action<double, double, string, double> near)
    {
        var g = new GameEngine(712);
        check(g.State.Stocks.Count == 30 && g.State.Bots.All(t => t.Abilities.Values().Length == 10 && t.Abilities.Values().All(v => v is >= 1 and <= 100)), "Thirty stocks and ten representative abilities");
        check(g.AdvanceTime(2.4, 50) == 24 && g.AdvanceTime(1.2, 100) == 24, "50x and 100x timing");
        var t = g.Owner(1);
        for (int i = 0; i < 9; i++)
        {
            t.CreditScore = 95 - i * 10; var offer = g.LoanTerms(t);
            check((int)offer.Rating == i && offer.AssetRatio == GameEngine.CreditLimits[i], "Nine credit grades and asset limits");
            if (i > 0) check(offer.AnnualRate > g.State.Government.Policy.BaseRate + GameEngine.CreditSpreads[i - 1], "Lower credit pays more interest");
        }
        g.State.Government.Policy.LoanLimitMultiplier = .8;
        check(g.LoanTerms(t).AssetRatio == .3, "C grade keeps the required 30 percent minimum under regulation");
        g.State.Government.Policy.LoanLimitMultiplier = 1;
        t.CreditScore = 95; long own = t.Equity(g.State.Stocks); long cash = t.Cash;
        check(g.BorrowCash(t.Id, own) is null && t.LoanDebt == own && t.Cash == cash + own, "AAA may borrow 100 percent of own assets");
        check(g.LoanTerms(t).Limit == own && g.BorrowCash(t.Id, 1) is not null, "Borrowing cannot recursively expand its limit");
        check(g.RepayLoan(t.Id, own) == own && t.LoanDebt == 0, "Bank repayment"); validate(g);
        var shortGame = new GameEngine(77); var shortTrader = shortGame.Owner(1); int stock = 0;
        long inventory = shortGame.State.Bank.ShareInventory[stock]; long sellerLong = shortTrader.Shares[stock];
        check(shortGame.SubmitOrder(101, stock, false, 52400, 1, shortSale: true) is not null, "Retail cannot short");
        check(shortGame.SubmitOrder(1, stock, false, 52500, 2, shortSale: true) is null, "Borrowed short order");
        check(shortGame.State.Bank.ReservedLending[stock] == 2 && shortTrader.ReservedCash > 0, "Short share/cash reservation");
        check(shortGame.SubmitOrder(2, stock, true, 52600, 1) is null, "Partial short fill");
        check(shortTrader.ShortShares[stock] == 1 && shortTrader.Shares[stock] == sellerLong && shortGame.State.Bank.ShareInventory[stock] == inventory - 1, "Actual borrowed shares settle at the resting price");
        shortGame.CancelOrders(1); check(shortGame.State.Bank.ReservedLending[stock] == 0, "Short cancellation releases lending inventory");
        shortGame.SubmitOrder(3, stock, false, 51000, 1);
        check(shortGame.SubmitOrder(1, stock, true, 51100, 1, cover: true) is null, "Cover trade");
        check(shortTrader.ShortShares[stock] == 0 && shortGame.State.Bank.ShareInventory[stock] == inventory && shortTrader.RealizedProfit == 1500, "Cover returns borrowed shares and realizes profit");
        check(shortTrader.Taxes > 0, "Short profit is taxed"); validate(shortGame);
        check(shortGame.SubmitOrder(1, 0, false, 100, 500, shortSale: true) is not null, "Low limit price cannot bypass marked short risk cap");
        shortGame.SubmitOrder(1, 0, false, 51000, 2, shortSale: true); shortGame.SubmitOrder(2, 0, true, 51500, 2);
        var coverSeller = shortGame.State.Bots.First(b => b.Id != 1 && b.Shares[0] - b.ReservedShares[0] >= 2);
        check(shortGame.SubmitOrder(coverSeller.Id, 0, false, 52500, 2) is null, "Funded cover counterparty");
        check(shortGame.SubmitOrder(1, 0, true, 52600, 2, cover: true) is null && shortTrader.RealizedProfit == -1500, "Losing short realizes the full loss"); validate(shortGame);
        var lendingSave = new GameEngine(121); lendingSave.SubmitOrder(1, 0, false, 55000, 2, shortSale: true);
        var lendingRestore = GameEngine.Deserialize(lendingSave.Serialize());
        check(lendingRestore.Serialize() == lendingSave.Serialize() && lendingRestore.State.Bank.ReservedLending[0] == 2, "Short reservations survive persistence");
        lendingRestore.CancelOrders(1); validate(lendingRestore);
        var interestGame = new GameEngine(55); var debtor = interestGame.Owner(101); interestGame.BorrowCash(101, 20_000);
        for (int i = 0; i < 24; i++) interestGame.AdvanceHour();
        check(debtor.InterestExpense > 0 && interestGame.State.Bank.InterestIncome > 0, "Interest settles daily into bank ledger"); validate(interestGame);
        shortGame.State.Government.Policy.ShortSellingAllowed = false;
        check(shortGame.SubmitOrder(1, 0, false, 52400, 1, shortSale: true) is not null, "Government short ban");
        var operationGame = new GameEngine(10);
        check(operationGame.ProposeOperation(1, 2, 0) is null && operationGame.ProposeOperation(1, 3, 1) is not null, "Representative cooperation and exclusive participation");
        operationGame.State.Government.Policy.Enforcement = 1;
        var op = operationGame.State.Operations[0]; op.EndsHour = 1; operationGame.AdvanceHour();
        check(op.Status == OperationStatus.Failed && op.Fine > 0 && operationGame.Owner(1).Fines > 0, "Detection produces fines and visible failure"); validate(operationGame);
        var penalized = operationGame.Owner(3); operationGame.AssessFine(penalized, penalized.Cash + 1000);
        check(penalized.FineDebt == 1000 && penalized.Cash == 0, "Unpaid fines remain a liability without negative cash"); validate(operationGame);
        var loss = new GameEngine(4); var a = loss.Owner(1); a.Abilities.RiskManagement = 100;
        foreach (var s in loss.State.Stocks) s.History = Enumerable.Range(0, 24).Select(i => (double)(s.Price * (1.3 - i * .3 / 23))).ToList();
        loss.AdvanceHour();
        check(a.Decision.Contains("하락") && loss.Analyze(a, 0).TargetExposure < .3, "Bear market reduces exposure and strengthens risk control");
        var strong = new Trader { Abilities = new Abilities { Valuation = 50, RiskManagement = 100, Macro = 60 }, SeasonOpeningEquity = 1, Cash = 1_000_000, Risk = .5 };
        var weak = new Trader { Abilities = new Abilities { Valuation = 50, RiskManagement = 20, Macro = 60 }, SeasonOpeningEquity = 1, Cash = 1_000_000, Risk = .5 };
        check(loss.Analyze(strong, 0).TargetExposure < loss.Analyze(weak, 0).TargetExposure, "Risk ability changes defensive position sizing");
        strong.Abilities.Valuation = 100; weak.Abilities.Valuation = 20; loss.State.Stocks[0].FairValue *= 1.4;
        check(loss.Analyze(strong, 0).Score > loss.Analyze(weak, 0).Score, "Valuation ability changes stock conviction");
        for (int i = 0; i < 240; i++) loss.AdvanceHour();
        check(loss.State.Bots.All(b => b.Equity(loss.State.Stocks) > 0) && loss.State.Bots.Sum(b => b.Cash) > 0, "Ten-day bear simulation preserves institutional solvency and liquidity"); validate(loss);
        Console.WriteLine($"Bear scenario: solvent institutions={loss.State.Bots.Count(b => b.Equity(loss.State.Stocks) > 0)}, cash={loss.State.Bots.Sum(b => b.Cash):N0}");
        var recovery = new GameEngine(301);
        foreach (var s in recovery.State.Stocks) { s.Price = s.PreviousPrice = s.DayOpenPrice = 100; s.History = Enumerable.Repeat(100.0, 24).ToList(); }
        for (int i = 0; i < 120; i++) recovery.AdvanceHour();
        check(recovery.State.Stocks.Any(s => s.Price > 100), "Value demand can recover from the price floor"); validate(recovery);
        var company = new GameEngine(85); var previous = company.State.Stocks[0].Report;
        company.ApplyNewsImpact(0, 1); company.ApplyNewsImpact(1, -1);
        for (int i = 0; i < 720; i++) company.AdvanceHour();
        foreach (var s in company.State.Stocks)
        {
            var r = s.Report;
            check(r.Season == 1 && r.NewsCount >= 0 && s.SeasonNewsCount <= 1, "Monthly report uses accumulated news then resets");
            check(r.OpeningCash + r.OperatingCashFlow + r.InvestingCashFlow + r.FinancingCashFlow == r.Cash, "Corporate cash flow reconciliation");
            check(r.OpeningEquity + r.NetIncome - r.Dividends + r.CapitalChange == r.Equity, "Corporate statement of equity reconciliation");
            check(r.Assets == r.Debt + r.Equity, "Corporate balance sheet");
        }
        check(company.State.Government.Policy.Season == 2 && company.State.Government.History.Count == 2, "Monthly government policy rollover");
        var ten = company.Period(ComparisonPeriod.TenDays); var five = company.Period(ComparisonPeriod.FiveDays); var day = company.Period(ComparisonPeriod.PreviousDay);
        check(ten.Start.Hour == 480 && five.Start.Hour == 600 && day.Start.Hour == 696, "Comparison periods cross season boundaries");
        check(ten.Volume == ten.End.Volume - ten.Start.Volume && ten.Volume >= five.Volume, "Period volume uses cumulative differences");
        foreach (var metric in Enum.GetValues<RankingMetric>())
        {
            var ranks = company.Ranking(metric, ComparisonPeriod.TenDays);
            check(ranks.Count == 100 && ranks.Zip(ranks.Skip(1)).All(pair => company.RankingValue(pair.First, metric, ComparisonPeriod.TenDays, ten.Start) >= company.RankingValue(pair.Second, metric, ComparisonPeriod.TenDays, ten.Start)), "Metric ranking is complete and ordered");
        }
        check(company.State.PendingSeasons[0].Days.Count == 30 && company.State.PendingSeasons[0].CompanyReports.All(r => r.Season == 1), "Season archive includes graphs and closing reports");
        var old = MakeV3Fixture(); var migrated = GameEngine.Deserialize(old.ToJsonString());
        check(migrated.State.Version == 5 && migrated.State.MigratedFromV3 && migrated.State.Stocks.Count == 30, "v3 upgrades into thirty-stock economy");
        check(migrated.State.Bots.All(t => t.Cash == (long)old["Bots"]![t.Id - 1]!["Cash"]! && t.Shares.Take(8).SequenceEqual(old["Bots"]![t.Id - 1]!["Shares"]!.AsArray().Select(x => (long)x!))), "v3 cash and holdings preserved");
        validate(migrated);
        var corruptHistory = JsonNode.Parse(company.Serialize())!.AsObject(); corruptHistory["DailyHistory"] = new JsonArray();
        try { GameEngine.Deserialize(corruptHistory.ToJsonString()); check(false, "Empty history must be rejected before UI indexing"); }
        catch (InvalidDataException) { check(true, "Empty history rejected"); }
        Console.WriteLine("PASS economy: abilities, 50/100x, nine grades, loans, shorts, taxes, operations, bear risk, monthly reports/policies, comparisons and v3 migration");
    }
    static JsonObject MakeV3Fixture()
    {
        var g = new GameEngine(111);
        foreach (var t in g.Participants)
        {
            for (int i = 8; i < g.State.Stocks.Count; i++) t.Cash += (long)t.Shares[i] * g.State.Stocks[i].Price;
            t.Shares = t.Shares.Take(8).ToArray(); t.AverageCost = t.AverageCost.Take(8).ToArray(); t.ReservedShares = t.ReservedShares.Take(8).ToArray();
            t.ShortShares = new long[8]; t.ShortAveragePrice = new double[8]; t.ReservedCovers = new long[8]; t.OpeningCash = t.Cash;
        }
        g.State.Stocks.RemoveRange(8, g.State.Stocks.Count - 8); g.State.News.RemoveAll(n => n.StockIndex >= 8);
        for (int i = 0; i < 8; i++) g.State.Stocks[i].TotalShares = g.Participants.Sum(t => (long)t.Shares[i]);
        g.State.InitialSystemCash = g.Participants.Sum(t => t.Cash) + g.State.FeePool;
        var json = JsonNode.Parse(g.Serialize())!.AsObject(); json["Version"] = 3;
        foreach (var participant in json["Bots"]!.AsArray().Concat(json["Retail"]!.AsArray()))
            foreach (string key in new[] { "ShortShares", "ShortAveragePrice", "ReservedCovers", "Abilities", "Disposition", "CreditScore", "LoanDebt", "FineDebt", "OpeningSnapshot", "SeasonSnapshot" })
                participant!.AsObject().Remove(key);
        return json;
    }
}
