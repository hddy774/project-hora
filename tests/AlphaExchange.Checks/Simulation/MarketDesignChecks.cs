using AlphaExchange.Core;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO.Compression;

static class MarketDesignChecks
{
    public static void Run(Action<bool, string> check, Action<GameEngine> validate)
    {
        var game = new GameEngine(110);
        check(game.State.Version == 10 && game.State.Stocks.Count == 15 && game.State.Stocks.GroupBy(s => s.Sector).All(g => g.Count() == 3) &&
            game.State.Stocks.Select(s => s.Sector).Distinct().Count() == 5, "Exactly fifteen securities across five balanced sectors");
        check(game.State.Retail.Count == 10000 && game.State.Bots.Count == 100, "Only investor institutions and retail participate");
        check(typeof(GameState).GetProperty("World") is null && typeof(GameState).GetProperty("CompanyVotes") is null &&
            typeof(GameState).Assembly.GetType("AlphaExchange.Core.Person") is null, "Non-investor people and voting models are removed from the runtime");
        check(game.State.Stocks.All(s => s.Price == s.StartPrice) && game.State.TotalMatches == 0 &&
            game.State.Retail.All(t => t.Shares.Any(q => q > 0)), "Funded opening allocation supplies stock without synthetic market prints");
        validate(game);
        TestPublicSignals(game, check);
        TestPlansAndMarket(check, validate);
        TestFinancialAnalysis(check);
        TestSnapshots(check);
        TestReplacements(check, validate);
        check(KoreanNumber.Compact(10_000) == "1만" && KoreanNumber.Compact(100_000_000) == "1억" &&
            KoreanNumber.Compact(1_000_000_000_000) == "1조" && KoreanNumber.Compact(-100_000_000) == "-1억", "Signed Korean money units");
        check(KoreanNumber.Dual(long.MinValue).Contains(KoreanNumber.Full(long.MinValue)), "Exact amount remains available at long limits");
        Console.WriteLine("PASS investor market: removed roles, fixed catalog, public news/tape response, budgeted plans, aggressive matching, accounting and isolated checkpoints");
    }

    static void TestPublicSignals(GameEngine game, Action<bool, string> check)
    {
        var retail = game.Owner(101); var stock = game.State.Stocks[0];
        foreach (var item in game.State.Stocks) item.Sentiment = 0;
        double neutral = game.RetailReaction(retail, 0).BuyProbability;
        int price = stock.Price;
        game.ApplyMarketEvent(MarketEventKind.Company, 0, .05);
        double good = game.RetailReaction(retail, 0).BuyProbability;
        game.ApplyMarketEvent(MarketEventKind.Company, 0, -.1);
        double bad = game.RetailReaction(retail, 0).BuyProbability;
        check(good > neutral && bad < neutral && stock.Price == price && game.State.TotalMatches == 0,
            "Positive and negative news move retail demand but cannot write a stock price");
        var reaction = game.RetailReaction(retail, 0);
        stock.FairValue *= 10; stock.Report.Revenue *= 4; retail.Abilities.Valuation = 100; retail.AverageCost[0] = 1;
        check(game.RetailReaction(retail, 0) == reaction, "Retail ignores valuation, financial statements, ability and personal profit-taking");
        stock.Sentiment = 0; stock.History = [price * 1.15, price * 1.05, (double)price];
        check(game.RetailReaction(retail, 0).BuyProbability < neutral, "Retail follows falling public price history");
        stock.History = [price * .85, price * .95, (double)price];
        check(game.RetailReaction(retail, 0).BuyProbability > neutral, "Retail follows rising public price history");
        foreach (var kind in Enum.GetValues<MarketEventKind>())
        {
            var prices = game.State.Stocks.Select(s => s.Price).ToArray();
            long cash = game.SystemCash();
            var oldPolicy = game.State.Government.Policy;
            double oldRate = oldPolicy.BaseRate;
            game.ApplyMarketEvent(kind, 2, .04);
            check(prices.SequenceEqual(game.State.Stocks.Select(s => s.Price)) && game.SystemCash() == cash,
                "Random environment event changes funded rules, never prices or system cash: " + kind);
            if (kind == MarketEventKind.Finance) check(oldPolicy.BaseRate == oldRate && oldPolicy != game.State.Government.Policy,
                "Finance events preserve previously published policy history");
        }
    }

