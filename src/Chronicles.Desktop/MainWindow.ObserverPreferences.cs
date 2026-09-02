using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Chronicles.Application;
using Chronicles.Core;
using Chronicles.Simulation;

namespace Chronicles.Desktop;

public partial class MainWindow
{
    private readonly HashSet<int> _watchlistedAgentIds = [];
    private ObserverUiPreferencesLoadResult? _observerPreferencesLoad;
    private double _uiScale = 1d;
    private double _restoredAgentScrollOffset;
    private bool _watchlistMode;
    private bool _peopleStatisticsMode;
    private bool _restoringObserverUi;
    private bool _observerPreferencesSaved;

    private void LoadObserverUiPreferences()
    {
        _observerPreferencesLoad = ObserverUiPreferencesStore.Load();
        var preferences = _observerPreferencesLoad.Preferences;
        _uiScale = preferences.UiScale;
        _windowDisplayMode = StringComparer.OrdinalIgnoreCase.Equals(
            preferences.WindowDisplayMode,
            "FullScreen")
                ? WindowDisplayMode.FullScreen
                : WindowDisplayMode.Windowed;
        SettingsWorkspacePanel.SetUiScale(_uiScale, _uiScale);
        SettingsWorkspacePanel.SetObserverUiState(
            _observerPreferencesLoad.Status,
            _observerPreferencesLoad.Message,
            _observerPreferencesLoad.Path);
        _workspaceRoute = Enum.TryParse<WorkspaceRoute>(
            preferences.ActiveWorkspace,
            ignoreCase: true,
            out var route)
                ? route
                : WorkspaceRoute.World;
        _selectedAgent = preferences.SelectedAgentId is { } selected
            ? new AgentId(selected)
            : null;
        _explorerOffset = preferences.ExplorerOffset;
        SearchTextBox.Text = preferences.ExplorerSearch;
        _agentDetailSection = Enum.TryParse<AgentDetailSection>(
            preferences.AgentDetailSection,
            ignoreCase: true,
            out var detailSection)
                ? detailSection
                : AgentDetailSection.Overview;
        _restoredAgentScrollOffset = preferences.AgentDetailScrollOffset;
        _compactPersonShowsDetail = preferences.CompactPersonShowsDetail;
        RestorePeopleListMode(preferences.PeopleListMode);
        _watchlistedAgentIds.Clear();
        foreach (var agentId in preferences.WatchlistedAgentIds)
        {
            _watchlistedAgentIds.Add(agentId);
        }
        SaveWorkspacePanel.RestoreUiState(
            preferences.SelectedSaveSlotId,
            preferences.SaveWorkspaceTabIndex);
    }

    private void ApplyChronicleRetentionWatchlist(GameApplicationSession session)
    {
        var population = session.GetWorldMetadata().Population;
        foreach (var value in _watchlistedAgentIds)
        {
            if ((uint)value < (uint)population)
            {
                session.SetDetailedChronicleProtection(new AgentId(value), isProtected: true);
            }
        }
    }

    private void RestoreChronicleRetentionWatchlist(GameApplicationSession session)
    {
        _watchlistedAgentIds.Clear();
        foreach (var agentId in session.GetDetailedChronicleProtectionList())
        {
            _watchlistedAgentIds.Add(agentId.Value);
        }
    }

    private void ApplyRestoredObserverUiState()
    {
        if (_observerPreferencesLoad is null)
        {
            return;
        }

        var population = _runner?.GetWorldMetadata().Population ?? 0;
        if (_selectedAgent is { } selected && (uint)selected.Value >= (uint)population)
        {
            _selectedAgent = null;
        }
        _watchlistedAgentIds.RemoveWhere(item => (uint)item >= (uint)population);
        ApplyPeopleListModeControls();
        SelectWorkspaceNavigationToggle(_workspaceRoute);
        _restoringObserverUi = true;
        try
        {
            ApplyWorkspaceRoute(_workspaceRoute);
            if (_workspaceRoute == WorkspaceRoute.People)
            {
                SelectAgentDetailSection(_agentDetailSection);
                RefreshVisibleObservation();
            }
        }
        finally
        {
            _restoringObserverUi = false;
        }

        if (_workspaceRoute is WorkspaceRoute.People or WorkspaceRoute.Relations
            && _restoredAgentScrollOffset > 0d)
        {
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() => AgentDetailScrollViewer.ScrollToVerticalOffset(
                    _restoredAgentScrollOffset)));
        }

        var level = _observerPreferencesLoad.Status is ObserverUiPreferencesLoadStatus.Invalid
            or ObserverUiPreferencesLoadStatus.Incompatible
                ? DebugLogLevel.Warning
                : DebugLogLevel.Information;
        _logService.AddDebug(level, _observerPreferencesLoad.Message);
    }

    private void SaveObserverUiPreferences()
    {
        if (_observerPreferencesSaved)
        {
            return;
        }

        _observerPreferencesSaved = true;
        try
        {
            var preferences = new ObserverUiPreferences(
                ObserverUiPreferences.CurrentSchemaVersion,
                _workspaceRoute.ToString(),
                _uiScale,
                _selectedAgent?.Value,
                GetPeopleListModeName(),
                _explorerOffset,
                SearchTextBox.Text,
                _agentDetailSection.ToString(),
                AgentDetailScrollViewer.VerticalOffset,
                _compactPersonShowsDetail,
                SaveWorkspacePanel.SelectedSlotId,
                SaveWorkspacePanel.SelectedWorkspaceTabIndex,
                _watchlistedAgentIds.Order().ToArray(),
                _windowDisplayMode.ToString());
            ObserverUiPreferencesStore.Save(preferences);
            _logService.AddDebug(
                DebugLogLevel.Information,
                $"관찰 UI 상태 저장 · {ObserverUiPreferencesStore.DefaultPath}");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or NotSupportedException)
        {
            _logService.AddDebug(
                DebugLogLevel.Warning,
                $"관찰 UI 상태 저장 실패 · {exception.Message}");
        }
    }

    private void SelectWorkspaceNavigationToggle(WorkspaceRoute route)
    {
        foreach (var toggle in WorkspaceNavigationPanel.Children.OfType<RadioButton>())
        {
            toggle.IsChecked = toggle.Tag is string tag
                && StringComparer.OrdinalIgnoreCase.Equals(tag, route.ToString());
        }
    }

    private void RestorePeopleListMode(string mode)
    {
        _peopleStatisticsMode = StringComparer.OrdinalIgnoreCase.Equals(mode, "Statistics");
        if (_peopleStatisticsMode)
        {
            _watchlistMode = false;
            _rankingCategory = null;
            return;
        }

        _watchlistMode = StringComparer.OrdinalIgnoreCase.Equals(mode, "Watchlist");
        if (_watchlistMode)
        {
            _rankingCategory = null;
            return;
        }

        _rankingCategory = StringComparer.OrdinalIgnoreCase.Equals(mode, "Explorer")
            ? null
            : Enum.TryParse<RankingCategory>(mode, ignoreCase: true, out var category)
                ? category
                : RankingCategory.CombatPower;
    }

    private string GetPeopleListModeName() => _peopleStatisticsMode
        ? "Statistics"
        : _watchlistMode
            ? "Watchlist"
            : _rankingCategory?.ToString() ?? "Explorer";
}
