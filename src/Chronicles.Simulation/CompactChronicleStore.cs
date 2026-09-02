using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Chronicles.Core;

namespace Chronicles.Simulation;

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
    private readonly byte[] _headSlots;
    private readonly byte[] _tailCounts;
    private readonly bool[] _protected;
    private readonly List<CompactLifeChronicleEntry[]> _entrySegments = [];
    private readonly List<int[]> _nextSegments = [];
    private readonly Stack<int> _freeBlocks = [];
    private readonly PersonIdentityId[] _firstPersonIds;
    private readonly string?[] _firstPersonNames;
    private Dictionary<PersonIdentityId, string>? _additionalPersonNames;
    private int _blockCount;
    private int _activeBlockCount;

    internal static int StoredEntrySizeBytes =>
        Unsafe.SizeOf<CompactLifeChronicleEntry>();

    internal int AllocatedEntrySlotCount => checked(
        _entrySegments.Count * BlocksPerSegment * EntriesPerBlock);

    internal int AllocatedBlockCount => _activeBlockCount;

    public CompactChronicleStore(int population)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        _population = population;
        _heads = new int[population];
        _tails = new int[population];
        _counts = new int[population];
        _headSlots = new byte[population];
        _tailCounts = new byte[population];
        _protected = new bool[population];
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

    public void SetProtected(int agentIndex, bool isProtected)
    {
        ValidateAgent(agentIndex);
        _protected[agentIndex] = isProtected;
        if (!isProtected)
        {
            TrimToLast(agentIndex, PersonalChronicleRules.DetailedRetentionLimit);
        }
    }

    public bool IsProtected(int agentIndex)
    {
        ValidateAgent(agentIndex);
        return _protected[agentIndex];
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

    public void RegisterPersonNameForLife(
        int agentIndex,
        int lifeNumber,
        string personName)
    {
        ValidateAgent(agentIndex);
        RegisterPersonName(
            agentIndex,
            PersonCausalRules.CreatePersonId(agentIndex, lifeNumber),
            personName);
    }

    public void PrunePersonNameForLife(int agentIndex, int lifeNumber)
    {
        ValidateAgent(agentIndex);
        if (ContainsLife(agentIndex, lifeNumber))
        {
            return;
        }

        var personId = PersonCausalRules.CreatePersonId(agentIndex, lifeNumber);
        if (_firstPersonIds[agentIndex] == personId)
        {
            _firstPersonIds[agentIndex] = default;
            _firstPersonNames[agentIndex] = null;
        }
        _additionalPersonNames?.Remove(personId);
    }

    public void PrunePersonNames(
        int agentIndex,
        Func<int, bool> retainArchivedLife)
    {
        ValidateAgent(agentIndex);
        ArgumentNullException.ThrowIfNull(retainArchivedLife);
        if (_firstPersonNames[agentIndex] is not null)
        {
            var lifeNumber = checked((int)(_firstPersonIds[agentIndex].Value >> 32));
            if (!retainArchivedLife(lifeNumber) && !ContainsLife(agentIndex, lifeNumber))
            {
                _firstPersonIds[agentIndex] = default;
                _firstPersonNames[agentIndex] = null;
            }
        }
        if (_additionalPersonNames is null)
        {
            return;
        }

        List<PersonIdentityId>? removals = null;
        var encodedAgent = checked((uint)agentIndex + 1U);
        foreach (var (personId, _) in _additionalPersonNames)
        {
            if ((uint)personId.Value != encodedAgent)
            {
                continue;
            }
            var lifeNumber = checked((int)(personId.Value >> 32));
            if (!retainArchivedLife(lifeNumber) && !ContainsLife(agentIndex, lifeNumber))
            {
                (removals ??= []).Add(personId);
            }
        }
        if (removals is null)
        {
            return;
        }
        foreach (var personId in removals)
        {
            _additionalPersonNames.Remove(personId);
        }
    }

    private int GetCount(int agentIndex) => _counts[agentIndex];

    private bool ContainsLife(int agentIndex, int lifeNumber)
    {
        var remaining = _counts[agentIndex];
        var block = _heads[agentIndex];
        var slot = (int)_headSlots[agentIndex];
        while (remaining-- > 0)
        {
            if (slot == EntriesPerBlock)
            {
                block = GetNextBlock(block);
                slot = 0;
            }
            if (GetCompactEntry(block, slot++).LifeNumber == lifeNumber)
            {
                return true;
            }
        }
        return false;
    }

    private LifeChronicleEntry Get(int agentIndex, int logicalIndex)
    {
        if ((uint)logicalIndex >= (uint)_counts[agentIndex])
        {
            throw new ArgumentOutOfRangeException(nameof(logicalIndex));
        }

        var block = _heads[agentIndex];
        var remaining = checked(_headSlots[agentIndex] + logicalIndex);
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
        if (_tails[agentIndex] < 0 || _tailCounts[agentIndex] == EntriesPerBlock)
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
            _tailCounts[agentIndex] = 0;
        }

        SetCompactEntry(
            _tails[agentIndex],
            _tailCounts[agentIndex]++,
            CompactLifeChronicleEntry.Create(value));
        _counts[agentIndex] = checked(count + 1);
        if (!_protected[agentIndex])
        {
            TrimToLast(agentIndex, PersonalChronicleRules.DetailedRetentionLimit);
        }
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
        var slot = (int)_headSlots[agentIndex];
        while (block >= 0 && resultIndex < count)
        {
            var entries = Math.Min(EntriesPerBlock - slot, count - resultIndex);
            for (var offset = 0; offset < entries; offset++)
            {
                result[resultIndex++] = Expand(
                    agentIndex,
                    GetCompactEntry(block, slot + offset));
            }
            block = GetNextBlock(block);
            slot = 0;
        }
        return result;
    }

    public void RemoveArchivedThrough(int agentIndex, int lifeNumber, long ageYears)
    {
        ValidateAgent(agentIndex);
        if (_protected[agentIndex])
        {
            return;
        }

        while (_counts[agentIndex] > 0)
        {
            var first = GetCompactEntry(_heads[agentIndex], _headSlots[agentIndex]);
            if (first.LifeNumber > lifeNumber
                || (first.LifeNumber == lifeNumber && first.AgeYears > ageYears))
            {
                break;
            }
            RemoveFirst(agentIndex);
        }
    }

    private void TrimToLast(int agentIndex, int limit)
    {
        while (_counts[agentIndex] > limit)
        {
            RemoveFirst(agentIndex);
        }
    }

    private void RemoveFirst(int agentIndex)
    {
        var block = _heads[agentIndex];
        if (block < 0 || _counts[agentIndex] <= 0)
        {
            throw new InvalidOperationException("비어 있는 연대기에서 항목을 제거할 수 없습니다.");
        }

        _counts[agentIndex]--;
        _headSlots[agentIndex]++;
        if (_headSlots[agentIndex] == EntriesPerBlock)
        {
            var next = GetNextBlock(block);
            _heads[agentIndex] = next;
            _headSlots[agentIndex] = 0;
            ReleaseBlock(block);
        }
        if (_counts[agentIndex] == 0)
        {
            if (_heads[agentIndex] >= 0)
            {
                ReleaseBlock(_heads[agentIndex]);
            }
            _heads[agentIndex] = -1;
            _tails[agentIndex] = -1;
            _headSlots[agentIndex] = 0;
            _tailCounts[agentIndex] = 0;
        }
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
        if (_freeBlocks.TryPop(out var reused))
        {
            _activeBlockCount++;
            return reused;
        }

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
        _activeBlockCount++;
        return block;
    }

    private void ReleaseBlock(int block)
    {
        SetNextBlock(block, -1);
        _freeBlocks.Push(block);
        _activeBlockCount--;
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
                _slot = owner._headSlots[agentIndex];
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
                _slot = _owner._headSlots[_agentIndex];
                _current = default;
            }
            public void Dispose() { }
        }
    }
}
