using Chronicles.Core;
using Chronicles.Queries;
using Chronicles.Runtime;
using Chronicles.Simulation;

namespace Chronicles.Desktop;

public partial class MainWindow
{
    private void RefreshVisibleObservation(
        AgentId? selectedAgent = null,
        bool skipIfSimulationBusy = false)
    {
        if (_runner is null)
        {
            return;
        }

        if (selectedAgent is { } selected)
        {
            _selectedAgent = selected;
        }

        if (!IsObservationWorkspaceVisible)
        {
            ApplyRuntimeShellSnapshot(_runner.GetShellSnapshot());
            return;
        }

        var request = CreateVisibleObservationRequest();
        if (_workspaceRoute is WorkspaceRoute.People or WorkspaceRoute.Relations)
        {
            if (!skipIfSimulationBusy)
            {
                ApplyPersonObservation(_runner.GetObservation(request));
            }
            else if (_runner.TryGetObservation(request, out var observation))
            {
                ApplyPersonObservation(observation!);
            }
            return;
        }

        if (!skipIfSimulationBusy)
        {
            ApplyWorldSnapshot(_runner.GetSnapshot(request));
        }
        else if (_runner.TryGetSnapshot(request, out var snapshot))
        {
            ApplyWorldSnapshot(snapshot!);
        }
    }

    private ObservationRequest CreateVisibleObservationRequest()
    {
        if (!IsObservationWorkspaceVisible)
        {
            return new ObservationRequest(
                RankingCount: 1,
                EventCount: 0,
                Sections: ObservationSections.None,
                WorldChronicleCount: 0,
                ContentSecretRealmLimit: 0,
                KnowledgeEditionLimit: 0,
                LawCandidateLimit: 0);
        }

        var sections = GetVisibleObservationSections();
        var mapAgentsVisible = _workspaceRoute == WorkspaceRoute.Map
            && MapObservationPanel.ShouldLoadAgents;
        var contentVisible = _workspaceRoute == WorkspaceRoute.Content;
        return new ObservationRequest(
            SelectedAgent: _workspaceRoute is WorkspaceRoute.People or WorkspaceRoute.Relations
                ? _selectedAgent
                : null,
            RankingCount: GetVisibleRankingCount(),
            EventCount: (sections & ObservationSections.WorldLog) != 0 ? 150 : 0,
            Sections: sections,
            DetailedMapLocationId: mapAgentsVisible
                ? MapObservationPanel.SelectedLocationId
                : null,
            DetailedMapAgentOffset: mapAgentsVisible ? MapObservationPanel.AgentOffset : 0,
            WorldChronicleCount: (sections & ObservationSections.WorldChronicle) != 0
                ? WorldChronicleDisplayLimit
                : 0,
            ContentSecretRealmOffset: contentVisible
                ? ContentWorkspacePanel.SecretRealmOffset
                : 0,
            ContentSecretRealmLimit: contentVisible
                && ContentWorkspacePanel.ShouldLoadSecretRealmRows
                    ? ContentWorkspaceView.SecretRealmPageSize
                    : 0,
            KnowledgeEditionLimit: contentVisible
                && ContentWorkspacePanel.ShouldLoadKnowledgeRows
                    ? ContentWorkspaceView.KnowledgeEditionPageSize
                    : 0,
            LawCandidateLimit: contentVisible
                && ContentWorkspacePanel.ShouldLoadLawRows
                    ? ContentWorkspaceView.LawCandidatePageSize
                    : 0,
            ContentItemOffset: contentVisible ? ContentWorkspacePanel.ItemOffset : 0,
            ContentItemLimit: contentVisible && ContentWorkspacePanel.ShouldLoadItemRows
                ? ContentWorkspaceView.ItemPageSize
                : 0);
    }

