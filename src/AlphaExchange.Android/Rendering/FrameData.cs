using AlphaExchange.Client;
using AlphaExchange.Core;

namespace AlphaExchange.App;

public sealed partial class GameView
{
    readonly ObservationCache frameData = new();

    List<RankedTrader> FrameRanking(RankingMetric metric = RankingMetric.Return,
        ComparisonPeriod period = ComparisonPeriod.Season) => frameData.Ranking(game!, metric, period);

    int FrameRankOf(int id) => FrameRanking().Find(row => row.Trader.Id == id).Rank;

    List<BookLevel> FrameDepth(int index, bool buy, int levels) => frameData.Depth(game!, index, buy, levels);
}
