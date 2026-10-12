namespace AlphaExchange.Core;

public sealed record PeriodStatistics(DailySnapshot Start, DailySnapshot End, IReadOnlyList<DailySnapshot> Points, bool Partial)
{
    public long Volume => End.Volume - Start.Volume;
    public long Turnover => End.Turnover - Start.Turnover;
    public double Change => (double)End.Capitalization / Math.Max(1, Start.Capitalization) - 1;
}
