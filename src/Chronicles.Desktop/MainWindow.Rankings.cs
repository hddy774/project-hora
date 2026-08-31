using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Chronicles.Core;
using Chronicles.Simulation;
using static Chronicles.Desktop.PresentationText;

namespace Chronicles.Desktop;

public partial class MainWindow
{
    private void UpdateRankingRows(WorldSnapshot snapshot)
    {
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
                var first = rows.Length == 0 ? 0 : _explorerOffset + 1;
                var last = _explorerOffset + rows.Length;
                RankingScopeText.Text = $"{first:N0}–{last:N0} / {snapshot.Population:N0}";
                var currentPage = (_explorerOffset / ExplorerPageSize) + 1;
                var pageCount = Math.Max(1, (snapshot.Population + ExplorerPageSize - 1) / ExplorerPageSize);
                ExplorerPageText.Text = $"{currentPage:N0} / {pageCount:N0} 페이지";
                ExplorerPreviousButton.IsEnabled = _explorerOffset > 0;
                ExplorerNextButton.IsEnabled = _explorerOffset + ExplorerPageSize < snapshot.Population;
                ExplorerNavigationPanel.Visibility = Visibility.Visible;
            }
            else
            {
                RankingScopeText.Text = $"검색 {rows.Length:N0}명";
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
                $"{changed:N0}자리 · {basisPoints / 100m:0.##}%");
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
            || _explorerOffset + ExplorerPageSize >= _latestSnapshot.Population)
        {
            return;
        }

        _explorerOffset += ExplorerPageSize;
        UpdateRankingRows(_latestSnapshot);
    }

    private void SetListMode(RankingCategory? category)
    {
        _rankingCategory = category;
        _explorerOffset = 0;
        var explorer = category is null;
        SearchTextBox.IsEnabled = explorer;
        if (!explorer && SearchTextBox.Text.Length > 0)
        {
            SearchTextBox.Clear();
        }

        RankingTitleText.Text = category switch
        {
            RankingCategory.CombatPower => "전투력 랭킹",
            RankingCategory.Realm => "경지 랭킹",
            RankingCategory.Aptitude => "자질 랭킹",
            RankingCategory.Wealth => "부자 순위",
            null => "전체 수선자 탐색",
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };
        RankingGrid.Columns[3].Header = category switch
        {
            RankingCategory.CombatPower => "전투력",
            RankingCategory.Realm => "수행량",
            RankingCategory.Aptitude => "자질 합계",
            RankingCategory.Wealth => "보유 영석",
            null => "전투력",
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };

        RefreshVisibleObservation();
    }
}
