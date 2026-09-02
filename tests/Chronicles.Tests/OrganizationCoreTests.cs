using Chronicles.Core;
using Chronicles.Simulation;

internal static class OrganizationCoreTests
{
    public static void Run()
    {
        FoundingGatesMembershipsAndRights();
        PreparationPlansPersistAndUseFiniteOre();
        MembershipEconomyDiplomacySplitAndReconnection();
        SuccessionMergeAtomicityAndPersistence();
        OrganizationHistoryIsBounded();
        LongRunWorldProducesOrganizations();
        Schema25MigratesToEmptyOrganizationCore();
    }

    private static void OrganizationHistoryIsBounded()
    {
        var store = new OrganizationStore(80);
        var organizationId = store.TryFound(
            CreateSectEvidence(0, 1, RealmStage.GoldenCore),
            tick: 100).OrganizationId!.Value;
        for (var index = 0; index < 150; index++)
        {
            TestAssert.True(
                store.TryAdjustTreasury(organizationId, 0, 1, 101 + index).IsApplied,
                $"조직 역사 상한 입력 {index}");
        }

        var checkpoint = store.CreateCheckpoint();
        var history = checkpoint.Organizations.Single().History;
        TestAssert.Equal(
            OrganizationRules.HistoryRetentionLimit,
            history.Length,
            "조직별 최근 역사 100건 상한");
        TestAssert.Equal(
            checkpoint.HistorySequence,
            history[^1].Sequence,
            "조직 역사 최신 sequence 보존");
        TestAssert.Equal(
            checkpoint.HistorySequence - OrganizationRules.HistoryRetentionLimit + 1,
            history[0].Sequence,
            "조직 역사 오래된 항목 제거");

        var oversizedHistory = Enumerable.Range(1, 120)
            .Select(sequence => new OrganizationHistoryEntry(
                sequence,
                100 + sequence,
                OrganizationHistoryKind.TreasuryAdjusted,
                0,
                null,
                1))
            .ToArray();
        var legacy = checkpoint with
        {
            HistorySequence = oversizedHistory[^1].Sequence,
            Organizations =
            [checkpoint.Organizations[0] with { History = oversizedHistory }],
        };
        var migrated = OrganizationStore.Restore(80, legacy, trimHistory: true).CreateCheckpoint();
        TestAssert.Equal(
            OrganizationRules.HistoryRetentionLimit,
            migrated.Organizations[0].History.Length,
            "기존 무제한 조직 역사 복원 시 최근 100건 이관");
        TestAssert.Equal(21L, migrated.Organizations[0].History[0].Sequence, "이관 최초 sequence");
        TestAssert.Equal(120L, migrated.Organizations[0].History[^1].Sequence, "이관 최신 sequence");
    }

    private static void PreparationPlansPersistAndUseFiniteOre()
    {
        var store = new OrganizationStore(1_000);
        var pledged = Enumerable.Range(1, OrganizationRules.SectFounderMinimumPledgedMembers)
            .ToArray();
        TestAssert.True(store.TryRegisterPreparationPlan(0, pledged), "종문 준비 계획 등록");
        TestAssert.True(store.TryDepositFoundingReserve(0, 1_234), "종문 준비 적립");
        var checkpoint = store.CreateCheckpoint();
        TestAssert.True(OrganizationStore.IsValidCheckpoint(1_000, checkpoint), "종문 준비 체크포인트 무결성");
        var restored = OrganizationStore.Restore(1_000, checkpoint);
        TestAssert.True(
            restored.TryGetPreparationSponsor(1, out var sponsor) && sponsor == 0,
            "서약자의 준비 sponsor 복원");
        TestAssert.Equal(1_234L, restored.GetFoundingReserve(0), "창설 적립금 복원");

        var baseline = new CityMarket(1).CreateCheckpoint();
        var market = new CityMarket(
            baseline with
            {
                TreasurySpiritStones = 500_000,
                SpiritStoneReserveTarget = 500_000,
                RemainingSpiritStoneOre = 100,
            },
            WorldCheckpoint.CurrentSchemaVersion,
            population: 1);
        TestAssert.True(!market.NeedsSpiritStoneMining, "도시 자체 채굴 수요 없음");
        TestAssert.True(
            market.TryCompleteOrganizationPreparationMining(
                RealmStage.QiRefining,
                preparationReserve: 0,
                out var reward,
                out var extracted,
                maximumExtractable: 30),
            "조직 준비 수요로 유한 광맥 채굴");
        TestAssert.Equal(30L, extracted, "Atlas 상한만큼만 채굴");
        TestAssert.True(reward > 0, "조직 준비 채굴 보상");

        restored.HandlePreparationLifeEnded(0, candidate => candidate != 0);
        TestAssert.True(
            restored.TryGetPreparationSponsor(2, out var replacement)
            && replacement == 1,
            "창설 준비자 사망 시 결정론적 서약자 승계");
        TestAssert.Equal(1_234L, restored.GetFoundingReserve(1), "준비 적립금 승계");
    }

