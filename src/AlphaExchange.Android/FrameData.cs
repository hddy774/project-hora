using AlphaExchange.Core;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    readonly record struct FrameKey(AlphaExchange.Core.GameState State,long Hour,long Transaction,long Event,long Book);
    sealed record RankRow(Trader Trader,double Value,int Rank);
    FrameKey dataKey;
    readonly Dictionary<(RankingMetric Metric,ComparisonPeriod Period,long From,long To),List<RankRow>> frameRanks=[];
    readonly Dictionary<(int Index,bool Buy,int Levels),List<BookLevel>> frameBooks=[];
    void RefreshFrameData()
    {
        var key=new FrameKey(S,S.CompletedHours,S.NextTransactionId,S.NextCorporateEventId,game!.BookRevision);
        if(dataKey==key) return;
        dataKey=key; frameRanks.Clear(); frameBooks.Clear();
    }
    List<RankRow> FrameRanking(RankingMetric metric=RankingMetric.Return,ComparisonPeriod period=ComparisonPeriod.Season)
    {
        RefreshFrameData(); var key=(metric,period,S.ComparisonFrom,S.ComparisonTo);
        if(frameRanks.TryGetValue(key,out var rows)) return rows;
        var range=period is ComparisonPeriod.All or ComparisonPeriod.Season ? null : game!.PeriodSummary(period);
        double Value(Trader t)
        {
            if(metric==RankingMetric.Cash) return t.Cash;
            if(metric==RankingMetric.Assets) return t.Equity(S.Stocks);
            if(period is ComparisonPeriod.All or ComparisonPeriod.Season)
            {
                var start=period==ComparisonPeriod.All ? t.OpeningSnapshot : t.SeasonSnapshot;
                return metric switch {
                    RankingMetric.Return=>t.ReturnIndex/Math.Max(1e-12,start.ReturnIndex)-1,
                    RankingMetric.Volume=>t.TradedVolume-start.Volume,
                    RankingMetric.Turnover=>t.TradedTurnover-start.Turnover,
                    _=>t.Financials(S.Stocks).NetIncome-t.NetContribution-start.NetIncome };
            }
            return game!.RankingValue(t,metric,period,range!.Start,period==ComparisonPeriod.Custom ? range.End : null);
        }
        rows=S.Bots.Select(t=>(Trader:t,Value:Value(t))).OrderByDescending(x=>x.Value).ThenBy(x=>x.Trader.Id)
            .Select((x,i)=>new RankRow(x.Trader,x.Value,i+1)).ToList();
        if(frameRanks.Count>12) frameRanks.Clear();
        frameRanks[key]=rows; return rows;
    }
    int FrameRankOf(int id) => FrameRanking().Find(r=>r.Trader.Id==id)!.Rank;
    List<BookLevel> FrameDepth(int index,bool buy,int levels)
    {
        RefreshFrameData(); var key=(index,buy,levels);
        if(frameBooks.TryGetValue(key,out var result)) return result;
        result=game!.Depth(index,buy,levels); frameBooks[key]=result; return result;
    }
}