    private ObservationSections GetVisibleObservationSections()
    {
        if (!IsObservationWorkspaceVisible)
        {
            return ObservationSections.None;
        }

        var sections = _workspaceRoute switch
        {
            WorkspaceRoute.World => ObservationSections.StatisticsSummary,
            WorkspaceRoute.City => ObservationSections.MarketSummary,
            WorkspaceRoute.Economy => MarketObservationPanel.ShouldLoadGoods
                ? ObservationSections.Market
                : ObservationSections.MarketSummary,
            WorkspaceRoute.Map => ObservationSections.Map,
            WorkspaceRoute.Organizations => ObservationSections.Organization,
            WorkspaceRoute.Content => ObservationSections.Content,
            WorkspaceRoute.Records or WorkspaceRoute.Diagnostics =>
                WorldRecordsPanel.SelectedRecordIndex switch
            {
                0 => ObservationSections.WorldLog,
                1 => ObservationSections.WorldChronicle,
                _ => ObservationSections.None,
            },
            _ => ObservationSections.None,
        };
        if (_workspaceRoute == WorkspaceRoute.World)
        {
            if (StatisticsDetailTabControl.SelectedIndex == 0)
            {
                sections |= ObservationSections.RealmDistribution
                    | ObservationSections.MarketSummary
                    | ObservationSections.AllRankings;
            }

            if (StatisticsDetailTabControl.SelectedIndex == 2
                && StatisticsDistributionTabControl.SelectedIndex == 0)
            {
                sections |= ObservationSections.RealmDistribution;
            }

            if (StatisticsDetailTabControl.SelectedIndex == 2
                && StatisticsDistributionTabControl.SelectedIndex == 1)
            {
                sections |= ObservationSections.CombatPowerDistribution;
            }
        }

        if (_workspaceRoute is not (WorkspaceRoute.People or WorkspaceRoute.Relations))
        {
            return sections;
        }

        if (_peopleStatisticsMode)
        {
            return sections | ObservationSections.StatisticsSummary
                | ObservationSections.RealmDistribution;
        }

        var personDetailVisible = !_compactPersonLayout || _compactPersonShowsDetail;
        if (personDetailVisible && _agentDetailSection == AgentDetailSection.Cultivation)
        {
            sections |= ObservationSections.Cultivation;
        }

        if (personDetailVisible && _agentDetailSection == AgentDetailSection.Records)
        {
            sections |= AgentLogTabControl.SelectedIndex switch
            {
                0 => ObservationSections.PersonalLog,
                1 => ObservationSections.Chronicle,
                2 => ObservationSections.PersonalCenturyChronicle,
                3 => ObservationSections.Reincarnation,
                _ => ObservationSections.None,
            };
        }
        if (personDetailVisible
            && _agentDetailSection == AgentDetailSection.Cultivation
            && AptitudeDetailsExpander.IsExpanded)
        {
            sections |= ObservationSections.AptitudeDetails;
        }
        if (personDetailVisible
            && _agentDetailSection == AgentDetailSection.Cultivation
            && CultivationDetailProgressExpander.IsExpanded)
        {
            sections |= ObservationSections.CultivationDetail;
        }
        if (personDetailVisible
            && _agentDetailSection == AgentDetailSection.Social
            && RelationsExpander.IsExpanded)
        {
            sections |= ObservationSections.Relations;
        }
        if (personDetailVisible
            && _agentDetailSection == AgentDetailSection.Social
            && TeachingExpander.IsExpanded)
        {
            sections |= ObservationSections.Teaching;
        }
        if (personDetailVisible
            && _agentDetailSection == AgentDetailSection.Social
            && CausalRuntimeExpander.IsExpanded)
        {
            sections |= ObservationSections.CausalRuntime;
        }

        sections |= _rankingCategory switch
        {
            RankingCategory.CombatPower => ObservationSections.CombatPowerRanking,
            RankingCategory.Realm => ObservationSections.RealmRanking,
            RankingCategory.Aptitude => ObservationSections.AptitudeRanking,
            RankingCategory.Wealth => ObservationSections.WealthRanking,
            null => ObservationSections.None,
            _ => ObservationSections.None,
        };
        return sections;
    }

    private int GetVisibleRankingCount() =>
        _workspaceRoute == WorkspaceRoute.World
            && StatisticsDetailTabControl.SelectedIndex == 0
                ? 1
                : _workspaceRoute is WorkspaceRoute.People or WorkspaceRoute.Relations
                    && !_peopleStatisticsMode
                    && _rankingCategory is not null
                        ? 1_000
                        : 1;

    private void ApplyPersonObservation(WorldObservation observation)
    {
        _applyingObservation = true;
        var snapshot = observation.World;
        _selectedAgent = observation.SelectedAgent.Id;
        ApplyShellSnapshot(snapshot);
        try
        {
            UpdateRankingRows(snapshot);
            if (!_peopleStatisticsMode
                && (!_compactPersonLayout || _compactPersonShowsDetail))
            {
                UpdateSelectedAgent(
                    observation.SelectedAgent,
                    observation.SelectedPersonalLog,
                    observation.SelectedChronicle,
                    observation.SelectedPersonalCenturyChronicle,
                    observation.SelectedReincarnation);
            }
        }
        finally
        {
            _applyingObservation = false;
        }
    }

    private void ApplyWorldSnapshot(WorldSnapshot snapshot)
    {
        _applyingObservation = true;
        ApplyShellSnapshot(snapshot);
        try
        {
            if (_workspaceRoute == WorkspaceRoute.Organizations)
            {
                OrganizationWorkspacePanel.Apply(snapshot.Organizations);
            }
            else if (_workspaceRoute == WorkspaceRoute.Content)
            {
                ContentWorkspacePanel.Apply(snapshot.Content);
            }
            else if (IsObservationWorkspaceVisible)
            {
                UpdateVisibleWorldPanel(snapshot);
            }
        }
        finally
        {
            _applyingObservation = false;
        }
    }
}
