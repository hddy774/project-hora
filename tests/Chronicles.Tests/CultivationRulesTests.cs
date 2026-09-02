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

internal static class CultivationRulesTests
{
    public static void GameTimeAndRealmRules()
    {
        var expectedVersion = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "VERSION")).Trim();
        Equal(expectedVersion, ProductInfo.Version, "제품 버전");
        Equal(2_000L, GameCalendar.TicksPerYear, "1게임년 tick 수");
        Equal(10L, GameCalendar.GetYear(new SimulationTick(20_000)), "tick의 게임년 변환");
        Equal(1d, GameCalendar.GetRealSecondsPerGameYear(10), "10배속 게임년 현실 초");
        Equal(
            200,
            SimulationPacing.GetTicksPerPulse(1) * 1_000 / SimulationPacing.PulseMilliseconds,
            "1배속 초당 tick");
        True(
            SimulationPacing.GetTicksPerPulse(100) % GameCalendar.TicksPerYear != 0,
            "100배속 화면 갱신 보폭은 정확히 1년이면 안 됩니다.");
        True(
            Enumerable.Range(1, 10)
                .Select(step => GameCalendar.GetDate(new SimulationTick(
                    (long)SimulationPacing.GetTicksPerPulse(100) * step)).Month)
                .Distinct()
                .Count() > 1,
            "100배속 월 표시 변화");

        var expectedLifespans = new long[]
        {
            150, 300, 600, 1_200, 3_000, 9_000, 20_000, 100_000, 100_000, 1_000_000,
        };
        Equal(expectedLifespans.Length, RealmRules.All.Count, "10대경지 수");
        for (var index = 0; index < expectedLifespans.Length; index++)
        {
            Equal(expectedLifespans[index], RealmRules.GetProfile((RealmStage)index).MaximumLifespanYears, "경지별 수명");
            Equal(
                expectedLifespans[index] / 3m,
                RealmRules.GetProfile((RealmStage)index).TargetCultivationYears,
                "기준 수행 기간은 수명의 3분의 1");
        }

        using var runner = new SimulationRunner(World.Create(1, 10));
        foreach (var speed in new[] { 1, 10, 25, 50, 100 })
        {
            runner.SetSpeed(speed);
            Equal(speed, runner.Speed, "지원 배속");
        }

        Throws<ArgumentOutOfRangeException>(() => runner.SetSpeed(4), "미지원 배속");
    }

    public static void CalendarAndMonthlyActionLimit()
    {
        Equal(new GameDate(0, 1, 1), GameCalendar.GetDate(new SimulationTick(0)), "세계 시작일");
        Equal(new GameDate(0, 1, 30), GameCalendar.GetDate(new SimulationTick(166)), "첫 달 마지막 날");
        Equal(new GameDate(0, 2, 1), GameCalendar.GetDate(new SimulationTick(167)), "둘째 달 첫날");
        Equal(new GameDate(0, 12, 30), GameCalendar.GetDate(new SimulationTick(1_999)), "첫해 마지막 날");
        Equal(new GameDate(1, 1, 1), GameCalendar.GetDate(new SimulationTick(2_000)), "다음해 첫날");
        Equal(167L, GameCalendar.GetMonthStart(1).Value, "둘째 달 시작 tick");
        Equal(334L, GameCalendar.GetMonthStart(2).Value, "셋째 달 시작 tick");
        Equal(2_000L, GameCalendar.GetMonthStart(12).Value, "다음해 시작 tick");

        var world = World.Create(99001, 500);
        var previousActions = new int[world.Population];
        for (var month = 0; month < 24; month++)
        {
            var endOfMonth = GameCalendar.GetMonthStart(month + 1).Value - 1;
            world.AdvanceTo(new SimulationTick(endOfMonth));
            for (var index = 0; index < world.Population; index++)
            {
                var agent = world.GetAgentSnapshot(new AgentId(index));
                var delta = agent.ActionsTaken - previousActions[index];
                True(delta is >= 0 and <= 1, $"Agent {index}의 월 행동 횟수는 최대 1회여야 합니다.");
                previousActions[index] = agent.ActionsTaken;
            }
        }

        True(previousActions.All(count => count > 0), "모든 AI가 월별 행동 일정에 참여해야 합니다.");
        var checkpoint = world.CreateCheckpoint();
        var restored = World.Restore(checkpoint);
        for (var index = 0; index < world.Population; index++)
        {
            var originalAgent = world.GetAgentSnapshot(new AgentId(index));
            var restoredAgent = restored.GetAgentSnapshot(new AgentId(index));
            Equal(originalAgent.ActionsTaken, restoredAgent.ActionsTaken, "저장 후 월 행동 누계");
            True(
                Nullable.Equals(originalAgent.LastActionDate, restoredAgent.LastActionDate),
                "저장 후 마지막 행동일");
        }
    }

    public static void CultivationAuthorityRules()
    {
        Equal(9, RealmRules.MinorStageCount, "모든 대경지는 1~9층");
        Equal(1_000, CultivationRules.GetMonthlyRealmWorkGain(5_000), "자질 50.00 월 Work");
        Equal(500, CultivationRules.GetMonthlyRealmWorkGain(2_500), "자질 25.00 월 Work");
        Equal(2_000, CultivationRules.GetMonthlyRealmWorkGain(10_000), "자질 100.00 월 Work");
        foreach (var profile in RealmRules.All)
        {
            var expected = checked(profile.MaximumLifespanYears * 4L * CultivationRules.BaseMonthlyWork);
            var totalRealmWork = RealmRules.GetTotalRealmRequiredWork(profile.Stage);
            var baselineMonths = totalRealmWork / (decimal)CultivationRules.BaseMonthlyWork;
            Equal(expected, totalRealmWork, "대경지 1층→9층 Work 합계");
            Equal(
                profile.MaximumLifespanYears * 4m,
                baselineMonths,
                "자질 50.00에서 대경지 1층→9층 대원만 전체 기준 개월");
            Equal(
                profile.TargetCultivationYears,
                baselineMonths / GameCalendar.MonthsPerYear,
                "대경지 전체 기준 수행 기간은 수명의 3분의 1");
            True(
                RealmRules.GetMinorStageRequiredWork(profile.Stage, 1) < totalRealmWork,
                "층 하나에 대경지 전체 기준 기간을 적용하면 안 됩니다.");
        }

        var gate = CultivationRules.EvaluateEntryGate(1, 5_500, 5_000);
        Equal(6_250, gate.GateScoreHundredths, "입문 GateScore");
        True(gate.Passed, "Core 40.00 및 GateScore 60.00 통과");
        True(CultivationRules.ProvisionalBalances.All(item => item.RequiresReview), "임시 수치는 재검토 표시");
        Equal(8, CultivationFormulaRegistry.All.Count, "F-CULT 공식 레지스트리 수");
        Equal(
            8,
            CultivationFormulaRegistry.All
                .Select(item => item.StableId)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            "F-CULT 안정 ID 중복 금지");
        True(
            CultivationRules.GetAttemptClass(9, greatPerfection: false)
                == BreakthroughClass.FloorNineCompletion,
            "9층 Work 완료 뒤 별도 소경지 돌파 분류");
        True(
            CultivationRules.GetAttemptClass(9, greatPerfection: true)
                == BreakthroughClass.MajorRealm,
            "대원만 뒤 대경지 돌파 분류");
        Throws<ArgumentOutOfRangeException>(
            () => CultivationFormulaRegistry.Get(CultivationFormulaKind.None),
            "빈 수행 공식 ID 거부");
    }

    public static void CultivationDetailProgressProjection()
    {
        Equal(1, CultivationDetailProgressRules.CurrentRevision, "세부 수행 정의 revision");
        True(
            CultivationDetailProgressRules.ProvisionalBalance.RequiresReview,
            "미확정 단계와 가중치는 PC_TEMP_REVIEW");

        var allStableIds = new HashSet<string>(StringComparer.Ordinal);
        var entryDefinitions = CultivationDetailProgressRules.GetDefinitions(
            CultivationLifecyclePhase.EntryTraining,
            RealmStage.QiRefining);
        Equal(CultivationDetailProgressRules.StageCount, entryDefinitions.Count, "범인 입문 세부 단계 수");
        foreach (var definition in entryDefinitions)
        {
            True(allStableIds.Add(definition.StableId), "범인 입문 세부 단계 안정 ID 고유");
        }

        foreach (var realm in RealmRules.All.Select(profile => profile.Stage))
        {
            var definitions = CultivationDetailProgressRules.GetDefinitions(
                CultivationLifecyclePhase.RealmCultivation,
                realm);
            Equal(CultivationDetailProgressRules.StageCount, definitions.Count, $"{realm} 세부 단계 수");
            Equal(
                CultivationDetailProgressRules.BasisPointMaximum,
                definitions.Sum(definition => definition.WeightBasisPoints),
                $"{realm} 세부 단계 가중치 합");
            True(definitions.All(definition => definition.RequiresReview), $"{realm} 임시 검토 표식");
            foreach (var definition in definitions)
            {
                True(allStableIds.Add(definition.StableId), $"{realm} 세부 단계 안정 ID 고유");
            }
        }

        Equal(
            55,
            allStableIds.Count,
            "범인 입문 5개와 10대경지 50개 안정 ID");
        var qiDefinitions = CultivationDetailProgressRules.GetDefinitions(
            CultivationLifecyclePhase.RealmCultivation,
            RealmStage.QiRefining);
        Equal("영기 감지", qiDefinitions[0].KoreanName, "연기경 1단계 영기 감지");
        Equal("천지 감응", qiDefinitions[1].KoreanName, "연기경 2단계 감응");
        Equal("원리 이해·깨달음", qiDefinitions[2].KoreanName, "연기경 3단계 깨달음");
        Equal("영기 인도", qiDefinitions[3].KoreanName, "연기경 4단계 영기 인도");
        Equal("단전 축적", qiDefinitions[4].KoreanName, "연기경 5단계 단전 축적");

        var totalWork = RealmRules.GetTotalRealmRequiredWork(RealmStage.QiRefining);
        var checkpoints = new[] { 0, 1_000, 2_500, 4_500, 7_000, 10_000 };
        var previousOverall = -1;
        for (var index = 0; index < checkpoints.Length; index++)
        {
            var target = totalWork * checkpoints[index] / 10_000;
            var snapshot = CreateQiDetailAtAccumulatedWork(target, totalWork);
            Equal(checkpoints[index], snapshot.OverallProgressBasisPoints, "연기경 전체 세부 진척 경계");
            True(snapshot.OverallProgressBasisPoints >= previousOverall, "세부 수행 진척 단조 증가");
            previousOverall = snapshot.OverallProgressBasisPoints;
            if (index < checkpoints.Length - 1)
            {
                Equal(index, snapshot.CurrentStageIndex, "가중치 경계의 현재 세부 단계");
                True(
                    snapshot.Stages.Take(index).All(stage =>
                        stage.State == CultivationDetailStageState.Completed),
                    "이전 세부 단계 완료");
                True(
                    snapshot.Stages[index].State == CultivationDetailStageState.Active,
                    "현재 세부 단계 활성");
            }
            else
            {
                True(
                    snapshot.Stages.All(stage =>
                        stage.State == CultivationDetailStageState.Completed),
                    "연기경 전체 Work 완료 시 모든 세부 단계 완료");
            }
        }

        var world = World.Create(741105, 10);
        var beforeHash = world.ComputeStateHash();
        var beforeAgent = world.GetAgentSnapshot(new AgentId(0));
        using (var runner = new SimulationRunner(world))
        {
            var hidden = runner.GetObservation(
                new AgentId(0),
                sections: ObservationSections.None);
            True(hidden.SelectedAgent.Cultivation.DetailProgress is null, "접힌 세부 수행 UI는 DTO 미생성");
            Equal(0, hidden.SelectedAgent.Cultivation.EntryGates.Count, "수행 탭이 닫히면 입문 Gate DTO 0개");
            Equal(0, hidden.SelectedAgent.Cultivation.Components.Count, "수행 탭이 닫히면 경지 구성요소 DTO 0개");
            True(hidden.SelectedAgent.Cultivation.ActionPlan is null, "수행 탭이 닫히면 계획 DTO 미생성");
            True(
                hidden.SelectedAgent.Cultivation.FoundationFormation is null,
                "수행 탭이 닫히면 축기 형성 DTO 미생성");
            var cultivationVisible = runner.GetObservation(
                new AgentId(0),
                sections: ObservationSections.Cultivation);
            Equal(3, cultivationVisible.SelectedAgent.Cultivation.EntryGates.Count, "수행 탭이 열리면 입문 Gate DTO 생성");
            True(
                cultivationVisible.SelectedAgent.Cultivation.DetailProgress is null,
                "세부 토글이 닫히면 수행 단계 DTO는 계속 미생성");
            var visible = runner.GetObservation(
                new AgentId(0),
                sections: ObservationSections.Cultivation | ObservationSections.CultivationDetail);
            Equal(
                CultivationDetailProgressRules.StageCount,
                visible.SelectedAgent.Cultivation.DetailProgress!.Stages.Count,
                "펼친 세부 수행 UI만 5개 DTO 생성");
            Equal(
                "범인 입문 준비",
                visible.SelectedAgent.Cultivation.DetailProgress.ScopeKoreanName,
                "미입문자는 연기경으로 위장하지 않음");

            for (var warmup = 0; warmup < 10; warmup++)
            {
                _ = runner.GetObservation(new AgentId(0), sections: ObservationSections.None);
                _ = runner.GetObservation(new AgentId(0), sections: ObservationSections.Cultivation);
            }

            var hiddenStart = GC.GetAllocatedBytesForCurrentThread();
            for (var sample = 0; sample < 500; sample++)
            {
                _ = runner.GetObservation(new AgentId(0), sections: ObservationSections.None);
            }
            var hiddenAllocated = GC.GetAllocatedBytesForCurrentThread() - hiddenStart;
            var visibleStart = GC.GetAllocatedBytesForCurrentThread();
            for (var sample = 0; sample < 500; sample++)
            {
                _ = runner.GetObservation(new AgentId(0), sections: ObservationSections.Cultivation);
            }
            var visibleAllocated = GC.GetAllocatedBytesForCurrentThread() - visibleStart;
            True(hiddenAllocated < visibleAllocated, "닫힌 수행 탭은 열린 탭보다 적게 할당");
            Console.WriteLine(
                $"CULTIVATION DETAIL UI PERF: 닫힘 {hiddenAllocated:N0} B / " +
                $"수행 탭 {visibleAllocated:N0} B · " +
                $"할당 감소 {(1m - (hiddenAllocated / (decimal)visibleAllocated)) * 100m:0.0}% (500회)");
        }

        var afterAgent = world.GetAgentSnapshot(new AgentId(0));
        Equal(beforeHash, world.ComputeStateHash(), "세부 수행 관찰은 권위 상태·RNG 무영향");
        Equal(beforeAgent.CultivationProgress, afterAgent.CultivationProgress, "세부 수행 관찰은 Work 불변");
        Equal(beforeAgent.TechniqueQuality, afterAgent.TechniqueQuality, "깨달음 표시는 공법 품질 불변");
        Equal(beforeAgent.SpellQuality, afterAgent.SpellQuality, "깨달음 표시는 법술 품질 불변");

        static CultivationDetailProgressSnapshot CreateQiDetailAtAccumulatedWork(
            long accumulatedWork,
            long totalWork)
        {
            if (accumulatedWork >= totalWork)
            {
                return CultivationDetailProgressRules.Create(
                    CultivationLifecyclePhase.RealmCultivation,
                    RealmStage.QiRefining,
                    RealmRules.MinorStageCount,
                    RealmRules.GetMinorStageRequiredWork(
                        RealmStage.QiRefining,
                        RealmRules.MinorStageCount),
                    floorNineCompleted: true,
                    greatPerfectionAchieved: false);
            }

            var remaining = accumulatedWork;
            for (var floor = 1; floor <= RealmRules.MinorStageCount; floor++)
            {
                var requirement = RealmRules.GetMinorStageRequiredWork(RealmStage.QiRefining, floor);
                if (remaining <= requirement)
                {
                    return CultivationDetailProgressRules.Create(
                        CultivationLifecyclePhase.RealmCultivation,
                        RealmStage.QiRefining,
                        floor,
                        checked((int)remaining),
                        floorNineCompleted: false,
                        greatPerfectionAchieved: false);
                }

                remaining -= requirement;
            }

            throw new InvalidOperationException("연기경 누적 Work를 층에 매핑할 수 없습니다.");
        }
    }

    public static void EntryGateRecoveryAndAppliedWork()
    {
        var blockedWorld = World.Create(551120, 1);
        var blockedCheckpoint = blockedWorld.CreateCheckpoint();
        var blockedAgent = TestCheckpointFactory.WithSynchronizedAptitudeDetails(blockedCheckpoint.Agents[0] with
        {
            SpiritualRoot = 0,
            Bone = 0,
            Constitution = 0,
            Comprehension = 0,
            Mind = 0,
            Soul = 0,
            CultivationProgress = CultivationRules.RequiredEntryWork,
            CultivationState = CultivationState.EntryTraining,
            CultivationBottleneckCause = CultivationBottleneckCause.None,
            Activity = AgentActivity.Cultivating,
            NextDecisionTick = 1,
        });
        blockedWorld = World.Restore(blockedCheckpoint with { Agents = [blockedAgent] });
        blockedWorld.AdvanceTo(new SimulationTick(1));
        var blocked = blockedWorld.GetAgentSnapshot(new AgentId(0));
        True(blocked.CultivationState == CultivationState.Bottleneck, "Gate 실패는 실제 병목 상태");
        True(blocked.BottleneckCause == CultivationBottleneckCause.EntryGate, "입문 Gate 병목 원인");
        Equal(0, blocked.Cultivation.LatestTrace.WorkGain, "이미 가득 찬 입문 Work의 실제 적용 증가량");

        blockedWorld.AdvanceTo(GameCalendar.GetMonthStart(1));
        var tempered = blockedWorld.GetAgentSnapshot(new AgentId(0));
        True(
            tempered.Cultivation.EntryGates.Any(gate => gate.TemperingBonusHundredths > 0),
            "실패 Gate에 단련 보정이 누적되어야 합니다.");
        var restored = World.Restore(blockedWorld.CreateCheckpoint());
        Equal(blockedWorld.ComputeStateHash(), restored.ComputeStateHash(), "입문 Gate 단련 저장 왕복");

        restored.AdvanceTo(GameCalendar.FromYears(10));
        var entered = restored.GetAgentSnapshot(new AgentId(0));
        True(
            entered.CultivationState != CultivationState.EntryTraining
                && !(entered.CultivationState == CultivationState.Bottleneck
                    && entered.BottleneckCause == CultivationBottleneckCause.EntryGate),
            "고정 선천자질이 낮아도 Gate 단련으로 입문 정체를 해소해야 합니다.");
        True(
            restored.GetAgentChronicle(new AgentId(0))
                .Any(entry => entry.Kind == ChronicleEventKind.CultivationEntered),
            "Gate 단련 뒤 입문 연대기");

        var cappedWorld = World.Create(551121, 1);
        var cappedCheckpoint = cappedWorld.CreateCheckpoint();
        var cappedAgent = TestCheckpointFactory.WithSynchronizedAptitudeDetails(cappedCheckpoint.Agents[0] with
        {
            SpiritualRoot = 10_000,
            Bone = 10_000,
            Constitution = 10_000,
            Comprehension = 10_000,
            Mind = 10_000,
            Soul = 10_000,
            CultivationProgress = CultivationRules.RequiredEntryWork - 1_000,
            CultivationState = CultivationState.EntryTraining,
            CultivationBottleneckCause = CultivationBottleneckCause.None,
            Activity = AgentActivity.Cultivating,
            NextDecisionTick = 1,
        });
        cappedWorld = World.Restore(cappedCheckpoint with { Agents = [cappedAgent] });
        cappedWorld.AdvanceTo(new SimulationTick(1));
        Equal(
            1_000,
            cappedWorld.GetAgentSnapshot(new AgentId(0)).Cultivation.LatestTrace.WorkGain,
            "입문 Work 표시는 원시 gain이 아니라 cap 적용분이어야 합니다.");
    }

    public static void CultivationStateMachine()
    {
        var baseline = World.Create(551122, 1);
        var requirement = RealmRules.GetMinorStageRequiredWork(RealmStage.QiRefining, 1);
        var checkpoint = baseline.CreateCheckpoint();
        var preparedAgent = checkpoint.Agents[0] with
        {
            CultivationProgress = requirement,
            CultivationState = CultivationState.RealmCultivation,
            CultivationBottleneckCause = CultivationBottleneckCause.None,
            Activity = AgentActivity.Cultivating,
            NextDecisionTick = 1,
        };
        var world = World.Restore(checkpoint with { Agents = [preparedAgent] });
        var randomBeforePreparation = world.CreateCheckpoint().RandomState;

        world.AdvanceTo(new SimulationTick(1));
        var preparing = world.GetAgentSnapshot(new AgentId(0));
        True(
            preparing.CultivationState == CultivationState.PreparingBreakthrough,
            "Work 충족 뒤에는 즉시 확률 판정하지 않고 돌파 준비에 들어가야 합니다.");
        Equal(randomBeforePreparation, world.CreateCheckpoint().RandomState, "돌파 준비는 RNG를 소비하지 않음");

        world.AdvanceTo(GameCalendar.GetMonthStart(1));
        var preflight = world.GetAgentSnapshot(new AgentId(0));
        True(preflight.CultivationState == CultivationState.PreflightReady, "준비 뒤 Preflight 대기 상태");
        Equal(randomBeforePreparation, world.CreateCheckpoint().RandomState, "Preflight 준비는 RNG를 소비하지 않음");

        world.AdvanceTo(GameCalendar.GetMonthStart(2));
        var attempted = world.GetAgentSnapshot(new AgentId(0));
        True(
            attempted.CultivationState is CultivationState.Stable or CultivationState.PreparingBreakthrough,
            "돌파 시도는 성공 안정 또는 실제 실패 후 재준비로 끝나야 합니다.");

        var restored = World.Restore(world.CreateCheckpoint());
        var restoredAgent = restored.GetAgentSnapshot(new AgentId(0));
        True(attempted.CultivationState == restoredAgent.CultivationState, "수행 상태 저장 왕복");
        True(attempted.BottleneckCause == restoredAgent.BottleneckCause, "병목 원인 저장 왕복");

        foreach (var profile in RealmRules.All)
        {
            True(!string.IsNullOrWhiteSpace(profile.StableId), "Realm Profile 안정 ID");
            Equal(RealmProfile.CurrentRevision, profile.Revision, "Realm Profile revision");
            Equal(
                profile.Stage != RealmStage.QiRefining,
                profile.RequiresStabilization,
                "대경지 진입 뒤에만 안착 상태");
        }
    }

    public static void LongCultivationPlanConditionsAndPersistence()
    {
        Equal(1, CultivationPlanRules.MinimumPlanMonths, "최소 행동 계획 1개월");
        Equal(120_000, CultivationPlanRules.MaximumPlanMonths, "최대 행동 계획 1만년");

        var world = World.Create(551129, 256);
        var initial = world.GetAgentSnapshot(new AgentId(0));
        var decisionRevision = initial.Decision.Latest.AuthorityRevision;
        world.AdvanceTo(initial.NextDecisionTick);
        var committed = world.GetAgentSnapshot(new AgentId(0));
        var plan = committed.Cultivation.ActionPlan
            ?? throw new InvalidOperationException("수행 행동 경계에서 장기 계획이 생성되어야 합니다.");

        True(plan.IsActive, "현재 수행 계획 활성 상태");
        True(
            plan.PlannedMonths is >= CultivationPlanRules.MinimumPlanMonths
                and <= CultivationPlanRules.MaximumPlanMonths,
            "수행 계획 기간 범위");
        True(plan.PlannedEndTick.Value > plan.StartedTick.Value, "수행 시작·종료 시점 순서");
        Equal(committed.LocationId, plan.LocationId, "수행 장소 고정");
        True(Enum.IsDefined(plan.Intensity), "수행 강도 typed 조건");
        True(Enum.IsDefined(plan.ResourcePolicy), "소비 자원 typed 조건");
        True(Enum.IsDefined(plan.FormationSupport), "수행 진법 typed 조건");
        True(Enum.IsDefined(plan.TeachingRole), "가르침 역할 typed 조건");
        True(plan.ConditionFactorBasisPoints is >= 5_000 and <= 20_000, "조건 종합 계수 범위");
        True(plan.TeachingBonusBasisPoints is >= 10_000 and <= 13_000, "가르침 흡수 계수 범위");
        True(plan.RequiresReview, "임의 수행 조건 PC_TEMP_REVIEW 표식");
        Equal(
            decisionRevision,
            committed.Decision.Latest.AuthorityRevision,
            "장기 계획 pulse는 목표·계획을 매번 다시 계산하지 않음");

        var checkpoint = world.CreateCheckpoint();
        var persistedPlan = checkpoint.Agents[0].Cultivation!.Value.ActionPlan
            ?? throw new InvalidOperationException("수행 계획 권위 저장 누락");
        Equal(plan.AuthorityRevision, persistedPlan.AuthorityRevision, "수행 계획 revision 저장");
        Equal(plan.PlannedMonths, persistedPlan.PlannedMonths, "수행 계획 기간 저장");
        var restored = World.Restore(WorldSaveCodec.Decode(WorldSaveCodec.Encode(checkpoint)));
        Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "수행 계획 codec·hash 왕복");

        restored.AdvanceTo(plan.PlannedEndTick);
        var boundary = restored.GetAgentSnapshot(new AgentId(0));
        True(
            boundary.Decision.Latest.AuthorityRevision > decisionRevision,
            "수행 계획 종료 경계에서만 다음 행동을 판단");
    }

    public static void HighRealmHealthRatioDoesNotOverflow()
    {
        Equal(
            10_000,
            CultivationPlanRules.GetBodyConditionBasisPoints(int.MaxValue, int.MaxValue),
            "int 상한 최대 체력 비율");
        Equal(
            5_000,
            CultivationPlanRules.GetBodyConditionBasisPoints(1_000_000_000, 2_000_000_000),
            "대형 체력 50% 비율");
        Equal(
            10_000,
            CultivationPlanRules.GetBodyConditionBasisPoints(int.MaxValue, 1),
            "현재 체력이 최대값을 넘는 손상 입력 상한");
        Throws<ArgumentOutOfRangeException>(
            () => CultivationPlanRules.GetBodyConditionBasisPoints(1, 0),
            "최대 체력 0 거부");
    }

    public static void FloorNineGreatPerfectionAndFormulaTrace()
    {
        var seedWorld = World.Create(551123, 1);
        var checkpoint = seedWorld.CreateCheckpoint();
        var requirement = RealmRules.GetMinorStageRequiredWork(RealmStage.QiRefining, 9);
        var cultivation = checkpoint.Agents[0].Cultivation!.Value with
        {
            PendingCarryoverWork = 0,
            LastWorkBefore = requirement - 1,
            LastWorkGain = 0,
            FloorNineCompleted = false,
            GreatPerfectionAchieved = false,
            LatestTrace = CultivationTraceState.Empty,
            AuthorityRevision = 0,
        };
        var floorNineAgent = checkpoint.Agents[0] with
        {
            RealmStage = RealmStage.QiRefining,
            RealmLayer = 9,
            CultivationProgress = requirement - 1,
            CultivationState = CultivationState.RealmCultivation,
            CultivationBottleneckCause = CultivationBottleneckCause.None,
            Activity = AgentActivity.Cultivating,
            NextDecisionTick = 1,
            LastActionMonth = -1,
            LastActionTick = 0,
            ActionsTaken = 0,
            Cultivation = cultivation,
        };
        var world = World.Restore(checkpoint with { Agents = [floorNineAgent] });

        world.AdvanceTo(new SimulationTick(1));
        var preparing = world.GetAgentSnapshot(new AgentId(0));
        True(!preparing.Cultivation.FloorNineCompleted, "Work 충족만으로 9층을 완성하면 안 됨");
        True(preparing.CultivationState == CultivationState.PreparingBreakthrough, "9층 돌파 준비 상태");
        True(
            preparing.Cultivation.LatestTrace.FormulaKind == CultivationFormulaKind.ProgressAccumulation,
            "Work Formula Trace");
        Equal(1L, preparing.Cultivation.AuthorityRevision, "첫 수행 권위 revision");

        var nextMonth = 1L;
        world.AdvanceTo(GameCalendar.GetMonthStart(nextMonth++));
        var preflight = world.GetAgentSnapshot(new AgentId(0));
        True(preflight.CultivationState == CultivationState.PreflightReady, "9층 Preflight 준비");
        True(
            preflight.Cultivation.LatestTrace.FormulaKind == CultivationFormulaKind.BreakthroughReadiness,
            "Preflight Formula Trace");

        AgentSnapshot completed = preflight;
        for (var attempt = 0; attempt < 24 && !completed.Cultivation.FloorNineCompleted; attempt++)
        {
            world.AdvanceTo(GameCalendar.GetMonthStart(nextMonth++));
            completed = world.GetAgentSnapshot(new AgentId(0));
        }

        True(completed.Cultivation.FloorNineCompleted, "9층 소경지 돌파가 유한 시도 안에 완료되어야 함");
        True(!completed.Cultivation.GreatPerfectionAchieved, "9층 완성과 대원만은 같은 commit이면 안 됨");
        Equal(9, completed.RealmLayer, "9층 완성 뒤 층 유지");
        True(
            completed.Cultivation.LatestTrace.AttemptClass == BreakthroughClass.FloorNineCompletion,
            "9층 Typed Result");
        True(
            completed.Cultivation.LatestTrace.FormulaKind == CultivationFormulaKind.BreakthroughResult,
            "돌파 결과 Formula Trace");
        Equal(
            completed.Cultivation.AuthorityRevision,
            completed.Cultivation.LatestTrace.AuthorityRevisionAfter,
            "Trace와 수행 권위 revision 일치");
        True(
            world.GetAgentChronicle(new AgentId(0))
                .Any(entry => entry.Kind == ChronicleEventKind.FloorNineCompletion),
            "9층 소경지 완성 영구 연대기");

        var restored = World.Restore(WorldSaveCodec.Decode(WorldSaveCodec.Encode(world.CreateCheckpoint())));
        Equal(world.ComputeStateHash(), restored.ComputeStateHash(), "수행 Formula Trace 저장·codec 왕복");
        var restoredCompleted = restored.GetAgentSnapshot(new AgentId(0));
        Equal(completed.Cultivation.AuthorityRevision, restoredCompleted.Cultivation.AuthorityRevision, "수행 권위 revision 복원");

        restored.AdvanceTo(GameCalendar.GetMonthStart(nextMonth));
        var greatPerfection = restored.GetAgentSnapshot(new AgentId(0));
        True(greatPerfection.Cultivation.GreatPerfectionAchieved, "9층 완성 다음 별도 행동에서 대원만 완성");
        True(
            greatPerfection.Cultivation.LatestTrace.AttemptClass
                == BreakthroughClass.GreatPerfectionCompletion,
            "대원만 Typed Result");
        True(
            restored.GetAgentChronicle(new AgentId(0))
                .Any(entry => entry.Kind == ChronicleEventKind.GreatPerfectionAchieved),
            "대원만 영구 연대기");

        var rankingSeed = World.Create(551124, 3);
        var rankingCheckpoint = rankingSeed.CreateCheckpoint();
        var rankedAgents = rankingCheckpoint.Agents
            .Select((agent, index) => agent with
            {
                RealmStage = RealmStage.QiRefining,
                RealmLayer = 9,
                CultivationProgress = requirement,
                CultivationState = CultivationState.Stable,
                CultivationBottleneckCause = CultivationBottleneckCause.None,
                Cultivation = agent.Cultivation!.Value with
                {
                    PendingCarryoverWork = 0,
                    FloorNineCompleted = index <= 1,
                    GreatPerfectionAchieved = index == 0,
                    LatestTrace = CultivationTraceState.Empty,
                    AuthorityRevision = 0,
                },
            })
            .ToArray();
        var rankingWorld = World.Restore(rankingCheckpoint with { Agents = rankedAgents });
        var realmRanking = rankingWorld.CreateSnapshot(
            3,
            0,
            ObservationSections.RealmRanking).RealmRankings;
        Equal(new AgentId(0), realmRanking[0].AgentId, "대원만이 같은 경지·층 1위");
        Equal(new AgentId(1), realmRanking[1].AgentId, "9층 완성이 Work 충족보다 우선");
        Equal(new AgentId(2), realmRanking[2].AgentId, "Work 충족 미돌파는 같은 층 최하위");
        True(realmRanking[0].Score > realmRanking[1].Score, "대원만 경지 점수 분리");
        True(realmRanking[1].Score > realmRanking[2].Score, "9층 완성 경지 점수 분리");
    }

    public static void CultivationBreakthroughChronicle()
    {
        var minorWorld = World.Create(444001, 1);
        var checkpoint = minorWorld.CreateCheckpoint();
        var requirement = RealmRules.GetMinorStageRequiredWork(RealmStage.QiRefining, 1);
        var minorAgent = TestCheckpointFactory.WithSynchronizedAptitudeDetails(checkpoint.Agents[0] with
        {
            SpiritualRoot = 10_000,
            Bone = 10_000,
            Constitution = 10_000,
            Comprehension = 10_000,
            Mind = 10_000,
            CultivationProgress = requirement,
            CultivationState = CultivationState.PreflightReady,
            CultivationBottleneckCause = CultivationBottleneckCause.None,
            Health = checkpoint.Agents[0].MaximumHealth,
            Qi = checkpoint.Agents[0].MaximumQi,
            Activity = AgentActivity.Cultivating,
            NextDecisionTick = 1,
        });
        minorWorld = World.Restore(checkpoint with { Agents = [minorAgent] });
        for (var month = 1; month <= 12
            && minorWorld.GetAgentSnapshot(new AgentId(0)).RealmLayer == 1; month++)
        {
            minorWorld.AdvanceTo(GameCalendar.GetMonthStart(month));
        }

        Equal(2, minorWorld.GetAgentSnapshot(new AgentId(0)).RealmLayer, "1→2층 돌파");
        True(
            minorWorld.GetAgentChronicle(new AgentId(0))
                .Any(item => item.Kind == ChronicleEventKind.MinorStageBreakthrough),
            "소경지 돌파 영구 연대기");

        var majorWorld = World.Create(444002, 1);
        var majorCheckpoint = majorWorld.CreateCheckpoint();
        var majorCultivation = majorCheckpoint.Agents[0].Cultivation!.Value with
        {
            FloorNineCompleted = true,
            GreatPerfectionAchieved = true,
        };
        var majorAgent = TestCheckpointFactory.WithSynchronizedAptitudeDetails(majorCheckpoint.Agents[0] with
        {
            SpiritualRoot = 10_000,
            Bone = 10_000,
            Constitution = 10_000,
            Comprehension = 10_000,
            Mind = 10_000,
            RealmLayer = 9,
            CultivationProgress = RealmRules.GetMinorStageRequiredWork(RealmStage.QiRefining, 9),
            CultivationState = CultivationState.PreflightReady,
            CultivationBottleneckCause = CultivationBottleneckCause.None,
            TechniqueMastery = 5_000,
            SkillProgress = majorCheckpoint.Agents[0].SkillProgress!.Value with
            {
                TechniqueUnderstanding = 5_000,
            },
            Health = majorCheckpoint.Agents[0].MaximumHealth,
            Qi = majorCheckpoint.Agents[0].MaximumQi,
            Activity = AgentActivity.Cultivating,
            NextDecisionTick = 1,
            Cultivation = majorCultivation,
        });
        majorWorld = World.Restore(majorCheckpoint with { Agents = [majorAgent] });
        for (var month = 1; month <= 36
            && majorWorld.GetAgentSnapshot(new AgentId(0)).RealmStage == RealmStage.QiRefining; month++)
        {
            majorWorld.AdvanceTo(GameCalendar.GetMonthStart(month));
        }

        var foundationAgent = majorWorld.GetAgentSnapshot(new AgentId(0));
        True(
            foundationAgent.RealmStage == RealmStage.FoundationEstablishment,
            "연기→축기 대경지 돌파");
        var formation = foundationAgent.Cultivation.FoundationFormation
            ?? throw new InvalidOperationException("축기 형성 결과가 없습니다.");
        Equal(FoundationMethodRules.StableId, formation.MethodStableId, "삼도축기요결 Method Ref");
        True(formation.CompletedSteps == FoundationProcedureStep.Complete,
            "삼도축기요결 5단계 완료");
        Equal(10, formation.QualityComponentsHundredths.Count, "축기 형성 품질 10요소");
        True(formation.Source.Route != FoundationRoute.None, "축기 FormationRoute 권위");
        True(
            formation.Source.Availability is CultivationValueAvailability.Actual
                or CultivationValueAvailability.ActualZero,
            "축기 Source Snapshot 가용성");
        var foundationCheckpoint = majorWorld.CreateCheckpoint();
        True(
            foundationCheckpoint.Agents[0].Cultivation!.Value.FoundationFormation is not null,
            "축기 형성 결과 저장 연결");
        var restoredFoundation = World.Restore(
            WorldSaveCodec.Decode(WorldSaveCodec.Encode(foundationCheckpoint)));
        Equal(majorWorld.ComputeStateHash(), restoredFoundation.ComputeStateHash(),
            "축기 형성 결과 결정론 저장 왕복");
        var corruptedCultivation = foundationCheckpoint.Agents[0].Cultivation!.Value;
        var corruptedFormation = corruptedCultivation.FoundationFormation!.Value with
        {
            MethodRevision = FoundationMethodRules.CurrentRevision + 1,
        };
        Throws<InvalidDataException>(
            () => World.Restore(foundationCheckpoint with
            {
                Agents =
                [
                    foundationCheckpoint.Agents[0] with
                    {
                        Cultivation = corruptedCultivation with
                        {
                            FoundationFormation = corruptedFormation,
                        },
                    },
                ],
            }),
            "축기 형성 Method revision 손상 저장 거부");
        True(
            majorWorld.GetAgentChronicle(new AgentId(0))
                .Any(item => item.Kind == ChronicleEventKind.MajorRealmBreakthrough),
            "대경지 돌파 영구 연대기");
    }
}

