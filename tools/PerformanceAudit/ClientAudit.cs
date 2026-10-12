using AlphaExchange.Client;
using AlphaExchange.Core;
using System.Diagnostics;
using System.Text.Json;

static class ClientAudit
{
    sealed record LegacyRankRow(Trader Trader, double Value, int Rank);

    public static void Run(string output)
    {
        var game = new GameEngine(20261004);
        for (int i = 0; i < 168; i++) game.AdvanceHour();
        double[] points = Enumerable.Range(0, 500).Select(i => Math.Sin(i * .07) * 150).ToArray();
        var buffer = new ChartSeriesBuffer();
        double sink = 0;
        var results = new Dictionary<string, object>();

        void Measure(string name, Action action)
        {
            // New generic sort/buffer paths need time for tiered JIT, just like
            // framework paths already warmed by the engine. Do not time tier 0.
            long warmup = Stopwatch.GetTimestamp();
            do { action(); } while (Stopwatch.GetElapsedTime(warmup).TotalMilliseconds < 500);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            var samples = new List<double>();
            var allocations = new List<long>();
            for (int i = 0; i < 11; i++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
                action();
                samples.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                allocations.Add(GC.GetAllocatedBytesForCurrentThread() - before);
            }
            var ordered = samples.Order().ToArray();
            results[name] = new
            {
                medianMs = ordered[5],
                maximumMs = ordered[^1],
                allocatedMedianBytes = allocations.Order().ElementAt(5),
                samples
            };
        }

        // These reference algorithms reproduce v1.8 hot paths only in this
        // measurement tool; the app has one Core ranking implementation.
        Measure("referenceChart1000", () =>
        {
            for (int i = 0; i < 1000; i++)
            {
                var values = ((IEnumerable<double>)points).ToArray();
                double low = values.Min(), high = values.Max();
                if (Math.Abs(high - low) < 1) { low -= 1; high += 1; }
                double padding = (high - low) * .15;
                sink = low - padding + high + padding + values.Length;
            }
        });
        Measure("bufferChart1000", () =>
        {
            for (int i = 0; i < 1000; i++)
            {
                buffer.Capture(points);
                sink = buffer.Low + buffer.High + buffer.Count;
            }
        });
        Measure("referenceUiRankings100", () =>
        {
            for (int i = 0; i < 100; i++)
            {
                var rows = game.State.Bots.Select(t => (Trader: t, Value: t.ReturnIndex / Math.Max(1e-12, t.SeasonSnapshot.ReturnIndex) - 1))
                    .OrderByDescending(x => x.Value).ThenBy(x => x.Trader.Id)
                    .Select((x, index) => new LegacyRankRow(x.Trader, x.Value, index + 1)).ToList();
                sink = rows[0].Value;
            }
        });
        Measure("coreUiRankings100", () =>
        {
            for (int i = 0; i < 100; i++) sink = game.RankingRows()[0].Value;
        });
        Measure("referenceCoreRankings100", () =>
        {
            for (int i = 0; i < 100; i++)
            {
                var rows = game.State.Bots.OrderByDescending(t =>
                {
                    var financials = t.Financials(game.State.Stocks);
                    var snapshot = new TraderSnapshot
                    {
                        Id = t.Id,
                        Generation = t.Generation,
                        Equity = financials.Equity,
                        Cash = t.Cash,
                        NetIncome = financials.NetIncome - t.NetContribution,
                        ReturnIndex = t.ReturnIndex,
                        NetContribution = t.NetContribution,
                        Volume = t.TradedVolume,
                        Turnover = t.TradedTurnover
                    };
                    return snapshot.ReturnIndex / Math.Max(1e-12, t.SeasonSnapshot.ReturnIndex) - 1;
                }).ThenBy(t => t.Id).ToList();
                sink = rows[0].Id;
            }
        });
        Measure("coreRankings100", () =>
        {
            for (int i = 0; i < 100; i++) sink = game.Ranking()[0].Id;
        });
        if (!double.IsFinite(sink)) throw new InvalidOperationException("Invalid measurement.");
        var report = new
        {
            label = "portable-client",
            referenceCommit = "7e81f9a8327eeef21734fc7c737f46cf6ab1ba0f",
            framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            processorCount = Environment.ProcessorCount,
            chartPoints = points.Length,
            results,
            limitations = "Algorithm CPU/managed allocation comparison on Linux. Excludes Android Canvas, native memory, physical FPS and battery."
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Saved " + output);
    }
}
