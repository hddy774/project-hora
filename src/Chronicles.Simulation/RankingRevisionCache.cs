namespace Chronicles.Simulation;

internal sealed class RankingRevisionCache
{
    public ulong DataRevision { get; private set; } = 1;
    public ulong SortedRevision { get; private set; }
    public long SortCount { get; private set; }
    public bool IsDirty => DataRevision != SortedRevision;

    public void MarkDirty()
    {
        if (DataRevision == ulong.MaxValue)
        {
            DataRevision = 1;
            SortedRevision = 0;
            return;
        }

        DataRevision++;
    }

    public void MarkSorted()
    {
        SortedRevision = DataRevision;
        SortCount++;
    }

    public RankingCategoryCacheDiagnostics GetDiagnostics() => new(
        DataRevision,
        SortedRevision,
        SortCount);
}

internal readonly record struct RankingCategoryCacheDiagnostics(
    ulong DataRevision,
    ulong SortedRevision,
    long SortCount)
{
    public bool IsDirty => DataRevision != SortedRevision;
}

internal readonly record struct RankingCacheDiagnostics(
    RankingCategoryCacheDiagnostics CombatPower,
    RankingCategoryCacheDiagnostics Realm,
    RankingCategoryCacheDiagnostics Aptitude,
    RankingCategoryCacheDiagnostics Wealth)
{
    public long TotalSortCount =>
        CombatPower.SortCount + Realm.SortCount + Aptitude.SortCount + Wealth.SortCount;
}
