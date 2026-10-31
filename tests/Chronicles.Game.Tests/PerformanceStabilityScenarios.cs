using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chronicles.Game;
using Chronicles.Game.Tests;

internal static partial class PerformanceScenarios
{
    private const string PerformancePolicy = "v021-revision6-section11-T04";
    private static readonly JsonSerializerOptions StabilityPlanJson = new()
    { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private sealed record StabilityCase(int Population, long HorizonMonths, int CheckpointEveryMonths,
        int ValidateEveryMonths, double WallTimeLimitSeconds);
    private sealed record StabilityPlan(int SchemaVersion, string PolicyId, string BaselineReportSha256,
        string Rationale, StabilityCase[] Cases);
    private sealed record StabilityCaseResult(object Report, bool Passed, string CompletionLog);

    // A caller supplies a reviewed, post-baseline plan. No canonical duration or speed is invented here.
    public static void Stability(string path, string planPath)
    {
        var total = Stopwatch.StartNew(); var environment = MeasurementEnvironment();
        var bytes = File.ReadAllBytes(planPath);
        var plan = JsonSerializer.Deserialize<StabilityPlan>(bytes, StabilityPlanJson)
            ?? throw new InvalidDataException("장기 안정성 계획이 없습니다.");
        if (plan.SchemaVersion != 1 || plan.PolicyId != PerformancePolicy || string.IsNullOrWhiteSpace(plan.Rationale) ||
            string.IsNullOrEmpty(plan.BaselineReportSha256) || plan.BaselineReportSha256.Length != 64 || plan.BaselineReportSha256.Any(c => !Uri.IsHexDigit(c)) ||
            plan.Cases is null || plan.Cases.Length == 0 || !plan.Cases.Any(c => c.Population == 10000) ||
            plan.Cases.Select(c => c.Population).Distinct().Count() != plan.Cases.Length ||
            plan.Cases.Any(c => !SyntheticPopulationFixture.Sizes.Contains(c.Population) || c.HorizonMonths <= 12 ||
                c.CheckpointEveryMonths <= 0 || c.CheckpointEveryMonths > c.HorizonMonths / 2 ||
                c.ValidateEveryMonths <= 0 || c.ValidateEveryMonths > c.CheckpointEveryMonths ||
                !double.IsFinite(c.WallTimeLimitSeconds) || c.WallTimeLimitSeconds <= 0))
            throw new InvalidDataException("실측 기준선·근거·10,000명 포함 작업량·저장/불변식 경계·시간 상한을 명시하세요.");
        var directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, Path.GetFileNameWithoutExtension(path) + "-saves");
        if (Directory.Exists(directory)) throw new IOException("새 검증 출력 경로를 지정하세요. 기존 저장 근거는 덮어쓰지 않습니다.");
        Directory.CreateDirectory(directory);
        var reports = new List<object>(); var allPassed = true; var finishedCases = new List<int>();
        int? runningPopulation = null; string? executionFailure = null;
        void SaveProgress()
        {
            var complete = allPassed && finishedCases.Count == plan.Cases.Length;
            WriteMeasurementReport(path, new { schema_version = 1, kind = "seonro-long-run-stability",
                policy = PerformancePolicy, environment, baselineReportSha256 = plan.BaselineReportSha256,
                planSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)), totalElapsedSeconds = total.Elapsed.TotalSeconds,
                complete, status = allPassed ? complete ? "completed" : "running" : "failed",
                runningPopulation, finishedCases, executionFailure, passed = complete, reports,
                limitations = "Declared synthetic initial conditions, not founder growth history. Total time includes fixture, real AI, checks, observer I/O, atomic save/resume and prior report writes; it is captured before the current report write. Case time excludes final result projection/report writing. Natural death/population change is allowed; no 100x/60-second claim or fixed living population is required." });
        }
        SaveProgress();
        try
        {
            foreach (var workload in plan.Cases)
            {
                runningPopulation = workload.Population; SaveProgress();
                var result = MeasureStabilityCase(workload, directory);
                allPassed &= result.Passed;
                reports.Add(result.Report);
                finishedCases.Add(workload.Population); runningPopulation = null; SaveProgress();
                Console.WriteLine(result.CompletionLog);
            }
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            allPassed = false; executionFailure = error.ToString(); SaveProgress();
            throw;
        }
    }

    // Do not carry the preceding world's closure/final snapshot into the next case's process-heap sample.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static StabilityCaseResult MeasureStabilityCase(StabilityCase workload, string directory)
    {
        var watch = Stopwatch.StartNew();
        var world = GameWorld.Restore(SyntheticPopulationFixture.Build(workload.Population));
        var fixtureSeconds = watch.Elapsed.TotalSeconds;
        var checkpoints = new List<object>(); var boundaryChecks = 0; string? failure = null;
        var slot = Path.Combine(directory, $"population-{workload.Population}.seonro");
        void Checkpoint()
        {
            var retained = CollectedManagedBytes(); GC.KeepAlive(world);
            var state = world.Checkpoint(); Invariants.Validate(state); boundaryChecks++;
            var beforeQuery = world.StateHash(); world.View();
            if (world.StateHash() != beforeQuery) throw new InvalidDataException("장기 조회가 세계를 변경했습니다.");
            var command = world.Execute(new($"stability:{workload.Population}:{world.Month}", 0, "observe"));
            if (!command.Accepted) throw new InvalidDataException(command.Message);
            var hash = world.StateHash();
            GameSaveStore.SaveAtomic(world, slot); world = GameSaveStore.Load(slot);
            if (world.StateHash() != hash) throw new InvalidDataException("실제 파일 저장·재개 hash가 다릅니다.");
            Invariants.Validate(world.Checkpoint()); boundaryChecks++;
            checkpoints.Add(new { month = world.Month, people = state.People.Count,
                living = state.People.Count(p => p.Status == LifeStatus.Alive), births = state.BirthCount, deaths = state.DeathCount,
                retainedManagedBytes = retained, saveBytes = new FileInfo(slot).Length,
                invariantValid = true, saveResumeEqual = true, queryUnchanged = true, publicCommandAccepted = true,
                hash, workload = Workload(state) });
            Console.WriteLine($"STABILITY population={workload.Population} month={world.Month}/{workload.HorizonMonths} living={state.People.Count(p => p.Status == LifeStatus.Alive)} elapsed={watch.Elapsed.TotalSeconds:F2}s");
        }
        try
        {
            Checkpoint();
            while (world.Month < workload.HorizonMonths && watch.Elapsed.TotalSeconds < workload.WallTimeLimitSeconds)
            {
                world.AdvanceTo(world.Month + 1); // Real AI, work, births, deaths and physical flows remain active.
                if (world.Month % workload.ValidateEveryMonths == 0)
                { Invariants.Validate(world.Checkpoint()); boundaryChecks++; }
                if (world.Month % workload.CheckpointEveryMonths == 0 || world.Month == workload.HorizonMonths) Checkpoint();
            }
            if (world.Month % workload.CheckpointEveryMonths != 0 && world.Month != workload.HorizonMonths) Checkpoint();
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException)
        { failure = error.ToString(); }
        watch.Stop(); var final = world.Checkpoint();
        var passed = failure is null && world.Month == workload.HorizonMonths &&
            watch.Elapsed.TotalSeconds <= workload.WallTimeLimitSeconds && checkpoints.Count >= 3;
        var report = new { population = workload.Population, targetMonths = workload.HorizonMonths, completedMonths = world.Month,
            elapsedSeconds = watch.Elapsed.TotalSeconds, fixtureSeconds, boundaryChecks, checkpoints, passed, failure,
            living = final.People.Count(p => p.Status == LifeStatus.Alive), births = final.BirthCount, deaths = final.DeathCount,
            fixture = SyntheticPopulationFixture.Describe(workload.Population), usedLaws = final.Laws,
            workload = Workload(final), finalHash = world.StateHash() };
        var completionLog = $"STABILITY CASE population={workload.Population} month={world.Month}/{workload.HorizonMonths} living={final.People.Count(p => p.Status == LifeStatus.Alive)} elapsed={watch.Elapsed.TotalSeconds:F2}s result={(passed ? "completed" : "failed")}";
        return new(report, passed, completionLog);
    }

    internal static object MeasurementEnvironment()
    {
        var source = MeasurementGit("rev-parse", "HEAD");
        if (source.Length != 40 || source.Any(c => !Uri.IsHexDigit(c)) ||
            MeasurementGit("status", "--porcelain", "--untracked-files=all").Length != 0)
            throw new InvalidOperationException("측정 소스 커밋을 확인하세요.");
#if DEBUG
        throw new InvalidOperationException("성능 근거는 Release 빌드에서만 기록합니다.");
#else
        var arguments = Environment.GetCommandLineArgs(); var filter = Array.IndexOf(arguments, "--filter");
        return new { sourceCommit = source, sourceWorktreeClean = true, buildConfiguration = "Release", platform = "host", engine = "none",
            device = Environment.MachineName, runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(), cpu = Environment.ProcessorCount,
            totalAvailableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            precedingFunctionalSelection = filter >= 0 && filter + 1 < arguments.Length ? arguments[filter + 1] : "all",
            processScope = "same process after the requested functional selection; shared/JIT caches may be warm; fixture worlds are independent",
            thermalState = Environment.GetEnvironmentVariable("V021_THERMAL_STATE") ?? "not measured; host result only",
            powerState = Environment.GetEnvironmentVariable("V021_POWER_STATE") ?? "not measured; host result only" };
#endif
    }

    private static string MeasurementGit(params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var git = Process.Start(start) ?? throw new InvalidOperationException("측정 소스 정보를 읽을 수 없습니다.");
        var output = git.StandardOutput.ReadToEnd(); git.WaitForExit();
        if (git.ExitCode != 0) throw new InvalidOperationException("측정 소스 정보를 확인하세요.");
        return output.Trim();
    }
}
