using Chronicles.Application;
using Chronicles.Core;
using Chronicles.Persistence;
using Chronicles.Queries;
using Chronicles.Simulation;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using static TestAssert;

internal static class LifeReincarnationTests
{
    public static void BirthAptitudesChronicleAndReincarnation()
    {
        var initial = World.Create(8801, 1);
        var initialAgent = initial.GetAgentSnapshot(new AgentId(0), includeAptitudeDetails: true);
        var initialSoul = initialAgent.Aptitudes.Soul;
        var initialSoulId = initialAgent.Identity.SoulId;
        var initialBodyId = initialAgent.Identity.BodyId;
        var initialPersonId = initialAgent.Identity.PersonId;
        Equal(1, initialAgent.LifeNumber, "첫 생 번호");
        Equal(0L, initialAgent.AgeYears, "탄생 나이");
        Equal(6, new[]
        {
            initialAgent.Aptitudes.SpiritualRoot,
            initialAgent.Aptitudes.Bone,
            initialAgent.Aptitudes.Constitution,
            initialAgent.Aptitudes.Comprehension,
            initialAgent.Aptitudes.Mind,
            initialAgent.Aptitudes.Soul,
        }.Length, "6대 수행자질");
        var initialChronicle = initial.GetAgentChronicle(new AgentId(0));
        Equal(1, initialChronicle.Count, "첫 일대기 기록 수");
        True(initialChronicle[0].Kind == ChronicleEventKind.Birth, "탄생 기록");

        var deathTick = GameCalendar.FromYears(150).Value;
        var birthCheckpoint = initial.CreateCheckpoint();
        initial.AdvanceTo(new SimulationTick(deathTick - 1));
        var checkpoint = initial.CreateCheckpoint();
        var preparedAgent = birthCheckpoint.Agents[0] with
        {
            BirthTick = 0,
            RealmStage = RealmStage.QiRefining,
            RealmLayer = (byte)MinorStage.Floor1,
            CultivationProgress = 0,
            Activity = AgentActivity.Cultivating,
            NextDecisionTick = deathTick,
            Opponent = -1,
        };
        var prepared = checkpoint with
        {
            SchemaVersion = 24,
            Agents = [preparedAgent],
            Causal = null,
        };
        var world = World.Restore(prepared);
        world.AdvanceTo(new SimulationTick(deathTick));
        var dead = world.GetAgentSnapshot(new AgentId(0));
        Equal(150L, dead.AgeYears, "연기경 최대 수명 도달");
        True(dead.Activity == AgentActivity.Reincarnating, "윤회의 강 상태");
        True(dead.Identity.LifeState == PersonLifeState.ReincarnationRiver, "윤회의 강 신원 상태");
        Equal(initialSoulId, dead.Identity.SoulId, "사망 후 영혼 ID 유지");
        Equal(initialBodyId, dead.Identity.BodyId, "윤회의 강에서 직전 육신 ID 유지");
        Equal(initialPersonId, dead.Identity.PersonId, "윤회의 강에서 직전 현생 ID 유지");
        Equal(1, world.TotalDeaths, "사망 집계");

        world.AdvanceTo(new SimulationTick(
            deathTick + ReincarnationRules.GetTotalDurationTicks(RealmStage.QiRefining)));
        var reborn = world.GetAgentSnapshot(
            new AgentId(0),
            includeAptitudeDetails: true,
            includeCausal: true);
        Equal(2, reborn.LifeNumber, "환생 후 생 번호");
        Equal(0L, reborn.AgeYears, "환생 나이");
        Equal(initialSoul, reborn.Aptitudes.Soul, "영혼 자질 윤회 연속성");
        Equal(initialSoulId, reborn.Identity.SoulId, "환생 후 영혼 ID 연속성");
        True(reborn.Identity.BodyId != initialBodyId, "환생 후 새 육신 ID");
        True(reborn.Identity.PersonId != initialPersonId, "환생 후 새 현생 ID");
        True(reborn.Identity.LifeState == PersonLifeState.Living, "환생 후 생존 신원 상태");
        True(
            reborn.Causal!.CurrentLifeMemories.All(memory => memory.LifeNumber == 2),
            "전생 기억을 새 생 기억으로 자동 복사하지 않음");
        True(
            reborn.Causal.CurrentLifeMemories.Any(memory => memory.Kind == AgentMemoryKind.Rebirth),
            "새 생 환생 경계 기억");
        True(
            reborn.Aptitudes.SpiritualRoot != initialAgent.Aptitudes.SpiritualRoot
            || reborn.Aptitudes.Bone != initialAgent.Aptitudes.Bone
            || reborn.Aptitudes.Constitution != initialAgent.Aptitudes.Constitution
            || reborn.Aptitudes.Comprehension != initialAgent.Aptitudes.Comprehension
            || reborn.Aptitudes.Mind != initialAgent.Aptitudes.Mind,
            "새 육신의 자질은 다시 형성되어야 합니다.");
        var initialSoulComponents = initialAgent.AptitudeDetails!.Groups[(int)AptitudeDomain.Soul]
            .Components.Select(component => component.InnateValue).ToArray();
        var rebornSoulComponents = reborn.AptitudeDetails!.Groups[(int)AptitudeDomain.Soul]
            .Components.Select(component => component.InnateValue).ToArray();
        True(initialSoulComponents.SequenceEqual(rebornSoulComponents), "영혼 세부 자질 윤회 연속성");
        True(
            !initialAgent.AptitudeDetails.Groups[(int)AptitudeDomain.Bone].Components
                .Select(component => component.InnateValue)
                .SequenceEqual(reborn.AptitudeDetails.Groups[(int)AptitudeDomain.Bone].Components
                    .Select(component => component.InnateValue)),
            "새 육신의 근골 세부 자질은 다시 형성되어야 합니다.");
        Equal(1, world.TotalReincarnations, "환생 집계");

        var chronicle = world.GetAgentChronicle(new AgentId(0));
        True(chronicle.Any(entry => entry.Kind == ChronicleEventKind.Death), "사망 일대기");
        True(chronicle.Any(entry => entry.Kind == ChronicleEventKind.ReincarnationEntered), "윤회의 강 일대기");
        True(chronicle.Any(entry => entry.Kind == ChronicleEventKind.Rebirth), "환생 일대기");
    }

