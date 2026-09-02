using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using Chronicles.Core;
using Chronicles.Presentation;
using Chronicles.Simulation;

namespace Chronicles.Desktop;

public sealed class OrganizationAgentSelectedEventArgs(AgentId agentId) : EventArgs
{
    public AgentId AgentId { get; } = agentId;
}

public partial class OrganizationWorkspaceView : UserControl
{
    private WorldOrganizationSnapshot _snapshot = WorldOrganizationSnapshot.Empty;
    private OrganizationId? _selectedOrganizationId;
    private long _projectedRevision = long.MinValue;
    private bool _applying;

    public OrganizationWorkspaceView()
    {
        InitializeComponent();
    }

    public event EventHandler<OrganizationAgentSelectedEventArgs>? AgentSelected;

    public void ResetProjection()
    {
        _snapshot = WorldOrganizationSnapshot.Empty;
        _selectedOrganizationId = null;
        _projectedRevision = long.MinValue;
        OrganizationGrid.ItemsSource = Array.Empty<OrganizationListRow>();
        ClearDetails();
    }

    public void Apply(WorldOrganizationSnapshot? snapshot)
    {
        snapshot ??= WorldOrganizationSnapshot.Empty;
        _snapshot = snapshot;
        if (_projectedRevision == snapshot.AuthorityRevision)
        {
            return;
        }

        TotalCountText.Text = KoreanNumberFormatter.Format(snapshot.Organizations.Count);
        SectCountText.Text = KoreanNumberFormatter.Format(snapshot.ActiveSects);
        ClanCountText.Text = KoreanNumberFormatter.Format(snapshot.ActiveClans);
        DynastyCountText.Text = KoreanNumberFormatter.Format(snapshot.ActiveDynasties);
        _projectedRevision = snapshot.AuthorityRevision;
        RebuildList();
    }

