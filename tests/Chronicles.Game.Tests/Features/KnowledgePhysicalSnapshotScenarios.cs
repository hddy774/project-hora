using System.Text.Json;
using Chronicles.Game.Knowledge;

namespace Chronicles.Game.Tests.Features;

public sealed class KnowledgePhysicalSnapshotScenario : IFeatureScenario
{
    public string Name => "INV-007/024 knowledge physical snapshot: 전체 사본의 매체·접근권과 순서·동기화 시점·중첩 배열 격리가 같다";

    public void Run()
    {
        var authority = KnowledgeFixture.Fresh();
        var rights = KnowledgeRights.Read | KnowledgeRights.Copy | KnowledgeRights.Teach;
        KnowledgeFixture.Ok(authority.BindMedium(303, 1, 1, 0, KnowledgeMediumKind.Book, 4,
            rights, [3, 1], false, 0, "physical:303"));
        KnowledgeFixture.Ok(authority.BindMedium(202, 1, 2, 1, KnowledgeMediumKind.JadeSlip, 6,
            rights, [], false, 0, "physical:202"));
        KnowledgeFixture.Ok(authority.BindMedium(404, 1, 1, 0, KnowledgeMediumKind.Inscription, 0,
            KnowledgeRights.Read, [2], true, 0, "physical:404"));
        KnowledgeFixture.Ok(authority.GrantAccess(101, 1, 2, KnowledgeRights.Read | KnowledgeRights.Copy,
            9, 0, "physical:grant101"));
        KnowledgeFixture.Ok(authority.GrantAccess(303, 1, 3, KnowledgeRights.Read, 2, 0, "physical:expiring"));
        KnowledgeFixture.Ok(authority.GrantAccess(202, 2, 4, KnowledgeRights.Read, 10, 0, "physical:grant202"));
        KnowledgeFixture.Study(authority, 101, [1], 0, 2, "physical:study");
        KnowledgeFixture.Ok(authority.SynchronizeMedium(404, 1, 0, 0, [2], true, true, 2, "physical:destroy404"));

        var before = authority.Checkpoint();
        var initialAuthority = KnowledgeFixture.Json(authority);
        var first = authority.PhysicalCheckpoint(includeAccess: false);
        var firstAgain = authority.PhysicalCheckpoint(includeAccess: false);
        KnowledgeFixture.Check(Json(first.Media) == Json(before.Media) && first.Access.Length == 0 &&
            first.Media.Select(m => m.ItemId).SequenceEqual(new long[] { 101, 303, 202, 404 }) &&
            first.Media[^1].Destroyed && before.Activities.Count > 0 && before.Memories.Count > 0,
            "첫 물리 사본이 전체 사본의 매체 순서/결손/종결 기록을 바꾸거나 불필요한 접근권을 포함했습니다.");
        KnowledgeFixture.Check(!ReferenceEquals(first.Media, firstAgain.Media) &&
            first.Media.Zip(firstAgain.Media).All(pair => !ReferenceEquals(pair.First, pair.Second)) &&
            !ReferenceEquals(first.Media[1].MissingChapters, firstAgain.Media[1].MissingChapters) &&
            KnowledgeFixture.Json(authority) == initialAuthority,
            "물리 조회가 권위/이력 또는 독립 매체/결손 배열을 공유했습니다.");

        // Match SettleKnowledge's two read boundaries: physical synchronization occurs between them.
        KnowledgeFixture.Ok(authority.SynchronizeMedium(303, 2, 2, 1, [2, 3], true, false, 2, "physical:sync303"));
        KnowledgeFixture.Ok(authority.RevokeAccess(101, 1, 2, 2, "physical:revoke101"));
        KnowledgeFixture.Ok(authority.GrantAccess(202, 2, 5, KnowledgeRights.Read, 12, 2, "physical:new202"));
        var after = authority.Checkpoint();
        var second = authority.PhysicalCheckpoint();
        var secondAgain = authority.PhysicalCheckpoint();
        var expected = Json(new { after.Media, after.Access });
        KnowledgeFixture.Check(Json(new { second.Media, second.Access }) == expected &&
            first.Media[1].OwnerId == 1 && first.Media[1].MissingChapters.SequenceEqual(new[] { 1, 3 }) &&
            second.Media[1].OwnerId == 2 && second.Media[1].MissingChapters.SequenceEqual(new[] { 2, 3 }) &&
            second.Access.Any(a => a.Revoked) && second.Access.Any(a => a.ExpiresMonth == 2) &&
            second.Access.Any(a => a.PersonId == 5),
            "둘째 사본이 afterSync의 전체 매체/접근권 값·개정/출처·만료/철회 기록과 다르거나 첫 사본을 소급 변경했습니다.");
        KnowledgeFixture.Check(!ReferenceEquals(second.Access, secondAgain.Access) &&
            second.Access.Zip(secondAgain.Access).All(pair => !ReferenceEquals(pair.First, pair.Second)) &&
            second.Media.Zip(secondAgain.Media).All(pair => !ReferenceEquals(pair.First, pair.Second)),
            "두 물리 사본이 mutable 배열이나 record 참조를 공유했습니다.");

        var unchanged = KnowledgeFixture.Json(authority);
        first.Media[0] = first.Media[0] with { OwnerId = 999 };
        first.Media[1].MissingChapters[0] = 999;
        second.Media[1].MissingChapters[0] = 888;
        second.Media[0] = second.Media[0] with { Place = 777 };
        second.Access[0] = second.Access[0] with { PersonId = 666 };
        after.Media[1].MissingChapters[0] = 555;
        after.Access.Clear();
        var current = authority.PhysicalCheckpoint();
        KnowledgeFixture.Check(KnowledgeFixture.Json(authority) == unchanged &&
            Json(new { current.Media, current.Access }) == expected &&
            Json(new { secondAgain.Media, secondAgain.Access }) == expected &&
            firstAgain.Media[1].MissingChapters.SequenceEqual(new[] { 1, 3 }),
            "물리 사본 또는 공개 전체 사본 변조가 권위/다른 사본/다음 조회로 유출됐습니다.");
        KnowledgeFixture.Check(authority.ViewAccessible(2, 0, 2).Rows.All(m => m.ItemId != 101) &&
            authority.ViewAccessible(4, 1, 2).Rows.Single().ItemId == 202 &&
            authority.ViewAccessible(5, 1, 2).Rows.Single().ItemId == 202,
            "사본 조회/변조가 실제 철회·접근권을 바꿨습니다.");
        var restored = KnowledgeFixture.RoundTrip(authority);
        var restoredPhysical = restored.PhysicalCheckpoint();
        KnowledgeFixture.Check(Json(new { restoredPhysical.Media, restoredPhysical.Access }) == expected,
            "공개 복원 뒤 물리 투영이 같은 전체 필드와 다릅니다.");
        KnowledgeAuthority.Validate(authority.Checkpoint());
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
}
