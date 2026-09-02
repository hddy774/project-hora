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

/// <summary>
/// 영구 연대기에서 반복되는 인물명 참조를 제거하고 Person identity로 보관한다.
/// 공개 query/checkpoint에서는 기존 LifeChronicleEntry를 지연 복원한다.
/// </summary>
internal sealed class CompactChronicleStore
{
    private const int EntriesPerBlock = 4;
    private const int BlocksPerSegment = 1_024;

    private readonly int _population;
    private readonly int[] _heads;
    private readonly int[] _tails;
    private readonly int[] _counts;
    private readonly List<CompactLifeChronicleEntry[]> _entrySegments = [];
    private readonly List<int[]> _nextSegments = [];
    private readonly PersonIdentityId[] _firstPersonIds;
    private readonly string?[] _firstPersonNames;
    private Dictionary<PersonIdentityId, string>? _additionalPersonNames;
    private int _blockCount;

    internal static int StoredEntrySizeBytes =>
        Unsafe.SizeOf<CompactLifeChronicleEntry>();

    internal int AllocatedEntrySlotCount => checked(
        _entrySegments.Count * BlocksPerSegment * EntriesPerBlock);

    internal int AllocatedBlockCount => _blockCount;

    public CompactChronicleStore(int population)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        _population = population;
        _heads = new int[population];
        _tails = new int[population];
        _counts = new int[population];
        Array.Fill(_heads, -1);
        Array.Fill(_tails, -1);
        _firstPersonIds = new PersonIdentityId[population];
        _firstPersonNames = new string?[population];
    }

    public AgentChronicle this[int agentIndex]
    {
        get
        {
            ValidateAgent(agentIndex);
            return new AgentChronicle(this, agentIndex);
        }
    }

    public bool TryGetPersonName(
        int agentIndex,
        PersonIdentityId personId,
        out string personName)
    {
        ValidateAgent(agentIndex);
        if (_firstPersonIds[agentIndex] == personId
            && _firstPersonNames[agentIndex] is { } firstName)
        {
            personName = firstName;
            return true;
        }
        if (_additionalPersonNames is not null
            && _additionalPersonNames.TryGetValue(personId, out var additionalName))
        {
            personName = additionalName;
            return true;
        }

        personName = string.Empty;
        return false;
    }

    private int GetCount(int agentIndex) => _counts[agentIndex];

    private LifeChronicleEntry Get(int agentIndex, int logicalIndex)
    {
        if ((uint)logicalIndex >= (uint)_counts[agentIndex])
        {
            throw new ArgumentOutOfRangeException(nameof(logicalIndex));
        }

        var block = _heads[agentIndex];
        var remaining = logicalIndex;
        while (remaining >= EntriesPerBlock)
        {
            block = GetNextBlock(block);
            remaining -= EntriesPerBlock;
        }
        return Expand(agentIndex, GetCompactEntry(block, remaining));
    }

    private void Add(int agentIndex, LifeChronicleEntry value)
    {
        var expectedPersonId = PersonCausalRules.CreatePersonId(agentIndex, value.LifeNumber);
        RegisterPersonName(agentIndex, expectedPersonId, value.PersonName);

        var count = _counts[agentIndex];
        var slot = count % EntriesPerBlock;
        if (slot == 0)
        {
            var newBlock = AllocateBlock();
            if (_tails[agentIndex] < 0)
            {
                _heads[agentIndex] = newBlock;
            }
            else
            {
                SetNextBlock(_tails[agentIndex], newBlock);
            }
            _tails[agentIndex] = newBlock;
        }

        SetCompactEntry(
            _tails[agentIndex],
            slot,
            CompactLifeChronicleEntry.Create(value));
        _counts[agentIndex] = checked(count + 1);
    }

    private LifeChronicleEntry[] ToArray(int agentIndex)
    {
        var count = _counts[agentIndex];
        if (count == 0)
        {
            return [];
        }

        var result = new LifeChronicleEntry[count];
        var resultIndex = 0;
        var block = _heads[agentIndex];
        while (block >= 0 && resultIndex < count)
        {
            var entries = Math.Min(EntriesPerBlock, count - resultIndex);
            for (var slot = 0; slot < entries; slot++)
            {
                result[resultIndex++] = Expand(agentIndex, GetCompactEntry(block, slot));
            }
            block = GetNextBlock(block);
        }
        return result;
    }

    private LifeChronicleEntry Expand(int agentIndex, CompactLifeChronicleEntry entry)
    {
        var personId = PersonCausalRules.CreatePersonId(agentIndex, entry.LifeNumber);
        if (!TryGetPersonName(agentIndex, personId, out var personName))
        {
            throw new InvalidOperationException("연대기 인물 identity의 이름을 복원할 수 없습니다.");
        }

        return new LifeChronicleEntry(
            entry.Sequence,
            new SimulationTick(entry.Tick),
            entry.LifeNumber,
            entry.AgeYears,
            personName,
            entry.Kind,
            entry.RealmStage,
            entry.MinorStage,
            entry.Value);
    }

    private void RegisterPersonName(
        int agentIndex,
        PersonIdentityId personId,
        string personName)
    {
        if (string.IsNullOrWhiteSpace(personName))
        {
            throw new InvalidDataException("연대기 인물 이름이 비어 있습니다.");
        }

        if (_firstPersonNames[agentIndex] is null)
        {
            _firstPersonIds[agentIndex] = personId;
            _firstPersonNames[agentIndex] = personName;
            return;
        }

        if (_firstPersonIds[agentIndex] == personId)
        {
            if (!string.Equals(_firstPersonNames[agentIndex], personName, StringComparison.Ordinal))
            {
                throw new InvalidDataException("같은 생의 연대기 인물 이름이 서로 다릅니다.");
            }
            return;
        }

        _additionalPersonNames ??= [];
        if (_additionalPersonNames.TryGetValue(personId, out var existing))
        {
            if (!string.Equals(existing, personName, StringComparison.Ordinal))
            {
                throw new InvalidDataException("같은 생의 연대기 인물 이름이 서로 다릅니다.");
            }
            return;
        }
        _additionalPersonNames.Add(personId, personName);
    }

    private int AllocateBlock()
    {
        var block = _blockCount++;
        var segmentIndex = block / BlocksPerSegment;
        if (segmentIndex == _entrySegments.Count)
        {
            _entrySegments.Add(new CompactLifeChronicleEntry[
                checked(BlocksPerSegment * EntriesPerBlock)]);
            var next = new int[BlocksPerSegment];
            Array.Fill(next, -1);
            _nextSegments.Add(next);
        }
        return block;
    }

    private CompactLifeChronicleEntry GetCompactEntry(int block, int slot) =>
        _entrySegments[block / BlocksPerSegment][
            checked(((block % BlocksPerSegment) * EntriesPerBlock) + slot)];

    private void SetCompactEntry(int block, int slot, CompactLifeChronicleEntry value) =>
        _entrySegments[block / BlocksPerSegment][
            checked(((block % BlocksPerSegment) * EntriesPerBlock) + slot)] = value;

    private int GetNextBlock(int block) =>
        _nextSegments[block / BlocksPerSegment][block % BlocksPerSegment];

    private void SetNextBlock(int block, int next) =>
        _nextSegments[block / BlocksPerSegment][block % BlocksPerSegment] = next;

    private void ValidateAgent(int agentIndex)
    {
        if ((uint)agentIndex >= (uint)_population)
        {
            throw new ArgumentOutOfRangeException(nameof(agentIndex));
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly struct CompactLifeChronicleEntry
    {
        private readonly long _sequence;
        private readonly long _tick;
        private readonly int _lifeNumber;
        private readonly int _ageYears;
        private readonly int _value;
        private readonly byte _kind;
        private readonly byte _realmStage;
        private readonly byte _minorStage;

        private CompactLifeChronicleEntry(
            long sequence,
            long tick,
            int lifeNumber,
            int ageYears,
            int value,
            byte kind,
            byte realmStage,
            byte minorStage)
        {
            _sequence = sequence;
            _tick = tick;
            _lifeNumber = lifeNumber;
            _ageYears = ageYears;
            _value = value;
            _kind = kind;
            _realmStage = realmStage;
            _minorStage = minorStage;
        }

        public long Sequence => _sequence;
        public long Tick => _tick;
        public int LifeNumber => _lifeNumber;
        public int AgeYears => _ageYears;
        public int Value => _value;
        public ChronicleEventKind Kind => (ChronicleEventKind)_kind;
        public RealmStage RealmStage => (RealmStage)_realmStage;
        public MinorStage MinorStage => (MinorStage)_minorStage;

        public static CompactLifeChronicleEntry Create(LifeChronicleEntry value)
        {
            if (value.AgeYears is < 0 or > int.MaxValue)
            {
                throw new InvalidDataException("연대기 나이가 압축 저장 범위를 벗어났습니다.");
            }

            return new CompactLifeChronicleEntry(
                value.Sequence,
                value.Tick.Value,
                value.LifeNumber,
                checked((int)value.AgeYears),
                value.Value,
                checked((byte)value.Kind),
                checked((byte)value.RealmStage),
                checked((byte)value.MinorStage));
        }
    }

    internal readonly struct AgentChronicle : IReadOnlyList<LifeChronicleEntry>
    {
        private readonly CompactChronicleStore _owner;
        private readonly int _agentIndex;

        internal AgentChronicle(CompactChronicleStore owner, int agentIndex)
        {
            _owner = owner;
            _agentIndex = agentIndex;
        }

        public int Count => _owner.GetCount(_agentIndex);
        public LifeChronicleEntry this[int index] => _owner.Get(_agentIndex, index);

        public void Add(LifeChronicleEntry value) => _owner.Add(_agentIndex, value);

        public void AddRange(IEnumerable<LifeChronicleEntry> values)
        {
            ArgumentNullException.ThrowIfNull(values);
            foreach (var value in values)
            {
                Add(value);
            }
        }

        public LifeChronicleEntry[] ToArray() => _owner.ToArray(_agentIndex);

        public Enumerator GetEnumerator() => new(_owner, _agentIndex);

        IEnumerator<LifeChronicleEntry> IEnumerable<LifeChronicleEntry>.GetEnumerator() =>
            new Enumerator(_owner, _agentIndex);

        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(_owner, _agentIndex);

        internal struct Enumerator : IEnumerator<LifeChronicleEntry>
        {
            private readonly CompactChronicleStore _owner;
            private readonly int _agentIndex;
            private int _remaining;
            private int _block;
            private int _slot;
            private LifeChronicleEntry _current;

            internal Enumerator(CompactChronicleStore owner, int agentIndex)
            {
                _owner = owner;
                _agentIndex = agentIndex;
                _remaining = owner.GetCount(agentIndex);
                _block = owner._heads[agentIndex];
                _slot = 0;
                _current = default;
            }

            public LifeChronicleEntry Current => _current;
            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                if (_remaining == 0)
                {
                    return false;
                }
                if (_slot == EntriesPerBlock)
                {
                    _block = _owner.GetNextBlock(_block);
                    _slot = 0;
                }
                if (_block < 0)
                {
                    throw new InvalidOperationException("연대기 블록 연결이 손상되었습니다.");
                }

                _current = _owner.Expand(
                    _agentIndex,
                    _owner.GetCompactEntry(_block, _slot++));
                _remaining--;
                return true;
            }

            public void Reset()
            {
                _remaining = _owner.GetCount(_agentIndex);
                _block = _owner._heads[_agentIndex];
                _slot = 0;
                _current = default;
            }
            public void Dispose() { }
        }
    }
}
