using System.Windows;
using System.Windows.Controls;

namespace Chronicles.Desktop;

internal enum WorkspaceRoute : byte
{
    World,
    People,
    Relations,
    Map,
    City,
    Economy,
    Organizations,
    Content,
    Records,
    Save,
    Diagnostics,
    Settings,
}

public partial class MainWindow
{
    private bool _applyingWorkspaceRoute;
    private WorkspaceRoute _workspaceRoute = WorkspaceRoute.World;
    private bool _compactPersonLayout;
    private bool _compactPersonShowsDetail;

    private bool IsDashboardWorkspaceVisible =>
        DashboardWorkspaceGrid.Visibility == Visibility.Visible;

    private bool IsObservationWorkspaceVisible =>
        IsDashboardWorkspaceVisible
        || OrganizationWorkspacePanel.Visibility == Visibility.Visible
        || ContentWorkspacePanel.Visibility == Visibility.Visible;

    private void WorkspaceNavigation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag }
            || !Enum.TryParse<WorkspaceRoute>(tag, ignoreCase: true, out var route))
        {
            return;
        }

        if (route == WorkspaceRoute.People)
        {
            _compactPersonShowsDetail = false;
        }
        else if (route == WorkspaceRoute.Relations)
        {
            _compactPersonShowsDetail = true;
        }
        ApplyWorkspaceRoute(route);
    }

    private void ApplyWorkspaceRoute(WorkspaceRoute route)
    {
        _workspaceRoute = route;
        SelectWorkspaceNavigationToggle(route);
        var isSettings = route == WorkspaceRoute.Settings;
        var isSave = route == WorkspaceRoute.Save;
        var isOrganizations = route == WorkspaceRoute.Organizations;
        var isContent = route == WorkspaceRoute.Content;
        DashboardWorkspaceGrid.Visibility = !isSettings && !isSave && !isOrganizations && !isContent
            ? Visibility.Visible
            : Visibility.Collapsed;
        SettingsWorkspacePanel.Visibility = isSettings ? Visibility.Visible : Visibility.Collapsed;
        SaveWorkspacePanel.Visibility = isSave ? Visibility.Visible : Visibility.Collapsed;
        OrganizationWorkspacePanel.Visibility = isOrganizations
            ? Visibility.Visible
            : Visibility.Collapsed;
        ContentWorkspacePanel.Visibility = isContent
            ? Visibility.Visible
            : Visibility.Collapsed;
        _runner?.SetPerformanceDiagnosticsEnabled(
            isSettings && SettingsWorkspacePanel.IsPerformanceMeterEnabled);
        ConfigureWorkspaceLayout(route);

        _applyingWorkspaceRoute = true;
        try
        {
            switch (route)
            {
                case WorkspaceRoute.World:
                    WorldInfoTabControl.SelectedIndex = 0;
                    break;
                case WorkspaceRoute.People:
                    WorldInfoTabControl.SelectedIndex = 0;
                    if (!_restoringObserverUi)
                    {
                        SelectAgentDetailSection(AgentDetailSection.Overview);
                    }
                    break;
                case WorkspaceRoute.Relations:
                    WorldInfoTabControl.SelectedIndex = 0;
                    if (!_restoringObserverUi)
                    {
                        SelectAgentDetailSection(AgentDetailSection.Social);
                    }
                    RelationsExpander.IsExpanded = true;
                    break;
                case WorkspaceRoute.Map:
                    WorldInfoTabControl.SelectedIndex = 3;
                    break;
                case WorkspaceRoute.City:
                    WorldInfoTabControl.SelectedIndex = 1;
                    break;
                case WorkspaceRoute.Economy:
                    WorldInfoTabControl.SelectedIndex = 2;
                    break;
                case WorkspaceRoute.Records:
                    WorldInfoTabControl.SelectedIndex = 4;
                    WorldRecordsPanel.SelectRecordTab(0);
                    break;
                case WorkspaceRoute.Save:
                    break;
                case WorkspaceRoute.Diagnostics:
                    WorldInfoTabControl.SelectedIndex = 4;
                    WorldRecordsPanel.SelectRecordTab(2);
                    break;
                case WorkspaceRoute.Settings:
                    break;
                case WorkspaceRoute.Organizations:
                    break;
                case WorkspaceRoute.Content:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(route), route, null);
            }
        }
        finally
        {
            _applyingWorkspaceRoute = false;
        }

        if (isSave)
        {
            _ = RefreshSaveCatalogAsync();
        }
        else
        {
            RefreshVisibleObservation();
        }
    }

    private void OrganizationWorkspacePanel_AgentSelected(
        object? sender,
        OrganizationAgentSelectedEventArgs e)
    {
        _selectedAgent = e.AgentId;
        _compactPersonShowsDetail = true;
        ApplyWorkspaceRoute(WorkspaceRoute.People);
    }

    private void ContentWorkspacePanel_PageRequested(object? sender, EventArgs e) =>
        RefreshVisibleObservation();

    private void ConfigureWorkspaceLayout(WorkspaceRoute route)
    {
        var personWorkspace = route is WorkspaceRoute.People or WorkspaceRoute.Relations;
        var windowWidth = ActualWidth > 0d ? ActualWidth : Width;
        var personLogicalWidth = windowWidth / Math.Max(0.9d, _uiScale) - 184d;
        _compactPersonLayout = personWorkspace && personLogicalWidth < 900d;
        CompactPersonNavigationPanel.Visibility = _compactPersonLayout
            ? Visibility.Visible
            : Visibility.Collapsed;
        Grid.SetColumn(WorkspaceListPanel, 0);
        Grid.SetColumn(WorkspaceDetailPanel, _compactPersonLayout ? 0 : 2);
        Grid.SetColumnSpan(WorkspaceListPanel, _compactPersonLayout ? 5 : 1);
        Grid.SetColumnSpan(WorkspaceDetailPanel, _compactPersonLayout ? 5 : 1);
        WorkspaceListPanel.Visibility = personWorkspace
            && (!_compactPersonLayout || !_compactPersonShowsDetail)
                ? Visibility.Visible
                : Visibility.Collapsed;
        WorkspaceDetailPanel.Visibility = personWorkspace
            && (!_compactPersonLayout || _compactPersonShowsDetail)
                ? Visibility.Visible
                : Visibility.Collapsed;
        WorkspaceContextPanel.Visibility = personWorkspace ? Visibility.Collapsed : Visibility.Visible;
        WorkspaceListColumn.Width = personWorkspace && !_compactPersonLayout
            ? new GridLength(470)
            : personWorkspace
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(0);
        WorkspaceListGapColumn.Width = personWorkspace && !_compactPersonLayout
            ? new GridLength(14)
            : new GridLength(0);
        WorkspaceDetailColumn.Width = personWorkspace && !_compactPersonLayout
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        WorkspaceContextGapColumn.Width = new GridLength(0);
        WorkspaceContextColumn.Width = personWorkspace
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
        CompactPersonListToggle.IsChecked = !_compactPersonShowsDetail;
        CompactPersonDetailToggle.IsChecked = _compactPersonShowsDetail;
    }

    private void CompactPersonListButton_Click(object sender, RoutedEventArgs e) =>
        ShowCompactPersonPage(showDetail: false);

    private void CompactPersonDetailButton_Click(object sender, RoutedEventArgs e) =>
        ShowCompactPersonPage(showDetail: true);

    private void ShowCompactPersonPage(bool showDetail)
    {
        _compactPersonShowsDetail = showDetail;
        if (_workspaceRoute is WorkspaceRoute.People or WorkspaceRoute.Relations)
        {
            ConfigureWorkspaceLayout(_workspaceRoute);
            if (showDetail)
            {
                RefreshVisibleObservation();
            }
        }
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_workspaceRoute is WorkspaceRoute.People or WorkspaceRoute.Relations)
        {
            ConfigureWorkspaceLayout(_workspaceRoute);
        }
    }

}
