using Chronicles.Core;
using Chronicles.Simulation;

internal static class OrganizationCoreTests
{
    public static void Run()
    {
        FoundingGatesMembershipsAndRights();
        SuccessionMergeAtomicityAndPersistence();
        Schema25MigratesToEmptyOrganizationCore();
    }

    private static void FoundingGatesMembershipsAndRights()
    {
        var store = new OrganizationStore(80);
        var invalid = store.TryFound(
            CreateSectEvidence(0, 1, RealmStage.FoundationEstablishment),
            tick: 10);
        TestAssert.Equal(OrganizationCommandStatus.GateNotMet, invalid.Status, "금단경 미만 종문 창설 거부");
        TestAssert.Equal(0, store.Count, "실패 창설 원자성");

        var sects = new OrganizationId[3];
        for (var index = 0; index < sects.Length; index++)
        {
            var founder = index * 10;
            var result = store.TryFound(
                CreateSectEvidence(founder, founder + 1, RealmStage.GoldenCore),
                tick: 100 + index);
            TestAssert.True(result.IsApplied, $"종문 {index + 1} 창설");
            TestAssert.Equal(OrganizationRules.SectFoundingSpiritStoneCost, result.SpiritStoneCost, "종문 자원 문턱");
            sects[index] = result.OrganizationId!.Value;
        }

        var duplicate = store.TryFound(
            CreateSectEvidence(0, 31, RealmStage.GoldenCore),
            tick: 200);
        TestAssert.Equal(
            OrganizationCommandStatus.MembershipConflict,
            duplicate.Status,
            "같은 조직 종류 중복 소속 거부");

        var clan = store.TryFound(CreateClanEvidence(founder: 0, memberStart: 31), tick: 210);
        TestAssert.True(clan.IsApplied, "종문과 가문 동시 소속 허용");
        var dynasty = store.TryFound(
            CreateDynastyEvidence(founder: 0, sects),
            tick: 220);
        TestAssert.True(dynasty.IsApplied, "실제 창건 가문·봉신 3개·주 권리 왕조 창건");

        var names = Enumerable.Range(0, 80).Select(index => $"인물{index}").ToArray();
        var snapshot = store.CreateSnapshot(names);
        TestAssert.Equal(3, snapshot.ActiveSects, "활성 종문 수");
        TestAssert.Equal(1, snapshot.ActiveClans, "활성 가문 수");
        TestAssert.Equal(1, snapshot.ActiveDynasties, "활성 왕조 수");
        TestAssert.True(!snapshot.CompositePowerRankingAvailable, "미승인 종합 세력 순위 DataUnavailable");
        var dynastySnapshot = snapshot.Organizations.Single(item => item.Kind == OrganizationKind.Dynasty);
        TestAssert.Equal(3, dynastySnapshot.Diplomacy.Count, "왕조 봉신 관계 실제 연결");
        TestAssert.True(
            dynastySnapshot.Rights.Any(right => right.Kind == OrganizationRightKind.Territory),
            "왕조 실제 주 영지 권리");
    }

    private static void SuccessionMergeAtomicityAndPersistence()
    {
        var store = new OrganizationStore(80);
        var first = store.TryFound(
            CreateSectEvidence(0, 1, RealmStage.GoldenCore),
            tick: 100).OrganizationId!.Value;
        var second = store.TryFound(
            CreateSectEvidence(10, 11, RealmStage.GoldenCore),
            tick: 101).OrganizationId!.Value;
        var clan = store.TryFound(CreateClanEvidence(30, 31), tick: 102).OrganizationId!.Value;

        store.HandleLifeEnded(
            0,
            tick: 200,
            candidate => candidate != 0,
            candidate => 10_000L - candidate);
        var names = Enumerable.Range(0, 80).Select(index => $"인물{index}").ToArray();
        var afterDeath = store.CreateSnapshot(names);
        var succeeded = afterDeath.Organizations.Single(item => item.Id == first);
        TestAssert.Equal(new AgentId(1), succeeded.LeaderAgentId!.Value, "결정론적 후계자 승계");
        TestAssert.True(
            succeeded.History.Any(item => item.Kind == OrganizationHistoryKind.LeaderSucceeded),
            "승계 역사 기록");

        var revisionBeforeFailure = store.AuthorityRevision;
        var failedMerge = store.TryMerge(first, clan, tick: 210);
        TestAssert.Equal(OrganizationCommandStatus.InvalidTransition, failedMerge.Status, "종류가 다른 조직 병합 거부");
        TestAssert.Equal(revisionBeforeFailure, store.AuthorityRevision, "실패 saga 권위 revision 불변");

        var merge = store.TryMerge(first, second, tick: 220);
        TestAssert.True(merge.IsApplied, "같은 종류 조직 원자 병합");
        var afterMerge = store.CreateSnapshot(names);
        TestAssert.Equal(1, afterMerge.ActiveSects, "병합 후 활성 종문 수");
        TestAssert.Equal(
            OrganizationStatus.Merged,
            afterMerge.Organizations.Single(item => item.Id == second).Status,
            "피병합 조직 종료 상태");

        var checkpoint = store.CreateCheckpoint();
        TestAssert.True(OrganizationStore.IsValidCheckpoint(80, checkpoint), "조직 체크포인트 무결성");
        var restored = OrganizationStore.Restore(80, checkpoint);
        ulong firstHash = 14695981039346656037UL;
        ulong secondHash = 14695981039346656037UL;
        store.AddStateHash(ref firstHash);
        restored.AddStateHash(ref secondHash);
        TestAssert.Equal(firstHash, secondHash, "조직 권위 저장 복원 hash");
        TestAssert.Equal(store.AuthorityRevision, restored.AuthorityRevision, "조직 revision 복원");
    }

