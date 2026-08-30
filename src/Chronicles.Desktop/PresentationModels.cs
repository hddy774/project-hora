using System.Windows.Media;
using Chronicles.Core;

namespace Chronicles.Desktop;

internal sealed record RankingRow(
    int Rank,
    AgentId AgentId,
    string Name,
    string Realm,
    string Power,
    string Activity);

internal sealed record EventRow(
    string Time,
    string Summary,
    Brush Color);
