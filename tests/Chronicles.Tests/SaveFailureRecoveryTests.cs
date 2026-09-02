using Chronicles.Application;
using Chronicles.Simulation;

internal static class SaveFailureRecoveryTests
{
    public static void Run()
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            "cmip-save-failure-recovery",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        try
        {
            var failingRoot = Path.Combine(testRoot, "root-is-a-file");
            File.WriteAllText(failingRoot, "not a directory");
            var healthyRoot = Path.Combine(testRoot, "healthy-root");

            using var session = GameApplicationSession.Create(0x23_00_23UL, 64);
            session.SetPaused(true);
            session.SetSaveRoot(failingRoot);
            var hashBeforeFailure = GetStateHash(session);

            SaveOperationException? failure = null;
            try
            {
                _ = session.SaveSlotAsync("default").GetAwaiter().GetResult();
            }
            catch (SaveOperationException exception)
            {
                failure = exception;
            }

            TestAssert.True(failure is not null, "파일인 저장 root는 typed 저장 실패여야 함");
            TestAssert.Equal(SaveOperationKind.Storage, failure!.Kind, "파일 root 저장 실패 종류");
            TestAssert.True(failure.IsRecoverable, "파일 root 저장 실패 복구 가능");
            TestAssert.Equal(hashBeforeFailure, GetStateHash(session), "저장 실패 뒤 권위 world hash 보존");
            TestAssert.True(session.IsPaused, "저장 실패 뒤 기존 일시정지 상태 보존");

            session.SetSaveRoot(healthyRoot);
            var saved = session.SaveSlotAsync("slot-02").GetAwaiter().GetResult();
            TestAssert.Equal("slot-02", saved.SlotId, "같은 세션의 다른 슬롯 재저장");
            TestAssert.Equal(hashBeforeFailure, saved.StateHash, "재저장 state hash");
            TestAssert.Equal(hashBeforeFailure, GetStateHash(session), "재저장 뒤 권위 world hash 보존");
            TestAssert.True(session.IsPaused, "재저장 뒤 기존 일시정지 상태 보존");

            var loaded = session.LoadSlotAsync("slot-02").GetAwaiter().GetResult();
            TestAssert.Equal(saved.StateHash, loaded.StateHash, "재저장 슬롯 불러오기 hash");
            TestAssert.Equal(hashBeforeFailure, GetStateHash(session), "재저장 슬롯 복원 뒤 world hash");
            TestAssert.True(session.IsPaused, "재저장 슬롯 복원 뒤 기존 일시정지 상태 보존");
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    private static ulong GetStateHash(GameApplicationSession session) =>
        World.Restore(session.CreateCheckpoint()).ComputeStateHash();
}
