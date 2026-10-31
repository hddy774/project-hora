namespace Chronicles.Game.Execution;

internal readonly record struct ExecutionIdentity(long Person, long Body, long Soul, int Slot, uint Generation)
{
    internal static ExecutionIdentity Of(Person p) => new(p.Id, p.BodyId, p.SoulId, p.Slot, p.SlotGeneration);
}

internal readonly record struct PopulationRow(ExecutionIdentity Identity, int Ordinal, long StructureRevision, long LeaseGeneration,
    int Place, int Health, ActionKind Action, long Due);

/// <summary>Derived hot columns, in the original Person list order. Person remains the only writer.</summary>
internal sealed class PopulationColumns(int chunkSize = 64)
{
    private sealed class Chunk(int size)
    {
        internal readonly long[] Person = new long[size], Body = new long[size], Soul = new long[size], Due = new long[size];
        internal readonly int[] Slot = new int[size], Place = new int[size], Health = new int[size];
        internal readonly uint[] Generation = new uint[size];
        internal readonly ActionKind[] Action = new ActionKind[size];
        // Keep the same object references that the former living Person[] held, including same-month death.
        internal readonly Person?[] Original = new Person?[size];
    }
    private readonly int width = chunkSize > 0 ? chunkSize : throw new ArgumentOutOfRangeException(nameof(chunkSize));
    private readonly List<Chunk> chunks = [];
    private int count;
    private bool borrowed;
    private long leaseGeneration;
    internal long StructureRevision { get; private set; }
    internal int ChunkCount => chunks.Count;

    internal void Refresh(IReadOnlyList<Person> people, IReadOnlyList<PopulationSlot> slots)
    {
        if (borrowed) throw new InvalidOperationException("Population columns cannot move during a read lease.");
        foreach (var chunk in chunks) Array.Clear(chunk.Original);
        count = 0; StructureRevision = checked(StructureRevision + 1);
        foreach (var p in people)
        {
            if (p.Status != LifeStatus.Alive) continue;
            if (p.Slot < 0 || p.Slot >= slots.Count || slots[p.Slot].Person != p.Id || slots[p.Slot].Generation != p.SlotGeneration)
                throw new InvalidDataException("A hot-column capture requires the actual current person/slot generation.");
            if (count / width == chunks.Count) chunks.Add(new(width));
            var c = chunks[count / width]; var n = count % width;
            c.Person[n] = p.Id; c.Body[n] = p.BodyId; c.Soul[n] = p.SoulId; c.Slot[n] = p.Slot;
            c.Generation[n] = p.SlotGeneration; c.Place[n] = p.Place; c.Health[n] = p.Health;
            c.Action[n] = p.Action; c.Due[n] = p.Due; c.Original[n] = p; count++;
        }
        var needed = (count + width - 1) / width;
        if (chunks.Count > needed) chunks.RemoveRange(needed, chunks.Count - needed);
    }

    internal ScopedLease<PopulationRow> Borrow()
    {
        if (borrowed) throw new InvalidOperationException("Population columns already have a reader.");
        borrowed = true; var revision = StructureRevision; var generation = checked(++leaseGeneration);
        return new(count, Read, () => Check(revision, generation), () => borrowed = false);
    }

    private PopulationRow Read(int index)
    {
        var c = chunks[index / width]; var n = index % width;
        return new(new(c.Person[n], c.Body[n], c.Soul[n], c.Slot[n], c.Generation[n]), index,
            StructureRevision, leaseGeneration, c.Place[n], c.Health[n], c.Action[n], c.Due[n]);
    }

    internal Person Original(PopulationRow row)
    {
        Check(row.StructureRevision, row.LeaseGeneration);
        if ((uint)row.Ordinal >= (uint)count || Read(row.Ordinal).Identity != row.Identity)
            throw new InvalidOperationException("A population row does not belong to this capture.");
        return chunks[row.Ordinal / width].Original[row.Ordinal % width]
            ?? throw new InvalidOperationException("The captured object was released.");
    }

    private void Check(long revision, long generation)
    {
        if (!borrowed || revision != StructureRevision || generation != leaseGeneration)
            throw new InvalidOperationException("The population lease is stale.");
    }
}
