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

/// <summary>
/// 점수 내림차순, 동일 점수 Agent index 오름차순 계약을 할당 없이 정렬한다.
/// 매 월 10,000명을 여러 번 비교 정렬하던 경로에서 점수 계산과 delegate 호출을 제거한다.
/// </summary>
internal static class RankingRadixSorter
{
    private const int Radix = 256;
    private const int PassCount = sizeof(long);
    private const ulong SignedOrderMask = 1UL << 63;

    public static void SortDescending(
        int[] orderedAgents,
        long[] scoreByAgent,
        int[] scratchAgents)
    {
        ArgumentNullException.ThrowIfNull(orderedAgents);
        ArgumentNullException.ThrowIfNull(scoreByAgent);
        ArgumentNullException.ThrowIfNull(scratchAgents);
        if (orderedAgents.Length != scoreByAgent.Length
            || orderedAgents.Length != scratchAgents.Length)
        {
            throw new ArgumentException("랭킹 정렬 버퍼 길이가 일치하지 않습니다.");
        }

        for (var agent = 0; agent < orderedAgents.Length; agent++)
        {
            orderedAgents[agent] = agent;
        }

        var source = orderedAgents;
        var destination = scratchAgents;
        Span<int> offsets = stackalloc int[Radix];
        for (var pass = 0; pass < PassCount; pass++)
        {
            offsets.Clear();
            var shift = pass * 8;
            for (var index = 0; index < source.Length; index++)
            {
                offsets[GetDigit(scoreByAgent[source[index]], shift)]++;
            }

            var nextOffset = 0;
            for (var digit = 0; digit < Radix; digit++)
            {
                var count = offsets[digit];
                offsets[digit] = nextOffset;
                nextOffset += count;
            }

            for (var index = 0; index < source.Length; index++)
            {
                var agent = source[index];
                var digit = GetDigit(scoreByAgent[agent], shift);
                destination[offsets[digit]++] = agent;
            }

            (source, destination) = (destination, source);
        }

        // 64비트는 8회의 짝수 pass이므로 최종 source가 orderedAgents여야 한다.
        // 길이 0을 포함한 계약 변경에도 안전하도록 방어 복사를 유지한다.
        if (!ReferenceEquals(source, orderedAgents))
        {
            Array.Copy(source, orderedAgents, source.Length);
        }
    }

    private static int GetDigit(long score, int shift)
    {
        // signed 오름차순 key를 뒤집으면 signed 내림차순이 된다.
        var descendingKey = ~(unchecked((ulong)score) ^ SignedOrderMask);
        return (int)((descendingKey >> shift) & 0xFFUL);
    }
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
