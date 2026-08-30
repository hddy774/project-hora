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
    private int _speed = 1;
    private bool _disposed;
    private bool _applyingObservation;

    public MainWindow()
    {
        InitializeComponent();
        Title = $"{ProductInfo.KoreanTitle} PC v{ProductInfo.Version}";
        VersionText.Text = $"VERSION {ProductInfo.Version}";
        var world = World.CreateDefault(DefaultWorldSeed);
        _runner = new SimulationRunner(world);
        _runner.SnapshotPublished += Runner_SnapshotPublished;
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

        _runner.SnapshotPublished -= Runner_SnapshotPublished;
        _runner.Faulted -= Runner_Faulted;
        _runner.Dispose();
        _runner = null;
        GC.SuppressFinalize(this);
    }

    private void Runner_SnapshotPublished(WorldSnapshot snapshot)
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => ApplySnapshot(snapshot)));
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

    private void ApplySnapshot(WorldSnapshot snapshot)
    {
        if (_runner is null)
        {
            return;
        }

        _selectedAgent ??= snapshot.LeaderId;
        var observation = _runner.GetObservation(_selectedAgent);
        ApplyObservation(observation);
    }

    private void ApplyObservation(WorldObservation observation)
    {
        _applyingObservation = true;
        var snapshot = observation.World;
        _latestSnapshot = snapshot;
        WorldTickText.Text = $"{KoreanNumberFormatter.Format(GameCalendar.GetYear(snapshot.Tick))}년";
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
            UpdateEventRows(snapshot);
            UpdateSelectedAgent(observation.SelectedAgent, observation.SelectedChronicle);
        }
        finally
        {
            _applyingObservation = false;
        }
    }

    private void UpdateRankingRows(WorldSnapshot snapshot)
    {
        IReadOnlyList<RankingRow> rows;
        var search = SearchTextBox.Text.Trim();
        if (_rankingCategory is { } category)
        {
            var rankings = category switch
            {
                RankingCategory.CombatPower => snapshot.CombatPowerRankings,
                RankingCategory.Realm => snapshot.RealmRankings,
                RankingCategory.Aptitude => snapshot.AptitudeRankings,
                _ => throw new ArgumentOutOfRangeException(nameof(category)),
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
                var first = rows.Count == 0 ? 0 : _explorerOffset + 1;
                var last = _explorerOffset + rows.Count;
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
                RankingScopeText.Text = $"검색 {rows.Count:N0}명";
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

    private void UpdateSelectedAgent(
        AgentSnapshot agent,
        IReadOnlyList<LifeChronicleEntry> chronicle)
    {
        SelectedNameText.Text = agent.Name;
        SelectedIdText.Text = $"{agent.Id} · 최종 목표 #{agent.TargetRank}";
        SelectedRankText.Text = $"전투력 #{agent.Rank:N0} · 경지 #{agent.RealmRank:N0} · 자질 #{agent.AptitudeRank:N0}";
        RealmText.Text = FormatRealm(agent.RealmStage, agent.RealmLayer);
        AgeText.Text = $"{KoreanNumberFormatter.Format(agent.AgeYears)} / {KoreanNumberFormatter.Format(agent.MaximumLifespanYears)}세";
        LifeText.Text = $"제 {agent.LifeNumber:N0}생";
        ActivityText.Text = FormatActivity(agent.Activity);
        OpponentText.Text = agent.OpponentId is { } opponent
            ? GetAgentName(opponent)
            : "없음";
        CultivationProgressBar.Maximum = Math.Max(1, agent.CultivationRequirement);
        CultivationProgressBar.Value = Math.Min(agent.CultivationProgress, agent.CultivationRequirement);
        CultivationText.Text = $"{KoreanNumberFormatter.Format(agent.CultivationProgress)} / {KoreanNumberFormatter.Format(agent.CultivationRequirement)}";
        RootText.Text = QualityRules.Format(agent.Aptitudes.SpiritualRoot);
        BoneText.Text = QualityRules.Format(agent.Aptitudes.Bone);
        ConstitutionText.Text = QualityRules.Format(agent.Aptitudes.Constitution);
        ComprehensionText.Text = QualityRules.Format(agent.Aptitudes.Comprehension);
        MindText.Text = QualityRules.Format(agent.Aptitudes.Mind);
        SoulText.Text = QualityRules.Format(agent.Aptitudes.Soul);
        TechniqueText.Text = $"{QualityRules.Format(agent.TechniqueQuality)} · 숙련 {KoreanNumberFormatter.Format(agent.TechniqueMastery)}";
        SpellText.Text = $"{QualityRules.Format(agent.SpellQuality)} · 숙련 {KoreanNumberFormatter.Format(agent.SpellMastery)}";
        HealthText.Text = $"{KoreanNumberFormatter.Format(Math.Max(0, agent.Health))} / {KoreanNumberFormatter.Format(agent.MaximumHealth)}";
        QiText.Text = $"{KoreanNumberFormatter.Format(agent.Qi)} / {KoreanNumberFormatter.Format(agent.MaximumQi)}";
        RecordText.Text = $"{KoreanNumberFormatter.Format(agent.Wins)}승 {KoreanNumberFormatter.Format(agent.Losses)}패 · {KoreanNumberFormatter.Format(agent.CombatExperience)}";
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
        SetPowerBreakdown(agent.CombatPower);
        ChronicleList.ItemsSource = chronicle
            .Reverse()
            .Select(FormatChronicle)
            .ToArray();
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
        FormatRealm(entry.RealmStage, entry.RealmLayer),
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
        FormatRealm(agent.RealmStage, agent.RealmLayer),
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
            WorldEventKind.BreakthroughFailed => $"{primary}, 돌파 실패로 내상을 입음",
            WorldEventKind.MinorStageBreakthrough => $"{primary}, {FormatEncodedRealm(worldEvent.Value)} 소경지 돌파",
            WorldEventKind.BottleneckBreakthrough => $"{primary}, {worldEvent.Value:N0}번의 시도 끝에 병목 돌파",
            WorldEventKind.MajorRealmBreakthrough => $"{primary}, {FormatEncodedRealm(worldEvent.Value)} 대경지 돌파",
            WorldEventKind.TechniqueEnlightenment => $"{primary}, 공법 {FormatQuality(worldEvent.Value)}의 깨달음 획득",
            WorldEventKind.SpellEnlightenment => $"{primary}, 법술 {FormatQuality(worldEvent.Value)}의 깨달음 획득",
            WorldEventKind.Death => $"{primary}, {worldEvent.Value:N0}세에 수명을 다함",
            WorldEventKind.ReincarnationEntered => $"{primary}, 윤회의 강에 들어감",
            WorldEventKind.Rebirth => $"{primary}, 제 {worldEvent.Value:N0}생으로 환생",
            WorldEventKind.MissionCompleted => $"{primary}, 도시 임무 완료 · 영석 +{KoreanNumberFormatter.Format(worldEvent.Value)}",
            WorldEventKind.TreasureCrafted => $"{primary}, {FormatQuality(worldEvent.Value)} 법보 제작",
            WorldEventKind.PillCrafted => $"{primary}, {FormatPill((PillKind)worldEvent.Value)} 제작",
            WorldEventKind.PillPurchased => $"{primary}, 영석으로 {FormatPill((PillKind)worldEvent.Value)} 구매",
            WorldEventKind.MaterialSold => $"{primary}, {EconomyRules.GetMaterial((MaterialId)worldEvent.Value).KoreanName} 판매",
            WorldEventKind.NewLeader => $"{primary}, {secondary}을 제치고 새로운 1위 등극",
            WorldEventKind.CombatStarted => $"{primary}, 상위권 {secondary}에게 도전",
            WorldEventKind.CombatPhysicalAttack => $"{primary} → {secondary}, 공법 공격 {worldEvent.Value:N0} 피해",
            WorldEventKind.CombatSpellCast => $"{primary} → {secondary}, 법술 {worldEvent.Value:N0} 피해",
            WorldEventKind.CombatGuarded => $"{primary}, 다음 공격에 대비해 방어",
            WorldEventKind.CombatRetreated => $"{primary}, {secondary}과의 전투에서 이탈",
            WorldEventKind.CombatEnded => $"{primary}, {secondary}에게 승리 · 경험 +{worldEvent.Value:N0}",
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
                or WorldEventKind.PillCrafted => (Brush)FindResource("AccentBrush"),
            WorldEventKind.CombatEnded => (Brush)FindResource("PositiveBrush"),
            WorldEventKind.BreakthroughFailed or WorldEventKind.CombatRetreated => (Brush)FindResource("DangerBrush"),
            _ => (Brush)FindResource("PrimaryTextBrush"),
        };
        return new EventRow($"T+{worldEvent.Tick.Value:N0}", summary, color);
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
            ChronicleEventKind.TechniqueEnlightenment => $"공법 {FormatQuality(entry.Value)}의 깨달음을 얻음",
            ChronicleEventKind.SpellEnlightenment => $"법술 {FormatQuality(entry.Value)}의 깨달음을 얻음",
            ChronicleEventKind.FortuitousEncounter => "인생을 바꾸는 기연을 만남",
            ChronicleEventKind.Death => $"{entry.Value:N0}세에 수명을 다함",
            ChronicleEventKind.ReincarnationEntered => "육신을 떠나 윤회의 강에 들어감",
            ChronicleEventKind.Rebirth => $"{entry.PersonName}, 새로운 육신으로 환생",
            _ => entry.Kind.ToString(),
        };
        var color = entry.Kind switch
        {
            ChronicleEventKind.Death => (Brush)FindResource("DangerBrush"),
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
            $"제 {entry.LifeNumber:N0}생 · {entry.AgeYears:N0}세 · 세계 {GameCalendar.GetYear(entry.Tick):N0}년",
            summary,
            color);
    }

    private string GetAgentName(AgentId agentId) =>
        _latestSnapshot is null ? agentId.ToString() : GetAgentName(_latestSnapshot, agentId);

    private static string GetAgentName(WorldSnapshot snapshot, AgentId agentId) =>
        snapshot.AgentNames.TryGetValue(agentId.Value, out var name) ? name : agentId.ToString();

    private static string FormatEncodedRealm(long encodedRealm)
    {
        var stage = (RealmStage)(encodedRealm / 10);
        var layer = checked((int)(encodedRealm % 10));
        return FormatRealm(stage, layer);
    }

    private static string FormatQuality(long hundredths) =>
        QualityRules.Format(new QualityValue(checked((ushort)hundredths)));

    private static string FormatRealm(RealmStage stage, int layer)
    {
        var name = stage switch
        {
            RealmStage.QiRefining => "연기",
            RealmStage.FoundationEstablishment => "축기",
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
        if (stage is RealmStage.QiRefining or RealmStage.FoundationEstablishment)
        {
            name += "경";
        }

        var minorStage = (MinorStage)layer switch
        {
            MinorStage.Early => "초기",
            MinorStage.Middle => "중기",
            MinorStage.Late => "후기",
            MinorStage.GreatCompletion => "대원만",
            _ => layer.ToString(CultureInfo.InvariantCulture),
        };
        return $"{name} {minorStage}";
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

    private void RankingGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applyingObservation
            || _runner is null
            || RankingGrid.SelectedItem is not RankingRow row)
        {
            return;
        }

        _selectedAgent = row.AgentId;
        ApplyObservation(_runner.GetObservation(row.AgentId));
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

        if (_latestSnapshot is not null)
        {
            UpdateRankingRows(_latestSnapshot);
        }
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
