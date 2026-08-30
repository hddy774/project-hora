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
        WorldTickText.Text = $"{snapshot.Tick.Value:N0} tick";
        PopulationText.Text = snapshot.Population.ToString("N0", CultureInfo.CurrentCulture);
        ActiveCombatText.Text = snapshot.ActiveCombats.ToString("N0", CultureInfo.CurrentCulture);
        CompletedCombatText.Text = snapshot.TotalCombatsCompleted.ToString("N0", CultureInfo.CurrentCulture);
        ChallengeText.Text = snapshot.TotalChallenges.ToString("N0", CultureInfo.CurrentCulture);
        BreakthroughText.Text = snapshot.TotalBreakthroughs.ToString("N0", CultureInfo.CurrentCulture);
        StatusText.Text = _runner?.IsPaused == true
            ? $"일시정지 · {_speed}× · seed {DefaultWorldSeed}"
            : $"실시간 진행 · {_speed}× · seed {DefaultWorldSeed}";
        StatusText.Foreground = (Brush)FindResource("SecondaryTextBrush");

        try
        {
            UpdateRankingRows(snapshot);
            UpdateEventRows(snapshot);
            UpdateSelectedAgent(observation.SelectedAgent);
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

    private void UpdateSelectedAgent(AgentSnapshot agent)
    {
        SelectedNameText.Text = agent.Name;
        SelectedIdText.Text = $"{agent.Id} · 최종 목표 #{agent.TargetRank}";
        SelectedRankText.Text = $"RANK #{agent.Rank:N0}";
        RealmText.Text = FormatRealm(agent.RealmStage, agent.RealmLayer);
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
        TechniqueText.Text = $"{agent.TechniqueGrade}품 · 숙련 {agent.TechniqueMastery:N0}";
        SpellText.Text = $"{agent.SpellGrade}품 · 숙련 {agent.SpellMastery:N0}";
        HealthText.Text = $"{Math.Max(0, agent.Health):N0} / {agent.MaximumHealth:N0}";
        QiText.Text = $"{agent.Qi:N0} / {agent.MaximumQi:N0}";
        RecordText.Text = $"{agent.Wins:N0}승 {agent.Losses:N0}패 · {agent.CombatExperience:N0}";
        SetPowerBreakdown(agent.CombatPower);
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
            WorldEventKind.NewLeader or WorldEventKind.BreakthroughSucceeded => (Brush)FindResource("AccentBrush"),
            WorldEventKind.CombatEnded => (Brush)FindResource("PositiveBrush"),
            WorldEventKind.BreakthroughFailed or WorldEventKind.CombatRetreated => (Brush)FindResource("DangerBrush"),
            _ => (Brush)FindResource("PrimaryTextBrush"),
        };
        return new EventRow($"T+{worldEvent.Tick.Value:N0}", summary, color);
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
            RealmStage.CoreFormation => "결단",
            RealmStage.NascentSoul => "원영",
            RealmStage.SpiritTransformation => "화신",
            _ => stage.ToString(),
        };
        return $"{name} {layer}층";
    }

    private static string FormatActivity(AgentActivity activity) => activity switch
    {
        AgentActivity.Cultivating => "수행",
        AgentActivity.TrainingTechnique => "공법 수련",
        AgentActivity.TrainingSpell => "법술 연마",
        AgentActivity.Recovering => "회복",
        AgentActivity.SeekingChallenge => "도전 탐색",
        AgentActivity.Fighting => "전투 중",
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

    private void Speed4Button_Click(object sender, RoutedEventArgs e) => SetSpeed(4);

    private void Speed16Button_Click(object sender, RoutedEventArgs e) => SetSpeed(16);

    private void SetSpeed(int speed)
    {
        _speed = speed;
        _runner?.SetSpeed(speed);
    }
}
