using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Chronicles.Runtime;
using Chronicles.Simulation;

namespace Chronicles.Desktop;

public sealed record NewWorldSettingsRequest(
    int Population,
    ulong Seed,
    WorldBalanceConfiguration Balance);

public sealed class UiScaleRequestedEventArgs(double scale) : EventArgs
{
    public double Scale { get; } = scale;
}

public sealed class PerformanceMeterChangedEventArgs(bool enabled) : EventArgs
{
    public bool Enabled { get; } = enabled;
}

public sealed class PerformanceSampleRecordedEventArgs(string line) : EventArgs
{
    public string Line { get; } = line;
}

public sealed class NewWorldRequestedEventArgs(NewWorldSettingsRequest request) : EventArgs
{
    public NewWorldSettingsRequest Request { get; } = request;
}

public partial class SettingsWorkspaceView : UserControl
{
    private readonly PerformanceDiagnosticsHistory _performanceHistory = new();
    private RuntimePerformanceSnapshot _latestPerformance;
    private PerformanceDiagnosticSample? _latestPerformanceSample;
    private bool _hasLatestPerformance;
    private string? _performanceFileStatus;
    private bool _performanceFileStatusIsError;

    public SettingsWorkspaceView()
    {
        InitializeComponent();
    }

    public event EventHandler<UiScaleRequestedEventArgs>? UiScaleRequested;
    public event EventHandler? RestoreWindowRequested;
    public event EventHandler? MaximizeWindowRequested;
    public event EventHandler<PerformanceMeterChangedEventArgs>? PerformanceMeterChanged;
    public event EventHandler<PerformanceSampleRecordedEventArgs>? PerformanceSampleRecorded;
    public event EventHandler? PerformanceAutoOutputChanged;
    public event EventHandler? PerformanceExportRequested;
    public event EventHandler? PerformanceFolderOpenRequested;
    public event EventHandler<NewWorldRequestedEventArgs>? NewWorldRequested;

    public bool IsPerformanceMeterEnabled => PerformanceMeterCheckBox.IsChecked == true;
    public bool AutoPerformanceOutput => PerformanceAutoOutputCheckBox.IsChecked == true;

    public void SetCurrentWorldSettings(
        ulong seed,
        int population,
        WorldBalanceConfiguration balance)
    {
        SeedInput.Text = seed.ToString(CultureInfo.InvariantCulture);
        PopulationInput.Text = population.ToString(CultureInfo.InvariantCulture);
        WorldSettingsModeCheckBox.IsChecked = balance.DeveloperModeEnabled;
        CultivationBalanceSlider.Value = balance.CultivationWorkPercent;
        CombatBalanceSlider.Value = balance.CombatAggressionPercent;
        EconomyBalanceSlider.Value = balance.EconomyActivityPercent;
        MonsterBalanceSlider.Value = balance.MonsterDangerPercent;
    }

    public void UpdatePerformance(RuntimePerformanceSnapshot snapshot)
    {
        _latestPerformance = snapshot;
        _hasLatestPerformance = true;
        if (_performanceHistory.TryCapture(snapshot, out var sample) && sample is not null)
        {
            _latestPerformanceSample = sample;
            PerformanceSampleRecorded?.Invoke(
                this,
                new PerformanceSampleRecordedEventArgs(sample.ToLogLine()));
        }

        RefreshSelectedPerformanceTab();
    }

    public string[] GetPerformanceLogLines() => _performanceHistory.GetLogLines();

    public void ResetPerformanceHistory()
    {
        _performanceHistory.Clear();
        _latestPerformanceSample = null;
        _hasLatestPerformance = false;
        _performanceFileStatus = null;
        PerformanceLogGrid.ItemsSource = null;
        RefreshSelectedPerformanceTab();
    }

    public void SetPerformanceFileStatus(string text, bool isError = false)
    {
        _performanceFileStatus = text;
        _performanceFileStatusIsError = isError;
        RefreshPerformanceFileTab();
    }

    private void RefreshSelectedPerformanceTab()
    {
        if (!IsInitialized || PerformanceDetailTabControl is null)
        {
            return;
        }

        switch (PerformanceDetailTabControl.SelectedIndex)
        {
            case 0:
                RefreshCurrentPerformanceTab();
                break;
            case 1:
                RefreshPerformanceGraph();
                break;
            case 2:
                RefreshPerformanceLogTab();
                break;
            case 3:
                RefreshPerformanceFileTab();
                break;
        }
    }

