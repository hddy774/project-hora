using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Chronicles.Simulation;

namespace Chronicles.Desktop;

public partial class MainWindow
{
    private void AutoPersonalOutputCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        var enabled = AutoPersonalOutputCheckBox.IsChecked == true;
        if (_logService.AutoPersonalOutput != enabled)
        {
            _logService.SetAutoPersonalOutput(enabled);
        }

        SetPersonalRecordOutputStatus(
            enabled ? "선택 인물 자동 기록 중" : "개인 자동 기록 꺼짐");
        if (!_applyingObservation && _runner is not null)
        {
            RefreshVisibleObservation();
        }
    }

    private async void PersonalRecordExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_runner is null || _selectedAgent is not { } selectedAgent)
        {
            SetPersonalRecordOutputStatus("먼저 인물을 선택해 주세요.", isError: true);
            return;
        }

        try
        {
            SetPersonalRecordOutputStatus("개인 기록 출력 중…");
            var observation = _runner.GetObservation(
                selectedAgent,
                rankingCount: 1,
                eventCount: 0,
                sections: ObservationSections.PersonalLog
                    | ObservationSections.Chronicle
                    | ObservationSections.PersonalCenturyChronicle,
                worldChronicleCount: 0);
            var agent = observation.SelectedAgent;
            var recent = observation.SelectedPersonalLog
                .Select(entry => FormatEventRowFileLine(FormatPersonalLog(agent, entry)))
                .ToArray();
            var detailed = observation.SelectedChronicle
                .Select(entry => FormatEventRowFileLine(FormatChronicle(entry)))
                .ToArray();
            var century = observation.SelectedPersonalCenturyChronicle
                .Select(entry => FormatEventRowFileLine(FormatPersonalCenturyChronicle(entry)))
                .ToArray();
            var path = await _logService.ExportPersonalAsync(
                agent.Id,
                agent.Name,
                agent.LifeNumber,
                recent,
                detailed,
                century);
            _logService.AddDebug(
                DebugLogLevel.Information,
                $"개인 기록 수동 출력 완료 · {agent.Id} · {path}");
            SetPersonalRecordOutputStatus($"완료 · {Path.GetFileName(path)}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logService.AddDebug(
                DebugLogLevel.Error,
                $"개인 기록 수동 출력 실패 · {exception.Message}");
            SetPersonalRecordOutputStatus($"출력 실패 · {exception.Message}", isError: true);
        }
    }

    private void PersonalRecordFolderButton_Click(object sender, RoutedEventArgs e) =>
        OpenLogOutputFolder(SetPersonalRecordOutputStatus, "개인 기록");

    private void OpenLogOutputFolder(Action<string, bool> setStatus, string source)
    {
        ArgumentNullException.ThrowIfNull(setStatus);
        try
        {
            var directory = GameLogService.EnsureOutputDirectory();
            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true,
            });
            _logService.AddDebug(DebugLogLevel.Information, $"{source} 로그 폴더 열기 · {directory}");
            setStatus("로그 폴더 열림", false);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or Win32Exception
            or InvalidOperationException)
        {
            _logService.AddDebug(
                DebugLogLevel.Error,
                $"{source} 로그 폴더 열기 실패 · {exception.Message}");
            setStatus($"폴더 열기 실패 · {exception.Message}", true);
        }
    }

    private void SetPersonalRecordOutputStatus(string text, bool isError = false)
    {
        PersonalRecordOutputStatusText.Text = text;
        PersonalRecordOutputStatusText.Foreground = (Brush)FindResource(
            isError ? "DangerBrush" : "SecondaryTextBrush");
    }
}
