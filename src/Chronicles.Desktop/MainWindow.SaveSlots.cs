using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Chronicles.Application;

namespace Chronicles.Desktop;

public partial class MainWindow
{
    private IReadOnlyList<SaveSlotCatalogEntry> _saveSlotCatalog = [];
    private int _saveCatalogRefreshInProgress;
    private int _saveSlotOperationInProgress;

    private void OpenSaveWorkspace()
    {
        SaveWorkspaceToggle.IsChecked = true;
        ApplyWorkspaceRoute(WorkspaceRoute.Save);
    }

    private async Task RefreshSaveCatalogAsync()
    {
        if (_runner is null
            || _workspaceRoute != WorkspaceRoute.Save
            || Interlocked.Exchange(ref _saveCatalogRefreshInProgress, 1) != 0)
        {
            return;
        }

        SaveWorkspacePanel.SetBusy(true, "9개 세이브 슬롯을 확인하는 중…");
        try
        {
            var entries = await _runner.ListSaveSlotsAsync();
            _saveSlotCatalog = entries;
            if (_workspaceRoute == WorkspaceRoute.Save)
            {
                SaveWorkspacePanel.Apply(entries);
                SaveWorkspacePanel.SetStatus("세이브 슬롯 확인 완료");
            }
        }
        catch (Exception exception) when (IsPersistenceException(exception))
        {
            SaveWorkspacePanel.SetStatus(
                $"세이브 목록 확인 실패 · {exception.Message}",
                isError: true);
            ShowPersistenceError(exception);
        }
        finally
        {
            Interlocked.Exchange(ref _saveCatalogRefreshInProgress, 0);
        }
    }

    private void SaveWorkspacePanel_RefreshRequested(object? sender, EventArgs e) =>
        _ = RefreshSaveCatalogAsync();

    private void SaveWorkspacePanel_RootFolderRequested(object? sender, EventArgs e) =>
        OpenSaveDirectory(GameApplicationSession.EnsureSaveRootDirectory(), "저장 루트");

    private void SaveWorkspacePanel_FolderRequested(
        object? sender,
        SaveSlotActionEventArgs e) =>
        OpenSaveDirectory(
            GameApplicationSession.EnsureSaveSlotDirectory(e.SlotId),
            SaveSlotRegistry.Get(e.SlotId).DisplayName);

