using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Chronicles.Game;
using Chronicles.Game.Tests;

internal static class PerformanceScenarios
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static void Run(string path)
    {
        var reports = new List<object>();
        foreach (var size in SyntheticPopulationFixture.Sizes)
        {
            var initialHash = Warmup(size);
            var measured = GameWorld.Restore(SyntheticPopulationFixture.Build(size));
            if (measured.StateHash() != initialHash) throw new InvalidOperationException("Independent synthetic fixture builds must match before warmup.");
            var samples = new List<double>();
            var allocation = GC.GetTotalAllocatedBytes(); var sw = new Stopwatch();
            for (var month = 1; month <= 12; month++)
            {
                sw.Restart(); measured.AdvanceTo(month); sw.Stop(); samples.Add(sw.Elapsed.TotalMilliseconds);
            }
            allocation = GC.GetTotalAllocatedBytes() - allocation;
            var final = measured.Checkpoint(); Invariants.Validate(final);
            sw.Restart(); var view = measured.View(); sw.Stop(); var queryMs = sw.Elapsed.TotalMilliseconds;
            sw.Restart(); var bytes = GameCodec.Encode(measured); sw.Stop(); var saveMs = sw.Elapsed.TotalMilliseconds;
            sw.Restart(); var restored = GameCodec.Decode(bytes); sw.Stop(); var loadMs = sw.Elapsed.TotalMilliseconds;
            if (restored.StateHash() != measured.StateHash()) throw new InvalidOperationException("성능 fixture 왕복 hash");
            var sorted = samples.Order().ToArray();
            reports.Add(new { population = size, fixture = SyntheticPopulationFixture.Describe(size), usedLaws = final.Laws,
                advance12MonthsMs = samples.Sum(), monthlyP50Ms = sorted[6], monthlyP95Ms = sorted[11],
                queryMs, saveMs, loadMs, allocatedBytes = allocation, saveBytes = bytes.Length,
                observedPeople = view.People.Count, living = final.People.Count(p => p.Status == LifeStatus.Alive),
                workload = Workload(final), initialHash, finalHash = measured.StateHash() });
            Console.WriteLine($"PERF {size}: advance 12mo={samples.Sum():F1}ms, query={queryMs:F1}ms, save={saveMs:F1}ms");
        }
        File.WriteAllText(path, JsonSerializer.Serialize(new { runtime = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription, cpu = Environment.ProcessorCount, reports,
            note = "A 12-month synthetic cost measurement, not a 60-second real-time 100x soak or an Android device measurement." },
            Options));
    }

    public static void Soak(string path)
    {
        var world = GameWorld.Restore(SyntheticPopulationFixture.Build(10000));
        var watch = Stopwatch.StartNew();
        var target = 7200L;
        while (world.Month < target && watch.Elapsed.TotalSeconds < 60)
        {
            world.AdvanceTo(world.Month + 1);
            if (world.Month % 1000 == 0) Console.WriteLine($"SOAK {world.Month}/{target} months in {watch.Elapsed.TotalSeconds:F2}s");
        }
        watch.Stop(); var state = world.Checkpoint(); Invariants.Validate(state);
        var living = state.People.Count(p => p.Status == LifeStatus.Alive);
        var report = new { population = 10000, living, elapsedSeconds = watch.Elapsed.TotalSeconds,
            targetMonths = target, completedMonths = world.Month, targetSpeed = 100,
            achievedSpeed = world.Month / 12d * 10 / watch.Elapsed.TotalSeconds,
            passed = world.Month == target && living == 10000 && watch.Elapsed.TotalSeconds <= 60,
            fixture = SyntheticPopulationFixture.Describe(10000), usedLaws = state.Laws, workload = Workload(state),
            finalHash = world.StateHash(), runtime = RuntimeInformation.FrameworkDescription, cpu = Environment.ProcessorCount };
        File.WriteAllText(path, JsonSerializer.Serialize(report, Options));
        Console.WriteLine(JsonSerializer.Serialize(report, Options));
    }

    private static string Warmup(int population)
    {
        var warm = GameWorld.Restore(SyntheticPopulationFixture.Build(population));
        var initialHash = warm.StateHash();
        warm.AdvanceTo(2);
        return initialHash;
    }

    private static object Workload(GameState state) => new
    {
        peopleWithDecisions = state.People.Count(p => p.Decisions.Count > 0),
        currentActions = state.People.Where(p => p.Status == LifeStatus.Alive).GroupBy(p => p.Action)
            .OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count(), StringComparer.Ordinal),
        completedKnowledgeActivities = state.Knowledge.Activities.Count(a => a.Status == Chronicles.Game.Knowledge.KnowledgeActivityStatus.Completed),
        completedPhysicalJobs = state.Inventory.Jobs.Count(j => j.Status == "completed"),
        actualExtraction = state.Materials.Values.Sum(m => m.Extraction), actualProduction = state.Materials.Values.Sum(m => m.Production)
    };
}
