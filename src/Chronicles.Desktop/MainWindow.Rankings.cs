using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Chronicles.Core;
using Chronicles.Simulation;
using static Chronicles.Presentation.PresentationText;

namespace Chronicles.Desktop;

public partial class MainWindow
{
    private int _peopleListResultCount;

    private void UpdateRankingRows(WorldSnapshot snapshot)
    {
        if (_peopleStatisticsMode)
        {
            var metrics = ObservationStatistics.CreatePeople(snapshot);
            RankingScopeText.Text = $"현재 {snapshot.Population:N0}명";
            PeopleStatisticsPanel.Apply(
                "세계 인물 통계",
                "범인과 수선자 인구, 생애·전투·경제 누계를 함께 비교합니다. 이 화면에서는 랭킹 정렬과 인물 행을 만들지 않습니다.",
                metrics,
                ObservationStatistics.CreateRevision(metrics));
            return;
        }

        RankingRow[] rows;
        var search = SearchTextBox.Text.Trim();
        if (_rankingCategory is { } category)
        {
            var rankings = category switch
            {
                RankingCategory.CombatPower => snapshot.CombatPowerRankings,
                RankingCategory.Realm => snapshot.RealmRankings,
                RankingCategory.Aptitude => snapshot.AptitudeRankings,
                RankingCategory.Wealth => snapshot.WealthRankings,
                _ => throw new InvalidOperationException($"지원하지 않는 랭킹 분류입니다: {category}"),
            };
            rows = rankings.Select(ToRankingRow).ToArray();
            RankingScopeText.Text = $"TOP {rankings.Count:N0}";
            ExplorerNavigationPanel.Visibility = Visibility.Collapsed;
        }
        else if (_runner is null)
        {
            rows = Array.Empty<RankingRow>();
        }
        else if (_watchlistMode)
        {
            var watchlistIds = _watchlistedAgentIds
                .Order()
                .Select(value => new AgentId(value))
                .ToArray();
            var page = _runner.BrowseAgentsById(
                watchlistIds,
                search,
                _explorerOffset,
                ExplorerPageSize);
            if (_explorerOffset >= page.TotalCount && _explorerOffset > 0)
            {
                _explorerOffset = Math.Max(
                    0,
                    ((Math.Max(0, page.TotalCount - 1)) / ExplorerPageSize) * ExplorerPageSize);
                page = _runner.BrowseAgentsById(
                    watchlistIds,
                    search,
                    _explorerOffset,
                    ExplorerPageSize);
            }

            rows = page.Agents.Select(ToRankingRow).ToArray();
            _peopleListResultCount = page.TotalCount;
            if (page.TotalCount == 0)
            {
                RankingScopeText.Text = search.Length == 0
                    ? "관심 목록 비어 있음"
                    : "관심 목록 검색 결과 없음";
                ExplorerNavigationPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                var first = _explorerOffset + 1;
                var last = _explorerOffset + rows.Length;
                RankingScopeText.Text = $"관심 {first:N0}–{last:N0} / {page.TotalCount:N0}명";
                UpdatePeopleListNavigation(page.TotalCount);
            }
        }
        else
        {
            var maximumOffset = Math.Max(0, ((snapshot.Population - 1) / ExplorerPageSize) * ExplorerPageSize);
            _explorerOffset = Math.Min(_explorerOffset, maximumOffset);
            var agents = search.Length == 0
                ? _runner.BrowseAgents(_explorerOffset, ExplorerPageSize)
                : _runner.SearchAgents(search, 1_000);
            rows = agents.Select(ToRankingRow).ToArray();

            if (search.Length == 0)
            {
                _peopleListResultCount = snapshot.Population;
                var first = rows.Length == 0 ? 0 : _explorerOffset + 1;
                var last = _explorerOffset + rows.Length;
                RankingScopeText.Text = $"{first:N0}–{last:N0} / {snapshot.Population:N0}";
                UpdatePeopleListNavigation(snapshot.Population);
            }
            else
            {
                _peopleListResultCount = rows.Length;
                RankingScopeText.Text = rows.Length == 0
                    ? "검색 결과 없음"
                    : $"검색 {rows.Length:N0}명";
                ExplorerNavigationPanel.Visibility = Visibility.Collapsed;
            }
        }

        RankingGrid.ItemsSource = rows;
        if (_selectedAgent is { } selected)
        {
            RankingGrid.SelectedItem = rows.FirstOrDefault(item => item.AgentId == selected);
        }
    }

