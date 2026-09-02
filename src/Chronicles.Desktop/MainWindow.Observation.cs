using Chronicles.Core;
using Chronicles.Queries;
using Chronicles.Runtime;
using Chronicles.Simulation;

namespace Chronicles.Desktop;

public partial class MainWindow
{
    private void RefreshVisibleObservation(AgentId? selectedAgent = null)
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
            var shellSnapshot = _runner.GetSnapshot(
                rankingCount: 1,
                eventCount: 0,
                sections: ObservationSections.None,
                worldChronicleCount: 0);
            ApplyShellSnapshot(shellSnapshot);
            return;
        }

        var observation = _runner.GetObservation(
            _selectedAgent,
            rankingCount: GetVisibleRankingCount(),
            sections: GetVisibleObservationSections(),
            detailedMapLocationId: _workspaceRoute == WorkspaceRoute.Map
                && MapObservationPanel.ShouldLoadAgents
                    ? MapObservationPanel.SelectedLocationId
                    : null,
            detailedMapAgentOffset: _workspaceRoute == WorkspaceRoute.Map
                && MapObservationPanel.ShouldLoadAgents
                    ? MapObservationPanel.AgentOffset
                    : 0,
            worldChronicleCount: WorldChronicleDisplayLimit,
            contentSecretRealmOffset: _workspaceRoute == WorkspaceRoute.Content
                ? ContentWorkspacePanel.SecretRealmOffset
                : 0,
            contentSecretRealmLimit: _workspaceRoute == WorkspaceRoute.Content
                && ContentWorkspacePanel.ShouldLoadSecretRealmRows
                    ? ContentWorkspaceView.SecretRealmPageSize
                    : 0,
            knowledgeEditionLimit: _workspaceRoute == WorkspaceRoute.Content
                && ContentWorkspacePanel.ShouldLoadKnowledgeRows
                    ? ContentWorkspaceView.KnowledgeEditionPageSize
                    : 0,
            lawCandidateLimit: _workspaceRoute == WorkspaceRoute.Content
                && ContentWorkspacePanel.ShouldLoadLawRows
                    ? ContentWorkspaceView.LawCandidatePageSize
                    : 0,
            contentItemOffset: _workspaceRoute == WorkspaceRoute.Content
                ? ContentWorkspacePanel.ItemOffset
                : 0,
            contentItemLimit: _workspaceRoute == WorkspaceRoute.Content
                && ContentWorkspacePanel.ShouldLoadItemRows
                    ? ContentWorkspaceView.ItemPageSize
                    : 0);
        ApplyObservation(observation);
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

    private void ApplyObservation(WorldObservation observation)
    {
        _applyingObservation = true;
        var snapshot = observation.World;
        _selectedAgent = observation.SelectedAgent.Id;
        ApplyShellSnapshot(snapshot);
        try
        {
            if (_workspaceRoute is WorkspaceRoute.People or WorkspaceRoute.Relations)
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
            else if (_workspaceRoute == WorkspaceRoute.Organizations)
            {
                OrganizationWorkspacePanel.Apply(snapshot.Organizations);
            }
            else if (_workspaceRoute == WorkspaceRoute.Content)
            {
                ContentWorkspacePanel.Apply(snapshot.Content);
            }
            else
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