    public static void StagedReincarnationLineageTraceAndMigration()
    {
        var realms = new[]
        {
            RealmStage.QiRefining,
            RealmStage.FoundationEstablishment,
            RealmStage.GoldenCore,
            RealmStage.NascentSoul,
            RealmStage.SpiritTransformation,
            RealmStage.VoidRefinement,
            RealmStage.BodyIntegration,
            RealmStage.TribulationCrossing,
            RealmStage.Mahayana,
            RealmStage.HalfImmortal,
        };
        var expectedDurationYears = new[] { 3, 5, 8, 13, 21, 34, 55, 89, 144, 233 };
        for (var index = 0; index < realms.Length; index++)
        {
            Equal(
                expectedDurationYears[index],
                ReincarnationRules.GetTotalDurationYears(realms[index]),
                $"{realms[index]} 윤회의 강 총 기간");
            Equal(
                checked((long)expectedDurationYears[index] * GameCalendar.TicksPerYear),
                ReincarnationRules.GetTotalDurationTicks(realms[index]),
                $"{realms[index]} 윤회의 강 tick 기간");
        }

        Equal(
            834L,
            ReincarnationRules.GetStageBoundaryTick(
                startedTick: 0,
                RealmStage.FoundationEstablishment,
                ReincarnationStage.Entering),
            "12로 나누어떨어지지 않는 축기경 첫 경계 올림");
        True(
            ReincarnationRules.GetPreservationLevel(2_999) == TracePreservationLevel.Lost
            && ReincarnationRules.GetPreservationLevel(3_000) == TracePreservationLevel.Fragment
            && ReincarnationRules.GetPreservationLevel(5_500) == TracePreservationLevel.Partial
            && ReincarnationRules.GetPreservationLevel(8_000) == TracePreservationLevel.Intact,
            "전생 흔적 보존 등급 경계");
        True(
            ReincarnationRules.GetAccessLevel(2_999) == TraceAccessLevel.Sealed
            && ReincarnationRules.GetAccessLevel(3_000) == TraceAccessLevel.Latent
            && ReincarnationRules.GetAccessLevel(6_000) == TraceAccessLevel.Partial
            && ReincarnationRules.GetAccessLevel(8_500) == TraceAccessLevel.Immediate,
            "전생 흔적 접근 등급 경계");

        var stageContractStore = new ReincarnationStore(1);
        stageContractStore.InitializeFirstLife(
            agentIndex: 0,
            PersonCausalRules.CreateSoulId(0),
            PersonCausalRules.CreateBodyId(0, 1),
            PersonCausalRules.CreatePersonId(0, 1),
            personName: "단계계약",
            birthTick: 0,
            RealmStage.QiRefining,
            realmLayer: (byte)MinorStage.Floor1,
            combatPower: 100);
        var stageCandidates = ReincarnationRules.AllTraceDomains
            .Select(domain => new ReincarnationTraceCandidate(domain, 5_500, 3_000))
            .ToArray();
        var stageContractJourney = stageContractStore.BeginJourney(
            agentIndex: 0,
            deathTick: 0,
            LifeEndKind.Lifespan,
            RealmStage.QiRefining,
            stageCandidates);
        Throws<InvalidOperationException>(
            () => stageContractStore.AdvanceStage(
                agentIndex: 0,
                tick: checked(stageContractJourney.NextTransitionTick + 1)),
            "윤회 단계의 지연 전이는 유효하지 않은 저장 상태를 만들 수 없어야 함");

        var birthContextStore = new ReincarnationStore(1);
        birthContextStore.InitializeFirstLife(
            0,
            PersonCausalRules.CreateSoulId(0),
            PersonCausalRules.CreateBodyId(0, 1),
            PersonCausalRules.CreatePersonId(0, 1),
            "출생후보검증",
            birthTick: 0,
            RealmStage.QiRefining,
            realmLayer: 1,
            combatPower: 100);
        var birthJourney = birthContextStore.BeginJourney(
            0,
            deathTick: GameCalendar.TicksPerYear,
            LifeEndKind.Combat,
            RealmStage.QiRefining,
            stageCandidates,
            SoulDamageLevel.Severe,
            new SimulationEventRecordId(0xC001));
        while (birthJourney.Stage != ReincarnationStage.PreparingRebirth)
        {
            birthContextStore.AdvanceStage(
                0,
                birthJourney.NextTransitionTick,
                new SimulationEventRecordId(0xC002));
            birthJourney = birthContextStore.GetCurrentJourney(0)!;
        }
        var noCandidate = birthContextStore.PrepareBirthCandidate(
            0,
            birthJourney.RebirthReadyTick,
            parentOnePersonId: null,
            parentTwoPersonId: null,
            allowIndependentManifestation: false);
        True(noCandidate == BirthCandidateStatus.UnavailableLongWait, "출생 후보 부재 명시");
        var deferredTick = birthContextStore.DeferForBirthCandidate(
            0,
            birthJourney.RebirthReadyTick);
        var deferred = birthContextStore.GetCurrentJourney(0)!;
        Equal(1, deferred.BirthCandidateWaitYears, "출생 후보 부재 1년 장기 대기");
        True(deferred.BirthCandidateStatus == BirthCandidateStatus.Searching, "출생 후보 재탐색 상태");
        Equal(deferredTick, deferred.RebirthReadyTick, "연기된 환생 준비 경계");
        True(
            ReincarnationStore.IsValidCheckpoint(
                birthContextStore.CreateCheckpoint(),
                maximumTick: deferredTick - 1),
            "장기 출생 대기 저장 검증");
        True(birthContextStore.AdvanceStage(0, deferredTick).ReadyForRebirth, "연기 경계 재도달");
        var parentOne = new PersonIdentityId(0xAA01);
        var parentTwo = new PersonIdentityId(0xAA02);
        True(
            birthContextStore.PrepareBirthCandidate(
                0,
                deferredTick,
                parentOne,
                parentTwo,
                allowIndependentManifestation: false) == BirthCandidateStatus.Located,
            "출생 부모 후보 확정");
        birthContextStore.CompleteRebirth(
            0,
            deferredTick,
            PersonCausalRules.CreateSoulId(0),
            PersonCausalRules.CreateBodyId(0, 2),
            PersonCausalRules.CreatePersonId(0, 2),
            "출생후보검증2",
            RealmStage.QiRefining,
            initialRealmLayer: 1,
            initialCombatPower: 100,
            rebirthCausalRecordId: new SimulationEventRecordId(0xC003));
        var birthContext = birthContextStore.GetSnapshot(0, deferredTick);
        True(birthContext.CurrentJourney is null, "부모 연결 뒤 환생 완료");
        Equal(parentOne, birthContext.Lineage[^1].ParentOnePersonId!.Value, "첫 부모 현생 ID");
        Equal(parentTwo, birthContext.Lineage[^1].ParentTwoPersonId!.Value, "둘째 부모 현생 ID");
        True(birthContext.Lineage[^1].FamilyId.Value != 0, "부모 조합의 안정 가계 ID");
        True(
            birthContext.Lineage[^1].BirthStatus == BirthCandidateStatus.Located,
            "새 생 출생 후보 상태");
        True(
            birthContext.Traces.All(trace =>
                trace.EvidenceStatus == PreservationEvidenceStatus.Available),
            "새 윤회 흔적 provenance 근거 상태");
        var completedBirthJourney = birthContextStore.CreateCheckpoint().Journeys.Single();
        Equal(new SimulationEventRecordId(0xC001), completedBirthJourney.RootCausalRecordId, "윤회 root causal record");
        Equal(new SimulationEventRecordId(0xC002), completedBirthJourney.CrossingCausalRecordId, "도하 완료 causal record");
        Equal(new SimulationEventRecordId(0xC003), completedBirthJourney.RebirthCausalRecordId, "환생 causal record");

        var initial = World.Create(8810, 1);
        var firstLife = initial.GetAgentSnapshot(new AgentId(0));
        var birthCheckpoint = initial.CreateCheckpoint();
        var deathTick = GameCalendar.FromYears(150).Value;
        initial.AdvanceTo(new SimulationTick(deathTick - 1));
        var nearDeathCheckpoint = initial.CreateCheckpoint();
        var preparedAgent = birthCheckpoint.Agents[0] with
        {
            BirthTick = 0,
            RealmStage = RealmStage.QiRefining,
            RealmLayer = (byte)MinorStage.Floor1,
            CultivationProgress = 0,
            CultivationState = CultivationState.PreCultivation,
            TechniqueQuality = QualityValue.MaximumHundredths,
            TechniqueMastery = SkillProgressionRules.MaximumHundredths,
            SpellQuality = QualityValue.MaximumHundredths,
            SpellMastery = SkillProgressionRules.MaximumHundredths,
            SkillProgress = new SkillProgressCheckpoint(
                SkillProgressionRules.GetRequiredExperience(SkillProgressionRules.MaximumHundredths),
                SkillProgressionRules.MaximumHundredths,
                SkillProgressionRules.GetRequiredExperience(SkillProgressionRules.MaximumHundredths),
                SkillProgressionRules.MaximumHundredths),
            CombatExperience = 1_000_000,
            CombatProgress = new CombatProgressCheckpoint(QualityValue.MaximumHundredths),
            Activity = AgentActivity.Cultivating,
            NextDecisionTick = deathTick,
            Opponent = -1,
        };
        var world = World.Restore(nearDeathCheckpoint with
        {
            SchemaVersion = 24,
            Agents = [preparedAgent],
            Causal = null,
        });
        var actionsAtDeath = preparedAgent.ActionsTaken;
        world.AdvanceTo(new SimulationTick(deathTick));

        var riverAgent = world.GetAgentSnapshot(new AgentId(0));
        var river = world.GetAgentReincarnationSnapshot(new AgentId(0));
        Equal(150L, riverAgent.AgeYears, "사망 시점 나이");
        Equal(actionsAtDeath, riverAgent.ActionsTaken, "사망 전이에서 행동 횟수 불변");
        True(river.CurrentJourney!.Stage == ReincarnationStage.Entering, "윤회의 강 진입 단계");
        Equal(1, river.Lineage.Count, "첫 사망 시 계보 수");
        Equal(6, river.Traces.Count, "전생 흔적 6영역");
        Equal(
            ReincarnationRules.TraceDomainCount,
            river.Traces.Select(trace => trace.Domain).Distinct().Count(),
            "전생 흔적 영역 중복 없음");
        True(river.Traces.All(trace => trace.HasEvidence), "새 schema 사망은 흔적 근거를 보유");
        True(
            river.Traces.All(trace => trace.AccessibleHundredths == 0
                && trace.IntegratedHundredths == 0
                && trace.PendingAccessibleHundredths <= trace.PreservedHundredths),
            "환생 전 흔적은 접근·통합되지 않음");

        var durationTicks = ReincarnationRules.GetTotalDurationTicks(RealmStage.QiRefining);
        var boundaryNumerators = new[] { 1, 8, 10, 11, 12 };
        var stageBeforeBoundary = new[]
        {
            ReincarnationStage.Entering,
            ReincarnationStage.Crossing,
            ReincarnationStage.LateCrossing,
            ReincarnationStage.CrossingComplete,
            ReincarnationStage.PreparingRebirth,
        };
        var stageAfterBoundary = new[]
        {
            ReincarnationStage.Crossing,
            ReincarnationStage.LateCrossing,
            ReincarnationStage.CrossingComplete,
            ReincarnationStage.PreparingRebirth,
        };

        for (var boundaryIndex = 0; boundaryIndex < boundaryNumerators.Length; boundaryIndex++)
        {
            var expectedBoundary = checked(
                deathTick + ((durationTicks * boundaryNumerators[boundaryIndex]) + 11) / 12);
            var beforeBoundary = world.GetAgentReincarnationSnapshot(new AgentId(0));
            True(
                beforeBoundary.CurrentJourney!.Stage == stageBeforeBoundary[boundaryIndex],
                $"윤회 {boundaryIndex + 1}차 경계 이전 단계");
            Equal(
                expectedBoundary,
                beforeBoundary.CurrentJourney.NextTransitionTick.Value,
                $"윤회 {boundaryIndex + 1}차 경계 tick");

            Equal(
                expectedBoundary,
                world.GetAgentSnapshot(new AgentId(0)).NextDecisionTick.Value,
                $"윤회 {boundaryIndex + 1}차 Agent schedule 결합");
            world.AdvanceTo(new SimulationTick(expectedBoundary - 1));
            var immediatelyBeforeBoundary = world.GetAgentSnapshot(new AgentId(0));
            True(
                world.GetAgentReincarnationSnapshot(new AgentId(0)).CurrentJourney!.Stage
                    == stageBeforeBoundary[boundaryIndex],
                $"윤회 {boundaryIndex + 1}차 경계 직전 단계 불변");
            Equal(actionsAtDeath, immediatelyBeforeBoundary.ActionsTaken, "윤회 경계 직전 행동 불변");
            Equal(150L, immediatelyBeforeBoundary.AgeYears, "윤회 경계 직전 나이 고정");
            if (boundaryIndex == 2)
            {
                Equal(
                    0,
                    world.GetAgentChronicle(new AgentId(0)).Count(entry =>
                        entry.Kind == ChronicleEventKind.ReincarnationCrossingCompleted),
                    "도하 완료 경계 직전 Chronicle 없음");
            }

            world.AdvanceTo(new SimulationTick(expectedBoundary));
            var atBoundaryAgent = world.GetAgentSnapshot(new AgentId(0));
            Equal(actionsAtDeath, atBoundaryAgent.ActionsTaken, "윤회 중 행동 횟수 불변");
            Equal(
                boundaryIndex < stageAfterBoundary.Length ? 150L : 0L,
                atBoundaryAgent.AgeYears,
                boundaryIndex < stageAfterBoundary.Length
                    ? "윤회 중 나이는 사망 시점에 고정"
                    : "환생 완료 시 새 생은 0세");

            if (boundaryIndex < stageAfterBoundary.Length)
            {
                True(
                    world.GetAgentReincarnationSnapshot(new AgentId(0)).CurrentJourney!.Stage
                        == stageAfterBoundary[boundaryIndex],
                    $"윤회 {boundaryIndex + 1}차 경계 이후 단계");
            }
            if (boundaryIndex == 2)
            {
                var crossingEntry = world.GetAgentChronicle(new AgentId(0)).Single(entry =>
                    entry.Kind == ChronicleEventKind.ReincarnationCrossingCompleted);
                Equal(expectedBoundary, crossingEntry.Tick.Value, "도하 완료 Chronicle tick");
                Equal(1, crossingEntry.LifeNumber, "도하 완료 Chronicle 원천 생");
            }

            if (boundaryIndex == 0)
            {
                var stateHashBeforeObservation = world.ComputeStateHash();
                using (var runner = new SimulationRunner(world))
                {
                    var withoutReincarnation = runner.GetObservation(
                        new AgentId(0),
                        rankingCount: 1,
                        eventCount: 0,
                        sections: ObservationSections.None);
                    True(
                        withoutReincarnation.SelectedReincarnation is null,
                        "윤회 탭 비활성 시 투영 생략");
                    var withReincarnation = runner.GetObservation(
                        new AgentId(0),
                        rankingCount: 1,
                        eventCount: 0,
                        sections: ObservationSections.Reincarnation);
                    True(withReincarnation.SelectedReincarnation is not null, "윤회 탭 활성 시 지연 투영");
                }
                Equal(
                    stateHashBeforeObservation,
                    world.ComputeStateHash(),
                    "윤회 관찰 플래그는 권위 상태·RNG 무영향");

                var middleCheckpoint = world.CreateCheckpoint();
                var restored = World.Restore(WorldSaveCodec.Decode(
                    WorldSaveCodec.Encode(middleCheckpoint)));
                Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "윤회 중간 단계 codec·hash 왕복");

                var schema22Checkpoint = WorldSaveCodec.Decode(WorldSaveCodec.Encode(
                    middleCheckpoint with
                    {
                        SchemaVersion = 22,
                        Reincarnation = null,
                    }));
                var migratedSchema22 = World.Restore(schema22Checkpoint);
                var migratedReincarnation = migratedSchema22.GetAgentReincarnationSnapshot(
                    new AgentId(0));
                True(migratedReincarnation.IsCompleteLineage, "첫 생 schema 22 이전은 완전 계보");
                Equal(1, migratedReincarnation.Lineage.Count, "schema 22 현재 생 계보 이전");
                Equal(6, migratedReincarnation.Traces.Count, "schema 22 윤회 중 6영역 흔적 생성");
                True(
                    migratedReincarnation.Traces.All(trace => !trace.HasEvidence
                        && trace.PreservedHundredths == 0
                        && trace.PendingAccessibleHundredths == 0
                        && trace.AccessibleHundredths == 0
                        && trace.IntegratedHundredths == 0),
                    "schema 22에는 없던 전생 근거를 조작하지 않음");
                var migratedCheckpoint = migratedSchema22.CreateCheckpoint();
                Equal(WorldCheckpoint.CurrentSchemaVersion, migratedCheckpoint.SchemaVersion, "schema 22는 재저장 시 현재 schema");
                True(migratedCheckpoint.Reincarnation is not null, "이전 후 윤회 권위 저장 생성");

                world = restored;
            }
        }

