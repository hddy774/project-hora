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

public sealed class NewWorldRequestedEventArgs(NewWorldSettingsRequest request) : EventArgs
{
    public NewWorldSettingsRequest Request { get; } = request;
}

public partial class SettingsWorkspaceView : UserControl
{
    public SettingsWorkspaceView()
    {
        InitializeComponent();
    }

    public event EventHandler<UiScaleRequestedEventArgs>? UiScaleRequested;
    public event EventHandler? RestoreWindowRequested;
    public event EventHandler? MaximizeWindowRequested;
    public event EventHandler<PerformanceMeterChangedEventArgs>? PerformanceMeterChanged;
    public event EventHandler<NewWorldRequestedEventArgs>? NewWorldRequested;

    public bool IsPerformanceMeterEnabled => PerformanceMeterCheckBox.IsChecked == true;

    public void SetCurrentWorldSettings(
        ulong seed,
        int population,
        WorldBalanceConfiguration balance)
    {
        SeedInput.Text = seed.ToString(CultureInfo.InvariantCulture);
        PopulationInput.Text = population.ToString(CultureInfo.InvariantCulture);
        DeveloperModeCheckBox.IsChecked = balance.DeveloperModeEnabled;
        CultivationBalanceSlider.Value = balance.CultivationWorkPercent;
        CombatBalanceSlider.Value = balance.CombatAggressionPercent;
        EconomyBalanceSlider.Value = balance.EconomyActivityPercent;
        MonsterBalanceSlider.Value = balance.MonsterDangerPercent;
    }

    public void UpdatePerformance(RuntimePerformanceSnapshot snapshot)
    {
        if (!snapshot.IsEnabled)
        {
            SimulationTimingText.Text = "계측 꺼짐";
            SimulationAverageText.Text = string.Empty;
            ObservationTimingText.Text = "계측 꺼짐";
            ObservationAverageText.Text = string.Empty;
            BacklogText.Text = "0 ms";
            PerformanceSampleText.Text = "0회";
            return;
        }

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
        PulseBudgetText.Text = $"pulse 예산 {snapshot.PulseBudgetMilliseconds} ms · {snapshot.Speed}×";
        PerformanceSampleText.Text =
            $"pulse {snapshot.SimulationPulseCount:N0} · query {snapshot.ObservationCount:N0}";
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

    private void DeveloperModeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = DeveloperModeCheckBox.IsChecked == true;
        DeveloperBalancePanel.IsEnabled = enabled;
        SettingsStateText.Text = enabled ? "개발자 조정 준비" : "일반 모드";
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

        var enabled = DeveloperModeCheckBox.IsChecked == true;
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
