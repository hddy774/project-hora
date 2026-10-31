using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Chronicles.Game;

internal static class Program
{
    private static Func<int, GameState> build = null!;
    private static Func<GameState, object> workload = null!;
    private sealed record Result(List<object> Reports, List<WeakReference> Worlds, List<WeakReference> Snapshots);

    private static void Main(string[] args)
    {
        var mode = args[0];
        var output = args[1];
        var tests = Assembly.LoadFrom(args[2]);
        build = tests.GetType("Chronicles.Game.Tests.SyntheticPopulationFixture")!
            .GetMethod("Build", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Func<int, GameState>>();
        workload = tests.GetType("PerformanceScenarios")!.GetMethod("Workload", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<Func<GameState, object>>();
        Directory.CreateDirectory(output);
        var result = Sequence(mode, output);
        var retained = Collected();
        var worlds = result.Worlds.Select(Alive).ToArray();
        var snapshots = result.Snapshots.Select(Alive).ToArray();
        Marker(mode, "after-frame-return", 1000, retained, new { worlds, snapshots });
        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
        {
            source = "df7240556a260202a248e57b1f9868e372b9d995", mode,
            scope = "diagnostic opening boundaries only; prior 120-month cases not executed; no acceptance claim",
            precedingFunctionalSelection = "none in this diagnostic child",
            result.Reports, releasedRetainedManagedBytes = retained, worldsAlive = worlds, snapshotsAlive = snapshots
        }, new JsonSerializerOptions { WriteIndented = true }));
        GC.KeepAlive(result);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Alive(WeakReference reference) => reference.IsAlive;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Result Sequence(string mode, string output)
    {
        var reports = new List<object>();
        var worlds = new List<WeakReference>();
        var snapshots = new List<WeakReference>();
        var populations = mode == "fresh" ? new[] { 1000 } : new[] { 2, 100, 1000 };
        foreach (var population in populations)
        {
            var world = GameWorld.Restore(build(population));
            var checkpoints = new List<object>();
            var slot = Path.Combine(output, $"population-{population}.seonro");
            void Checkpoint()
            {
                var retained = Collected();
                GC.KeepAlive(world);
                if (population == 1000) Marker(mode, "pre-snapshot-opening", population, retained, null);
                var state = world.Checkpoint();
                Invariants.Validate(state);
                var beforeQuery = world.StateHash();
                world.View();
                if (world.StateHash() != beforeQuery) throw new InvalidDataException("query changed world");
                var command = world.Execute(new($"stability:{population}:{world.Month}", 0, "observe"));
                if (!command.Accepted) throw new InvalidDataException(command.Message);
                var hash = world.StateHash();
                GameSaveStore.SaveAtomic(world, slot);
                world = GameSaveStore.Load(slot);
                if (world.StateHash() != hash) throw new InvalidDataException("save hash changed");
                Invariants.Validate(world.Checkpoint());
                checkpoints.Add(new { month = world.Month, people = state.People.Count,
                    living = state.People.Count(p => p.Status == LifeStatus.Alive), births = state.BirthCount, deaths = state.DeathCount,
                    retainedManagedBytes = retained, saveBytes = new FileInfo(slot).Length, hash, workload = workload(state) });
            }
            Checkpoint();
            var final = world.Checkpoint();
            worlds.Add(new WeakReference(world));
            snapshots.Add(new WeakReference(final));
            reports.Add(new { population, completedMonths = world.Month, checkpoints,
                living = final.People.Count(p => p.Status == LifeStatus.Alive), births = final.BirthCount, deaths = final.DeathCount,
                usedLaws = final.Laws, workload = workload(final), finalHash = world.StateHash() });
        }
        return new(reports, worlds, snapshots);
    }

    private static long Collected()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        return GC.GetTotalMemory(false);
    }

    private static void Marker(string mode, string phase, int population, long retained, object? lifetimes)
    {
        var gc = GC.GetGCMemoryInfo();
        Console.WriteLine("MARKER " + JsonSerializer.Serialize(new
        {
            pid = Environment.ProcessId, mode, phase, population, month = 0,
            retainedManagedBytes = retained, gc.Index, gc.HeapSizeBytes, gc.FragmentedBytes, gc.TotalCommittedBytes,
            gen2 = GC.CollectionCount(2), serverGc = GCSettings.IsServerGC, cpu = Environment.ProcessorCount, lifetimes
        }));
        Console.Out.Flush();
        if (Console.ReadLine() != "continue") throw new InvalidOperationException("explicit resume missing");
    }
}
