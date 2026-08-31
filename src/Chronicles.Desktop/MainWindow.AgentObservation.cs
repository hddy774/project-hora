using System.Windows.Controls;
using Chronicles.Core;
using Chronicles.Simulation;
using static Chronicles.Presentation.PresentationText;

namespace Chronicles.Desktop;

public partial class MainWindow
{
    private void UpdateSelectedAgent(
        AgentSnapshot agent,
        IReadOnlyList<PersonalLogEntry> personalLog,
        IReadOnlyList<LifeChronicleEntry> chronicle,
        ReincarnationAgentSnapshot? reincarnation)
    {
        var selectedAgentChanged = _latestAgent?.Id != agent.Id;
        var hadReincarnation = _latestReincarnation is not null;
        _latestAgent = agent;
        _latestPersonalLog = personalLog;
        _latestChronicle = chronicle;
        _latestReincarnation = reincarnation;
        if (selectedAgentChanged || (reincarnation is null && hadReincarnation))
        {
            ClearReincarnationView();
        }
        SelectedNameText.Text = agent.Name;
        SelectedIdText.Text = $"{agent.Id} · 최종 목표 #{agent.TargetRank}";
        IdentityText.Text =
            $"영혼 {agent.Identity.SoulId} · 육신 {agent.Identity.BodyId} · 현생 {agent.Identity.PersonId} · {FormatLifeState(agent.Identity.LifeState)}";
        SelectedRankText.Text =
            $"전투력 #{agent.Rank:N0} · 경지 #{agent.RealmRank:N0} · " +
            $"자질 #{agent.AptitudeRank:N0} · 부자 #{agent.WealthRank:N0}";
        RealmText.Text = HasEnteredCultivation(agent.CultivationState, agent.BottleneckCause)
            ? FormatRealm(
                agent.RealmStage,
                agent.RealmLayer,
                agent.Cultivation.GreatPerfectionAchieved)
            : "미입문 · 입문 수련";
        AgeText.Text = $"{KoreanNumberFormatter.Format(agent.AgeYears)} / {KoreanNumberFormatter.Format(agent.MaximumLifespanYears)}세";
        LifeText.Text = $"제 {agent.LifeNumber:N0}생";
        ActivityText.Text = FormatActivity(agent.Activity);
        OpponentText.Text = agent.OpponentId is { } opponent
            ? GetAgentName(opponent)
            : "없음";
        var nextActionText = agent.NextDecisionTick.Value == long.MaxValue
            ? agent.OpponentId is not null
                ? "상대 턴 대기"
                : "예약 없음"
            : FormatGameDate(GameCalendar.GetDate(agent.NextDecisionTick));
        ActionScheduleText.Text =
            $"누적 행동 {KoreanNumberFormatter.Format(agent.ActionsTaken)}회 · " +
            $"마지막 {FormatOptionalGameDate(agent.LastActionDate)} · 다음 {nextActionText}";
        var location = WorldMapRules.GetLocation(agent.LocationId);
        LocationText.Text =
            $"현재 위치 {location.KoreanName} · {FormatBiome(location.Biome)} · 요수 사냥 {KoreanNumberFormatter.Format(agent.MonsterVictories)}/{KoreanNumberFormatter.Format(agent.MonsterHunts)}승";
        if (DecisionReasonExpander.IsExpanded)
        {
            UpdateDecisionReasonView(agent.Decision);
        }
        if (RelationsExpander.IsExpanded)
        {
            UpdateRelationsView(agent.Relations ?? Array.Empty<RelationSnapshot>());
        }
        if (TeachingExpander.IsExpanded && agent.Teaching is { } teaching)
        {
            UpdateTeachingView(teaching);
        }
        CultivationProgressBar.Maximum = Math.Max(1, agent.CultivationRequirement);
        CultivationProgressBar.Value = Math.Min(agent.CultivationProgress, agent.CultivationRequirement);
        CultivationText.Text = $"{KoreanNumberFormatter.Format(agent.CultivationProgress)} / {KoreanNumberFormatter.Format(agent.CultivationRequirement)}";
        CultivationStateText.Text = FormatCultivationState(agent.CultivationState, agent.BottleneckCause);
        var cultivation = agent.Cultivation;
        CultivationAuthorityText.Text =
            $"선천 수행자질 종합 {cultivation.InnateAptitudeCompositeHundredths / 100m:0.00} · 50.00 기준 대경지 전체(1층→9층 대원만) {cultivation.BaselineCultivationYears:N2}년 · 직전 Work {KoreanNumberFormatter.Format(cultivation.WorkBefore)} + {KoreanNumberFormatter.Format(cultivation.LastGain)} = {KoreanNumberFormatter.Format(cultivation.WorkAfter)} · 이월 {KoreanNumberFormatter.Format(cultivation.PendingCarryoverWork)}";
        var gateText = cultivation.EntryGates.Count == 0
            ? "입문 Gate 해당 없음"
            : string.Join(
                " · ",
                cultivation.EntryGates.Select(gate =>
                    $"G{gate.GateNumber} {(gate.Passed ? "통과" : "대기")} {gate.GateScoreHundredths / 100m:0.00} (단련 +{gate.TemperingBonusHundredths / 100m:0.00})"));
        var componentText = cultivation.Components.Count == 0
            ? "경지 구성요소 준비 전"
            : string.Join(
                " · ",
                cultivation.Components.Select(component =>
                    $"{component.KoreanName} {component.ValueHundredths / 100m:0.00}/{component.LimitHundredths / 100m:0.00}"));
        CultivationComponentText.Text =
            $"{gateText} · 축기 경로 {FormatFoundationRoute(cultivation.FoundationRoute)} · {componentText}";
        if (CultivationTraceExpander.IsExpanded)
        {
            UpdateCultivationTraceView(cultivation);
        }
        RootText.Text = QualityRules.Format(agent.Aptitudes.SpiritualRoot);
        BoneText.Text = QualityRules.Format(agent.Aptitudes.Bone);
        ConstitutionText.Text = QualityRules.Format(agent.Aptitudes.Constitution);
        ComprehensionText.Text = QualityRules.Format(agent.Aptitudes.Comprehension);
        MindText.Text = QualityRules.Format(agent.Aptitudes.Mind);
        SoulText.Text = QualityRules.Format(agent.Aptitudes.Soul);
        if (AptitudeDetailsExpander.IsExpanded)
        {
            UpdateAptitudeDetailView(agent);
        }

        TechniqueText.Text = $"{QualityRules.Format(agent.TechniqueQuality)} · 숙련 {agent.TechniqueMastery / 100m:0.00} · 이해 {agent.TechniqueUnderstanding / 100m:0.00} · 경험 {KoreanNumberFormatter.Format(agent.TechniqueTrainingExperience)}";
        SpellText.Text = $"{QualityRules.Format(agent.SpellQuality)} · 숙련 {agent.SpellMastery / 100m:0.00} · 이해 {agent.SpellUnderstanding / 100m:0.00} · 경험 {KoreanNumberFormatter.Format(agent.SpellTrainingExperience)}";
        HealthText.Text = $"{KoreanNumberFormatter.Format(Math.Max(0, agent.Health))} / {KoreanNumberFormatter.Format(agent.MaximumHealth)}";
        QiText.Text = $"{KoreanNumberFormatter.Format(agent.Qi)} / {KoreanNumberFormatter.Format(agent.MaximumQi)}";
        var combat = agent.CombatAttributes;
        CombatAttributeText.Text =
            $"공격 {KoreanNumberFormatter.Format(combat.Attack)} · 방어 {KoreanNumberFormatter.Format(combat.Defense)} · 공격속도 {combat.AttackSpeedBasisPoints / 100m:0.00}% · 방어속도 {combat.DefenseSpeedBasisPoints / 100m:0.00}%\n" +
            $"관통 {KoreanNumberFormatter.Format(combat.Penetration)} · 관통방어 {KoreanNumberFormatter.Format(combat.PenetrationDefense)} · 치명타 확률 {combat.CriticalChanceBasisPoints / 100m:0.00}% · 치명타 위력 {combat.CriticalPowerBasisPoints / 100m:0.00}% · 치명타 방어 {combat.CriticalDefenseBasisPoints / 100m:0.00}% · 회피 {combat.EvasionBasisPoints / 100m:0.00}%\n" +
            $"판단력 {KoreanNumberFormatter.Format(combat.Judgment)} · 집중력 {KoreanNumberFormatter.Format(combat.Focus)} · 법술 효율 {combat.SpellEfficiencyBasisPoints / 100m:0.00}% · 수행 효율 {combat.CultivationEfficiencyBasisPoints / 100m:0.00}%";
        InjuryText.Text =
            $"부상 {FormatInjurySeverity(agent.InjurySeverity)} · 누적 {agent.InjuryPoints / 100m:0.00}% · 중상부터 전투 사망 확률 발생";
        var battles = agent.Wins + agent.Losses;
        var winRate = battles == 0 ? 0m : agent.Wins * 100m / battles;
        RecordText.Text =
            $"{KoreanNumberFormatter.Format(agent.Wins)}승 {KoreanNumberFormatter.Format(agent.Losses)}패 · 승률 {winRate:0.##}% · 전투 숙련 {agent.CombatMastery / 100m:0.00} · 이해 {agent.CombatUnderstanding / 100m:0.00} · 경험 {KoreanNumberFormatter.Format(agent.CombatExperience)}";
        SpiritStoneText.Text = $"{KoreanNumberFormatter.Format(agent.SpiritStones)}개";
        PillInventoryText.Text = $"수행단 {agent.GrowthPills:N0} · 돌파단 {agent.BreakthroughPills:N0}";
        MissionText.Text = agent.ActiveMission is { } mission
            ? $"{EconomyRules.GetMaterial(mission.Material).KoreanName} {mission.HeldCount:N0}/{mission.RequiredCount:N0} · 완료 {agent.MissionsCompleted:N0}"
            : $"수주 대기 · 완료 {agent.MissionsCompleted:N0}";
        MaterialText.Text = $"{KoreanNumberFormatter.Format(agent.InventoryItemCount)}개";
        EquipmentText.Text = string.Join(
            "  ·  ",
            agent.EquippedTreasures.Select(item =>
                $"{FormatTreasureSlot(item.Slot)} {(item.Quality is { } quality ? QualityRules.Format(quality) : "미장착")}" +
                (item.RealmGrade is { } grade ? $" · {FormatRealmStage(grade)}급" : string.Empty)));
        MaterialDetailText.Text = agent.Materials.Count == 0
            ? "보유 재료 없음"
            : string.Join(
                "  ·  ",
                agent.Materials.Select(item =>
                    $"{EconomyRules.GetMaterial(item.Material).KoreanName} {KoreanNumberFormatter.Format(item.Count)}"));
        AptitudeBonusText.Text =
            $"세부 자질 파생 보정 · 공법 {FormatEffectPercent(agent.CombatPowerDetails.TechniqueAptitudeBonusBasisPoints)} · " +
            $"법술 {FormatEffectPercent(agent.CombatPowerDetails.SpellAptitudeBonusBasisPoints)} · " +
            $"전투 숙련 {FormatEffectPercent(agent.CombatPowerDetails.CombatMasteryBonusBasisPoints)} · " +
            $"전투 이해 {FormatEffectPercent(agent.CombatPowerDetails.CombatUnderstandingBonusBasisPoints)} · " +
            $"전투 경험 {FormatEffectPercent(agent.CombatPowerDetails.CombatExperienceGainBonusBasisPoints)}";
        SetPowerBreakdown(agent.CombatPower);
        SetPowerFormulaDetails(agent.CombatPowerDetails);
        UpdateAgentLogViews();
    }

