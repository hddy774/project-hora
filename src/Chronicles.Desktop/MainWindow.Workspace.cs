using System.Windows;
using System.Windows.Controls;

namespace Chronicles.Desktop;

internal enum WorkspaceRoute : byte
{
    World,
    People,
    Relations,
    Map,
    Economy,
    Organizations,
    Content,
    Records,
    Diagnostics,
    Settings,
}

public partial class MainWindow
{
    private bool _applyingWorkspaceRoute;
    private WorkspaceRoute _workspaceRoute = WorkspaceRoute.World;

    private bool IsDashboardWorkspaceVisible =>
        DashboardWorkspaceGrid.Visibility == Visibility.Visible;

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
        var isAvailable = route is not (WorkspaceRoute.Organizations or WorkspaceRoute.Content);
        var isSettings = route == WorkspaceRoute.Settings;
        DashboardWorkspaceGrid.Visibility = isAvailable && !isSettings
            ? Visibility.Visible
            : Visibility.Collapsed;
        WorkspaceUnavailablePanel.Visibility = isAvailable ? Visibility.Collapsed : Visibility.Visible;
        SettingsWorkspacePanel.Visibility = isSettings ? Visibility.Visible : Visibility.Collapsed;
        _runner?.SetPerformanceDiagnosticsEnabled(
            isSettings && SettingsWorkspacePanel.IsPerformanceMeterEnabled);
        ConfigureWorkspaceLayout(route, isAvailable);

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
                    break;
                case WorkspaceRoute.Relations:
                    WorldInfoTabControl.SelectedIndex = 0;
                    RelationsExpander.IsExpanded = true;
                    break;
                case WorkspaceRoute.Map:
                    WorldInfoTabControl.SelectedIndex = 3;
                    break;
                case WorkspaceRoute.Economy:
                    WorldInfoTabControl.SelectedIndex = 2;
                    break;
                case WorkspaceRoute.Records:
                    WorldInfoTabControl.SelectedIndex = 4;
                    WorldRecordsPanel.SelectRecordTab(0);
                    break;
                case WorkspaceRoute.Diagnostics:
                    WorldInfoTabControl.SelectedIndex = 4;
                    WorldRecordsPanel.SelectRecordTab(2);
                    break;
                case WorkspaceRoute.Settings:
                    break;
                case WorkspaceRoute.Organizations:
                    ShowUnavailableWorkspace(
                        "조직 작업공간 · 준비 중",
                        "Organization Core와 종문·가문·왕조의 권위 상태는 0.0.20 이식 범위입니다. 현재 버전에는 표시할 실제 조직 데이터가 없습니다.");
                    break;
                case WorkspaceRoute.Content:
                    ShowUnavailableWorkspace(
                        "콘텐츠 작업공간 · 준비 중",
                        "비경·아이템 실물·서적 판본·지식·법칙 이해의 권위 상태는 0.0.21 이식 범위입니다. 현재 버전에는 표시할 실제 콘텐츠 데이터가 없습니다.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(route), route, null);
            }
        }
        finally
        {
            _applyingWorkspaceRoute = false;
        }

        RefreshVisibleObservation();
    }

    private void ConfigureWorkspaceLayout(WorkspaceRoute route, bool isAvailable)
    {
        if (!isAvailable)
        {
            return;
        }

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

    private void ShowUnavailableWorkspace(string title, string description)
    {
        WorkspaceUnavailableTitleText.Text = title;
        WorkspaceUnavailableDescriptionText.Text = description;
    }
}
