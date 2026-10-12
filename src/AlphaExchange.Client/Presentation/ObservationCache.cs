using AlphaExchange.Core;

namespace AlphaExchange.Client;

// Accessed under the host's simulation gate. Core owns the calculations;
// this cache owns only bounded display data.
internal sealed class ObservationCache
{
    readonly record struct Revision(GameState State, long Minute, long Transaction, long Event, long Book);
    Revision revision;
    readonly Dictionary<(RankingMetric, ComparisonPeriod, long, long), List<RankedTrader>> ranks = [];
    readonly Dictionary<(int, bool, int), List<BookLevel>> books = [];

    public void Clear()
    {
        revision = default;
        ranks.Clear();
        books.Clear();
    }

    void Refresh(GameEngine game)
    {
        var state = game.State;
        var next = new Revision(state, state.CompletedMinutes, state.NextTransactionId,
            state.NextCorporateEventId, game.BookRevision);
        if (revision == next) return;
        Clear();
        revision = next;
    }

    public List<RankedTrader> Ranking(GameEngine game, RankingMetric metric, ComparisonPeriod period)
    {
        Refresh(game);
        var key = (metric, period, game.State.ComparisonFrom, game.State.ComparisonTo);
        if (ranks.TryGetValue(key, out var cached)) return cached;
        var result = game.RankingRows(metric, period);
        if (ranks.Count >= 12) ranks.Clear();
        ranks[key] = result;
        return result;
    }

    public List<BookLevel> Depth(GameEngine game, int index, bool buy, int levels)
    {
        Refresh(game);
        var key = (index, buy, levels);
        if (books.TryGetValue(key, out var cached)) return cached;
        var result = game.Depth(index, buy, levels);
        if (books.Count >= 96) books.Clear();
        books[key] = result;
        return result;
    }
}
