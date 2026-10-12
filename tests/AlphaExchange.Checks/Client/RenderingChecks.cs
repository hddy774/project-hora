using AlphaExchange.Client;
using AlphaExchange.Core;

static class RenderingChecks
{
    public static void Run(Action<bool, string> check)
    {
        var buffer = new ChartSeriesBuffer();
        double[][] cases = [[], [12], [7, 7, 7], [-4, 11, 2, -1], [1, 1.01],
            Enumerable.Range(0, 1000).Select(i => Math.Sin(i * .07) * 150).ToArray()];
        foreach (var source in cases)
        {
            var expected = source.Length < 2 ? new[] { source.FirstOrDefault(), source.FirstOrDefault() } : source;
            double low = expected.Min(), high = expected.Max();
            if (Math.Abs(high - low) < 1) { low -= 1; high += 1; }
            double padding = (high - low) * .15;
            buffer.Capture(source);
            check(buffer.Values.SequenceEqual(expected), "Chart preserves endpoints and all samples");
            check(buffer.Low == low - padding && buffer.High == high + padding, "Chart preserves vertical bounds");
        }
        buffer.Capture(cases[^1]);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) buffer.Capture(cases[^1]);
        check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Warmed chart capture allocates no arrays");
        bool rejected = false;
        try { buffer.Capture(Enumerable.Repeat(1.0, ChartSeriesBuffer.MaximumPoints + 1)); }
        catch (InvalidOperationException) { rejected = true; }
        check(rejected, "Unbounded chart input requires explicit downsampling");
        check(PortraitSampling.SampleSize(307, 307, 46 * 2.625f, 46 * 2.625f) == 2,
            "Thumbnail sampling accounts for density");
        check(PortraitSampling.SampleSize(307, 500, 300 * 2.625f, 490 * 2.625f) == 1,
            "Zoom keeps full source resolution");
        check(PortraitSampling.SampleSize(307, 307, 40, 40) == 4, "Small portraits avoid full atlas decode");

        var game = new GameEngine(20261004);
        var cache = new ObservationCache();
        var first = cache.Ranking(game, RankingMetric.Return, ComparisonPeriod.Season);
        check(ReferenceEquals(first, cache.Ranking(game, RankingMetric.Return, ComparisonPeriod.Season)),
            "Unchanged frames reuse rankings");
        game.AdvanceMinute();
        check(!ReferenceEquals(first, cache.Ranking(game, RankingMetric.Return, ComparisonPeriod.Season)),
            "Completed minute invalidates frame rankings");
        first = cache.Ranking(game, RankingMetric.Cash, ComparisonPeriod.Season);
        game.State.Bots[0].Cash += 1_000_000_000;
        cache.Clear();
        check(!ReferenceEquals(first, cache.Ranking(game, RankingMetric.Cash, ComparisonPeriod.Season)),
            "Paused changes explicitly invalidate display caches");
        var book = cache.Depth(game, 0, true, 5);
        check(ReferenceEquals(book, cache.Depth(game, 0, true, 5)), "Unchanged frames reuse depth");
        cache.Clear();
        check(!ReferenceEquals(book, cache.Depth(game, 0, true, 5)), "Explicit invalidation refreshes depth");

        string folder = Path.Combine(Path.GetTempPath(), "hora-history-cache-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new GameStore(folder);
            store.Attach(game);
            var history = new CorporateHistoryCache(store);
            var firstEvents = history.Read(game.State);
            check(ReferenceEquals(firstEvents, history.Read(game.State)), "Unchanged frames reuse corporate history");
            long id = game.State.NextCorporateEventId++;
            game.State.CorporateEvents.Add(new CorporateEvent { Id = id, Hour = game.State.CompletedHours,
                SecurityId = game.State.Stocks[0].SecurityId });
            var refreshed = history.Read(game.State);
            check(refreshed.Any(row => row.Id == id), "New corporate event invalidates cached history");
            check(history.Read(new GameEngine(999).State).All(row => row.Id != id),
                "A new run never reuses another run's cached history");
        }
        finally { Directory.Delete(folder, true); }
    }
}
