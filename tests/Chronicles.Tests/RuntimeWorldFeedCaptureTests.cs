using Chronicles.Core;
using Chronicles.Runtime;
using Chronicles.Simulation;

internal static class RuntimeWorldFeedCaptureTests
{
    public static void Run()
    {
        var world = World.Create(
            0x23_00_FEEDUL,
            100,
            WorldBalanceConfiguration.Default,
            WorldGenerationConfiguration.Default);
        using var runner = new SimulationRunner(world);

        world.AdvanceTo(GameCalendar.FromYears(101));
        TestAssert.True(!runner.IsWorldFeedCaptureEnabled,
            "runtime world feed는 기본적으로 비활성");
        TestAssert.Equal(0, runner.DrainWorldEventFeed().Events.Count,
            "자동 출력 비활성 이벤트 retained 0");
        TestAssert.Equal(0, runner.DrainWorldCenturyChronicleFeed().Entries.Count,
            "자동 출력 비활성 역사서 retained 0");

        runner.SetWorldFeedCaptureEnabled(true);
        world.AdvanceTo(GameCalendar.FromYears(202));
        TestAssert.True(runner.DrainWorldEventFeed().Events.Count > 0,
            "자동 출력 활성 이벤트 feed");
        TestAssert.True(runner.DrainWorldCenturyChronicleFeed().Entries.Count > 0,
            "자동 출력 활성 100년 역사서 feed");

        world.AdvanceTo(GameCalendar.FromYears(303));
        runner.SetWorldFeedCaptureEnabled(false);
        TestAssert.Equal(0, runner.DrainWorldEventFeed().Events.Count,
            "자동 출력 해제 시 대기 이벤트 즉시 해제");
        TestAssert.Equal(0, runner.DrainWorldCenturyChronicleFeed().Entries.Count,
            "자동 출력 해제 시 대기 역사서 즉시 해제");
    }
}
