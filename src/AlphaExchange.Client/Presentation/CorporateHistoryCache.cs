using AlphaExchange.Core;

namespace AlphaExchange.Client;

internal sealed class CorporateHistoryCache(GameStore store)
{
    GameState? state;
    long nextEvent;
    readonly Dictionary<(string?, long, int), IReadOnlyList<CorporateEvent>> records = [];

    public IReadOnlyList<CorporateEvent> Read(GameState current, string? securityId = null,
        long beforeId = long.MaxValue, int maximum = 60)
    {
        if (!ReferenceEquals(state, current) || nextEvent != current.NextCorporateEventId)
        {
            records.Clear();
            state = current;
            nextEvent = current.NextCorporateEventId;
        }
        var key = (securityId, beforeId, maximum);
        if (records.TryGetValue(key, out var cached)) return cached;
        var result = store.ReadEvents(current, securityId, beforeId, maximum);
        if (records.Count >= 8) records.Clear();
        records[key] = result;
        return result;
    }
}
