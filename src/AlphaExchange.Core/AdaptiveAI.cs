namespace AlphaExchange.Core;

public readonly record struct InvestmentSignal(double Score, double TargetExposure, double StockLimit, double SectorLimit, bool Defensive, double Volatility);

public sealed partial class GameEngine
{
    double[] trends = [], fastTrends = [], volatilities = [], dividendYields=[];
    InvestmentSignal[] institutionSignals=[];
    double marketTrend;
    internal void PrepareMarketSignals()
    {
        if (trends.Length != State.Stocks.Count)
        { trends = new double[State.Stocks.Count]; fastTrends = new double[State.Stocks.Count]; volatilities = new double[State.Stocks.Count]; dividendYields=new double[State.Stocks.Count]; institutionSignals=new InvestmentSignal[State.Stocks.Count]; }
        marketTrend = 0;
        for (int i = 0; i < State.Stocks.Count; i++)
        {
            var s = State.Stocks[i]; int n = s.History.Count; dividendYields[i]=s.DividendYield;
            trends[i] = (double)s.PreviousPrice / s.History[Math.Max(0, n - 24)] - 1;
            fastTrends[i] = (double)s.PreviousPrice / s.History[Math.Max(0, n - 3)] - 1;
            double sum = 0; int count = 0;
            for (int j = Math.Max(1, n - 12); j < n; j++) { sum += Math.Abs((double)s.History[j] / s.History[j - 1] - 1); count++; }
            volatilities[i] = s.Volatility = count == 0 ? 0 : sum / count; marketTrend += trends[i] / State.Stocks.Count;
        }
    }
    public InvestmentSignal Analyze(Trader t, int index)
    {
        if(t.IsRetail || t.Id==0) throw new ArgumentException("기관 전용 판단입니다.",nameof(t));
        if (index < 0 || index >= State.Stocks.Count) throw new ArgumentOutOfRangeException(nameof(index));
        PrepareMarketSignals();
        return Analyze(t,index,t.Equity(State.Stocks));
    }
    InvestmentSignal Analyze(Trader t,int index,long equity)
    {
        var s = State.Stocks[index]; var a = t.Abilities;
        double trend = trends[index], fast = fastTrends[index], volatility = volatilities[index];
        double risk = a.RiskManagement / 100.0, macro = a.Macro / 100.0;
        bool defensive = marketTrend < -.025 || trend < -.05 || volatility > .012 || equity < t.SeasonOpeningEquity * .88;
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
        double cost = State.Government.Policy.FeeBasisPoints / 10000.0 * 2 + .001;
        score += dividendYields[index] * (.02 + a.Valuation * .0003);
        score -= Math.Sign(score) * Math.Min(Math.Abs(score),cost * (1 - a.Execution / 200.0));
        return new InvestmentSignal(score, Math.Clamp(exposure, .06, .75), .20 - a.Diversification * .001,
            .48 - a.Diversification * .002, defensive, volatility);
    }
    void DecideInstitution(Trader t)
    {
        CancelOrders(t.Id); var stocks = State.Stocks;
        if(t.WaitingForCapital) return;
        long equity = Math.Max(1, t.Equity(stocks));
        var signals = institutionSignals; bool defensive=false; double bestScore=double.MinValue;
        for (int i = 0; i < stocks.Count; i++)
        { signals[i] = Analyze(t,i,equity); if(stocks[i].Active) { defensive|=signals[i].Defensive; bestScore=Math.Max(bestScore,signals[i].Score); } }
        t.Decision = defensive ? "하락·변동성 대응: 현금 확보 / 부채 축소 / 헤지" : "가치·추세·뉴스·단타·초단타 혼합 / 분산 비중 관리";
        var offer = LoanTerms(t);
        if (t.LoanDebt > 0 && (defensive || t.LoanDebt > offer.Limit || AvailableCash(t) > equity * .40))
            RepayLoan(t.Id, Math.Max(t.LoanDebt - offer.Limit, (long)(AvailableCash(t) * .15)));
        else if (!defensive && t.CreditScore >= 70 && t.Abilities.Valuation > 60 && bestScore > .025 && t.LoanDebt < equity * .1)
            BorrowCash(t.Id, Math.Min(offer.Available, Math.Max(0, (long)(equity * .08) - t.LoanDebt)));
        bool margin = t.ShortCollateral(stocks) + t.ReservedCash > t.Cash;
        if (margin) { t.MarginCalls++; t.Decision = "담보 부족: 공매도 상환·보유 주식 청산"; }
        bool sellForCash = defensive || margin || t.LoanDebt > offer.Limit;
        int attempts = 2 + (t.Abilities.Execution > 75 ? 1 : 0);
        for (int j = 0; j < attempts; j++)
        {
            int index = (int)(Next() * stocks.Count);
            if (j == 0 && sellForCash)
            {
                long largest=-1;
                for(int i=0;i<stocks.Count;i++) if(stocks[i].Active && (long)t.Shares[i]*stocks[i].Price>largest)
                { index=i; largest=(long)t.Shares[i]*stocks[i].Price; }
            }
            var s = stocks[index]; if (!s.Active) continue; var signal = signals[index];
            double longExposure = (double)(t.GrossAssets(stocks) - t.Cash) / equity;
            double weight = (double)t.Shares[index] * s.Price / equity;
            double sectorValue=0;
            for(int i=0;i<stocks.Count;i++) if(stocks[i].Sector==s.Sector) sectorValue+=(double)t.Shares[i]*stocks[i].Price;
            double sectorWeight=sectorValue/equity;
            int desired = Math.Max(1, (int)(equity * (.003 + t.Abilities.Execution * .000035) / s.Price));
            double spread = .003 + (100 - t.Abilities.Execution) * .00004;
            double noise = (Next() - .5) * .006 * (1 - t.Abilities.Technical / 130.0);
            double offset = Math.Clamp(signal.Score * .12 + noise, -.018, .018);
            bool exitShort = t.ShortShares[index] > 0 && (margin || !State.Government.Policy.ShortSellingAllowed || signal.Score > .004 ||
                (double)s.Price / Math.Max(1, t.ShortAveragePrice[index]) > 1.06 || (double)s.Price / Math.Max(1, t.ShortAveragePrice[index]) < .94);
            if (exitShort)
            {
                int price = Quote(s.PreviousPrice * (1 + spread), true);
                int q = (int)Math.Min(t.ShortShares[index], Math.Max(desired, margin ? Math.Min(1_000_000, t.ShortShares[index]) : 1));
                while (q > 0 && SubmitOrder(t.Id, index, true, price, q, 1, cover: true) is not null) q /= 2;
                continue;
            }
            bool overweight = weight >= signal.StockLimit || sectorWeight >= signal.SectorLimit;
            bool buy = !margin && !overweight && longExposure < signal.TargetExposure &&
                (signal.Score>.002 || signal.Score>-.02 && Next()<.12);
            if (buy)
            {
                double budget = Math.Min(signal.StockLimit - weight, signal.SectorLimit - sectorWeight);
                budget = Math.Min(budget, signal.TargetExposure - longExposure);
                // Seek liquidity only for a strong discount; otherwise let reactive
                // orders come to a bid that compensates spread, fees and uncertainty.
                int price = Quote(s.PreviousPrice * (1 + offset + (signal.Score>.04 ? spread : -spread)), true);
                int q = Math.Min(desired, Math.Min(MaxBuy(t, index, price), Math.Max(0, (int)(equity * budget / price))));
                if (q > 0 && s.FundedIpo && s.TotalShares < 2000 && State.Season <= s.ListedSeason + 2 && asks[index].Count == 0)
                    SubscribeIssue(t.Id,s.SecurityId,Math.Min(2,q));
                else if (q > 0) SubmitOrder(t.Id, index, true, price, q, t.Abilities.Scalping > 70 ? 1 : 3);
            }
            else if (t.Shares[index] > 0 && (sellForCash || overweight || signal.Score < .004 || Next() < .3))
            {
                int price = Quote(s.PreviousPrice * (1 + offset + (sellForCash || signal.Score<-.04 ? -spread : spread)), false);
                int q = (int)Math.Min(t.Shares[index] - t.ReservedShares[index], desired * (sellForCash ? 3 : 1));
                if (q > 0) SubmitOrder(t.Id, index, false, price, q, 2);
            }
            else if (signal.Score < -.025 && trends[index]<-.015 && fastTrends[index]<=0 &&
                t.Abilities.RiskManagement > 45 && t.Abilities.Technical > 45 && t.ShortLiability(stocks) < equity * .06)
            {
                int price = Quote(s.PreviousPrice * (1 - spread), false);
                SubmitOrder(t.Id, index, false, price, desired, 1, shortSale: true);
            }
        }
        if (margin)
            for(int i=0;i<stocks.Count;i++) if(t.ShortShares[i]>0 && t.ReservedCovers[i]==0)
            { int price = Math.Clamp((int)(stocks[i].Price * 1.015), 1, 10_000_000); int q = (int)Math.Min(1_000_000, t.ShortShares[i]); while (q > 0 && SubmitOrder(t.Id, i, true, price, q, 1, cover: true) is not null) q /= 2; }
        var operation = State.Operations.FirstOrDefault(o => o.Status == OperationStatus.Active && (o.LeaderId == t.Id || o.PartnerId == t.Id));
        if (operation is not null && !sellForCash)
        {
            int i = operation.StockIndex; var signal = signals[i]; long value = (long)t.Shares[i] * stocks[i].Price;
            if (value < equity * signal.StockLimit && (double)(t.GrossAssets(stocks) - t.Cash) / equity < signal.TargetExposure)
            { int price = Math.Clamp((int)(stocks[i].Price * 1.005), 1, 10_000_000); int q = Math.Min(MaxBuy(t, i, price), Math.Min(2, (int)((equity * signal.StockLimit - value) / price))); if (q > 0) SubmitOrder(t.Id, i, true, price, q, 1); }
            t.Decision = "대표 공동 작전 참여 · 적발/벌금 위험";
        }
    }
    static int Quote(double price, bool buy)
    { int tick = price < 1000 ? 1 : 10; return Math.Clamp((int)(buy ? Math.Ceiling(price / tick) : Math.Floor(price / tick)) * tick, 1, 10_000_000); }
}