    static void TestPlansAndMarket(Action<bool, string> check, Action<GameEngine> validate)
    {
        var game = new GameEngine(1110);
        var stages = new HashSet<CampaignPhase>();
        for (int minute = 0; minute < 3 * 1440; minute++)
        {
            game.AdvanceMinute();
            if (minute % 60 != 59) continue;
            foreach (var trader in game.State.Bots)
            {
                var d = trader.Development!;
                stages.Add(d.CampaignPhase);
                check(d.Plans.Count == 15 && d.Plans.Sum(p => p.TargetWeight) + d.TargetCashRatio <= 1.00000001,
                    "All security targets fit one cash budget");
                check(d.Plans.All(p => p.TargetWeight >= 0 && p.TargetWeight <= p.MaximumWeight + 1e-9 && p.LossBudget >= 0 &&
                    p.StopPrice > 0 && p.TakeProfitPrice >= p.StopPrice && p.ReviewMinute >= p.CreatedMinute), "Bounded stock targets and explicit exits");
                foreach (var sector in game.State.Stocks.GroupBy(s => s.Sector))
                {
                    var ids = sector.Select(s => s.SecurityId).ToHashSet();
                    double cap = game.Rules.Investment.SectorLimit - trader.Abilities.Diversification / 100.0 * game.Rules.Investment.DiversificationSectorEffect;
                    check(d.Plans.Where(p => ids.Contains(p.SecurityId)).Sum(p => p.TargetWeight) <= cap + 1e-9, "Sector targets cannot overcommit shared exposure");
                }
            }
            if (minute == 119) check(game.State.Bots.All(t => t.Development!.PortfolioReviews >= 4), "Every institution receives at least twice the old hourly planning opportunities");
        }
        var state = game.State;
        check(stages.Count == 4, "Investor campaigns actually progress through accumulation, attraction, distribution and retreat");
        check(state.TotalMatches > 1000 && state.Activity.AggressiveOrders > 0 && state.Orders.All(o => o.OwnerId > 0) &&
            state.Tape.All(t => t.BuyerId > 0 && t.SellerId > 0 && t.BuyerId != t.SellerId), "Competitive funded investor orders trade without a bank AI or self-trades");
        check(state.Activity.InstitutionMatches + state.Activity.RetailMatches + state.Activity.CrossGroupMatches == state.TotalMatches,
            "Counterparty statistics account for every real trade once");
        check(state.Activity.CashToInstitutions + state.Activity.CashToRetail == state.Activity.CrossGroupTurnover, "Intergroup cash statistics reconcile with actual turnover");
        check(game.ConfidentialPlans(2, 1).Count == 0 && game.ConfidentialReports(2, 1).Count == 0, "Another investor cannot read the owner's private plan through research API");
        validate(game);
        string saved = game.Serialize(); var restored = GameEngine.Deserialize(saved);
        for (int i = 0; i < 75; i++) { game.AdvanceMinute(); restored.AdvanceMinute(); }
        check(game.Serialize() == restored.Serialize(), "RNG, event schedule, campaigns and minute execution continue identically after saving");
    }

    static void TestFinancialAnalysis(Action<bool, string> check)
    {
        var game = new GameEngine(2110); var trader = game.Owner(1);
        var risk = FinancialAnalysis.Portfolio(trader, game.State.Stocks);
        long shock = (long)decimal.Round((trader.GrossAssets(game.State.Stocks) - trader.Cash - trader.ShortLiability(game.State.Stocks)) * .1m);
        check(risk.EquityAfterTenPercentFall == trader.Equity(game.State.Stocks) - shock &&
            risk.EquityAfterTenPercentRise == trader.Equity(game.State.Stocks) + shock, "Long/short stress report uses net exposure and exact money rounding");
        var r = new CompanyReport { Revenue = 1000, OperatingCosts = 600, Depreciation = 50, Interest = 100, Tax = 50,
            OperatingCashFlow = 250, InvestingCashFlow = -80, Debt = 12000 };
        check(FinancialAnalysis.Ebitda(r) == 400 && FinancialAnalysis.FreeCashFlow(r) == 170 &&
            FinancialAnalysis.InterestCoverage(r) == 3.5 && FinancialAnalysis.CashConversion(r) == 1.25 && FinancialAnalysis.IncomeAfterRateRise(r) == 190,
            "Cash generation, interest cover and monthly rate sensitivity use independent financial identities");
    }

