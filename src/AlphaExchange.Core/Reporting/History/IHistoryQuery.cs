namespace AlphaExchange.Core;

public interface IHistoryQuery
{
    DailySnapshot? At(long hour);
    IReadOnlyList<DailySnapshot> Range(long start, long end, int maximumPoints = 500);
    bool Covers(long start, long end);
}
