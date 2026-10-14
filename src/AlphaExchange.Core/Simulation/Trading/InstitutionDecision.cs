namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    internal void DecideInstitution(Trader trader)
    {
        if (trader.WaitingForCapital) return;
        var d = trader.Development!;
        bool news = false;
        for (int i = 0; i < StockCount; i++)
            if (Math.Abs(State.Stocks[i].Sentiment - d.LastNewsSignals[i]) >= Rules.Investment.MaterialNews) { news = true; break; }
        if (State.CompletedMinutes < d.NextExecutionMinute && !(news && State.CompletedMinutes - d.LastDecisionMinute >= 5)) return;
        CancelInstitutionOrders(trader.Id, false);
        long equity = trader.Equity(State.Stocks);
        var skills = Capabilities(trader).Effective;
        d.PortfolioReviews++;
        d.LastDecisionHour = State.CompletedHours;
        d.LastDecisionMinute = State.CompletedMinutes;
        d.NextExecutionMinute = State.CompletedMinutes + ExecutionDelay(trader, skills);
        d.NextDecisionHour = State.CompletedHours + 1;
        ReviewCampaign(trader, equity);
        WriteAnalystReports(trader, skills);
        BuildPortfolioPlan(trader, equity, skills);
        ManageInstitutionCredit(trader, equity);
        bool margin = trader.ShortCollateral(State.Stocks) > trader.Cash;
        if (margin) trader.MarginCalls++;
        // Risk exits and excess positions execute before new purchases.
        int attempts = 0;
        int orderBudget = Rules.Investment.OrderAttempts;
        for (int i = 0; i < StockCount && attempts < orderBudget; i++)
            if (ExecuteRiskExit(trader, i, margin)) attempts++;
        if (!margin && StockAllocationShortfall(trader) == 0 && attempts < orderBudget)
        {
            for (int i = 0; i < StockCount && attempts < 2; i++)
                if (trader.Shares[i] == 0 && institutionSignals[i].Score < Math.Min(-.12, Rules.Investment.ShortScore) &&
                    PlaceHedge(trader, i, Math.Max(1, trader.GrossAssets(State.Stocks)))) attempts++;
        }
        Span<bool> visited = stackalloc bool[StockCount];
        while (attempts < orderBudget)
        {
            long gross = Math.Max(1, trader.GrossAssets(State.Stocks));
            int selected = -1; double best = StockAllocationShortfall(trader) > 0 ? .001 : .025;
            for (int i = 0; i < StockCount; i++)
            {
                if (visited[i] || !State.Stocks[i].Active) continue;
                double weight = (double)State.Stocks[i].Value(trader.Shares[i]) / gross;
                double delta = decisionPlans[i]!.TargetWeight - weight;
                double priority = Math.Abs(delta) * (delta < 0 ? 1.5 : 1);
                if (decisionPlans[i]!.TargetWeight == 0 && trader.Shares[i] > 0) priority = Math.Max(priority, .03);
                if (priority > best) { best = priority; selected = i; }
            }
            if (selected < 0) break;
            visited[selected] = true;
            if (PlacePortfolioOrder(trader, selected, margin)) attempts++;
        }

        string campaign = d.CampaignIndex < 0 ? "분산 관찰" : State.Stocks[d.CampaignIndex].Symbol + " " + CampaignNames[(int)d.CampaignPhase];
        trader.Decision = margin ? "담보 부족 · 보유 청산·상환 우선" : $"{campaign} · 현금 목표 {d.TargetCashRatio:P0}";
        trader.ActiveMethods = $"가치·추세·뉴스·호가·개미 수요 · {ExecutionDelay(trader, skills)}분 재평가";
    }

    void ManageInstitutionCredit(Trader trader, long equity)
    {
        var terms = LoanTerms(trader); var d = trader.Development!;
        long gross = Math.Max(1, trader.GrossAssets(State.Stocks));
        long excess = Math.Max(0, AvailableCash(trader) - (long)(gross * d.TargetCashRatio));
        if (trader.LoanDebt > 0 && (trader.LoanDebt > terms.Limit || d.MaximumDrawdown > .1 || excess > gross * .08))
            RepayLoan(trader.Id, Math.Max(trader.LoanDebt - terms.Limit, excess));
        else if (trader.LoanDebt < equity * Rules.Investment.MaximumLoanRatio &&
            trader.CreditScore >= Rules.Investment.CreditBorrowMinimum && d.MaximumDrawdown < .08 &&
            CashRatio(trader) < d.TargetCashRatio && institutionSignals.Any(s => s.Score > Rules.Investment.BorrowScore && !s.Defensive))
        {
            long amount = Math.Min(terms.Available, Math.Max(0, (long)(equity * Rules.Investment.MaximumLoanRatio) - trader.LoanDebt));
            if (amount > 0) BorrowCash(trader.Id, amount);
        }
    }
}
