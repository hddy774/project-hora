using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace Chronicles.Game.Tests.Experiments;

internal sealed record ReadStorageExperimentOptions(string ProductSourceFingerprint, string Device,
    int Listings = 32, int Repetitions = 25, int Warmups = 2, string? Workspace = null,
    string ThermalPowerState = "이 하네스는 열/전원 상태를 수집하지 않음; 측정 호스트의 별도 기록 필요");
internal sealed record ReadStorageRawSample(long Operation, long Nanoseconds, long ManagedAllocatedBytes,
    string OutputHash, long OutputBytes, bool MatchesReference);
internal sealed record ReadStoragePercentiles(long P50, long P95, long P99);
internal sealed record ReadStorageVariant(string Name, string Includes, string Excludes,
    ExperimentMeasurement Measurement, ReadStorageRawSample[] RawSamples,
    ReadStoragePercentiles Nanoseconds, ReadStoragePercentiles ManagedAllocatedBytes);
internal sealed record ReadStorageComparison(string Id, string Scope, string FixtureHash, string CommandHash,
    object Correctness, ReadStorageVariant[] Variants, string Decision, string Rationale);
internal sealed record ReadStorageExperimentReport(string SourceFingerprint, string Runtime, string OS,
    string ProcessArchitecture, int LogicalProcessors, string HostScope, string Device, ReadStorageExperimentOptions Options,
    string FixturePreparationScope, ReadStorageRawSample WarmupPreparation,
    ReadStorageRawSample MeasuredPreparation, ReadStorageComparison[] Comparisons);
internal sealed record ReadStorageOutput(string Hash, long Bytes = 0);

/// <summary>Explicit EX-03/05/06 measurement entrypoint. Functional discovery never invokes this sampler.</summary>
internal static class ExecutionExperimentScenarios
{
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };
    internal static void Run(string path, string productSourceFingerprint, string device, int listings = 32,
        int repetitions = 25, int warmups = 2, string? workspace = null, string? selectedExperiment = null)
    {
#if DEBUG
        throw new InvalidOperationException("실제 비교 측정은 Release 빌드에서만 실행합니다.");
#else
        var options = new ReadStorageExperimentOptions(productSourceFingerprint, device, listings, repetitions, warmups, workspace);
        if (string.IsNullOrWhiteSpace(productSourceFingerprint) || string.IsNullOrWhiteSpace(device) || listings is < 2 or > 10000 ||
            repetitions is < 3 or > 10000 || warmups is < 1 or > 100 ||
            selectedExperiment is not (null or "EX-03" or "EX-05" or "EX-06"))
            throw new ArgumentException("출처/기기와 고정 실험 작업량·예열/반복·선택 ID가 필요합니다.");
        var warmPreparation = Prepare(listings, out var warm);
        var measuredPreparation = Prepare(listings, out var measured);
        Require(warm.Hash == measured.Hash && warm.CommandHash == measured.CommandHash,
            "예열/측정은 별도 실제 세계지만 시작 fixture·명령열이 같아야 합니다.");
        var comparisons = new List<ReadStorageComparison>();
        if (selectedExperiment is null or "EX-03") comparisons.Add(AuthorizedReadExperiment.MeasureMarket(warm, measured, options));
        if (selectedExperiment is null or "EX-05") comparisons.Add(SnapshotStorageExperiment.Measure(warm, measured, options));
        if (selectedExperiment is null or "EX-06") comparisons.Add(AuthorizedReadExperiment.MeasureQueries(warm, measured, options));
        var report = new ReadStorageExperimentReport(productSourceFingerprint, RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription, RuntimeInformation.ProcessArchitecture.ToString(), Environment.ProcessorCount,
            "현재 .NET PC 호스트 실험; Android/Unity 실행 근거가 아님", device, options,
            "각각 World.Create·공개 출품/GrantUse 도메인명령·권한 DTO 캡처·GameCodec 검증을 포함. 작업 커널 표본과 별도 기록하며 전체 비용에서 숨기지 않음.",
            warmPreparation, measuredPreparation, comparisons.ToArray());
        File.WriteAllText(path, JsonSerializer.Serialize(report, ReportOptions));
#endif
    }

    private static ReadStorageRawSample Prepare(int listings, out PublicReadFixture fixture)
    {
        var allocation = GC.GetAllocatedBytesForCurrentThread(); var start = Stopwatch.GetTimestamp();
        fixture = PublicReadFixture.Create(listings);
        var elapsed = Elapsed(start); var allocated = GC.GetAllocatedBytesForCurrentThread() - allocation;
        return new(0, elapsed, allocated, fixture.Hash, fixture.Envelope.Length, true);
    }

    internal static ReadStorageVariant Sample(string experiment, string name, string includes, string excludes,
        PublicReadFixture warm, PublicReadFixture measured, ReadStorageExperimentOptions options,
        Func<PublicReadFixture, ReadStorageOutput> operation, string expected)
    {
        // Read kernels use detached immutable input; stateful storage/settlement uses a fresh path/decoded clone per operation.
        for (var index = 0; index < options.Warmups; index++) Require(operation(warm).Hash == expected, "예열 후보의 실제 결과 불일치: " + name);
        Require(warm.World.StateHash() == warm.Hash, "예열이 원본 세계를 바꾸었습니다.");
        var samples = new List<ReadStorageRawSample>();
        for (var index = 0; index < options.Repetitions; index++)
        {
            var allocation = GC.GetAllocatedBytesForCurrentThread(); var start = Stopwatch.GetTimestamp();
            var output = operation(measured);
            var elapsed = Elapsed(start); var allocated = GC.GetAllocatedBytesForCurrentThread() - allocation;
            samples.Add(new(index, elapsed, allocated, output.Hash, output.Bytes, output.Hash == expected));
        }
        Require(measured.World.StateHash() == measured.Hash, "측정이 권위 입력을 바꾸었습니다.");
        var correct = samples.All(sample => sample.MatchesReference);
        var rationale = correct
            ? "같은 권한 입력·실제 정답과 일치한 원시 표본. 후보 이점·유지비·플랫폼을 검토하기 전 제품 채택으로 승격하지 않음."
            : "실제 정답과 다른 출력이 있어 이 후보 결과를 채택할 수 없음.";
        var common = new ExperimentMeasurement(options.ProductSourceFingerprint, measured.Hash, measured.CommandHash,
            experiment + ":" + name + ":v1", measured.Listings,
            "같은 개인 cached DTO/실제 명령; 관리 thread 할당만 계측·native/I/O/OS 메모리는 별도",
            RuntimeInformation.FrameworkDescription, options.Device, warm.Hash, measured.Hash,
            samples.Select(sample => new BackendSample(sample.Operation, sample.Nanoseconds)).ToArray(), correct ? "revisit" : "reject", rationale);
        ExecutionExperimentPlan.ValidateMeasuredEvidence(common);
        return new(name, includes, excludes, common, samples.ToArray(),
            Percentiles(samples.Select(sample => sample.Nanoseconds)), Percentiles(samples.Select(sample => sample.ManagedAllocatedBytes)));
    }

    private static long Elapsed(long start) => checked((long)Math.Ceiling(Stopwatch.GetElapsedTime(start).TotalNanoseconds));

    private static ReadStoragePercentiles Percentiles(IEnumerable<long> values)
    {
        var ordered = values.Order().ToArray();
        long At(double fraction) => ordered[Math.Max(0, (int)Math.Ceiling(ordered.Length * fraction) - 1)];
        return new(At(.50), At(.95), At(.99));
    }

    internal static string Hash<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    internal static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
