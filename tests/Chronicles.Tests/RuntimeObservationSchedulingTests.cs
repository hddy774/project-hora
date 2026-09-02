using Chronicles.Queries;
using Chronicles.Runtime;
using Chronicles.Simulation;

internal static class RuntimeObservationSchedulingTests
{
    public static void Run()
    {
        using var runner = new SimulationRunner(World.Create(
            0x26_00_01UL,
            100,
            WorldBalanceConfiguration.Default,
            WorldGenerationConfiguration.Default));
        runner.SetPerformanceDiagnosticsEnabled(true);
        var initial = runner.GetShellSnapshot();
        TestAssert.Equal(0L, runner.GetPerformanceSnapshot().ObservationCount,
            "shell 조회는 observation query를 만들지 않음");

        using var gateEntered = new ManualResetEventSlim();
        using var releaseGate = new ManualResetEventSlim();
        var lockedOperation = Task.Run(() => runner.ExecuteSaveAtSafeBoundary(_ =>
        {
            gateEntered.Set();
            if (!releaseGate.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("runtime lock 검증 해제가 지연됐습니다.");
            }
            return 0;
        }));
        TestAssert.True(gateEntered.Wait(TimeSpan.FromSeconds(5)),
            "저장 안전 경계 lock 진입");

        var request = new ObservationRequest(
            RankingCount: 1,
            EventCount: 0,
            Sections: ObservationSections.None,
            WorldChronicleCount: 0,
            ContentSecretRealmLimit: 0,
            KnowledgeEditionLimit: 0,
            LawCandidateLimit: 0);
        TestAssert.True(!runner.TryGetSnapshot(request, out _),
            "수동 입력이 아닌 관찰은 simulation 사용 중 프레임 건너뜀");
        var shellRead = Task.Run(runner.GetShellSnapshot);
        TestAssert.True(shellRead.Wait(TimeSpan.FromMilliseconds(500)),
            "shell snapshot은 권위 lock을 기다리지 않음");
        TestAssert.Equal(initial, shellRead.Result, "lock 중 마지막 완성 shell snapshot");

        releaseGate.Set();
        lockedOperation.GetAwaiter().GetResult();
        TestAssert.True(runner.TryGetSnapshot(request, out var snapshot) && snapshot is not null,
            "권위 lock 해제 후 관찰 성공");
        TestAssert.Equal(1L, runner.GetPerformanceSnapshot().ObservationCount,
            "실제 materialization만 observation 표본으로 기록");

        runner.SetSpeed(100);
        runner.Start();
        TestAssert.True(SpinWait.SpinUntil(
                () => runner.GetShellSnapshot().Tick.Value > initial.Tick.Value,
                TimeSpan.FromSeconds(5)),
            "worker가 lock-free shell snapshot을 발행");
        TestAssert.Equal(1L, runner.GetPerformanceSnapshot().ObservationCount,
            "shell 갱신은 닫힌 화면 query를 실행하지 않음");
    }
}
