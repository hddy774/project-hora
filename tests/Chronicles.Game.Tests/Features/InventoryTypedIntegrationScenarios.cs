using Chronicles.Game.Inventory;

namespace Chronicles.Game.Tests.Features;

public sealed class InventoryTypedPublicReferenceScenario : IFeatureScenario
{
    public string Name => "inventory typed integration: 실제 typed factory를 통과한 두 공개 전체 transcript·저장 속행 일치";
    public void Run()
    {
        new InventoryOwnedWorldReferenceScenario(true, true).Run();
        new InventoryOwnedEcologyReferenceScenario(true, true).Run();
    }
}

public sealed class InventoryTypedWorldResumeScenario : IFeatureScenario
{
    public string Name => "inventory typed integration: instance 선택·Execute 전파·실제 counter와 Restore 기본 JSON 보존";
    public void Run()
    {
        var world = InventoryWorldFixture.Create(119); var actor = world.Checkpoint().People[0].Id;
        InventoryFixture.Check(world.InventoryProjectionEnabled && !world.InventoryTypedCopyExperimentEnabled && world.InventoryTypedCopySnapshots == 0, "일반 world가 typed 실험을 기본 채택했습니다.");
        world.EnableInventoryTypedCopyForExperiment(); InventoryWorldFixture.Accept(world, new("typed-control", actor, "possess"));
        var request = new GameCommand("typed-real-gather", actor, "gather", Data: "food"); InventoryWorldFixture.Accept(world, request);
        var state = world.Checkpoint(); var job = state.Inventory.Jobs.Single(j => j.Actor == actor && j.Status == "processing");
        InventoryFixture.Check(world.InventoryTypedCopyExperimentEnabled && world.InventoryTypedCopySnapshots > 0 && world.InventoryProjectionSnapshots == world.InventoryTypedCopySnapshots,
            "Execute 후보가 실제 typed snapshot 경로를 사용하지 않았습니다.");
        var bytes = GameCodec.Encode(world); var json = GameCodec.Decode(bytes); var typed = GameWorld.Restore(state);
        InventoryFixture.Check(json.InventoryProjectionEnabled && !json.InventoryTypedCopyExperimentEnabled && !typed.InventoryTypedCopyExperimentEnabled &&
            json.InventoryTypedCopySnapshots == 0 && typed.InventoryTypedCopySnapshots == 0, "typed mode·counter가 저장/Restore로 노출됐습니다.");
        typed.EnableInventoryTypedCopyForExperiment();
        foreach (var candidate in new[] { world, json, typed }) candidate.AdvanceTo(job.Due);
        InventoryFixture.Check(world.StateHash() == json.StateHash() && world.StateHash() == typed.StateHash() && json.InventoryTypedCopySnapshots == 0 && typed.InventoryTypedCopySnapshots > 0 &&
            GameCodec.Encode(world).SequenceEqual(GameCodec.Encode(json)), "typed/JSON 실제 작업·저장·재개 결과가 다릅니다.");
        var before = world.StateHash(); InventoryWorldFixture.Accept(world, request); InventoryFixture.Check(world.StateHash() == before, "typed 작업 재전송이 실물을 다시 소비했습니다.");
        world.EnableInventoryProjectionForExperiment();
        InventoryFixture.Check(world.InventoryProjectionEnabled && !world.InventoryTypedCopyExperimentEnabled && world.StateHash() == before, "기존 JSON opt-in이나 실제 상태를 변경했습니다.");
    }
}

public sealed class InventoryTypedOwnerContractScenario : IFeatureScenario
{
    public string Name => "inventory typed integration: opaque 전이·영수증·재인계·실패와 동일15필드 stale 계약";
    public void Run()
    {
        new InventoryProjectionStaleScenario(true).Run();
        var json = OwnedInventoryFixture.Create(); var typed = GameCodec.Clone(json);
        using (var expected = InventoryEngine.OwnProjected(json))
        using (var actual = InventoryEngine.OwnTypedProjectedForExperiment(typed))
        {
            long Apply(InventoryCommand command)
            {
                var a = expected.Apply(command); var b = actual.Apply(command);
                InventoryFixture.Check(a.Accepted && a == b, "typed wrapper의 실제 명령·응답·재전송 결과가 다릅니다: " + command.Id); return a.Subject;
            }
            var original = OwnedInventoryFixture.Snapshot(typed);
            var split = Apply(new SplitInventoryItem("typed-split", 1, typed.Items[0].Id, 2));
            Apply(new MergeStacks("typed-merge", 1, typed.Items[0].Id, split));
            var job = Apply(new StartProduction("typed-production", 1, "pill", 2));
            Apply(new BeginProduction("typed-processing", 1, job)); Apply(new BeginProduction("typed-processing", 1, job));
            actual.AttachKnowledgeActivity(job, 17); expected.AttachKnowledgeActivity(job, 17);
            InventoryFixture.Check(actual.Job(job) == expected.Job(job) && original == OwnedInventoryFixture.Snapshot(typed), "typed wrapper가 값 DTO나 입력 원자성을 바꿨습니다.");
            expected.CommitTo(json); actual.CommitTo(typed);
            InventoryFixture.Check(OwnedInventoryFixture.Snapshot(json) == OwnedInventoryFixture.Snapshot(typed), "typed opaque 인계가 실제 결과를 바꿨습니다.");
            OwnedInventoryFixture.Closed(actual, typed);
        }
        typed = OwnedInventoryFixture.Create(); var unchanged = OwnedInventoryFixture.Snapshot(typed);
        using var failed = InventoryEngine.OwnTypedProjectedForExperiment(typed);
        var started = failed.Apply(new StartProduction("typed-failed-start", 1, "pill", 2));
        InventoryFixture.Check(started.Accepted && !failed.Apply(new BeginProduction("typed-failed-process", 2, started.Subject)).Accepted && unchanged == OwnedInventoryFixture.Snapshot(typed),
            "typed 후보의 두 번째 처리 실패가 실물·할당자·원장을 유출했습니다.");
        OwnedInventoryFixture.Closed(failed, typed);
    }
}