    private void SetPowerBreakdown(CombatPowerBreakdown power)
    {
        var maximum = Math.Max(1, power.Total);
        TotalPowerText.Text = KoreanNumberFormatter.Format(power.Total);
        SetPowerRow(RealmPowerBar, RealmPowerText, power.Realm, maximum);
        AptitudePowerBar.Maximum = 1;
        AptitudePowerBar.Value = 0;
        AptitudePowerText.Text = "간접";
        SetPowerRow(TechniquePowerBar, TechniquePowerText, power.Technique, maximum);
        SetPowerRow(SpellPowerBar, SpellPowerText, power.Spell, maximum);
        SetPowerRow(ExperiencePowerBar, ExperiencePowerText, power.Experience, maximum);
        SetPowerRow(TreasurePowerBar, TreasurePowerText, power.Treasure, maximum);
    }

    private static void SetPowerRow(ProgressBar bar, TextBlock label, long value, long maximum)
    {
        bar.Maximum = maximum;
        bar.Value = value;
        label.Text = KoreanNumberFormatter.Format(value);
    }

    private void SetPowerFormulaDetails(CombatPowerFormulaDetails detail)
    {
        PowerFormulaText.Text =
            $"공식 r{detail.FormulaRevision} · 총합 = 경지 + 공법 + 법술 + 전투 성장 + 법보 (자질 직접 합산 0)\n" +
            $"경지: 기본 100 + 대경지({detail.RealmStageFactor})³ 보정 + {detail.RealmLayer}층 보정 = {KoreanNumberFormatter.Format(detail.RealmContribution)}\n" +
            $"공법: 품질·숙련 {detail.TechniqueMastery / 100m:0.00}·이해 {detail.TechniqueUnderstanding / 100m:0.00} → {KoreanNumberFormatter.Format(detail.TechniqueBeforeAptitudeBonus)} × 자질 {detail.TechniqueAptitudeBonusBasisPoints / 100m:0.##}% = {KoreanNumberFormatter.Format(detail.TechniqueContribution)}\n" +
            $"법술: 품질·숙련 {detail.SpellMastery / 100m:0.00}·이해 {detail.SpellUnderstanding / 100m:0.00} → {KoreanNumberFormatter.Format(detail.SpellBeforeAptitudeBonus)} × 자질 {detail.SpellAptitudeBonusBasisPoints / 100m:0.##}% = {KoreanNumberFormatter.Format(detail.SpellContribution)}\n" +
            $"전투 성장: 경험 {KoreanNumberFormatter.Format(detail.CombatExperience)} · 숙련 {detail.CombatMastery / 100m:0.00}({detail.CombatMasteryBonusBasisPoints / 100m:0.##}%) · 이해 {detail.CombatUnderstanding / 100m:0.00}({detail.CombatUnderstandingBonusBasisPoints / 100m:0.##}%) = {KoreanNumberFormatter.Format(detail.CombatExperienceContribution)}\n" +
            $"법보: 본명·방어·공격 법보 품질 효과 합계 = {KoreanNumberFormatter.Format(detail.TreasureContribution)}\n" +
            $"표시 전투력 {KoreanNumberFormatter.Format(detail.Breakdown.Total)} · 실전 전투는 공격·방어·속도·관통·치명타·회피와 턴 진행을 별도 계산";
    }

