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
    int WorldChronicleCount = int.MaxValue);

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
        int worldChronicleCount = int.MaxValue) =>
        _world.CreateSnapshot(
            rankingCount,
            eventCount,
            sections,
            detailedMapLocationId,
            detailedMapAgentOffset,
            worldChronicleCount);

    public AgentSnapshot GetAgent(AgentId agentId) => _world.GetAgentSnapshot(agentId);

    public WorldObservation GetObservation(ObservationRequest request)
    {
        var world = _world.CreateSnapshot(
            request.RankingCount,
            request.EventCount,
            request.Sections,
            request.DetailedMapLocationId,
            request.DetailedMapAgentOffset,
            request.WorldChronicleCount);
        var agentId = request.SelectedAgent ?? world.LeaderId;
        var agent = _world.GetAgentSnapshot(
            agentId,
            includeAptitudeDetails:
                (request.Sections & ObservationSections.AptitudeDetails) != 0,
            includeRelations:
                (request.Sections & ObservationSections.Relations) != 0,
            includeTeaching:
                (request.Sections & ObservationSections.Teaching) != 0,
            includeCausal:
                (request.Sections & ObservationSections.CausalRuntime) != 0);
        var needsChronicle = (request.Sections
            & (ObservationSections.Chronicle | ObservationSections.PersonalCenturyChronicle)) != 0;
        var chronicle = needsChronicle
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
                ? PersonalCenturyChronicleProjection.Create(chronicle, agent, world.Tick)
                : Array.Empty<PersonalCenturyChronicleEntry>(),
            (request.Sections & ObservationSections.Reincarnation) != 0
                ? _world.GetAgentReincarnationSnapshot(agentId)
                : null);
    }

    public IReadOnlyList<AgentSnapshot> BrowseAgents(int offset, int limit = 500) =>
        _world.BrowseAgents(offset, limit);

    public IReadOnlyList<AgentSnapshot> SearchAgents(string? query, int limit = 1_000) =>
        _world.SearchAgents(query, limit);
}
