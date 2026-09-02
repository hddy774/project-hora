using System.Collections.Concurrent;
using System.Diagnostics;
using Chronicles.Runtime;
using Chronicles.Simulation;

internal static class RuntimeHundredSpeedSoakTests
{
    private const string DurationEnvironmentVariable = "CMIP_SOAK_SECONDS";

    public static void Run()
    {
        var durationSeconds = GetDurationSeconds();
        var enforceSustainedBudget = durationSeconds >= 60;
        var warmupSeconds = durationSeconds >= 60 ? 30 : 1;
        var samples = new ConcurrentQueue<PulseSample>();
        Exception? fault = null;
        using var runner = new SimulationRunner(World.CreateDefault(0x23_00_23UL));
        runner.SetPerformanceDiagnosticsEnabled(true);
        runner.SetSpeed(100);
        long lastPulse = 0;
        var started = Stopwatch.StartNew();
        runner.Faulted += exception => Volatile.Write(ref fault, exception);
        runner.ObservationInvalidated += () =>
        {
            var snapshot = runner.GetPerformanceSnapshot();
            if (snapshot.SimulationPulseCount <= Interlocked.Read(ref lastPulse))
            {
                return;
            }

            Interlocked.Exchange(ref lastPulse, snapshot.SimulationPulseCount);
            samples.Enqueue(new PulseSample(
                started.Elapsed.TotalSeconds,
                snapshot.LastSimulationMilliseconds,
                snapshot.EstimatedBacklogMilliseconds));
        };

        runner.Start();
        while (started.Elapsed < TimeSpan.FromSeconds(durationSeconds))
        {
            if (Volatile.Read(ref fault) is { } runnerFault)
            {
                throw new InvalidOperationException("100배속 SOAK worker가 중단됐습니다.", runnerFault);
            }
            Thread.Sleep(50);
        }
        runner.SetPaused(true);

        var measured = samples
            .Where(sample => sample.ElapsedSeconds >= warmupSeconds)
            .ToArray();
        TestAssert.True(measured.Length >= Math.Max(3, durationSeconds - warmupSeconds),
            "100배속 SOAK warm-up 이후 표본 수");
        var ordered = measured
            .Select(sample => sample.PulseMilliseconds)
            .Order()
            .ToArray();
        var p95 = Percentile(ordered, 0.95);
        var p99 = Percentile(ordered, 0.99);
        var healthyPercent = measured.Count(sample => sample.PulseMilliseconds <= 80d)
            * 100d
            / measured.Length;
        var maximumBacklog = measured.Max(sample => sample.BacklogMilliseconds);
        var finalBacklog = measured[^1].BacklogMilliseconds;

        Console.WriteLine(
            $"100X {(enforceSustainedBudget ? "SOAK" : "SMOKE")}: " +
            $"{durationSeconds:N0}s · warm-up {warmupSeconds:N0}s · " +
            $"표본 {measured.Length:N0} · p95 {p95:N2} ms · p99 {p99:N2} ms · " +
            $"정상 {healthyPercent:N2}% · backlog 최대/최종 " +
            $"{maximumBacklog:N2}/{finalBacklog:N2} ms");
        if (enforceSustainedBudget)
        {
            TestAssert.True(p95 <= 80d,
                $"100배속 pulse p95 80ms 예산: {p95:N2}ms");
            TestAssert.True(p99 <= 100d,
                $"100배속 pulse p99 100ms 예산: {p99:N2}ms");
            TestAssert.True(healthyPercent >= 95d,
                $"100배속 정상 상태 95% 예산: {healthyPercent:N2}%");
        }
        TestAssert.True(finalBacklog <= 1_000d,
            $"100배속 backlog 1초 이내 해소: {finalBacklog:N2}ms");
    }

    private static int GetDurationSeconds()
    {
        var raw = Environment.GetEnvironmentVariable(DurationEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 5;
        }
        if (!int.TryParse(raw, out var seconds) || seconds is < 2 or > 3_600)
        {
            throw new InvalidOperationException(
                $"{DurationEnvironmentVariable}는 2~3600초 정수여야 합니다.");
        }
        return seconds;
    }

    private static double Percentile(double[] ordered, double percentile)
    {
        if (ordered.Length == 0)
        {
            return 0d;
        }
        var index = (int)Math.Ceiling(ordered.Length * percentile) - 1;
        return ordered[Math.Clamp(index, 0, ordered.Length - 1)];
    }

    private readonly record struct PulseSample(
        double ElapsedSeconds,
        double PulseMilliseconds,
        double BacklogMilliseconds);
}