    private void UpdateAgentLogViews()
    {
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
                UpdateReincarnationView(agent, _latestReincarnation);
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
                $"원천 {FormatRealmStage(journey.SourceRealm)} · 총 {journey.RequiredYears:N0}년" +
                (journey.RequiresReview
                    ? $" · {ReincarnationRules.StageReviewId} 검토값"
                    : string.Empty);
            ReincarnationTimingText.Text =
                $"시작 {FormatReincarnationDate(journey.StartedTick)} · " +
                $"단계 시작 {FormatReincarnationDate(journey.StageStartedTick)} · " +
                $"다음 단계 {FormatReincarnationDate(journey.NextTransitionTick)} · " +
                $"예정 완료 {FormatReincarnationDate(journey.RebirthReadyTick)} · " +
                $"완료일 {FormatOptionalReincarnationDate(journey.CompletedTick, "미완료")}";
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
            $"{life.Id} · 이전 생 {predecessor} · 영혼 {life.SoulId} · 육신 {life.BodyId} · 인물 {life.PersonId}",
            $"탄생 {FormatReincarnationDate(life.BirthTick)} · " +
            $"사망 {FormatOptionalReincarnationDate(life.DeathTick, "—")} · 윤회 완료 {completion}",
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
            return $"{domain} · 권위 근거 없음";
        }

