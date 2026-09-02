using System.Windows;
using System.Windows.Controls;
using Chronicles.Core;
using Chronicles.Simulation;
using static Chronicles.Presentation.PresentationText;

namespace Chronicles.Desktop;

public partial class MainWindow
{
    private void UpdateWorldSituation(WorldSnapshot snapshot)
    {
        var mortalPopulation = snapshot.Statistics.RealmDistribution
            .FirstOrDefault(item => item.Realm is null)
            .Count;
        var cultivatorPopulation = Math.Max(0, snapshot.Population - mortalPopulation);
        var flowDifference = snapshot.Market.RecentSupply - snapshot.Market.RecentDemand;
        var marketDescription = flowDifference switch
        {
            > 0 => $"최근 시장은 공급이 수요보다 {KoreanNumberFormatter.Format(flowDifference)} 많습니다.",
            < 0 => $"최근 시장은 수요가 공급보다 {KoreanNumberFormatter.Format(-flowDifference)} 많습니다.",
            _ => "최근 시장의 공급과 수요가 균형을 이루고 있습니다.",
        };
        var combatDescription = snapshot.ActiveCombats == 0
            ? "현재 진행 중인 전투는 없습니다."
            : $"현재 {KoreanNumberFormatter.Format(snapshot.ActiveCombats)}건의 전투가 이어지고 있습니다.";
        WorldSituationText.Text =
            $"현재 {KoreanNumberFormatter.Format(snapshot.Population)}명 가운데 " +
            $"범인 {KoreanNumberFormatter.Format(mortalPopulation)}명, " +
            $"수선자 {KoreanNumberFormatter.Format(cultivatorPopulation)}명입니다. " +
            $"{combatDescription} {marketDescription} " +
            $"지금까지 경지 돌파 {KoreanNumberFormatter.Format(snapshot.TotalBreakthroughs)}회, " +
            $"사망 {KoreanNumberFormatter.Format(snapshot.Statistics.TotalDeaths)}회, " +
            $"윤회 {KoreanNumberFormatter.Format(snapshot.Statistics.TotalReincarnations)}회가 기록되었습니다.";

        ApplyLeader(
            CombatPowerLeaderButton,
            CombatPowerLeaderText,
            GetFirst(snapshot.CombatPowerRankings),
            entry => KoreanNumberFormatter.Format(entry.CombatPower.Total));
        ApplyLeader(
            RealmLeaderButton,
            RealmLeaderText,
            GetFirst(snapshot.RealmRankings),
            entry => entry.HasEnteredCultivation
                ? FormatRealm(entry.RealmStage, entry.RealmLayer, entry.GreatPerfectionAchieved)
                : "범인");
        ApplyLeader(
            AptitudeLeaderButton,
            AptitudeLeaderText,
            GetFirst(snapshot.AptitudeRankings),
            entry => $"종합 {entry.AptitudeScore / 100m:0.00}");
        ApplyLeader(
            WealthLeaderButton,
            WealthLeaderText,
            GetFirst(snapshot.WealthRankings),
            entry => $"{KoreanNumberFormatter.Format(entry.Score)} 영석");
    }

    private static void ApplyLeader(
        Button button,
        TextBlock text,
        RankingEntry? leader,
        Func<RankingEntry, string> formatValue)
    {
        button.Tag = leader?.AgentId;
        button.IsEnabled = leader is not null;
        text.Text = leader is null
            ? "DataUnavailable"
            : $"{leader.Name} · {formatValue(leader)}";
    }

    private static RankingEntry? GetFirst(IReadOnlyList<RankingEntry> rankings) =>
        rankings.Count == 0 ? null : rankings[0];

    private void WorldLeaderButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AgentId agentId })
        {
            return;
        }

        _selectedAgent = agentId;
        _peopleStatisticsMode = false;
        _watchlistMode = false;
        _rankingCategory = RankingCategory.CombatPower;
        _compactPersonShowsDetail = true;
        ApplyPeopleListModeControls();
        ApplyWorkspaceRoute(WorkspaceRoute.People);
    }
}
