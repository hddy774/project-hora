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
}

public partial class MainWindow
{
    private bool _applyingWorkspaceRoute;

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
        var isAvailable = route is not (WorkspaceRoute.Organizations or WorkspaceRoute.Content);
        DashboardWorkspaceGrid.Visibility = isAvailable ? Visibility.Visible : Visibility.Collapsed;
        WorkspaceUnavailablePanel.Visibility = isAvailable ? Visibility.Collapsed : Visibility.Visible;

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

    private void ShowUnavailableWorkspace(string title, string description)
    {
        WorkspaceUnavailableTitleText.Text = title;
        WorkspaceUnavailableDescriptionText.Text = description;
    }
}