    private void RebuildList()
    {
        var search = SearchTextBox.Text.Trim();
        var selectedKind = KindFilterBox.SelectedIndex switch
        {
            1 => OrganizationKind.Sect,
            2 => OrganizationKind.Clan,
            3 => OrganizationKind.Dynasty,
            _ => (OrganizationKind?)null,
        };
        var selectedStatusGroup = StatusFilterBox.SelectedIndex;
        var rows = _snapshot.Organizations
            .Where(organization => selectedKind is null || organization.Kind == selectedKind)
            .Where(organization => MatchesStatus(organization.Status, selectedStatusGroup))
            .Where(organization => string.IsNullOrEmpty(search)
                || organization.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || organization.FounderName.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || organization.LeaderName.Contains(search, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(organization => organization.Kind)
            .ThenBy(organization => organization.Status)
            .ThenBy(organization => organization.Id.Value)
            .Select(organization => new OrganizationListRow(
                organization.Id,
                FormatKind(organization.Kind),
                organization.Name,
                FormatStatus(organization.Status),
                organization.Members.Count(member => member.Active).ToString(
                    "N0",
                    CultureInfo.CurrentCulture)))
            .ToArray();

        _applying = true;
        try
        {
            OrganizationGrid.ItemsSource = rows;
            var selectedRow = _selectedOrganizationId is { } selectedId
                ? rows.FirstOrDefault(row => row.Id == selectedId)
                : null;
            selectedRow ??= rows.FirstOrDefault();
            OrganizationGrid.SelectedItem = selectedRow;
            _selectedOrganizationId = selectedRow?.Id;
        }
        finally
        {
            _applying = false;
        }

        var preparationStatus = _snapshot.PreparingSectPlans > 0
            ? $" · 종문 준비 {_snapshot.PreparingSectPlans:N0}건 · 적립 {KoreanNumberFormatter.Format(_snapshot.TotalFoundingReserves)} 영석"
            : string.Empty;
        ListStatusText.Text = (rows.Length == 0
            ? _snapshot.Organizations.Count == 0
                ? "아직 창설된 조직이 없습니다. 승인 Gate를 충족한 AI만 실제 조직을 만듭니다."
                : "현재 필터와 일치하는 조직이 없습니다."
            : $"{rows.Length:N0}개 표시 · 전체 {_snapshot.Organizations.Count:N0}개")
            + preparationStatus;
        RenderSelectedOrganization();
    }

    private void RenderSelectedOrganization()
    {
        var organization = GetSelectedOrganization();
        if (organization is null)
        {
            ClearDetails();
            return;
        }

        OrganizationTitleText.Text = organization.Name;
        OrganizationSubtitleText.Text =
            $"{organization.Id} · {FormatKind(organization.Kind)} · 창시자 {organization.FounderName}";
        OrganizationStatusBadgeText.Text = FormatStatus(organization.Status);
        RenderActiveDetailTab(organization);
    }

    private void RenderActiveDetailTab(OrganizationSnapshot organization)
    {
        switch (OrganizationDetailTabControl.SelectedIndex)
        {
            case 0:
                RenderOverview(organization);
                break;
            case 1:
                MemberGrid.ItemsSource = organization.Members
                    .Where(member => member.Active)
                    .OrderBy(member => member.Role)
                    .ThenBy(member => member.AgentId.Value)
                    .Select(member => new OrganizationMemberRow(
                        member.AgentId,
                        member.Name,
                        FormatRole(member.Role),
                        $"{member.LoyaltyHundredths / 100m:0.##}",
                        "소속"))
                    .ToArray();
                break;
            case 2:
                RightGrid.ItemsSource = organization.Rights
                    .Select(right => new OrganizationRightRow(
                        FormatRight(right.Kind),
                        right.LocationNodeId.ToString(),
                        FormatTick(right.GrantedTick)))
                    .ToArray();
                break;
            case 3:
                DiplomacyGrid.ItemsSource = organization.Diplomacy
                    .Select(diplomacy => new OrganizationDiplomacyRow(
                        ResolveOrganizationName(diplomacy.OtherOrganizationId),
                        FormatDiplomacy(diplomacy.Kind),
                        FormatTick(diplomacy.ChangedTick)))
                    .ToArray();
                break;
            case 4:
                CompositeRankingStateText.Text = _snapshot.CompositePowerRankingAvailable
                    ? "계산 가능"
                    : _snapshot.CompositePowerDataState;
                TemporaryReviewText.Text =
                    $"현재 임시 전략축 · 성장↔안정 {organization.Strategy.GrowthVsStabilityHundredths / 100m:+0.##;-0.##;0} · " +
                    $"협력↔지배 {organization.Strategy.CooperationVsDominanceHundredths / 100m:+0.##;-0.##;0} · " +
                    $"위험 감수 {organization.Strategy.RiskToleranceHundredths / 100m:0.##}\n" +
                    $"종류별 공개 상태 · 계보 {organization.Profile.LivingGenealogyCount:N0} · " +
                    $"지배 주 {organization.Profile.ControlledProvinceCount:N0} · 봉신 {organization.Profile.VassalOrganizationCount:N0} · " +
                    $"정식화 {(organization.Profile.IsFormalized ? "완료" : "준비 중")}\n\n" +
                    $"{OrganizationRules.StrategyReviewId}\n" +
                    "전략축 분류 경계는 승인값이 아니며 다음 버전 검토 대상입니다.\n\n" +
                    $"{OrganizationRules.TemporaryMappingReviewId}\n" +
                    "조직운영·창설 자질의 PC 임시 매핑은 승인값이 아니며 다음 버전 검토 대상입니다.\n\n" +
                    $"{OrganizationRules.SuccessionReviewId}\n" +
                    "결정론적 승계 순서와 공석→쇠퇴→해산 시간 경계는 다음 버전 검토 대상입니다.";
                break;
            case 5:
                HistoryGrid.ItemsSource = organization.History
                    .OrderByDescending(entry => entry.Sequence)
                    .Select(entry => new OrganizationHistoryRow(
                        entry.Sequence.ToString("N0", CultureInfo.CurrentCulture),
                        FormatTick(entry.Tick),
                        FormatHistory(entry.Kind),
                        FormatHistoryDetail(entry)))
                    .ToArray();
                break;
        }
    }

    private void RenderOverview(OrganizationSnapshot organization)
    {
        FounderButton.Content = organization.FounderName;
        FounderButton.Tag = organization.FounderAgentId;
        FounderButton.IsEnabled = true;
        LeaderButton.Content = string.IsNullOrEmpty(organization.LeaderName)
            ? "공석"
            : organization.LeaderName;
        LeaderButton.Tag = organization.LeaderAgentId;
        LeaderButton.IsEnabled = organization.LeaderAgentId is not null;
        var activeMembers = organization.Members.Count(member => member.Active);
        OverviewMembersText.Text = organization.Kind == OrganizationKind.Sect
            ? $"{activeMembers:N0} / {OrganizationRules.MaximumSectMembers:N0}"
            : $"{activeMembers:N0}";
        FoundedDateText.Text = FormatTick(organization.FoundedTick.Value);
        TreasuryText.Text = $"{KoreanNumberFormatter.Format(organization.TreasurySpiritStones)} 영석";
        ReputationText.Text = $"{organization.ReputationHundredths / 100m:0.##}";
        OverviewAuthorityText.Text =
            $"권리 {organization.Rights.Count:N0} · 외교 {organization.Diplomacy.Count:N0}";
        OverviewDescriptionText.Text = organization.Status switch
        {
            OrganizationStatus.Active => "지도자와 활성 구성원이 있는 운영 중 조직입니다.",
            OrganizationStatus.Vacant => "지도자 후계자가 없어 공석 절차가 진행 중입니다.",
            OrganizationStatus.Declining => "장기 공석으로 쇠퇴 절차가 진행 중입니다.",
            OrganizationStatus.Dissolved => "해산되어 구성원과 권위 상태가 종료되었습니다.",
            OrganizationStatus.Merged => "다른 조직에 합병되어 독립 권위 상태가 종료되었습니다.",
            _ => "상태 정보가 없습니다.",
        };
    }

    private void ClearDetails()
    {
        OrganizationTitleText.Text = "조직을 선택해 주세요.";
        OrganizationSubtitleText.Text = "실제 권위 상태가 있는 조직만 표시합니다.";
        OrganizationStatusBadgeText.Text = "선택 없음";
        FounderButton.Content = "—";
        FounderButton.Tag = null;
        FounderButton.IsEnabled = false;
        LeaderButton.Content = "—";
        LeaderButton.Tag = null;
        LeaderButton.IsEnabled = false;
        OverviewMembersText.Text = "—";
        FoundedDateText.Text = "—";
        TreasuryText.Text = "—";
        ReputationText.Text = "—";
        OverviewAuthorityText.Text = "—";
        OverviewDescriptionText.Text = "조직 창설 조건을 충족하는 AI가 나타나면 이곳에서 관찰할 수 있습니다.";
        MemberGrid.ItemsSource = Array.Empty<OrganizationMemberRow>();
        RightGrid.ItemsSource = Array.Empty<OrganizationRightRow>();
        DiplomacyGrid.ItemsSource = Array.Empty<OrganizationDiplomacyRow>();
        HistoryGrid.ItemsSource = Array.Empty<OrganizationHistoryRow>();
        CompositeRankingStateText.Text = _snapshot.CompositePowerDataState;
        TemporaryReviewText.Text = "미승인 공식은 임의 수치로 표시하지 않습니다.";
    }

    private OrganizationSnapshot? GetSelectedOrganization() =>
        _selectedOrganizationId is { } selectedId
            ? _snapshot.Organizations.FirstOrDefault(organization => organization.Id == selectedId)
            : null;

    private string ResolveOrganizationName(OrganizationId id)
    {
        var organization = _snapshot.Organizations.FirstOrDefault(candidate => candidate.Id == id);
        return organization is null ? id.ToString() : $"{organization.Name} · {id}";
    }

    private string FormatHistoryDetail(OrganizationHistoryEntry entry)
    {
        var actor = entry.PrimaryAgentIndex >= 0
            ? new AgentId(entry.PrimaryAgentIndex).ToString()
            : "세계";
        var counterpart = entry.OtherOrganizationId is { } other
            ? $" · 상대 {ResolveOrganizationName(other)}"
            : string.Empty;
        var value = entry.Value == 0 ? string.Empty : $" · 값 {entry.Value:N0}";
        return $"주체 {actor}{counterpart}{value}";
    }

    private void OrganizationGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_applying || OrganizationGrid.SelectedItem is not OrganizationListRow row)
        {
            return;
        }

        _selectedOrganizationId = row.Id;
        RenderSelectedOrganization();
    }

