using Chronicles.Simulation;

internal static class LawAuthorityTests
{
    public static void Run()
    {
        CandidateCatalogStaysInReviewWithoutStableIds();
        PersonalClaimsAndObjectiveLawRemainSeparated();
        WorldAiAndTiandaoJudgmentsRemainNoEffectBeforeReady();
        CheckpointAndLazyObservationAreConnected();
    }

    private static void CandidateCatalogStaysInReviewWithoutStableIds()
    {
        var store = LawAuthorityStore.CreateReviewCatalog(32);
        var snapshot = store.CreateSnapshot();

        TestAssert.Equal(LawAuthorityRules.CandidateCount, snapshot.CandidateCount,
            "법칙 후보 전체 수");
        TestAssert.Equal(LawAuthorityRules.BaseCandidateCount, snapshot.BaseCandidateCount,
            "기초 법칙 후보 수");
        TestAssert.Equal(LawAuthorityRules.DerivedCandidateCount, snapshot.DerivedCandidateCount,
            "파생 법칙 후보 수");
        TestAssert.Equal(0, snapshot.ReadyCandidateCount,
            "실행 증거 없는 후보 READY 0");
        TestAssert.Equal(0, snapshot.StableLawCount,
            "실행 증거 없는 Stable LawId 0");
        TestAssert.True(snapshot.Candidates.All(value =>
                value.MissingEvidence == LawEvidenceFacet.All
                && value.ScopeAvailability == LawContractAvailability.DataUnavailable
                && value.RelationAvailability == LawContractAvailability.DataUnavailable),
            "Scope·Relation·7개 실행 증거를 임의 완료하지 않음");
    }

    private static void PersonalClaimsAndObjectiveLawRemainSeparated()
    {
        var store = LawAuthorityStore.CreateReviewCatalog(8);
        var candidate = new LawCandidateRef(0, LawCandidateKind.Base);
        var beforeInvalid = store.ComputeStateHash();
        var missingEditionEvidence = store.TryObservePersonalLaw(new ObservePersonalLawCommand(
            AgentIndex: 0,
            candidate,
            LawResearchSourceKind.EditionOrInheritance,
            LawKnowledgeClaimState.Incomplete,
            LawContractAvailability.DataUnavailable, 0,
            LawContractAvailability.DataUnavailable, 0,
            LawContractAvailability.DataUnavailable, 0,
            LawContractAvailability.DataUnavailable, 0,
            LawContractAvailability.DataUnavailable, 0,
            KnowledgeProvenanceId: 0,
            ApplicationSequence: 0,
            Source(1),
            Tick: 1));
        TestAssert.Equal(LawCommandStatus.InvalidSource, missingEditionEvidence.Status,
            "접근 가능한 판본 provenance 없는 연구 거부");
        TestAssert.Equal(beforeInvalid, store.ComputeStateHash(),
            "실패한 법칙 관찰은 권위 상태 불변");

        var misunderstood = store.TryObservePersonalLaw(new ObservePersonalLawCommand(
            AgentIndex: 0,
            candidate,
            LawResearchSourceKind.ObservedPhenomenon
                | LawResearchSourceKind.ExperimentOrContent,
            LawKnowledgeClaimState.Misunderstood,
            LawContractAvailability.Specified, 1_200,
            LawContractAvailability.DataUnavailable, 0,
            LawContractAvailability.DataUnavailable, 0,
            LawContractAvailability.DataUnavailable, 0,
            LawContractAvailability.Specified, 4_000,
            KnowledgeProvenanceId: 0,
            ApplicationSequence: 0,
            Source(2),
            Tick: 2));
        TestAssert.True(misunderstood.Applied, "개인 오해를 객관 법칙과 별도 기록");

        var corrected = store.TryObservePersonalLaw(new ObservePersonalLawCommand(
            AgentIndex: 0,
            candidate,
            LawResearchSourceKind.ObservedPhenomenon
                | LawResearchSourceKind.ExperimentOrContent,
            LawKnowledgeClaimState.Corrected,
            LawContractAvailability.Specified, 2_400,
            LawContractAvailability.DataUnavailable, 0,
            LawContractAvailability.DataUnavailable, 0,
            LawContractAvailability.DataUnavailable, 0,
            LawContractAvailability.Specified, 6_000,
            KnowledgeProvenanceId: 0,
            ApplicationSequence: 0,
            Source(3),
            Tick: 3));
        TestAssert.True(corrected.Applied, "오해 교정 typed transition");

        var snapshot = store.CreateSnapshot();
        TestAssert.Equal(1, snapshot.PersonalStateCount,
            "10,000×54 행렬 대신 실제 개인 상태만 희소 저장");
        TestAssert.Equal(0, snapshot.MisunderstoodClaimCount, "교정 뒤 오해 집계 제거");
        TestAssert.Equal(0, snapshot.ReadyCandidateCount,
            "개인 이해가 객관 Law READY를 자동 승격하지 않음");
        TestAssert.Equal(LawCommandStatus.DuplicateSource,
            store.TryRecordWorldJudgment(new RecordLawWorldJudgmentCommand(
                LawWorldActor.WorldAi, candidate, Source(3), Tick: 4)).Status,
            "개인/세계 판단 간 source_result 재생 방지");
    }

