namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    // A completed long/short position is one investment. Partial fills accumulate;
    // dividends, financing expenses and forced corporate exits are reported elsewhere.
    void TrackInvestment(Trader trader, int index, bool shortPosition, double profit, long fee, long tax, bool closed)
    {
        if (trader.Development is not { } d) return;
        var tracked = shortPosition ? d.TrackedShortInvestments : d.TrackedInvestments;
        var net = shortPosition ? d.ShortInvestmentNet : d.InvestmentNet;
        if (tracked[index]) net[index] += (decimal)profit - fee - Math.Max(0, tax);
        if (!closed) return;
        if (tracked[index])
        {
            d.CompletedInvestments++;
            if (net[index] > 0) d.SuccessfulInvestments++;
        }
        if (!shortPosition) d.LastExitMinutes[index] = State.CompletedMinutes;
        tracked[index] = false;
        net[index] = 0;
    }

    static void ExcludeCorporateInvestment(Trader trader, int index)
    {
        if (trader.Development is not { } d) return;
        d.TrackedInvestments[index] = d.TrackedShortInvestments[index] = false;
        d.InvestmentNet[index] = d.ShortInvestmentNet[index] = 0;
    }
}
