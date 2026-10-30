namespace Chronicles.Game.Inventory;

public static partial class InventoryEngine
{
    // A fresh read scope for one Validate call. Never stored on state or reused by a mutation.
    private sealed class ValidationReadIndex
    {
        private readonly Dictionary<long, Person> people = [];
        private readonly Dictionary<long, Item> items = [];
        private readonly Dictionary<long, ItemMetadata> metadata = [];
        private readonly Dictionary<long, List<Item>> children = [];
        private readonly Dictionary<long, List<Item>> owned = [];
        private readonly Dictionary<long, List<Item>> borrowedRoots = [];
        private readonly Dictionary<long, Item> roots = [];
        private readonly Dictionary<long, long> weights = [];
        private readonly Dictionary<long, int> equippedBy = [];
        private readonly Dictionary<long, AssetReservation> reservations = [];
        private readonly Dictionary<long, LaborReservation> laborReservations = [];
        private readonly HashSet<long> sourceIds = [];
        private readonly Dictionary<long, List<ProductionJob>> activeJobsBySource = [];
        private readonly Dictionary<long, List<ProductionJob>> processingJobsByReservation = [];
        private readonly HashSet<(long Item, long Reservation)> equippedRepairs = [];
        private readonly InventoryState state;
        private bool equipmentIndexed;
        private bool jobsIndexed;

        public ValidationReadIndex(InventoryState state)
        {
            this.state = state;
            foreach (var person in state.People)
                Ensure(people.TryAdd(person.Id, person), "Inventory IDs are duplicated or the allocator is stale.");
            foreach (var item in state.Items)
                Ensure(items.TryAdd(item.Id, item), "Inventory IDs are duplicated or the allocator is stale.");
            foreach (var row in state.Ledger.Metadata)
                Ensure(metadata.TryAdd(row.Item, row), "Duplicate item metadata.");
            // Global identity checks have already run. Indexing numeric identities neither validates
            // later clauses nor dereferences physical/input lists that may only be visited later.
            foreach (var reservation in state.Ledger.Reservations) reservations.Add(reservation.Id, reservation);
            foreach (var labor in state.Ledger.Labor) laborReservations.Add(labor.Id, labor);
            foreach (var source in state.Ledger.Sources) sourceIds.Add(source.Id);
            foreach (var item in state.Items)
            {
                Append(owned, item.Owner, item);
                if (item.Container != 0) Append(children, item.Container, item);
                if (item.Container == 0 && item.Place == -1)
                {
                    var holder = Holder(item);
                    if (holder != item.Owner) Append(borrowedRoots, holder, item);
                }
            }
        }

        private static void Append<T>(Dictionary<long, List<T>> index, long id, T item)
        {
            if (!index.TryGetValue(id, out var rows)) index.Add(id, rows = []);
            rows.Add(item);
        }

        public Person PersonAt(long id)
        {
            Require(people.TryGetValue(id, out var person), "Person is absent or no longer alive.");
            return person!;
        }
        public Item ItemAt(long id) => FindItem(id) ?? throw new RuleViolation("Physical item does not exist.");
        public Item? FindItem(long id) => items.GetValueOrDefault(id);
        public ItemMetadata? Metadata(long id) => metadata.GetValueOrDefault(id);
        public IReadOnlyList<Item> Children(long id) => children.TryGetValue(id, out var rows) ? rows : Array.Empty<Item>();
        private long Holder(Item root)
        {
            var row = Metadata(root.Id);
            return row?.Carrier > 0 ? row.Carrier : row?.Custodian > 0 ? row.Custodian : root.Owner;
        }
        public Item Root(Item item)
        {
            var path = new List<Item>(); var visited = new HashSet<long>(); var root = item;
            while (root.Container != 0)
            {
                if (roots.TryGetValue(root.Id, out var known)) { root = known; break; }
                Require(visited.Add(root.Id), "Container cycle detected."); path.Add(root);
                root = ItemAt(root.Container);
            }
            roots.TryAdd(root.Id, root);
            foreach (var entry in path) roots[entry.Id] = root;
            return root;
        }
        public int Location(long id)
        {
            var root = Root(ItemAt(id));
            return root.Place >= 0 ? root.Place : PersonAt(Holder(root)).Place;
        }
        public int Capacity(Item item) => item.Damage >= 100 ||
            (item.Defects & (ItemDefect.BrokenCircuit | ItemDefect.LostCore)) != 0 ? 0 :
            Metadata(item.Id)?.Capacity ?? InventoryDefinitions.Definition(item.Definition).Item.Capacity;

