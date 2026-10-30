namespace Chronicles.Game.Execution;

internal readonly record struct ActionSchedule(ExecutionIdentity Identity, ActionKind Kind, long Target,
    string Data, long OwnerRevision, long Due, bool Alive)
{
    internal bool Active => Alive && Kind != ActionKind.Idle;
    internal static ActionSchedule Of(Person p) => new(ExecutionIdentity.Of(p), p.Action, p.ActionTarget,
        p.ActionData, p.ActionRevision, p.Due, p.Status == LifeStatus.Alive);
}

internal readonly record struct EventOrder(long Tick, int Phase, long Subject, long Sequence) : IComparable<EventOrder>
{
    public int CompareTo(EventOrder other)
    {
        var c = Tick.CompareTo(other.Tick); if (c != 0) return c;
        c = Phase.CompareTo(other.Phase); if (c != 0) return c;
        c = Subject.CompareTo(other.Subject); return c != 0 ? c : Sequence.CompareTo(other.Sequence);
    }
}

internal sealed record ScheduledAction(ActionSchedule Schedule, EventOrder Order);

/// <summary>Heap promotion determines eligibility; the world's existing Person order still executes work.</summary>
internal sealed class StableEventQueue
{
    private PriorityQueue<ScheduledAction, EventOrder> heap = new();
    private readonly Dictionary<long, ScheduledAction> active = [];
    private readonly Dictionary<long, ScheduledAction> ready = [];
    private long sequence;
    internal int QueuedCount => heap.Count;
    internal int ActiveCount => active.Count;
    internal long Discarded { get; private set; }
    internal long Rebuilds { get; private set; }

    internal void Reconcile(ActionSchedule schedule)
    {
        var id = schedule.Identity.Person;
        if (!schedule.Active) { Cancel(id); return; }
        if (active.TryGetValue(id, out var current) && current.Schedule == schedule) return;
        Cancel(id);
        var entry = new ScheduledAction(schedule, new(schedule.Due, 0, id, checked(++sequence)));
        active.Add(id, entry); heap.Enqueue(entry, entry.Order);
    }

    internal void Cancel(long person)
    {
        active.Remove(person); ready.Remove(person);
    }

    internal bool TakeDue(ActionSchedule current, long month)
    {
        Reconcile(current); Promote(month);
        if (!ready.TryGetValue(current.Identity.Person, out var entry) || entry.Schedule != current) return false;
        Cancel(current.Identity.Person); return true;
    }

    internal void Promote(long month)
    {
        while (heap.TryPeek(out var entry, out var order) && order.Tick <= month)
        {
            heap.Dequeue();
            if (active.TryGetValue(entry.Schedule.Identity.Person, out var current) && current.Order == entry.Order)
                ready[entry.Schedule.Identity.Person] = entry;
            else Discarded++;
        }
    }

    internal EventOrder? Next()
    {
        while (heap.TryPeek(out var entry, out _))
        {
            if (active.TryGetValue(entry.Schedule.Identity.Person, out var current) && current.Order == entry.Order) return entry.Order;
            heap.Dequeue(); Discarded++;
        }
        return null;
    }

    internal EventOrder[] ReadyOrder() => ready.Values.OrderBy(e => e.Order).Select(e => e.Order).ToArray();

    internal void Rebuild()
    {
        var replacement = new PriorityQueue<ScheduledAction, EventOrder>();
        foreach (var entry in active.Values)
            if (!ready.ContainsKey(entry.Schedule.Identity.Person)) replacement.Enqueue(entry, entry.Order);
        Discarded += heap.Count - replacement.Count; heap = replacement; Rebuilds++;
    }

    // A deterministic provisional memory bound, not a measured optimal compaction threshold.
    internal void BoundInvalidEntries()
    {
        if (heap.Count > Math.Max(32L, (long)active.Count * 2)) Rebuild();
    }
}