        var reborn = world.GetAgentSnapshot(new AgentId(0));
        var continuity = world.GetAgentReincarnationSnapshot(new AgentId(0));
        Equal(2, reborn.LifeNumber, "단계 완료 뒤 새 생 번호");
        Equal(firstLife.Identity.SoulId, reborn.Identity.SoulId, "환생 영혼 ID 연속성");
        True(firstLife.Identity.BodyId != reborn.Identity.BodyId, "환생 육신 ID 교체");
        True(firstLife.Identity.PersonId != reborn.Identity.PersonId, "환생 현생 ID 교체");
        Equal(2, continuity.Lineage.Count, "환생 뒤 두 생 계보");
        True(
            continuity.Lineage[1].PredecessorId == continuity.Lineage[0].Id,
            "새 생 predecessor는 직전 생을 가리킴");
        True(continuity.CurrentJourney is null, "완료된 윤회는 진행 여정에서 제거");
        True(
            continuity.Traces.All(trace => trace.HasEvidence
                && trace.IntegratedHundredths <= trace.AccessibleHundredths
                && trace.AccessibleHundredths <= trace.PreservedHundredths
                && trace.IntegratedHundredths == 0),
            "환생 후 보존·접근·통합 분리 invariant");
        Equal(6, continuity.Traces.Count, "완료 여정의 6영역 흔적 수");
        var cachedContinuity = world.GetAgentReincarnationSnapshot(new AgentId(0));
        True(
            ReferenceEquals(continuity.Lineage, cachedContinuity.Lineage)
            && ReferenceEquals(continuity.Traces, cachedContinuity.Traces),
            "권위 revision 불변 시 계보·흔적 projection 재사용");
        True(
            continuity.Lineage is not LifeLineageSnapshot[]
            && continuity.Traces is not ReincarnationTraceSnapshot[],
            "projection 캐시의 내부 배열 비노출");
        True(reborn.RealmStage == RealmStage.QiRefining, "전생 흔적으로 경지 자동 복구 없음");
        Equal(1, reborn.RealmLayer, "전생 흔적으로 소경지 자동 복구 없음");
        Equal(0, reborn.CultivationProgress, "전생 흔적으로 수행 진척 자동 복구 없음");
        Equal(0, reborn.CombatExperience, "전생 흔적으로 전투 경험 자동 복구 없음");
        Equal(0, reborn.CombatUnderstanding, "전생 흔적으로 전투 이해 자동 복구 없음");
        True(
            reborn.TechniqueMastery < SkillProgressionRules.MaximumHundredths
            && reborn.SpellMastery < SkillProgressionRules.MaximumHundredths,
            "전생 공법·법술 숙련 자동 복구 없음");
        Equal(0, reborn.TechniqueUnderstanding, "전생 공법 이해 자동 복구 없음");
        Equal(0, reborn.SpellUnderstanding, "전생 법술 이해 자동 복구 없음");
        Equal(
            1,
            world.GetAgentChronicle(new AgentId(0)).Count(entry =>
                entry.Kind == ChronicleEventKind.ReincarnationCrossingCompleted),
            "윤회의 강 도하 완료 Chronicle 정확히 1회");

