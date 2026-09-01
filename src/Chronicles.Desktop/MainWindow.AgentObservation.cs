using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Globalization;
using Chronicles.Core;
using Chronicles.Simulation;
using static Chronicles.Presentation.PresentationText;

namespace Chronicles.Desktop;

internal enum AgentDetailSection : byte
{
    Overview,
    Cultivation,
    Combat,
    Social,
    Records,
}

public partial class MainWindow
{
    private AgentDetailSection _agentDetailSection = AgentDetailSection.Overview;

    private void AgentDetailSectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag }
            || !Enum.TryParse<AgentDetailSection>(tag, ignoreCase: true, out var section))
        {
            return;
        }

        SelectAgentDetailSection(section);
        RefreshVisibleObservation();
    }

    private void SelectAgentDetailSection(AgentDetailSection section)
    {
        _agentDetailSection = section;
        AgentOverviewSection.Visibility = section == AgentDetailSection.Overview
            ? Visibility.Visible
            : Visibility.Collapsed;
        AgentCultivationSection.Visibility = section == AgentDetailSection.Cultivation
            ? Visibility.Visible
            : Visibility.Collapsed;
        AgentCombatSection.Visibility = section == AgentDetailSection.Combat
            ? Visibility.Visible
            : Visibility.Collapsed;
        AgentSocialSection.Visibility = section == AgentDetailSection.Social
            ? Visibility.Visible
            : Visibility.Collapsed;
        AgentRecordsSection.Visibility = section == AgentDetailSection.Records
            ? Visibility.Visible
            : Visibility.Collapsed;
        AgentDetailScrollViewer.ScrollToTop();
        AgentOverviewTab.IsChecked = section == AgentDetailSection.Overview;
        AgentCultivationTab.IsChecked = section == AgentDetailSection.Cultivation;
        AgentCombatTab.IsChecked = section == AgentDetailSection.Combat;
        AgentSocialTab.IsChecked = section == AgentDetailSection.Social;
        AgentRecordsTab.IsChecked = section == AgentDetailSection.Records;
    }

    private void UpdateSelectedAgent(
        AgentSnapshot agent,
        IReadOnlyList<PersonalLogEntry> personalLog,
        IReadOnlyList<LifeChronicleEntry> chronicle,
        IReadOnlyList<PersonalCenturyChronicleEntry> personalCenturyChronicle,
        ReincarnationAgentSnapshot? reincarnation)
    {
        var selectedAgentChanged = _latestAgent?.Id != agent.Id;
        var hadReincarnation = _latestReincarnation is not null;
        _latestAgent = agent;
        _latestPersonalLog = personalLog;
        _latestChronicle = chronicle;
        _latestPersonalCenturyChronicle = personalCenturyChronicle;
        _latestReincarnation = reincarnation;
        if (selectedAgentChanged || (reincarnation is null && hadReincarnation))
        {
            ClearReincarnationView();
        }
        SelectedNameText.Text = agent.Name;
        SelectedIdText.Text = $"{agent.Id} · 최종 목표 #{agent.TargetRank}";
        IdentityText.Text =
            $"영혼 {agent.Identity.SoulId} · 육신 {agent.Identity.BodyId} · 현생 {agent.Identity.PersonId} · " +
            $"{FormatGender(agent.LifeAuthority.Gender)} · {FormatLifeState(agent.Identity.LifeState)}";
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
        var layer = WorldAtlasRules.GetHeavenlyLayer(location.HeavenlyLayer);
        var movement = agent.Movement.Status is MovementStatus.Idle or MovementStatus.Arrived
            ? "이동 없음"
            : $"{FormatMovementStatus(agent.Movement.Status)} → " +
                WorldMapRules.GetLocation(agent.Movement.DestinationLocationId).KoreanName;
        LocationText.Text =
            $"{layer.KoreanName} / {location.KoreanName} · {FormatBiome(location.Biome)} · " +
            $"발견 {agent.SpatialKnowledge.KnownLocationCount:N0}/{agent.SpatialKnowledge.TotalProductionLocationCount:N0} · " +
            $"{movement} · 요수 사냥 {KoreanNumberFormatter.Format(agent.MonsterVictories)}/{KoreanNumberFormatter.Format(agent.MonsterHunts)}승";
        if (_agentDetailSection == AgentDetailSection.Overview
            && DecisionReasonExpander.IsExpanded)
        {
            UpdateDecisionReasonView(agent.Decision);
        }
        if (_agentDetailSection == AgentDetailSection.Overview
            && LifeAuthorityExpander.IsExpanded)
        {
            UpdateLifeAuthorityView(agent.LifeAuthority);
        }
        if (_agentDetailSection == AgentDetailSection.Social
            && CausalRuntimeExpander.IsExpanded
            && agent.Causal is { } causal)
        {
            UpdateCausalRuntimeView(causal);
        }
        if (_agentDetailSection == AgentDetailSection.Social
            && RelationsExpander.IsExpanded)
        {
            UpdateRelationsView(agent.Relations ?? Array.Empty<RelationSnapshot>());
        }
        if (_agentDetailSection == AgentDetailSection.Social
            && TeachingExpander.IsExpanded
            && agent.Teaching is { } teaching)
        {
            UpdateTeachingView(teaching);
        }
        if (_agentDetailSection == AgentDetailSection.Cultivation)
        {
            CultivationProgressBar.Maximum = Math.Max(1, agent.CultivationRequirement);
            CultivationProgressBar.Value = Math.Min(agent.CultivationProgress, agent.CultivationRequirement);
            CultivationText.Text = $"{KoreanNumberFormatter.Format(agent.CultivationProgress)} / {KoreanNumberFormatter.Format(agent.CultivationRequirement)}";
            CultivationStateText.Text = FormatCultivationState(agent.CultivationState, agent.BottleneckCause);
            var cultivation = agent.Cultivation;
            CultivationDetailProgressExpander.Header = cultivation.DetailProgress is { } detailHeader
                ? $"대경지 세부 수행 진척 · {detailHeader.CurrentStageKoreanName}"
                : "대경지 세부 수행 진척";
            if (CultivationDetailProgressExpander.IsExpanded
                && cultivation.DetailProgress is { } detailProgress)
            {
                UpdateCultivationDetailProgressView(detailProgress);
            }
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
            CultivationPlanExpander.Visibility = cultivation.ActionPlan is null
                ? Visibility.Collapsed
                : Visibility.Visible;
            CultivationPlanExpander.Header = cultivation.ActionPlan is { IsActive: true }
                ? "현재 장기 수행 계획"
                : "최근 종료된 수행 계획";
            if (CultivationPlanExpander.IsExpanded
                && cultivation.ActionPlan is { } actionPlan)
            {
                UpdateCultivationPlanView(cultivation, actionPlan);
            }
            if (CultivationTraceExpander.IsExpanded)
            {
                UpdateCultivationTraceView(cultivation);
            }
            FoundationFormationExpander.Visibility = cultivation.FoundationFormation is null
                ? Visibility.Collapsed
                : Visibility.Visible;
            if (FoundationFormationExpander.IsExpanded
                && cultivation.FoundationFormation is { } formation)
            {
                UpdateFoundationFormationView(formation);
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
        }

        if (_agentDetailSection == AgentDetailSection.Combat)
        {
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
        }
        if (_agentDetailSection == AgentDetailSection.Records)
        {
            UpdateAgentLogViews();
            if (_logService.IsAutoPersonalOutput(agent.Id))
            {
                _logService.CapturePersonalRecords(
                    agent.Id,
                    personalLog,
                    entry => FormatEventRowFileLine(FormatPersonalLog(agent, entry)),
                    chronicle,
                    entry => FormatEventRowFileLine(FormatChronicle(entry)),
                    personalCenturyChronicle,
                    entry => FormatEventRowFileLine(FormatPersonalCenturyChronicle(entry)));
            }
        }
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
        var completedVolumes = _latestPersonalCenturyChronicle.Count(entry => entry.IsComplete);
        PersonalRecordCountText.Text =
            $"최근 로그 {_latestPersonalLog.Count:N0}/100 · 세부 연대기 {_latestChronicle.Count:N0}건 · " +
            $"100년 종합 {_latestPersonalCenturyChronicle.Count:N0}권(완결 {completedVolumes:N0})";
        var automatic = _logService.IsAutoPersonalOutput(agent.Id);
        AutoPersonalOutputCheckBox.IsChecked = automatic;
        if (automatic)
        {
            SetPersonalRecordOutputStatus($"{agent.Id} 자동 기록 중");
        }
        else
        {
            SetPersonalRecordOutputStatus("개인 자동 기록 꺼짐");
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

    private void UpdateLifeAuthorityView(PersonLifeAuthoritySnapshot life)
    {
        var appearance = life.Appearance;
        var transition = life.LatestTransition;
        LifeAuthorityText.Text =
            $"{PersonLifeAuthorityRules.ContractId} r{PersonLifeAuthorityRules.ContractRevision} · 권위 revision {life.AuthorityRevision:N0}\n" +
            $"성별 {FormatGender(life.Gender)} · 영혼 {FormatSoulState(life.SoulState)} · 육신 {FormatBodyState(life.BodyState)} · 현생 {FormatLifePhase(life.LifePhase)}\n" +
            $"외형 {appearance.DefinitionId} · 선천 미도 {appearance.InnateBeautyHundredths / 100m:0.00} · 파생 미도 {appearance.DerivedBeautyHundredths / 100m:0.00} · 흉터 {appearance.ScarSeverityHundredths / 100m:0.00}\n" +
            $"키 {appearance.HeightHundredths / 100m:0.00} · 체형 {appearance.BuildHundredths / 100m:0.00} · 이목구비 {appearance.FacialFeatureHundredths / 100m:0.00} · 피부 {appearance.ComplexionHundredths / 100m:0.00} · 모발 {appearance.HairHundredths / 100m:0.00} · 기품 {appearance.BearingHundredths / 100m:0.00}\n" +
            $"최근 전이 {FormatLifeTransition(transition.Reason)} · {transition.Status} · {FormatGameDate(GameCalendar.GetDate(new SimulationTick(transition.Tick)))} · " +
            $"{PersonLifeAuthorityRules.AppearanceDistributionReviewId} 다음 버전 검토";
    }

    private void UpdateCausalRuntimeView(AgentCausalSnapshot causal)
    {
        var path = causal.CurrentPath;
        var facts = causal.PerceivedFacts.Count == 0
            ? "인지 사실 없음"
            : string.Join(" · ", causal.PerceivedFacts.Select(FormatPerceivedFact));
        var memories = causal.CurrentLifeMemories.Count == 0
            ? "현재 생 기억 없음"
            : string.Join(
                "\n",
                causal.CurrentLifeMemories.Reverse().Select(memory =>
                    $"{memory.Id} · {FormatMemoryKind(memory.Kind)} · 강도 {memory.StrengthHundredths / 100m:0.00} · 강화 {memory.ReinforcementCount:N0}회 · " +
                    $"{FormatGameDate(GameCalendar.GetDate(new SimulationTick(memory.LastReinforcedTick)))}" +
                    (memory.RelatedPersonId.Value == 0 ? string.Empty : $" · 상대 {memory.RelatedPersonId}") +
                    (memory.RelationId.Value == 0 ? string.Empty : $" · {memory.RelationId}")));
        var claim = causal.LatestKnowledgeClaim is { } knowledge
            ? $"{knowledge.Id} · {knowledge.Kind}/{knowledge.Status} · 출처 {knowledge.SourcePersonId} · {knowledge.TeachingProvenanceId}"
            : "지식 주장 없음";
        CausalRuntimeText.Text =
            $"{causal.ContractId} r{causal.ContractRevision} · 권위 revision {causal.AuthorityRevision:N0}\n" +
            $"{path.GoalId} → {path.PlanId} → {path.TaskId} → {path.ActionId}\n" +
            $"인과 {path.ThreadId} · 정의 {path.EventDefinitionId} · 사건 {path.EventInstanceId} · 결과 {path.ResultId} · 기록 {path.EventRecordId}\n" +
            $"root {path.RootRecordId} · parent {(path.ParentRecordId?.ToString() ?? "없음")} · {path.Resolution} · 대기 {path.IsWaiting} · 중단 {path.WasInterrupted}\n" +
            $"실패 범위 {path.FailureScope} · 행동 실패와 Task·Plan·Goal 실패는 별도 상태로 보존\n" +
            $"인지: {facts}\n지식: {claim}\n현재 생 기억 {causal.CurrentLifeMemories.Count}/{AgentCausalRules.MemoryCapacityPerAgent}\n{memories}\n" +
            $"{AgentCausalRules.MemoryPolicyReviewId} 다음 버전 검토";
    }

    private static string FormatGender(GenderIdentity gender) => gender switch
    {
        GenderIdentity.Female => "여성",
        GenderIdentity.Male => "남성",
        GenderIdentity.NonBinary => "비이분법",
        _ => "미정",
    };

    private static string FormatSoulState(SoulEmbodimentState state) => state switch
    {
        SoulEmbodimentState.Embodied => "육신 안착",
        SoulEmbodimentState.AwaitingReincarnation => "윤회 대기",
        SoulEmbodimentState.Crossing => "윤회의 강 도하",
        SoulEmbodimentState.Dissolved => "소멸",
        _ => state.ToString(),
    };

    private static string FormatBodyState(BodyLifeState state) => state switch
    {
        BodyLifeState.Living => "생존",
        BodyLifeState.Dead => "사망",
        BodyLifeState.Destroyed => "소실",
        _ => state.ToString(),
    };

    private static string FormatLifePhase(CurrentLifePhase phase) => phase switch
    {
        CurrentLifePhase.Living => "생존 중",
        CurrentLifePhase.DeathConfirmed => "사망 확정",
        CurrentLifePhase.ReincarnationHandoff => "윤회 인계",
        CurrentLifePhase.Archived => "생 기록 보존",
        _ => phase.ToString(),
    };

    private static string FormatLifeTransition(LifeTransitionReason reason) => reason switch
    {
        LifeTransitionReason.Birth => "탄생",
        LifeTransitionReason.LifespanDeath => "수명 종료",
        LifeTransitionReason.CombatDeath => "전투 사망",
        LifeTransitionReason.BodyDestroyed => "육신 소실",
        LifeTransitionReason.ReincarnationEntered => "윤회 진입",
        LifeTransitionReason.Rebirth => "환생",
        _ => reason.ToString(),
    };

    private static string FormatMemoryKind(AgentMemoryKind kind) => kind switch
    {
        AgentMemoryKind.Birth => "탄생",
        AgentMemoryKind.Decision => "행동 선택",
        AgentMemoryKind.Failure => "실패·보류",
        AgentMemoryKind.Relationship => "관계 상호작용",
        AgentMemoryKind.Teaching => "가르침·학습",
        AgentMemoryKind.Combat => "전투",
        AgentMemoryKind.Death => "죽음",
        AgentMemoryKind.Rebirth => "환생",
        _ => kind.ToString(),
    };

    private static string FormatPerceivedFact(PerceivedFactState fact) => fact.Kind switch
    {
        PerceivedFactKind.OwnLifeState => $"생 상태 {(PersonLifeState)fact.NumericValue}",
        PerceivedFactKind.OwnHealthRatio => $"체력 {fact.NumericValue / 100m:0.##}%",
        PerceivedFactKind.OwnCultivationState => $"수행 {(CultivationState)fact.NumericValue}",
        PerceivedFactKind.CurrentLocation => $"위치 #{fact.NumericValue}",
        PerceivedFactKind.ObservedCombatRank => $"관찰 순위 #{fact.NumericValue:N0}",
        _ => fact.NumericValue.ToString("N0", CultureInfo.CurrentCulture),
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
                var latestResult = relation.LatestInteractionResult;
                var resultText = latestResult.Id.Value == 0
                    ? "최근 typed result 없음"
                    : $"최근 결과 {latestResult.Id} · {FormatRelationResultStatus(latestResult.Status)} · " +
                        $"{FormatGameDate(GameCalendar.GetDate(new SimulationTick(latestResult.Tick)))} · " +
                        $"source {(latestResult.SourceEventRecordId.Value == 0 ? "생애 경계" : latestResult.SourceEventRecordId.ToString())}";
                var context = relation.ExternalContext;
                var contextText =
                    $"맥락 · 같은 지역 {(context.IsCoLocated ? "예" : "아니오")} · " +
                    $"가계 {FormatRelationContext(context.FamilyContext, context.SelfFamilyId, context.OtherFamilyId)} · " +
                    $"조직 {FormatRelationContext(context.OrganizationContext)} · " +
                    $"평판 {FormatRelationContext(context.ReputationContext)} · " +
                    $"지식 {FormatRelationKnowledgeContext(context)}";
                return
                    $"{relation.OtherName} · {relation.OtherAgentId} · {relation.OtherPersonId} · {relation.Id}\n" +
                    $"역할 {role}\n" +
                    $"나→상대 {FormatRelationMetrics(relation.TowardOther)}\n" +
                    $"상대→나 {FormatRelationMetrics(relation.OtherTowardSelf)}\n" +
                    $"{history}\n{resultText}\n{contextText}";
            })) +
            $"\n\n희소 후보·축 변화·Role 임시 수치: {RelationRules.ProvisionalReviewId} 다음 버전 재검토";
    }

    private static string FormatRelationContext(
        RelationContextAvailability availability,
        FamilyLineageId selfFamily = default,
        FamilyLineageId otherFamily = default)
    {
        if (availability == RelationContextAvailability.DataUnavailable)
        {
            return "DataUnavailable";
        }

        return selfFamily.Value == 0
            ? "Available"
            : $"{selfFamily}/{otherFamily}";
    }

    private static string FormatRelationKnowledgeContext(RelationExternalContextSnapshot context) =>
        context.KnowledgeContext == RelationContextAvailability.DataUnavailable
            ? "DataUnavailable"
            : context.LatestKnowledgeClaimId.ToString();

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

    private static string FormatRelationResultStatus(RelationInteractionStatus status) => status switch
    {
        RelationInteractionStatus.Applied => "상호작용 적용",
        RelationInteractionStatus.TeachingProgress => "가르침 진전",
        RelationInteractionStatus.TeachingMisunderstood => "오해 발생",
        RelationInteractionStatus.TeachingCorrected => "오해 교정",
        RelationInteractionStatus.TeachingRefused => "거절·중단",
        RelationInteractionStatus.InterruptedByLifeBoundary => "생애 경계 중단",
        _ => status.ToString(),
    };

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
        var attempt = trace.RollBasisPoints < 0
            ? "Attempt Snapshot 해당 없음"
            : $"Snapshot {(trace.AttemptSnapshotFrozen ? "동결" : "미동결")} · RNG {trace.RngDomain} · " +
                $"Effect Registry r{trace.AttemptEffectRegistryRevision} · 후보 {trace.CandidateEffectCount:N0} · " +
                $"선택 {trace.SelectedEffectCount:N0} · {FormatCultivationAvailability(trace.AttemptEffectAvailability)}";
        var failure = trace.FailureEffectAvailability == CultivationValueAvailability.NotApplicable
            ? "실패 효과 해당 없음"
            : $"실패 효과 {FormatCultivationFailureEffect(trace.FailureEffect)} · " +
                FormatCultivationAvailability(trace.FailureEffectAvailability);
        var external = trace.EnvironmentAvailability == CultivationValueAvailability.NotCalculated
            ? "외부 계수 미계산"
            : $"환경 {trace.EnvironmentFactorBasisPoints / 100m:0.00}% · " +
                $"법칙 이해 {FormatCultivationAvailability(trace.LawUnderstandingAvailability)} · " +
                $"운명/행운 {FormatCultivationAvailability(trace.LuckFateAvailability)}";
        CultivationTraceText.Text =
            $"{formula.StableId} r{formula.Revision} · {formula.Purpose}" +
            (formula.RequiresReview ? " · 다음 버전 검토값 포함" : string.Empty) +
            $"\n권위 revision {trace.AuthorityRevisionBefore:N0} → {trace.AuthorityRevisionAfter:N0} · " +
            $"{FormatBreakthroughClass(trace.AttemptClass)} · {FormatBreakthroughOutcome(trace.Outcome)}" +
            $"\nWork {KoreanNumberFormatter.Format(trace.WorkBefore)} + {KoreanNumberFormatter.Format(trace.WorkGain)} = " +
            $"{KoreanNumberFormatter.Format(trace.WorkAfter)} / {KoreanNumberFormatter.Format(trace.RequiredWork)} · " +
            $"이월 {KoreanNumberFormatter.Format(trace.CarryoverWork)}" +
            $"\n{chance} · 차단 {FormatCultivationBlocker(trace.Blocker)}" +
            $"\n{attempt} · {failure}" +
            $"\n{external}";
    }

    private void UpdateCultivationDetailProgressView(
        CultivationDetailProgressSnapshot progress)
    {
        CultivationDetailProgressSummaryText.Text =
            $"{progress.ScopeKoreanName} 전체 · " +
            $"{KoreanNumberFormatter.Format(progress.AccumulatedWork)} / " +
            $"{KoreanNumberFormatter.Format(progress.TotalRequiredWork)} Work " +
            $"({progress.OverallProgressBasisPoints / 100m:0.00}%) · " +
            $"현재 {progress.CurrentStageIndex + 1}/{progress.Stages.Count} " +
            progress.CurrentStageKoreanName;
        CultivationDetailProgressItems.ItemsSource = progress.Stages
            .Select(stage => new CultivationDetailProgressRow(
                stage.KoreanName,
                $"{FormatCultivationDetailStageState(stage.State)} · {stage.Description}",
                stage.ProgressBasisPoints / 100d,
                $"{stage.ProgressBasisPoints / 100m:0.00}%",
                FormatCultivationDetailStageState(stage.State)))
            .ToArray();
    }

    private static string FormatCultivationDetailStageState(
        CultivationDetailStageState state) => state switch
    {
        CultivationDetailStageState.Pending => "대기",
        CultivationDetailStageState.Active => "진행 중",
        CultivationDetailStageState.Completed => "완료",
        _ => state.ToString(),
    };

    private void UpdateCultivationPlanView(
        CultivationReadProjection cultivation,
        CultivationActionPlanSnapshot plan)
    {
        var completedMonths = Math.Min(plan.CompletedMonths, plan.PlannedMonths);
        var remainingMonths = Math.Max(0, plan.PlannedMonths - completedMonths);
        CultivationPlanScheduleText.Text =
            $"상태 {(plan.IsActive ? "진행 중" : "종료")} · 강도 {FormatCultivationPlanIntensity(plan.Intensity)} · " +
            $"계획 {FormatPlanMonths(plan.PlannedMonths)} · 진행 {FormatPlanMonths(completedMonths)} · " +
            $"남음 {FormatPlanMonths(remainingMonths)}\n" +
            $"시작 {FormatGameDate(GameCalendar.GetDate(plan.StartedTick))} · " +
            $"종료 예정 {FormatGameDate(GameCalendar.GetDate(plan.PlannedEndTick))}\n" +
            $"고정 장소 {WorldMapRules.GetLocation(plan.LocationId).KoreanName} · " +
            "계획 중에는 위치·목표·행동을 다시 선택하지 않음";

        CultivationPlanResourceText.Text =
            $"소비 방침 {FormatCultivationResourcePolicy(plan.ResourcePolicy)}\n" +
            $"영석 {KoreanNumberFormatter.Format(plan.SpiritStonesConsumed)} / " +
            $"{KoreanNumberFormatter.Format(plan.SpiritStoneBudget)} · " +
            $"성장 단약 {plan.GrowthPillsConsumed:N0} / {plan.GrowthPillBudget:N0}\n" +
            $"수행 진법 {FormatCultivationFormationSupport(plan.FormationSupport)} · " +
            $"지식 {plan.FormationKnowledgeHundredths / 100m:0.00} · " +
            $"설치 경험 {plan.FormationInstallationExperienceHundredths / 100m:0.00} · " +
            $"설치 결과 {(plan.FormationInstallationSucceeded ? "성공" : "해당 없음·실패")}";

        var partner = plan.TeachingPartner is { } partnerId
            ? GetAgentName(partnerId)
            : "없음";
        CultivationPlanTeachingText.Text =
            $"공동 수행 역할 {FormatCultivationTeachingRole(plan.TeachingRole)} · 상대 {partner}\n" +
            $"학습 흡수 보정 {plan.TeachingBonusBasisPoints / 100m:0.00}% · " +
            $"전체 조건 계수 {plan.ConditionFactorBasisPoints / 100m:0.00}% · " +
            $"누적 수행 경험 {KoreanNumberFormatter.Format(cultivation.CultivationExperience)}\n" +
            "교사의 수행 진척을 이전하지 않습니다. 학습자는 교사 지식·경험과 양측 자질에 따른 별도 보너스를 얻습니다." +
            (plan.RequiresReview ? " · PC_TEMP_REVIEW: 기간·진법·소비 계수는 다음 버전 검토" : string.Empty);
    }

    private static string FormatPlanMonths(long months)
    {
        var years = months / GameCalendar.MonthsPerYear;
        var remainingMonths = months % GameCalendar.MonthsPerYear;
        if (years == 0)
        {
            return $"{remainingMonths:N0}개월";
        }

        return remainingMonths == 0
            ? $"{KoreanNumberFormatter.Format(years)}년"
            : $"{KoreanNumberFormatter.Format(years)}년 {remainingMonths:N0}개월";
    }

    private static string FormatCultivationPlanIntensity(CultivationPlanIntensity intensity) =>
        intensity switch
        {
            CultivationPlanIntensity.Leisurely => "여유 수행",
            CultivationPlanIntensity.Steady => "일반 수행",
            CultivationPlanIntensity.ClosedDoor => "폐관 수행",
            _ => intensity.ToString(),
        };

    private static string FormatCultivationResourcePolicy(CultivationResourcePolicy policy) =>
        policy switch
        {
            CultivationResourcePolicy.None => "무소비",
            CultivationResourcePolicy.SpiritStones => "영석",
            CultivationResourcePolicy.GrowthPills => "성장 단약",
            CultivationResourcePolicy.Mixed => "단약 우선·영석 보조",
            _ => policy.ToString(),
        };

    private static string FormatCultivationFormationSupport(
        CultivationFormationSupport support) => support switch
        {
            CultivationFormationSupport.None => "미설치",
            CultivationFormationSupport.ExistingAtLocation => "장소 기존 진법",
            CultivationFormationSupport.SelfInstalled => "직접 설치",
            CultivationFormationSupport.ExternalAssistance => "외부 도움 설치",
            _ => support.ToString(),
        };

    private static string FormatCultivationTeachingRole(CultivationTeachingRole role) =>
        role switch
        {
            CultivationTeachingRole.None => "단독 수행",
            CultivationTeachingRole.Instructor => "가르치는 입장",
            CultivationTeachingRole.Learner => "배우는 입장",
            _ => role.ToString(),
        };

    private void UpdateFoundationFormationView(FoundationFormationSnapshot formation)
    {
        var source = formation.Source;
        FoundationFormationText.Text =
            $"{formation.MethodStableId} r{formation.MethodRevision} · 5단계 {formation.CompletedSteps}" +
            $"\n경로 {FormatFoundationRoute(source.Route)} · Source {FormatFoundationSource(source.Kind)} · " +
            $"위치 {WorldMapRules.GetLocation(source.LocationId).KoreanName} · revision {source.SourceRevision} · " +
            $"동결 {FormatGameDate(GameCalendar.GetDate(new SimulationTick(source.FrozenTick)))} · " +
            $"가용성 {FormatCultivationAvailability(source.Availability)}" +
            $"\nSource 수 {source.SourceCount:N0} · 남은 공급 {KoreanNumberFormatter.Format(source.RemainingSupply)} · " +
            $"형성 품질 10요소 {string.Join(" / ", formation.QualityComponentsHundredths.Select(value => value / 100m).Select(value => value.ToString("0.00", CultureInfo.InvariantCulture)))} · " +
            $"안착 필요 {(formation.RequiresStabilization ? "예" : "아니오")}";
    }

    private static string FormatCultivationAvailability(CultivationValueAvailability value) => value switch
    {
        CultivationValueAvailability.Actual => "실제값",
        CultivationValueAvailability.ActualZero => "확정 0",
        CultivationValueAvailability.NotCalculated => "미계산",
        CultivationValueAvailability.NotApplicable => "해당 없음",
        CultivationValueAvailability.Unavailable => "자료 없음",
        CultivationValueAvailability.Unapproved => "미승인",
        CultivationValueAvailability.Blocked => "차단",
        _ => value.ToString(),
    };

    private static string FormatCultivationFailureEffect(CultivationFailureEffectKind value) => value switch
    {
        CultivationFailureEffectKind.CleanFailure => "무손실 실패",
        CultivationFailureEffectKind.AttemptResourceConsumed => "시도 자원 소비",
        CultivationFailureEffectKind.FailureResourceLoss => "실패 자원 손실",
        CultivationFailureEffectKind.PreparationOrWorkDisturbance => "준비/Work 교란",
        CultivationFailureEffectKind.RealmComponentDisturbance => "경지 구성요소 교란",
        CultivationFailureEffectKind.Injury => "부상",
        CultivationFailureEffectKind.SoulOrMentalBurden => "영혼/정신 부담",
        CultivationFailureEffectKind.FatalBodyLoss => "육신 상실",
        CultivationFailureEffectKind.RetryBlock => "재시도 차단",
        _ => value.ToString(),
    };

    private static string FormatFoundationSource(FoundationSourceKind value) => value switch
    {
        FoundationSourceKind.None => "없음",
        FoundationSourceKind.InternalBodyAndSpiritualRoot => "육신·영근 내부 기반",
        FoundationSourceKind.LocationSpiritVeins => "지점 영맥 Snapshot",
        FoundationSourceKind.ManifestationEnvironmentWindow => "현현·환경 유효창",
        _ => value.ToString(),
    };

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
        CultivationBottleneckCause.Environment => "환경·Source",
        _ => value.ToString(),
    };

    private static string FormatMovementStatus(MovementStatus value) => value switch
    {
        MovementStatus.Idle => "대기",
        MovementStatus.Planned => "경로 계획",
        MovementStatus.InTransit => "이동 중",
        MovementStatus.Arrived => "도착",
        MovementStatus.Blocked => "차단",
        MovementStatus.Rerouted => "재경로",
        MovementStatus.Interrupted => "중단",
        _ => value.ToString(),
    };

}