    private static void WorldAiAndTiandaoJudgmentsRemainNoEffectBeforeReady()
    {
        var store = LawAuthorityStore.CreateReviewCatalog(8);
        var candidate = new LawCandidateRef(10, LawCandidateKind.Derived);
        var worldAi = store.TryRecordWorldJudgment(new RecordLawWorldJudgmentCommand(
            LawWorldActor.WorldAi, candidate, Source(10), Tick: 10));
        var tiandao = store.TryRecordWorldJudgment(new RecordLawWorldJudgmentCommand(
            LawWorldActor.Tiandao, candidate, Source(11), Tick: 11));

        TestAssert.Equal(LawPreflightStatus.ReviewRequired, worldAi.Preflight,
            "세계 AI 후보 preflight REVIEW");
        TestAssert.Equal(LawPreflightStatus.ReviewRequired, tiandao.Preflight,
            "천도 후보 preflight REVIEW");
        var snapshot = store.CreateSnapshot();
        TestAssert.Equal(1, snapshot.WorldAiJudgmentCount, "세계 AI 판단 독립 집계");
        TestAssert.Equal(1, snapshot.TiandaoJudgmentCount, "천도 판단 독립 집계");
        TestAssert.Equal(0, snapshot.ApplicationCount,
            "REVIEW 판단은 법칙 효과를 적용하지 않음");
    }

    private static void CheckpointAndLazyObservationAreConnected()
    {
        var store = LawAuthorityStore.CreateReviewCatalog(16);
        store.TryRecordWorldJudgment(new RecordLawWorldJudgmentCommand(
            LawWorldActor.WorldAi,
            new LawCandidateRef(0, LawCandidateKind.Base),
            Source(20),
            Tick: 20));
        var checkpoint = store.CreateCheckpoint();
        var restoredStore = LawAuthorityStore.Restore(checkpoint, 16);
        TestAssert.Equal(store.ComputeStateHash(), restoredStore.ComputeStateHash(),
            "법칙 권위 checkpoint 결정론 왕복");

        var invalid = checkpoint with
        {
            Candidates =
            [
                checkpoint.Candidates[0] with
                {
                    Readiness = new LawReadinessCheckpoint(
                        AdapterEvidence: 1,
                        ResultContractEvidence: 0,
                        MetricEvidence: 0,
                        StateInputEvidence: 0,
                        StateOutputEvidence: 0,
                        SqliteRoundTripEvidence: 0,
                        SaveReplayEvidence: 0),
                },
                .. checkpoint.Candidates.Skip(1),
            ],
        };
        TestAssert.Throws<InvalidDataException>(
            () => LawAuthorityStore.Restore(invalid, 16),
            "근거 없는 READY 증거 위조 checkpoint 거부");

        var world = World.Create(0x1301UL, 16);
        var closed = world.CreateSnapshot(
            rankingCount: 1,
            eventCount: 0,
            sections: ObservationSections.None,
            worldChronicleCount: 0);
        TestAssert.Equal(0, closed.Content!.Laws!.CandidateCount,
            "닫힌 콘텐츠 작업공간 법칙 query 0건");

        var summary = world.CreateSnapshot(
            rankingCount: 1,
            eventCount: 0,
            sections: ObservationSections.Content,
            worldChronicleCount: 0,
            contentSecretRealmLimit: 0,
            knowledgeEditionLimit: 0,
            lawCandidateLimit: 0);
        TestAssert.Equal(LawAuthorityRules.CandidateCount,
            summary.Content!.Laws!.CandidateCount,
            "법칙 요약 후보 수");
        TestAssert.Equal(0, summary.Content.Laws.Candidates.Count,
            "법칙 내부 탭이 닫히면 후보 행 materialization 0건");

        var opened = world.CreateSnapshot(
            rankingCount: 1,
            eventCount: 0,
            sections: ObservationSections.Content,
            worldChronicleCount: 0,
            contentSecretRealmLimit: 0,
            knowledgeEditionLimit: 0,
            lawCandidateLimit: LawAuthorityRules.CandidateCount);
        TestAssert.Equal(LawAuthorityRules.CandidateCount,
            opened.Content!.Laws!.Candidates.Count,
            "법칙 내부 탭에서만 후보 행 투영");

        var worldCheckpoint = world.CreateCheckpoint();
        TestAssert.Equal(WorldCheckpoint.CurrentSchemaVersion, worldCheckpoint.SchemaVersion,
            "법칙·세계 생성·월 경계 랭킹 현재 checkpoint schema");
        TestAssert.True(worldCheckpoint.Laws is not null, "법칙 checkpoint 연결");
        var restoredWorld = World.Restore(worldCheckpoint);
        TestAssert.Equal(world.ComputeStateHash(), restoredWorld.ComputeStateHash(),
            "세계 법칙 권위 저장 hash 왕복");

        var schema28 = worldCheckpoint with { SchemaVersion = 28, Laws = null };
        var migrated28 = World.Restore(schema28);
        TestAssert.Equal(
            world.ComputeStateHashForCheckpointSchema(28),
            migrated28.ComputeStateHashForCheckpointSchema(28),
            "schema 28 저장 hash 호환");
        TestAssert.Equal(LawAuthorityRules.CandidateCount,
            migrated28.CreateCheckpoint().Laws!.Candidates.Length,
            "schema 28은 비활성 REVIEW 후보 catalog로 안전 이전");
    }

    private static ulong Source(int ordinal) =>
        0xF021_0000_0000_0000UL + checked((ulong)ordinal);
}
