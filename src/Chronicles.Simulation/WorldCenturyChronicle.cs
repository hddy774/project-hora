using System.Numerics;
using Chronicles.Core;

namespace Chronicles.Simulation;

/// <summary>
/// 개인 사건을 복제하지 않고 100년 단위 세계 통계만 영구 보존하는 권위 원장이다.
/// 문자열 역사서는 UI가 관찰 중인 세부 탭에 맞춰 지연 생성한다.
/// </summary>
internal sealed class WorldCenturyChronicle
{
    public const int YearsPerVolume = 100;
    public const int RankingTierCount = 4;
    public const int RankingCacheSize = 10;

    private static readonly int[] RankingTierLimits = [1, 3, 5, 10];

    private readonly int _population;
    private readonly int _agentWordCount;
    private readonly List<WorldCenturyChronicleEntry> _entries = [];
    private CenturyAccumulator _current;
    private long _sequence;

    public event Action<WorldCenturyChronicleEntry>? EntryRecorded;

    public WorldCenturyChronicle(int population, long currentYear)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        ArgumentOutOfRangeException.ThrowIfNegative(currentYear);
        _population = population;
        _agentWordCount = checked((population + 63) / 64);
        _current = CenturyAccumulator.Create(currentYear / YearsPerVolume, _agentWordCount);
    }

    private WorldCenturyChronicle(
        int population,
        WorldCenturyChronicleCheckpoint checkpoint)
    {
        _population = population;
        _agentWordCount = checked((population + 63) / 64);
        _sequence = checkpoint.Sequence;
        _entries.AddRange(checkpoint.Entries.Select(CloneEntry));
        _current = CenturyAccumulator.Restore(checkpoint.Current);
    }

    public int Count => _entries.Count;

    public int ReincarnationRiverResidents => _current.ReincarnationRiverResidents;

    public static WorldCenturyChronicle Restore(
        int population,
        WorldCenturyChronicleCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        return new WorldCenturyChronicle(population, checkpoint);
    }

    public WorldCenturyChronicleEntry[] GetTail(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return _entries
            .Skip(Math.Max(0, _entries.Count - count))
            .Select(CloneEntry)
            .ToArray();
    }

    public WorldCenturyChronicleEntry[] GetAll() =>
        _entries.Select(CloneEntry).ToArray();

    public void AdvanceTo(SimulationTick tick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tick.Value);
        EnsureCentury(GameCalendar.GetYear(tick));
    }

    public void RestoreLegacyRiverResidents(int count)
    {
        if (count is < 0 || count > _population)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }
        _current.ReincarnationRiverResidents = count;
    }

    public void RecordRankingSample(
        SimulationTick tick,
        ReadOnlySpan<int> combatPowerRanking,
        ReadOnlySpan<int> realmRanking,
        ReadOnlySpan<int> aptitudeRanking,
        ReadOnlySpan<int> wealthRanking,
        Func<int, string> getAgentName)
    {
        ArgumentNullException.ThrowIfNull(getAgentName);
        var year = GameCalendar.GetYear(tick);
        EnsureCentury(year);
        if (_current.LastRankingSampleYear == year)
        {
            return;
        }

        AccumulatePositionChanges(
            combatPowerRanking,
            _current.PreviousCombatPowerTop,
            _current.CombatPowerPositionChanges);
        AccumulatePositionChanges(
            realmRanking,
            _current.PreviousRealmTop,
            _current.RealmPositionChanges);
        AccumulatePositionChanges(
            aptitudeRanking,
            _current.PreviousAptitudeTop,
            _current.AptitudePositionChanges);
        AccumulatePositionChanges(
            wealthRanking,
            _current.PreviousWealthTop,
            _current.WealthPositionChanges);

        _current.PreviousCombatPowerTop = CopyTop(combatPowerRanking);
        _current.PreviousRealmTop = CopyTop(realmRanking);
        _current.PreviousAptitudeTop = CopyTop(aptitudeRanking);
        _current.PreviousWealthTop = CopyTop(wealthRanking);
        _current.CombatPowerLeader = CreateLeader(combatPowerRanking, getAgentName);
        _current.RealmLeader = CreateLeader(realmRanking, getAgentName);
        _current.AptitudeLeader = CreateLeader(aptitudeRanking, getAgentName);
        _current.WealthLeader = CreateLeader(wealthRanking, getAgentName);
        _current.LastRankingSampleYear = year;
    }

    public void RecordWorldEvent(
        SimulationTick tick,
        WorldEventKind kind,
        int primaryAgent,
        long value)
    {
        EnsureCentury(GameCalendar.GetYear(tick));
        switch (kind)
        {
            case WorldEventKind.CultivationEntered:
                RecordRealmBreakthrough(RealmStage.QiRefining, primaryAgent);
                break;
            case WorldEventKind.MajorRealmBreakthrough:
                var realm = (int)(value / 10);
                if ((uint)realm < (uint)RealmRules.All.Count)
                {
                    RecordRealmBreakthrough((RealmStage)realm, primaryAgent);
                }
                break;
            case WorldEventKind.Death:
            case WorldEventKind.CombatDeath:
                _current.DeathCount = checked(_current.DeathCount + 1);
                MarkUnique(
                    _current.DeathAgentBits,
                    primaryAgent,
                    ref _current.UniqueDeathCount);
                break;
            case WorldEventKind.ReincarnationEntered:
                _current.ReincarnationEntryCount = checked(
                    _current.ReincarnationEntryCount + 1);
                MarkUnique(
                    _current.ReincarnationEntryAgentBits,
                    primaryAgent,
                    ref _current.UniqueReincarnationEntryCount);
                _current.ReincarnationRiverResidents = checked(
                    _current.ReincarnationRiverResidents + 1);
                break;
            case WorldEventKind.Rebirth:
                _current.RebirthCount = checked(_current.RebirthCount + 1);
                MarkUnique(
                    _current.RebirthAgentBits,
                    primaryAgent,
                    ref _current.UniqueRebirthCount);
                _current.ReincarnationRiverResidents = Math.Max(
                    0,
                    _current.ReincarnationRiverResidents - 1);
                break;
            case WorldEventKind.CombatEnded:
                _current.CombatsCompleted = checked(_current.CombatsCompleted + 1);
                break;
            case WorldEventKind.MonsterHuntWon:
            case WorldEventKind.MonsterHuntLost:
                _current.MonsterHunts = checked(_current.MonsterHunts + 1);
                break;
            case WorldEventKind.MissionCompleted:
                _current.MissionsCompleted = checked(_current.MissionsCompleted + 1);
                break;
            case WorldEventKind.SpiritStoneMined:
                _current.SpiritStonesMined = checked(_current.SpiritStonesMined + value);
                break;
            case WorldEventKind.TreasureCrafted:
                _current.TreasuresCrafted = checked(_current.TreasuresCrafted + 1);
                break;
            case WorldEventKind.PillCrafted:
                _current.PillsCrafted = checked(_current.PillsCrafted + 1);
                break;
        }
    }

    public WorldCenturyChronicleCheckpoint CreateCheckpoint() => new(
        _sequence,
        _entries.Select(CloneEntry).ToArray(),
        _current.CreateCheckpoint());

    private void EnsureCentury(long year)
    {
        var targetCentury = year / YearsPerVolume;
        if (targetCentury < _current.CenturyIndex)
        {
            throw new InvalidOperationException("세계 연대기 시간을 되돌릴 수 없습니다.");
        }

        while (_current.CenturyIndex < targetCentury)
        {
            var entry = CreateEntry(_current);
            _entries.Add(entry);
            EntryRecorded?.Invoke(CloneEntry(entry));
            _current = _current.CreateNext(_agentWordCount);
        }
    }

    private WorldCenturyChronicleEntry CreateEntry(CenturyAccumulator source)
    {
        var startYear = checked(source.CenturyIndex * YearsPerVolume);
        return new WorldCenturyChronicleEntry(
            ++_sequence,
            source.CenturyIndex + 1,
            startYear,
            checked(startYear + YearsPerVolume - 1),
            source.RealmBreakthroughCounts.ToArray(),
            source.RealmBreakthroughUniqueCounts.ToArray(),
            source.CombatPowerPositionChanges.ToArray(),
            source.RealmPositionChanges.ToArray(),
            source.AptitudePositionChanges.ToArray(),
            source.WealthPositionChanges.ToArray(),
            source.CombatPowerLeader,
            source.RealmLeader,
            source.AptitudeLeader,
            source.WealthLeader,
            source.DeathCount,
            source.UniqueDeathCount,
            source.ReincarnationEntryCount,
            source.UniqueReincarnationEntryCount,
            source.RebirthCount,
            source.UniqueRebirthCount,
            source.ReincarnationRiverResidents,
            source.CombatsCompleted,
            source.MonsterHunts,
            source.MissionsCompleted,
            source.SpiritStonesMined,
            source.TreasuresCrafted,
            source.PillsCrafted);
    }

    private void RecordRealmBreakthrough(RealmStage realm, int agent)
    {
        var index = (int)realm;
        _current.RealmBreakthroughCounts[index] = checked(
            _current.RealmBreakthroughCounts[index] + 1);
        MarkUnique(
            _current.RealmBreakthroughAgentBits[index],
            agent,
            ref _current.RealmBreakthroughUniqueCounts[index]);
    }

    private void MarkUnique(ulong[] bits, int agent, ref int uniqueCount)
    {
        if ((uint)agent >= (uint)_population)
        {
            throw new ArgumentOutOfRangeException(nameof(agent));
        }

        var word = agent >> 6;
        var mask = 1UL << (agent & 63);
        if ((bits[word] & mask) == 0)
        {
            bits[word] |= mask;
            uniqueCount = checked(uniqueCount + 1);
        }
    }

    private static void AccumulatePositionChanges(
        ReadOnlySpan<int> currentRanking,
        int[] previousRanking,
        long[] totals)
    {
        if (previousRanking.Length == 0)
        {
            return;
        }

        for (var tier = 0; tier < RankingTierLimits.Length; tier++)
        {
            totals[tier] = checked(
                totals[tier]
                + RankingHistory.CountPositionalChanges(
                    currentRanking,
                    previousRanking,
                    RankingTierLimits[tier]));
        }
    }

    private static int[] CopyTop(ReadOnlySpan<int> ranking) =>
        ranking[..Math.Min(RankingCacheSize, ranking.Length)].ToArray();

    private static WorldChronicleLeader CreateLeader(
        ReadOnlySpan<int> ranking,
        Func<int, string> getAgentName)
    {
        if (ranking.Length == 0)
        {
            throw new InvalidOperationException("세계 랭킹 1위가 없습니다.");
        }

        var agent = ranking[0];
        return new WorldChronicleLeader(new AgentId(agent), getAgentName(agent));
    }

    internal static int CountSetBits(ulong[] bits)
    {
        var count = 0;
        foreach (var word in bits)
        {
            count = checked(count + BitOperations.PopCount(word));
        }
        return count;
    }

    private static WorldCenturyChronicleEntry CloneEntry(WorldCenturyChronicleEntry entry) =>
        entry with
        {
            RealmBreakthroughCounts = entry.RealmBreakthroughCounts.ToArray(),
            RealmBreakthroughUniqueCounts = entry.RealmBreakthroughUniqueCounts.ToArray(),
            CombatPowerPositionChanges = entry.CombatPowerPositionChanges.ToArray(),
            RealmPositionChanges = entry.RealmPositionChanges.ToArray(),
            AptitudePositionChanges = entry.AptitudePositionChanges.ToArray(),
            WealthPositionChanges = entry.WealthPositionChanges.ToArray(),
        };

    private sealed class CenturyAccumulator
    {
        private CenturyAccumulator()
        {
        }

        public long CenturyIndex { get; private init; }
        public int[] RealmBreakthroughCounts { get; private init; } = [];
        public int[] RealmBreakthroughUniqueCounts { get; private init; } = [];
        public ulong[][] RealmBreakthroughAgentBits { get; private init; } = [];
        public long[] CombatPowerPositionChanges { get; private init; } = [];
        public long[] RealmPositionChanges { get; private init; } = [];
        public long[] AptitudePositionChanges { get; private init; } = [];
        public long[] WealthPositionChanges { get; private init; } = [];
        public int[] PreviousCombatPowerTop { get; set; } = [];
        public int[] PreviousRealmTop { get; set; } = [];
        public int[] PreviousAptitudeTop { get; set; } = [];
        public int[] PreviousWealthTop { get; set; } = [];
        public WorldChronicleLeader CombatPowerLeader { get; set; }
        public WorldChronicleLeader RealmLeader { get; set; }
        public WorldChronicleLeader AptitudeLeader { get; set; }
        public WorldChronicleLeader WealthLeader { get; set; }
        public long LastRankingSampleYear { get; set; } = -1;
        public int DeathCount { get; set; }
        public int UniqueDeathCount;
        public ulong[] DeathAgentBits { get; private init; } = [];
        public int ReincarnationEntryCount { get; set; }
        public int UniqueReincarnationEntryCount;
        public ulong[] ReincarnationEntryAgentBits { get; private init; } = [];
        public int RebirthCount { get; set; }
        public int UniqueRebirthCount;
        public ulong[] RebirthAgentBits { get; private init; } = [];
        public int ReincarnationRiverResidents { get; set; }
        public long CombatsCompleted { get; set; }
        public long MonsterHunts { get; set; }
        public long MissionsCompleted { get; set; }
        public long SpiritStonesMined { get; set; }
        public long TreasuresCrafted { get; set; }
        public long PillsCrafted { get; set; }

        public static CenturyAccumulator Create(long centuryIndex, int wordCount) => new()
        {
            CenturyIndex = centuryIndex,
            RealmBreakthroughCounts = new int[RealmRules.All.Count],
            RealmBreakthroughUniqueCounts = new int[RealmRules.All.Count],
            RealmBreakthroughAgentBits = Enumerable.Range(0, RealmRules.All.Count)
                .Select(_ => new ulong[wordCount])
                .ToArray(),
            CombatPowerPositionChanges = new long[RankingTierCount],
            RealmPositionChanges = new long[RankingTierCount],
            AptitudePositionChanges = new long[RankingTierCount],
            WealthPositionChanges = new long[RankingTierCount],
            DeathAgentBits = new ulong[wordCount],
            ReincarnationEntryAgentBits = new ulong[wordCount],
            RebirthAgentBits = new ulong[wordCount],
        };

        public CenturyAccumulator CreateNext(int wordCount)
        {
            var next = Create(checked(CenturyIndex + 1), wordCount);
            next.PreviousCombatPowerTop = PreviousCombatPowerTop.ToArray();
            next.PreviousRealmTop = PreviousRealmTop.ToArray();
            next.PreviousAptitudeTop = PreviousAptitudeTop.ToArray();
            next.PreviousWealthTop = PreviousWealthTop.ToArray();
            next.CombatPowerLeader = CombatPowerLeader;
            next.RealmLeader = RealmLeader;
            next.AptitudeLeader = AptitudeLeader;
            next.WealthLeader = WealthLeader;
            next.LastRankingSampleYear = LastRankingSampleYear;
            next.ReincarnationRiverResidents = ReincarnationRiverResidents;
            return next;
        }

        public WorldCenturyAccumulatorCheckpoint CreateCheckpoint() => new(
            CenturyIndex,
            RealmBreakthroughCounts.ToArray(),
            RealmBreakthroughUniqueCounts.ToArray(),
            RealmBreakthroughAgentBits.Select(bits => bits.ToArray()).ToArray(),
            CombatPowerPositionChanges.ToArray(),
            RealmPositionChanges.ToArray(),
            AptitudePositionChanges.ToArray(),
            WealthPositionChanges.ToArray(),
            PreviousCombatPowerTop.ToArray(),
            PreviousRealmTop.ToArray(),
            PreviousAptitudeTop.ToArray(),
            PreviousWealthTop.ToArray(),
            CombatPowerLeader,
            RealmLeader,
            AptitudeLeader,
            WealthLeader,
            LastRankingSampleYear,
            DeathCount,
            UniqueDeathCount,
            DeathAgentBits.ToArray(),
            ReincarnationEntryCount,
            UniqueReincarnationEntryCount,
            ReincarnationEntryAgentBits.ToArray(),
            RebirthCount,
            UniqueRebirthCount,
            RebirthAgentBits.ToArray(),
            ReincarnationRiverResidents,
            CombatsCompleted,
            MonsterHunts,
            MissionsCompleted,
            SpiritStonesMined,
            TreasuresCrafted,
            PillsCrafted);

        public static CenturyAccumulator Restore(WorldCenturyAccumulatorCheckpoint value) => new()
        {
            CenturyIndex = value.CenturyIndex,
            RealmBreakthroughCounts = value.RealmBreakthroughCounts.ToArray(),
            RealmBreakthroughUniqueCounts = value.RealmBreakthroughUniqueCounts.ToArray(),
            RealmBreakthroughAgentBits = value.RealmBreakthroughAgentBits
                .Select(bits => bits.ToArray())
                .ToArray(),
            CombatPowerPositionChanges = value.CombatPowerPositionChanges.ToArray(),
            RealmPositionChanges = value.RealmPositionChanges.ToArray(),
            AptitudePositionChanges = value.AptitudePositionChanges.ToArray(),
            WealthPositionChanges = value.WealthPositionChanges.ToArray(),
            PreviousCombatPowerTop = value.PreviousCombatPowerTop.ToArray(),
            PreviousRealmTop = value.PreviousRealmTop.ToArray(),
            PreviousAptitudeTop = value.PreviousAptitudeTop.ToArray(),
            PreviousWealthTop = value.PreviousWealthTop.ToArray(),
            CombatPowerLeader = value.CombatPowerLeader,
            RealmLeader = value.RealmLeader,
            AptitudeLeader = value.AptitudeLeader,
            WealthLeader = value.WealthLeader,
            LastRankingSampleYear = value.LastRankingSampleYear,
            DeathCount = value.DeathCount,
            UniqueDeathCount = value.UniqueDeathCount,
            DeathAgentBits = value.DeathAgentBits.ToArray(),
            ReincarnationEntryCount = value.ReincarnationEntryCount,
            UniqueReincarnationEntryCount = value.UniqueReincarnationEntryCount,
            ReincarnationEntryAgentBits = value.ReincarnationEntryAgentBits.ToArray(),
            RebirthCount = value.RebirthCount,
            UniqueRebirthCount = value.UniqueRebirthCount,
            RebirthAgentBits = value.RebirthAgentBits.ToArray(),
            ReincarnationRiverResidents = value.ReincarnationRiverResidents,
            CombatsCompleted = value.CombatsCompleted,
            MonsterHunts = value.MonsterHunts,
            MissionsCompleted = value.MissionsCompleted,
            SpiritStonesMined = value.SpiritStonesMined,
            TreasuresCrafted = value.TreasuresCrafted,
            PillsCrafted = value.PillsCrafted,
        };
    }
}