    private static void LongRunWorldProducesOrganizations()
    {
        var world = World.Create(260920, 1_000);
        world.AdvanceTo(GameCalendar.FromYears(1_200));
        var worldSnapshot = world.CreateSnapshot(
            rankingCount: 1,
            eventCount: 0,
            sections: ObservationSections.Organization | ObservationSections.WorldChronicle,
            worldChronicleCount: 100);
        var snapshot = worldSnapshot.Organizations!;
        var agents = Enumerable.Range(0, world.Population)
            .Select(index => world.GetAgentSnapshot(
                new AgentId(index),
                includeRelations: true))
            .ToArray();
        var goldenCore = agents.Count(agent => agent.RealmStage >= RealmStage.GoldenCore);
        var funded = agents.Count(agent =>
            agent.SpiritStones >= OrganizationRules.SectFoundingSpiritStoneCost);
        var realmAndFunds = agents.Count(agent =>
            agent.RealmStage >= RealmStage.GoldenCore
            && agent.SpiritStones >= OrganizationRules.SectFoundingSpiritStoneCost);
        var maximumRealm = agents.Max(agent => agent.RealmStage);
        var goldenCoreWithAptitudes = agents.Count(agent =>
            agent.RealmStage >= RealmStage.GoldenCore
            && agent.Aptitudes.Mind.Hundredths
                >= OrganizationRules.SectMinimumOrganizationOperationHundredths
            && agent.Aptitudes.Comprehension.Hundredths
                >= OrganizationRules.SectMinimumFoundingAptitudeHundredths);
        var goldenCoreWithSite = agents.Count(agent =>
        {
            if (agent.RealmStage < RealmStage.GoldenCore)
            {
                return false;
            }
            var location = WorldMapRules.GetLocation(agent.LocationId);
            var pointId = checked((ushort)(((1 + (location.Y * 3)) * 10)
                + 1 + (location.X * 3)));
            return WorldGeographyRules.GetPoint(2, pointId).Resources.HasSpiritVein;
        });
        var historicalGoldenCoreBreakthroughs = worldSnapshot.PermanentWorldChronicle.Sum(entry =>
            entry.RealmBreakthroughCounts[(int)RealmStage.GoldenCore]);
        var pledgedReady = agents.Count(agent =>
        {
            var relations = agent.Relations ?? Array.Empty<RelationSnapshot>();
            var eligible = relations.Where(relation =>
                relation.TowardOther.TrustDistrust > 0
                && relation.OtherTowardSelf.TrustDistrust > 0)
                .OrderByDescending(relation =>
                    relation.TowardOther.TrustDistrust
                    + relation.OtherTowardSelf.TrustDistrust)
                .Take(OrganizationRules.SectFounderMinimumPledgedMembers)
                .ToArray();
            return eligible.Length >= OrganizationRules.SectFounderMinimumPledgedMembers
                && eligible.Sum(relation =>
                    (long)relation.TowardOther.TrustDistrust
                    + relation.OtherTowardSelf.TrustDistrust)
                    / (eligible.Length * 2L)
                    >= OrganizationRules.SectMinimumAverageTrustHundredths;
        });
        var maximumRelationCount = agents.Max(agent => agent.Relations?.Count ?? 0);
        var maximumEightRelationAverage = agents.Max(agent =>
        {
            var eligible = (agent.Relations ?? Array.Empty<RelationSnapshot>())
                .OrderByDescending(relation =>
                    relation.TowardOther.TrustDistrust
                    + relation.OtherTowardSelf.TrustDistrust)
                .Take(OrganizationRules.SectFounderMinimumPledgedMembers)
                .ToArray();
            return eligible.Length < OrganizationRules.SectFounderMinimumPledgedMembers
                ? 0
                : checked((int)(eligible.Sum(relation =>
                    (long)relation.TowardOther.TrustDistrust
                    + relation.OtherTowardSelf.TrustDistrust)
                    / (eligible.Length * 2L)));
        });
        var maximumFoundingPool = agents.Max(agent =>
        {
            var relations = agent.Relations ?? Array.Empty<RelationSnapshot>();
            return agent.SpiritStones + relations
                .Take(OrganizationRules.SectFounderMinimumPledgedMembers)
                .Sum(relation => agents[relation.OtherAgentId.Value].SpiritStones);
        });
        var organizationAuthority = world.CreateCheckpoint().Organizations!;
        Console.WriteLine(
            $"ORGANIZATION SOAK: 1,000명·1,200년 · 조직 {snapshot.Organizations.Count:N0} · " +
            $"종문 {snapshot.ActiveSects:N0} · 최근 가입 역사 {snapshot.Organizations.Sum(item => item.History.Count(history => history.Kind == OrganizationHistoryKind.MemberJoined)):N0} · " +
            $"준비 {snapshot.PreparingSectPlans:N0}/{snapshot.TotalFoundingReserves:N0}석 · " +
            $"금단 {goldenCore:N0}(자질 {goldenCoreWithAptitudes:N0}·영맥 {goldenCoreWithSite:N0}) · 최대 {maximumRealm} · 역사 금단 돌파 {historicalGoldenCoreBreakthroughs:N0} · " +
            $"자금 {funded:N0} · 동시 충족 {realmAndFunds:N0} · 서약 관계 {pledgedReady:N0} · " +
            $"최대 관계 {maximumRelationCount:N0} · 상위 8 평균 신뢰 {maximumEightRelationAverage / 100m:0.##} · " +
            $"최대 공동 자금 {maximumFoundingPool:N0}");
        TestAssert.True(
            snapshot.Organizations.Any(item => item.Kind == OrganizationKind.Sect),
            "장기 세계에서 실제 종문 창설 및 생명주기 보존");
        TestAssert.True(
            organizationAuthority.Organizations.Any(item =>
                item.Members.Length > OrganizationRules.SectFounderMinimumPledgedMembers + 1),
            "장기 세계에서 창설 후 AI 가입");
        TestAssert.True(
            organizationAuthority.Organizations.All(item =>
                item.History.Length <= OrganizationRules.HistoryRetentionLimit),
            "장기 세계 조직별 최근 역사 100건 상한");
    }