        return compact
            ? $"{domain} · 보존 {trace.PreservedHundredths / 100m:0.00} " +
                $"/ 접근 {trace.AccessibleHundredths / 100m:0.00} " +
                $"/ 통합 {trace.IntegratedHundredths / 100m:0.00}"
            : $"{domain} · Preserved {trace.PreservedHundredths / 100m:0.00} " +
                $"({FormatTracePreservationLevel(trace.PreservationLevel)}) · " +
                $"Accessible {trace.AccessibleHundredths / 100m:0.00} " +
                $"({FormatTraceAccessLevel(trace.AccessLevel)}) · " +
                $"Integrated {trace.IntegratedHundredths / 100m:0.00}";
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
            || entry.WorldEventKind == WorldEventKind.TravelCompleted,
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

    private void UpdateDecisionReasonView(AgentDecisionSnapshot decision)
    {
        var latest = decision.Latest;
        var alternative = latest.AlternativeActivity is { } alternativeActivity
            ? $" · 차선 후보 {FormatActivity(alternativeActivity)}"
            : string.Empty;
        DecisionReasonText.Text =
            $"{decision.ContractId} r{decision.ContractRevision} · 권위 revision {latest.AuthorityRevision:N0}" +
            (decision.RequiresReview ? $" · {PersonCausalRules.ProvisionalDecisionReviewId} 다음 버전 검토" : string.Empty) +
            $"\n최종 목표 {FormatGoal(latest.PrimaryGoal)} → 현재 목표 {FormatGoal(latest.SupportingGoal)}" +
            $"\n선택 {FormatActivity(latest.SelectedActivity)} · 이유 {FormatDecisionReason(latest.SelectedReason)} · {FormatDecisionResolution(latest.Resolution)}{alternative}" +
            $"\n판정 시점 {FormatGameDate(GameCalendar.GetDate(new SimulationTick(latest.Tick)))} · 정확한 Utility·전환 비용 수치는 아직 확정하지 않음";
    }