    private void RefreshCurrentPerformanceTab()
    {
        if (!_hasLatestPerformance || !_latestPerformance.IsEnabled)
        {
            SimulationTimingText.Text = "계측 꺼짐";
            SimulationAverageText.Text = string.Empty;
            ObservationTimingText.Text = "계측 꺼짐";
            ObservationAverageText.Text = string.Empty;
            BacklogText.Text = "0 ms";
            PulseBudgetText.Text = "성능 탭에서 계측 사용 가능";
            PerformanceSampleText.Text = $"최근 {_performanceHistory.Count:N0} / 100";
            PerformanceHealthText.Text = "계측 중지";
            PerformanceHealthDetailText.Text = "기존 최근 표본은 유지됩니다.";
            PerformanceHeaderStatusText.Text = "계측 중지";
            ApplyHealthBrush(PerformanceHealthLevel.Measuring);
            RefreshProcessAndPercentileCards();
            return;
        }

        var snapshot = _latestPerformance;
        SimulationTimingText.Text = $"최근 {snapshot.LastSimulationMilliseconds:0.00} ms";
        SimulationAverageText.Text =
            $"평균 {snapshot.AverageSimulationMilliseconds:0.00} · 최대 {snapshot.MaximumSimulationMilliseconds:0.00} ms";
        ObservationTimingText.Text = $"최근 {snapshot.LastObservationMilliseconds:0.00} ms";
        ObservationAverageText.Text =
            $"평균 {snapshot.AverageObservationMilliseconds:0.00} · 최대 {snapshot.MaximumObservationMilliseconds:0.00} ms";
        BacklogText.Text = $"{snapshot.EstimatedBacklogMilliseconds:0.00} ms";
        BacklogText.Foreground = (Brush)FindResource(
            snapshot.EstimatedBacklogMilliseconds > 0.01
                ? "DangerBrush"
                : "PositiveBrush");
        PulseBudgetText.Text =
            $"예산 {snapshot.PulseBudgetMilliseconds} ms · {snapshot.Speed}× · " +
            (snapshot.IsPaused ? "일시정지" : "진행");
        PerformanceSampleText.Text =
            $"pulse {snapshot.SimulationPulseCount:N0} · query {snapshot.ObservationCount:N0}";

        var health = _latestPerformanceSample?.Health
            ?? PerformanceDiagnosticsHistory.Evaluate(snapshot);
        PerformanceHealthText.Text = health switch
        {
            PerformanceHealthLevel.Measuring => "표본 수집 중",
            PerformanceHealthLevel.Healthy => "정상",
            PerformanceHealthLevel.Warning => "주의",
            PerformanceHealthLevel.Overloaded => "과부하",
            _ => health.ToString(),
        };
        PerformanceHeaderStatusText.Text = PerformanceHealthText.Text;
        PerformanceHealthDetailText.Text = health switch
        {
            PerformanceHealthLevel.Measuring => "pulse와 query 표본을 기다리고 있습니다.",
            PerformanceHealthLevel.Healthy => "backlog 0 · pulse/query 예산 이내",
            PerformanceHealthLevel.Warning => "예산에 접근한 최근 표본이 있습니다.",
            PerformanceHealthLevel.Overloaded => "backlog 또는 예산 초과 표본을 확인하세요.",
            _ => string.Empty,
        };
        ApplyHealthBrush(health);
        RefreshProcessAndPercentileCards();
    }

    private void RefreshProcessAndPercentileCards()
    {
        if (_latestPerformanceSample is { } latest)
        {
            ProcessCpuText.Text = $"{latest.ProcessCpuPercent:0.0}%";
            ProcessThreadText.Text = $"프로세스 스레드 {latest.ProcessThreadCount:N0}개";
            WorkingSetText.Text = $"{latest.WorkingSetMegabytes:0.0} MiB";
            ManagedHeapText.Text = $"관리 힙 {latest.ManagedHeapMegabytes:0.0} MiB";
        }
        else
        {
            ProcessCpuText.Text = "표본 대기";
            ProcessThreadText.Text = string.Empty;
            WorkingSetText.Text = "표본 대기";
            ManagedHeapText.Text = string.Empty;
        }

        var samples = _performanceHistory.GetChronological();
        if (samples.Length == 0)
        {
            RecentP95Text.Text = "표본 대기";
            RecentSampleWindowText.Text = "최근 0 / 최대 100개";
            return;
        }

        var pulseP95 = PerformanceDiagnosticsHistory.Percentile95(
            samples.Select(item => item.Runtime.LastSimulationMilliseconds));
        var queryP95 = PerformanceDiagnosticsHistory.Percentile95(
            samples.Select(item => item.Runtime.LastObservationMilliseconds));
        RecentP95Text.Text = $"pulse {pulseP95:0.00} · query {queryP95:0.00} ms";
        RecentSampleWindowText.Text = $"최근 {samples.Length:N0} / 최대 100개 · 1초 간격";
    }