        var validCheckpoint = world.CreateCheckpoint();
        var validReincarnation = validCheckpoint.Reincarnation!;
        Equal(1, validReincarnation.Journeys.Length, "첫 윤회 완료 여정 수");
        True(validReincarnation.Journeys[0].CompletedTick is not null, "첫 윤회 완료 tick 보존");
        var validRoundTrip = World.Restore(WorldSaveCodec.Decode(WorldSaveCodec.Encode(validCheckpoint)));
        Equal(world.ComputeStateHash(), validRoundTrip.ComputeStateHash(), "최종 윤회 checkpoint codec·hash 왕복");

        var legacySecondLife = World.Restore(validCheckpoint with
        {
            SchemaVersion = 22,
            Reincarnation = null,
        }).GetAgentReincarnationSnapshot(new AgentId(0));
        True(!legacySecondLife.IsCompleteLineage, "schema 22 제2생 이전은 불완전 계보");
        Equal(2, legacySecondLife.FirstTrackedLifeNumber, "schema 22 첫 추적 생 번호");
        Equal(1, legacySecondLife.Lineage.Count, "schema 22에 없는 전생 계보를 만들지 않음");
        Equal(0, legacySecondLife.Traces.Count, "schema 22 생존 상태에 없는 전생 흔적을 만들지 않음");

