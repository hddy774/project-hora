using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Chronicles.Core;

namespace Chronicles.Simulation;

/// <summary>
/// 최근 개인 로그를 인물별 List 객체 없이 보관하는 고정 용량 분할 링 저장소다.
/// 각 인물의 논리 순서는 항상 오래된 항목부터 최신 항목까지 유지된다.
/// </summary>
internal sealed class PersonalLogRingStore
{
    public const int Capacity = 100;

    private const int AgentsPerSegment = 64;
    private const int EntriesPerChunk = 10;
    private const int ChunksPerAgent = Capacity / EntriesPerChunk;
    private const int EagerChunksPerSegment = 3;

    internal static int StoredEntrySizeBytes =>
        Unsafe.SizeOf<CompactPersonalLogEntry>();

    private readonly int _population;
    private readonly Segment?[] _segments;
    private readonly byte[] _counts;
    private readonly byte[] _starts;

    public PersonalLogRingStore(int population)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        _population = population;
        _segments = new Segment[checked((population + AgentsPerSegment - 1)
            / AgentsPerSegment)];
        _counts = new byte[population];
        _starts = new byte[population];
    }

    /// <summary>
    /// 실제로 생성된 10행 청크 수다. 저장소 희소성 회귀 검증에만 사용한다.
    /// </summary>
    internal int AllocatedChunkCount
    {
        get
        {
            var count = 0;
            foreach (var segment in _segments)
            {
                if (segment is not null)
                {
                    count += segment.AllocatedChunkCount;
                }
            }
            return count;
        }
    }

    /// <summary>
    /// 현재 청크가 확보한 압축 entry 슬롯 수다.
    /// </summary>
    internal int AllocatedEntrySlotCount
    {
        get
        {
            var count = 0;
            foreach (var segment in _segments)
            {
                if (segment is not null)
                {
                    count = checked(count + segment.AllocatedEntrySlotCount);
                }
            }
            return count;
        }
    }

    public AgentPersonalLog this[int agentIndex]
    {
        get
        {
            ValidateAgent(agentIndex);
            return new AgentPersonalLog(this, agentIndex);
        }
    }

    private int GetCount(int agentIndex) => _counts[agentIndex];

    private PersonalLogEntry Get(int agentIndex, int logicalIndex)
    {
        var count = _counts[agentIndex];
        if ((uint)logicalIndex >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalIndex));
        }

        var segment = _segments[agentIndex / AgentsPerSegment]
            ?? throw new InvalidOperationException("개인 로그 분할 저장소가 초기화되지 않았습니다.");
        return segment.Get(
            agentIndex % AgentsPerSegment,
            GetPhysicalIndex(agentIndex, logicalIndex));
    }

    private void Set(int agentIndex, int logicalIndex, PersonalLogEntry value)
    {
        var count = _counts[agentIndex];
        if ((uint)logicalIndex >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalIndex));
        }

        var segment = _segments[agentIndex / AgentsPerSegment]
            ?? throw new InvalidOperationException("개인 로그 분할 저장소가 초기화되지 않았습니다.");
        segment.SetExisting(
            agentIndex % AgentsPerSegment,
            GetPhysicalIndex(agentIndex, logicalIndex),
            value);
    }

    private bool TryExtendLatestAction(
        int agentIndex,
        AgentActivity activity,
        int lifeNumber,
        SimulationTick tick,
        long currentMonth)
    {
        var count = _counts[agentIndex];
        if (count == 0)
        {
            return false;
        }

        var segment = _segments[agentIndex / AgentsPerSegment]
            ?? throw new InvalidOperationException("개인 로그 분할 저장소가 초기화되지 않았습니다.");
        return segment.TryExtendLatestAction(
            agentIndex % AgentsPerSegment,
            GetPhysicalIndex(agentIndex, count - 1),
            activity,
            lifeNumber,
            tick,
            currentMonth);
    }

    private void Add(int agentIndex, PersonalLogEntry value)
    {
        var count = _counts[agentIndex];
        var start = _starts[agentIndex];
        var logicalIndex = count < Capacity ? count : 0;
        var segmentIndex = agentIndex / AgentsPerSegment;
        var segment = _segments[segmentIndex] ??= new Segment(
            GetSegmentAgentCount(segmentIndex));
        var segmentAgentIndex = agentIndex % AgentsPerSegment;

        if (count < Capacity)
        {
            segment.Set(
                segmentAgentIndex,
                GetPhysicalIndex(agentIndex, logicalIndex),
                value);
            _counts[agentIndex] = checked((byte)(count + 1));
            return;
        }

        segment.SetExisting(segmentAgentIndex, start, value);
        _starts[agentIndex] = checked((byte)((start + 1) % Capacity));
    }

    private PersonalLogEntry[] ToArray(int agentIndex)
    {
        var count = _counts[agentIndex];
        if (count == 0)
        {
            return [];
        }

        var result = new PersonalLogEntry[count];
        CopyTo(agentIndex, result);
        return result;
    }

    private void CopyTo(int agentIndex, Span<PersonalLogEntry> destination)
    {
        var count = _counts[agentIndex];
        if (destination.Length < count)
        {
            throw new ArgumentException("개인 로그 복사 대상이 부족합니다.", nameof(destination));
        }
        if (count == 0)
        {
            return;
        }

        var segment = _segments[agentIndex / AgentsPerSegment]
            ?? throw new InvalidOperationException("개인 로그 분할 저장소가 초기화되지 않았습니다.");
        segment.CopyTo(
            agentIndex % AgentsPerSegment,
            _starts[agentIndex],
            count,
            destination);
    }

    private int GetPhysicalIndex(int agentIndex, int logicalIndex) =>
        (_starts[agentIndex] + logicalIndex) % Capacity;

    private int GetSegmentAgentCount(int segmentIndex) => Math.Min(
        AgentsPerSegment,
        _population - (segmentIndex * AgentsPerSegment));

    private void ValidateAgent(int agentIndex)
    {
        if ((uint)agentIndex >= (uint)_population)
        {
            throw new ArgumentOutOfRangeException(nameof(agentIndex));
        }
    }

    /// <summary>
    /// 한 segment의 64명을 10행 단위 slab으로 나눈다. 첫 로그에서 자주 사용하는
    /// 30행만 함께 확보해 장기 tick 중의 할당을 초기화 경계로 옮기고, 나머지
    /// 70행은 실제 사용 시점까지 지연한다. 100행에 도달하면 열 청크가 모두
    /// 고정되어 이후 링 덮어쓰기에서는 managed allocation이 발생하지 않는다.
    /// </summary>
    private sealed class Segment
    {
        private readonly int _agentCount;
        private readonly CompactPersonalLogEntry[]?[] _chunks =
            new CompactPersonalLogEntry[ChunksPerAgent][];

        public Segment(int agentCount)
        {
            _agentCount = agentCount;
            for (var chunkIndex = 0;
                chunkIndex < EagerChunksPerSegment;
                chunkIndex++)
            {
                _chunks[chunkIndex] = new CompactPersonalLogEntry[checked(
                    _agentCount * EntriesPerChunk)];
            }
            AllocatedChunkCount = EagerChunksPerSegment;
        }

        public int AllocatedChunkCount { get; private set; }
        public int AllocatedEntrySlotCount => checked(
            AllocatedChunkCount * _agentCount * EntriesPerChunk);

        public PersonalLogEntry Get(int segmentAgentIndex, int physicalIndex)
        {
            var chunkIndex = physicalIndex / EntriesPerChunk;
            var chunk = _chunks[chunkIndex]
                ?? throw new InvalidOperationException("개인 로그 청크가 초기화되지 않았습니다.");
            return chunk[GetChunkOffset(segmentAgentIndex, physicalIndex)].Expand();
        }

        public void Set(
            int segmentAgentIndex,
            int physicalIndex,
            PersonalLogEntry value)
        {
            var chunkIndex = physicalIndex / EntriesPerChunk;
            var chunk = _chunks[chunkIndex];
            if (chunk is null)
            {
                chunk = new CompactPersonalLogEntry[checked(
                    _agentCount * EntriesPerChunk)];
                _chunks[chunkIndex] = chunk;
                AllocatedChunkCount++;
            }
            chunk[GetChunkOffset(segmentAgentIndex, physicalIndex)] =
                CompactPersonalLogEntry.Create(value);
        }

        public void SetExisting(
            int segmentAgentIndex,
            int physicalIndex,
            PersonalLogEntry value)
        {
            var chunkIndex = physicalIndex / EntriesPerChunk;
            var chunk = _chunks[chunkIndex]
                ?? throw new InvalidOperationException("개인 로그 청크가 초기화되지 않았습니다.");
            chunk[GetChunkOffset(segmentAgentIndex, physicalIndex)] =
                CompactPersonalLogEntry.Create(value);
        }

        public bool TryExtendLatestAction(
            int segmentAgentIndex,
            int physicalIndex,
            AgentActivity activity,
            int lifeNumber,
            SimulationTick tick,
            long currentMonth)
        {
            var chunkIndex = physicalIndex / EntriesPerChunk;
            var chunk = _chunks[chunkIndex]
                ?? throw new InvalidOperationException("개인 로그 청크가 초기화되지 않았습니다.");
            var offset = GetChunkOffset(segmentAgentIndex, physicalIndex);
            return chunk[offset].TryExtendAction(
                activity,
                lifeNumber,
                tick,
                currentMonth,
                out chunk[offset]);
        }

        public void CopyTo(
            int segmentAgentIndex,
            int start,
            int count,
            Span<PersonalLogEntry> destination)
        {
            var copied = 0;
            var physicalIndex = start;
            while (copied < count)
            {
                var chunkIndex = physicalIndex / EntriesPerChunk;
                var withinChunk = physicalIndex % EntriesPerChunk;
                var copyLength = Math.Min(
                    count - copied,
                    EntriesPerChunk - withinChunk);
                var chunk = _chunks[chunkIndex]
                    ?? throw new InvalidOperationException("개인 로그 청크가 초기화되지 않았습니다.");
                var sourceOffset = checked(
                    (segmentAgentIndex * EntriesPerChunk) + withinChunk);
                for (var index = 0; index < copyLength; index++)
                {
                    destination[copied + index] = chunk[sourceOffset + index].Expand();
                }
                copied += copyLength;
                physicalIndex = (physicalIndex + copyLength) % Capacity;
            }
        }

        private static int GetChunkOffset(int segmentAgentIndex, int physicalIndex) =>
            checked((segmentAgentIndex * EntriesPerChunk)
                + (physicalIndex % EntriesPerChunk));
    }

    /// <summary>
    /// 공개 DTO의 nullable wrapper와 정렬 padding을 저장 배열에 반복하지 않는다.
    /// 원래 값은 조회 경계에서만 복원되며 Pack=1 고정으로 entry당 36바이트를 사용한다.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly struct CompactPersonalLogEntry
    {
        private const byte KindMask = 0b0000_0111;
        private const byte PrimaryActorFlag = 0b0000_1000;
        private const byte HasActivityFlag = 0b0001_0000;
        private const byte HasWorldEventFlag = 0b0010_0000;
        private const byte HasChronicleEventFlag = 0b0100_0000;
        private const byte HasRelatedAgentFlag = 0b1000_0000;

        private readonly long _sequence;
        private readonly long _tick;
        private readonly long _value;
        private readonly int _lifeNumber;
        private readonly int _relatedAgentValue;
        private readonly byte _kindAndFlags;
        private readonly byte _activity;
        private readonly byte _worldEventKind;
        private readonly byte _chronicleEventKind;

        private CompactPersonalLogEntry(
            long sequence,
            long tick,
            long value,
            int lifeNumber,
            int relatedAgentValue,
            byte kindAndFlags,
            byte activity,
            byte worldEventKind,
            byte chronicleEventKind)
        {
            _sequence = sequence;
            _tick = tick;
            _value = value;
            _lifeNumber = lifeNumber;
            _relatedAgentValue = relatedAgentValue;
            _kindAndFlags = kindAndFlags;
            _activity = activity;
            _worldEventKind = worldEventKind;
            _chronicleEventKind = chronicleEventKind;
        }

        public static CompactPersonalLogEntry Create(PersonalLogEntry value)
        {
            var kind = (byte)value.Kind;
            if ((kind & ~KindMask) != 0)
            {
                throw new InvalidDataException("개인 로그 종류가 압축 범위를 벗어났습니다.");
            }

            var flags = kind;
            if (value.IsPrimaryActor)
            {
                flags |= PrimaryActorFlag;
            }
            if (value.Activity.HasValue)
            {
                flags |= HasActivityFlag;
            }
            if (value.WorldEventKind.HasValue)
            {
                flags |= HasWorldEventFlag;
            }
            if (value.ChronicleEventKind.HasValue)
            {
                flags |= HasChronicleEventFlag;
            }
            if (value.RelatedAgent.HasValue)
            {
                flags |= HasRelatedAgentFlag;
            }

            return new CompactPersonalLogEntry(
                value.Sequence,
                value.Tick.Value,
                value.Value,
                value.LifeNumber,
                value.RelatedAgent.GetValueOrDefault().Value,
                flags,
                (byte)value.Activity.GetValueOrDefault(),
                (byte)value.WorldEventKind.GetValueOrDefault(),
                (byte)value.ChronicleEventKind.GetValueOrDefault());
        }

        public PersonalLogEntry Expand() => new(
            _sequence,
            new SimulationTick(_tick),
            _lifeNumber,
            (PersonalLogKind)(_kindAndFlags & KindMask),
            Has(HasActivityFlag) ? (AgentActivity)_activity : null,
            Has(HasWorldEventFlag) ? (WorldEventKind)_worldEventKind : null,
            Has(HasChronicleEventFlag) ? (ChronicleEventKind)_chronicleEventKind : null,
            Has(PrimaryActorFlag),
            Has(HasRelatedAgentFlag) ? new AgentId(_relatedAgentValue) : null,
            _value);

        public bool TryExtendAction(
            AgentActivity activity,
            int lifeNumber,
            SimulationTick tick,
            long currentMonth,
            out CompactPersonalLogEntry extended)
        {
            if ((_kindAndFlags & KindMask) != (byte)PersonalLogKind.ActionCompleted
                || !Has(HasActivityFlag)
                || _activity != (byte)activity
                || _lifeNumber != lifeNumber)
            {
                extended = this;
                return false;
            }

            var elapsedMonths = Math.Max(
                1L,
                currentMonth - GameCalendar.GetMonthIndex(new SimulationTick(_tick)));
            extended = new CompactPersonalLogEntry(
                _sequence,
                tick.Value,
                checked(Math.Max(1L, _value) + elapsedMonths),
                _lifeNumber,
                _relatedAgentValue,
                _kindAndFlags,
                _activity,
                _worldEventKind,
                _chronicleEventKind);
            return true;
        }

        private bool Has(byte flag) => (_kindAndFlags & flag) != 0;
    }

    internal readonly struct AgentPersonalLog : IReadOnlyList<PersonalLogEntry>
    {
        private readonly PersonalLogRingStore _owner;
        private readonly int _agentIndex;

        internal AgentPersonalLog(PersonalLogRingStore owner, int agentIndex)
        {
            _owner = owner;
            _agentIndex = agentIndex;
        }

        public int Count => _owner.GetCount(_agentIndex);

        public PersonalLogEntry this[int index]
        {
            get => _owner.Get(_agentIndex, index);
            set => _owner.Set(_agentIndex, index, value);
        }

        public PersonalLogEntry this[Index index]
        {
            get => this[index.GetOffset(Count)];
            set => this[index.GetOffset(Count)] = value;
        }

        public void Add(PersonalLogEntry value) => _owner.Add(_agentIndex, value);

        public bool TryExtendLatestAction(
            AgentActivity activity,
            int lifeNumber,
            SimulationTick tick,
            long currentMonth) => _owner.TryExtendLatestAction(
                _agentIndex,
                activity,
                lifeNumber,
                tick,
                currentMonth);

        public void AddRange(IEnumerable<PersonalLogEntry> values)
        {
            ArgumentNullException.ThrowIfNull(values);
            foreach (var value in values)
            {
                Add(value);
            }
        }

        public PersonalLogEntry[] ToArray() => _owner.ToArray(_agentIndex);

        public Enumerator GetEnumerator() => new(this);

        IEnumerator<PersonalLogEntry> IEnumerable<PersonalLogEntry>.GetEnumerator() =>
            new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        internal struct Enumerator : IEnumerator<PersonalLogEntry>
        {
            private readonly AgentPersonalLog _log;
            private int _index;

            internal Enumerator(AgentPersonalLog log)
            {
                _log = log;
                _index = -1;
            }

            public PersonalLogEntry Current => _log[_index];
            object IEnumerator.Current => Current;

            public bool MoveNext() => ++_index < _log.Count;
            public void Reset() => _index = -1;
            public void Dispose() { }
        }
    }
}
