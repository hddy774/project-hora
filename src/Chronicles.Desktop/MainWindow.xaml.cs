using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Chronicles.Core;
using Chronicles.Application;
using Chronicles.Runtime;
using Chronicles.Simulation;
using static Chronicles.Presentation.PresentationText;

namespace Chronicles.Desktop;

public partial class MainWindow : Window, IDisposable
{
    private const ulong DefaultWorldSeed = 20260830UL;
    private const int ExplorerPageSize = 500;
    private const int WorldChronicleDisplayLimit = 1_000;
    private GameApplicationSession? _runner;
    private ulong _worldSeed = DefaultWorldSeed;
    private WorldBalanceConfiguration _activeBalance = WorldBalanceConfiguration.Default;
    private WorldSnapshot? _latestSnapshot;
    private AgentId? _selectedAgent;
    private RankingCategory? _rankingCategory = RankingCategory.CombatPower;
    private RankingMovementPeriod _rankingMovementPeriod = RankingMovementPeriod.OneYear;
    private RankingCategory _rankingMovementCategory = RankingCategory.CombatPower;
    private int _explorerOffset;
    private int _personalLogCategory;
    private int _chronicleCategory;
    private int _personalCenturyCategory;
    private int? _selectedChronicleLife;
    private int? _selectedPersonalCenturyLife;
    private AgentSnapshot? _latestAgent;
    private IReadOnlyList<PersonalLogEntry> _latestPersonalLog = Array.Empty<PersonalLogEntry>();
    private IReadOnlyList<LifeChronicleEntry> _latestChronicle = Array.Empty<LifeChronicleEntry>();
    private IReadOnlyList<PersonalCenturyChronicleEntry> _latestPersonalCenturyChronicle =
        Array.Empty<PersonalCenturyChronicleEntry>();
    private ReincarnationAgentSnapshot? _latestReincarnation;
    private AgentId? _projectedReincarnationAgent;
    private long _projectedReincarnationRevision = long.MinValue;
    private int _speed = 1;
    private bool _disposed;
    private bool _applyingObservation;
    private int _observationDispatchQueued;
    private long _projectedRecentEventSequence = long.MinValue;
    private int _projectedRecentEventCount = -1;
    private WorldLogCategory? _projectedWorldLogCategory;
    private long _projectedWorldChronicleSequence = long.MinValue;
    private int _projectedWorldChronicleCount = -1;
    private WorldChronicleCategory? _projectedWorldChronicleCategory;
    private long _projectedDebugVersion = long.MinValue;
    private readonly GameLogService _logService = new();