    private static string FormatLifeState(PersonLifeState state) => state switch
    {
        PersonLifeState.Living => "현생 생존",
        PersonLifeState.ReincarnationRiver => "윤회의 강 체류",
        _ => state.ToString(),
    };

    private static string FormatGoal(AgentGoalKind goal) => goal switch
    {
        AgentGoalKind.ReachRankOne => "상위권·1위 도달",
        AgentGoalKind.EnterCultivation => "수행 입문",
        AgentGoalKind.AdvanceRealm => "경지 성장",
        AgentGoalKind.CompleteBreakthrough => "돌파 절차 완수",
        AgentGoalKind.ResolveBottleneck => "병목 해소",
        AgentGoalKind.RecoverBodyAndQi => "육신·영력 회복",
        AgentGoalKind.ImproveTechnique => "공법 성장",
        AgentGoalKind.ImproveSpell => "법술 성장",
        AgentGoalKind.AcquireResources => "자원 확보",
        AgentGoalKind.SeekStrongerOpponent => "강자 도전",
        AgentGoalKind.ExploreWorld => "세계 탐색",
        AgentGoalKind.CompleteReincarnation => "윤회 완료",
        AgentGoalKind.GuideDisciple => "제자 지도·전승",
        _ => goal.ToString(),
    };

    private static string FormatDecisionReason(DecisionReasonCode reason) => reason switch
    {
        DecisionReasonCode.InitialLifeCultivation => "새 생의 수행 기반 마련",
        DecisionReasonCode.ForcedRecovery => "생명력·영력·부상 회복 우선",
        DecisionReasonCode.BreakthroughPipeline => "진행 중인 돌파 절차 유지",
        DecisionReasonCode.BottleneckRemediation => "현재 병목 원인 보완",
        DecisionReasonCode.CultivationEntry => "입문 Gate 준비",
        DecisionReasonCode.CultivationPriority => "경지 성장 우선순위",
        DecisionReasonCode.ChallengeOpportunity => "상위권 경쟁 기회",
        DecisionReasonCode.MapOpportunity => "지역 탐색·사냥 기회",
        DecisionReasonCode.GrowthBalance => "공법·법술·경제·수행 균형 성장",
        DecisionReasonCode.CombatChallenge => "선택한 상대에게 도전",
        DecisionReasonCode.CombatResponse => "상대의 도전에 대응",
        DecisionReasonCode.CombatRecovery => "전투 종료 후 회복",
        DecisionReasonCode.ReincarnationHandoff => "죽음 이후 윤회 절차 진입",
        DecisionReasonCode.TeachingOpportunity => "관계·지식 격차에 따른 가르침 기회",
        DecisionReasonCode.TeachingContinuation => "진행 중인 가르침·전승 유지",
        DecisionReasonCode.TeachingInterrupted => "전투·생애 경계로 가르침 중단",
        _ => reason.ToString(),
    };

    private static string FormatDecisionResolution(DecisionResolutionKind resolution) => resolution switch
    {
        DecisionResolutionKind.Selected => "자율 선택",
        DecisionResolutionKind.Forced => "필수 전이",
        DecisionResolutionKind.Deferred => "보류",
        DecisionResolutionKind.Blocked => "차단",
        _ => resolution.ToString(),
    };

    private void UpdateRelationsView(IReadOnlyList<RelationSnapshot> relations)
    {
        if (relations.Count == 0)
        {
            RelationsText.Text =
                $"현재 생의 희소 관계 없음 · 인물당 최대 {RelationRules.MaximumRelationsPerAgent:N0}개 · " +
                $"{RelationRules.ProvisionalReviewId} 다음 버전 검토";
            return;
        }

        RelationsText.Text = string.Join(
            "\n\n",
            relations.Select(relation =>
            {
                var role = FormatRelationRoles(relation);
                var latestHistory = relation.ImportantHistory.Count == 0
                    ? default
                    : relation.ImportantHistory[^1];
                var history = latestHistory.Sequence == 0
                    ? "중요 역사 없음"
                    : $"최근 역사 {FormatRelationHistory(latestHistory.Kind)} · " +
                        $"{FormatGameDate(GameCalendar.GetDate(new SimulationTick(latestHistory.Tick)))}" +
                        (latestHistory.TeachingTarget is { } target
                            ? $" · {FormatTeachingTarget(target)} · {latestHistory.KnowledgeRef} · {latestHistory.ProvenanceId}"
                            : string.Empty);
                return
                    $"{relation.OtherName} · {relation.OtherAgentId} · {relation.OtherPersonId} · {relation.Id}\n" +
                    $"역할 {role}\n" +
                    $"나→상대 {FormatRelationMetrics(relation.TowardOther)}\n" +
                    $"상대→나 {FormatRelationMetrics(relation.OtherTowardSelf)}\n" +
                    $"{history}";
            })) +
            $"\n\n희소 후보·축 변화·Role 임시 수치: {RelationRules.ProvisionalReviewId} 다음 버전 재검토";
    }

