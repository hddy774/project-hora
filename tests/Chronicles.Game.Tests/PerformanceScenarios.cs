using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Chronicles.Game;

internal static class PerformanceScenarios
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static void Run(string path)
    {
        var reports = new List<object>();
        foreach (var size in new[] { 2, 100, 1000, 10000 })
        {
            var fixture = Fixture(size);
            var warm = GameWorld.Restore(fixture); warm.AdvanceTo(2);
            var measured = GameWorld.Restore(fixture);
            var samples = new List<double>();
            var allocation = GC.GetTotalAllocatedBytes(); var sw = new Stopwatch();
            for (var month = 1; month <= 12; month++)
            {
                sw.Restart(); measured.AdvanceTo(month); sw.Stop(); samples.Add(sw.Elapsed.TotalMilliseconds);
            }
            allocation = GC.GetTotalAllocatedBytes() - allocation;
            Invariants.Validate(measured.Checkpoint());
            sw.Restart(); var view = measured.View(); sw.Stop(); var queryMs = sw.Elapsed.TotalMilliseconds;
            sw.Restart(); var bytes = GameCodec.Encode(measured); sw.Stop(); var saveMs = sw.Elapsed.TotalMilliseconds;
            sw.Restart(); var restored = GameCodec.Decode(bytes); sw.Stop(); var loadMs = sw.Elapsed.TotalMilliseconds;
            if (restored.StateHash() != measured.StateHash()) throw new InvalidOperationException("성능 fixture 왕복 hash");
            var sorted = samples.Order().ToArray();
            reports.Add(new { population = size, fixture = "synthetic, evenly distributed; foodUse=0, births=0, disasters=0; not founder history",
                advance12MonthsMs = samples.Sum(), monthlyP50Ms = sorted[6], monthlyP95Ms = sorted[11],
                queryMs, saveMs, loadMs, allocatedBytes = allocation, saveBytes = bytes.Length,
                observedPeople = view.People.Count, finalHash = measured.StateHash() });
            Console.WriteLine($"PERF {size}: advance 12mo={samples.Sum():F1}ms, query={queryMs:F1}ms, save={saveMs:F1}ms");
        }
        File.WriteAllText(path, JsonSerializer.Serialize(new { runtime = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription, cpu = Environment.ProcessorCount, reports,
            note = "A 12-month synthetic cost measurement, not a 60-second real-time 100x soak or an Android device measurement." },
            Options));
    }

    private static GameState Fixture(int size)
    {
        var s = GameWorld.Create(42, new Dictionary<string, long> { ["birth.enabled"] = 0,
            ["ecology.foodUse"] = 0, ["ecology.disasterChance"] = 0,
            ["ecology.sourceCapacity"] = 1000000, ["ecology.regrowth"] = 10000 }).Checkpoint();
        for (var n = 2; n < size; n++)
        {
            var p = new Person { Id = s.NextId++, BodyId = s.NextId++, SoulId = s.NextId++,
                Name = $"합성 인물 {n}", Born = -216, Lifespan = 1200000, Place = n % 100,
                Gestates = n % 2 == 0, Realm = Realm.Qi,
                Aptitudes = Enumerable.Repeat(5000, 6).ToArray(), Roots = Enumerable.Repeat(5000, 5).ToArray(),
                Personality = Enumerable.Repeat(50, 14).ToArray(), KnownPlaces = [n % 100] };
            p.Understanding["entry"] = 10000;
            s.People.Add(p); s.Souls.Add(new Soul { Id = p.SoulId, Root = p.SoulId, CurrentPerson = p.Id, Evidence = "합성 성능 fixture" });
        }
        return s;
    }
}
