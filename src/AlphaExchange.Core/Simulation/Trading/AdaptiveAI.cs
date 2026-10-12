namespace AlphaExchange.Core;

public readonly record struct InvestmentSignal(double Score, double TargetExposure, double StockLimit, double SectorLimit,
    bool Defensive, double Volatility, InvestmentHorizon Horizon = InvestmentHorizon.Short);

public sealed partial class GameEngine
{
    double[] trends = [], fastTrends = [], longTrends = [], volatilities = [], dividendYields = [];
    InvestmentSignal[] institutionSignals = [];
    int[] signalSectors = [];
    double[] portfolioSectors = [];
    string[] sectorNames = [];
    readonly Dictionary<string, int> sectorIds = new(StringComparer.Ordinal);
    readonly Dictionary<string, int> decisionSecurities = new(StringComparer.Ordinal);
    InvestmentPlan?[] decisionPlans = [];
    double marketTrend;
    internal void PrepareMarketSignals()
    {
        int count = State.Stocks.Count;
        if (trends.Length != count)
        {
            trends = new double[count]; fastTrends = new double[count]; longTrends = new double[count]; volatilities = new double[count];
            dividendYields = new double[count]; institutionSignals = new InvestmentSignal[count]; signalSectors = new int[count];
            portfolioSectors = new double[count];
        }
        bool changed = sectorNames.Length != count;
        if (!changed) for (int i = 0; i < count; i++) if (sectorNames[i] != State.Stocks[i].Sector) { changed = true; break; }
        if (changed)
        {
            sectorNames = new string[count]; sectorIds.Clear();
            for (int i = 0; i < count; i++)
            {
                string name = sectorNames[i] = State.Stocks[i].Sector;
                if (!sectorIds.TryGetValue(name, out int sector)) sectorIds.Add(name, sector = sectorIds.Count);
                signalSectors[i] = sector;
            }
        }
        var tenDays = State.DailyHistory.LastOrDefault(p => p.Hour <= State.CompletedHours - Rules.Horizons[2].MinimumHours);
        marketTrend = 0; int active = 0;
        for (int i = 0; i < count; i++)
        {
            var s = State.Stocks[i]; int n = s.History.Count; dividendYields[i] = s.DividendYield;
            trends[i] = (double)s.Price / s.History[Math.Max(0, n - 24)] - 1;
            fastTrends[i] = (double)s.Price / s.History[Math.Max(0, n - 3)] - 1;
            longTrends[i] = trends[i];
            if (tenDays is not null && i < tenDays.SecurityIds.Length && i < tenDays.SplitFactors.Length && tenDays.SplitFactors[i] > 0 && tenDays.SecurityIds[i] == s.SecurityId)
                longTrends[i] = (double)s.Price / (tenDays.StockPrices[i] * tenDays.SplitFactors[i] / s.SplitFactor) - 1;
            double sum = 0; int samples = 0;
            for (int j = Math.Max(1, n - 12); j < n; j++) { sum += Math.Abs(s.History[j] / s.History[j - 1] - 1); samples++; }
            volatilities[i] = s.Volatility = samples == 0 ? 0 : sum / samples;
            if (s.Active) { marketTrend += trends[i]; active++; }
        }
        marketTrend /= Math.Max(1, active);
    }
    public InvestmentSignal Analyze(Trader t, int index)
    {
        if (t.IsRetail || t.Id == 0) throw new ArgumentException("기관 전용 판단입니다.", nameof(t));
        if (index < 0 || index >= State.Stocks.Count) throw new ArgumentOutOfRangeException(nameof(index));
        PrepareMarketSignals(); return Analyze(t, index, t.Equity(State.Stocks), Capabilities(t).Effective);
    }
    InvestmentSignal Analyze(Trader t, int index, long equity, Abilities a)
    {
        var s = State.Stocks[index]; var r = Rules.Investment;
        double risk = a.RiskManagement / 100.0, macro = a.Macro / 100.0;
        bool defensive = marketTrend < r.FallingMarket || trends[index] < r.FallingStock || volatilities[index] > r.VolatilityRisk ||
            equity < t.SeasonOpeningEquity * (1 - r.DrawdownRisk);
        double cash = r.BaseCash + r.DispositionCash[(int)t.Disposition] - t.Risk * r.RiskCashEffect + (defensive ? r.DefensiveCash * risk : 0);
        cash = Math.Clamp(cash, r.MinimumCash, r.MaximumCash);
        double value = Math.Clamp(s.FairValue / s.Price - 1, -1, 2);
        InvestmentHorizon horizon = value > r.LongDiscount && s.Report.NetIncome > 0
            ? InvestmentHorizon.Long : Math.Abs(fastTrends[index]) >= r.FastMove &&
                (a.Scalping > a.SwingTrading || t.Disposition is Disposition.Opportunistic or Disposition.Aggressive)
                ? InvestmentHorizon.UltraShort : InvestmentHorizon.Short;
        var d = t.Development!;
        if ((t.Shares[index] > 0 || t.ShortShares[index] > 0) && d.NextReviewHours[index] > State.CompletedHours &&
            Math.Abs(s.Sentiment) < r.MaterialNews && !defensive && value >= r.ThesisBreakValue)
            horizon = d.PositionHorizons[index];
        var method = Rules.Horizons[(int)horizon];
        double Skill(int skill) => r.SkillFloor + (1 - r.SkillFloor) * skill / 100.0;
        double trend = horizon == InvestmentHorizon.Long ? longTrends[index] : trends[index];
        double score = value * method.ValueWeight * Skill(a.Valuation) + trend * method.TrendWeight * Skill(a.Technical) +
            fastTrends[index] * method.FastWeight * Skill(method.FastWeight < 0 ? a.SwingTrading : a.Scalping) +
            s.Sentiment * method.NewsWeight * Skill(a.News) + dividendYields[index] * method.DividendWeight * Skill(a.Valuation);
        score -= volatilities[index] * risk + (s.Report.DebtRatio * State.Government.Policy.BaseRate) * macro;
        foreach (var report in t.Development!.AnalystReports)
                if (report.OwnerId == t.Id && report.Sector == s.Sector && report.ExpiresMinute > State.CompletedMinutes)
                {
                    score = score * (1 - MarketRules.AnalystWeight) + report.ExpectedReturn * report.Confidence * MarketRules.AnalystWeight;
                    break;
                }
        double spread = bids[index].Count > 0 && asks[index].Count > 0 ?
            Math.Max(0, (asks[index][0].Price - bids[index][0].Price) / (double)s.Price) : r.BaseSpread * 2;
        double imbalance = BookImbalance(index);
        double crowd = RetailPressure(index);
        double style = t.Strategy switch
        {
            Strategy.Momentum => fastTrends[index] * .25 + crowd * .025,
            Strategy.Value => value * .1 - crowd * .012,
            Strategy.Contrarian => -fastTrends[index] * .2 - crowd * .025,
            Strategy.News => s.Sentiment * .6,
            Strategy.Explorer => imbalance * .03 + fastTrends[index] * .12,
            _ => value * .04 - volatilities[index] * .5
        };
        score += style + imbalance * .012;
        double roundTrip = State.Government.Policy.FeeBasisPoints / 10000.0 * 2 + spread;
        // A short holding must clear more trading friction per game day; long
        // positions amortize it, and dividends remain part of their thesis.
        double cost = roundTrip * 24 / Math.Max(24, method.ExpectedHours);
        score -= Math.Sign(score) * Math.Min(Math.Abs(score), cost * Skill(100 - a.Execution));
        return new(score, 1 - cash, r.StockLimit - a.Diversification / 100.0 * r.DiversificationStockEffect,
            r.SectorLimit - a.Diversification / 100.0 * r.DiversificationSectorEffect, defensive, volatilities[index], horizon);
    }
    static int Quote(double price, bool buy)
    {
        int tick = price < 1000 ? 1 : 10;
        return Math.Clamp((int)(buy ? Math.Ceiling(price / tick) : Math.Floor(price / tick)) * tick, 1, 10_000_000);
    }
}
