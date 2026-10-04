namespace AlphaExchange.Core;

public sealed record InvestmentSignal(double Score, double TargetExposure, double StockLimit, double SectorLimit, bool Defensive, double Volatility);

public sealed partial class GameEngine
{
    readonly double[] trends = new double[StockCount], fastTrends = new double[StockCount], volatilities = new double[StockCount];
    double marketTrend;
    internal void PrepareMarketSignals()
    {
        marketTrend = 0;
        for (int i = 0; i < State.Stocks.Count; i++)
        {
            var s = State.Stocks[i]; int n = s.History.Count;
            trends[i] = (double)s.PreviousPrice / s.History[Math.Max(0, n - 24)] - 1;
            fastTrends[i] = (double)s.PreviousPrice / s.History[Math.Max(0, n - 3)] - 1;
            double sum = 0; int count = 0;
            for (int j = Math.Max(1, n - 12); j < n; j++) { sum += Math.Abs((double)s.History[j] / s.History[j - 1] - 1); count++; }
            volatilities[i] = s.Volatility = count == 0 ? 0 : sum / count; marketTrend += trends[i] / State.Stocks.Count;
        }
    }
    public InvestmentSignal Analyze(Trader t, int index)
    {
        if (index < 0 || index >= State.Stocks.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var s = State.Stocks[index]; var a = t.Abilities;
        double trend = trends[index], fast = fastTrends[index], volatility = volatilities[index];
        double risk = a.RiskManagement / 100.0, macro = a.Macro / 100.0;
        bool defensive = marketTrend < -.025 || trend < -.05 || volatility > .012 || t.Equity(State.Stocks) < t.SeasonOpeningEquity * .88;
        double exposure = .40 + t.Risk * .20 - State.Government.Policy.BaseRate * macro * 1.5;
        if (t.Disposition == Disposition.Cautious) exposure -= .12;
        if (t.Disposition == Disposition.Aggressive) exposure += .06;
        if (defensive) exposure *= 1 - (.35 + risk * .45);
        double value = Math.Clamp(s.FairValue / s.PreviousPrice - 1, -.75, 2);
        // Every method participates; skill and regime change its weight.
        double score = value * (.04 + a.Valuation * .0018) + trend * (.03 + a.Technical * .002) + s.Sentiment * (.03 + a.News * .002)
            - fast * (.02 + a.SwingTrading * .001) + fast * (.01 + a.Scalping * .0015) + s.Report.Margin * .015;
        score += t.Disposition switch { Disposition.Analytical => value * .035, Disposition.Opportunistic => fast * .06,
            Disposition.Sociable => s.Sentiment * .03, Disposition.Cautious => -volatility * .4, _ => trend * .025 };
        if (defensive) score -= .025 + risk * .03 + Math.Max(0, -trend) * macro * .3;
        // Strong fundamental discounts permit gradual re-entry while keeping a cash reserve.
        if (value > .25 && a.Valuation > 55 && volatility < .03)
            exposure = Math.Max(exposure, .25 + a.RiskManagement * .001);
        score -= State.Government.Policy.BaseRate * macro * .08;
        return new InvestmentSignal(score, Math.Clamp(exposure, .06, .75), .20 - a.Diversification * .001,
            .48 - a.Diversification * .002, defensive, volatility);
    }
    void DecideInstitution(Trader t)
    {
        CancelOrders(t.Id); var stocks = State.Stocks;
        long equity = Math.Max(1, t.Equity(stocks));
        var signals = new InvestmentSignal[stocks.Count];
        for (int i = 0; i < stocks.Count; i++) signals[i] = Analyze(t, i);
        bool defensive = signals.Any(s => s.Defensive);
        t.Decision = defensive ? "하락·변동성 대응: 현금 확보 / 부채 축소 / 헤지" : "가치·추세·뉴스·단타·초단타 혼합 / 분산 비중 관리";
        var offer = LoanTerms(t);
        if (t.LoanDebt > 0 && (defensive || t.LoanDebt > offer.Limit || AvailableCash(t) > equity * .40))
            RepayLoan(t.Id, Math.Max(t.LoanDebt - offer.Limit, (long)(AvailableCash(t) * .15)));
        else if (!defensive && t.CreditScore >= 70 && t.Abilities.Valuation > 60 && signals.Max(s => s.Score) > .025 && t.LoanDebt < equity * .1)
            BorrowCash(t.Id, Math.Min(offer.Available, Math.Max(0, (long)(equity * .08) - t.LoanDebt)));
        bool margin = t.ShortCollateral(stocks) + t.ReservedCash > t.Cash;
        if (margin) { t.MarginCalls++; t.Decision = "담보 부족: 공매도 상환·보유 주식 청산"; }
        bool sellForCash = defensive || margin || t.LoanDebt > offer.Limit;
        int attempts = 2 + (t.Abilities.Execution > 75 ? 1 : 0);
        for (int j = 0; j < attempts; j++)
        {
            int index = (int)(Next() * stocks.Count);
            if (j == 0 && sellForCash) index = Enumerable.Range(0, stocks.Count).OrderByDescending(i => (long)t.Shares[i] * stocks[i].Price).First();
            var s = stocks[index]; var signal = signals[index];
            double longExposure = (double)(t.GrossAssets(stocks) - t.Cash) / equity;
            double weight = (double)t.Shares[index] * s.Price / equity;
            double sectorWeight = stocks.Select((stock, i) => (stock, i)).Where(x => x.stock.Sector == s.Sector).Sum(x => (double)t.Shares[x.i] * x.stock.Price) / equity;
            int desired = Math.Max(1, (int)(equity * (.003 + t.Abilities.Execution * .000035) / s.Price));
            double spread = .003 + (100 - t.Abilities.Execution) * .00004;
            double noise = (Next() - .5) * .006 * (1 - t.Abilities.Technical / 130.0);
            double offset = Math.Clamp(signal.Score * .12 + noise, -.018, .018);
            bool exitShort = t.ShortShares[index] > 0 && (margin || !State.Government.Policy.ShortSellingAllowed || signal.Score > .004 ||
                (double)s.Price / Math.Max(1, t.ShortAveragePrice[index]) > 1.06 || (double)s.Price / Math.Max(1, t.ShortAveragePrice[index]) < .94);
            if (exitShort)
            {
                int price = Quote(s.PreviousPrice * (1 + spread), true);
                int q = Math.Min(t.ShortShares[index], Math.Max(desired, margin ? t.ShortShares[index] : 1));
                while (q > 0 && SubmitOrder(t.Id, index, true, price, q, 1, cover: true) is not null) q /= 2;
                continue;
            }
            bool overweight = weight >= signal.StockLimit || sectorWeight >= signal.SectorLimit;
            bool buy = !margin && !overweight && longExposure < signal.TargetExposure && (signal.Score > -.008 || Next() < .3);
            if (buy)
            {
                double budget = Math.Min(signal.StockLimit - weight, signal.SectorLimit - sectorWeight);
                budget = Math.Min(budget, signal.TargetExposure - longExposure);
                int price = Quote(s.PreviousPrice * (1 + offset + spread), true);
                int q = Math.Min(desired, Math.Min(MaxBuy(t, index, price), Math.Max(0, (int)(equity * budget / price))));
                if (q > 0) SubmitOrder(t.Id, index, true, price, q, t.Abilities.Scalping > 70 ? 1 : 3);
            }
            else if (t.Shares[index] > 0 && (sellForCash || overweight || signal.Score < .004 || Next() < .3))
            {
                int price = Quote(s.PreviousPrice * (1 + offset - spread), false);
                int q = Math.Min(t.Shares[index] - t.ReservedShares[index], desired * (sellForCash ? 3 : 1));
                if (q > 0) SubmitOrder(t.Id, index, false, price, q, 2);
            }
            else if (signal.Score < -.025 && t.Abilities.RiskManagement > 45 && t.Abilities.Technical > 45 && t.ShortLiability(stocks) < equity * .06)
            {
                int price = Quote(s.PreviousPrice * (1 - spread), false);
                SubmitOrder(t.Id, index, false, price, desired, 1, shortSale: true);
            }
        }
        if (margin)
            foreach (int i in Enumerable.Range(0, stocks.Count).Where(i => t.ShortShares[i] > 0 && t.ReservedCovers[i] == 0))
            { int price = Math.Clamp((int)(stocks[i].Price * 1.015), 100, 10_000_000); int q = t.ShortShares[i]; while (q > 0 && SubmitOrder(t.Id, i, true, price, q, 1, cover: true) is not null) q /= 2; }
        var operation = State.Operations.FirstOrDefault(o => o.Status == OperationStatus.Active && (o.LeaderId == t.Id || o.PartnerId == t.Id));
        if (operation is not null && !sellForCash)
        {
            int i = operation.StockIndex; var signal = signals[i]; long value = (long)t.Shares[i] * stocks[i].Price;
            if (value < equity * signal.StockLimit && (double)(t.GrossAssets(stocks) - t.Cash) / equity < signal.TargetExposure)
            { int price = Math.Clamp((int)(stocks[i].Price * 1.005), 100, 10_000_000); int q = Math.Min(MaxBuy(t, i, price), Math.Min(2, (int)((equity * signal.StockLimit - value) / price))); if (q > 0) SubmitOrder(t.Id, i, true, price, q, 1); }
            t.Decision = "대표 공동 작전 참여 · 적발/벌금 위험";
        }
    }
    static int Quote(double price, bool buy)
    { int tick = price < 1000 ? 1 : 10; return Math.Clamp((int)(buy ? Math.Ceiling(price / tick) : Math.Floor(price / tick)) * tick, 100, 10_000_000); }
}