        var invalidLineage = validReincarnation.Lineage.ToArray();
        var secondLifeIndex = Array.FindIndex(invalidLineage, entry => entry.LifeNumber == 2);
        invalidLineage[secondLifeIndex] = invalidLineage[secondLifeIndex] with
        {
            PredecessorId = new LifeRecordId(ulong.MaxValue),
        };
        Throws<InvalidDataException>(
            () => World.Restore(validCheckpoint with
            {
                Reincarnation = validReincarnation with { Lineage = invalidLineage },
            }),
            "잘못된 윤회 predecessor 저장");

        var invalidTraces = validReincarnation.Traces.ToArray();
        invalidTraces[0] = invalidTraces[0] with
        {
            AccessibleHundredths = 0,
            IntegratedHundredths = 1,
        };
        Throws<InvalidDataException>(
            () => World.Restore(validCheckpoint with
            {
                Reincarnation = validReincarnation with { Traces = invalidTraces },
            }),
            "통합치가 접근치를 초과한 윤회 흔적 저장");
        Throws<InvalidDataException>(
            () => World.Restore(validCheckpoint with
            {
                Reincarnation = validReincarnation with { Journeys = [] },
            }),
            "사망 생에 필요한 윤회 여정 누락 저장");
        Throws<InvalidDataException>(
            () => World.Restore(validCheckpoint with
            {
                Reincarnation = validReincarnation with
                {
                    Journeys =
                    [
                        validReincarnation.Journeys[0],
                        validReincarnation.Journeys[0],
                    ],
                },
            }),
            "중복 윤회 여정 ID 저장");
        var invalidEndKindLineage = validReincarnation.Lineage.ToArray();
        invalidEndKindLineage[0] = invalidEndKindLineage[0] with
        {
            EndKind = (LifeEndKind)byte.MaxValue,
        };
        Throws<InvalidDataException>(
            () => World.Restore(validCheckpoint with
            {
                Reincarnation = validReincarnation with { Lineage = invalidEndKindLineage },
            }),
            "알 수 없는 생 종료 원인 저장");
        Throws<InvalidDataException>(
            () => World.Restore(validCheckpoint with
            {
                TotalDeaths = checked(validCheckpoint.TotalDeaths + 1),
            }),
            "사망·환생·윤회의 강 집계 불일치 저장");

