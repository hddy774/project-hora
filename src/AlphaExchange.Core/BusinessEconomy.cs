namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    bool closingBooks;
    public long SystemCash() => checked(Participants.Sum(t => t.Cash) + State.Bank.Cash + State.Government.Cash
        + State.FeePool + State.RealEconomy.Cash + State.Stocks.Sum(s => s.Report.Cash));

    void TransferCash(ICashAccount from, ICashAccount to, long amount, string reason)
    {
        if (amount == 0) return;
        if (amount < 0 || from.Cash < amount) throw new InvalidOperationException("현금 정산 재원 부족: " + reason);
        long destination = checked(to.Cash + amount);
        from.Cash -= amount; to.Cash = destination; cachedStats = null;
        State.CashFlows[reason] = checked(State.CashFlows.GetValueOrDefault(reason) + amount);
        Append(State.Journal, new LedgerEntry(State.NextTransactionId++, State.CompletedHours,
            AccountName(from), AccountName(to), reason, amount), 240);
    }
    static string AccountName(ICashAccount account) => account switch
    {
        Trader t => "investor:" + t.Id, CompanyReport r => "company:" + r.SecurityId,
        BankState => "bank", GovernmentState => "government", GameState => "exchange", _ => "real-economy"
    };

    void InitializeBusiness(Stock s)
    {
        long cap = Math.Max(100_000, s.MarketCap), assets = cap * 3 / 2;
        var r = new CompanyReport { IsOpening = true, SecurityId = s.SecurityId, Season = Math.Max(0, State.Season - 1),
            AverageShares = s.OutstandingShares, OutstandingShares = s.OutstandingShares,
            Cash = assets / 4, Inventory = assets / 8, FixedAssets = assets - assets / 4 - assets / 8,
            Debt = assets - cap, Revenue = cap / 20, OperatingCosts = (long)(cap / 20 * s.CostRatio),
            Depreciation = (assets - assets / 4 - assets / 8) / 500,
            Interest = (long)((assets - cap) * State.Government.Policy.BaseRate / 12) };
        r.Tax = (long)(Math.Max(0, r.OperatingProfit - r.Interest) * State.Government.Policy.TaxRate);
        r.OperatingCashFlow = r.NetIncome + r.Depreciation; r.OpeningCash = r.Cash - r.OperatingCashFlow;
        r.OpeningEquity = r.Equity - r.NetIncome;
        s.BaseRevenue = r.Revenue; s.InitialFixedAssets = r.FixedAssets;
        s.Report = r; s.Reports = [CopyReport(r)];
    }

    void PayWages(long budget, ICashAccount source, string reason)
    {
        budget = Math.Min(budget, source.Cash);
        long each = budget / RetailCount;
        if (each == 0) { TransferCash(source, State.RealEconomy, budget, reason); return; }
        foreach (var t in State.Retail)
        { TransferCash(source, t, each, reason); t.WageIncome += each; }
        State.RealEconomy.Wages += each * RetailCount;
        long remainder = budget - each * RetailCount;
        TransferCash(source, State.RealEconomy, remainder, reason);
    }

    void CloseBusinesses()
    {
        var p = State.Government.Policy;
        // Households consume from available funds, and capital flows are excluded from investment returns.
        foreach (var t in State.Retail)
        {
            CancelOrders(t.Id);
            long consumption = Math.Min(AvailableCash(t), Math.Max(0, t.Cash / 150));
            TransferCash(t, State.RealEconomy, consumption, "consumption");
            t.Consumption += consumption; State.RealEconomy.Consumption += consumption;
        }
        State.RealEconomy.Demand = Math.Clamp(.98 + .12 * Math.Sin(State.Season * Math.PI / 6)
            - p.BaseRate * 1.5, .65, 1.2);
        foreach (var s in State.Stocks.Where(s => s.Active))
        {
            var old = s.Report;
            double capacity = Math.Clamp((double)old.FixedAssets / Math.Max(1, s.InitialFixedAssets), .4, 3);
            double demand = (1 + (State.RealEconomy.Demand - 1) * s.DemandSensitivity) * Math.Clamp(1 + s.SeasonNewsImpact * .35, .6, 1.4);
            long target = Math.Max(0, (long)(s.BaseRevenue * (.65 + .35 * capacity) * demand));
            long revenue = Math.Min(target, Math.Max(0, State.RealEconomy.Cash / 20));
            var r = new CompanyReport { SecurityId = s.SecurityId, Season = State.Season - 1,
                AverageShares = s.ShareHours == 0 ? s.OutstandingShares : s.ShareHourSum / s.ShareHours,
                OutstandingShares = s.OutstandingShares, SplitFactor = s.SplitFactor,
                Cash = old.Cash, Inventory = old.Inventory, Receivables = old.Receivables,
                FixedAssets = old.FixedAssets, Debt = old.Debt,
                OpeningCash = s.Reports.LastOrDefault(r => r.AccountingBasis == 5)?.Cash ?? old.Cash,
                OpeningEquity = s.Reports.LastOrDefault(r => r.AccountingBasis == 5)?.Equity ?? old.Equity,
                Dividends = s.PendingDividends, DividendsPerShare = s.PendingDividendPerShare, CapitalChange = s.PendingCapitalChange, FinancingCashFlow = s.PendingFinancingFlow,
                InvestingCashFlow = s.PendingInvestingFlow,
                NewsCount = s.SeasonNewsCount, NewsImpact = s.SeasonNewsImpact };
            s.Report = r;
            TransferCash(State.RealEconomy, r, revenue, "company-sales"); r.Revenue = revenue;
            State.RealEconomy.CorporateSales += revenue;
            double costRatio = Math.Clamp(s.CostRatio - s.SeasonNewsImpact * .06 + p.BaseRate * .35, .60, .94);
            long costs = Math.Min(r.Cash, (long)(revenue * costRatio));
            long wages = costs / 3;
            PayWages(wages, r, "company-wages");
            TransferCash(r, State.RealEconomy, costs - wages, "company-costs"); r.OperatingCosts = costs;
            State.RealEconomy.CorporateCosts += costs;
            r.Depreciation = Math.Min(r.FixedAssets, r.FixedAssets / 500); r.FixedAssets -= r.Depreciation;
            r.Interest = (long)Math.Ceiling(r.Debt * p.BaseRate / 12);
            long interestPaid = Math.Min(r.Cash, r.Interest);
            TransferCash(r, State.Bank, interestPaid, "company-interest"); State.Bank.CorporateInterest += interestPaid;
            r.Debt += r.Interest - interestPaid;
            r.Tax = Math.Min(r.Cash, (long)(Math.Max(0, r.OperatingProfit - r.Interest) * p.TaxRate));
            TransferCash(r, State.Government, r.Tax, "company-tax");
            State.Government.CorporateTaxes += r.Tax;
            r.OperatingCashFlow = revenue - costs - interestPaid - r.Tax;
            long investment = Math.Min(Math.Max(0, r.NetIncome / 4), Math.Max(0, r.Cash - revenue / 2));
            TransferCash(r, State.RealEconomy, investment, "company-investment");
            r.FixedAssets += investment; r.InvestingCashFlow -= investment; State.RealEconomy.Investment += investment;
            long dividendBudget = Math.Min(Math.Max(0, (long)(r.NetIncome * s.DividendPayout)), Math.Max(0, r.Cash - revenue));
            PayDividend(s.SecurityId, dividendBudget);
            Append(s.Reports, CopyReport(r), 12);
            s.PendingCapitalChange = s.PendingFinancingFlow = s.PendingInvestingFlow = s.PendingDividends = s.PendingDividendPerShare = 0;
            s.ShareHourSum = s.ShareHours = 0; s.SeasonNewsImpact = 0; s.SeasonNewsCount = 0;
            // Public per-share cash generation and book assets, never an initial-price multiplier.
            double annualEps = (double)r.NetIncome * 12 / Math.Max(1, s.OutstandingShares);
            double bps = (double)r.Equity / Math.Max(1, s.OutstandingShares);
            double earningsValue = Math.Max(0, annualEps) * Math.Clamp(11 - p.BaseRate * 55, 5, 14);
            double estimate = earningsValue * .65 + Math.Max(0, bps) * .35;
            s.FairValue = Math.Clamp(s.FairValue * .55 + estimate * .45, 1, 10_000_000);
        }
        State.Government.TaxEscrow = 0;
        foreach (var t in Participants)
        {
            t.LossCarryForward = p.LossCarryForward ? Math.Max(0, t.LossCarryForward - t.SeasonRealizedProfit) : 0;
            t.SeasonRealizedProfit = 0; t.SeasonTaxPaid = 0;
        }
        long govtBudget = Math.Min(State.Government.Cash, (long)(State.Government.Cash * p.SpendingRatio * .18));
        PayWages(govtBudget / 4, State.Government, "government-wages");
        TransferCash(State.Government, State.RealEconomy, govtBudget - govtBudget / 4, "government-spending");
        State.Government.Spending += govtBudget;
        long exchangeBudget = State.FeePool * 85 / 100;
        PayWages(exchangeBudget / 2, State, "exchange-wages");
        TransferCash(State, State.RealEconomy, exchangeBudget - exchangeBudget / 2, "exchange-operating");
        State.ExchangeSpending += exchangeBudget;
        RestoreSectorListings();
        RunCorporatePolicy();
        foreach (var stock in State.Stocks.Where(s => s.Active && !s.Report.IsOpening))
        {
            stock.Reports.RemoveAll(r => r.Season == stock.Report.Season && r.AccountingBasis == 5);
            Append(stock.Reports,CopyReport(stock.Report),12);
        }
    }

    void UpdatePerformance()
    {
        foreach (var t in Participants)
        {
            long equity = t.Equity(State.Stocks), contribution = t.NetContribution;
            if (t.LastReturnEquity > 0)
                t.ReturnIndex *= Math.Max(0, (double)(equity - (contribution - t.LastNetContribution)) / t.LastReturnEquity);
            t.LastReturnEquity = equity; t.LastNetContribution = contribution;
        }
        State.PriceIndex = State.Stocks.Sum(s => s.MarketCap) / State.IndexDivisor;
        if (State.LastPriceIndex > 0)
            State.TotalReturnIndex *= (State.PriceIndex + (State.DividendIndexPoints - State.LastDividendIndexPoints)) / State.LastPriceIndex;
        State.LastPriceIndex = State.PriceIndex; State.LastMarketDividends = State.MarketDividends; State.LastDividendIndexPoints = State.DividendIndexPoints;
    }
}
