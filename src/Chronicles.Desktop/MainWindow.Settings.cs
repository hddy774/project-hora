using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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

    private void SettingsWorkspacePanel_PerformanceSampleRecorded(
        object? sender,
        PerformanceSampleRecordedEventArgs e) =>
        _logService.CapturePerformance(e.Line);

    private void SettingsWorkspacePanel_PerformanceAutoOutputChanged(
        object? sender,
        EventArgs e)
    {
        if (_logService.AutoPerformanceOutput != SettingsWorkspacePanel.AutoPerformanceOutput)
        {
            _logService.SetAutoPerformanceOutput(SettingsWorkspacePanel.AutoPerformanceOutput);
        }
        SettingsWorkspacePanel.SetPerformanceFileStatus(
            SettingsWorkspacePanel.AutoPerformanceOutput
                ? "성능·상태 로그 자동 기록 중"
                : "성능·상태 로그 자동 기록 꺼짐");
    }

    private async void SettingsWorkspacePanel_PerformanceExportRequested(
        object? sender,
        EventArgs e)
    {
        try
        {
            SettingsWorkspacePanel.SetPerformanceFileStatus("최근 성능·상태 로그 출력 중…");
            var lines = SettingsWorkspacePanel.GetPerformanceLogLines();
            var path = await _logService.ExportPerformanceAsync(lines);
            _logService.AddDebug(
                DebugLogLevel.Information,
                $"성능·상태 로그 수동 출력 완료 · {path}");
            SettingsWorkspacePanel.SetPerformanceFileStatus(
                $"완료 · {Path.GetFileName(path)} · 표본 {lines.Length:N0}개");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logService.AddDebug(
                DebugLogLevel.Error,
                $"성능·상태 로그 수동 출력 실패 · {exception.Message}");
            SettingsWorkspacePanel.SetPerformanceFileStatus(
                $"출력 실패 · {exception.Message}",
                isError: true);
        }
    }

    private void SettingsWorkspacePanel_PerformanceFolderOpenRequested(
        object? sender,
        EventArgs e)
    {
        try
        {
            var directory = GameLogService.EnsureOutputDirectory();
            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true,
            });
            _logService.AddDebug(DebugLogLevel.Information, $"성능 로그 폴더 열기 · {directory}");
            SettingsWorkspacePanel.SetPerformanceFileStatus("로그 폴더 열림");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or Win32Exception
            or InvalidOperationException)
        {
            _logService.AddDebug(
                DebugLogLevel.Error,
                $"성능 로그 폴더 열기 실패 · {exception.Message}");
            SettingsWorkspacePanel.SetPerformanceFileStatus(
                $"폴더 열기 실패 · {exception.Message}",
                isError: true);
        }
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
        _latestPersonalLog = Array.Empty<Chronicles.Simulation.PersonalLogEntry>();
        _latestChronicle = Array.Empty<Chronicles.Simulation.LifeChronicleEntry>();
        _latestPersonalCenturyChronicle =
            Array.Empty<Chronicles.Simulation.PersonalCenturyChronicleEntry>();
        _logService.ResetPersonalCaptureState();
        _explorerOffset = 0;
        _projectedRecentEventSequence = long.MinValue;
        _projectedWorldChronicleSequence = long.MinValue;
        _projectedReincarnationRevision = long.MinValue;
        SettingsWorkspacePanel.ResetPerformanceHistory();
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
            (balance.DeveloperModeEnabled ? "세계 설정 배율" : "기본 배율"));
        RefreshVisibleObservation();
    }
}