    private void UpdateRankingMovementRows(WorldSnapshot snapshot)
    {
        var windows = (snapshot.Statistics.RankingMovementWindows
                ?? Array.Empty<RankingMovementWindowSnapshot>())
            .Where(item => item.Period == _rankingMovementPeriod)
            .ToArray();
        if (windows.Length == 0)
        {
            RankingMovementPeriodText.Text = "선택 기간의 랭킹 이력이 아직 없습니다.";
            RankingMovementGrid.ItemsSource = Array.Empty<RankingMovementRow>();
            return;
        }

        var first = windows[0];
        var elapsedYears = (first.ToTick.Value - first.FromTick.Value)
            / (decimal)GameCalendar.TicksPerYear;
        RankingMovementPeriodText.Text =
            $"{FormatRankingPeriod(_rankingMovementPeriod)} · 실제 {elapsedYears:0.##}년 · " +
            $"연간 표본 {first.ObservationIntervalCount:N0}회 · " +
            $"{FormatGameDate(GameCalendar.GetDate(first.FromTick))}부터";
        RankingMovementGrid.ItemsSource = windows.Select(item =>
        {
            var cutoff = _rankingMovementCategory switch
            {
                RankingCategory.CombatPower => KoreanNumberFormatter.Format(item.CombatPowerCutoff),
                RankingCategory.Realm => item.HasRealmCutoff
                    ? FormatRealm(item.RealmCutoff, item.RealmLayerCutoff)
                    : "미입문",
                RankingCategory.Wealth => $"{KoreanNumberFormatter.Format(item.WealthCutoff)} 영석",
                _ => throw new InvalidOperationException("지원하지 않는 변동 랭킹 분류입니다."),
            };
            var (changed, basisPoints) = _rankingMovementCategory switch
            {
                RankingCategory.CombatPower => (
                    item.CombatPowerChangedCount,
                    item.CombatPowerChangeBasisPoints),
                RankingCategory.Realm => (item.RealmChangedCount, item.RealmChangeBasisPoints),
                RankingCategory.Wealth => (item.WealthChangedCount, item.WealthChangeBasisPoints),
                _ => throw new InvalidOperationException("지원하지 않는 변동 랭킹 분류입니다."),
            };
            return new RankingMovementRow(
                $"상위 {item.TopCount:N0}위",
                cutoff,
                $"{changed:N0}회 · 연간 {basisPoints / 100m:0.##}%");
        }).ToArray();
    }

    private RankingRow ToRankingRow(RankingEntry entry) => new(
        entry.Rank,
        entry.AgentId,
        entry.Name,
        entry.HasEnteredCultivation
            ? FormatRealm(entry.RealmStage, entry.RealmLayer, entry.GreatPerfectionAchieved)
            : "미입문 · 입문 수련",
        entry.Category switch
        {
            RankingCategory.CombatPower => KoreanNumberFormatter.Format(entry.CombatPower.Total),
            RankingCategory.Realm => KoreanNumberFormatter.Format(entry.Score % 1_000_000_000L),
            RankingCategory.Aptitude => (entry.AptitudeScore / 100m).ToString("0.##", CultureInfo.CurrentCulture),
            RankingCategory.Wealth => KoreanNumberFormatter.Format(entry.Score),
            _ => entry.Score.ToString(CultureInfo.CurrentCulture),
        },
        FormatActivity(entry.Activity));

    private RankingRow ToRankingRow(AgentSnapshot agent) => new(
        agent.Rank,
        agent.Id,
        agent.Name,
        HasEnteredCultivation(agent.CultivationState, agent.BottleneckCause)
            ? FormatRealm(
                agent.RealmStage,
                agent.RealmLayer,
                agent.Cultivation.GreatPerfectionAchieved)
            : "미입문 · 입문 수련",
        KoreanNumberFormatter.Format(agent.CombatPower.Total),
        FormatActivity(agent.Activity));

