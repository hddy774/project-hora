using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    public IReadOnlyList<AgentSnapshot> SearchAgents(string? query, int limit = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        var normalized = query?.Trim() ?? string.Empty;
        var results = new List<AgentSnapshot>(Math.Min(limit, Population));

        if (TryParseAgentId(normalized, out var exactAgent))
        {
            results.Add(GetAgentSnapshot(exactAgent));
            return results;
        }

        for (var rankOffset = 0; rankOffset < Population && results.Count < limit; rankOffset++)
        {
            var index = _agentByRank[rankOffset];
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
