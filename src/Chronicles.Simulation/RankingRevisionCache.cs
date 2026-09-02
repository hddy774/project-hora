using System.Numerics;

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

internal sealed class RankingSortWorkspace
{
    public RankingSortWorkspace(int population)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        Scores = new long[population];
        ScratchAgents = new int[population];
    }

    public long[] Scores { get; }
    private int[] ScratchAgents { get; }

    public void Sort(int[] orderedAgents) =>
        RankingRadixSorter.SortDescending(orderedAgents, Scores, ScratchAgents);
}

/// <summary>
/// 점수 내림차순, 동일 점수 Agent index 오름차순 계약을 할당 없이 정렬한다.
/// 실제 랭킹의 비음수 점수는 최고 유효 bit까지만 통과하고, 진단용 signed 입력은 전체
/// 64-bit 경로를 사용한다.
/// </summary>
internal static class RankingRadixSorter
{
    private const int RadixBits = 11;
    private const int Radix = 1 << RadixBits;
    private const int PassCount = (64 + RadixBits - 1) / RadixBits;
    private const ulong RadixMask = Radix - 1;
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

        var hasNegativeScore = false;
        ulong maximumScore = 0;
        for (var agent = 0; agent < orderedAgents.Length; agent++)
        {
            orderedAgents[agent] = agent;
            var score = scoreByAgent[agent];
            hasNegativeScore |= score < 0;
            if (score >= 0)
            {
                maximumScore = Math.Max(maximumScore, (ulong)score);
            }
        }

        if (!hasNegativeScore)
        {
            SortNonNegativeDescending(
                orderedAgents,
                scoreByAgent,
                scratchAgents,
                GetNonNegativePassCount(maximumScore));
            return;
        }

        SortSignedDescending(orderedAgents, scoreByAgent, scratchAgents);
    }

    internal static int GetRequiredPassCount(ReadOnlySpan<long> scores)
    {
        ulong maximum = 0;
        foreach (var score in scores)
        {
            if (score < 0)
            {
                return PassCount;
            }
            maximum = Math.Max(maximum, (ulong)score);
        }
        return GetNonNegativePassCount(maximum);
    }

    private static void SortNonNegativeDescending(
        int[] orderedAgents,
        long[] scoreByAgent,
        int[] scratchAgents,
        int passCount)
    {
        var source = orderedAgents;
        var destination = scratchAgents;
        Span<int> offsets = stackalloc int[Radix];
        for (var pass = 0; pass < passCount; pass++)
        {
            offsets.Clear();
            var shift = pass * RadixBits;
            for (var index = 0; index < source.Length; index++)
            {
                offsets[(int)(((ulong)scoreByAgent[source[index]] >> shift) & RadixMask)]++;
            }

            var nextOffset = 0;
            for (var digit = Radix - 1; digit >= 0; digit--)
            {
                var count = offsets[digit];
                offsets[digit] = nextOffset;
                nextOffset += count;
            }

            for (var index = 0; index < source.Length; index++)
            {
                var agent = source[index];
                var digit = (int)(((ulong)scoreByAgent[agent] >> shift) & RadixMask);
                destination[offsets[digit]++] = agent;
            }

            (source, destination) = (destination, source);
        }

        if (!ReferenceEquals(source, orderedAgents))
        {
            Array.Copy(source, orderedAgents, source.Length);
        }
    }

    private static void SortSignedDescending(
        int[] orderedAgents,
        long[] scoreByAgent,
        int[] scratchAgents)
    {
        var source = orderedAgents;
        var destination = scratchAgents;
        Span<int> offsets = stackalloc int[Radix];
        for (var pass = 0; pass < PassCount; pass++)
        {
            offsets.Clear();
            var shift = pass * RadixBits;
            for (var index = 0; index < source.Length; index++)
            {
                offsets[GetSignedDigit(scoreByAgent[source[index]], shift)]++;
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
                var digit = GetSignedDigit(scoreByAgent[agent], shift);
                destination[offsets[digit]++] = agent;
            }
            (source, destination) = (destination, source);
        }
        if (!ReferenceEquals(source, orderedAgents))
        {
            Array.Copy(source, orderedAgents, source.Length);
        }
    }

    private static int GetNonNegativePassCount(ulong maximumScore) =>
        maximumScore == 0
            ? 1
            : (BitOperations.Log2(maximumScore) / RadixBits) + 1;

    private static int GetSignedDigit(long score, int shift)
    {
        // signed 오름차순 key를 뒤집으면 signed 내림차순이 된다.
        var descendingKey = ~(unchecked((ulong)score) ^ SignedOrderMask);
        return (int)((descendingKey >> shift) & RadixMask);
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
