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

internal static class RelationChronicleTests
{
    public static void SparseDirectionalRelationsAndRoles()
    {
        Equal(8, RelationRules.MaximumRelationsPerAgent, "인물당 희소 관계 슬롯 임시 상한");
        Equal(0, RelationRules.DirectTeachingCultivationSpeedBonusBasisPoints, "일반 Teaching 직접 수행속도 0%");
        var initialRoles = new[]
        {
            RelationRole.MasterDisciple,
            RelationRole.Friendship,
            RelationRole.Romance,
            RelationRole.Companion,
            RelationRole.PersistentRivalry,
        };
        Equal(5, initialRoles.Distinct().Count(), "초기 Relation Role 5종");

        var stopwatch = Stopwatch.StartNew();
        var world = World.Create(8803, 500);
        world.AdvanceTo(GameCalendar.FromYears(160));
        stopwatch.Stop();
        var checkpoint = world.CreateCheckpoint();
        var relationStore = checkpoint.Relations;
        True(relationStore is not null, "schema 21 희소 관계 저장 존재");
        True(relationStore!.Relations.Length > 0, "실제 공존·전투에서 관계 생성");
        True(
            relationStore.Relations.Length <= 500 * RelationRules.MaximumRelationsPerAgent / 2,
            "희소 관계 총량 상한");
        var degree = new int[world.Population];
        foreach (var relation in relationStore.Relations)
        {
            degree[relation.LeftAgent]++;
            degree[relation.RightAgent]++;
            Equal(RelationRules.CreateId(relation.LeftAgent, relation.RightAgent), relation.Id, "관계 안정 ID");
            True(RelationRules.IsValidMetrics(relation.LeftTowardRight), "정방향 6축 범위");
            True(RelationRules.IsValidMetrics(relation.RightTowardLeft), "역방향 6축 범위");
        }
        True(degree.All(count => count <= RelationRules.MaximumRelationsPerAgent), "모든 인물 관계 adjacency 상한");
        True(
            relationStore.Relations.Any(relation => relation.Roles != RelationRole.None),
            "실제 반복 상호작용에서 Role 성립");
        True(
            relationStore.Relations.Any(relation => relation.LeftTowardRight != relation.RightTowardLeft),
            "승자·패자 관점의 방향별 6축 분리");
        True(
            relationStore.Relations.SelectMany(relation => relation.History)
                .Any(history => history.Kind == RelationHistoryKind.RoleFormed),
            "중요 Role 성립 역사");
        True(
            relationStore.Relations.SelectMany(relation => relation.History)
                .Any(history => history.Kind == RelationHistoryKind.RoleEndedAtLifeBoundary),
            "사망·윤회 생애 경계 Role 종료 역사");
        True(relationStore.ResultSequence > 0, "관계 typed result 전역 sequence");
        True(
            relationStore.Relations.Any(relation =>
                relation.LatestInteractionResult.InteractionKind == RelationInteractionKind.Dialogue),
            "일반 대화 상호작용 typed result");
        True(
            relationStore.Relations.Any(relation =>
                relation.LatestInteractionResult.Id.Value != 0
                && relation.LatestInteractionResult.Id == RelationRules.CreateResultId(
                    relation.Id,
                    relation.LatestInteractionResult.Sequence)),
            "관계 결과 안정 ID");
        True(
            relationStore.Relations.Any(relation =>
                relation.LatestInteractionResult.SourceEventRecordId.Value != 0),
            "관계 결과가 AI causal event record를 참조");

        var beforeObservation = world.ComputeStateHash();
        var hidden = world.GetAgentSnapshot(new AgentId(0));
        True(hidden.Relations is null, "접힌 관계 UI DTO 미생성");
        var visible = Enumerable.Range(0, world.Population)
            .Select(index => world.GetAgentSnapshot(new AgentId(index), includeRelations: true))
            .First(agent => agent.Relations is { Count: > 0 });
        True(visible.Relations!.Count <= RelationRules.MaximumRelationsPerAgent, "펼친 관계 UI 희소 DTO");
        True(
            visible.Relations.All(relation =>
                relation.ExternalContext.OrganizationContext == RelationContextAvailability.Available
                && relation.ExternalContext.ReputationContext == RelationContextAvailability.DataUnavailable),
            "조직 맥락은 사용 가능하고 미구현 평판은 DataUnavailable");
        True(
            visible.Relations.All(relation =>
                relation.ExternalContext.FamilyContext is RelationContextAvailability.Available
                    or RelationContextAvailability.DataUnavailable),
            "가계 맥락 availability 명시");
        Equal(beforeObservation, world.ComputeStateHash(), "관계 관찰은 권위 상태 무영향");

        var restored = World.Restore(WorldSaveCodec.Decode(WorldSaveCodec.Encode(checkpoint)));
        Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "희소 관계 codec·hash 왕복");
        var legacy = checkpoint with { SchemaVersion = 20, Relations = null };
        Equal(0, World.Restore(legacy).TotalRelations, "schema 20은 빈 희소 관계로 이전");
        Console.WriteLine(
            $"RELATION PERF: 500명·160년 {stopwatch.Elapsed.TotalMilliseconds:N1} ms · " +
            $"관계 {relationStore.Relations.Length:N0}개 · 최대 degree {degree.Max():N0}");
    }

    public static void TeachingVerticalFlow()
    {
        Equal(0, RelationRules.DirectTeachingCultivationSpeedBonusBasisPoints, "일반 Teaching 직접 수행속도 0%");
        Equal(
            8,
            Enum.GetValues<TeachingResultKind>().Distinct().Count(),
            "Teaching Typed Result 8종");
        var deterministicRoll = TeachingRules.GetDedicatedRollBasisPoints(
            8804,
            new TeachingProvenanceId(123456789),
            55_000,
            3);
        Equal(
            deterministicRoll,
            TeachingRules.GetDedicatedRollBasisPoints(
                8804,
                new TeachingProvenanceId(123456789),
                55_000,
                3),
            "Teaching 전용 roll 결정론");

        var schema21Source = World.Create(88040, 8).CreateCheckpoint();
        var schema21Agents = schema21Source.Agents
            .Select(agent => agent with { Teaching = null })
            .ToArray();
        var schema21Relation = new RelationStateCheckpoint(
            RelationRules.CreateId(0, 1),
            0,
            1,
            schema21Agents[0].Identity!.Value.PersonId,
            schema21Agents[1].Identity!.Value.PersonId,
            default,
            default,
            RelationRole.None,
            MasterAgent: -1,
            CooperativeInteractions: 0,
            JourneyInteractions: 0,
            CombatInteractions: 0,
            History:
            [
                new RelationHistoryEntry(
                    1,
                    0,
                    RelationHistoryKind.RelationCreated,
                    RelationRole.None,
                    schema21Agents[0].Identity!.Value.PersonId,
                    schema21Agents[1].Identity!.Value.PersonId),
            ]);
        var schema21Checkpoint = schema21Source with
        {
            SchemaVersion = 21,
            Agents = schema21Agents,
            Relations = new RelationStoreCheckpoint(1, [schema21Relation]),
        };
        var schema21Container = RemoveSchema22TeachingProperties(
            WorldSaveCodec.Encode(schema21Checkpoint));
        var schema21Restored = World.Restore(WorldSaveCodec.Decode(schema21Container));
        True(
            schema21Restored.CreateCheckpoint().Agents.All(agent =>
                agent.Teaching is
                {
                    AuthorityRevision: 0,
                    LearningAuthorityRevision: 0,
                    Latest.AuthorityRevision: 0,
                    LatestLearning.AuthorityRevision: 0,
                }),
            "schema 21은 빈 Teaching 권위로 이전");
        Equal(1, schema21Restored.TotalRelations, "schema 21 관계 continuity 누락 필드 이전");

        var world = World.Create(8804, 500);
        WorldCheckpoint? checkpoint = null;
        var activeTeacher = -1;
        for (var year = 20; year <= 160 && activeTeacher < 0; year += 10)
        {
            world.AdvanceTo(GameCalendar.FromYears(year));
            checkpoint = world.CreateCheckpoint();
            activeTeacher = Array.FindIndex(
                checkpoint.Agents,
                agent => agent.Activity == AgentActivity.Teaching
                    && agent.Teaching is { Latest.IsActive: true });
        }

        True(checkpoint is not null && activeTeacher >= 0, "실제 AI Teaching 행동 예약");
        var source = checkpoint!;
        var teacher = source.Agents[activeTeacher];
        var teaching = teacher.Teaching!.Value;
        var trace = teaching.Latest;
        var learnerIndex = trace.LearnerAgent;
        var learner = source.Agents[learnerIndex];
        True(
            learner.Teaching is { } learning
            && learning.LatestLearning.IsActive
            && learning.LatestLearning.ProvenanceId == trace.ProvenanceId,
            "교사·학습자 Teaching context provenance 연결");
        True(trace.TargetKnowledgeRef.Value != 0 && trace.ProvenanceId.Value != 0, "지식 Ref·provenance 안정 ID");
        True(
            source.Relations!.Relations.Any(relation =>
                relation.Id == trace.RelationId
                && (relation.Roles & RelationRole.MasterDisciple) != 0
                && relation.MasterAgent == activeTeacher),
            "사제 Role 성립");

        var missingMirrorAgents = source.Agents.ToArray();
        var missingMirrorLearnerTeaching = learner.Teaching!.Value;
        missingMirrorAgents[learnerIndex] = learner with
        {
            Teaching = missingMirrorLearnerTeaching with
            {
                LearningAuthorityRevision = 0,
                LatestLearning = TeachingTraceState.Empty,
            },
        };
        Throws<InvalidDataException>(
            () => World.Restore(source with { Agents = missingMirrorAgents }),
            "활성 Teaching 한쪽 mirror 누락 저장 거부");

        var forgedProvenance = new TeachingProvenanceId(trace.ProvenanceId.Value ^ 1UL);
        var forgedAgents = source.Agents.ToArray();
        forgedAgents[activeTeacher] = teacher with
        {
            Teaching = teaching with
            {
                Latest = teaching.Latest with { ProvenanceId = forgedProvenance },
            },
        };
        forgedAgents[learnerIndex] = learner with
        {
            Teaching = missingMirrorLearnerTeaching with
            {
                LatestLearning = missingMirrorLearnerTeaching.LatestLearning with
                {
                    ProvenanceId = forgedProvenance,
                },
            },
        };
        Throws<InvalidDataException>(
            () => World.Restore(source with { Agents = forgedAgents }),
            "Teaching provenance 파생값 위조 저장 거부");

        var agents = source.Agents.ToArray();
        var teacherDueTick = teacher.NextDecisionTick;
        var postponedTick = checked(teacherDueTick + GameCalendar.TicksPerYear);
        for (var index = 0; index < agents.Length; index++)
        {
            if (index != activeTeacher
                && agents[index].Activity != AgentActivity.Reincarnating
                && agents[index].NextDecisionTick != long.MaxValue)
            {
                agents[index] = agents[index] with
                {
                    NextDecisionTick = Math.Max(agents[index].NextDecisionTick, postponedTick),
                };
            }
        }
        var isolated = World.Restore(source with { Agents = agents });
        True(
            isolated.GetAgentSnapshot(new AgentId(activeTeacher)).Teaching is null,
            "접힌 Teaching UI는 상세 DTO를 생성하지 않음");
        var teacherBefore = isolated.GetAgentSnapshot(
            new AgentId(activeTeacher),
            includeTeaching: true);
        var learnerBefore = isolated.GetAgentSnapshot(
            new AgentId(learnerIndex),
            includeTeaching: true);
        isolated.AdvanceTo(new SimulationTick(teacherDueTick));
        var teacherAfter = isolated.GetAgentSnapshot(
            new AgentId(activeTeacher),
            includeTeaching: true);
        var learnerAfter = isolated.GetAgentSnapshot(
            new AgentId(learnerIndex),
            includeTeaching: true,
            includeCausal: true);
        True(!teacherAfter.Teaching!.Latest.IsActive, "Teaching 결과 정산 완료");
        True(
            teacherAfter.Teaching!.Latest.LastResult is not TeachingResultKind.None,
            "Teaching Typed Result 발생");
        True(
            !string.IsNullOrWhiteSpace(
                teacherAfter.Teaching.LatestParticipants?.TeacherName)
            && !string.IsNullOrWhiteSpace(
                teacherAfter.Teaching.LatestParticipants?.LearnerName),
            "Teaching 상세 DTO 교사·학습자 이름 투영");
        Equal(teacherBefore.CultivationProgress, teacherAfter.CultivationProgress, "교사의 직접 수행 Work 0");
        Equal(learnerBefore.CultivationProgress, learnerAfter.CultivationProgress, "학습자의 직접 수행 Work 0");
        True(
            learnerAfter.TechniqueTrainingExperience != learnerBefore.TechniqueTrainingExperience
            || learnerAfter.SpellTrainingExperience != learnerBefore.SpellTrainingExperience
            || learnerAfter.TechniqueUnderstanding != learnerBefore.TechniqueUnderstanding
            || learnerAfter.SpellUnderstanding != learnerBefore.SpellUnderstanding
            || learnerAfter.Teaching!.TechniqueMisunderstandingPoints
                != learnerBefore.Teaching!.TechniqueMisunderstandingPoints
            || learnerAfter.Teaching!.SpellMisunderstandingPoints
                != learnerBefore.Teaching!.SpellMisunderstandingPoints,
            "Teaching은 숙련 경험·이해 또는 오해 상태를 변경");
        True(
            learnerAfter.Causal!.LatestKnowledgeClaim is { } claim
            && claim.TeachingProvenanceId == trace.ProvenanceId
            && claim.RecipientPersonId == learnerAfter.Identity.PersonId,
            "학습 결과 KnowledgeClaim·provenance·현생 연결");
        True(
            learnerAfter.Causal.CurrentLifeMemories.Any(memory =>
                memory.Kind == AgentMemoryKind.Teaching
                && memory.RelationId == trace.RelationId),
            "Teaching 결과가 관계 provenance를 가진 생별 기억으로 승격");

        var completedCheckpoint = isolated.CreateCheckpoint();
        var completedTrace = teacherAfter.Teaching.Latest;
        var completedRelation = completedCheckpoint.Relations!.Relations.Single(
            relation => relation.Id == completedTrace.RelationId);
        var persistedTeachingSessions = completedTrace.TargetKind == TeachingTargetKind.Technique
            ? completedRelation.TechniqueTeachingSessions
            : completedRelation.SpellTeachingSessions;
        Equal(
            completedTrace.SessionsCompleted,
            persistedTeachingSessions,
            "relation+knowledgeRef 전승 진전 회차 연속성");
        var transmissionHistory = completedCheckpoint.Relations!.Relations
            .SelectMany(relation => relation.History)
            .FirstOrDefault(history => history.Kind is
                RelationHistoryKind.TeachingTransmissionEstablished
                or RelationHistoryKind.TeachingTransmissionUpdated);
        True(
            transmissionHistory.Sequence > 0
            && transmissionHistory.TeachingTarget is not null
            && transmissionHistory.KnowledgeRef.Value != 0
            && transmissionHistory.ProvenanceId.Value != 0,
            "중요 전승 관계 역사는 대상 Ref·provenance를 영구 보존");

        var jointCultivationTick = GameCalendar.GetNextMonthStart(
            new SimulationTick(completedCheckpoint.Tick)).Value;
        var jointAgents = completedCheckpoint.Agents.ToArray();
        for (var index = 0; index < jointAgents.Length; index++)
        {
            if (index == activeTeacher || index == learnerIndex)
            {
                jointAgents[index] = jointAgents[index] with
                {
                    Activity = AgentActivity.Cultivating,
                    NextDecisionTick = jointCultivationTick,
                    Opponent = -1,
                    Cultivation = jointAgents[index].Cultivation!.Value with
                    {
                        ActionPlan = CultivationActionPlanState.Empty,
                    },
                };
            }
            else if (jointAgents[index].Activity != AgentActivity.Reincarnating
                && jointAgents[index].NextDecisionTick != long.MaxValue)
            {
                // 월 경계 랭킹 계약으로 동일 seed에서 선택되는 교사가 자신의 스승도 가진
                // 경우가 있다. 이 검증은 지정한 사제 두 명의 공동 수행만 보는 테스트이므로
                // 다른 수선자의 대기 중 수행 상태를 함께 격리한다.
                jointAgents[index] = jointAgents[index] with
                {
                    Activity = jointAgents[index].Activity == AgentActivity.Cultivating
                        ? AgentActivity.TrainingTechnique
                        : jointAgents[index].Activity,
                    Cultivation = jointAgents[index].Activity == AgentActivity.Cultivating
                        ? jointAgents[index].Cultivation!.Value with
                        {
                            ActionPlan = CultivationActionPlanState.Empty,
                        }
                        : jointAgents[index].Cultivation,
                    NextDecisionTick = Math.Max(
                        jointAgents[index].NextDecisionTick,
                        checked(jointCultivationTick + GameCalendar.TicksPerYear)),
                };
            }
        }

        var jointWorld = World.Restore(completedCheckpoint with { Agents = jointAgents });
        var instructorExperienceBefore = jointWorld.GetAgentSnapshot(
            new AgentId(activeTeacher)).Cultivation.CultivationExperience;
        jointWorld.AdvanceTo(new SimulationTick(jointCultivationTick));
        var instructorCultivation = jointWorld.GetAgentSnapshot(
            new AgentId(activeTeacher)).Cultivation;
        var learnerCultivation = jointWorld.GetAgentSnapshot(
            new AgentId(learnerIndex)).Cultivation;
        var instructorPlan = instructorCultivation.ActionPlan
            ?? throw new InvalidOperationException("사제 공동 수행의 교사 계획 누락");
        var learnerPlan = learnerCultivation.ActionPlan
            ?? throw new InvalidOperationException("사제 공동 수행의 학습자 계획 누락");
        True(
            instructorPlan.TeachingRole == CultivationTeachingRole.Instructor,
            $"공동 수행 교사 역할 · 실제 {instructorPlan.TeachingRole} · 상대 {instructorPlan.TeachingPartner}");
        True(
            instructorPlan.TeachingPartner == new AgentId(learnerIndex),
            "공동 수행 교사 대상");
        True(
            learnerPlan.TeachingRole == CultivationTeachingRole.Learner,
            "공동 수행 학습자 역할");
        True(
            learnerPlan.TeachingPartner == new AgentId(activeTeacher),
            "공동 수행 학습자 스승");
        True(
            learnerPlan.TeachingBonusBasisPoints > CultivationPlanRules.NeutralFactorBasisPoints,
            "스승 지식·경험과 학습자 자질에 따른 수행 흡수 보너스");
        True(
            instructorCultivation.CultivationExperience > instructorExperienceBefore,
            "가르치며 함께 수행한 경험은 교사에게 귀속");

        var restored = World.Restore(WorldSaveCodec.Decode(WorldSaveCodec.Encode(completedCheckpoint)));
        Equal(isolated.ComputeStateHash(), restored.ComputeStateHash(), "Teaching codec·hash 왕복");
        var beforeObservation = isolated.ComputeStateHash();
        _ = isolated.GetAgentSnapshot(
            new AgentId(activeTeacher),
            includeRelations: true,
            includeTeaching: true);
        Equal(beforeObservation, isolated.ComputeStateHash(), "Teaching·관계 관찰 무영향");
    }

    public static void WorldPersonalLogsAndPermanentChronicle()
    {
        var world = World.Create(772211, 1);
        var endTick = GameCalendar.GetMonthStart(180).Value - 1;
        world.AdvanceTo(new SimulationTick(endTick));
        var personalLog = world.GetAgentPersonalLog(new AgentId(0));
        True(personalLog.Count <= World.PersonalLogCapacity, "개인 로그 최대 100줄");
        True(
            personalLog.Zip(personalLog.Skip(1), (left, right) => left.Sequence < right.Sequence).All(value => value),
            "개인 로그는 발생 순서대로 유지되어야 합니다.");
        True(personalLog.Any(entry => entry.Kind == PersonalLogKind.ActionCompleted), "개인 행동 로그");
        True(
            personalLog.Any(entry =>
                entry.Kind == PersonalLogKind.ActionCompleted && entry.Value > 1),
            "같은 장기 행동은 로그 행 추가 대신 기간 수치를 갱신");

        var restoredLogWorld = World.Restore(world.CreateCheckpoint());
        True(
            personalLog.SequenceEqual(restoredLogWorld.GetAgentPersonalLog(new AgentId(0))),
            "개인 로그 저장 왕복");

        var busyWorld = World.Create(772212, 2_000);
        busyWorld.AdvanceTo(new SimulationTick(8_000));
        var worldLog = busyWorld.CreateSnapshot(10, 500).RecentEvents;
        True(worldLog.Count <= World.WorldLogCapacity, "세계 로그 최대 100줄");
        True(
            worldLog.All(entry => entry.Kind is not (
                WorldEventKind.CombatStarted
                or WorldEventKind.CombatPhysicalAttack
                or WorldEventKind.CombatSpellCast
                or WorldEventKind.CombatGuarded
                or WorldEventKind.CombatRetreated
                or WorldEventKind.MinorStageBreakthrough
                or WorldEventKind.BreakthroughFailed
                or WorldEventKind.MissionCompleted
                or WorldEventKind.MaterialSold
                or WorldEventKind.MaterialPurchased
                or WorldEventKind.PillPurchased)),
            "전체 로그에는 반복적인 개인 사건이 노출되면 안 됩니다.");

        var chronicleWorld = World.Create(772213, 1);
        var checkpoint = chronicleWorld.CreateCheckpoint();
        var agent = checkpoint.Agents[0];
        var permanentEntries = Enumerable.Range(1, 150)
            .Select(sequence => new LifeChronicleEntry(
                sequence,
                new SimulationTick(0),
                1,
                0,
                agent.Name,
                ChronicleEventKind.FortuitousEncounter,
                RealmStage.QiRefining,
                MinorStage.Floor1,
                sequence))
            .ToArray();
        var legacy = checkpoint with
        {
            SchemaVersion = 31,
            ChronicleSequence = permanentEntries.Length,
            Agents = [agent with { Chronicle = permanentEntries }],
        };
        var ordinary = World.Restore(legacy, Array.Empty<AgentId>());
        Equal(100, ordinary.GetAgentChronicle(new AgentId(0)).Count,
            "일반 인물 세부 연대기는 최근 100건");
        Equal(150, ordinary.GetAgentPersonalCenturyChronicle(new AgentId(0))[0].EventCount,
            "삭제된 상세 사건은 100년 종합 집계에 보존");
        var ordinaryLoaded = World.Restore(
            WorldSaveCodec.Decode(WorldSaveCodec.Encode(ordinary.CreateCheckpoint())));
        Equal(100, ordinaryLoaded.GetAgentChronicle(new AgentId(0)).Count,
            "일반 인물 세부 연대기 저장 왕복");

        var watched = World.Restore(legacy, [new AgentId(0)]);
        Equal(150, watched.GetAgentChronicle(new AgentId(0)).Count,
            "관심등록 인물 세부 연대기는 제한 없이 보존");
        var watchedLoaded = World.Restore(
            WorldSaveCodec.Decode(WorldSaveCodec.Encode(watched.CreateCheckpoint())));
        Equal(150, watchedLoaded.GetAgentChronicle(new AgentId(0)).Count,
            "관심등록 보존 상태와 세부 연대기 저장 왕복");
        watchedLoaded.SetDetailedChronicleProtection(new AgentId(0), isProtected: false);
        Equal(100, watchedLoaded.GetAgentChronicle(new AgentId(0)).Count,
            "관심 해제 시 최근 100건 상한 적용");
    }

    public static void PersonalCenturyChronicleProjectionTest()
    {
        var world = World.Create(772214, 1);
        var current = world.GetAgentSnapshot(new AgentId(0)) with { AgeYears = 250 };
        var detailed = new[]
        {
            new LifeChronicleEntry(
                1, new SimulationTick(0), 1, 0, current.Name,
                ChronicleEventKind.Birth, RealmStage.QiRefining, MinorStage.Floor1, 0),
            new LifeChronicleEntry(
                2, new SimulationTick(600), 1, 50, current.Name,
                ChronicleEventKind.MinorStageBreakthrough,
                RealmStage.QiRefining, MinorStage.Floor2, 0),
            new LifeChronicleEntry(
                3, new SimulationTick(1_200), 1, 100, current.Name,
                ChronicleEventKind.MajorRealmBreakthrough,
                RealmStage.FoundationEstablishment, MinorStage.Floor1, 0),
            new LifeChronicleEntry(
                4, new SimulationTick(2_388), 1, 199, current.Name,
                ChronicleEventKind.BreakthroughFailure,
                RealmStage.FoundationEstablishment, MinorStage.Floor5, 1),
            new LifeChronicleEntry(
                5, new SimulationTick(3_000), 1, 250, current.Name,
                ChronicleEventKind.TechniqueAcquired,
                RealmStage.FoundationEstablishment, MinorStage.Floor6, 7_999),
        };

        var volumes = PersonalCenturyChronicleProjection.Create(
            detailed,
            current,
            new SimulationTick(3_000));
        Equal(3, volumes.Count, "0~99·100~199·200~299세 3권");
        True(volumes[0].IsComplete, "첫 100년 종합 연대기 완료");
        Equal(1, volumes[0].GrowthEventCount, "첫 100년 성장 사건 집계");
        True(volumes[1].IsComplete, "둘째 100년 종합 연대기 완료");
        Equal(1, volumes[1].MajorRealmBreakthroughCount, "대경지 돌파 집계");
        Equal(1, volumes[1].BreakthroughFailureCount, "돌파 실패 집계");
        True(!volumes[2].IsComplete, "현재 100년 구간 작성 중");
        Equal(1, volumes[2].AcquisitionEventCount, "획득 사건 집계");
        Equal(5, detailed.Length, "legacy 투영 입력 불변");

        var sparseCurrent = current with { AgeYears = 350 };
        var sparse = PersonalCenturyChronicleProjection.Create(
            [detailed[0], detailed[^1] with { AgeYears = 350 }],
            sparseCurrent,
            new SimulationTick(4_000));
        Equal(4, sparse.Count, "사건이 없는 100년 구간도 역사서 권으로 유지");
        Equal(0, sparse[1].EventCount, "100~199세 빈 권");
        Equal(0, sparse[2].EventCount, "200~299세 빈 권");
        Equal(199L, sparse[1].ObservedThroughAgeYears, "완료된 빈 권 관찰 종료 나이");
        True(sparse[1].IsComplete && sparse[2].IsComplete, "생존한 빈 권 완결");
    }

    static byte[] RemoveSchema22TeachingProperties(byte[] container)
    {
        using var input = new MemoryStream(container, writable: false);
        using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
        var magic = reader.ReadBytes(4);
        var containerVersion = reader.ReadInt32();
        var payloadLength = reader.ReadInt32();
        _ = reader.ReadBytes(32);
        var payload = reader.ReadBytes(payloadLength);

        using var compressed = new MemoryStream(payload, writable: false);
        using var decompressor = new BrotliStream(compressed, CompressionMode.Decompress);
        using var jsonBuffer = new MemoryStream();
        decompressor.CopyTo(jsonBuffer);
        var root = JsonNode.Parse(jsonBuffer.ToArray())!.AsObject();
        foreach (var agentNode in root["agents"]!.AsArray())
        {
            agentNode!.AsObject().Remove("teaching");
        }

        foreach (var relationNode in root["relations"]!["relations"]!.AsArray())
        {
            var relation = relationNode!.AsObject();
            relation.Remove("techniqueTeachingSessions");
            relation.Remove("spellTeachingSessions");
            relation.Remove("techniqueTransmissionEstablished");
            relation.Remove("spellTransmissionEstablished");
            relation.Remove("techniqueKnowledgeRef");
            relation.Remove("spellKnowledgeRef");
            foreach (var historyNode in relation["history"]!.AsArray())
            {
                var history = historyNode!.AsObject();
                history.Remove("teachingTarget");
                history.Remove("knowledgeRef");
                history.Remove("provenanceId");
            }
        }

        var migratedJson = Encoding.UTF8.GetBytes(root.ToJsonString());
        using var payloadBuffer = new MemoryStream();
        using (var compressor = new BrotliStream(
            payloadBuffer,
            CompressionLevel.Optimal,
            leaveOpen: true))
        {
            compressor.Write(migratedJson);
        }

        var migratedPayload = payloadBuffer.ToArray();
        var checksum = SHA256.HashData(migratedPayload);
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.Write(magic);
        writer.Write(containerVersion);
        writer.Write(migratedPayload.Length);
        writer.Write(checksum);
        writer.Write(migratedPayload);
        writer.Flush();
        return output.ToArray();
    }
}