        public long Weight(Item item)
        {
            if (weights.TryGetValue(item.Id, out var known)) return known;
            var pending = new Stack<(Item Item, bool Expanded)>(); var visiting = new HashSet<long>();
            pending.Push((item, false));
            while (pending.TryPop(out var next))
            {
                if (weights.ContainsKey(next.Item.Id)) continue;
                var descendants = Children(next.Item.Id);
                if (next.Expanded)
                {
                    var weight = checked((long)next.Item.Quantity * InventoryDefinitions.Definition(next.Item.Definition).Item.Weight);
                    foreach (var child in descendants) weight = checked(weight + weights[child.Id]);
                    weights.Add(next.Item.Id, weight); visiting.Remove(next.Item.Id);
                }
                else
                {
                    Require(visiting.Add(next.Item.Id), "Container cycle detected.");
                    pending.Push((next.Item, true));
                    for (var i = descendants.Count - 1; i >= 0; i--)
                        if (!weights.ContainsKey(descendants[i].Id)) pending.Push((descendants[i], false));
                }
            }
            return weights[item.Id];
        }
        public long CarriedWeight(Person person)
        {
            var weight = 0L;
            if (owned.TryGetValue(person.Id, out var own))
                foreach (var item in own.Where(i => i.Container == 0 && i.Place == -1 && Holder(i) == person.Id))
                    weight = checked(weight + Weight(item));
            if (borrowedRoots.TryGetValue(person.Id, out var borrowed))
                foreach (var item in borrowed) weight = checked(weight + Weight(item));
            return weight;
        }
        public long CarryCapacity(Person person) => state.Ledger.BaseCarryCapacity + person.Equipment.Values.Distinct()
            .Sum(id => (long)Capacity(ItemAt(id)));
        public int EquippedByCount(long item)
        {
            if (!equipmentIndexed)
            {
                foreach (var person in state.People)
                    foreach (var id in person.Equipment.Values.Distinct()) equippedBy[id] = equippedBy.GetValueOrDefault(id) + 1;
                equipmentIndexed = true;
            }
            return equippedBy.GetValueOrDefault(item);
        }

        public bool HasSource(long id) => sourceIds.Contains(id);
        public bool HasReservation(long id) => reservations.ContainsKey(id);
        public bool HasActiveReservation(long id) => reservations.TryGetValue(id, out var row) && row.Active;
        public bool HasLabor(long id) => laborReservations.ContainsKey(id);
        public bool HasActiveLabor(long id) => laborReservations.TryGetValue(id, out var row) && row.Active;
        public bool BacksPhysicalItem(long reservation, long item) =>
            reservations.TryGetValue(reservation, out var row) && row.Active && row.Items.Contains(item);

        private void IndexJobs()
        {
            if (jobsIndexed) return;
            foreach (var job in state.Ledger.Jobs)
            {
                if (job.Status is not ("reserved" or "processing")) continue;
                Append(activeJobsBySource, job.Source, job);
                if (job.Status == "processing") Append(processingJobsByReservation, job.Reservation, job);
                if (job.Recipe == "repair") equippedRepairs.Add((job.Target, job.Reservation));
            }
            jobsIndexed = true;
        }

        public bool BacksEquippedRepair(long item, long reservation)
        {
            IndexJobs();
            return equippedRepairs.Contains((item, reservation));
        }

        public bool BacksConsumedInput(long reservation, long item)
        {
            IndexJobs();
            if (processingJobsByReservation.TryGetValue(reservation, out var rows))
                // Keep job order and short-circuiting: eagerly copying every input list could visit
                // a malformed later list before the original Any predicate would reach it.
                foreach (var job in rows)
                    if (job.Inputs.Contains(item)) return true;
            return false;
        }

        public long ReservedNaturalStock(long source)
        {
            IndexJobs();
            var reserved = 0L;
            if (activeJobsBySource.TryGetValue(source, out var rows))
                // Sum only when this source's original validation clause is reached, in original
                // job order. Never pre-sum a later source ahead of an earlier source's rejection.
                foreach (var job in rows) reserved = checked(reserved + job.OutputQuantity);
            return reserved;
        }
    }
}
