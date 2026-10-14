namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    public IReadOnlyList<AnalystReport> ConfidentialReports(int requesterId, int ownerId)
        => requesterId == ownerId && ownerId is >= 1 and <= AiCount ? Owner(ownerId).Development!.AnalystReports.AsReadOnly() : Array.Empty<AnalystReport>();
    public IReadOnlyList<InvestmentPlan> ConfidentialPlans(int requesterId, int ownerId)
        => requesterId == ownerId && ownerId is >= 1 and <= AiCount ? Owner(ownerId).Development!.Plans.AsReadOnly() : Array.Empty<InvestmentPlan>();

    void WriteAnalystReports(Trader trader, Abilities skills)
    {
        var d = trader.Development!;
        if (d.LastReportMinute >= 0 && State.CompletedMinutes - d.LastReportMinute < MarketRules.ReportIntervalHours * 60) return;
        d.AnalystReports.Clear();
        foreach (var sector in sectorIds)
        {
            double total = 0; int count = 0;
            for (int i = 0; i < StockCount; i++)
            {
                var stock = State.Stocks[i];
                if (!stock.Active || signalSectors[i] != sector.Value) continue;
                total += Math.Clamp(stock.FairValue / stock.Price - 1, -.5, .8) * .45 + trends[i] * .2 +
                    stock.Sentiment * .25 + (State.Environment.EconomicActivity - 1) * .1 - State.Government.Policy.BaseRate * stock.Report.DebtRatio * .1;
                count++;
            }
            double skill = (skills.Valuation + skills.News + skills.Macro) / 300.0;
            double expected = Math.Clamp(total / Math.Max(1, count) * (.6 + skill * .4), -.5, .8);
            d.AnalystReports.Add(new AnalystReport
            {
                OwnerId = trader.Id, Sector = sector.Key, WrittenMinute = State.CompletedMinutes,
                ExpiresMinute = State.CompletedMinutes + MarketRules.ReportLifetimeHours * 60,
                ExpectedReturn = expected, Confidence = Math.Clamp(.4 + skill * .5, .1, .95),
                Thesis = $"{sector.Key} {(expected >= 0 ? "상승" : "하락")} 전망 · 실적·공개 뉴스·금리 분석"
            });
        }
        d.LastReportMinute = State.CompletedMinutes;
    }

    void BuildPortfolioPlan(Trader trader, long equity, Abilities skills)
    {
        var d = trader.Development!;
        Span<double> weights = stackalloc double[StockCount];
        double cashTarget = Rules.Investment.MinimumCash;
        decisionSecurities.Clear();
        for (int i = 0; i < StockCount; i++) decisionSecurities.Add(State.Stocks[i].SecurityId, i);
        d.Plans.RemoveAll(p => !decisionSecurities.ContainsKey(p.SecurityId));
        if (decisionPlans.Length != StockCount) decisionPlans = new InvestmentPlan?[StockCount];
        else Array.Clear(decisionPlans);
        foreach (var plan in d.Plans) decisionPlans[decisionSecurities[plan.SecurityId]] = plan;
        for (int i = 0; i < StockCount; i++)
        {
            var stock = State.Stocks[i];
            var signal = institutionSignals[i] = Analyze(trader, i, equity, skills);
            cashTarget = Math.Max(cashTarget, 1 - signal.TargetExposure);
            double raw = Math.Clamp(1 + signal.Score * 4 + ((trader.Id * 7 + i * 11) % 13 - 6) * .025, .25, 3) / (1 + signal.Volatility * 8);
            if (!stock.Active || stock.Report.Equity <= 0) raw = 0;
            var existing = decisionPlans[i];
            bool stopped = existing is not null && trader.Shares[i] > 0 && stock.Price <= existing.StopPrice;
            bool takeProfit = existing is not null && trader.Shares[i] > 0 && stock.Price >= existing.TakeProfitPrice;
            if (stopped || takeProfit || d.LastExitMinutes[i] >= 0 && State.CompletedMinutes - d.LastExitMinutes[i] < 1440) raw = 0;
            if (i == d.CampaignIndex)
            {
                raw *= d.CampaignPhase switch { CampaignPhase.Accumulate => 1.25, CampaignPhase.AttractFollowers => 1.35, CampaignPhase.Distribute => 0, _ => 0 };
                if (d.CampaignPhase == CampaignPhase.AttractFollowers && !stopped && raw > 0) raw += .2;
            }
            // Each institution also retains a different long-horizon core name.
            // This prevents a shared news signal from removing every bid at once.
            if (stock.Active && i == (trader.Id - 1) % StockCount) raw = Math.Max(.75, raw);
            weights[i] = raw;
            d.LastNewsSignals[i] = stock.Sentiment;
        }
        int eligible = 0;
        for (int i = 0; i < StockCount; i++) if (weights[i] > 0) eligible++;
        // If every thesis fails, retain a diversified core rather than abandon
        // the mandatory allocation. Restore a diversified set of eligible names.
        if (eligible < 5)
            for (int i = 0; i < StockCount; i++)
                if (State.Stocks[i].Active && (eligible == 0 || weights[i] == 0)) weights[i] = Math.Max(.25, weights[i]);
        d.TargetCashRatio = Math.Clamp(cashTarget, Rules.Investment.MinimumCash, Math.Min(.5, Rules.Investment.MaximumCash));
        AllocatePortfolioWeights(weights, 1 - d.TargetCashRatio);
        for (int i = 0; i < StockCount; i++)
        {
            var signal = institutionSignals[i];
            var plan = PlanInvestment(trader, i, signal, decisionPlans[i], weights[i], equity);
            decisionPlans[i] = plan;
            d.PositionHorizons[i] = signal.Horizon;
            d.NextReviewHours[i] = State.CompletedHours + 1;
        }
        d.PlanRevision++;
    }

    void AllocatePortfolioWeights(Span<double> weights, double budget)
    {
        // Bounded water-filling: a single cash budget, stock cap and sector cap.
        // All targets together fit; fifteen independent maxima cannot overcommit.
        Span<double> raw = stackalloc double[StockCount]; weights.CopyTo(raw); weights.Clear();
        Span<double> sector = stackalloc double[MarketDefaults.SectorCount];
        for (int pass = 0; pass < StockCount && budget > 1e-8; pass++)
        {
            double sum = 0;
            for (int i = 0; i < StockCount; i++)
                if (weights[i] < institutionSignals[i].StockLimit - 1e-8 && sector[signalSectors[i]] < institutionSignals[i].SectorLimit - 1e-8) sum += raw[i];
            if (sum <= 0) break;
            double available = budget;
            for (int i = 0; i < StockCount; i++)
            {
                var signal = institutionSignals[i];
                double add = Math.Min(available * raw[i] / sum,
                    Math.Min(signal.StockLimit - weights[i], signal.SectorLimit - sector[signalSectors[i]]));
                add = Math.Max(0, Math.Min(add, budget));
                weights[i] += add; sector[signalSectors[i]] += add; budget -= add;
            }
        }
    }

    InvestmentPlan PlanInvestment(Trader trader, int index, InvestmentSignal signal, InvestmentPlan? previous, double target, long equity)
    {
        var stock = State.Stocks[index]; var d = trader.Development!;
        bool renew = previous is null || previous.TargetMinute <= State.CompletedMinutes || previous.Horizon != signal.Horizon ||
            trader.Shares[index] > 0 && trader.Development!.PositionOpenedHours[index] * 60 > previous.CreatedMinute;
        long duration = Rules.Horizons[(int)signal.Horizon].ExpectedHours * 60L;
        double risk = Math.Clamp(.035 + signal.Volatility * 3 + trader.Risk * .04, .025, .14);
        double reference = trader.Shares[index] > 0 && trader.AverageCost[index] > 0 ? trader.AverageCost[index] : stock.Price;
        var plan = previous ?? new InvestmentPlan { SecurityId = stock.SecurityId };
        if (renew)
        {
            plan.CreatedMinute = State.CompletedMinutes;
            plan.DurationMinutes = duration;
            plan.TargetMinute = plan.CreatedMinute + duration;
            plan.StopPrice = Quote(reference * (1 - risk), false);
            plan.TakeProfitPrice = Quote(reference * (1 + risk * 2), true);
        }
        plan.ReviewMinute = State.CompletedMinutes + ExecutionDelay(trader, trader.Abilities);
        plan.LowerPrice = Quote(stock.Price * (1 - Math.Clamp(risk, MarketRules.PlanBandMinimum, MarketRules.PlanBandMaximum)), false);
        plan.UpperPrice = Quote(stock.Price * (1 + Math.Clamp(risk, MarketRules.PlanBandMinimum, MarketRules.PlanBandMaximum)), true);
        plan.TargetWeight = target;
        plan.MaximumWeight = signal.StockLimit;
        plan.ExpectedReturn = signal.Score;
        plan.Confidence = Math.Clamp(.45 + trader.Abilities.Valuation / 250.0 - signal.Volatility * 3, .1, .9);
        plan.EstimatedCost = State.Government.Policy.FeeBasisPoints * .0002 + SpreadRatio(index);
        plan.LossBudget = (long)(Math.Max(0, equity) * target * risk);
        plan.Style = signal.Defensive ? PortfolioStyle.Defensive : trader.Disposition == Disposition.Aggressive ? PortfolioStyle.Opportunistic : PortfolioStyle.Balanced;
        plan.Horizon = signal.Horizon;
        if (renew) plan.Thesis = $"가치 {stock.FairValue / stock.Price - 1:P1} · 개미 수요 {RetailPressure(index):+0.00;-0.00;0.00} · 호가 불균형 {BookImbalance(index):+0.00;-0.00;0.00}";
        if (renew) plan.ExitCondition = $"손절 {KoreanNumber.Compact(plan.StopPrice)} / 익절 {KoreanNumber.Compact(plan.TakeProfitPrice)} · 기한·뉴스·비중 이탈 재평가";
        if (previous is null) d.Plans.Add(plan);
        return plan;
    }

    int ExecutionDelay(Trader trader, Abilities skills)
        => Math.Clamp(30 - skills.Execution / 10 - (int)(trader.Risk * 5), 15, 30);
}
