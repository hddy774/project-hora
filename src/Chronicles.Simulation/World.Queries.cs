using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    public IReadOnlyList<LifeChronicleEntry> GetAgentChronicle(AgentId agentId)
    {
        var index = ValidateAgent(agentId);
        return _agents.Chronicles[index].ToArray();
    }

    public IReadOnlyList<AgentSnapshot> BrowseAgents(int offset, int limit = 500)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        if (offset >= Population)
        {
            return Array.Empty<AgentSnapshot>();
        }

        var count = Math.Min(limit, Population - offset);
        var results = new AgentSnapshot[count];
        for (var resultIndex = 0; resultIndex < count; resultIndex++)
        {
            results[resultIndex] = GetAgentSnapshot(new AgentId(offset + resultIndex));
        }

        return results;
    }

    public IReadOnlyList<AgentSnapshot> SearchAgents(string? query, int limit = 1_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        var normalized = query?.Trim() ?? string.Empty;
        var results = new List<AgentSnapshot>(Math.Min(limit, Population));

        if (TryParseAgentId(normalized, out var exactAgent))
        {
            results.Add(GetAgentSnapshot(exactAgent));
            return results;
        }

        for (var index = 0; index < Population && results.Count < limit; index++)
        {
            if (normalized.Length == 0
                || _agents.Names[index].Contains(normalized, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(GetAgentSnapshot(new AgentId(index)));
            }
        }

        return results;
    }

    private bool TryParseAgentId(string query, out AgentId agentId)
    {
        var numeric = query.StartsWith('A') ? query[1..] : query;
        if (int.TryParse(numeric, out var displayId)
            && displayId >= 1
            && displayId <= Population)
        {
            agentId = new AgentId(displayId - 1);
            return true;
        }

        agentId = default;
        return false;
    }
}