    private static string FormatRelationRoles(RelationSnapshot relation)
    {
        var roles = new List<string>(5);
        if ((relation.Roles & RelationRole.MasterDisciple) != 0)
        {
            roles.Add(relation.IsMaster ? "스승" : relation.IsDisciple ? "제자" : "사제");
        }
        if ((relation.Roles & RelationRole.Friendship) != 0)
        {
            roles.Add("우정");
        }
        if ((relation.Roles & RelationRole.Romance) != 0)
        {
            roles.Add("연애");
        }
        if ((relation.Roles & RelationRole.Companion) != 0)
        {
            roles.Add("실제 동료");
        }
        if ((relation.Roles & RelationRole.PersistentRivalry) != 0)
        {
            roles.Add("지속 경쟁");
        }

        return roles.Count == 0 ? "아직 성립 전" : string.Join("·", roles);
    }

    private static string FormatRelationMetrics(RelationMetrics metrics) =>
        $"호감/반감 {metrics.FavorDisfavor / 100m:+0.00;-0.00;0.00} · " +
        $"신뢰/불신 {metrics.TrustDistrust / 100m:+0.00;-0.00;0.00} · " +
        $"존중/경시 {metrics.RespectContempt / 100m:+0.00;-0.00;0.00} · " +
        $"은혜 {metrics.Gratitude / 100m:0.00} · 원한 {metrics.Grievance / 100m:0.00} · " +
        $"경계 {metrics.Vigilance / 100m:0.00}";

    private static string FormatRelationHistory(RelationHistoryKind kind) => kind switch
    {
        RelationHistoryKind.RelationCreated => "관계 인지 시작",
        RelationHistoryKind.RoleFormed => "관계 역할 성립",
        RelationHistoryKind.RoleEndedAtLifeBoundary => "생애 경계로 관계 역할 종료",
        RelationHistoryKind.TeachingTransmissionEstablished => "중요 전승 성립",
        RelationHistoryKind.TeachingTransmissionUpdated => "중요 전승 갱신",
        _ => kind.ToString(),
    };

    private void UpdateTeachingView(TeachingSnapshot teaching)
    {
        if (teaching.Latest.AuthorityRevision == 0
            && teaching.LatestLearning.AuthorityRevision == 0)
        {
            TeachingText.Text =
                $"{teaching.ContractId} r{teaching.ContractRevision} · 아직 사제 Teaching 기록 없음\n" +
                $"일반 Teaching 직접 수행 속도 {teaching.DirectCultivationSpeedBonusBasisPoints / 100m:0.##}% · " +
                $"{TeachingRules.ProvisionalReviewId} 다음 버전 검토";
            return;
        }

        var traces = new List<string>(2);
        if (teaching.Latest.AuthorityRevision > 0)
        {
            traces.Add(FormatTeachingTrace(
                "가르친 기록",
                teaching.Latest,
                teaching.LatestParticipants));
        }
        if (teaching.LatestLearning.AuthorityRevision > 0)
        {
            traces.Add(FormatTeachingTrace(
                "배운 기록",
                teaching.LatestLearning,
                teaching.LatestLearningParticipants));
        }

        TeachingText.Text =
            $"{teaching.ContractId} r{teaching.ContractRevision}" +
            (teaching.RequiresReview ? $" · {TeachingRules.ProvisionalReviewId} 다음 버전 검토" : string.Empty) +
            $"\n오해 누적(공법/법술) {teaching.TechniqueMisunderstandingPoints / 100m:0.00}/{teaching.SpellMisunderstandingPoints / 100m:0.00}" +
            $"\n일반 Teaching 직접 수행 속도 {teaching.DirectCultivationSpeedBonusBasisPoints / 100m:0.##}% — 숙련 경험·이해·오해 교정·전승 접근만 변경" +
            $"\n\n{string.Join("\n\n", traces)}";
    }

