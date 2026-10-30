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

        ApplyWorkspaceRoute(route);
    }

    private void ApplyWorkspaceRoute(WorkspaceRoute route)
    {
        _workspaceRoute = route;
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
                    SelectAgentDetailSection(AgentDetailSection.Overview);
                    break;
                case WorkspaceRoute.Relations:
                    WorldInfoTabControl.SelectedIndex = 0;
                    SelectAgentDetailSection(AgentDetailSection.Social);
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
        ApplyWorkspaceRoute(WorkspaceRoute.People);
    }

    private void ContentWorkspacePanel_PageRequested(object? sender, EventArgs e) =>
        RefreshVisibleObservation();

    private void ConfigureWorkspaceLayout(WorkspaceRoute route)
    {
        var personWorkspace = route is WorkspaceRoute.People or WorkspaceRoute.Relations;
        WorkspaceListPanel.Visibility = personWorkspace ? Visibility.Visible : Visibility.Collapsed;
        WorkspaceDetailPanel.Visibility = personWorkspace ? Visibility.Visible : Visibility.Collapsed;
        WorkspaceContextPanel.Visibility = personWorkspace ? Visibility.Collapsed : Visibility.Visible;
        WorkspaceListColumn.Width = personWorkspace
            ? new GridLength(470)
            : new GridLength(0);
        WorkspaceListGapColumn.Width = personWorkspace
            ? new GridLength(14)
            : new GridLength(0);
        WorkspaceDetailColumn.Width = personWorkspace
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
        WorkspaceContextGapColumn.Width = new GridLength(0);
        WorkspaceContextColumn.Width = personWorkspace
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
    }

}