    private async void SaveWorkspacePanel_SaveRequested(
        object? sender,
        SaveSlotActionEventArgs e)
    {
        if (_runner is null || Interlocked.Exchange(ref _saveSlotOperationInProgress, 1) != 0)
        {
            return;
        }

        var definition = SaveSlotRegistry.Get(e.SlotId);
        var occupied = _saveSlotCatalog.FirstOrDefault(entry =>
            StringComparer.Ordinal.Equals(entry.Definition.SlotId, e.SlotId))?.Status
            is not (null or SaveSlotCatalogStatus.Empty);
        if (occupied && MessageBox.Show(
                $"{definition.DisplayName}의 기존 세계를 현재 세계로 덮어쓸까요?\n" +
                "기존 primary는 backup 후보로 보존됩니다.",
                "세이브 덮어쓰기",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            Interlocked.Exchange(ref _saveSlotOperationInProgress, 0);
            SaveWorkspacePanel.SetStatus("덮어쓰기를 취소했습니다.");
            return;
        }

        var wasPaused = _runner.IsPaused;
        SetSimulationPausedForStorage(true);
        SaveWorkspacePanel.BeginOperation($"{definition.DisplayName}에 저장하는 중…");
        try
        {
            var progress = new Progress<SaveOperationProgress>(
                SaveWorkspacePanel.SetOperationProgress);
            var result = await _runner.SaveSlotAsync(e.SlotId, progress: progress);
            _logService.AddDebug(
                DebugLogLevel.Information,
                $"세계 저장 완료 · {definition.DisplayName} · schema {result.WorldSchema}/{result.CheckpointSchema} · seq {result.CreatedSequence}");
            SaveWorkspacePanel.SetStatus($"{definition.DisplayName} 저장 완료");
        }
        catch (Exception exception) when (IsPersistenceException(exception))
        {
            SaveWorkspacePanel.SetStatus(
                $"{definition.DisplayName} 저장 실패 · {exception.Message}",
                isError: true);
            ShowPersistenceError(exception);
        }
        finally
        {
            SetSimulationPausedForStorage(wasPaused);
            Interlocked.Exchange(ref _saveSlotOperationInProgress, 0);
            await RefreshSaveCatalogAsync();
        }
    }

    private async void SaveWorkspacePanel_LoadRequested(
        object? sender,
        SaveSlotActionEventArgs e)
    {
        if (_runner is null || Interlocked.Exchange(ref _saveSlotOperationInProgress, 1) != 0)
        {
            return;
        }

        var definition = SaveSlotRegistry.Get(e.SlotId);
        if (MessageBox.Show(
                $"현재 세계를 닫고 {definition.DisplayName}을(를) 불러올까요?\n" +
                "현재 세계는 자동 저장되지 않습니다.",
                "세이브 불러오기",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            Interlocked.Exchange(ref _saveSlotOperationInProgress, 0);
            SaveWorkspacePanel.SetStatus("불러오기를 취소했습니다.");
            return;
        }

        var wasPaused = _runner.IsPaused;
        SetSimulationPausedForStorage(true);
        SaveWorkspacePanel.BeginOperation($"{definition.DisplayName}을(를) 복원하는 중…");
        try
        {
            var progress = new Progress<SaveOperationProgress>(
                SaveWorkspacePanel.SetOperationProgress);
            var result = await _runner.LoadSlotAsync(e.SlotId, progress: progress);
            if (!result.IsSuccess)
            {
                throw new InvalidDataException(result.Message);
            }

            ResetObservationAfterLoad();
            _logService.AddDebug(
                DebugLogLevel.Information,
                $"세계 불러오기 완료 · {definition.DisplayName} · {result.CandidateKind} · schema {result.WorldSchema}/{result.CheckpointSchema}");
            SaveWorkspacePanel.SetStatus($"{definition.DisplayName} 불러오기 완료");
        }
        catch (Exception exception) when (IsPersistenceException(exception))
        {
            SaveWorkspacePanel.SetStatus(
                $"{definition.DisplayName} 불러오기 실패 · {exception.Message}",
                isError: true);
            ShowPersistenceError(exception);
        }
        finally
        {
            SetSimulationPausedForStorage(wasPaused);
            Interlocked.Exchange(ref _saveSlotOperationInProgress, 0);
            await RefreshSaveCatalogAsync();
        }
    }

    private void ResetObservationAfterLoad()
    {
        if (_runner is null)
        {
            return;
        }

        var metadata = _runner.GetWorldMetadata();
        _worldSeed = metadata.Seed;
        _activeBalance = metadata.Balance;
        _activeGeneration = metadata.Generation;
        SettingsWorkspacePanel.SetCurrentWorldSettings(
            metadata.Seed,
            metadata.Population,
            metadata.Balance,
            metadata.Generation);
        _selectedAgent = null;
        _watchlistedAgentIds.Clear();
        _latestAgent = null;
        _latestSnapshot = null;
        _latestPersonalLog = [];
        _latestChronicle = [];
        _latestPersonalCenturyChronicle = [];
        _explorerOffset = 0;
        _projectedRecentEventSequence = long.MinValue;
        _projectedRecentEventCount = -1;
        _projectedWorldLogCategory = null;
        _projectedWorldChronicleSequence = long.MinValue;
        _projectedWorldChronicleCount = -1;
        _projectedWorldChronicleCategory = null;
        _projectedDebugVersion = long.MinValue;
        ClearReincarnationView();
        OrganizationWorkspacePanel.ResetProjection();
        ContentWorkspacePanel.ResetProjection();
        _logService.ResetPersonalCaptureState();
        SettingsWorkspacePanel.ResetPerformanceHistory();
        RefreshVisibleObservation();
    }

    private void SetSimulationPausedForStorage(bool paused)
    {
        _runner?.SetPaused(paused);
        PauseButton.Content = paused ? "계속 진행" : "일시정지";
    }

    private void OpenSaveDirectory(string directory, string label)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true,
            });
            SaveWorkspacePanel.SetStatus($"{label} 폴더를 열었습니다.");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or Win32Exception
            or InvalidOperationException)
        {
            SaveWorkspacePanel.SetStatus(
                $"폴더 열기 실패 · {exception.Message}",
                isError: true);
            _logService.AddDebug(
                DebugLogLevel.Error,
                $"세이브 폴더 열기 실패 · {exception.Message}");
        }
    }

    private static bool IsPersistenceException(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or InvalidDataException
            or OperationCanceledException;
}
