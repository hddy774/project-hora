using Chronicles.Core;
using Chronicles.Simulation;

namespace Chronicles.Queries;

/// <summary>
/// Describes the exact immutable observation fragments requested by the active UI route.
/// Closed routes must not add their sections to this request.
/// </summary>
public readonly record struct ObservationRequest(
    AgentId? SelectedAgent = null,
    int RankingCount = 1_000,
    int EventCount = 150,
    ObservationSections Sections = ObservationSections.All,
    ushort? DetailedMapLocationId = null,
    int DetailedMapAgentOffset = 0,
    int WorldChronicleCount = int.MaxValue,
    int ContentSecretRealmOffset = 0,
    int ContentSecretRealmLimit = 100,
    int KnowledgeEditionOffset = 0,
    int KnowledgeEditionLimit = 100,
    int LawCandidateOffset = 0,
    int LawCandidateLimit = 54,
    int ContentItemOffset = 0,
    int ContentItemLimit = 0);

public sealed record AgentListPage(
    IReadOnlyList<AgentSnapshot> Agents,
    int TotalCount,
    int Offset,
    int Limit);

/// <summary>
/// Read-only projection boundary over the authoritative simulation world.
/// The runtime supplies synchronization; this type never advances or mutates the world.
/// </summary>
public sealed class WorldObservationQueries
{
    private readonly World _world;

    public WorldObservationQueries(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public WorldSnapshot GetSnapshot(
        int rankingCount = 1_000,
        int eventCount = 150,
        ObservationSections sections = ObservationSections.All,
        ushort? detailedMapLocationId = null,
        int detailedMapAgentOffset = 0,
        int worldChronicleCount = int.MaxValue,
        int contentSecretRealmOffset = 0,
        int contentSecretRealmLimit = 100,
        int knowledgeEditionOffset = 0,
        int knowledgeEditionLimit = 100,
        int lawCandidateOffset = 0,
        int lawCandidateLimit = 54,
        int contentItemOffset = 0,
        int contentItemLimit = 0) =>
        _world.CreateSnapshot(
            rankingCount,
            eventCount,
            sections,
            detailedMapLocationId,
            detailedMapAgentOffset,
            worldChronicleCount,
            contentSecretRealmOffset,
            contentSecretRealmLimit,
            knowledgeEditionOffset,
            knowledgeEditionLimit,
            lawCandidateOffset,
            lawCandidateLimit,
            contentItemOffset,
            contentItemLimit);

    public AgentSnapshot GetAgent(AgentId agentId) => _world.GetAgentSnapshot(agentId);

    public WorldObservation GetObservation(ObservationRequest request)
    {
        var world = _world.CreateSnapshot(
            request.RankingCount,
            request.EventCount,
            request.Sections,
            request.DetailedMapLocationId,
            request.DetailedMapAgentOffset,
            request.WorldChronicleCount,
            request.ContentSecretRealmOffset,
            request.ContentSecretRealmLimit,
            request.KnowledgeEditionOffset,
            request.KnowledgeEditionLimit,
            request.LawCandidateOffset,
            request.LawCandidateLimit,
            request.ContentItemOffset,
            request.ContentItemLimit);
        var agentId = request.SelectedAgent ?? world.LeaderId;
        var cultivationSections = CultivationProjectionSections.None;
        if ((request.Sections & ObservationSections.Cultivation) != 0)
        {
            cultivationSections |= CultivationProjectionSections.Pipeline;
        }
        if ((request.Sections & ObservationSections.CultivationDetail) != 0)
        {
            cultivationSections |= CultivationProjectionSections.DetailProgress;
        }
        var agent = _world.GetAgentSnapshot(
            agentId,
            includeAptitudeDetails:
                (request.Sections & ObservationSections.AptitudeDetails) != 0,
            includeRelations:
                (request.Sections & ObservationSections.Relations) != 0,
            includeTeaching:
                (request.Sections & ObservationSections.Teaching) != 0,
            includeCausal:
                (request.Sections & ObservationSections.CausalRuntime) != 0,
            cultivationSections: cultivationSections);
        var chronicle = (request.Sections & ObservationSections.Chronicle) != 0
            ? _world.GetAgentChronicle(agentId)
            : Array.Empty<LifeChronicleEntry>();
        return new WorldObservation(
            world,
            agent,
            (request.Sections & ObservationSections.PersonalLog) != 0
                ? _world.GetAgentPersonalLog(agentId)
                : Array.Empty<PersonalLogEntry>(),
            (request.Sections & ObservationSections.Chronicle) != 0
                ? chronicle
                : Array.Empty<LifeChronicleEntry>(),
            (request.Sections & ObservationSections.PersonalCenturyChronicle) != 0
                ? _world.GetAgentPersonalCenturyChronicle(agentId)
                : Array.Empty<PersonalCenturyChronicleEntry>(),
            (request.Sections & ObservationSections.Reincarnation) != 0
                ? _world.GetAgentReincarnationSnapshot(agentId)
                : null);
    }

    public IReadOnlyList<AgentSnapshot> BrowseAgents(int offset, int limit = 500) =>
        _world.BrowseAgents(offset, limit);

    public IReadOnlyList<AgentSnapshot> SearchAgents(string? query, int limit = 1_000) =>
        _world.SearchAgents(query, limit);

    public AgentListPage BrowseAgentsById(
        IReadOnlyList<AgentId> agentIds,
        string? query,
        int offset,
        int limit)
    {
        ArgumentNullException.ThrowIfNull(agentIds);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        var normalized = query?.Trim() ?? string.Empty;
        var validIds = agentIds
            .Where(item => (uint)item.Value < (uint)_world.Population)
            .Distinct()
            .OrderBy(item => item.Value)
            .ToArray();
        if (normalized.Length == 0)
        {
            var pageAgents = offset >= validIds.Length
                ? Array.Empty<AgentSnapshot>()
                : validIds
                    .Skip(offset)
                    .Take(limit)
                    .Select(item => _world.GetAgentSnapshot(
                        item,
                        cultivationSections: CultivationProjectionSections.None))
                    .ToArray();
            return new AgentListPage(pageAgents, validIds.Length, offset, limit);
        }

        var matching = new List<AgentSnapshot>(validIds.Length);
        foreach (var agentId in validIds)
        {
            var agent = _world.GetAgentSnapshot(
                agentId,
                cultivationSections: CultivationProjectionSections.None);
            if (normalized.Length == 0
                || agent.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || agent.Id.ToString().Contains(normalized, StringComparison.OrdinalIgnoreCase))
            {
                matching.Add(agent);
            }
        }

        var page = offset >= matching.Count
            ? Array.Empty<AgentSnapshot>()
            : matching.Skip(offset).Take(limit).ToArray();
        return new AgentListPage(page, matching.Count, offset, limit);
    }
}