    private void ApplyHealthBrush(PerformanceHealthLevel health)
    {
        var resource = health switch
        {
            PerformanceHealthLevel.Healthy => "PositiveBrush",
            PerformanceHealthLevel.Warning => "AccentBrush",
            PerformanceHealthLevel.Overloaded => "DangerBrush",
            _ => "SecondaryTextBrush",
        };
        var brush = (Brush)FindResource(resource);
        PerformanceHealthText.Foreground = brush;
        PerformanceHeaderStatusText.Foreground = brush;
    }

    private void RefreshPerformanceGraph()
    {
        var samples = _performanceHistory.GetChronological();
        var width = PerformanceGraphCanvas.ActualWidth;
        var height = PerformanceGraphCanvas.ActualHeight;
        if (samples.Length == 0 || width <= 1d || height <= 1d)
        {
            SimulationGraphLine.Points = [];
            ObservationGraphLine.Points = [];
            BacklogGraphLine.Points = [];
            PerformanceGraphSummaryText.Text =
                "최근 표본이 쌓이면 100% 예산선을 기준으로 그래프를 표시합니다.";
            return;
        }

        var loads = samples.SelectMany(item => new[]
        {
            item.Runtime.LastSimulationMilliseconds
                / Math.Max(1d, item.Runtime.PulseBudgetMilliseconds) * 100d,
            item.Runtime.LastObservationMilliseconds
                / PerformanceDiagnosticsHistory.ObservationBudgetMilliseconds * 100d,
            item.Runtime.EstimatedBacklogMilliseconds
                / Math.Max(1d, item.Runtime.PulseBudgetMilliseconds) * 100d,
        }).ToArray();
        var graphMaximumPercent = Math.Clamp(
            Math.Ceiling(Math.Max(125d, loads.Max()) / 25d) * 25d,
            125d,
            400d);
        var simulationPoints = new PointCollection(samples.Length);
        var observationPoints = new PointCollection(samples.Length);
        var backlogPoints = new PointCollection(samples.Length);
        for (var index = 0; index < samples.Length; index++)
        {
            var x = samples.Length == 1
                ? width
                : index * width / (samples.Length - 1d);
            var runtime = samples[index].Runtime;
            simulationPoints.Add(new Point(
                x,
                ToGraphY(
                    runtime.LastSimulationMilliseconds
                        / Math.Max(1d, runtime.PulseBudgetMilliseconds) * 100d,
                    graphMaximumPercent,
                    height)));
            observationPoints.Add(new Point(
                x,
                ToGraphY(
                    runtime.LastObservationMilliseconds
                        / PerformanceDiagnosticsHistory.ObservationBudgetMilliseconds * 100d,
                    graphMaximumPercent,
                    height)));
            backlogPoints.Add(new Point(
                x,
                ToGraphY(
                    runtime.EstimatedBacklogMilliseconds
                        / Math.Max(1d, runtime.PulseBudgetMilliseconds) * 100d,
                    graphMaximumPercent,
                    height)));
        }

        SimulationGraphLine.Points = simulationPoints;
        ObservationGraphLine.Points = observationPoints;
        BacklogGraphLine.Points = backlogPoints;
        var budgetY = ToGraphY(100d, graphMaximumPercent, height);
        PerformanceBudgetLine.X1 = 0d;
        PerformanceBudgetLine.X2 = width;
        PerformanceBudgetLine.Y1 = budgetY;
        PerformanceBudgetLine.Y2 = budgetY;
        PerformanceGraphSummaryText.Text =
            $"최근 {samples.Length:N0}개 · 세로축 0~{graphMaximumPercent:0}% · " +
            $"pulse p95 {PerformanceDiagnosticsHistory.Percentile95(samples.Select(item => item.Runtime.LastSimulationMilliseconds)):0.00} ms · " +
            $"query p95 {PerformanceDiagnosticsHistory.Percentile95(samples.Select(item => item.Runtime.LastObservationMilliseconds)):0.00} ms";
    }

    private static double ToGraphY(double percent, double maximumPercent, double height) =>
        height - Math.Clamp(percent, 0d, maximumPercent) / maximumPercent * height;