    private static void MembershipEconomyDiplomacySplitAndReconnection()
    {
        var store = new OrganizationStore(80);
        var first = store.TryFound(
            CreateSectEvidence(0, 1, RealmStage.GoldenCore),
            tick: 100).OrganizationId!.Value;
        var second = store.TryFound(
            CreateSectEvidence(20, 21, RealmStage.GoldenCore),
            tick: 101).OrganizationId!.Value;

        var joined = store.TryJoin(
            first,
            authorizedByAgent: 0,
            joiningAgent: 10,
            new PersonIdentityId(11),
            new SoulIdentityId(11),
            OrganizationRole.Member,
            loyaltyHundredths: 6_500,
            tick: 110);
        TestAssert.True(joined.IsApplied, "지도자 승인 조직 가입");
        var revisionBeforeDenied = store.AuthorityRevision;
        var denied = store.TryJoin(
            first,
            authorizedByAgent: 1,
            joiningAgent: 11,
            new PersonIdentityId(12),
            new SoulIdentityId(12),
            OrganizationRole.Member,
            loyaltyHundredths: 6_500,
            tick: 111);
        TestAssert.Equal(OrganizationCommandStatus.PermissionDenied, denied.Status, "무권한 가입 거부");
        TestAssert.Equal(revisionBeforeDenied, store.AuthorityRevision, "실패 가입 원자성");

        TestAssert.True(
            store.TryChangeRole(first, 0, 10, OrganizationRole.Treasurer, 112).IsApplied,
            "구성원 역할 변경");
        TestAssert.True(store.TryAdjustTreasury(first, 0, 2_000, 113).IsApplied, "조직 재정 증가");
        var revisionBeforeFundsFailure = store.AuthorityRevision;
        TestAssert.Equal(
            OrganizationCommandStatus.InsufficientAssets,
            store.TryAdjustTreasury(first, 0, -50_000, 114).Status,
            "조직 재정 음수 원자 거부");
        TestAssert.Equal(revisionBeforeFundsFailure, store.AuthorityRevision, "실패 재정 원자성");

        TestAssert.True(
            store.TrySetDiplomacy(
                first,
                authorizedByAgent: 0,
                second,
                OrganizationDiplomacyKind.Vassal,
                tick: 120).IsApplied,
            "봉신 외교 양방향 연결");
        var revisionBeforeCycle = store.AuthorityRevision;
        TestAssert.Equal(
            OrganizationCommandStatus.InvalidTransition,
            store.TrySetDiplomacy(
                second,
                authorizedByAgent: 20,
                first,
                OrganizationDiplomacyKind.Vassal,
                tick: 121).Status,
            "봉신 순환 거부");
        TestAssert.Equal(revisionBeforeCycle, store.AuthorityRevision, "실패 외교 원자성");

        var split = store.TrySplit(
            first,
            authorizedByAgent: 0,
            newName: "분파-7",
            newLeaderAgent: 7,
            movingAgents: [7, 8],
            transferredSpiritStones: 1_000,
            transferredRights:
            [
                new OrganizationRightState(
                    OrganizationRightKind.SpiritVein,
                    WorldGeographyRules.GetPoint(2, 22).NodeId,
                    100),
            ],
            tick: 130);
        TestAssert.True(split.IsApplied, "구성원·재정·권리 원자 분열");
        TestAssert.True(
            store.TryGetActiveMembershipOrganization(7, OrganizationKind.Sect, out var splitMembership)
            && splitMembership == split.OrganizationId,
            "분열 구성원 index 이전");

        store.HandleLifeEnded(
            6,
            tick: 140,
            candidate => candidate != 6,
            candidate => 10_000L - candidate);
        TestAssert.True(
            store.TryReconnectSoul(
                first,
                agent: 6,
                previousPersonId: new PersonIdentityId(7),
                reincarnatedPersonId: new PersonIdentityId(1_007),
                soulId: new SoulIdentityId(7),
                tick: 150).IsApplied,
            "동일 Soul 명시적 조직 재연결");

        TestAssert.True(
            store.TryUpdateLoyalty(
                first,
                authorizedByAgent: 10,
                memberAgent: 10,
                replacementLoyaltyHundredths: 2_000,
                tick: 160,
                candidate => true,
                candidate => 10_000L - candidate).IsApplied,
            "충심 붕괴 이탈 전이");
        TestAssert.True(
            !store.HasActiveMembership(10, OrganizationKind.Sect),
            "충심 붕괴 후 활성 소속 해제");

        var names = Enumerable.Range(0, 80).Select(index => $"인물{index}").ToArray();
        var snapshot = store.CreateSnapshot(names);
        var firstSnapshot = snapshot.Organizations.Single(item => item.Id == first);
        var secondSnapshot = snapshot.Organizations.Single(item => item.Id == second);
        TestAssert.True(
            firstSnapshot.Diplomacy.Any(item =>
                item.OtherOrganizationId == second
                && item.Kind == OrganizationDiplomacyKind.Vassal),
            "지배 조직 봉신 방향");
        TestAssert.True(
            secondSnapshot.Diplomacy.Any(item =>
                item.OtherOrganizationId == first
                && item.Kind == OrganizationDiplomacyKind.Overlord),
            "봉신 조직 상위 지배 방향");
        TestAssert.True(
            firstSnapshot.Members.Count(member => member.AgentId == new AgentId(6)) == 2,
            "윤회 재연결 전후 Person membership 보존");
        TestAssert.True(
            firstSnapshot.History.Any(item => item.Kind == OrganizationHistoryKind.SoulReconnected),
            "윤회 재연결 조직 역사");
        TestAssert.True(OrganizationStore.IsValidCheckpoint(80, store.CreateCheckpoint()), "확장 전이 체크포인트 무결성");
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
        TestAssert.Equal(
            WorldCheckpoint.CurrentSchemaVersion,
            restored.CreateCheckpoint().SchemaVersion,
            "복원 후 현재 checkpoint schema 저장");
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
