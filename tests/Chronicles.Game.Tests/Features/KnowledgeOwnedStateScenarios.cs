using System.Text.Json;
using Chronicles.Game.Knowledge;

namespace Chronicles.Game.Tests.Features;

public sealed class KnowledgeOwnedAuthorityIsolationScenario : IFeatureScenario
{
    public string Name => "INV-004/024 knowledge world-owned state: 내부 전이는 즉시 보이고 공개 사본·입력·조회 배열은 분리된다";

    public void Run()
    {
        var owned = KnowledgeFixture.Fresh().Checkpoint();
        var activities = owned.Activities;
        var authority = KnowledgeAuthority.AttachWorldOwned(owned);
        var id = KnowledgeFixture.Ok(authority.StartStudy(
            new("owned-study", KnowledgeFixture.One, 101, [1], 0, 2)));
        KnowledgeFixture.Check(ReferenceEquals(activities, owned.Activities) && activities.Single().Id == id,
            "내부 전이가 독점 소유 상태에 즉시 반영되지 않았다.");
        KnowledgeFixture.Rejected(authority, KnowledgeStatus.Busy, () => authority.StartStudy(
            new("busy-study", KnowledgeFixture.One, 101, [1], 0, 1)));
        var lookup = authority.FindActivity(id)!;
        lookup.Chapters[0] = 99;
        KnowledgeFixture.Check(authority.FindActivity(id)!.Chapters.SequenceEqual([1]),
            "조회 배열을 바꿔 내부 행동을 수정했다.");
        KnowledgeFixture.Ok(authority.Complete(new("owned-study.done", id, 2, [KnowledgeFixture.One])));
        KnowledgeFixture.Check(activities.Single().Status == KnowledgeActivityStatus.Completed && owned.Memories.Count == 1,
            "실제 완료가 세계 소유 상태에 즉시 반영되지 않았다.");
        VerifyDetachedCopies(authority);
        VerifyInvalidAttachment(owned);
    }

    private static void VerifyDetachedCopies(KnowledgeAuthority authority)
    {
        var snapshot = authority.Checkpoint();
        var restored = KnowledgeAuthority.Restore(snapshot);
        var expected = KnowledgeFixture.Json(authority);
        snapshot.Works[0].Chapters[0] = snapshot.Works[0].Chapters[0] with { Title = "외부 수정" };
        snapshot.Editions[0].Chapters[0] = new(1, "외부 주장");
        snapshot.Activities[0].Chapters[0] = 99;
        snapshot.Evidence[^1].Chapters[0] = 99;
        snapshot.Memories.Clear();
        KnowledgeFixture.Check(KnowledgeFixture.Json(authority) == expected &&
            KnowledgeFixture.Json(restored) == expected, "공개 사본이나 Restore 입력이 권위와 공유됐다.");
    }

    private static void VerifyInvalidAttachment(KnowledgeState owned)
    {
        var invalid = KnowledgeAuthority.Restore(owned).Checkpoint();
        invalid.Schema = 2;
        var before = JsonSerializer.Serialize(invalid);
        try
        {
            _ = KnowledgeAuthority.AttachWorldOwned(invalid);
            throw new InvalidOperationException("잘못된 내부 입력을 수용했다.");
        }
        catch (InvalidDataException)
        {
            KnowledgeFixture.Check(JsonSerializer.Serialize(invalid) == before, "입력 검증 예외가 상태를 바꿨다.");
        }
    }
}

public sealed class KnowledgeOwnedWorldCacheScenario : IFeatureScenario
{
    public string Name => "INV-004/006 knowledge world-owned state: 실제 명령 채택·월 완료·저장재개에서 캐시와 외부 상태가 분리된다";

    public void Run()
    {
        var world = KnowledgeWorldFixture.Fresh();
        var opening = world.Checkpoint();
        var actor = opening.People[0].Id;
        var book = opening.Items.First(i => i.Owner == actor && i.Definition == "path.1.0").Id;
        KnowledgeWorldFixture.Accept(world, "owned-control", "possess", actor);
        KnowledgeWorldFixture.Accept(world, "owned-first-study", "study", actor, book);
        world.AdvanceTo(1);
        var restored = GameCodec.Decode(GameCodec.Encode(world));
        var input = world.Checkpoint();
        var fromInput = GameWorld.Restore(input);
        var expected = world.StateHash();
        input.Knowledge.Works[0].Chapters[0] = input.Knowledge.Works[0].Chapters[0] with { Title = "외부" };
        input.Knowledge.Activities[0].Chapters[0] = 999;
        input.Knowledge.Memories.Clear();
        KnowledgeFixture.Check(world.StateHash() == expected && fromInput.StateHash() == expected,
            "world Checkpoint 또는 Restore 입력 수정이 세계에 전파됐다.");
        foreach (var candidate in new[] { world, restored, fromInput })
        {
            KnowledgeWorldFixture.Accept(candidate, "owned-next-study", "study", actor, book);
            candidate.AdvanceTo(2);
        }
        KnowledgeFixture.Check(world.StateHash() == restored.StateHash() && world.StateHash() == fromInput.StateHash(),
            "후보 상태 교체 뒤 이전 캐시를 쓰거나 저장재개 결과가 달라졌다.");
        var completed = world.Checkpoint().Knowledge.Activities;
        KnowledgeFixture.Check(completed.Count == 2 && completed.All(a => a.Status == KnowledgeActivityStatus.Completed),
            "서로 다른 실제 학습 완료가 유실되거나 중복됐다.");
    }
}

