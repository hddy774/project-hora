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
    private SimulationRunner? _runner;
    private WorldSnapshot? _latestSnapshot;
    private AgentId? _selectedAgent;
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
        WorldTickText.Text = $"{GameCalendar.GetYear(snapshot.Tick):N0}년";
        PopulationText.Text = snapshot.Population.ToString("N0", CultureInfo.CurrentCulture);
        ActiveCombatText.Text = snapshot.ActiveCombats.ToString("N0", CultureInfo.CurrentCulture);
        CompletedCombatText.Text = snapshot.TotalCombatsCompleted.ToString("N0", CultureInfo.CurrentCulture);
        ChallengeText.Text = snapshot.TotalChallenges.ToString("N0", CultureInfo.CurrentCulture);
        BreakthroughText.Text = snapshot.TotalBreakthroughs.ToString("N0", CultureInfo.CurrentCulture);
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
        if (search.Length == 0)
        {
            rows = snapshot.Rankings.Select(ToRankingRow).ToArray();
        }
        else if (_runner is not null)
        {
            rows = _runner.SearchAgents(search).Select(ToRankingRow).ToArray();
        }
        else
        {
            rows = Array.Empty<RankingRow>();
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
        SelectedRankText.Text = $"RANK #{agent.Rank:N0}";
        RealmText.Text = FormatRealm(agent.RealmStage, agent.RealmLayer);
        AgeText.Text = $"{agent.AgeYears:N0} / {agent.MaximumLifespanYears:N0}세";
        LifeText.Text = $"제 {agent.LifeNumber:N0}생";
        ActivityText.Text = FormatActivity(agent.Activity);
        OpponentText.Text = agent.OpponentId is { } opponent
            ? GetAgentName(opponent)
            : "없음";
        CultivationProgressBar.Maximum = Math.Max(1, agent.CultivationRequirement);
        CultivationProgressBar.Value = Math.Min(agent.CultivationProgress, agent.CultivationRequirement);
        CultivationText.Text = $"{agent.CultivationProgress:N0} / {agent.CultivationRequirement:N0}";
        RootText.Text = agent.Aptitudes.SpiritualRoot.ToString(CultureInfo.CurrentCulture);
        BoneText.Text = agent.Aptitudes.Bone.ToString(CultureInfo.CurrentCulture);
        ConstitutionText.Text = agent.Aptitudes.Constitution.ToString(CultureInfo.CurrentCulture);
        ComprehensionText.Text = agent.Aptitudes.Comprehension.ToString(CultureInfo.CurrentCulture);
        MindText.Text = agent.Aptitudes.Mind.ToString(CultureInfo.CurrentCulture);
        SoulText.Text = agent.Aptitudes.Soul.ToString(CultureInfo.CurrentCulture);
        TechniqueText.Text = $"{agent.TechniqueGrade}품 · 숙련 {agent.TechniqueMastery:N0}";
        SpellText.Text = $"{agent.SpellGrade}품 · 숙련 {agent.SpellMastery:N0}";
        HealthText.Text = $"{Math.Max(0, agent.Health):N0} / {agent.MaximumHealth:N0}";
        QiText.Text = $"{agent.Qi:N0} / {agent.MaximumQi:N0}";
        RecordText.Text = $"{agent.Wins:N0}승 {agent.Losses:N0}패 · {agent.CombatExperience:N0}";
        SetPowerBreakdown(agent.CombatPower);
        ChronicleList.ItemsSource = chronicle
            .Reverse()
            .Select(FormatChronicle)
            .ToArray();
    }

    private void SetPowerBreakdown(CombatPowerBreakdown power)
    {
        var maximum = Math.Max(1, power.Total);
        TotalPowerText.Text = power.Total.ToString("N0", CultureInfo.CurrentCulture);
        SetPowerRow(RealmPowerBar, RealmPowerText, power.Realm, maximum);
        SetPowerRow(AptitudePowerBar, AptitudePowerText, power.Aptitude, maximum);
        SetPowerRow(TechniquePowerBar, TechniquePowerText, power.Technique, maximum);
        SetPowerRow(SpellPowerBar, SpellPowerText, power.Spell, maximum);
        SetPowerRow(ExperiencePowerBar, ExperiencePowerText, power.Experience, maximum);
    }

    private static void SetPowerRow(ProgressBar bar, TextBlock label, int value, int maximum)
    {
        bar.Maximum = maximum;
        bar.Value = value;
        label.Text = value.ToString("N0", CultureInfo.CurrentCulture);
    }

    private RankingRow ToRankingRow(RankingEntry entry) => new(
        entry.Rank,
        entry.AgentId,
        entry.Name,
        FormatRealm(entry.RealmStage, entry.RealmLayer),
        entry.CombatPower.Total.ToString("N0", CultureInfo.CurrentCulture),
        FormatActivity(entry.Activity));

    private RankingRow ToRankingRow(AgentSnapshot agent) => new(
        agent.Rank,
        agent.Id,
        agent.Name,
        FormatRealm(agent.RealmStage, agent.RealmLayer),
        agent.CombatPower.Total.ToString("N0", CultureInfo.CurrentCulture),
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
            WorldEventKind.TechniqueEnlightenment => $"{primary}, 공법 {worldEvent.Value}품의 깨달음 획득",
            WorldEventKind.SpellEnlightenment => $"{primary}, 법술 {worldEvent.Value}품의 깨달음 획득",
            WorldEventKind.Death => $"{primary}, {worldEvent.Value:N0}세에 수명을 다함",
            WorldEventKind.ReincarnationEntered => $"{primary}, 윤회의 강에 들어감",
            WorldEventKind.Rebirth => $"{primary}, 제 {worldEvent.Value:N0}생으로 환생",
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
                or WorldEventKind.Rebirth => (Brush)FindResource("AccentBrush"),
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
            ChronicleEventKind.TechniqueEnlightenment => $"공법 {entry.Value}품의 깨달음을 얻음",
            ChronicleEventKind.SpellEnlightenment => $"법술 {entry.Value}품의 깨달음을 얻음",
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

    private static string FormatEncodedRealm(int encodedRealm)
    {
        var stage = (RealmStage)(encodedRealm / 10);
        var layer = encodedRealm % 10;
        return FormatRealm(stage, layer);
    }

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
        _ => activity.ToString(),
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
