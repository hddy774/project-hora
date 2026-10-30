using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Chronicles.Game;
using Chronicles.Game.Tests;

internal static partial class PerformanceScenarios
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static void Run(string path, int? population = null, bool inventoryProjectionExperiment = false)
    {
        var total = Stopwatch.StartNew(); var environment = MeasurementEnvironment();
        if (population is { } requested && !SyntheticPopulationFixture.Sizes.Contains(requested)) throw new ArgumentOutOfRangeException(nameof(population));
        var requestedSizes = population is { } single ? new[] { single } : SyntheticPopulationFixture.Sizes.ToArray();
        var reports = new List<object>(); var completedSizes = new List<int>();
        int? currentPopulation = null; var currentStage = "starting"; var currentMonth = 0;
        double[] currentSamples = [];
        void SaveProgress() => WriteMeasurementReport(path, new { schema_version = 2, kind = "seonro-population-baseline",
            complete = !inventoryProjectionExperiment && completedSizes.Order().SequenceEqual(SyntheticPopulationFixture.Sizes), requestedSizes, completedSizes,
            experimental_configuration = inventoryProjectionExperiment,
            configuration = new { inventoryProjectionExperiment, inventoryCandidateMode = "person15-adopted", inventoryProjectionAdoptedByDefault = true },
            environment, totalElapsedSeconds = total.Elapsed.TotalSeconds, reports,
            inProgress = new { population = currentPopulation, stage = currentStage, completedMonths = currentMonth, monthlySamplesMs = currentSamples },
            note = "12-month live synthetic baseline; monthly timings exclude fixture/warmup/restore/full collection, total time includes them; retained memory is the collected process managed heap, not cumulative allocations or per-world attribution; numerical budgets follow measurement; partial sizes do not satisfy the final gate; not Android or legacy 100x acceptance." });
        SaveProgress();
        foreach (var size in requestedSizes)
        {
            currentPopulation = size; currentStage = "fixture_and_warmup"; currentMonth = 0; currentSamples = [];
            SaveProgress(); Console.WriteLine($"PERF-BEGIN {size}: 실제 fixture·독립 예열 시작");
            reports.Add(MeasurePopulation(size, inventoryProjectionExperiment, (month, samples) =>
            {
                currentStage = "measuring"; currentMonth = month; currentSamples = samples.ToArray();
                SaveProgress(); Console.WriteLine($"PERF-MONTH {size}: {month}/12 · {samples[^1]:F1}ms");
            }, stage =>
            {
                currentStage = stage; SaveProgress(); Console.WriteLine($"PERF-STAGE {size}: {stage}");
            }));
            completedSizes.Add(size); currentPopulation = null; currentStage = "completed_population";
            SaveProgress(); // Preserve earlier real measurements even if a later large case is slow or fails.
        }
    }

    private static void WriteMeasurementReport(string path, object report)
    {
        var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var pending = full + ".pending";
        File.WriteAllText(pending, JsonSerializer.Serialize(report, Options)); File.Move(pending, full, overwrite: true);
    }

    private static object MeasurePopulation(int size, bool inventoryProjectionExperiment, Action<int, IReadOnlyList<double>> progress, Action<string> stage)
    {
        // A separate frame prevents the preceding population's world/restore snapshot from being retained by this frame.
        var warmup = Warmup(size, inventoryProjectionExperiment, stage); var initialHash = warmup.InitialHash;
        var measured = PrepareMeasurementWorld(size, "measured", inventoryProjectionExperiment, stage);
        stage("opening_hash");
        if (measured.StateHash() != initialHash) throw new InvalidOperationException("Independent synthetic fixture builds must match before warmup.");
        var samples = new List<double>(); stage("opening_heap_collection"); var openingRetained = CollectedManagedBytes();
        var allocation = 0L; var sw = new Stopwatch();
        for (var month = 1; month <= 12; month++)
        {
            stage($"advancing_month_{month}");
            var beforeAllocation = GC.GetTotalAllocatedBytes();
            sw.Restart(); measured.AdvanceTo(month); sw.Stop(); samples.Add(sw.Elapsed.TotalMilliseconds);
            allocation += GC.GetTotalAllocatedBytes() - beforeAllocation;
            progress(month, samples);
        }
        var measuredProjectionSnapshots = measured.InventoryProjectionSnapshots;
        if (measuredProjectionSnapshots <= 0) throw new InvalidOperationException("측정 world가 기본 Person 투영 snapshot을 실행하지 않았습니다.");
        // No full checkpoint clone has been created yet; the actual live world remains rooted through this measurement.
        stage("final_heap_collection"); var retained = CollectedManagedBytes(); GC.KeepAlive(measured);
        stage("final_checkpoint_validation");
        var final = measured.Checkpoint(); Invariants.Validate(final);
        stage("query");
        sw.Restart(); var view = measured.View(); sw.Stop(); var queryMs = sw.Elapsed.TotalMilliseconds;
        stage("save_encode");
        sw.Restart(); var bytes = GameCodec.Encode(measured); sw.Stop(); var saveMs = sw.Elapsed.TotalMilliseconds;
        stage("load_decode");
        sw.Restart(); var restored = GameCodec.Decode(bytes); sw.Stop(); var loadMs = sw.Elapsed.TotalMilliseconds;
        stage("roundtrip_hash_and_cognition");
        if (restored.StateHash() != measured.StateHash()) throw new InvalidOperationException("성능 fixture 왕복 hash");
        var sorted = samples.Order().ToArray();
        var cognition = CognitionMeasurements(final, out var cognitionBytes);
        Console.WriteLine($"PERF {size}: advance 12mo={samples.Sum():F1}ms, query={queryMs:F1}ms, save={saveMs:F1}ms");
        return new { population = size, fixture = SyntheticPopulationFixture.Describe(size), usedLaws = final.Laws,
            experimental_configuration = inventoryProjectionExperiment,
            inventoryProjectionSnapshots = new { warmup = warmup.ProjectionSnapshots, measured = measuredProjectionSnapshots },
            measuredMonths = 12, warmupMonths = 2, repetitions = 1, monthlySamplesMs = samples,
            advance12MonthsMs = samples.Sum(), monthlyP50Ms = (sorted[5] + sorted[6]) / 2, monthlyP95Ms = sorted[11],
            queryMs, saveMs, loadMs, allocatedBytes = allocation, saveBytes = bytes.Length,
            allocationScope = "sum of process-wide allocation deltas around each actual AdvanceTo; monthly progress JSON and console reporting are excluded",
            openingRetainedManagedBytes = openingRetained, retainedManagedBytes = retained, serializedCognitionBytes = cognitionBytes,
            retainedMemoryScope = "process managed heap after blocking compacting full GC, finalizer drain and second full GC; includes actual rooted world, shared/runtime caches and prior small result rows; excludes RSS/native memory; not a sum of per-person object sizes",
            cognition, observedPeople = view.People.Count, living = final.People.Count(p => p.Status == LifeStatus.Alive),
            workload = Workload(final), initialHash, finalHash = measured.StateHash() };
    }

    public static void Soak(string path)
    {
        // Preserve V020-G03 as a comparison with its original success/failure semantics.
        // This is not the revision-6 T04 long-run stability gate.
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
        var report = new { schema_version = 1, kind = "seonro-legacy-speed-comparison", mandatoryForV021 = false,
            population = 10000, living, elapsedSeconds = watch.Elapsed.TotalSeconds,
            targetMonths = target, completedMonths = world.Month, targetSpeed = 100,
            achievedSpeed = world.Month / 12d * 10 / watch.Elapsed.TotalSeconds,
            passed = world.Month == target && living == 10000 && watch.Elapsed.TotalSeconds <= 60,
            fixture = SyntheticPopulationFixture.Describe(10000), usedLaws = state.Laws, workload = Workload(state),
            finalHash = world.StateHash(), runtime = RuntimeInformation.FrameworkDescription, cpu = Environment.ProcessorCount };
        File.WriteAllText(path, JsonSerializer.Serialize(report, Options));
        Console.WriteLine(JsonSerializer.Serialize(report, Options));
    }

    private static GameWorld PrepareMeasurementWorld(int population, string prefix, bool inventoryProjectionExperiment, Action<string> stage)
    {
        stage(prefix + "_fixture"); var initial = SyntheticPopulationFixture.Build(population);
        stage(prefix + "_restore"); var world = GameWorld.Restore(initial);
        if (inventoryProjectionExperiment) world.EnableInventoryProjectionForExperiment();
        if (!world.InventoryProjectionEnabled) throw new InvalidOperationException("측정 world가 채택된 기본 Person 투영 경로를 사용하지 않습니다.");
        return world;
    }

    private static (string InitialHash, long ProjectionSnapshots) Warmup(int population, bool inventoryProjectionExperiment, Action<string> stage)
    {
        var warm = PrepareMeasurementWorld(population, "warmup", inventoryProjectionExperiment, stage);
        stage("warmup_hash");
        var initialHash = warm.StateHash();
        stage("warmup_month_1"); warm.AdvanceTo(1);
        stage("warmup_month_2"); warm.AdvanceTo(2);
        var snapshots = warm.InventoryProjectionSnapshots;
        if (snapshots <= 0) throw new InvalidOperationException("예열 world가 기본 Person 투영 snapshot을 실행하지 않았습니다.");
        return (initialHash, snapshots);
    }

    private static object Workload(GameState state) => new
    {
        peopleWithDecisions = state.People.Count(p => p.Decisions.Count > 0),
        retainedDecisionRecords = state.People.Sum(p => p.Decisions.Count),
        retainedInferenceExpansions = state.People.Sum(p => p.Decisions.Sum(d => (long)d.Expansions)),
        maximumRetainedInferenceExpansionsPerPerson = state.People.Max(p => p.Decisions.Sum(d => (long)d.Expansions)),
        currentActions = state.People.Where(p => p.Status == LifeStatus.Alive).GroupBy(p => p.Action)
            .OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count(), StringComparer.Ordinal),
        completedKnowledgeActivities = state.Knowledge.Activities.Count(a => a.Status == Chronicles.Game.Knowledge.KnowledgeActivityStatus.Completed),
        completedPhysicalJobs = state.Inventory.Jobs.Count(j => j.Status == "completed"),
        actualExtraction = state.Materials.Values.Sum(m => m.Extraction), actualProduction = state.Materials.Values.Sum(m => m.Production)
    };
}
