using System.Diagnostics;
using Chronicles.Core;
using Chronicles.Simulation;

internal static class CultivationParallelThroughputTests
{
    public static void Run()
    {
        const long targetTick = 20_000;

        // 독립 프로세스의 첫 실행 JIT 비용과 권위 전이 처리량을 섞지 않는다.
        var warmup = World.Create(0x2300UL, 256);
        warmup.ConfigureCultivationParallelismForTesting(enabled: true, minimumBatchSize: 2);
        warmup.AdvanceTo(new SimulationTick(targetTick));

        var initial = World.CreateDefault(20260830).CreateCheckpoint();
        var serial = World.Restore(initial);
        var parallel = World.Restore(initial);
        serial.ConfigureCultivationParallelismForTesting(enabled: false);
        parallel.ConfigureCultivationParallelismForTesting(enabled: true);

        var serialTimer = Stopwatch.StartNew();
        serial.AdvanceTo(new SimulationTick(targetTick));
        serialTimer.Stop();
        var parallelTimer = Stopwatch.StartNew();
        parallel.AdvanceTo(new SimulationTick(targetTick));
        parallelTimer.Stop();

        TestAssert.Equal(serial.ComputeStateHash(), parallel.ComputeStateHash(),
            "직렬·병렬 처리량 비교 상태 hash");
        var diagnostics = parallel.GetCultivationParallelDiagnostics();
        TestAssert.True(diagnostics.ParallelCandidateCount > 0,
            "처리량 비교에서 실제 병렬 수행 후보 처리");
        Console.WriteLine(
            $"CULTIVATION CPU: 직렬 {serialTimer.Elapsed.TotalMilliseconds:N0} ms · " +
            $"병렬 {parallelTimer.Elapsed.TotalMilliseconds:N0} ms · " +
            $"후보 {diagnostics.ParallelCandidateCount:N0}명/{diagnostics.ParallelBatchCount:N0}묶음");
    }
}
