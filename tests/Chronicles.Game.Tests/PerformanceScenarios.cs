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

    public static void Soak(string path)
    {
        var world = GameWorld.Restore(Fixture(10000));
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
            passed = world.Month == target && living == 10000,
            fixture = "synthetic; no births, food costs, disasters or hazards; long life overrides; not founder history or Android measurement",
            finalHash = world.StateHash(), runtime = RuntimeInformation.FrameworkDescription, cpu = Environment.ProcessorCount };
        File.WriteAllText(path, JsonSerializer.Serialize(report, Options));
        Console.WriteLine(JsonSerializer.Serialize(report, Options));
    }

    private static GameState Fixture(int size)
    {
        var s = GameWorld.Create(42, new Dictionary<string, long> { ["birth.enabled"] = 0,
            ["ecology.foodUse"] = 0, ["ecology.disasterChance"] = 0,
            ["ecology.sourceCapacity"] = 1000000, ["ecology.regrowth"] = 10000,
            ["life.qi"] = 120000, ["life.foundation"] = 120000, ["life.core"] = 120000,
            ["hazard.structureDamage"] = 0, ["hazard.bondDamage"] = 0, ["hazard.sealChance"] = 0,
            ["hazard.portalChance"] = 0, ["hazard.trialDamage"] = 0, ["cultivation.breakthroughRisk"] = 0 }).Checkpoint();
        for (var n = 2; n < size; n++)
        {
            var p = new Person { Id = s.NextId++, BodyId = s.NextId++, SoulId = s.NextId++,
                Name = $"합성 인물 {n}", Born = -216, Lifespan = 1200000, Place = n % 100,
                Gestates = n % 2 == 0, Realm = Realm.Qi,
                Aptitudes = Enumerable.Repeat(5000, 6).ToArray(), Roots = Enumerable.Repeat(5000, 5).ToArray(),
                Personality = Enumerable.Repeat(50, 14).ToArray(), KnownPlaces = [n % 100] };
            p.Understanding["entry"] = 10000;
            p.Slot = s.ActiveSlots.Count; p.SlotGeneration = 1;
            s.ActiveSlots.Add(new PopulationSlot { Person = p.Id });
            s.People.Add(p); s.Souls.Add(new Soul { Id = p.SoulId, Root = p.SoulId, CurrentPerson = p.Id, Evidence = "합성 성능 fixture" });
        }
        return s;
    }
}
