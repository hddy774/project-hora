using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Chronicles.Core;
using Chronicles.Simulation;
using static Chronicles.Presentation.PresentationText;

namespace Chronicles.Desktop;

/// <summary>
/// 선택 인물의 지연 로그·연대기·윤회 기록 투영과 표시 형식을 소유한다.
/// 개요·전투·수행 바인딩에서 기록 작업집합을 분리한다.
/// </summary>
public partial class MainWindow
{
    private void UpdateAgentLogViews()
    {
        ReleaseHiddenAgentRecordViews(AgentLogTabControl.SelectedIndex);
        if (_latestAgent is not { } agent)
        {
            return;
        }

        switch (AgentLogTabControl.SelectedIndex)
        {
            case 0:
                PersonalLogList.ItemsSource = _latestPersonalLog
                    .Where(entry => MatchesPersonalLogCategory(entry, _personalLogCategory))
                    .Reverse()
                    .Select(entry => FormatPersonalLog(agent, entry))
                    .ToArray();
                return;
            case 2:
                UpdatePersonalCenturyChronicleView();
                return;
            case 3:
                UpdateReincarnationView(agent, _latestReincarnation);
                return;
            case 4:
                UpdatePersonalRecordOutputView(agent);
                return;
            case 1:
                break;
            default:
                return;
        }

        var lifeNumbers = _latestChronicle
            .Select(entry => entry.LifeNumber)
            .Distinct()
            .OrderByDescending(value => value)
            .ToArray();
        if (_selectedChronicleLife is { } selectedLife
            && !lifeNumbers.Contains(selectedLife))
        {
            _selectedChronicleLife = null;
        }

        ChronicleLifeToggleList.ItemsSource = new[]
            {
                new LifeToggleRow(null, "전체 생", _selectedChronicleLife is null),
            }
            .Concat(lifeNumbers.Select(life => new LifeToggleRow(
                life,
                $"제 {life:N0}생",
                _selectedChronicleLife == life)))
            .ToArray();
        ChronicleList.ItemsSource = _latestChronicle
            .Where(entry => _selectedChronicleLife is null
                || entry.LifeNumber == _selectedChronicleLife)
            .Where(entry => MatchesChronicleCategory(entry.Kind, _chronicleCategory))
            .Reverse()
            .Select(FormatChronicle)
            .ToArray();
    }

    private void UpdatePersonalCenturyChronicleView()
    {
        var lifeNumbers = _latestPersonalCenturyChronicle
            .Select(entry => entry.LifeNumber)
            .Distinct()
            .OrderByDescending(value => value)
            .ToArray();
        if (_selectedPersonalCenturyLife is { } selectedLife
            && !lifeNumbers.Contains(selectedLife))
        {
            _selectedPersonalCenturyLife = null;
        }

        PersonalCenturyLifeToggleList.ItemsSource = new[]
            {
                new LifeToggleRow(null, "전체 생", _selectedPersonalCenturyLife is null),
            }
            .Concat(lifeNumbers.Select(life => new LifeToggleRow(
                life,
                $"제 {life:N0}생",
                _selectedPersonalCenturyLife == life)))
            .ToArray();
        PersonalCenturyChronicleList.ItemsSource = _latestPersonalCenturyChronicle
            .Where(entry => _selectedPersonalCenturyLife is null
                || entry.LifeNumber == _selectedPersonalCenturyLife)
            .Where(entry => MatchesPersonalCenturyCategory(entry, _personalCenturyCategory))
            .Reverse()
            .Select(FormatPersonalCenturyChronicle)
            .ToArray();
    }

    private void UpdatePersonalRecordOutputView(AgentSnapshot agent)
    {
        PersonalRecordIdentityText.Text =
            $"{agent.Name} · {agent.Id} · 현재 제 {agent.LifeNumber:N0}생";
        PersonalRecordCountText.Text =
            "기록은 각 탭을 열 때만 조회·표시합니다. 전체 파일 출력은 버튼을 누를 때 한 번 조회합니다.";
        var automatic = _logService.IsAutoPersonalOutput(agent.Id);
        AutoPersonalOutputCheckBox.IsChecked = automatic;
        if (automatic)
        {
            SetPersonalRecordOutputStatus($"{agent.Id} · 현재 열린 기록 탭 자동 출력 중");
        }
        else
        {
            SetPersonalRecordOutputStatus("개인 자동 기록 꺼짐");
        }
    }