    private static string FormatTeachingTrace(
        string label,
        TeachingTraceState trace,
        TeachingParticipantSnapshot? participants)
    {
        var teacherText = participants is null
            ? trace.TeacherPersonId.ToString()
            : $"{participants.TeacherName} · {participants.TeacherId} · {trace.TeacherPersonId}";
        var learnerText = participants is null
            ? $"{new AgentId(trace.LearnerAgent)} · {trace.LearnerPersonId}"
            : $"{participants.LearnerName} · {participants.LearnerId} · {trace.LearnerPersonId}";
        var resultTime = trace.IsActive || trace.LastResult == TeachingResultKind.None
            ? "판정 대기"
            : FormatGameDate(GameCalendar.GetDate(new SimulationTick(trace.LastResultTick)));
        var effect = trace.LastResult switch
        {
            TeachingResultKind.LearningProgress
                or TeachingResultKind.TransmissionEstablished
                or TeachingResultKind.TransmissionUpdated =>
                $"이해 +{trace.LastGain / 100m:0.00}",
            TeachingResultKind.Correction => $"오해 -{trace.LastGain / 100m:0.00}",
            TeachingResultKind.Misunderstanding =>
                $"오해 누적 {trace.MisunderstandingPoints / 100m:0.00}",
            _ => "수치 변화 없음",
        };
        return
            $"{label} · 권위 revision {trace.AuthorityRevision:N0}" +
            $"\n교사 {teacherText} → 학습자 {learnerText}" +
            $"\n대상 {FormatTeachingTarget(trace.TargetKind)} · 품질 {QualityRules.Format(QualityValue.FromHundredths(trace.SourceQuality))} · {trace.TargetKnowledgeRef}" +
            $"\nprovenance {trace.ProvenanceId} · 관계 {trace.RelationId}" +
            $"\n시작 {FormatGameDate(GameCalendar.GetDate(new SimulationTick(trace.StartedTick)))} · 상태 {(trace.IsActive ? "진행 중" : "정산 완료")}" +
            $"\n최근 결과 {FormatTeachingResult(trace.LastResult)} · {effect} · 전승 진전 {trace.SessionsCompleted:N0}회 · {resultTime}" +
            $"\n전승 {(trace.TransmissionEstablished ? "성립" : "미성립")}";
    }

    private static string FormatTeachingTarget(TeachingTargetKind target) => target switch
    {
        TeachingTargetKind.Technique => "공법",
        TeachingTargetKind.Spell => "법술",
        _ => target.ToString(),
    };

    private static string FormatTeachingResult(TeachingResultKind result) => result switch
    {
        TeachingResultKind.None => "판정 전",
        TeachingResultKind.LearningProgress => "학습·이해 진전",
        TeachingResultKind.Misunderstanding => "오해 발생",
        TeachingResultKind.Correction => "오해 교정",
        TeachingResultKind.TransmissionEstablished => "전승 성립",
        TeachingResultKind.TransmissionUpdated => "전승 갱신",
        TeachingResultKind.Interrupted => "중단",
        TeachingResultKind.Refused => "거부",
        _ => result.ToString(),
    };

    private void UpdateAptitudeDetailView(AgentSnapshot agent)
    {
        if (agent.AptitudeDetails is not { } details)
        {
            return;
        }

        var selectedIndex = Math.Clamp(
            AptitudeDetailTabControl.SelectedIndex,
            0,
            details.Groups.Count - 1);
        var group = details.Groups[selectedIndex];
        var target = group.Domain switch
        {
            AptitudeDomain.SpiritualRoot => RootDetailText,
            AptitudeDomain.Bone => BoneDetailText,
            AptitudeDomain.Constitution => ConstitutionDetailText,
            AptitudeDomain.Comprehension => ComprehensionDetailText,
            AptitudeDomain.Mind => MindDetailText,
            AptitudeDomain.Soul => SoulDetailText,
            _ => throw new InvalidOperationException("알 수 없는 선천자질 영역입니다."),
        };
        var review = details.RequiresReview
            ? " · 임시 분포·역할 조합/다음 버전 재검토"
            : string.Empty;
        var roles = details.RoleScores;
        target.Text =
            $"{group.KoreanName} 대표 {QualityRules.Format(group.Summary)} · {details.FormulaId} r{details.FormulaRevision}{review}\n" +
            string.Join(
                "\n",
                group.Components.Select((component, index) =>
                    $"{index + 1:N0}. {component.KoreanName} · {QualityRules.Format(component.InnateValue)}")) +
            "\n파생 공식(핵심 30·30 / 보조 20·20)\n" +
            string.Join(
                "\n",
                group.DerivedAbilities.Select(ability =>
                    $"{ability.KoreanName} = {ability.FormulaText} = {ability.Value.Amount:0.00}")) +
            $"\n역할 종합 · 공법 {roles.TechniqueEffectHundredths / 100m:0.00} · " +
            $"법술 {roles.SpellEffectHundredths / 100m:0.00} · " +
            $"전투 숙련 {roles.CombatMasteryHundredths / 100m:0.00} · " +
            $"전투 이해 {roles.CombatUnderstandingHundredths / 100m:0.00} · " +
            $"전투 경험 {roles.CombatExperienceGainHundredths / 100m:0.00}";
    }

