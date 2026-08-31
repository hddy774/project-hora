using System.Windows.Controls;
using Chronicles.Core;
using Chronicles.Simulation;
using static Chronicles.Desktop.PresentationText;

namespace Chronicles.Desktop;

public partial class MainWindow
{
    private void UpdateSelectedAgent(
        AgentSnapshot agent,
        IReadOnlyList<PersonalLogEntry> personalLog,
        IReadOnlyList<LifeChronicleEntry> chronicle)
    {
        _latestAgent = agent;
        _latestPersonalLog = personalLog;
        _latestChronicle = chronicle;
        SelectedNameText.Text = agent.Name;
        SelectedIdText.Text = $"{agent.Id} · 최종 목표 #{agent.TargetRank}";
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

        if (AgentLogTabControl.SelectedIndex == 0)
        {
            PersonalLogList.ItemsSource = _latestPersonalLog
                .Where(entry => MatchesPersonalLogCategory(entry, _personalLogCategory))
                .Reverse()
                .Select(entry => FormatPersonalLog(agent, entry))
                .ToArray();
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

    private static bool MatchesPersonalLogCategory(PersonalLogEntry entry, int category) => category switch
    {
        0 => true,
        1 => entry.Activity is AgentActivity.Cultivating
            or AgentActivity.TrainingTechnique
            or AgentActivity.TrainingSpell
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
                or WorldEventKind.Rebirth,
        _ => true,
    };

    private static bool MatchesChronicleCategory(ChronicleEventKind kind, int category) => category switch
    {
        0 => true,
        1 => kind is ChronicleEventKind.MinorStageBreakthrough
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
            or ChronicleEventKind.Rebirth,
        4 => kind is ChronicleEventKind.FortuitousEncounter
            or ChronicleEventKind.TechniqueAcquired
            or ChronicleEventKind.SpellAcquired,
        _ => true,
    };

    private static string FormatEffectPercent(int basisPoints) => $"{basisPoints / 100m:0.##}%";

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

}
