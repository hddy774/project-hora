using AlphaExchange.Core;
using System.Globalization;
using System.Text.Json;
using System.Diagnostics;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var destination = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Environment.CurrentDirectory, "artifacts", "market-audit");
Directory.CreateDirectory(destination);
uint[] seeds = args.Length > 2 ? args[2].Split(',').Select(uint.Parse).ToArray() : [20261004, 42, 17, 20260308, 314159];
int seasons = args.Length > 1 ? int.Parse(args[1]) : 36;
if (seasons is < 1 or > 120 || seeds.Length is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(args));
var summaries = new List<object>();
using var csv = new StreamWriter(Path.Combine(destination, "monthly.csv"));
csv.WriteLine("scenario,seed,season,hour,capitalization,investor_cash,investor_equity,exchange_fees,bank_cash,government_cash,loan_debt,short_liability,fine_debt,company_equity,company_profit,company_dividends,cumulative_declared_dividends,capitalization_return,investor_cash_change,investor_equity_return,system_cash_difference,initial_system_cash,initial_investor_cash,initial_investor_equity,initial_capitalization,fair_value_ratio,price_to_fair_value,price_floor_stocks,price_index,total_return_index,net_contributions,paid_dividends");
foreach (var scenario in new[] { "baseline", "zero-fee" })
foreach (uint seed in scenario == "baseline" ? seeds : seeds.Take(1))
{
    var watch = Stopwatch.StartNew();
    var game = new GameEngine(seed);
    if (scenario == "zero-fee") game.State.Government.Policy.FeeBasisPoints = 0;
    long initialCap = Cap(), initialInvestorCash = Cash(), initialEquity = Equity();
    long reportedDividends = 0;
    long lowestCap = initialCap, previousCap = initialCap;
    int decliningSeasons = 0;
    var initialFairValues = game.State.Stocks.ToDictionary(s => s.SecurityId,s => s.FairValue);
    Record(0);
    for (int hour = 1; hour <= seasons * 720; hour++)
    {
        game.AdvanceHour();
        if (hour % 720 != 0) continue;
        reportedDividends += game.State.Stocks.Sum(s => s.Report.Dividends);
        long cap = Cap();
        if (cap < previousCap) decliningSeasons++;
        previousCap = cap; lowestCap = Math.Min(lowestCap, cap);
        Record(hour / 720);
        game.State.PendingSeasons.Clear(); // Audit omits disk archives; no trading inputs change.
        // RefreshEconomy cancels all resting orders before changing the fee policy.
        if (scenario == "zero-fee") game.State.Government.Policy.FeeBasisPoints = 0;
    }
    var stocks = game.State.Stocks.Where(s => s.Active).Select((s,i) => new { s.Symbol, s.Sector, s.Price, s.StartPrice, s.TotalShares,
        s.FairValue, initialFairValue=initialFairValues.GetValueOrDefault(s.SecurityId,s.StartPrice), capitalization=s.MarketCap,
        corporateBookEquity=s.Report.Equity, monthlyProfit=s.Report.NetIncome, annualizedPer=s.Report.NetIncome > 0 ? (double?)s.MarketCap/(s.Report.NetIncome*12) : null,
        pbr=s.Report.Equity > 0 ? (double?)s.MarketCap/s.Report.Equity : null }).ToArray();
    var summary = new { scenario, seed, seasons, initialCapitalization=initialCap, finalCapitalization=Cap(),
        capitalizationReturn=(double)Cap()/initialCap-1, initialInvestorCash, finalInvestorCash=Cash(),
        investorCashChange=(double)Cash()/initialInvestorCash-1, initialInvestorEquity=initialEquity, finalInvestorEquity=Equity(),
        investorEquityReturn=(double)Equity()/initialEquity-1, exchangeFees=game.State.FeePool, bankCash=game.State.Bank.Cash,
        governmentCash=game.State.Government.Cash, initialSystemCash=game.State.InitialSystemCash,
        reportedDividends, decliningSeasons, lowestSeasonCloseCapitalization=lowestCap,
        priceIndex=game.State.PriceIndex, totalReturnIndex=game.State.TotalReturnIndex, netContributions=game.Participants.Sum(t=>t.NetContribution), paidDividends=game.Participants.Sum(t=>t.DividendIncome), wallSeconds=watch.Elapsed.TotalSeconds, stocks };
    summaries.Add(summary);
    Console.WriteLine($"{scenario} seed={seed}: cap={(double)Cap()/initialCap-1:P2}; investor cash={(double)Cash()/initialInvestorCash-1:P2}; fees={game.State.FeePool}; declared dividends={reportedDividends}; down seasons={decliningSeasons}/{seasons}; {watch.Elapsed.TotalSeconds:F2}s");

    long Cap() => game.State.Stocks.Sum(s => s.MarketCap);
    long Cash() => game.Participants.Sum(t => t.Cash);
    long Equity() => game.Participants.Sum(t => t.Equity(game.State.Stocks));
    void Record(int season)
    {
        var state = game.State;
        long cash = Cash(), equity = Equity(), capitalization = Cap();
        long difference = game.SystemCash() - state.InitialSystemCash;
        if (difference != 0) throw new Exception("Cash reconciliation failed");
        for (int i=0;i<state.Stocks.Count;i++)
            if (game.Participants.Sum(t => t.Shares[i]) + state.Bank.ShareInventory[i] + state.Stocks[i].TreasuryShares + state.Stocks[i].FounderShares != state.Stocks[i].TotalShares)
                throw new Exception("Share reconciliation failed");
        double fairRatio = state.Stocks.Where(s => s.Active).Average(s => s.FairValue/initialFairValues.GetValueOrDefault(s.SecurityId,s.StartPrice));
        double priceToFair = state.Stocks.Average(s => s.Price/s.FairValue);
        csv.WriteLine(string.Join(",", scenario,seed,season,state.CompletedHours,capitalization,cash,equity,state.FeePool,state.Bank.Cash,
            state.Government.Cash,game.Participants.Sum(t=>t.LoanDebt),game.Participants.Sum(t=>t.ShortLiability(state.Stocks)),
            game.Participants.Sum(t=>t.FineDebt),state.Stocks.Sum(s=>s.Report.Equity),state.Stocks.Sum(s=>s.Report.NetIncome),
            state.Stocks.Sum(s=>s.Report.Dividends),reportedDividends,(double)capitalization/initialCap-1,(double)cash/initialInvestorCash-1,
            (double)equity/initialEquity-1,difference,state.InitialSystemCash,initialInvestorCash,initialEquity,initialCap,fairRatio,priceToFair,
            state.Stocks.Count(s=>s.Active && s.Price==1),state.PriceIndex,state.TotalReturnIndex,game.Participants.Sum(t=>t.NetContribution),game.Participants.Sum(t=>t.DividendIncome)));
        csv.Flush();
    }
}
File.WriteAllText(Path.Combine(destination,"summary.json"),JsonSerializer.Serialize(new { source="project-hora current checkout", baselineReferenceCommit="e11579ac794f043c7b2cec10052746af9e5e6adf", seasons,seeds,summaries },new JsonSerializerOptions { WriteIndented=true }));