    public MainWindow()
    {
        InitializeComponent();
        ConfigureWorkspaceLayout(WorkspaceRoute.World, isAvailable: true);
        Title = $"{ProductInfo.KoreanTitle} PC v{ProductInfo.Version}";
        VersionText.Text = $"VERSION {ProductInfo.Version}";
        _runner = GameApplicationSession.CreateDefault(DefaultWorldSeed);
        SettingsWorkspacePanel.SetCurrentWorldSettings(
            DefaultWorldSeed,
            _runner.GetWorldMetadata().Population,
            WorldBalanceConfiguration.Default);
        _runner.ObservationInvalidated += Runner_ObservationInvalidated;
        _runner.Faulted += Runner_Faulted;
        _logService.FileWriteFailed += LogService_FileWriteFailed;
        _logService.AddDebug(DebugLogLevel.Information, $"게임 초기화 · VERSION {ProductInfo.Version}");
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _logService.AddDebug(DebugLogLevel.Information, "시뮬레이션 실행 시작");
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
        _logService.AddDebug(DebugLogLevel.Information, "게임 종료");
        if (_runner is null)
        {
            _logService.Dispose();
            return;
        }

        _runner.ObservationInvalidated -= Runner_ObservationInvalidated;
        _runner.Faulted -= Runner_Faulted;
        _runner.Dispose();
        _runner = null;
        _logService.FileWriteFailed -= LogService_FileWriteFailed;
        _logService.Dispose();
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
            _logService.AddDebug(DebugLogLevel.Error, $"시뮬레이션 오류 · {exception}");
            StatusText.Text = $"시뮬레이션 오류: {exception.Message}";
            StatusText.Foreground = (Brush)FindResource("DangerBrush");
        }));
    }

    private void LogService_FileWriteFailed(Exception exception)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            WorldRecordsPanel.SetOutputStatus($"파일 오류 · {exception.Message}", isError: true);
            SettingsWorkspacePanel.SetPerformanceFileStatus(
                $"파일 오류 · {exception.Message}",
                isError: true);
            SetPersonalRecordOutputStatus($"파일 오류 · {exception.Message}", isError: true);
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

        if (!IsDashboardWorkspaceVisible)
        {
            var shellSnapshot = _runner.GetSnapshot(
                rankingCount: 1,
                eventCount: 0,
                sections: ObservationSections.None,
                worldChronicleCount: 0);
            ApplyShellSnapshot(shellSnapshot);
            return;
        }

        var observation = _runner.GetObservation(
            _selectedAgent,
            sections: GetVisibleObservationSections(),
            detailedMapLocationId: _workspaceRoute == WorkspaceRoute.Map
                && MapObservationPanel.ShouldLoadAgents
                    ? MapObservationPanel.SelectedLocationId
                    : null,
            detailedMapAgentOffset: _workspaceRoute == WorkspaceRoute.Map
                && MapObservationPanel.ShouldLoadAgents
                    ? MapObservationPanel.AgentOffset
                    : 0,
            worldChronicleCount: WorldChronicleDisplayLimit);
        ApplyObservation(observation);
    }

    private ObservationSections GetVisibleObservationSections()
    {
        if (!IsDashboardWorkspaceVisible)
        {
            return ObservationSections.None;
        }

        var sections = _workspaceRoute switch
        {
            WorkspaceRoute.World => ObservationSections.StatisticsSummary,
            WorkspaceRoute.City => ObservationSections.Market,
            WorkspaceRoute.Economy => ObservationSections.Market,
            WorkspaceRoute.Map => ObservationSections.Map,
            WorkspaceRoute.Records or WorkspaceRoute.Diagnostics =>
                WorldRecordsPanel.SelectedRecordIndex switch
            {
                0 => ObservationSections.WorldLog,
                1 => ObservationSections.WorldChronicle,
                _ => ObservationSections.None,
            },
            _ => ObservationSections.None,
        };
        if (_workspaceRoute == WorkspaceRoute.World)
        {
            if (StatisticsDetailTabControl.SelectedIndex == 2
                && StatisticsDistributionTabControl.SelectedIndex == 0)
            {
                sections |= ObservationSections.RealmDistribution;
            }

            if (StatisticsDetailTabControl.SelectedIndex == 2
                && StatisticsDistributionTabControl.SelectedIndex == 1)
            {
                sections |= ObservationSections.CombatPowerDistribution;
            }
        }

        if (_workspaceRoute is not (WorkspaceRoute.People or WorkspaceRoute.Relations))
        {
            return sections;
        }

        if (_agentDetailSection == AgentDetailSection.Records)
        {
            sections |= _logService.AutoPersonalOutput
                ? ObservationSections.PersonalLog
                    | ObservationSections.Chronicle
                    | ObservationSections.PersonalCenturyChronicle
                : AgentLogTabControl.SelectedIndex switch
            {
                0 => ObservationSections.PersonalLog,
                1 => ObservationSections.Chronicle,
                2 => ObservationSections.PersonalCenturyChronicle,
                3 => ObservationSections.Reincarnation,
                4 => ObservationSections.PersonalLog
                    | ObservationSections.Chronicle
                    | ObservationSections.PersonalCenturyChronicle,
                _ => ObservationSections.None,
            };
        }
        if (_agentDetailSection == AgentDetailSection.Cultivation
            && AptitudeDetailsExpander.IsExpanded)
        {
            sections |= ObservationSections.AptitudeDetails;
        }
        if (_agentDetailSection == AgentDetailSection.Social
            && RelationsExpander.IsExpanded)
        {
            sections |= ObservationSections.Relations;
        }
        if (_agentDetailSection == AgentDetailSection.Social
            && TeachingExpander.IsExpanded)
        {
            sections |= ObservationSections.Teaching;
        }
        if (_agentDetailSection == AgentDetailSection.Social
            && CausalRuntimeExpander.IsExpanded)
        {
            sections |= ObservationSections.CausalRuntime;
        }

        sections |= _rankingCategory switch
        {
            RankingCategory.CombatPower => ObservationSections.CombatPowerRanking,
            RankingCategory.Realm => ObservationSections.RealmRanking,
            RankingCategory.Aptitude => ObservationSections.AptitudeRanking,
            RankingCategory.Wealth => ObservationSections.WealthRanking,
            null => ObservationSections.None,
            _ => ObservationSections.None,
        };
        return sections;
    }

    private void ApplyObservation(WorldObservation observation)
    {
        _applyingObservation = true;
        var snapshot = observation.World;
        _selectedAgent = observation.SelectedAgent.Id;
        ApplyShellSnapshot(snapshot);
        try
        {
            if (_workspaceRoute is WorkspaceRoute.People or WorkspaceRoute.Relations)
            {
                UpdateRankingRows(snapshot);
                UpdateSelectedAgent(
                    observation.SelectedAgent,
                    observation.SelectedPersonalLog,
                    observation.SelectedChronicle,
                    observation.SelectedPersonalCenturyChronicle,
                    observation.SelectedReincarnation);
            }
            else
            {
                UpdateVisibleWorldPanel(snapshot);
            }
        }
        finally
        {
            _applyingObservation = false;
        }
    }

    private void ApplyShellSnapshot(WorldSnapshot snapshot)
    {
        _latestSnapshot = snapshot;
        if (_runner is { } runner)
        {
            _logService.CaptureWorldFeed(
                runner.DrainWorldEventFeed(),
                FormatWorldEventNotification);
            _logService.CaptureWorldChronicleFeed(
                runner.DrainWorldCenturyChronicleFeed(),
                FormatWorldChronicleFileLine);
        }
        WorldTickText.Text = FormatGameDate(GameCalendar.GetDate(snapshot.Tick));
        PopulationText.Text = KoreanNumberFormatter.Format(snapshot.Population);
        SubtitleText.Text = $"만상경 · {snapshot.Population:N0}명의 자율 경쟁 관찰";
        ActiveCombatText.Text = KoreanNumberFormatter.Format(snapshot.ActiveCombats);
        CompletedCombatText.Text = KoreanNumberFormatter.Format(snapshot.TotalCombatsCompleted);
        ChallengeText.Text = KoreanNumberFormatter.Format(snapshot.TotalChallenges);
        BreakthroughText.Text = KoreanNumberFormatter.Format(snapshot.TotalBreakthroughs);
        StatusText.Text = _runner?.IsPaused == true
            ? $"일시정지 · {_speed}× · seed {_worldSeed}"
            : $"실시간 진행 · {_speed}× · 게임 1년당 현실 {GameCalendar.GetRealSecondsPerGameYear(_speed):0.###}초";
        if (_activeBalance.DeveloperModeEnabled)
        {
            StatusText.Text += " · 세계 설정 배율";
        }
        StatusText.Foreground = (Brush)FindResource("SecondaryTextBrush");
        if (_workspaceRoute == WorkspaceRoute.Settings && _runner is { } settingsRunner)
        {
            SettingsWorkspacePanel.UpdatePerformance(settingsRunner.GetPerformanceSnapshot());
        }
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
                MarketObservationPanel.Apply(snapshot.Market);
                break;
            case 3:
                MapObservationPanel.Apply(snapshot.Map);
                break;
            case 4:
                UpdateWorldRecordsPanel(snapshot);
                break;
        }
    }

    private void UpdateStatisticsPanel(WorldSnapshot snapshot)
    {
        var statistics = snapshot.Statistics;
        if (StatisticsDetailTabControl.SelectedIndex == 0)
        {
            DeathStatText.Text = KoreanNumberFormatter.Format(statistics.TotalDeaths);
            WinStatText.Text = KoreanNumberFormatter.Format(statistics.TotalWins);
            LossStatText.Text = KoreanNumberFormatter.Format(statistics.TotalLosses);
            ReincarnationStatText.Text = KoreanNumberFormatter.Format(statistics.TotalReincarnations);
            MissionStatText.Text = KoreanNumberFormatter.Format(statistics.TotalMissionsCompleted);
            SpiritStoneStatText.Text = KoreanNumberFormatter.Format(statistics.TotalSpiritStones);
            return;
        }

        if (StatisticsDetailTabControl.SelectedIndex == 1)
        {
            var movement = statistics.RankingMovement;
            var changeRate = snapshot.Population == 0
                ? 0m
                : movement.ChangedCount * 100m / snapshot.Population;
            var averageMovement = movement.ChangedCount == 0
                ? 0m
                : movement.TotalAbsoluteMovement / (decimal)movement.ChangedCount;
            RankingMovementText.Text =
                $"변동 {changeRate:0.##}% · 상승 {movement.RisenCount:N0} · 하락 {movement.FallenCount:N0} · 유지 {movement.UnchangedCount:N0} · 평균 {averageMovement:0.##}위";
            UpdateRankingMovementRows(snapshot);
            return;
        }

        if (StatisticsDistributionTabControl.SelectedIndex == 0)
        {
            RealmDistributionGrid.ItemsSource = statistics.RealmDistribution
                .Select(item => new DistributionRow(
                    item.Realm is { } realm ? FormatRealmStage(realm) : "미입문",
                    KoreanNumberFormatter.Format(item.Count),
                    FormatPopulationRatio(item.Count, snapshot.Population)))
                .ToArray();
        }

        else
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
        if (CityDetailTabControl.SelectedIndex == 0)
        {
            CityTreasuryText.Text = $"{KoreanNumberFormatter.Format(snapshot.Market.TreasurySpiritStones)} 영석";
            CityTransactionText.Text =
                $"구매 {KoreanNumberFormatter.Format(snapshot.Market.PurchaseTransactions)}건 · 판매 {KoreanNumberFormatter.Format(snapshot.Market.SaleTransactions)}건";
            CityFlowText.Text =
                $"공급 {KoreanNumberFormatter.Format(snapshot.Market.TotalSupply)} · 수요 {KoreanNumberFormatter.Format(snapshot.Market.TotalDemand)} · 미충족 {KoreanNumberFormatter.Format(snapshot.Market.UnmetDemand)}";
            return;
        }

        if (CityDetailTabControl.SelectedIndex == 1)
        {
            CityMissionText.Text =
                $"임무 납품 {KoreanNumberFormatter.Format(snapshot.Market.MissionDeliveries)}건 · 가공 재료 {KoreanNumberFormatter.Format(snapshot.Market.MaterialsProcessed)}개";
            CityProductionText.Text =
                $"단약 {KoreanNumberFormatter.Format(snapshot.Market.PillsProduced)} · 법보 {KoreanNumberFormatter.Format(snapshot.Market.TreasuresProduced)} · 공법서 {KoreanNumberFormatter.Format(snapshot.Market.TechniqueBooksProduced)} · 법술서 {KoreanNumberFormatter.Format(snapshot.Market.SpellBooksProduced)}";
            CityProductionQueueText.Text = snapshot.Market.ProductionQueue is { Count: > 0 } queue
                ? string.Join(
                    "\n",
                    queue.Select(item =>
                    {
                        var material = item.RequiredMaterial is { } requiredMaterial
                            ? $"{EconomyRules.GetMaterial(requiredMaterial).KoreanName} {item.MaterialCost:N0}"
                            : "재료 없음";
                        return $"{FormatMarketGood(EconomyRules.GetMarketGood(item.GoodId), item.Quality)} · " +
                            $"{item.CompletedMonths:N0}/{item.RequiredMonths:N0}개월 · {material} · " +
                            $"영석 {KoreanNumberFormatter.Format(item.SpiritStoneCost)}";
                    }))
                : "수요·재료·영석 조건을 모두 충족한 생산 대기열 없음";
            return;
        }

        CityMiningText.Text =
            $"누적 채굴 {KoreanNumberFormatter.Format(snapshot.Market.SpiritStonesMined)} · 남은 광맥 {KoreanNumberFormatter.Format(snapshot.Market.RemainingSpiritStoneOre)} · 채굴 임무 {KoreanNumberFormatter.Format(snapshot.Market.MiningMissionCount)}건";
        CityInventoryText.Text =
            $"총 {KoreanNumberFormatter.Format(snapshot.Market.TotalStock)}개 · 재료 {KoreanNumberFormatter.Format(snapshot.Market.MaterialStock)}개 · 가공품 {KoreanNumberFormatter.Format(snapshot.Market.ProcessedStock)}개";
    }

    private EventRow FormatChronicle(LifeChronicleEntry entry)
    {
        var summary = entry.Kind switch
        {
            ChronicleEventKind.Birth => $"{entry.PersonName}, 세상에 태어남",
            ChronicleEventKind.MinorStageBreakthrough => $"{FormatRealm(entry.RealmStage, (int)entry.MinorStage)} 소경지 돌파",
            ChronicleEventKind.BottleneckBreakthrough => $"{entry.Value:N0}번의 시도 끝에 병목을 돌파",
            ChronicleEventKind.MajorRealmBreakthrough => $"{FormatRealm(entry.RealmStage, (int)entry.MinorStage)} 대경지 돌파",
            ChronicleEventKind.FloorNineCompletion => $"{FormatRealm(entry.RealmStage, (int)entry.MinorStage)} 소경지 완성",
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
            ChronicleEventKind.ReincarnationCrossingCompleted =>
                "윤회의 강 도하를 마치고 새 생을 준비함",
            ChronicleEventKind.Rebirth => $"{entry.PersonName}, 새로운 육신으로 환생",
            _ => entry.Kind.ToString(),
        };
        var color = entry.Kind switch
        {
            ChronicleEventKind.Death or ChronicleEventKind.CombatDeath => (Brush)FindResource("DangerBrush"),
            ChronicleEventKind.Birth
                or ChronicleEventKind.ReincarnationCrossingCompleted
                or ChronicleEventKind.Rebirth => (Brush)FindResource("PositiveBrush"),
            ChronicleEventKind.MinorStageBreakthrough
                or ChronicleEventKind.BottleneckBreakthrough
                or ChronicleEventKind.MajorRealmBreakthrough
                or ChronicleEventKind.FloorNineCompletion
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
                FormatActionDuration(activity, entry.Value),
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
        WorldEventKind.ReincarnationCrossingCompleted => "윤회의 강 도하 완료 · 새 생 준비",
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
        WorldEventKind.SpiritStoneMined => $"도시 광맥 채굴 · 영석 {KoreanNumberFormatter.Format(entry.Value)} 생산",
        WorldEventKind.FloorNineCompletion => "9층 소경지 완성",
        _ => kind.ToString(),
    };

    private static string FormatPersonalChronicle(ChronicleEventKind kind, long value) => kind switch
    {
        ChronicleEventKind.Birth => "세상에 태어남",
        ChronicleEventKind.MinorStageBreakthrough => "소경지 돌파",
        ChronicleEventKind.BottleneckBreakthrough => $"{value:N0}번의 실패 끝에 병목 돌파",
        ChronicleEventKind.MajorRealmBreakthrough => "대경지 돌파",
        ChronicleEventKind.FloorNineCompletion => "9층 소경지 완성",
        ChronicleEventKind.BreakthroughFailure => "처음으로 현재 경지의 병목과 마주함",
        ChronicleEventKind.TechniqueEnlightenment => $"공법 {FormatQuality(value)}의 깨달음",
        ChronicleEventKind.SpellEnlightenment => $"법술 {FormatQuality(value)}의 깨달음",
        ChronicleEventKind.FortuitousEncounter => "인생을 바꾸는 기연",
        ChronicleEventKind.Death => $"{value:N0}세에 수명을 다함",
        ChronicleEventKind.CombatDeath => $"{value:N0}세에 전투 부상으로 사망",
        ChronicleEventKind.ReincarnationEntered => "윤회의 강에 들어감",
        ChronicleEventKind.ReincarnationCrossingCompleted => "윤회의 강 도하 완료",
        ChronicleEventKind.Rebirth => "새로운 육신으로 환생",
        _ => kind.ToString(),
    };

    private static string FormatGameDate(GameDate date) =>
        $"{KoreanNumberFormatter.Format(date.Year)}년 {date.Month:N0}월 {date.Day:N0}일";

    private static string FormatOptionalGameDate(GameDate? date) =>
        date is { } value ? FormatGameDate(value) : "아직 없음";

    private static string FormatActionDuration(AgentActivity activity, long elapsedMonths)
    {
        var months = Math.Max(1L, elapsedMonths);
        if (months < GameCalendar.MonthsPerYear)
        {
            return $"{FormatActivity(activity)} · {months:N0}개월차";
        }

        var years = months / GameCalendar.MonthsPerYear;
        var remainingMonths = months % GameCalendar.MonthsPerYear;
        return remainingMonths == 0
            ? $"{FormatActivity(activity)} · {years:N0}년차"
            : $"{FormatActivity(activity)} · {years:N0}년 {remainingMonths:N0}개월차";
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

    private static string FormatPill(PillKind kind) => kind switch
    {
        PillKind.Growth => "수행단",
        PillKind.Breakthrough => "돌파단",
        _ => kind.ToString(),
    };

    private static string FormatTreasureSlot(TreasureSlot slot) => slot switch
    {
        TreasureSlot.NatalTreasure => "본명법보",
        TreasureSlot.DefenseTreasure => "방어 법보",
        TreasureSlot.AttackTreasure => "공격 법보",
        _ => slot.ToString(),
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

    private void MapObservationPanel_ObservationRequested(object? sender, EventArgs e)
    {
        if (!_applyingObservation)
        {
            RefreshVisibleObservation();
        }
    }

    private void MapObservationPanel_AgentSelected(
        object? sender,
        MapAgentSelectedEventArgs e)
    {
        if (!_applyingObservation)
        {
            RefreshVisibleObservation(e.AgentId);
        }
    }

    private void PersonalLogCategoryTabControl_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_applyingObservation || !ReferenceEquals(e.Source, sender))
        {
            return;
        }

        _personalLogCategory = PersonalLogCategoryTabControl.SelectedIndex;
        UpdateAgentLogViews();
    }

    private void ChronicleCategoryTabControl_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_applyingObservation || !ReferenceEquals(e.Source, sender))
        {
            return;
        }

        _chronicleCategory = ChronicleCategoryTabControl.SelectedIndex;
        UpdateAgentLogViews();
    }

    private void ChronicleLifeToggle_Click(object sender, RoutedEventArgs e)
    {
        _selectedChronicleLife = sender is RadioButton { Tag: int lifeNumber }
            ? lifeNumber
            : null;
        UpdateAgentLogViews();
    }

    private void PersonalCenturyCategoryTabControl_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_applyingObservation || !ReferenceEquals(e.Source, sender))
        {
            return;
        }

        _personalCenturyCategory = PersonalCenturyCategoryTabControl.SelectedIndex;
        UpdateAgentLogViews();
    }

    private void PersonalCenturyLifeToggle_Click(object sender, RoutedEventArgs e)
    {
        _selectedPersonalCenturyLife = sender is RadioButton { Tag: int lifeNumber }
            ? lifeNumber
            : null;
        UpdateAgentLogViews();
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
        if (_applyingObservation
            || _applyingWorkspaceRoute
            || !ReferenceEquals(e.Source, sender))
        {
            return;
        }

        RefreshVisibleObservation();
    }

    private void VisibleExpander_Expanded(object sender, RoutedEventArgs e)
    {
        if (!_applyingObservation && !_applyingWorkspaceRoute)
        {
            RefreshVisibleObservation();
        }
    }

    private void AptitudeDetailTabControl_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_applyingObservation || !ReferenceEquals(e.Source, sender))
        {
            return;
        }

        if (_latestAgent?.AptitudeDetails is null)
        {
            RefreshVisibleObservation();
            return;
        }

        UpdateAptitudeDetailView(_latestAgent);
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
        _logService.AddDebug(
            DebugLogLevel.Information,
            paused ? "시뮬레이션 일시정지" : "시뮬레이션 진행 재개");
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
            await _runner.SaveDefaultAsync();
            StatusText.Text = $"저장 완료 · {GameApplicationSession.DefaultSavePath}";
            StatusText.Foreground = (Brush)FindResource("PositiveBrush");
            _logService.AddDebug(DebugLogLevel.Information, $"세계 저장 완료 · {GameApplicationSession.DefaultSavePath}");
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
            var restoredSchemaVersion = await _runner.LoadDefaultAsync();
            var metadata = _runner.GetWorldMetadata();
            _worldSeed = metadata.Seed;
            _activeBalance = metadata.Balance;
            SettingsWorkspacePanel.SetCurrentWorldSettings(
                metadata.Seed,
                metadata.Population,
                metadata.Balance);
            _projectedRecentEventSequence = long.MinValue;
            _projectedRecentEventCount = -1;
            _projectedWorldLogCategory = null;
            _projectedWorldChronicleSequence = long.MinValue;
            _projectedWorldChronicleCount = -1;
            _projectedWorldChronicleCategory = null;
            ClearReincarnationView();
            _selectedAgent = null;
            RefreshVisibleObservation();
            StatusText.Text = $"불러오기 완료 · {GameApplicationSession.DefaultSavePath}";
            StatusText.Foreground = (Brush)FindResource("PositiveBrush");
            _logService.AddDebug(DebugLogLevel.Information, $"세계 불러오기 완료 · schema {restoredSchemaVersion}");
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
        _logService.AddDebug(DebugLogLevel.Error, $"저장 처리 실패 · {exception}");
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
        _logService.AddDebug(DebugLogLevel.Information, $"게임 배속 변경 · {speed}×");
    }
}