    private void OrganizationDetailTabControl_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_applying || !ReferenceEquals(e.Source, sender) || !IsInitialized)
        {
            return;
        }

        if (GetSelectedOrganization() is { } organization)
        {
            RenderActiveDetailTab(organization);
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_applying && IsInitialized)
        {
            RebuildList();
        }
    }

    private void FilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_applying && IsInitialized)
        {
            RebuildList();
        }
    }

    private void AgentButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AgentId agentId })
        {
            AgentSelected?.Invoke(this, new OrganizationAgentSelectedEventArgs(agentId));
        }
    }

    private static bool MatchesStatus(OrganizationStatus status, int group) => group switch
    {
        1 => status == OrganizationStatus.Active,
        2 => status == OrganizationStatus.Vacant,
        3 => status == OrganizationStatus.Declining,
        4 => status is OrganizationStatus.Dissolved or OrganizationStatus.Merged,
        _ => true,
    };

    private static string FormatTick(long tick) =>
        FormatDate(GameCalendar.GetDate(new SimulationTick(Math.Max(0, tick))));

    private static string FormatDate(GameDate date) =>
        $"{date.Year:N0}년 {date.Month:N0}월 {date.Day:N0}일";

    private static string FormatKind(OrganizationKind kind) => kind switch
    {
        OrganizationKind.Sect => "종문",
        OrganizationKind.Clan => "가문",
        OrganizationKind.Dynasty => "왕조",
        _ => "미확인",
    };

    private static string FormatStatus(OrganizationStatus status) => status switch
    {
        OrganizationStatus.Active => "활성",
        OrganizationStatus.Vacant => "공석",
        OrganizationStatus.Declining => "쇠퇴",
        OrganizationStatus.Dissolved => "해산",
        OrganizationStatus.Merged => "합병",
        _ => "미확인",
    };

    private static string FormatRole(OrganizationRole role) => role switch
    {
        OrganizationRole.Member => "구성원",
        OrganizationRole.Founder => "창시자",
        OrganizationRole.Leader => "지도자",
        OrganizationRole.Elder => "원로",
        OrganizationRole.VassalRepresentative => "봉신 대표",
        OrganizationRole.Deputy => "부지도자",
        OrganizationRole.Treasurer => "재정 담당",
        _ => "미확인",
    };

    private static string FormatRight(OrganizationRightKind kind) => kind switch
    {
        OrganizationRightKind.Headquarters => "본산 거점권",
        OrganizationRightKind.Residence => "거주지 권리",
        OrganizationRightKind.Territory => "영지 권리",
        OrganizationRightKind.SpiritVein => "영맥 권리",
        _ => "미확인 권리",
    };

    private static string FormatDiplomacy(OrganizationDiplomacyKind kind) => kind switch
    {
        OrganizationDiplomacyKind.Neutral => "중립",
        OrganizationDiplomacyKind.Allied => "동맹",
        OrganizationDiplomacyKind.Hostile => "적대",
        OrganizationDiplomacyKind.Vassal => "봉신",
        OrganizationDiplomacyKind.Overlord => "상위 지배 조직",
        _ => "미확인",
    };

    private static string FormatHistory(OrganizationHistoryKind kind) => kind switch
    {
        OrganizationHistoryKind.Founded => "창설",
        OrganizationHistoryKind.MemberJoined => "가입",
        OrganizationHistoryKind.MemberDeparted => "이탈",
        OrganizationHistoryKind.LeaderSucceeded => "지도자 승계",
        OrganizationHistoryKind.LeadershipVacant => "지도자 공석",
        OrganizationHistoryKind.DeclineStarted => "쇠퇴 시작",
        OrganizationHistoryKind.Dissolved => "해산",
        OrganizationHistoryKind.Split => "분열",
        OrganizationHistoryKind.Merged => "합병",
        OrganizationHistoryKind.AssetTransferred => "자산 이전",
        OrganizationHistoryKind.DiplomacyChanged => "외교 변경",
        OrganizationHistoryKind.SoulReconnected => "Soul 재연결",
        _ => "미확인 사건",
    };

    private sealed record OrganizationListRow(
        OrganizationId Id,
        string Kind,
        string Name,
        string Status,
        string Members);

    private sealed record OrganizationMemberRow(
        AgentId AgentId,
        string Name,
        string Role,
        string Loyalty,
        string Status);

    private sealed record OrganizationRightRow(string Kind, string Location, string GrantedAt);

    private sealed record OrganizationDiplomacyRow(
        string Organization,
        string Kind,
        string ChangedAt);

    private sealed record OrganizationHistoryRow(
        string Sequence,
        string Date,
        string Kind,
        string Detail);
}
