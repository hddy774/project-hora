using System.Windows.Media;
using Chronicles.Core;

namespace Chronicles.Desktop;

internal sealed record RankingRow(
    int Rank,
    AgentId AgentId,
    string Name,
    string Realm,
    string Score,
    string Activity);

internal sealed record EventRow(
    string Time,
    string Summary,
    Brush Color);

internal sealed record DistributionRow(
    string Label,
    string Count,
    string Ratio);

internal sealed record RankingMovementRow(
    string Scope,
    string Cutoff,
    string Movement);

internal sealed record LifeToggleRow(
    int? LifeNumber,
    string Label,
    bool IsSelected);