        var secondDeathTick = checked(
            validCheckpoint.Tick + GameCalendar.FromYears(150).Value);
        var delayedSecondLife = validCheckpoint.Agents[0] with
        {
            Activity = AgentActivity.Cultivating,
            NextDecisionTick = secondDeathTick,
            Opponent = -1,
        };
        var threeLifeWorld = World.Restore(validCheckpoint with
        {
            Agents = [delayedSecondLife],
        });
        threeLifeWorld.AdvanceTo(new SimulationTick(secondDeathTick));
        var secondRiverCheckpoint = threeLifeWorld.CreateCheckpoint();
        var reorderedSecondRiver = secondRiverCheckpoint.Reincarnation! with
        {
            Traces = secondRiverCheckpoint.Reincarnation!.Traces.Reverse().ToArray(),
        };
        threeLifeWorld = World.Restore(secondRiverCheckpoint with
        {
            Reincarnation = reorderedSecondRiver,
        });
        threeLifeWorld.AdvanceTo(new SimulationTick(checked(
            secondDeathTick + ReincarnationRules.GetTotalDurationTicks(RealmStage.QiRefining))));
        var threeLifeContinuity = threeLifeWorld.GetAgentReincarnationSnapshot(new AgentId(0));
        Equal(3, threeLifeContinuity.Lineage.Count, "두 번 환생 뒤 세 생 계보");
        Equal(12, threeLifeContinuity.Traces.Count, "완료된 두 여정의 6영역 흔적 보존");
        True(
            threeLifeContinuity.Lineage[1].PredecessorId == threeLifeContinuity.Lineage[0].Id
            && threeLifeContinuity.Lineage[2].PredecessorId == threeLifeContinuity.Lineage[1].Id,
            "세 생 predecessor 연쇄");
        Equal(
            2,
            threeLifeContinuity.Traces.Select(trace => trace.JourneyId).Distinct().Count(),
            "두 윤회 여정의 흔적 분리");
    }

    public static void PersonIdentityAndDecisionAuthority()
    {
        var world = World.Create(8802, 8);
        var initial = world.GetAgentSnapshot(new AgentId(0));
        Equal(PersonCausalRules.CreateSoulId(0), initial.Identity.SoulId, "안정 영혼 ID");
        Equal(PersonCausalRules.CreateBodyId(0, 1), initial.Identity.BodyId, "첫 육신 ID");
        Equal(PersonCausalRules.CreatePersonId(0, 1), initial.Identity.PersonId, "첫 현생 ID");
        Equal(PersonCausalRules.DecisionContractId, initial.Decision.ContractId, "AI 결정 계약 ID");
        Equal(PersonCausalRules.DecisionContractRevision, initial.Decision.ContractRevision, "AI 결정 계약 revision");
        True(initial.Decision.RequiresReview, "미확정 Utility 다음 버전 재검토 표식");
        True(initial.Decision.Latest.PrimaryGoal == AgentGoalKind.ReachRankOne, "모든 AI 최종 목표는 1위");
        True(initial.Decision.Latest.SupportingGoal == AgentGoalKind.EnterCultivation, "첫 행동의 현재 목표");
        True(initial.Decision.Latest.SelectedActivity == AgentActivity.Cultivating, "첫 행동 선택");

        var beforeRevision = initial.Decision.Latest.AuthorityRevision;
        world.AdvanceTo(initial.NextDecisionTick);
        var committed = world.GetAgentSnapshot(new AgentId(0));
        Equal(
            beforeRevision,
            committed.Decision.Latest.AuthorityRevision,
            "장기 행동 계획 진행 중 decision revision 유지");
        var plan = committed.Cultivation.ActionPlan
            ?? throw new InvalidOperationException("장기 수행 계획 누락");
        world.AdvanceTo(plan.PlannedEndTick);
        var decided = world.GetAgentSnapshot(new AgentId(0));
        True(decided.Decision.Latest.AuthorityRevision > beforeRevision, "계획 경계에서 decision revision 증가");
        True(decided.Decision.Latest.Tick <= world.Tick.Value, "결정 시점은 현재 세계 시간 이하");

        var beforeObservation = world.ComputeStateHash();
        _ = world.GetAgentSnapshot(new AgentId(0));
        Equal(beforeObservation, world.ComputeStateHash(), "행동 이유 관찰은 권위 상태 무영향");

        var checkpoint = world.CreateCheckpoint();
        Equal(WorldCheckpoint.CurrentSchemaVersion, checkpoint.SchemaVersion, "윤회 계보·Teaching·인과·조직 저장 schema");
        var restored = World.Restore(WorldSaveCodec.Decode(WorldSaveCodec.Encode(checkpoint)));
        Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "인물 신원·결정 codec·hash 왕복");

        var legacy = checkpoint with
        {
            SchemaVersion = 19,
            Agents = checkpoint.Agents.Select(agent => agent with
            {
                Identity = null,
                Decision = null,
            }).ToArray(),
        };
        var migrated = World.Restore(legacy).GetAgentSnapshot(new AgentId(0));
        Equal(PersonCausalRules.CreateSoulId(0), migrated.Identity.SoulId, "schema 19 영혼 ID 결정론 이전");
        Equal(PersonCausalRules.CreateBodyId(0, migrated.LifeNumber), migrated.Identity.BodyId, "schema 19 육신 ID 이전");
        True(migrated.Decision.Latest.PrimaryGoal == AgentGoalKind.ReachRankOne, "schema 19 최종 목표 이전");
        True(migrated.Decision.Latest.AuthorityRevision >= 1, "schema 19 결정 revision 이전");
    }

    public static void AgentCausalLifeVerticalFlow()
    {
        var world = World.Create(0xCA05_A116UL, 256);
        var hidden = world.GetAgentSnapshot(new AgentId(0));
        True(hidden.Causal is null, "접힌 인과 UI는 상세 DTO를 만들지 않음");
        True(hidden.LifeAuthority.Gender != GenderIdentity.Undefined, "생별 성별 권위");
        True(hidden.LifeAuthority.SoulState == SoulEmbodimentState.Embodied, "영혼은 육신에 깃듦");
        True(hidden.LifeAuthority.BodyState == BodyLifeState.Living, "현재 육신 생존");
        True(hidden.LifeAuthority.LifePhase == CurrentLifePhase.Living, "현재 생 상태");
        True(hidden.LifeAuthority.Appearance.DefinitionId.Value != 0, "외형 안정 정의 ID");
        True(hidden.LifeAuthority.Appearance.DerivedBeautyHundredths is >= 0 and <= 10_000, "외형 파생값 범위");

        var beforeHash = world.ComputeStateHash();
        var visible = world.GetAgentSnapshot(new AgentId(0), includeCausal: true);
        Equal(beforeHash, world.ComputeStateHash(), "인과·외형 관찰은 권위 상태 불변");
        var causal = visible.Causal!;
        Equal(AgentCausalRules.ContractId, causal.ContractId, "인과 계약 ID");
        Equal(AgentCausalRules.PerceivedFactCapacity, causal.PerceivedFacts.Count, "인지 가능한 사실만 고정 투영");
        Equal(1, causal.CurrentLifeMemories.Count, "탄생 중요 기억");
        True(causal.CurrentLifeMemories[0].Kind == AgentMemoryKind.Birth, "탄생 기억 종류");
        True(causal.CurrentPath.GoalId.Value != 0, "Goal 안정 ID");
        True(causal.CurrentPath.PlanId.Value != 0, "Plan 안정 ID");
        True(causal.CurrentPath.TaskId.Value != 0, "Task 안정 ID");
        True(causal.CurrentPath.ActionId.Value != 0, "Action 안정 ID");
        True(causal.CurrentPath.EventDefinitionId.Value != 0, "Event Definition 안정 ID");
        True(causal.CurrentPath.EventInstanceId.Value != 0, "Event Instance 안정 ID");
        True(causal.CurrentPath.ResultId.Value != 0, "Result 안정 ID");
        True(causal.CurrentPath.EventRecordId.Value != 0, "Event Record 안정 ID");
        Equal(causal.CurrentPath.EventRecordId, causal.CurrentPath.RootRecordId, "첫 판정은 인과 root");
        True(causal.CurrentPath.ParentRecordId is null, "첫 판정 parent 없음");

        world.AdvanceTo(visible.NextDecisionTick);
        var committedSnapshot = world.GetAgentSnapshot(new AgentId(0), includeCausal: true);
        var committedCausal = committedSnapshot.Causal!;
        Equal(
            causal.AuthorityRevision,
            committedCausal.AuthorityRevision,
            "같은 장기 행동 pulse에서 인과 revision 유지");
        var plan = committedSnapshot.Cultivation.ActionPlan
            ?? throw new InvalidOperationException("인과 경로의 수행 계획 누락");
        world.AdvanceTo(plan.PlannedEndTick);
        var after = world.GetAgentSnapshot(new AgentId(0), includeCausal: true).Causal!;
        True(after.AuthorityRevision > causal.AuthorityRevision, "계획 경계에서 인과 revision 증가");
        Equal(causal.CurrentPath.RootRecordId, after.CurrentPath.RootRecordId, "같은 생 causal root 유지");
        True(after.CurrentPath.ParentRecordId == causal.CurrentPath.EventRecordId, "직전 record가 parent");
        True(after.CurrentPath.EventRecordId != causal.CurrentPath.EventRecordId, "판정별 record ID 분리");

        world.AdvanceTo(new SimulationTick(80L * GameCalendar.TicksPerYear));
        var bounded = world.GetAgentSnapshot(new AgentId(0), includeCausal: true).Causal!;
        True(
            bounded.CurrentLifeMemories.Count <= AgentCausalRules.MemoryCapacityPerAgent,
            "생별 기억 고정 상한");
        True(
            bounded.CurrentLifeMemories.All(memory => memory.ReinforcementCount > 0),
            "반복 경험은 새 로그 대신 기억 강화");

        var requiredResolutions = new[]
        {
            CausalResolutionCode.Committed,
            CausalResolutionCode.Reject,
            CausalResolutionCode.Revalidate,
            CausalResolutionCode.Retry,
            CausalResolutionCode.Defer,
            CausalResolutionCode.AlternativeResult,
            CausalResolutionCode.Compensate,
            CausalResolutionCode.Blocked,
        };
        Equal(8, requiredResolutions.Distinct().Count(), "공통 실패·회복 결과 8종");
        Equal(
            4,
            new[]
            {
                CausalFailureScope.Action,
                CausalFailureScope.Task,
                CausalFailureScope.Plan,
                CausalFailureScope.Goal,
            }.Distinct().Count(),
            "행동·Task·Plan·Goal 실패 범위 분리");
        True(
            world.BrowseAgents(0, 256)
                .Select(agent => agent.LifeAuthority.Gender)
                .All(gender => gender != GenderIdentity.Undefined),
            "모든 인물 성별 명시");

        var checkpoint = world.CreateCheckpoint();
        Equal(WorldCheckpoint.CurrentSchemaVersion, checkpoint.SchemaVersion, "생애·인과·조직 권위 schema");
        True(checkpoint.Causal is not null, "인과 store 권위 저장");
        True(checkpoint.Agents.All(agent => agent.LifeAuthority is not null), "생애·외형 권위 저장");
        var restored = World.Restore(checkpoint);
        Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "생애·인과 hash 왕복");
        var restoredCausal = restored.GetAgentSnapshot(new AgentId(0), includeCausal: true).Causal!;
        Equal(bounded.CurrentPath, restoredCausal.CurrentPath, "현재 인과 경로 왕복");
        EqualSequence(
            bounded.PerceivedFacts.ToArray(),
            restoredCausal.PerceivedFacts.ToArray(),
            "인지 사실 왕복");
        EqualSequence(
            bounded.CurrentLifeMemories.ToArray(),
            restoredCausal.CurrentLifeMemories.ToArray(),
            "생별 기억 왕복");

        var forgedPaths = checkpoint.Causal!.CurrentPaths.ToArray();
        forgedPaths[0] = forgedPaths[0] with { ActionId = new ActionId(forgedPaths[0].ActionId.Value ^ 1UL) };
        Throws<InvalidDataException>(
            () => World.Restore(checkpoint with
            {
                Causal = checkpoint.Causal with { CurrentPaths = forgedPaths },
            }),
            "위조된 Action 안정 ID 저장 거부");
    }
}

