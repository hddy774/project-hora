using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Chronicles.Core;
using Chronicles.Persistence;
using Chronicles.Simulation;

internal static class SqliteSlotPersistenceTests
{
    public static void Run()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "cmip-sqlite-slot-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string slotId = "ten-thousand";
            var paths = SlotSaveStore.GetPaths(root, slotId);
            var world = World.CreateDefault(0x2500_0015UL);
            var firstAuthority = world.CreateAuthorityCheckpoint();
            var firstHash = firstAuthority.StateHash;

            var firstSave = SlotSaveStore.Save(root, slotId, firstAuthority);

            Equal(25, firstSave.WorldSchema, "SQLite world schema");
            Equal(WorldCheckpoint.CurrentSchemaVersion, firstSave.CheckpointSchema, "권위 payload schema");
            Equal(firstHash, firstSave.StateHash, "저장 state hash");
            True(File.Exists(paths.PrimaryPath), "primary world.db 생성");
            var header = new byte[16];
            using (var stream = File.OpenRead(paths.PrimaryPath))
            {
                stream.ReadExactly(header);
            }
            Equal("SQLite format 3\0", Encoding.ASCII.GetString(header), "실제 SQLite 파일 header");

            var manifest = JsonSerializer.Deserialize<SlotManifest>(File.ReadAllBytes(paths.ManifestPath));
            True(manifest is not null, "슬롯 manifest 역직렬화");
            Equal(25, manifest!.WorldSchema, "manifest world schema");
            Equal(WorldCheckpoint.CurrentSchemaVersion, manifest.CheckpointSchema, "manifest checkpoint schema");

            AssertCandidateLoad(
                root,
                slotId,
                SlotCandidateKind.Primary,
                firstHash,
                expectedPopulation: 10_000);

            const string secondarySlotId = "secondary";
            var secondary = World.Create(0x2500_0016UL, 16);
            var secondaryAuthority = secondary.CreateAuthorityCheckpoint();
            var secondaryHash = secondaryAuthority.StateHash;
            var secondarySave = SlotSaveStore.Save(root, secondarySlotId, secondaryAuthority);
            AssertCandidateLoad(
                root,
                secondarySlotId,
                SlotCandidateKind.Primary,
                secondaryHash,
                expectedPopulation: 16);
            True(
                !string.Equals(firstSave.DatabasePath, secondarySave.DatabasePath, StringComparison.OrdinalIgnoreCase),
                "슬롯별 world.db 경로 분리");

            // A legacy selected file is rejected by its typed result without any file read.
            // Keeping an exclusive handle open makes accidental reads fail deterministically.
            var legacyPath = Path.Combine(root, "world.cmip");
            var legacyBytes = Encoding.ASCII.GetBytes("legacy-save-must-remain-byte-identical");
            File.WriteAllBytes(legacyPath, legacyBytes);
            var legacyWriteTime = File.GetLastWriteTimeUtc(legacyPath);
            using (var legacyLock = new FileStream(
                legacyPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None))
            {
                var legacyResult = SlotSaveStore.InspectSelectedSave(legacyPath);
                Equal(SlotLoadStatus.IncompatibleLegacySave, legacyResult.Status, "구버전 typed 거부");
            }
            True(File.ReadAllBytes(legacyPath).SequenceEqual(legacyBytes), "구버전 원본 byte 불변");
            Equal(legacyWriteTime, File.GetLastWriteTimeUtc(legacyPath), "구버전 원본 timestamp 불변");

            world.AdvanceTo(new SimulationTick(20_000));
            var secondAuthority = world.CreateAuthorityCheckpoint();
            var secondHash = secondAuthority.StateHash;
            var secondSave = SlotSaveStore.Save(root, slotId, secondAuthority);
            True(secondSave.CreatedSequence > firstSave.CreatedSequence, "저장 생성 sequence 증가");
            True(File.Exists(paths.BackupPath), "이전 primary backup 생성");

            Corrupt(paths.PrimaryPath);
            ForceCollection();
            AssertCandidateLoad(
                root,
                slotId,
                SlotCandidateKind.Backup,
                firstHash,
                expectedPopulation: 10_000);

            // Saving again establishes a valid primary. A valid crash-left temporary copy
            // must win when the primary is subsequently corrupt.
            var thirdSave = SlotSaveStore.Save(root, slotId, secondAuthority);
            File.Copy(paths.PrimaryPath, paths.TemporaryPath, overwrite: true);
            Corrupt(paths.PrimaryPath);
            ForceCollection();
            AssertCandidateLoad(
                root,
                slotId,
                SlotCandidateKind.Temporary,
                secondHash,
                expectedPopulation: 10_000,
                expectedSequence: thirdSave.CreatedSequence);