    private static string FormatRankingPeriod(RankingMovementPeriod period) => period switch
    {
        RankingMovementPeriod.OneYear => "최근 1년",
        RankingMovementPeriod.TenYears => "최근 10년",
        RankingMovementPeriod.HundredYears => "최근 100년",
        RankingMovementPeriod.ThousandYears => "최근 1,000년",
        RankingMovementPeriod.TenThousandYears => "최근 1만년",
        RankingMovementPeriod.All => "전체 누적",
        _ => period.ToString(),
    };

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_rankingCategory is null && _latestSnapshot is not null)
        {
            _explorerOffset = 0;
            UpdateRankingRows(_latestSnapshot);
        }
    }

    private void CombatPowerRankingButton_Click(object sender, RoutedEventArgs e) =>
        SetListMode(RankingCategory.CombatPower);

    private void RealmRankingButton_Click(object sender, RoutedEventArgs e) =>
        SetListMode(RankingCategory.Realm);

    private void AptitudeRankingButton_Click(object sender, RoutedEventArgs e) =>
        SetListMode(RankingCategory.Aptitude);

    private void WealthRankingButton_Click(object sender, RoutedEventArgs e) =>
        SetListMode(RankingCategory.Wealth);

    private void ExplorerButton_Click(object sender, RoutedEventArgs e) =>
        SetListMode(null);

    private void WatchlistButton_Click(object sender, RoutedEventArgs e) =>
        SetWatchlistMode();

    private void RankingPeriodButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string value }
            && Enum.TryParse(value, out RankingMovementPeriod period))
        {
            _rankingMovementPeriod = period;
            if (_latestSnapshot is { } snapshot)
            {
                UpdateRankingMovementRows(snapshot);
            }
        }
    }

    private void MovementCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string value }
            && Enum.TryParse(value, out RankingCategory category)
            && category is RankingCategory.CombatPower or RankingCategory.Realm or RankingCategory.Wealth)
        {
            _rankingMovementCategory = category;
            if (_latestSnapshot is { } snapshot)
            {
                UpdateRankingMovementRows(snapshot);
            }
        }
    }

    private void ExplorerPreviousButton_Click(object sender, RoutedEventArgs e)
    {
        _explorerOffset = Math.Max(0, _explorerOffset - ExplorerPageSize);
        if (_latestSnapshot is not null)
        {
            UpdateRankingRows(_latestSnapshot);
        }
    }

    private void ExplorerNextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_latestSnapshot is null
            || _explorerOffset + ExplorerPageSize >= _peopleListResultCount)
        {
            return;
        }

        _explorerOffset += ExplorerPageSize;
        UpdateRankingRows(_latestSnapshot);
    }

    private void SetListMode(RankingCategory? category)
    {
        _peopleStatisticsMode = false;
        _watchlistMode = false;
        _rankingCategory = category;
        _explorerOffset = 0;
        if (category is not null && SearchTextBox.Text.Length > 0)
        {
            SearchTextBox.Clear();
        }

        ApplyPeopleListModeControls();
        ConfigureWorkspaceLayout(_workspaceRoute);
        RefreshVisibleObservation();
    }

    private void SetWatchlistMode()
    {
        _peopleStatisticsMode = false;
        _watchlistMode = true;
        _rankingCategory = null;
        _explorerOffset = 0;
        ApplyPeopleListModeControls();
        ConfigureWorkspaceLayout(_workspaceRoute);
        RefreshVisibleObservation();
    }

    private void PeopleStatisticsButton_Click(object sender, RoutedEventArgs e)
    {
        _peopleStatisticsMode = true;
        _watchlistMode = false;
        _rankingCategory = null;
        _explorerOffset = 0;
        ApplyPeopleListModeControls();
        ConfigureWorkspaceLayout(_workspaceRoute);
        RefreshVisibleObservation();
    }

    private void ApplyPeopleListModeControls()
    {
        CombatPowerRankingToggle.IsChecked = !_peopleStatisticsMode && !_watchlistMode
            && _rankingCategory == RankingCategory.CombatPower;
        RealmRankingToggle.IsChecked = !_peopleStatisticsMode && !_watchlistMode
            && _rankingCategory == RankingCategory.Realm;
        AptitudeRankingToggle.IsChecked = !_peopleStatisticsMode && !_watchlistMode
            && _rankingCategory == RankingCategory.Aptitude;
        WealthRankingToggle.IsChecked = !_peopleStatisticsMode && !_watchlistMode
            && _rankingCategory == RankingCategory.Wealth;
        ExplorerRankingToggle.IsChecked = !_peopleStatisticsMode
            && !_watchlistMode
            && _rankingCategory is null;
        WatchlistRankingToggle.IsChecked = !_peopleStatisticsMode && _watchlistMode;
        PeopleStatisticsToggle.IsChecked = _peopleStatisticsMode;
        SearchTextBox.IsEnabled = !_peopleStatisticsMode && _rankingCategory is null;
        SearchTextBox.Visibility = _peopleStatisticsMode
            ? Visibility.Collapsed
            : Visibility.Visible;
        RankingGrid.Visibility = _peopleStatisticsMode
            ? Visibility.Collapsed
            : Visibility.Visible;
        PeopleStatisticsPanel.Visibility = _peopleStatisticsMode
            ? Visibility.Visible
            : Visibility.Collapsed;
        if (_peopleStatisticsMode)
        {
            ExplorerNavigationPanel.Visibility = Visibility.Collapsed;
        }

        RankingTitleText.Text = _peopleStatisticsMode
            ? "전체 인물 통계"
            : _watchlistMode
            ? "관심 인물 목록"
            : _rankingCategory switch
        {
            RankingCategory.CombatPower => "전투력 랭킹",
            RankingCategory.Realm => "경지 랭킹",
            RankingCategory.Aptitude => "자질 랭킹",
            RankingCategory.Wealth => "부자 순위",
            null => "전체 수선자 탐색",
            _ => throw new ArgumentOutOfRangeException(nameof(_rankingCategory)),
        };
        RankingGrid.Columns[3].Header = _rankingCategory switch
        {
            RankingCategory.CombatPower => "전투력",
            RankingCategory.Realm => "수행량",
            RankingCategory.Aptitude => "자질 합계",
            RankingCategory.Wealth => "보유 영석",
            null => "전투력",
            _ => throw new ArgumentOutOfRangeException(nameof(_rankingCategory)),
        };
    }

    private void UpdatePeopleListNavigation(int totalCount)
    {
        var currentPage = (_explorerOffset / ExplorerPageSize) + 1;
        var pageCount = Math.Max(1, (totalCount + ExplorerPageSize - 1) / ExplorerPageSize);
        ExplorerPageText.Text = $"{currentPage:N0} / {pageCount:N0} 페이지";
        ExplorerPreviousButton.IsEnabled = _explorerOffset > 0;
        ExplorerNextButton.IsEnabled = _explorerOffset + ExplorerPageSize < totalCount;
        ExplorerNavigationPanel.Visibility = Visibility.Visible;
    }

    private void SelectedAgentWatchlistToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedAgent is not { } selected)
        {
            return;
        }

        if (!_watchlistedAgentIds.Add(selected.Value))
        {
            _watchlistedAgentIds.Remove(selected.Value);
        }
        UpdateWatchlistToggle(selected);
        if (_watchlistMode && _latestSnapshot is { } snapshot)
        {
            UpdateRankingRows(snapshot);
        }
    }

    private void UpdateWatchlistToggle(AgentId agentId)
    {
        var watched = _watchlistedAgentIds.Contains(agentId.Value);
        SelectedAgentWatchlistToggle.IsChecked = watched;
        SelectedAgentWatchlistToggle.Content = watched ? "★ 관심 등록됨" : "☆ 관심 등록";
        SelectedAgentWatchlistToggle.ToolTip = watched
            ? "이 인물을 관심 목록에서 제거합니다."
            : "이 인물을 관심 목록에 추가합니다.";
    }
}
