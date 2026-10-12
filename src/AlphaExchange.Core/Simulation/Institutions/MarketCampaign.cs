namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    public static readonly string[] CampaignNames = ["저가 매집", "추세 추종 유도", "개미 수요에 분산 매도", "손실 제한·철수"];

    void ReviewCampaign(Trader trader, long equity)
    {
        var d = trader.Development!;
        d.PeakEquity = Math.Max(d.PeakEquity, equity);
        d.MaximumDrawdown = Math.Max(d.MaximumDrawdown, d.PeakEquity > 0 ? 1 - (double)equity / d.PeakEquity : 0);
        bool choose = d.CampaignIndex < 0 || !State.Stocks[d.CampaignIndex].Active;
        long age = State.CompletedMinutes - d.CampaignSinceMinute;
        if (!choose)
        {
            var stock = State.Stocks[d.CampaignIndex];
            double move = (double)stock.Price / Math.Max(1, d.CampaignReferencePrice) - 1;
            double crowd = RetailPressure(d.CampaignIndex);
            var next = d.CampaignPhase;
            if (next == CampaignPhase.Retreat && age >= 360) choose = true;
            else if (move < -.08 || equity < d.PeakEquity * .8) next = CampaignPhase.Retreat;
            else if (next == CampaignPhase.Accumulate && age >= 480 + trader.Id % 7 * 30) next = CampaignPhase.AttractFollowers;
            else if (next == CampaignPhase.AttractFollowers && (move > .035 || crowd > .75 || age >= 720)) next = CampaignPhase.Distribute;
            else if (next == CampaignPhase.Distribute && (trader.Shares[d.CampaignIndex] == 0 || crowd < -.15 || age >= 480)) next = CampaignPhase.Retreat;
            if (next != d.CampaignPhase)
            {
                d.CampaignPhase = next;
                d.CampaignSinceMinute = State.CompletedMinutes;
            }
        }
        if (!choose) return;
        int start = (int)(Next() * StockCount), selected = -1;
        double best = double.MinValue;
        for (int n = 0; n < StockCount; n++)
        {
            int i = (start + n) % StockCount; var stock = State.Stocks[i];
            if (!stock.Active || stock.Report.Equity <= 0) continue;
            double crowd = RetailPressure(i);
            double score = Math.Clamp(stock.FairValue / stock.Price - 1, -.5, .5) * .4 +
                Math.Max(0, crowd) * .12 + Math.Abs(stock.Sentiment) * .5 - Math.Abs(crowd) * .08 +
                (i % 5 == trader.Id % 5 ? .025 : 0);
            if (score > best) { best = score; selected = i; }
        }
        d.CampaignIndex = selected;
        d.CampaignPhase = CampaignPhase.Accumulate;
        d.CampaignSinceMinute = State.CompletedMinutes;
        d.CampaignReferencePrice = selected < 0 ? 1 : State.Stocks[selected].Price;
    }

    internal double BookImbalance(int index)
    {
        long buy = 0, sell = 0;
        for (int n = 0; n < Math.Min(5, bids[index].Count); n++) buy += bids[index][n].Remaining;
        for (int n = 0; n < Math.Min(5, asks[index].Count); n++) sell += asks[index][n].Remaining;
        return (double)(buy - sell) / Math.Max(1, buy + sell);
    }

    double SpreadRatio(int index) => bids[index].Count > 0 && asks[index].Count > 0
        ? Math.Max(0, asks[index][0].Price - bids[index][0].Price) / (double)State.Stocks[index].Price : .01;
}