            RunFivePassBenchmark(root, firstAuthority, firstSave.PayloadBytes);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void Corrupt(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None);
        stream.Position = 0;
        stream.Write(new byte[128]);
        stream.Flush(flushToDisk: true);
    }

    private static void AssertCandidateLoad(
        string root,
        string slotId,
        SlotCandidateKind expectedKind,
        ulong expectedStateHash,
        int expectedPopulation,
        long? expectedSequence = null)
    {
        var load = SlotSaveStore.Load(root, slotId);
        True(load.IsSuccess, $"SQLite candidate load: {load.Status} · {load.Message}");
        Equal(expectedKind, load.CandidateKind, "SQLite candidate 종류");
        Equal(expectedStateHash, load.StateHash, "SQLite candidate state hash");
        Equal(expectedPopulation, load.Checkpoint!.Agents.Length, "SQLite candidate 인구");
        if (expectedSequence is { } sequence)
        {
            Equal(sequence, load.CreatedSequence, "SQLite candidate 생성 sequence");
        }
    }

    private static void RunFivePassBenchmark(
        string root,
        WorldAuthorityCheckpoint authority,
        int payloadBytes)
    {
        const int repetitions = 5;
        var legacyPath = Path.Combine(root, "benchmark-legacy", "world.cmip");
        var legacySave = new OperationMeasurement[repetitions];
        var legacyLoad = new OperationMeasurement[repetitions];
        var sqliteSave = new OperationMeasurement[repetitions];
        var sqliteLoad = new OperationMeasurement[repetitions];

        for (var index = 0; index < repetitions; index++)
        {
            legacySave[index] = MeasureLiveManagedPeak(() =>
                WorldSaveStore.Save(legacyPath, authority.Checkpoint));
            legacyLoad[index] = MeasureLiveManagedPeak(() =>
            {
                var restored = World.Restore(WorldSaveStore.Load(legacyPath));
                GC.KeepAlive(restored);
            });

            sqliteSave[index] = MeasureLiveManagedPeak(() =>
                SlotSaveStore.Save(root, "benchmark-sqlite", authority));
            sqliteLoad[index] = MeasureLiveManagedPeak(() =>
            {
                var loaded = SlotSaveStore.Load(root, "benchmark-sqlite");
                True(loaded.IsSuccess, "SQLite benchmark load");
                GC.KeepAlive(loaded.Checkpoint);
            });
        }

        var legacySaveMilliseconds = Median(legacySave.Select(value => value.ElapsedMilliseconds));
        var legacyLoadMilliseconds = Median(legacyLoad.Select(value => value.ElapsedMilliseconds));
        var sqliteSaveMilliseconds = Median(sqliteSave.Select(value => value.ElapsedMilliseconds));
        var sqliteLoadMilliseconds = Median(sqliteLoad.Select(value => value.ElapsedMilliseconds));
        var legacyPeak = Median(legacySave.Select(value => (double)value.PeakManagedBytes));
        var sqlitePeak = Median(sqliteSave.Select(value => (double)value.PeakManagedBytes));
        var peakReduction = legacyPeak <= 0d
            ? 0d
            : (legacyPeak - sqlitePeak) * 100d / legacyPeak;
        var latencyBudgetPassed = sqliteSaveMilliseconds <= 1_000d && sqliteLoadMilliseconds <= 1_000d;
        var allocationBudgetPassed = peakReduction >= 30d;

        Console.WriteLine(
            $"SQLITE SLOT PERF (5회 중앙값): 10,000명 · payload {payloadBytes / 1024d / 1024d:N2} MiB · " +
            $"기존 save/load {legacySaveMilliseconds:N0}/{legacyLoadMilliseconds:N0} ms · " +
            $"SQLite save/load {sqliteSaveMilliseconds:N0}/{sqliteLoadMilliseconds:N0} ms · " +
            $"기존/SQLite save live managed peak {legacyPeak / 1024d / 1024d:N2}/{sqlitePeak / 1024d / 1024d:N2} MiB · " +
            $"peak 절감 {peakReduction:N1}% · " +
            $"1초 예산 {(latencyBudgetPassed ? "PASS" : "FAIL")} · 30% peak 절감 {(allocationBudgetPassed ? "PASS" : "FAIL")}");
        if (!latencyBudgetPassed || !allocationBudgetPassed)
        {
            Console.WriteLine(
                "SQLITE SLOT FOLLOW-UP: 미달 budget은 수치 조작 없이 PC_TEMP_REVIEW에 유지하고 " +
                "normalized authority table·streaming codec 최적화 대상으로 이관합니다.");
        }
    }

    private static OperationMeasurement MeasureLiveManagedPeak(Action action)
    {
        ForceCollection();
        var baseline = GC.GetTotalMemory(forceFullCollection: true);
        var peak = baseline;
        using var stop = new ManualResetEventSlim(initialState: false);
        var sampler = new Thread(() =>
        {
            while (!stop.IsSet)
            {
                UpdateMaximum(ref peak, GC.GetTotalMemory(forceFullCollection: false));
                Thread.Sleep(1);
            }
            UpdateMaximum(ref peak, GC.GetTotalMemory(forceFullCollection: false));
        })
        {
            IsBackground = true,
            Name = "SQLite save managed-heap sampler",
        };
        sampler.Start();
        var timer = Stopwatch.StartNew();
        try
        {
            action();
        }
        finally
        {
            timer.Stop();
            stop.Set();
            sampler.Join();
        }

        return new OperationMeasurement(
            timer.Elapsed.TotalMilliseconds,
            Math.Max(0, peak - baseline));
    }

    private static void UpdateMaximum(ref long target, long candidate)
    {
        while (true)
        {
            var current = Volatile.Read(ref target);
            if (candidate <= current
                || Interlocked.CompareExchange(ref target, candidate, current) == current)
            {
                return;
            }
        }
    }

    private static double Median(IEnumerable<double> source)
    {
        var values = source.Order().ToArray();
        return values[values.Length / 2];
    }

    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message}: expected={expected}, actual={actual}");
        }
    }

    private readonly record struct OperationMeasurement(
        double ElapsedMilliseconds,
        long PeakManagedBytes);
}