    private static void Schema25MigratesToEmptyOrganizationCore()
    {
        var world = World.Create(26, 32);
        var current = world.CreateCheckpoint();
        var legacy = current with
        {
            SchemaVersion = 25,
            Organizations = null,
        };
        var restored = World.Restore(legacy);
        var snapshot = restored.CreateSnapshot(
            rankingCount: 1,
            eventCount: 0,
            sections: ObservationSections.Organization,
            worldChronicleCount: 0);
        TestAssert.Equal(0, snapshot.Organizations!.Organizations.Count, "schema 25는 빈 조직 상태로 이식");
        TestAssert.Equal(26, restored.CreateCheckpoint().SchemaVersion, "복원 후 schema 26 저장");
    }

    private static OrganizationFoundingEvidence CreateSectEvidence(
        int founder,
        int memberStart,
        RealmStage realm)
    {
        var members = Enumerable.Range(memberStart, 8).ToArray();
        return new OrganizationFoundingEvidence(
            OrganizationKind.Sect,
            $"종문-{founder}",
            founder,
            new PersonIdentityId(checked((ulong)founder + 1)),
            new SoulIdentityId(checked((ulong)founder + 1)),
            realm,
            20_000,
            6_000,
            6_000,
            6_000,
            6_000,
            0,
            0,
            true,
            false,
            false,
            false,
            WorldGeographyRules.GetPoint(2, 22).NodeId,
            members,
            members.Select(member => new PersonIdentityId(checked((ulong)member + 1))).ToArray(),
            members.Select(member => new SoulIdentityId(checked((ulong)member + 1))).ToArray(),
            0,
            Array.Empty<OrganizationId>());
    }

    private static OrganizationFoundingEvidence CreateClanEvidence(int founder, int memberStart)
    {
        var members = Enumerable.Range(memberStart, 8).ToArray();
        return new OrganizationFoundingEvidence(
            OrganizationKind.Clan,
            $"가문-{founder}",
            founder,
            new PersonIdentityId(checked((ulong)founder + 1)),
            new SoulIdentityId(checked((ulong)founder + 1)),
            RealmStage.GoldenCore,
            8_000,
            0,
            0,
            6_000,
            0,
            0,
            0,
            false,
            true,
            false,
            false,
            WorldGeographyRules.GetPoint(2, 26).NodeId,
            members,
            members.Select(member => new PersonIdentityId(checked((ulong)member + 1))).ToArray(),
            members.Select(member => new SoulIdentityId(checked((ulong)member + 1))).ToArray(),
            8,
            Array.Empty<OrganizationId>());
    }

    private static OrganizationFoundingEvidence CreateDynastyEvidence(
        int founder,
        OrganizationId[] vassals) => new(
            OrganizationKind.Dynasty,
            "오천왕조",
            founder,
            new PersonIdentityId(checked((ulong)founder + 1)),
            new SoulIdentityId(checked((ulong)founder + 1)),
            RealmStage.GoldenCore,
            120_000,
            0,
            0,
            6_000,
            0,
            7_000,
            1,
            false,
            false,
            true,
            true,
            WorldGeographyRules.GetProvince(WorldGeographyRules.OcheonProvinceId).NodeId,
            Array.Empty<int>(),
            Array.Empty<PersonIdentityId>(),
            Array.Empty<SoulIdentityId>(),
            0,
            vassals);
}