    private void RefreshPerformanceLogTab()
    {
        var rows = _performanceHistory.GetNewestFirst();
        PerformanceLogGrid.ItemsSource = rows;
        PerformanceLogCountText.Text =
            $"최근 {rows.Length:N0} / 최대 {GameLogService.PerformanceLogCapacity:N0}개 · 새 표본이 위에 표시됩니다.";
    }

    private void RefreshPerformanceFileTab()
    {
        if (!IsInitialized || PerformanceFileStatusText is null)
        {
            return;
        }
        PerformanceFileStatusText.Text = _performanceFileStatus
            ?? $"최근 {_performanceHistory.Count:N0}개 · " +
                (AutoPerformanceOutput ? "자동 기록 중" : "자동 기록 꺼짐");
        PerformanceFileStatusText.Foreground = (Brush)FindResource(
            _performanceFileStatusIsError ? "DangerBrush" : "AccentBrush");
    }

    public void SetWorldCreationStatus(string text, bool isError)
    {
        WorldCreationStatusText.Text = text;
        WorldCreationStatusText.Foreground = (Brush)FindResource(
            isError ? "DangerBrush" : "PositiveBrush");
    }

    private void UiScaleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string raw }
            && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
        {
            UiScaleRequested?.Invoke(this, new UiScaleRequestedEventArgs(scale));
        }
    }

    private void RestoreWindowButton_Click(object sender, RoutedEventArgs e) =>
        RestoreWindowRequested?.Invoke(this, EventArgs.Empty);

    private void MaximizeWindowButton_Click(object sender, RoutedEventArgs e) =>
        MaximizeWindowRequested?.Invoke(this, EventArgs.Empty);

    private void PopulationPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string population })
        {
            PopulationInput.Text = population;
        }
    }

    private void PerformanceMeterCheckBox_Changed(object sender, RoutedEventArgs e) =>
        PerformanceMeterChanged?.Invoke(
            this,
            new PerformanceMeterChangedEventArgs(IsPerformanceMeterEnabled));

    private void PerformanceDetailTabControl_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.Source, sender))
        {
            RefreshSelectedPerformanceTab();
        }
    }

    private void PerformanceGraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (PerformanceDetailTabControl.SelectedIndex == 1)
        {
            RefreshPerformanceGraph();
        }
    }

    private void PerformanceAutoOutputCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized)
        {
            return;
        }
        _performanceFileStatus = null;
        _performanceFileStatusIsError = false;
        PerformanceAutoOutputChanged?.Invoke(this, EventArgs.Empty);
        RefreshPerformanceFileTab();
    }

    private void PerformanceExportButton_Click(object sender, RoutedEventArgs e) =>
        PerformanceExportRequested?.Invoke(this, EventArgs.Empty);

    private void PerformanceFolderButton_Click(object sender, RoutedEventArgs e) =>
        PerformanceFolderOpenRequested?.Invoke(this, EventArgs.Empty);

    private void WorldSettingsModeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = WorldSettingsModeCheckBox.IsChecked == true;
        WorldBalancePanel.IsEnabled = enabled;
        SettingsStateText.Text = enabled ? "세계 설정 준비" : "기본 세계";
    }

    private void BalanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized)
        {
            return;
        }
        CultivationBalanceValueText.Text = $"{(int)CultivationBalanceSlider.Value}%";
        CombatBalanceValueText.Text = $"{(int)CombatBalanceSlider.Value}%";
        EconomyBalanceValueText.Text = $"{(int)EconomyBalanceSlider.Value}%";
        MonsterBalanceValueText.Text = $"{(int)MonsterBalanceSlider.Value}%";
    }

    private void CreateWorldButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PopulationInput.Text, out var population)
            || population is < 100 or > 50_000)
        {
            SetWorldCreationStatus("AI 수는 100~50,000 사이의 정수여야 합니다.", isError: true);
            return;
        }
        if (!ulong.TryParse(SeedInput.Text, out var seed))
        {
            SetWorldCreationStatus("세계 seed는 0 이상의 정수여야 합니다.", isError: true);
            return;
        }

        var enabled = WorldSettingsModeCheckBox.IsChecked == true;
        var balance = new WorldBalanceConfiguration(
            enabled,
            (int)CultivationBalanceSlider.Value,
            (int)CombatBalanceSlider.Value,
            (int)EconomyBalanceSlider.Value,
            (int)MonsterBalanceSlider.Value);
        WorldBalanceRules.Validate(balance);
        NewWorldRequested?.Invoke(
            this,
            new NewWorldRequestedEventArgs(
                new NewWorldSettingsRequest(population, seed, balance)));
    }
}