    private void ReleaseHiddenAgentRecordViews(int selectedIndex)
    {
        if (selectedIndex != 0)
        {
            PersonalLogList.ItemsSource = null;
            _latestPersonalLog = Array.Empty<PersonalLogEntry>();
        }
        if (selectedIndex != 1)
        {
            ChronicleLifeToggleList.ItemsSource = null;
            ChronicleList.ItemsSource = null;
            _latestChronicle = Array.Empty<LifeChronicleEntry>();
        }
        if (selectedIndex != 2)
        {
            PersonalCenturyLifeToggleList.ItemsSource = null;
            PersonalCenturyChronicleList.ItemsSource = null;
            _latestPersonalCenturyChronicle = Array.Empty<PersonalCenturyChronicleEntry>();
        }
        if (selectedIndex != 3)
        {
            ClearReincarnationView();
            _latestReincarnation = null;
        }
    }

    private EventRow FormatPersonalCenturyChronicle(PersonalCenturyChronicleEntry entry)
    {
        var status = entry.IsComplete
            ? "완결"
            : $"작성 중 · {entry.ObservedThroughAgeYears:N0}세까지";
        var time =
            $"제 {entry.LifeNumber:N0}생 · {entry.StartAgeYears:N0}~{entry.EndAgeYears:N0}세 · {status} · " +
            $"세계 {FormatGameDate(GameCalendar.GetDate(entry.StartedTick))}~" +
            FormatGameDate(GameCalendar.GetDate(entry.ObservedThroughTick));
        var summary =
            $"총 {entry.EventCount:N0}건 · 성장 {entry.GrowthEventCount:N0} · 전투 {entry.CombatEventCount:N0} · " +
            $"생사 {entry.LifeEventCount:N0} · 획득 {entry.AcquisitionEventCount:N0} · " +
            $"대경지 돌파 {entry.MajorRealmBreakthroughCount:N0} · 돌파 실패 {entry.BreakthroughFailureCount:N0}\n" +
            $"권말 경지 {FormatRealm(entry.FinalRealmStage, (int)entry.FinalMinorStage)} · 당시 이름 {entry.PersonName}";
        return new EventRow(
            time,
            summary,
            (Brush)FindResource(entry.IsComplete ? "AccentBrush" : "PrimaryTextBrush"));
    }

    private static bool MatchesPersonalCenturyCategory(
        PersonalCenturyChronicleEntry entry,
        int category) => category switch
    {
        0 => true,
        1 => entry.GrowthEventCount > 0,
        2 => entry.CombatEventCount > 0,
        3 => entry.LifeEventCount > 0,
        4 => entry.AcquisitionEventCount > 0,
        _ => true,
    };

    private static string FormatEventRowFileLine(EventRow row) =>
        $"{row.Time} | {row.Summary.Replace('\n', ' ')}";

    private void ClearReincarnationView()
    {
        ReincarnationJourneyText.Text = "윤회 정보 없음";
        ReincarnationTimingText.Text = string.Empty;
        ReincarnationPeakText.Text = string.Empty;
        ReincarnationContinuityText.Text = string.Empty;
        ReincarnationLineageHeaderText.Text = "전체 생 계보";
        ReincarnationLineageList.ItemsSource = null;
        ReincarnationEmptyText.Visibility = System.Windows.Visibility.Visible;
        _projectedReincarnationAgent = null;
        _projectedReincarnationRevision = long.MinValue;
    }

