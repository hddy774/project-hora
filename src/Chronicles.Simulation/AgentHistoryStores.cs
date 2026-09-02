using System.Collections;
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

    private readonly int _population;
    private readonly PersonalLogEntry[]?[] _segments;
    private readonly byte[] _counts;
    private readonly byte[] _starts;

    public PersonalLogRingStore(int population)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        _population = population;
        _segments = new PersonalLogEntry[checked((population + AgentsPerSegment - 1)
            / AgentsPerSegment)][];
        _counts = new byte[population];
        _starts = new byte[population];
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
        return segment[GetPhysicalOffset(agentIndex, logicalIndex)];
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
        segment[GetPhysicalOffset(agentIndex, logicalIndex)] = value;
    }

    private void Add(int agentIndex, PersonalLogEntry value)
    {
        var count = _counts[agentIndex];
        var start = _starts[agentIndex];
        var logicalIndex = count < Capacity ? count : 0;
        var segmentIndex = agentIndex / AgentsPerSegment;
        var segment = _segments[segmentIndex] ??= new PersonalLogEntry[
            checked(GetSegmentAgentCount(segmentIndex) * Capacity)];

        if (count < Capacity)
        {
            segment[GetPhysicalOffset(agentIndex, logicalIndex)] = value;
            _counts[agentIndex] = checked((byte)(count + 1));
            return;
        }

        segment[GetSegmentAgentOffset(agentIndex) + start] = value;
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
        var agentOffset = GetSegmentAgentOffset(agentIndex);
        var start = _starts[agentIndex];
        var firstLength = Math.Min(count, Capacity - start);
        segment.AsSpan(agentOffset + start, firstLength).CopyTo(destination);
        if (firstLength < count)
        {
            segment.AsSpan(agentOffset, count - firstLength).CopyTo(destination[firstLength..]);
        }
    }

    private int GetPhysicalOffset(int agentIndex, int logicalIndex) => checked(
        GetSegmentAgentOffset(agentIndex)
        + ((_starts[agentIndex] + logicalIndex) % Capacity));

    private static int GetSegmentAgentOffset(int agentIndex) => checked(
        (agentIndex % AgentsPerSegment) * Capacity);

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

        SetCompactEntry(_tails[agentIndex], slot, new CompactLifeChronicleEntry(
            value.Sequence,
            value.Tick.Value,
            value.AgeYears,
            expectedPersonId,
            value.Value,
            value.Kind,
            value.RealmStage,
            value.MinorStage));
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
        if (!TryGetPersonName(agentIndex, entry.PersonId, out var personName))
        {
            throw new InvalidOperationException("연대기 인물 identity의 이름을 복원할 수 없습니다.");
        }

        return new LifeChronicleEntry(
            entry.Sequence,
            new SimulationTick(entry.Tick),
            checked((int)(entry.PersonId.Value >> 32)),
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

    private readonly record struct CompactLifeChronicleEntry(
        long Sequence,
        long Tick,
        long AgeYears,
        PersonIdentityId PersonId,
        int Value,
        ChronicleEventKind Kind,
        RealmStage RealmStage,
        MinorStage MinorStage);

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

        public Enumerator GetEnumerator() => new(this);

        IEnumerator<LifeChronicleEntry> IEnumerable<LifeChronicleEntry>.GetEnumerator() =>
            new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        internal struct Enumerator : IEnumerator<LifeChronicleEntry>
        {
            private readonly AgentChronicle _chronicle;
            private int _index;

            internal Enumerator(AgentChronicle chronicle)
            {
                _chronicle = chronicle;
                _index = -1;
            }

            public LifeChronicleEntry Current => _chronicle[_index];
            object IEnumerator.Current => Current;

            public bool MoveNext() => ++_index < _chronicle.Count;
            public void Reset() => _index = -1;
            public void Dispose() { }
        }
    }
}