    static void TestSnapshots(Action<bool, string> check)
    {
        string folder = Path.Combine(Path.GetTempPath(), "hora-design-" + Guid.NewGuid().ToString("N"));
        try
        {
            var game = new GameEngine(3110); var store = new GameStore(folder);
            for (int i = 0; i < 83; i++) game.AdvanceMinute();
            string before = game.Serialize(); var snapshot = store.PrepareSave(game);
            game.State.Environment.Rules.RetailHerdWeight = .2;
            game.Owner(1).Development!.Plans[0].TargetWeight = .999;
            game.State.Activity.CashToInstitutions++;
            game.Owner(1).Development!.InvestmentNet[0] += 999;
            game.Owner(1).Development!.TrackedInvestments[0] = !game.Owner(1).Development!.TrackedInvestments[0];
            game.Owner(1).Development!.LastExitMinutes[0] = game.State.CompletedMinutes;
            store.WriteSnapshot(snapshot);
            check(store.Load(out _)!.Serialize() == before, "New mutable rule/plan/activity objects are isolated from live state during asynchronous saving");
            var malformed = JsonNode.Parse(before)!; malformed["Version"] = 8;
            try { GameEngine.Deserialize(malformed.ToJsonString()); check(false, "Old markets must not be reinterpreted as the new shape"); }
            catch (InvalidDataException) { check(true, "Old markets require explicit file preservation"); }
            malformed = JsonNode.Parse(before)!;
            malformed["Activity"]!["CashToInstitutions"] = -1;
            try { GameEngine.Deserialize(malformed.ToJsonString()); check(false, "Negative cross-group cash rejected"); }
            catch (InvalidDataException) { check(true, "Corrupt flow record rejected"); }
            malformed = JsonNode.Parse(before)!;
            malformed["Bots"]![0]!["Development"]!["CampaignIndex"] = 15;
            try { GameEngine.Deserialize(malformed.ToJsonString()); check(false, "Campaign cannot reference a removed security"); }
            catch (InvalidDataException) { check(true, "Corrupt campaign index rejected"); }
            byte[] original = [0, 1, 9, 30, 200], wal = [7, 8, 9];
            File.WriteAllBytes(Path.Combine(folder, "history-v5.sqlite"), original);
            File.WriteAllBytes(Path.Combine(folder, "history-v5.sqlite-wal"), wal);
            using var archive = new MemoryStream(); store.ExportLegacy(archive); archive.Position = 0;
            using var zip = new ZipArchive(archive, ZipArchiveMode.Read);
            foreach (var (name, bytes) in new[] { ("history-v5.sqlite", original), ("history-v5.sqlite-wal", wal) })
            {
                using var input = zip.GetEntry(name)!.Open(); using var copy = new MemoryStream(); input.CopyTo(copy);
                check(copy.ToArray().SequenceEqual(bytes) && File.ReadAllBytes(Path.Combine(folder, name)).SequenceEqual(bytes),
                    "Legacy archive preserves database and WAL byte-for-byte: " + name);
            }
            check(zip.GetEntry("archive-manifest.json") is not null, "Legacy archive includes a checksum manifest");
        }
        finally { Directory.Delete(folder, true); }
    }

    static void TestReplacements(Action<bool, string> check, Action<GameEngine> validate)
    {
        var game = new GameEngine(4110);
        var old = game.State.Stocks[0];
        game.FailCompany(0);
        var replacement = game.State.Stocks[0];
        check(replacement.Symbol == old.Symbol && replacement.SecurityId != old.SecurityId && replacement.Generation == 2 &&
            game.State.Stocks.Count == 15 && replacement.Active, "Company replacement retains its fixed sector slot with a new security identity");
        check(game.State.Bots.Any(t => t.Shares[0] > 0) && game.State.Retail.Any(t => t.Shares[0] > 0) && game.State.TotalMatches == 0,
            "A replacement IPO supplies real, cash-funded investor inventory without bank quotes or fake trades");
        foreach (int owner in new[] { 1, 101 })
        {
            var trader = game.Owner(owner);
            game.FailTrader(trader, "회귀 검사: 강제 청산");
            check(game.Owner(owner).Generation == 2 && !game.Owner(owner).WaitingForCapital &&
                game.Owner(owner).Equity(game.State.Stocks) == (owner == 1 ? GameEngine.InitialCash : GameEngine.RetailInitialCapital),
                "Investor replacement is funded by the real economy and clears former positions");
        }
        validate(game);
        var restored = GameEngine.Deserialize(game.Serialize());
        check(restored.State.Bankruptcies.Count == 3 && restored.SystemCash() == restored.State.InitialSystemCash,
            "Bankruptcies, replacement identity and conserved cash survive restart");
    }
}