    private void UpdateCultivationTraceView(CultivationReadProjection cultivation)
    {
        var trace = cultivation.LatestTrace;
        if (trace.FormulaKind == CultivationFormulaKind.None)
        {
            CultivationTraceText.Text =
                $"권위 revision {cultivation.AuthorityRevision:N0} · 아직 수행 정산 기록 없음";
            return;
        }

        var formula = CultivationFormulaRegistry.Get(trace.FormulaKind);
        var chance = trace.RollBasisPoints < 0
            ? "RNG 미사용"
            : $"기본 {trace.BaseChanceBasisPoints / 100m:0.00}% + 보정 {trace.ModifierBasisPoints / 100m:0.00}% = " +
                $"최종 {trace.FinalChanceBasisPoints / 100m:0.00}% · roll {trace.RollBasisPoints / 100m:0.00}%";
        CultivationTraceText.Text =
            $"{formula.StableId} r{formula.Revision} · {formula.Purpose}" +
            (formula.RequiresReview ? " · 다음 버전 검토값 포함" : string.Empty) +
            $"\n권위 revision {trace.AuthorityRevisionBefore:N0} → {trace.AuthorityRevisionAfter:N0} · " +
            $"{FormatBreakthroughClass(trace.AttemptClass)} · {FormatBreakthroughOutcome(trace.Outcome)}" +
            $"\nWork {KoreanNumberFormatter.Format(trace.WorkBefore)} + {KoreanNumberFormatter.Format(trace.WorkGain)} = " +
            $"{KoreanNumberFormatter.Format(trace.WorkAfter)} / {KoreanNumberFormatter.Format(trace.RequiredWork)} · " +
            $"이월 {KoreanNumberFormatter.Format(trace.CarryoverWork)}" +
            $"\n{chance} · 차단 {FormatCultivationBlocker(trace.Blocker)}";
    }

    private static string FormatBreakthroughClass(BreakthroughClass value) => value switch
    {
        BreakthroughClass.None => "진척 정산",
        BreakthroughClass.Entry => "수행 입문",
        BreakthroughClass.NormalFloor => "일반 소경지",
        BreakthroughClass.EarlyMiddleBottleneck => "3→4층 병목",
        BreakthroughClass.MiddleLateBottleneck => "6→7층 병목",
        BreakthroughClass.FloorNineCompletion => "9층 소경지 완성",
        BreakthroughClass.GreatPerfectionCompletion => "대원만 Gate",
        BreakthroughClass.MajorRealm => "대경지 돌파",
        _ => value.ToString(),
    };

    private static string FormatBreakthroughOutcome(BreakthroughOutcome value) => value switch
    {
        BreakthroughOutcome.None => "판정 전",
        BreakthroughOutcome.Blocked => "차단",
        BreakthroughOutcome.Success => "성공",
        BreakthroughOutcome.Failure => "실패",
        _ => value.ToString(),
    };

    private static string FormatCultivationBlocker(CultivationBottleneckCause value) => value switch
    {
        CultivationBottleneckCause.None => "없음",
        CultivationBottleneckCause.Knowledge => "지식",
        CultivationBottleneckCause.Body => "육신",
        CultivationBottleneckCause.SpiritualRoot => "영근",
        CultivationBottleneckCause.Resources => "자원",
        CultivationBottleneckCause.Stability => "안정성",
        CultivationBottleneckCause.EntryGate => "입문 Gate",
        CultivationBottleneckCause.RealmComponent => "경지 구성요소",
        CultivationBottleneckCause.Method => "공법",
        _ => value.ToString(),
    };

}
