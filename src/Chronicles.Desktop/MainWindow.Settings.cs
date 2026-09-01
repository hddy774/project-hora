using System.Windows;
using System.Windows.Media;
using Chronicles.Application;

namespace Chronicles.Desktop;

public partial class MainWindow
{
    private void SettingsWorkspacePanel_UiScaleRequested(
        object? sender,
        UiScaleRequestedEventArgs e)
    {
        var scale = e.Scale;
        RootLayoutGrid.LayoutTransform = new ScaleTransform(scale, scale);
        _logService.AddDebug(DebugLogLevel.Information, $"UI 배율 변경 · {scale * 100:0}%");
    }

    private void SettingsWorkspacePanel_RestoreWindowRequested(object? sender, EventArgs e)
    {
        WindowState = WindowState.Normal;
        Width = 1720;
        Height = 1000;
        Left = Math.Max(0, (SystemParameters.WorkArea.Width - Width) / 2);
        Top = Math.Max(0, (SystemParameters.WorkArea.Height - Height) / 2);
    }

    private void SettingsWorkspacePanel_MaximizeWindowRequested(object? sender, EventArgs e) =>
        WindowState = WindowState.Maximized;

    private void SettingsWorkspacePanel_PerformanceMeterChanged(
        object? sender,
        PerformanceMeterChangedEventArgs e)
    {
        _runner?.SetPerformanceDiagnosticsEnabled(
            e.Enabled && _workspaceRoute == WorkspaceRoute.Settings);
        RefreshVisibleObservation();
    }

    private async void SettingsWorkspacePanel_NewWorldRequested(
        object? sender,
        NewWorldRequestedEventArgs e)
    {
        var request = e.Request;
        var answer = MessageBox.Show(
            $"현재 세계를 닫고 AI {request.Population:N0}명의 새 세계를 시작할까요?\n" +
            "현재 세계는 자동 저장되지 않습니다.",
            "새 세계 생성",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
        {
            SettingsWorkspacePanel.SetWorldCreationStatus("새 세계 생성을 취소했습니다.", isError: false);
            return;
        }

        var previous = _runner;
        previous?.SetPaused(true);
        SettingsWorkspacePanel.SetWorldCreationStatus(
            $"AI {request.Population:N0}명의 세계를 생성하는 중…",
            isError: false);
        try
        {
            var replacement = await Task.Run(() => GameApplicationSession.Create(
                request.Seed,
                request.Population,
                request.Balance));
            ReplaceRunner(replacement, request.Seed, request.Balance);
            previous?.Dispose();
            SettingsWorkspacePanel.SetWorldCreationStatus(
                $"AI {request.Population:N0}명 · seed {request.Seed} 세계 생성 완료",
                isError: false);
        }
        catch (Exception exception)
        {
            previous?.SetPaused(false);
            SettingsWorkspacePanel.SetWorldCreationStatus(
                $"세계 생성 실패 · {exception.Message}",
                isError: true);
            _logService.AddDebug(
                DebugLogLevel.Error,
                $"새 세계 생성 실패 · {exception}");
        }
    }

    private void ReplaceRunner(
        GameApplicationSession replacement,
        ulong seed,
        Chronicles.Simulation.WorldBalanceConfiguration balance)
    {
        if (_runner is { } current)
        {
            current.ObservationInvalidated -= Runner_ObservationInvalidated;
            current.Faulted -= Runner_Faulted;
        }

        _runner = replacement;
        _worldSeed = seed;
        _activeBalance = balance;
        var metadata = replacement.GetWorldMetadata();
        SettingsWorkspacePanel.SetCurrentWorldSettings(
            metadata.Seed,
            metadata.Population,
            metadata.Balance);
        _speed = 1;
        _selectedAgent = null;
        _latestAgent = null;
        _latestSnapshot = null;
        _explorerOffset = 0;
        _projectedRecentEventSequence = long.MinValue;
        _projectedWorldChronicleSequence = long.MinValue;
        _projectedReincarnationRevision = long.MinValue;
        Speed1Toggle.IsChecked = true;
        replacement.ObservationInvalidated += Runner_ObservationInvalidated;
        replacement.Faulted += Runner_Faulted;
        replacement.SetPerformanceDiagnosticsEnabled(
            _workspaceRoute == WorkspaceRoute.Settings
            && SettingsWorkspacePanel.IsPerformanceMeterEnabled);
        replacement.Start();
        _logService.AddDebug(
            DebugLogLevel.Information,
            $"새 세계 생성 · AI {metadata.Population:N0}명 · seed {seed} · " +
            (balance.DeveloperModeEnabled ? "개발자 밸런스" : "일반 밸런스"));
        RefreshVisibleObservation();
    }
}
