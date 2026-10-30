using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace Chronicles.Game.Tests.Experiments;

internal sealed record ExecutionMeasurementRequest(string OutputPath, int[] Populations, int Repetitions = 1, string? SelectedExperiment = null);
internal sealed record ExperimentHostIdentity(string Commit, string ProductSourceFingerprint, string Runtime, string Device,
    string OperatingSystem, string Architecture, int CpuCount, string ThermalState, string PowerState, string ProcessScope);
internal sealed record AllocationSample(long Operation, long CurrentThreadBytes);
internal sealed record OperationDistribution(string OperationKind, int Samples, long P50Nanoseconds, long P95Nanoseconds, long P99Nanoseconds);
internal sealed record ExperimentBoundary(long Operation, long Month, string StateHash, ulong Rng, string EventDigest,
    long Births, long Deaths, int Living, string SlotDigest);
internal sealed record ExecutionMeasuredVariant(string Experiment, string Variant, int Population, int Repetition,
    ExperimentMeasurement Measurement, AllocationSample[] Allocations, long P50Nanoseconds, long P95Nanoseconds,
    long P99Nanoseconds, long ProcessRetainedManagedBytes, string[] OperationLabels, string Scope, string Excluded,
    ExperimentBoundary[] Boundaries, string OutputDigest, OperationDistribution[] Distributions);

// Explicit measurement entry points alone construct this sampler. Feature discovery never does.
internal sealed class ExecutionMeasurementSeries
{
    private readonly List<BackendSample> samples = [];
    private readonly List<AllocationSample> allocations = [];
    private readonly List<string> labels = [];
    internal T Measure<T>(string label, Func<T> operation)
    {
        var allocated = GC.GetAllocatedBytesForCurrentThread(); var began = Stopwatch.GetTimestamp();
        var value = operation();
        var ticks = Stopwatch.GetTimestamp() - began;
        var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        var index = samples.Count;
        samples.Add(new(index, checked((long)((Int128)ticks * 1_000_000_000 / Stopwatch.Frequency))));
        allocations.Add(new(index, bytes)); labels.Add(label); return value;
    }
    internal void Measure(string label, Action operation) => Measure(label, () => { operation(); return true; });
    internal ExecutionMeasuredVariant Finish(string experiment, string variant, int population, int repetition,
        ExperimentHostIdentity host, string fixtureHash, string commandHash, string kernel, int batchLength,
        string features, string warmupHash, string measuredHash, long retained, string scope, string excluded,
        ExperimentBoundary[] boundaries, string outputDigest)
    {
        var measurement = new ExperimentMeasurement(host.ProductSourceFingerprint, fixtureHash, commandHash, kernel, batchLength,
            features, host.Runtime, host.Device, warmupHash, measuredHash, samples.ToArray(), "revisit",
            "실제 원시 표본과 정확성 결과다. 자동 속도 채택/예산 판정을 하지 않으며 전체 비용과 유지비의 별도 검토가 필요하다.");
        ExecutionExperimentPlan.ValidateMeasuredEvidence(measurement);
        var sorted = samples.Select(s => s.Nanoseconds).Order().ToArray();
        long Percentile(int percentile) => sorted[Math.Max(0, (int)Math.Ceiling(sorted.Length * percentile / 100d) - 1)];
        var distributions = labels.Select((label, i) => (label, Sample: samples[i])).GroupBy(row =>
            row.label.StartsWith("advance:", StringComparison.Ordinal) ? "advance-one-month" :
            row.label.StartsWith("command:", StringComparison.Ordinal) ? string.Join(':', row.label.Split(':').Take(2)) : row.label)
            .Select(group =>
            {
                var values = group.Select(v => v.Sample.Nanoseconds).Order().ToArray();
                long P(int p) => values[Math.Max(0, (int)Math.Ceiling(values.Length * p / 100d) - 1)];
                return new OperationDistribution(group.Key, values.Length, P(50), P(95), P(99));
            }).ToArray();
        return new(experiment, variant, population, repetition, measurement, allocations.ToArray(),
            Percentile(50), Percentile(95), Percentile(99), retained, labels.ToArray(), scope, excluded, boundaries, outputDigest, distributions);
    }
}

internal static class ExecutionMeasurementHost
{
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };
    internal static ExperimentHostIdentity Capture()
    {
#if DEBUG
        throw new InvalidOperationException("실험 계측은 Release 빌드에서만 기록합니다.");
#else
        var commit = Run("git", "rev-parse", "HEAD");
        if (commit.Length != 40 || commit.Any(c => !Uri.IsHexDigit(c)) || Run("git", "status", "--porcelain", "--untracked-files=all").Length != 0)
            throw new InvalidOperationException("실험은 수정/미추적 파일이 없는 고정 소스에서만 실행합니다.");
        var root = Run("git", "rev-parse", "--show-toplevel");
        var fingerprint = Run("python3", Path.Combine(root, "scripts", "verify-v021-release.py"), "fingerprint", "--commit", commit);
        if (fingerprint.Length != 64 || fingerprint.Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("제품 source fingerprint를 읽을 수 없습니다.");
        return new(commit, fingerprint, RuntimeInformation.FrameworkDescription, Environment.MachineName,
            RuntimeInformation.OSDescription, RuntimeInformation.ProcessArchitecture.ToString(), Environment.ProcessorCount,
            Environment.GetEnvironmentVariable("V021_THERMAL_STATE") ?? "미측정·PC host 결과만",
            Environment.GetEnvironmentVariable("V021_POWER_STATE") ?? "미측정·PC host 결과만",
            "동일 프로세스·앞선 기능/JIT/공유 캐시 영향 가능. 예열과 각 측정 세계는 별도 입력에서 새로 복원한다.");
#endif
    }
    private static string Run(string executable, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("실험 소스 확인을 시작할 수 없습니다.");
        var output = process.StandardOutput.ReadToEnd(); process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("실험 소스 확인에 실패했습니다.");
        return output.Trim();
    }
    internal static string Digest<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    internal static long RetainedManagedBytes()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true); GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true); return GC.GetTotalMemory(false);
    }
    internal static void Write(string path, object report)
    {
        var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full + ".pending", JsonSerializer.Serialize(report, ReportOptions));
        File.Move(full + ".pending", full, overwrite: true);
    }
}