    private void UpdateReincarnationView(
        AgentSnapshot agent,
        ReincarnationAgentSnapshot? reincarnation)
    {
        if (reincarnation is not { } snapshot)
        {
            ClearReincarnationView();
            return;
        }

        if (_projectedReincarnationAgent == agent.Id
            && _projectedReincarnationRevision == snapshot.AuthorityRevision)
        {
            return;
        }

        var tracesByJourney = snapshot.Traces
            .GroupBy(trace => trace.JourneyId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(trace => trace.Domain).ToArray());
        ReincarnationJourneyId? latestJourneyId = snapshot.CurrentJourney?.Id;
        if (latestJourneyId is null)
        {
            foreach (var life in snapshot.Lineage.Reverse())
            {
                if (life.JourneyId is { } journeyId)
                {
                    latestJourneyId = journeyId;
                    break;
                }
            }
        }

        if (snapshot.CurrentJourney is { } journey)
        {
            ReincarnationJourneyText.Text =
                $"{FormatReincarnationStage(journey.Stage)} · {journey.Id} · " +
                $"제 {journey.SourceLifeNumber:N0}생에서 출발 · " +
                $"원천 {FormatRealmStage(journey.SourceRealm)} · 총 {journey.RequiredYears:N0}년 · " +
                $"영혼 손상 {FormatSoulDamage(journey.SoulDamage)} · 출생 후보 {FormatBirthCandidateStatus(journey.BirthCandidateStatus)}" +
                (journey.RequiresReview
                    ? $" · {ReincarnationRules.StageReviewId} 검토값"
                    : string.Empty);
            ReincarnationTimingText.Text =
                $"시작 {FormatReincarnationDate(journey.StartedTick)} · " +
                $"단계 시작 {FormatReincarnationDate(journey.StageStartedTick)} · " +
                $"다음 단계 {FormatReincarnationDate(journey.NextTransitionTick)} · " +
                $"예정 완료 {FormatReincarnationDate(journey.RebirthReadyTick)} · " +
                $"완료일 {FormatOptionalReincarnationDate(journey.CompletedTick, "미완료")} · " +
                $"출생 대기 {journey.BirthCandidateWaitYears:N0}년 · 부모 {FormatParents(journey.ParentOnePersonId, journey.ParentTwoPersonId)}\n" +
                $"causal root {FormatCausalRecord(journey.RootCausalRecordId)} · 진입 {FormatCausalRecord(journey.EntryCausalRecordId)} · " +
                $"도하 완료 {FormatCausalRecord(journey.CrossingCausalRecordId)} · 환생 {FormatCausalRecord(journey.RebirthCausalRecordId)}";
        }
        else
        {
            var latestLife = snapshot.Lineage.Count == 0
                ? null
                : snapshot.Lineage[snapshot.Lineage.Count - 1];
            var latestCompletion = latestLife is { PredecessorId: not null }
                ? latestLife.BirthTick
                : (SimulationTick?)null;
            ReincarnationJourneyText.Text =
                $"활성 윤회 여정 없음 · {FormatLifeState(agent.Identity.LifeState)}" +
                (snapshot.RequiresReview ? " · 다음 버전 검토 계약" : string.Empty);
            ReincarnationTimingText.Text =
                $"시작 — · 단계 시작 — · 다음 단계 — · 예정 완료 — · " +
                $"완료일 {FormatOptionalReincarnationDate(
                    latestCompletion,
                    snapshot.IsCompleteLineage ? "해당 없음" : "추적 범위 밖")}";
        }

        ReincarnationPeakText.Text =
            $"역대 최고 · {FormatHistoricalPeak(snapshot.HistoricalPeak)}";
        ReincarnationContinuityText.Text =
            latestJourneyId is { } latestId
            && tracesByJourney.TryGetValue(latestId, out var latestTraces)
                ? string.Join(
                    Environment.NewLine,
                    latestTraces.Select(trace => FormatReincarnationTrace(trace, compact: false)))
                : "아직 권위 윤회 흔적 없음";

        var successorBirths = snapshot.Lineage
            .Where(life => life.PredecessorId is not null)
            .ToDictionary(life => life.PredecessorId!.Value, life => life.BirthTick);
        var rows = snapshot.Lineage
            .Reverse()
            .Select(life => CreateReincarnationLineageRow(
                life,
                successorBirths.TryGetValue(life.Id, out var completedTick)
                    ? completedTick
                    : null,
                snapshot.CurrentJourney?.Id,
                snapshot.IsCompleteLineage,
                tracesByJourney))
            .ToArray();
        ReincarnationLineageHeaderText.Text = snapshot.IsCompleteLineage
            ? $"전체 생 계보 · {rows.Length:N0}생"
            : $"추적된 생 계보 · 제 {snapshot.FirstTrackedLifeNumber:N0}생 이후 {rows.Length:N0}건";
        ReincarnationLineageList.ItemsSource = rows;
        ReincarnationEmptyText.Visibility = rows.Length == 0
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
        _projectedReincarnationAgent = agent.Id;
        _projectedReincarnationRevision = snapshot.AuthorityRevision;
    }

    private static ReincarnationLineageRow CreateReincarnationLineageRow(
        LifeLineageSnapshot life,
        SimulationTick? completedTick,
        ReincarnationJourneyId? activeJourneyId,
        bool isCompleteLineage,
        Dictionary<ReincarnationJourneyId, ReincarnationTraceSnapshot[]> tracesByJourney)
    {
        var end = life.DeathTick is null
            ? "생존 중"
            : FormatLifeEndKind(life.EndKind);
        var predecessor = life.PredecessorId is { } predecessorId
            ? predecessorId.ToString()
            : isCompleteLineage
                ? "없음"
                : "추적 범위 밖";
        var completion = completedTick is { } completed
            ? FormatReincarnationDate(completed)
            : life.JourneyId is { } journeyId && activeJourneyId == journeyId
                ? "미완료"
                : life.JourneyId is not null && !isCompleteLineage
                    ? "추적 범위 밖"
                    : "해당 없음";
        var continuity = life.JourneyId is { } traceJourneyId
            && tracesByJourney.TryGetValue(traceJourneyId, out var traces)
                ? string.Join(
                    Environment.NewLine,
                    traces.Select(trace => FormatReincarnationTrace(trace, compact: true)))
                : "이 생에서 생성된 윤회 흔적 없음";
        return new ReincarnationLineageRow(
            $"제 {life.LifeNumber:N0}생 · {life.PersonName} · {end}",
            $"{life.Id} · 이전 생 {predecessor} · 영혼 {life.SoulId} · 육신 {life.BodyId} · 인물 {life.PersonId} · 가계 {life.FamilyId}",
            $"탄생 {FormatReincarnationDate(life.BirthTick)} · " +
            $"사망 {FormatOptionalReincarnationDate(life.DeathTick, "—")} · 윤회 완료 {completion} · " +
            $"출생 {FormatBirthCandidateStatus(life.BirthStatus)} · 부모 {FormatParents(life.ParentOnePersonId, life.ParentTwoPersonId)}",
            $"개인 최고 · {FormatHistoricalPeak(life.PersonalPeak)}",
            continuity);
    }

    private static string FormatHistoricalPeak(HistoricalPeakSnapshot peak) =>
        $"{FormatRealm(peak.Realm, peak.RealmLayer)} · " +
        $"전투력 {KoreanNumberFormatter.Format(peak.CombatPower)} · " +
        $"제 {peak.LifeNumber:N0}생 {peak.PersonId} · " +
        $"{FormatReincarnationDate(peak.AchievedTick)}";

    private static string FormatReincarnationTrace(
        ReincarnationTraceSnapshot trace,
        bool compact)
    {
        var domain = FormatReincarnationTraceDomain(trace.Domain);
        if (!trace.HasEvidence)
        {
            return $"{domain} · 권위 근거 없음 · {trace.EvidenceStatus}";
        }

        return compact
            ? $"{domain} · 보존 {trace.PreservedHundredths / 100m:0.00} " +
                $"/ 접근 {trace.AccessibleHundredths / 100m:0.00} " +
                $"/ 통합 {trace.IntegratedHundredths / 100m:0.00}"
            : $"{domain} · Preserved {trace.PreservedHundredths / 100m:0.00} " +
                $"({FormatTracePreservationLevel(trace.PreservationLevel)}) · " +
                $"Accessible {trace.AccessibleHundredths / 100m:0.00} " +
                $"({FormatTraceAccessLevel(trace.AccessLevel)}) · " +
                $"Integrated {trace.IntegratedHundredths / 100m:0.00} · 근거 {trace.EvidenceStatus}";
    }

    private static string FormatReincarnationDate(SimulationTick tick) =>
        FormatGameDate(GameCalendar.GetDate(tick));

    private static string FormatOptionalReincarnationDate(
        SimulationTick? tick,
        string emptyText) => tick is { } value
            ? FormatReincarnationDate(value)
            : emptyText;

    private static string FormatReincarnationStage(ReincarnationStage stage) => stage switch
    {
        ReincarnationStage.None => "여정 없음",
        ReincarnationStage.Entering => "윤회의 강 진입",
        ReincarnationStage.Crossing => "도하 중",
        ReincarnationStage.LateCrossing => "도하 후반",
        ReincarnationStage.CrossingComplete => "도하 완료",
        ReincarnationStage.PreparingRebirth => "환생 준비",
        _ => stage.ToString(),
    };

    private static string FormatSoulDamage(SoulDamageLevel level) => level switch
    {
        SoulDamageLevel.None => "없음",
        SoulDamageLevel.Minor => "경미",
        SoulDamageLevel.Severe => "심각",
        SoulDamageLevel.Critical => "치명",
        _ => level.ToString(),
    };

    private static string FormatBirthCandidateStatus(BirthCandidateStatus status) => status switch
    {
        BirthCandidateStatus.Searching => "탐색 중",
        BirthCandidateStatus.Located => "부모 후보 확정",
        BirthCandidateStatus.UnavailableLongWait => "후보 없음·장기 대기",
        BirthCandidateStatus.IndependentManifestation => "천지 독립 현현",
        BirthCandidateStatus.Completed => "출생 완료",
        _ => status.ToString(),
    };

    private static string FormatParents(
        PersonIdentityId? parentOne,
        PersonIdentityId? parentTwo) => parentOne is null
            ? "없음"
            : parentTwo is null
                ? parentOne.Value.ToString()
                : $"{parentOne.Value} · {parentTwo.Value}";

    private static string FormatCausalRecord(SimulationEventRecordId recordId) =>
        recordId.Value == 0 ? "DataUnavailable" : recordId.ToString();

    private static string FormatReincarnationTraceDomain(ReincarnationTraceDomain domain) => domain switch
    {
        ReincarnationTraceDomain.AutobiographicalMemory => "자전 기억",
        ReincarnationTraceDomain.RelationshipAndSocialMemory => "관계·사회 기억",
        ReincarnationTraceDomain.CultivationPrinciplesAndRealmKnowledge => "수행 원리·경지 지식",
        ReincarnationTraceDomain.TechniqueSpellPillAndCraftingKnowledge => "공법·법술·단약·제작 지식",
        ReincarnationTraceDomain.WorldFactionAndEnvironmentKnowledge => "세계·세력·환경 지식",
        ReincarnationTraceDomain.EmbodiedAndActionTrace => "신체·행동 흔적",
        _ => domain.ToString(),
    };

    private static string FormatLifeEndKind(LifeEndKind kind) => kind switch
    {
        LifeEndKind.None => "종료 원인 미상",
        LifeEndKind.Lifespan => "수명 종료",
        LifeEndKind.Combat => "전투 사망",
        _ => kind.ToString(),
    };

    private static string FormatTracePreservationLevel(TracePreservationLevel level) => level switch
    {
        TracePreservationLevel.Lost => "상실",
        TracePreservationLevel.Fragment => "파편",
        TracePreservationLevel.Partial => "부분 보존",
        TracePreservationLevel.Intact => "온전",
        _ => level.ToString(),
    };

    private static string FormatTraceAccessLevel(TraceAccessLevel level) => level switch
    {
        TraceAccessLevel.Sealed => "봉인",
        TraceAccessLevel.Latent => "잠재",
        TraceAccessLevel.Partial => "부분 접근",
        TraceAccessLevel.Immediate => "즉시 접근",
        _ => level.ToString(),
    };

    private static bool MatchesPersonalLogCategory(PersonalLogEntry entry, int category) => category switch
    {
        0 => true,
        1 => entry.Activity is AgentActivity.Cultivating
            or AgentActivity.TrainingTechnique
            or AgentActivity.TrainingSpell
            or AgentActivity.Teaching
            or AgentActivity.Recovering
            || entry.WorldEventKind is WorldEventKind.TechniqueEnlightenment
                or WorldEventKind.SpellEnlightenment
                or WorldEventKind.BreakthroughSucceeded
                or WorldEventKind.BreakthroughFailed,
        2 => entry.Activity is AgentActivity.Fighting
            or AgentActivity.SeekingChallenge
            or AgentActivity.HuntingMonster
            || entry.WorldEventKind is WorldEventKind.CombatStarted
                or WorldEventKind.CombatPhysicalAttack
                or WorldEventKind.CombatSpellCast
                or WorldEventKind.CombatGuarded
                or WorldEventKind.CombatRetreated
                or WorldEventKind.CombatEnded
                or WorldEventKind.CombatInjury
                or WorldEventKind.CombatDeath
                or WorldEventKind.MonsterHuntWon
                or WorldEventKind.MonsterHuntLost,
        3 => entry.Activity is AgentActivity.DoingCityMission
            or AgentActivity.TradingInCity
            or AgentActivity.CraftingTreasure
            or AgentActivity.MiningSpiritStones
            || entry.WorldEventKind is WorldEventKind.MissionCompleted
                or WorldEventKind.MaterialSold
                or WorldEventKind.MaterialPurchased
                or WorldEventKind.PillPurchased
                or WorldEventKind.TreasurePurchased
                or WorldEventKind.TechniqueBookPurchased
                or WorldEventKind.SpellBookPurchased
                or WorldEventKind.SpiritStoneMined,
        4 => entry.Activity == AgentActivity.Traveling
            || entry.WorldEventKind is WorldEventKind.TravelCompleted
                or WorldEventKind.SecretRealmDiscovered,
        5 => entry.Activity == AgentActivity.Reincarnating
            || entry.WorldEventKind is WorldEventKind.Death
                or WorldEventKind.CombatDeath
                or WorldEventKind.ReincarnationEntered
                or WorldEventKind.ReincarnationCrossingCompleted
                or WorldEventKind.Rebirth,
        _ => true,
    };

    private static bool MatchesChronicleCategory(ChronicleEventKind kind, int category) => category switch
    {
        0 => true,
        1 => kind is ChronicleEventKind.MinorStageBreakthrough
            or ChronicleEventKind.FloorNineCompletion
            or ChronicleEventKind.BottleneckBreakthrough
            or ChronicleEventKind.MajorRealmBreakthrough
            or ChronicleEventKind.BreakthroughFailure
            or ChronicleEventKind.CultivationEntered
            or ChronicleEventKind.GreatPerfectionAchieved,
        2 => kind == ChronicleEventKind.CombatDeath,
        3 => kind is ChronicleEventKind.Birth
            or ChronicleEventKind.Death
            or ChronicleEventKind.CombatDeath
            or ChronicleEventKind.ReincarnationEntered
            or ChronicleEventKind.ReincarnationCrossingCompleted
            or ChronicleEventKind.Rebirth,
        4 => kind is ChronicleEventKind.FortuitousEncounter
            or ChronicleEventKind.TechniqueAcquired
            or ChronicleEventKind.SpellAcquired,
        _ => true,
    };

    private static string FormatEffectPercent(int basisPoints) => $"{basisPoints / 100m:0.##}%";

}
