using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Chronicles.Core;
using Chronicles.Persistence;
using Chronicles.Simulation;

namespace Chronicles.Desktop;

public partial class MainWindow : Window, IDisposable
{
    private const ulong DefaultWorldSeed = 20260830UL;
    private const int ExplorerPageSize = 500;
    private SimulationRunner? _runner;
    private WorldSnapshot? _latestSnapshot;
    private AgentId? _selectedAgent;
    private RankingCategory? _rankingCategory = RankingCategory.CombatPower;
    private int _explorerOffset;
    private int _selectedMarketGoodId;
    private int _speed = 1;
    private bool _disposed;
    private bool _applyingObservation;
    private int _observationDispatchQueued;

    public MainWindow()
    {
        InitializeComponent();
        Title = $"{ProductInfo.KoreanTitle} PC v{ProductInfo.Version}";
        VersionText.Text = $"VERSION {ProductInfo.Version}";
        var world = World.CreateDefault(DefaultWorldSeed);
        _runner = new SimulationRunner(world);
        _runner.ObservationInvalidated += Runner_ObservationInvalidated;
        _runner.Faulted += Runner_Faulted;
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _runner?.Start();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_runner is null)
        {
            return;
        }

        _runner.ObservationInvalidated -= Runner_ObservationInvalidated;
        _runner.Faulted -= Runner_Faulted;
        _runner.Dispose();
        _runner = null;
        GC.SuppressFinalize(this);
    }

    private void Runner_ObservationInvalidated()
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        if (Interlocked.Exchange(ref _observationDispatchQueued, 1) != 0)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                Interlocked.Exchange(ref _observationDispatchQueued, 0);
                RefreshVisibleObservation();
            }));
    }

    private void Runner_Faulted(Exception exception)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            StatusText.Text = $"시뮬레이션 오류: {exception.Message}";
            StatusText.Foreground = (Brush)FindResource("DangerBrush");
        }));
    }

    private void RefreshVisibleObservation(AgentId? selectedAgent = null)
    {
        if (_runner is null)
        {
            return;
        }

        if (selectedAgent is { } selected)
        {
            _selectedAgent = selected;
        }

        var observation = _runner.GetObservation(
            _selectedAgent,
            sections: GetVisibleObservationSections());
        ApplyObservation(observation);
    }

    private ObservationSections GetVisibleObservationSections()
    {
        var sections = WorldInfoTabControl.SelectedIndex switch
        {
            0 => ObservationSections.StatisticsSummary,
            1 => ObservationSections.MarketSummary,
            2 => ObservationSections.Market,
            3 => ObservationSections.Map,
            4 => ObservationSections.WorldLog,
            _ => ObservationSections.None,
        };
        if (WorldInfoTabControl.SelectedIndex == 0)
        {
            if (RealmDistributionExpander.IsExpanded)
            {
                sections |= ObservationSections.RealmDistribution;
            }

            if (PowerDistributionExpander.IsExpanded)
            {
                sections |= ObservationSections.CombatPowerDistribution;
            }
        }

        sections |= AgentLogTabControl.SelectedIndex == 1
            ? ObservationSections.Chronicle
            : ObservationSections.PersonalLog;
        sections |= _rankingCategory switch
        {
            RankingCategory.CombatPower => ObservationSections.CombatPowerRanking,
            RankingCategory.Realm => ObservationSections.RealmRanking,
            RankingCategory.Aptitude => ObservationSections.AptitudeRanking,
            null => ObservationSections.None,
            _ => ObservationSections.None,
        };
        return sections;
    }

    private void ApplyObservation(WorldObservation observation)
    {
        _applyingObservation = true;
        var snapshot = observation.World;
        _latestSnapshot = snapshot;
        _selectedAgent = observation.SelectedAgent.Id;
        WorldTickText.Text = FormatGameDate(GameCalendar.GetDate(snapshot.Tick));
        PopulationText.Text = KoreanNumberFormatter.Format(snapshot.Population);
        ActiveCombatText.Text = KoreanNumberFormatter.Format(snapshot.ActiveCombats);
        CompletedCombatText.Text = KoreanNumberFormatter.Format(snapshot.TotalCombatsCompleted);
        ChallengeText.Text = KoreanNumberFormatter.Format(snapshot.TotalChallenges);
        BreakthroughText.Text = KoreanNumberFormatter.Format(snapshot.TotalBreakthroughs);
        StatusText.Text = _runner?.IsPaused == true
            ? $"일시정지 · {_speed}× · seed {DefaultWorldSeed}"
            : $"실시간 진행 · {_speed}× · 게임 1년당 현실 {GameCalendar.GetRealSecondsPerGameYear(_speed):0.###}초";
        StatusText.Foreground = (Brush)FindResource("SecondaryTextBrush");

        try
        {
            UpdateRankingRows(snapshot);
            UpdateVisibleWorldPanel(snapshot);
            UpdateSelectedAgent(
                observation.SelectedAgent,
                observation.SelectedPersonalLog,
                observation.SelectedChronicle);
        }
        finally
        {
            _applyingObservation = false;
        }
    }

    private void UpdateRankingRows(WorldSnapshot snapshot)
    {
        RankingRow[] rows;
        var search = SearchTextBox.Text.Trim();
        if (_rankingCategory is { } category)
        {
            var rankings = category switch
            {
                RankingCategory.CombatPower => snapshot.CombatPowerRankings,
                RankingCategory.Realm => snapshot.RealmRankings,
                RankingCategory.Aptitude => snapshot.AptitudeRankings,
                _ => throw new InvalidOperationException($"지원하지 않는 랭킹 분류입니다: {category}"),
            };
            rows = rankings.Select(ToRankingRow).ToArray();
            RankingScopeText.Text = $"TOP {rankings.Count:N0}";
            ExplorerNavigationPanel.Visibility = Visibility.Collapsed;
        }
        else if (_runner is null)
        {
            rows = Array.Empty<RankingRow>();
        }
        else
        {
            var maximumOffset = Math.Max(0, ((snapshot.Population - 1) / ExplorerPageSize) * ExplorerPageSize);
            _explorerOffset = Math.Min(_explorerOffset, maximumOffset);
            var agents = search.Length == 0
                ? _runner.BrowseAgents(_explorerOffset, ExplorerPageSize)
                : _runner.SearchAgents(search, 1_000);
            rows = agents.Select(ToRankingRow).ToArray();

            if (search.Length == 0)
            {
                var first = rows.Length == 0 ? 0 : _explorerOffset + 1;
                var last = _explorerOffset + rows.Length;
                RankingScopeText.Text = $"{first:N0}–{last:N0} / {snapshot.Population:N0}";
                var currentPage = (_explorerOffset / ExplorerPageSize) + 1;
                var pageCount = Math.Max(1, (snapshot.Population + ExplorerPageSize - 1) / ExplorerPageSize);
                ExplorerPageText.Text = $"{currentPage:N0} / {pageCount:N0} 페이지";
                ExplorerPreviousButton.IsEnabled = _explorerOffset > 0;
                ExplorerNextButton.IsEnabled = _explorerOffset + ExplorerPageSize < snapshot.Population;
                ExplorerNavigationPanel.Visibility = Visibility.Visible;
            }
            else
            {
                RankingScopeText.Text = $"검색 {rows.Length:N0}명";
                ExplorerNavigationPanel.Visibility = Visibility.Collapsed;
            }
        }

        RankingGrid.ItemsSource = rows;
        if (_selectedAgent is { } selected)
        {
            RankingGrid.SelectedItem = rows.FirstOrDefault(item => item.AgentId == selected);
        }
    }

    private void UpdateEventRows(WorldSnapshot snapshot)
    {
        var rows = snapshot.RecentEvents
            .Reverse()
            .Select(item => FormatEvent(snapshot, item))
            .ToArray();
        EventList.ItemsSource = rows;
    }

    private void UpdateVisibleWorldPanel(WorldSnapshot snapshot)
    {
        switch (WorldInfoTabControl.SelectedIndex)
        {
            case 0:
                UpdateStatisticsPanel(snapshot);
                break;
            case 1:
                UpdateCityPanel(snapshot);
                break;
            case 2:
                UpdateMarketPanel(snapshot);
                break;
            case 3:
                UpdateMapPanel(snapshot);
                break;
            case 4:
                UpdateEventRows(snapshot);
                break;
        }
    }

    private void UpdateStatisticsPanel(WorldSnapshot snapshot)
    {
        var statistics = snapshot.Statistics;
        DeathStatText.Text = KoreanNumberFormatter.Format(statistics.TotalDeaths);
        WinStatText.Text = KoreanNumberFormatter.Format(statistics.TotalWins);
        LossStatText.Text = KoreanNumberFormatter.Format(statistics.TotalLosses);
        ReincarnationStatText.Text = KoreanNumberFormatter.Format(statistics.TotalReincarnations);
        MissionStatText.Text = KoreanNumberFormatter.Format(statistics.TotalMissionsCompleted);
        SpiritStoneStatText.Text = KoreanNumberFormatter.Format(statistics.TotalSpiritStones);

        var movement = statistics.RankingMovement;
        var changeRate = snapshot.Population == 0
            ? 0m
            : movement.ChangedCount * 100m / snapshot.Population;
        var averageMovement = movement.ChangedCount == 0
            ? 0m
            : movement.TotalAbsoluteMovement / (decimal)movement.ChangedCount;
        RankingMovementText.Text =
            $"변동 {changeRate:0.##}% · 상승 {movement.RisenCount:N0} · 하락 {movement.FallenCount:N0} · 유지 {movement.UnchangedCount:N0} · 평균 {averageMovement:0.##}위";

        if (RealmDistributionExpander.IsExpanded)
        {
            RealmDistributionGrid.ItemsSource = statistics.RealmDistribution
                .Select(item => new DistributionRow(
                    item.Realm is { } realm ? FormatRealmStage(realm) : "미입문",
                    KoreanNumberFormatter.Format(item.Count),
                    FormatPopulationRatio(item.Count, snapshot.Population)))
                .ToArray();
        }

        if (PowerDistributionExpander.IsExpanded)
        {
            PowerDistributionGrid.ItemsSource = statistics.CombatPowerDistribution
                .Select(item => new DistributionRow(
                    FormatPowerRange(item.MinimumInclusive, item.MaximumExclusive),
                    KoreanNumberFormatter.Format(item.Count),
                    FormatPopulationRatio(item.Count, snapshot.Population)))
                .ToArray();
        }
    }

    private void UpdateCityPanel(WorldSnapshot snapshot)
    {
        CityTreasuryText.Text = $"{KoreanNumberFormatter.Format(snapshot.Market.TreasurySpiritStones)} 영석";
        CityTransactionText.Text =
            $"구매 {KoreanNumberFormatter.Format(snapshot.Market.PurchaseTransactions)}건 · 판매 {KoreanNumberFormatter.Format(snapshot.Market.SaleTransactions)}건";
        CityMissionText.Text =
            $"임무 납품 {KoreanNumberFormatter.Format(snapshot.Market.MissionDeliveries)}건 · 가공 재료 {KoreanNumberFormatter.Format(snapshot.Market.MaterialsProcessed)}개";
        CityProductionText.Text =
            $"단약 {KoreanNumberFormatter.Format(snapshot.Market.PillsProduced)} · 법보 {KoreanNumberFormatter.Format(snapshot.Market.TreasuresProduced)} · 공법서 {KoreanNumberFormatter.Format(snapshot.Market.TechniqueBooksProduced)} · 법술서 {KoreanNumberFormatter.Format(snapshot.Market.SpellBooksProduced)}";
        CityInventoryText.Text =
            $"총 {KoreanNumberFormatter.Format(snapshot.Market.Goods.Sum(item => item.Stock))}개 · 재료 {KoreanNumberFormatter.Format(snapshot.Market.Goods.Where(item => item.Kind == MarketGoodKind.Material).Sum(item => item.Stock))}개 · 가공품 {KoreanNumberFormatter.Format(snapshot.Market.Goods.Where(item => item.Kind != MarketGoodKind.Material).Sum(item => item.Stock))}개";
        CityFlowText.Text =
            $"공급 {KoreanNumberFormatter.Format(snapshot.Market.Goods.Sum(item => item.Supply))} · 수요 {KoreanNumberFormatter.Format(snapshot.Market.Goods.Sum(item => item.Demand))}";
    }

    private void UpdateMarketPanel(WorldSnapshot snapshot)
    {
        MarketTreasuryText.Text = $"도시 영석 {KoreanNumberFormatter.Format(snapshot.Market.TreasurySpiritStones)}";
        var marketRows = snapshot.Market.Goods
            .Select(item => new MarketRow(
                item.GoodId,
                FormatMarketGood(item),
                KoreanNumberFormatter.Format(item.CityBasePrice),
                KoreanNumberFormatter.Format(item.CurrentPrice),
                FormatPriceChange(item.CityBasePrice, item.CurrentPrice),
                KoreanNumberFormatter.Format(item.Stock),
                KoreanNumberFormatter.Format(item.Supply),
                KoreanNumberFormatter.Format(item.Demand)))
            .ToArray();
        MarketGrid.ItemsSource = marketRows;
        var selectedMarketRow = marketRows.FirstOrDefault(item => item.GoodId == _selectedMarketGoodId)
            ?? marketRows[0];
        _selectedMarketGoodId = selectedMarketRow.GoodId;
        MarketGrid.SelectedItem = selectedMarketRow;
        if (MarketDetailExpander.IsExpanded)
        {
            UpdateMarketDetail(snapshot.Market.Goods[_selectedMarketGoodId]);
        }
    }

    private void UpdateMapPanel(WorldSnapshot snapshot)
    {
        MapSummaryText.Text =
            $"요수 사냥 {KoreanNumberFormatter.Format(snapshot.Map.TotalMonsterHunts)}회 · 승리 {KoreanNumberFormatter.Format(snapshot.Map.TotalMonsterVictories)}회 · 도시·숲·평야·산맥 실시간 인구";
        MapTileList.ItemsSource = snapshot.Map.Locations
            .OrderBy(item => item.Y)
            .ThenBy(item => item.X)
            .Select(item => new MapTileRow(
                item.Name,
                FormatBiome(item.Biome),
                $"{KoreanNumberFormatter.Format(item.Population)}명",
                FormatMonsterList(item),
                GetBiomeBrush(item.Biome)))
            .ToArray();
        if (MapLocationExpander.IsExpanded)
        {
            MapLocationGrid.ItemsSource = snapshot.Map.Locations
                .OrderBy(item => item.Y)
                .ThenBy(item => item.X)
                .Select(item => new MapLocationRow(
                    item.Name,
                    FormatBiome(item.Biome),
                    $"{item.X},{item.Y}",
                    KoreanNumberFormatter.Format(item.Population),
                    FormatMonsterList(item)))
                .ToArray();
        }
    }

    private void UpdateSelectedAgent(
        AgentSnapshot agent,
        IReadOnlyList<PersonalLogEntry> personalLog,
        IReadOnlyList<LifeChronicleEntry> chronicle)
    {
        SelectedNameText.Text = agent.Name;
        SelectedIdText.Text = $"{agent.Id} · 최종 목표 #{agent.TargetRank}";
        SelectedRankText.Text = $"전투력 #{agent.Rank:N0} · 경지 #{agent.RealmRank:N0} · 자질 #{agent.AptitudeRank:N0}";
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
        ActionScheduleText.Text =
            $"누적 행동 {KoreanNumberFormatter.Format(agent.ActionsTaken)}회 · 마지막 {FormatOptionalGameDate(agent.LastActionDate)} · 다음 {FormatGameDate(GameCalendar.GetDate(agent.NextDecisionTick))}";
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
        RecordText.Text = $"{KoreanNumberFormatter.Format(agent.Wins)}승 {KoreanNumberFormatter.Format(agent.Losses)}패 · 승률 {winRate:0.##}% · 경험 {KoreanNumberFormatter.Format(agent.CombatExperience)}";
        SpiritStoneText.Text = $"{KoreanNumberFormatter.Format(agent.SpiritStones)}개";
        PillInventoryText.Text = $"수행단 {agent.GrowthPills:N0} · 돌파단 {agent.BreakthroughPills:N0}";
        MissionText.Text = agent.ActiveMission is { } mission
            ? $"{EconomyRules.GetMaterial(mission.Material).KoreanName} {mission.HeldCount:N0}/{mission.RequiredCount:N0} · 완료 {agent.MissionsCompleted:N0}"
            : $"수주 대기 · 완료 {agent.MissionsCompleted:N0}";
        MaterialText.Text = $"{KoreanNumberFormatter.Format(agent.InventoryItemCount)}개";
        EquipmentText.Text = string.Join(
            "  ·  ",
            agent.EquippedTreasures.Select(item =>
                $"{FormatTreasureSlot(item.Slot)} {(item.Quality is { } quality ? QualityRules.Format(quality) : "미장착")}"));
        MaterialDetailText.Text = agent.Materials.Count == 0
            ? "보유 재료 없음"
            : string.Join(
                "  ·  ",
                agent.Materials.Select(item =>
                    $"{EconomyRules.GetMaterial(item.Material).KoreanName} {KoreanNumberFormatter.Format(item.Count)}"));
        SetPowerBreakdown(agent.CombatPower);
        if (AgentLogTabControl.SelectedIndex == 0)
        {
            PersonalLogList.ItemsSource = personalLog
                .Reverse()
                .Select(entry => FormatPersonalLog(agent, entry))
                .ToArray();
        }
        else
        {
            ChronicleList.ItemsSource = chronicle
                .Reverse()
                .Select(FormatChronicle)
                .ToArray();
        }
    }

    private void SetPowerBreakdown(CombatPowerBreakdown power)
    {
        var maximum = Math.Max(1, power.Total);
        TotalPowerText.Text = KoreanNumberFormatter.Format(power.Total);
        SetPowerRow(RealmPowerBar, RealmPowerText, power.Realm, maximum);
        SetPowerRow(AptitudePowerBar, AptitudePowerText, power.Aptitude, maximum);
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

    private RankingRow ToRankingRow(RankingEntry entry) => new(
        entry.Rank,
        entry.AgentId,
        entry.Name,
        entry.HasEnteredCultivation
            ? FormatRealm(entry.RealmStage, entry.RealmLayer, entry.GreatPerfectionAchieved)
            : "미입문 · 입문 수련",
        entry.Category switch
        {
            RankingCategory.CombatPower => KoreanNumberFormatter.Format(entry.CombatPower.Total),
            RankingCategory.Realm => KoreanNumberFormatter.Format(entry.Score % 1_000_000_000L),
            RankingCategory.Aptitude => (entry.AptitudeScore / 100m).ToString("0.##", CultureInfo.CurrentCulture),
            _ => entry.Score.ToString(CultureInfo.CurrentCulture),
        },
        FormatActivity(entry.Activity));

    private RankingRow ToRankingRow(AgentSnapshot agent) => new(
        agent.Rank,
        agent.Id,
        agent.Name,
        HasEnteredCultivation(agent.CultivationState, agent.BottleneckCause)
            ? FormatRealm(
                agent.RealmStage,
                agent.RealmLayer,
                agent.Cultivation.GreatPerfectionAchieved)
            : "미입문 · 입문 수련",
        KoreanNumberFormatter.Format(agent.CombatPower.Total),
        FormatActivity(agent.Activity));

    private EventRow FormatEvent(WorldSnapshot snapshot, WorldEvent worldEvent)
    {
        var primary = GetAgentName(snapshot, worldEvent.PrimaryAgent);
        var secondary = worldEvent.SecondaryAgent is { } secondaryAgent
            ? GetAgentName(snapshot, secondaryAgent)
            : string.Empty;
        var summary = worldEvent.Kind switch
        {
            WorldEventKind.BreakthroughSucceeded => $"{primary}, {FormatEncodedRealm(worldEvent.Value)} 돌파 성공",
            WorldEventKind.BreakthroughFailed => $"{primary}, 돌파 시도 실패 · 재준비",
            WorldEventKind.MinorStageBreakthrough => $"{primary}, {FormatEncodedRealm(worldEvent.Value)} 소경지 돌파",
            WorldEventKind.BottleneckBreakthrough => $"{primary}, {worldEvent.Value:N0}번의 시도 끝에 병목 돌파",
            WorldEventKind.MajorRealmBreakthrough => $"{primary}, {FormatEncodedRealm(worldEvent.Value)} 대경지 돌파",
            WorldEventKind.TechniqueEnlightenment => $"{primary}, 공법 깨달음 · 이해도 {worldEvent.Value / 100m:0.00}",
            WorldEventKind.SpellEnlightenment => $"{primary}, 법술 깨달음 · 이해도 {worldEvent.Value / 100m:0.00}",
            WorldEventKind.CultivationEntered => $"{primary}, 입문 3 Gate를 통과해 연기경에 입문",
            WorldEventKind.GreatPerfectionAchieved => $"{primary}, {FormatEncodedRealm(worldEvent.Value, greatPerfection: true)} 완성",
            WorldEventKind.Death => $"{primary}, {worldEvent.Value:N0}세에 수명을 다함",
            WorldEventKind.ReincarnationEntered => $"{primary}, 윤회의 강에 들어감",
            WorldEventKind.Rebirth => $"{primary}, 제 {worldEvent.Value:N0}생으로 환생",
            WorldEventKind.MissionCompleted => $"{primary}, 도시 임무 완료 · 영석 +{KoreanNumberFormatter.Format(worldEvent.Value)}",
            WorldEventKind.TreasureCrafted => $"{primary}, {FormatQuality(worldEvent.Value)} 법보 제작",
            WorldEventKind.PillCrafted => $"{primary}, {FormatPill((PillKind)worldEvent.Value)} 제작",
            WorldEventKind.PillPurchased => $"{primary}, 영석으로 {FormatPill((PillKind)worldEvent.Value)} 구매",
            WorldEventKind.MaterialSold => $"{primary}, {EconomyRules.GetMaterial((MaterialId)worldEvent.Value).KoreanName} 판매",
            WorldEventKind.MaterialPurchased => $"{primary}, 시장에서 {EconomyRules.GetMaterial((MaterialId)worldEvent.Value).KoreanName} 구매",
            WorldEventKind.TreasurePurchased => $"{primary}, 시장에서 {FormatQuality(worldEvent.Value)} 법보 구매",
            WorldEventKind.TechniqueBookPurchased => $"{primary}, 시장에서 {FormatQuality(worldEvent.Value)} 공법서 구매",
            WorldEventKind.SpellBookPurchased => $"{primary}, 시장에서 {FormatQuality(worldEvent.Value)} 법술서 구매",
            WorldEventKind.NewLeader => $"{primary}, {secondary}을 제치고 새로운 1위 등극",
            WorldEventKind.CombatStarted => $"{primary}, 상위권 {secondary}에게 도전",
            WorldEventKind.CombatPhysicalAttack => $"{primary} → {secondary}, 공법 공격 {worldEvent.Value:N0} 피해",
            WorldEventKind.CombatSpellCast => $"{primary} → {secondary}, 법술 {worldEvent.Value:N0} 피해",
            WorldEventKind.CombatGuarded => $"{primary}, 다음 공격에 대비해 방어",
            WorldEventKind.CombatRetreated => $"{primary}, {secondary}과의 전투에서 이탈",
            WorldEventKind.CombatEnded => $"{primary}, {secondary}에게 승리 · 경험 +{worldEvent.Value:N0}",
            WorldEventKind.CombatInjury => $"{primary}, 전투 중 {FormatInjurySeverity((InjurySeverity)worldEvent.Value)} 부상",
            WorldEventKind.CombatDeath => $"{primary}, {worldEvent.Value:N0}세에 전투 부상으로 사망",
            WorldEventKind.TravelCompleted => $"{primary}, {WorldMapRules.GetLocation(checked((ushort)worldEvent.Value)).KoreanName}에 도착",
            WorldEventKind.MonsterHuntWon => $"{primary}, {FormatMonster(worldEvent.Value)} 사냥 승리",
            WorldEventKind.MonsterHuntLost => $"{primary}, {FormatMonster(worldEvent.Value)} 사냥 패배",
            _ => worldEvent.Kind.ToString(),
        };
        var color = worldEvent.Kind switch
        {
            WorldEventKind.NewLeader
                or WorldEventKind.BreakthroughSucceeded
                or WorldEventKind.MinorStageBreakthrough
                or WorldEventKind.BottleneckBreakthrough
                or WorldEventKind.MajorRealmBreakthrough
                or WorldEventKind.Rebirth
                or WorldEventKind.TreasureCrafted
                or WorldEventKind.PillCrafted
                or WorldEventKind.TechniqueBookPurchased
                or WorldEventKind.SpellBookPurchased => (Brush)FindResource("AccentBrush"),
            WorldEventKind.CombatEnded or WorldEventKind.MonsterHuntWon => (Brush)FindResource("PositiveBrush"),
            WorldEventKind.BreakthroughFailed
                or WorldEventKind.CombatRetreated
                or WorldEventKind.CombatInjury
                or WorldEventKind.CombatDeath
                or WorldEventKind.MonsterHuntLost => (Brush)FindResource("DangerBrush"),
            _ => (Brush)FindResource("PrimaryTextBrush"),
        };
        return new EventRow(FormatGameDate(GameCalendar.GetDate(worldEvent.Tick)), summary, color);
    }

    private EventRow FormatChronicle(LifeChronicleEntry entry)
    {
        var summary = entry.Kind switch
        {
            ChronicleEventKind.Birth => $"{entry.PersonName}, 세상에 태어남",
            ChronicleEventKind.MinorStageBreakthrough => $"{FormatRealm(entry.RealmStage, (int)entry.MinorStage)} 소경지 돌파",
            ChronicleEventKind.BottleneckBreakthrough => $"{entry.Value:N0}번의 시도 끝에 병목을 돌파",
            ChronicleEventKind.MajorRealmBreakthrough => $"{FormatRealm(entry.RealmStage, (int)entry.MinorStage)} 대경지 돌파",
            ChronicleEventKind.BreakthroughFailure => "처음으로 현재 경지의 병목과 마주함",
            ChronicleEventKind.TechniqueEnlightenment => $"공법 깨달음으로 이해도 {entry.Value / 100m:0.00} 달성",
            ChronicleEventKind.SpellEnlightenment => $"법술 깨달음으로 이해도 {entry.Value / 100m:0.00} 달성",
            ChronicleEventKind.TechniqueAcquired => $"새 공법 획득 · {FormatQuality(entry.Value)}",
            ChronicleEventKind.SpellAcquired => $"새 법술 획득 · {FormatQuality(entry.Value)}",
            ChronicleEventKind.CultivationEntered => "입문 3 Gate를 통과해 연기경에 입문",
            ChronicleEventKind.GreatPerfectionAchieved => $"{FormatRealm(entry.RealmStage, (int)entry.MinorStage, greatPerfection: true)} 완성",
            ChronicleEventKind.FortuitousEncounter => "인생을 바꾸는 기연을 만남",
            ChronicleEventKind.Death => $"{entry.Value:N0}세에 수명을 다함",
            ChronicleEventKind.CombatDeath => $"{entry.Value:N0}세에 전투 부상으로 사망",
            ChronicleEventKind.ReincarnationEntered => "육신을 떠나 윤회의 강에 들어감",
            ChronicleEventKind.Rebirth => $"{entry.PersonName}, 새로운 육신으로 환생",
            _ => entry.Kind.ToString(),
        };
        var color = entry.Kind switch
        {
            ChronicleEventKind.Death or ChronicleEventKind.CombatDeath => (Brush)FindResource("DangerBrush"),
            ChronicleEventKind.Birth or ChronicleEventKind.Rebirth => (Brush)FindResource("PositiveBrush"),
            ChronicleEventKind.MinorStageBreakthrough
                or ChronicleEventKind.BottleneckBreakthrough
                or ChronicleEventKind.MajorRealmBreakthrough
                or ChronicleEventKind.TechniqueEnlightenment
                or ChronicleEventKind.SpellEnlightenment
                or ChronicleEventKind.FortuitousEncounter => (Brush)FindResource("AccentBrush"),
            _ => (Brush)FindResource("PrimaryTextBrush"),
        };
        return new EventRow(
            $"제 {entry.LifeNumber:N0}생 · {entry.AgeYears:N0}세 · 세계 {FormatGameDate(GameCalendar.GetDate(entry.Tick))}",
            summary,
            color);
    }

    private EventRow FormatPersonalLog(AgentSnapshot agent, PersonalLogEntry entry)
    {
        var related = entry.RelatedAgent is { } relatedAgent
            ? GetAgentName(relatedAgent)
            : "";
        var summary = entry.Kind switch
        {
            PersonalLogKind.ActionCompleted when entry.Activity is { } activity =>
                $"{FormatActivity(activity)} 행동을 마침",
            PersonalLogKind.WorldEvent when entry.WorldEventKind is { } eventKind =>
                FormatPersonalWorldEvent(entry, eventKind, related),
            PersonalLogKind.Chronicle when entry.ChronicleEventKind is { } chronicleKind =>
                FormatPersonalChronicle(chronicleKind, entry.Value),
            _ => "기록을 확인할 수 없음",
        };
        var color = entry.Kind switch
        {
            PersonalLogKind.Chronicle => (Brush)FindResource("AccentBrush"),
            PersonalLogKind.WorldEvent when entry.WorldEventKind is
                WorldEventKind.CombatEnded or WorldEventKind.MissionCompleted =>
                entry.IsPrimaryActor
                    ? (Brush)FindResource("PositiveBrush")
                    : (Brush)FindResource("DangerBrush"),
            PersonalLogKind.WorldEvent when entry.WorldEventKind is
                WorldEventKind.BreakthroughFailed
                or WorldEventKind.CombatRetreated
                or WorldEventKind.CombatInjury
                or WorldEventKind.CombatDeath
                or WorldEventKind.MonsterHuntLost =>
                (Brush)FindResource("DangerBrush"),
            _ => (Brush)FindResource("PrimaryTextBrush"),
        };
        return new EventRow(
            $"제 {entry.LifeNumber:N0}생 · {FormatGameDate(GameCalendar.GetDate(entry.Tick))} · {agent.Id}",
            summary,
            color);
    }

    private static string FormatPersonalWorldEvent(
        PersonalLogEntry entry,
        WorldEventKind kind,
        string related) => kind switch
    {
        WorldEventKind.NewLeader => entry.IsPrimaryActor ? "새로운 전투력 1위에 오름" : "전투력 1위 자리를 내어줌",
        WorldEventKind.CombatStarted => entry.IsPrimaryActor ? $"{related}에게 도전" : $"{related}의 도전을 받음",
        WorldEventKind.CombatPhysicalAttack => entry.IsPrimaryActor
            ? $"{related}에게 공법 공격 · 피해 {entry.Value:N0}"
            : $"{related}의 공법 공격을 받음 · 피해 {entry.Value:N0}",
        WorldEventKind.CombatSpellCast => entry.IsPrimaryActor
            ? $"{related}에게 법술 사용 · 피해 {entry.Value:N0}"
            : $"{related}의 법술을 받음 · 피해 {entry.Value:N0}",
        WorldEventKind.CombatGuarded => entry.IsPrimaryActor ? "다음 공격에 대비해 방어" : $"{related}이 방어 태세를 취함",
        WorldEventKind.CombatRetreated => entry.IsPrimaryActor ? $"{related}과의 전투에서 이탈" : $"{related}이 전투에서 이탈",
        WorldEventKind.CombatEnded => entry.IsPrimaryActor
            ? $"{related}에게 승리 · 경험 +{entry.Value:N0}"
            : $"{related}에게 패배",
        WorldEventKind.CombatInjury => $"{FormatInjurySeverity((InjurySeverity)entry.Value)} 부상",
        WorldEventKind.CombatDeath => $"{entry.Value:N0}세에 전투 부상으로 사망",
        WorldEventKind.TravelCompleted => $"{WorldMapRules.GetLocation(checked((ushort)entry.Value)).KoreanName}에 도착",
        WorldEventKind.MonsterHuntWon => $"{FormatMonster(entry.Value)} 사냥 승리",
        WorldEventKind.MonsterHuntLost => $"{FormatMonster(entry.Value)} 사냥 패배",
        WorldEventKind.MissionCompleted => $"도시 임무 완료 · 영석 +{KoreanNumberFormatter.Format(entry.Value)}",
        WorldEventKind.TreasureCrafted => $"{FormatQuality(entry.Value)} 법보 제작",
        WorldEventKind.PillCrafted => $"{FormatPill((PillKind)entry.Value)} 제작",
        WorldEventKind.PillPurchased => $"{FormatPill((PillKind)entry.Value)} 구매",
        WorldEventKind.MaterialSold => $"{EconomyRules.GetMaterial((MaterialId)entry.Value).KoreanName} 판매",
        WorldEventKind.MaterialPurchased => $"{EconomyRules.GetMaterial((MaterialId)entry.Value).KoreanName} 구매",
        WorldEventKind.TreasurePurchased => $"{FormatQuality(entry.Value)} 법보 구매",
        WorldEventKind.TechniqueBookPurchased => $"{FormatQuality(entry.Value)} 공법서 구매",
        WorldEventKind.SpellBookPurchased => $"{FormatQuality(entry.Value)} 법술서 구매",
        _ => kind.ToString(),
    };

    private static string FormatPersonalChronicle(ChronicleEventKind kind, long value) => kind switch
    {
        ChronicleEventKind.Birth => "세상에 태어남",
        ChronicleEventKind.MinorStageBreakthrough => "소경지 돌파",
        ChronicleEventKind.BottleneckBreakthrough => $"{value:N0}번의 실패 끝에 병목 돌파",
        ChronicleEventKind.MajorRealmBreakthrough => "대경지 돌파",
        ChronicleEventKind.BreakthroughFailure => "처음으로 현재 경지의 병목과 마주함",
        ChronicleEventKind.TechniqueEnlightenment => $"공법 {FormatQuality(value)}의 깨달음",
        ChronicleEventKind.SpellEnlightenment => $"법술 {FormatQuality(value)}의 깨달음",
        ChronicleEventKind.FortuitousEncounter => "인생을 바꾸는 기연",
        ChronicleEventKind.Death => $"{value:N0}세에 수명을 다함",
        ChronicleEventKind.CombatDeath => $"{value:N0}세에 전투 부상으로 사망",
        ChronicleEventKind.ReincarnationEntered => "윤회의 강에 들어감",
        ChronicleEventKind.Rebirth => "새로운 육신으로 환생",
        _ => kind.ToString(),
    };

    private static string FormatGameDate(GameDate date) =>
        $"{KoreanNumberFormatter.Format(date.Year)}년 {date.Month:N0}월 {date.Day:N0}일";

    private static string FormatOptionalGameDate(GameDate? date) =>
        date is { } value ? FormatGameDate(value) : "아직 없음";

    private static string FormatPriceChange(int basePrice, int currentPrice)
    {
        if (basePrice <= 0)
        {
            return "-";
        }

        var change = (currentPrice - basePrice) * 100m / basePrice;
        return change switch
        {
            > 0 => $"+{change:0.##}%",
            < 0 => $"{change:0.##}%",
            _ => "0%",
        };
    }

    private static string FormatCultivationState(
        CultivationState state,
        CultivationBottleneckCause cause) => state switch
    {
        CultivationState.PreCultivation => "선천 수행 준비기 · 5세부터 입문 수련",
        CultivationState.EntryTraining => "입문 수련 · Work와 3개 Gate를 함께 검증",
        CultivationState.RealmCultivation => "현재 층의 수행 Work를 축적하는 중",
        CultivationState.PreparingBreakthrough => "돌파 준비 중 · 다음 월에 Preflight 검증",
        CultivationState.PreflightReady => "돌파 Preflight 준비 완료 · 다음 월에 시도",
        CultivationState.AttemptingBreakthrough => "돌파 시도 중",
        CultivationState.Bottleneck => $"병목 · 원인: {FormatBottleneckCause(cause)}",
        CultivationState.Stabilizing => "돌파 성공 · 새 경지 안착 중",
        CultivationState.Stable => "현재 전이 완료 · 다음 수행 목표 정리 중",
        CultivationState.Completed => "반선경 대원만 · 현재 수행체계 완성",
        _ => state.ToString(),
    };

    private static string FormatBottleneckCause(CultivationBottleneckCause cause) => cause switch
    {
        CultivationBottleneckCause.Knowledge => "공법·법술 이해",
        CultivationBottleneckCause.Body => "육신 기반",
        CultivationBottleneckCause.SpiritualRoot => "영근·영력 운용",
        CultivationBottleneckCause.Resources => "돌파 자원",
        CultivationBottleneckCause.Stability => "정신·영혼 안정성",
        CultivationBottleneckCause.EntryGate => "입문 3개 Gate",
        CultivationBottleneckCause.RealmComponent => "경지 구성요소",
        CultivationBottleneckCause.Method => "돌파 공법 지식·숙련",
        _ => "미확인",
    };

    private static bool HasEnteredCultivation(
        CultivationState state,
        CultivationBottleneckCause cause) =>
        state is not (CultivationState.PreCultivation or CultivationState.EntryTraining)
        && !(state == CultivationState.Bottleneck && cause == CultivationBottleneckCause.EntryGate);

    private static string FormatFoundationRoute(FoundationRoute route) => route switch
    {
        FoundationRoute.None => "미정",
        FoundationRoute.Human => "인도축기",
        FoundationRoute.Earth => "지도축기",
        FoundationRoute.Heaven => "천도축기",
        _ => route.ToString(),
    };

    private string GetAgentName(AgentId agentId) =>
        _latestSnapshot is null ? agentId.ToString() : GetAgentName(_latestSnapshot, agentId);

    private static string GetAgentName(WorldSnapshot snapshot, AgentId agentId) =>
        snapshot.AgentNames.TryGetValue(agentId.Value, out var name) ? name : agentId.ToString();

    private static string FormatEncodedRealm(long encodedRealm, bool greatPerfection = false)
    {
        var stage = (RealmStage)(encodedRealm / 10);
        var layer = checked((int)(encodedRealm % 10));
        return FormatRealm(stage, layer, greatPerfection);
    }

    private static string FormatQuality(long hundredths) =>
        QualityRules.Format(new QualityValue(checked((ushort)hundredths)));

    private static string FormatRealm(
        RealmStage stage,
        int layer,
        bool greatPerfection = false)
    {
        var boundedLayer = Math.Clamp(layer, 1, RealmRules.MinorStageCount);
        var minorStage = greatPerfection && boundedLayer == RealmRules.MinorStageCount
            ? "대원만"
            : boundedLayer switch
            {
                <= 3 => "초기",
                <= 6 => "중기",
                _ => "후기",
            };
        return $"{FormatRealmStage(stage)} · {minorStage} · {boundedLayer.ToString(CultureInfo.InvariantCulture)}층";
    }

    private static string FormatRealmStage(RealmStage stage) => stage switch
    {
        RealmStage.QiRefining => "연기경",
        RealmStage.FoundationEstablishment => "축기경",
        RealmStage.GoldenCore => "금단경",
        RealmStage.NascentSoul => "원영경",
        RealmStage.SpiritTransformation => "화신경",
        RealmStage.VoidRefinement => "연허경",
        RealmStage.BodyIntegration => "합체경",
        RealmStage.TribulationCrossing => "도겁경",
        RealmStage.Mahayana => "대승경",
        RealmStage.HalfImmortal => "반선경",
        _ => stage.ToString(),
    };

    private static string FormatPopulationRatio(int count, int population) =>
        population == 0 ? "0%" : $"{count * 100m / population:0.##}%";

    private static string FormatPowerRange(long minimum, long? maximum)
    {
        if (maximum is null)
        {
            return $"{KoreanNumberFormatter.Format(minimum)} 이상";
        }

        if (minimum == 0)
        {
            return $"{KoreanNumberFormatter.Format(maximum.Value)} 미만";
        }

        return $"{KoreanNumberFormatter.Format(minimum)}~{KoreanNumberFormatter.Format(maximum.Value)}";
    }

    private static string FormatActivity(AgentActivity activity) => activity switch
    {
        AgentActivity.Cultivating => "수행",
        AgentActivity.TrainingTechnique => "공법 수련",
        AgentActivity.TrainingSpell => "법술 연마",
        AgentActivity.Recovering => "회복",
        AgentActivity.SeekingChallenge => "도전 탐색",
        AgentActivity.Fighting => "전투 중",
        AgentActivity.Reincarnating => "윤회 중",
        AgentActivity.DoingCityMission => "도시 임무",
        AgentActivity.TradingInCity => "도시 거래",
        AgentActivity.CraftingTreasure => "법보 제작",
        AgentActivity.CraftingPill => "단약 제작",
        AgentActivity.Traveling => "지역 이동",
        AgentActivity.HuntingMonster => "요수 사냥",
        _ => activity.ToString(),
    };

    private static string FormatPill(PillKind kind) => kind switch
    {
        PillKind.Growth => "수행단",
        PillKind.Breakthrough => "돌파단",
        _ => kind.ToString(),
    };

    private static string FormatTreasureSlot(TreasureSlot slot) => slot switch
    {
        TreasureSlot.NatalTreasure => "본명법보",
        TreasureSlot.ArmorOne => "방어구 I",
        TreasureSlot.ArmorTwo => "방어구 II",
        TreasureSlot.ArmorThree => "방어구 III",
        TreasureSlot.WeaponOne => "무기 I",
        TreasureSlot.WeaponTwo => "무기 II",
        TreasureSlot.WeaponThree => "무기 III",
        _ => slot.ToString(),
    };

    private static string FormatMarketGood(MarketGoodSnapshot item) => item.Kind switch
    {
        MarketGoodKind.Material when item.Material is { } material =>
            EconomyRules.GetMaterial(material).KoreanName,
        MarketGoodKind.GrowthPill => "수행단",
        MarketGoodKind.BreakthroughPill => "돌파단",
        MarketGoodKind.Treasure => "도시 법보",
        MarketGoodKind.TechniqueBook => "공법서",
        MarketGoodKind.SpellBook => "법술서",
        _ => item.Kind.ToString(),
    };

    private void UpdateMarketDetail(MarketGoodSnapshot item)
    {
        MarketDetailNameText.Text = FormatMarketGood(item);
        var (type, description, production) = item.Kind switch
        {
            MarketGoodKind.Material when item.Material is { } material => FormatMaterialDetail(material),
            MarketGoodKind.GrowthPill => (
                "단약 · 성장",
                "복용한 수선자의 수행 Work 축적을 돕는 성장용 단약입니다.",
                "도시는 단약 제작용 재료 2개를 가공해 수행단 또는 돌파단 1개를 생산합니다."),
            MarketGoodKind.BreakthroughPill => (
                "단약 · 돌파",
                "대경지 돌파 준비와 자원 병목 해소에 사용하는 돌파용 단약입니다.",
                "도시는 단약 제작용 재료 2개를 가공하며, 두 단약의 재고가 균형을 이루도록 생산합니다."),
            MarketGoodKind.Treasure => (
                $"법보 · 평균 품질 {(item.AverageQuality is { } quality ? QualityRules.Format(quality) : "재고 없음")}",
                "무기 3칸·방어구 3칸·본명법보 1칸 중 약한 장비를 교체하며 실제 공격·방어 속성에 반영됩니다.",
                "도시는 법보 제작용 재료 2개를 가공해 표준 법보를 만들고, 수선자도 제작품을 시장에 판매합니다."),
            MarketGoodKind.TechniqueBook => (
                "서적 · 공법",
                "0.00~100.00 품질의 새 공법서입니다. 현재 공법보다 품질이 높을 때만 교체되며 기존 공법 자체의 품질은 변하지 않습니다.",
                $"도시는 영석 {KoreanNumberFormatter.Format(EconomyRules.TechniqueBookProductionCost)}개를 사용해 공법서 1권을 제작합니다."),
            MarketGoodKind.SpellBook => (
                "서적 · 법술",
                "0.00~100.00 품질의 새 법술서입니다. 현재 법술보다 품질이 높을 때만 교체되며 기존 법술 자체의 품질은 변하지 않습니다.",
                $"도시는 영석 {KoreanNumberFormatter.Format(EconomyRules.SpellBookProductionCost)}개를 사용해 법술서 1권을 제작합니다."),
            _ => ("상품", "상세 정보가 아직 등록되지 않았습니다.", "생산 정보가 없습니다."),
        };
        MarketDetailTypeText.Text = type;
        MarketDetailDescriptionText.Text = description;
        MarketDetailProductionText.Text = production;
        MarketDetailEconomyText.Text =
            $"기준 {KoreanNumberFormatter.Format(item.CityBasePrice)} · 현재 {KoreanNumberFormatter.Format(item.CurrentPrice)} 영석 · 재고 {KoreanNumberFormatter.Format(item.Stock)} · 공급 {KoreanNumberFormatter.Format(item.Supply)} · 수요 {KoreanNumberFormatter.Format(item.Demand)}";
    }

    private static (string Type, string Description, string Production) FormatMaterialDetail(
        MaterialId material)
    {
        var definition = EconomyRules.GetMaterial(material);
        var (category, usage) = definition.Category switch
        {
            MaterialCategory.Treasure => ("법보 제작 재료", "법보 제작과 도시 법보 생산에 사용됩니다."),
            MaterialCategory.Pill => ("단약 제작 재료", "수행단·돌파단 제조와 도시 단약 생산에 사용됩니다."),
            MaterialCategory.Miscellaneous => ("기타 재료", "도시 임무 납품·수선자 간 거래·후속 제작 확장에 사용됩니다."),
            _ => ("재료", "용도가 미확정입니다."),
        };
        return (
            $"재료 · {category}",
            $"{definition.KoreanName}. {usage}",
            "수선자가 임무·요수 사냥으로 획득해 납품하거나 판매하면 도시의 실물 재고와 공급량이 증가합니다.");
    }

    private static string FormatBiome(BiomeType biome) => biome switch
    {
        BiomeType.City => "도시",
        BiomeType.Forest => "숲",
        BiomeType.Plains => "평야",
        BiomeType.Mountains => "산맥",
        _ => biome.ToString(),
    };

    private static string FormatInjurySeverity(InjurySeverity severity) => severity switch
    {
        InjurySeverity.None => "없음",
        InjurySeverity.Light => "경상",
        InjurySeverity.Moderate => "중등상",
        InjurySeverity.Severe => "중상",
        InjurySeverity.Critical => "치명상",
        InjurySeverity.Fatal => "사망상",
        _ => severity.ToString(),
    };

    private static string FormatMonster(long monsterId)
    {
        var index = checked((int)monsterId);
        return (uint)index < (uint)WorldMapRules.AllMonsters.Count
            ? WorldMapRules.AllMonsters[index].KoreanName
            : $"미확인 요수 #{index}";
    }

    private static string FormatMonsterList(MapLocationSnapshot location) =>
        location.Monsters.Count == 0
            ? "안전지대"
            : string.Join(" · ", location.Monsters.Select(monster => monster.KoreanName));

    private Brush GetBiomeBrush(BiomeType biome) => (Brush)FindResource(biome switch
    {
        BiomeType.City => "CityBiomeBrush",
        BiomeType.Forest => "ForestBiomeBrush",
        BiomeType.Plains => "PlainsBiomeBrush",
        BiomeType.Mountains => "MountainBiomeBrush",
        _ => "PanelAltBrush",
    });

    private void MarketGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applyingObservation
            || _latestSnapshot is null
            || MarketGrid.SelectedItem is not MarketRow row)
        {
            return;
        }

        _selectedMarketGoodId = row.GoodId;
        if (MarketDetailExpander.IsExpanded)
        {
            UpdateMarketDetail(_latestSnapshot.Market.Goods[row.GoodId]);
        }
    }

    private void RankingGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applyingObservation
            || _runner is null
            || RankingGrid.SelectedItem is not RankingRow row)
        {
            return;
        }

        RefreshVisibleObservation(row.AgentId);
    }

    private void VisibleSection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applyingObservation || !ReferenceEquals(e.Source, sender))
        {
            return;
        }

        RefreshVisibleObservation();
    }

    private void VisibleExpander_Expanded(object sender, RoutedEventArgs e)
    {
        if (!_applyingObservation)
        {
            RefreshVisibleObservation();
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_rankingCategory is null && _latestSnapshot is not null)
        {
            _explorerOffset = 0;
            UpdateRankingRows(_latestSnapshot);
        }
    }

    private void CombatPowerRankingButton_Click(object sender, RoutedEventArgs e) =>
        SetListMode(RankingCategory.CombatPower);

    private void RealmRankingButton_Click(object sender, RoutedEventArgs e) =>
        SetListMode(RankingCategory.Realm);

    private void AptitudeRankingButton_Click(object sender, RoutedEventArgs e) =>
        SetListMode(RankingCategory.Aptitude);

    private void ExplorerButton_Click(object sender, RoutedEventArgs e) =>
        SetListMode(null);

    private void ExplorerPreviousButton_Click(object sender, RoutedEventArgs e)
    {
        _explorerOffset = Math.Max(0, _explorerOffset - ExplorerPageSize);
        if (_latestSnapshot is not null)
        {
            UpdateRankingRows(_latestSnapshot);
        }
    }

    private void ExplorerNextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_latestSnapshot is null
            || _explorerOffset + ExplorerPageSize >= _latestSnapshot.Population)
        {
            return;
        }

        _explorerOffset += ExplorerPageSize;
        UpdateRankingRows(_latestSnapshot);
    }

    private void SetListMode(RankingCategory? category)
    {
        _rankingCategory = category;
        _explorerOffset = 0;
        var explorer = category is null;
        SearchTextBox.IsEnabled = explorer;
        if (!explorer && SearchTextBox.Text.Length > 0)
        {
            SearchTextBox.Clear();
        }

        RankingTitleText.Text = category switch
        {
            RankingCategory.CombatPower => "전투력 랭킹",
            RankingCategory.Realm => "경지 랭킹",
            RankingCategory.Aptitude => "자질 랭킹",
            null => "전체 수선자 탐색",
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };
        RankingGrid.Columns[3].Header = category switch
        {
            RankingCategory.CombatPower => "전투력",
            RankingCategory.Realm => "수행량",
            RankingCategory.Aptitude => "자질 합계",
            null => "전투력",
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };

        RefreshVisibleObservation();
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_runner is null)
        {
            return;
        }

        var paused = !_runner.IsPaused;
        _runner.SetPaused(paused);
        PauseButton.Content = paused ? "계속 진행" : "일시정지";
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_runner is null)
        {
            return;
        }

        try
        {
            StatusText.Text = "세계 저장 중…";
            var checkpoint = _runner.CreateCheckpoint();
            await Task.Run(() => WorldSaveStore.Save(WorldSaveStore.DefaultPath, checkpoint));
            StatusText.Text = $"저장 완료 · {WorldSaveStore.DefaultPath}";
            StatusText.Foreground = (Brush)FindResource("PositiveBrush");
        }
        catch (IOException exception)
        {
            ShowPersistenceError(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowPersistenceError(exception);
        }
        catch (InvalidOperationException exception)
        {
            ShowPersistenceError(exception);
        }
        catch (InvalidDataException exception)
        {
            ShowPersistenceError(exception);
        }
    }

    private async void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_runner is null)
        {
            return;
        }

        try
        {
            StatusText.Text = "세계 불러오는 중…";
            var checkpoint = await Task.Run(() => WorldSaveStore.Load(WorldSaveStore.DefaultPath));
            _runner.RestoreCheckpoint(checkpoint);
            _selectedAgent = null;
            RefreshVisibleObservation();
            StatusText.Text = $"불러오기 완료 · {WorldSaveStore.DefaultPath}";
            StatusText.Foreground = (Brush)FindResource("PositiveBrush");
        }
        catch (FileNotFoundException exception)
        {
            ShowPersistenceError(exception);
        }
        catch (InvalidDataException exception)
        {
            ShowPersistenceError(exception);
        }
        catch (IOException exception)
        {
            ShowPersistenceError(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowPersistenceError(exception);
        }
    }

    private void ShowPersistenceError(Exception exception)
    {
        StatusText.Text = $"저장 오류: {exception.Message}";
        StatusText.Foreground = (Brush)FindResource("DangerBrush");
    }

    private void Speed1Button_Click(object sender, RoutedEventArgs e) => SetSpeed(1);

    private void Speed10Button_Click(object sender, RoutedEventArgs e) => SetSpeed(10);

    private void Speed25Button_Click(object sender, RoutedEventArgs e) => SetSpeed(25);

    private void Speed50Button_Click(object sender, RoutedEventArgs e) => SetSpeed(50);

    private void Speed100Button_Click(object sender, RoutedEventArgs e) => SetSpeed(100);

    private void SetSpeed(int speed)
    {
        _speed = speed;
        _runner?.SetSpeed(speed);
    }
}