public sealed class KnowledgeOwnedWorldAtomicityScenario : IFeatureScenario
{
    public string Name => "INV-004/005/007 knowledge world-owned state: 거부·재전송·커밋 전 실제 예외가 원래 세계를 바꾸지 않는다";

    public void Run()
    {
        var world = KnowledgeWorldFixture.Fresh();
        var opening = world.Checkpoint();
        var actor = opening.People[0].Id;
        var book = opening.Items.First(i => i.Owner == actor && i.Definition == "path.1.0").Id;
        KnowledgeWorldFixture.Accept(world, "atomic-control", "possess", actor);
        var command = new GameCommand("atomic-study", actor, "study", book);
        KnowledgeFixture.Check(world.Execute(command).Accepted, "첫 학습 명령이 거부됐다.");
        var expected = world.StateHash();
        KnowledgeFixture.Check(world.Execute(command).Accepted && world.StateHash() == expected,
            "같은 ID 재전송이 내부 권위를 다시 수정했다.");
        KnowledgeFixture.Check(!world.Execute(command with { Id = "atomic-busy" }).Accepted &&
            !world.Execute(command with { Data = "다른 내용" }).Accepted && world.StateHash() == expected,
            "busy 또는 충돌 재전송 거부가 상태·RNG·ID를 바꿨다.");
        VerifyPhysicalRejection();
        VerifyCommitException(opening, actor, book);
    }

    private static void VerifyPhysicalRejection()
    {
        var state = KnowledgeWorldFixture.Fresh().Checkpoint();
        var actor = state.People[0].Id;
        var book = state.Items.First(i => i.Owner == actor && i.Definition == "path.1.0").Id;
        KnowledgeWorldFixture.AddStock(state, actor, "paper", 1);
        KnowledgeWorldFixture.AddStock(state, actor, "ink", 1);
        // 실제 저장 스키마가 허용하는 타 영혼 결속 재료: 접근량 검사는 통과하고 예약 단계에서 거부된다.
        state.Items.Last(i => i.Definition == "paper").BoundSoul = state.People[1].SoulId;
        var world = GameWorld.Restore(state);
        KnowledgeWorldFixture.Accept(world, "physical-control", "possess", actor);
        world.AdvanceTo(1);
        var restored = GameCodec.Decode(GameCodec.Encode(world));
        var expected = world.StateHash();
        var failure = world.Execute(new("physical-rejection", actor, "experiment", book));
        KnowledgeFixture.Check(!failure.Accepted && failure.Message.Contains("Ingredient belongs", StringComparison.Ordinal) &&
            world.StateHash() == expected, "지식 실습 시작 뒤 물리 예약 거부가 원래 상태에 누출됐다.");
        foreach (var candidate in new[] { world, restored })
        {
            KnowledgeWorldFixture.Accept(candidate, "after-physical-rejection", "study", actor, book);
            candidate.AdvanceTo(2);
        }
        KnowledgeFixture.Check(world.StateHash() == restored.StateHash(),
            "물리 거부 후보의 옛 인덱스가 다음 실제 학습에 재사용됐다.");
    }

    private static void VerifyCommitException(GameState opening, long actor, long book)
    {
        // 유효한 경계값에서 후보가 실제 학습을 시작한 뒤 revision overflow를 커밋 불변식이 거부한다.
        // 이는 공개 후보 예외 원자성 검사이며 정상 세계의 정책이나 성공 조건을 바꾸지 않는다.
        opening.Revision = long.MaxValue;
        var world = GameWorld.Restore(opening);
        var expected = world.StateHash();
        try
        {
            _ = world.Execute(new("overflow-study", actor, "study", book));
            throw new InvalidOperationException("실제 커밋 예외가 발생하지 않았다.");
        }
        catch (InvalidDataException error) when (error.Message.StartsWith("INV-004:", StringComparison.Ordinal))
        {
            KnowledgeFixture.Check(world.StateHash() == expected && world.Checkpoint().Knowledge.Activities.Count == 0,
                "후보의 실제 예외가 원래 지식·ID·RNG를 수정했다.");
        }
    }
}
