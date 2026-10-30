namespace Chronicles.Game.Inventory;

public static partial class InventoryEngine
{
    /// <summary>Checks physical identity, ownership/location, commitments, storage, labor and material conservation without mutation.</summary>
    public static void Validate(InventoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        try { ValidateCore(state); }
        catch (RuleViolation error) { throw new InvalidDataException(error.Message, error); }
    }
    private static void Ensure(bool condition, string message)
    { if (!condition) throw new InvalidDataException(message); }
    private static void ValidateCore(InventoryState state)
    {
        Ensure(state.Month >= 0 && state.Revision >= 0 && state.Ledger.BaseCarryCapacity >= 0 && state.Ledger.ProductionWork > 0 && state.Ledger.ProfessionalWork > 0 &&
            state.Ledger.ExtractionWorkPerUnit > 0, "Invalid inventory clock, carrying profile, or work profile.");
        var ids = state.Items.Select(i => i.Id).Concat(state.People.Select(p => p.Id))
            .Concat(state.Ledger.Reservations.Select(r => r.Id)).Concat(state.Ledger.Labor.Select(l => l.Id))
            .Concat(state.Ledger.Jobs.Select(j => j.Id)).Concat(state.Ledger.Contracts.Select(c => c.Id))
            .Concat(state.Ledger.Sources.Select(s => s.Id)).Concat(state.Ledger.Arrays.Select(a => a.Id)).Concat(state.Ledger.Bindings.Select(b => b.Id)).ToArray();
        Ensure(ids.All(id => id > 0) && ids.Distinct().Count() == ids.Length && state.NextId > ids.DefaultIfEmpty().Max(), "Inventory IDs are duplicated or the allocator is stale.");
        Ensure(state.Ledger.Metadata.Select(m => m.Item).Distinct().Count() == state.Ledger.Metadata.Count, "Duplicate item metadata.");
        foreach (var metadata in state.Ledger.Metadata)
        {
            ItemAt(state, metadata.Item);
            if (metadata.Custodian != 0) PersonAt(state, metadata.Custodian, false);
            if (metadata.Carrier != 0) PersonAt(state, metadata.Carrier, false);
            Ensure(metadata.Capacity is null or >= 0 && metadata.UseRights.Distinct().Count() == metadata.UseRights.Count, "Invalid storage profile or use rights.");
            foreach (var user in metadata.UseRights) PersonAt(state, user, false);
        }
        foreach (var item in state.Items)
        {
            PersonAt(state, item.Owner, false); var definition = InventoryDefinitions.Definition(item.Definition);
            Ensure(item.Quantity > 0 && item.Quality is >= 0 and <= 10000 && item.Damage is >= 0 and <= 100 && item.Place is >= -1 and < 100,
                "Invalid physical item quantity, quality, damage, or location.");
            Ensure(item.BoundSoul >= 0 && ((int)item.Defects & ~15) == 0, "Invalid soul binding or defect flags.");
            Ensure(definition.Item.Kind != ItemKind.Book || item.Quantity == 1, "A bound or blank book is a unique physical medium.");
            var root = Root(state, item); _ = Location(state, item.Id);
            Ensure(item.Place == root.Place, "Contained items cannot have another physical location.");
            if (definition.Item.Capacity > 0 || (Metadata(state, item.Id)?.Capacity ?? 0) > 0)
                Ensure(item.Quantity == 1, "Containers cannot be stacked.");
            if (item.Container != 0)
            {
                var parent = ItemAt(state, item.Container); var metadata = Metadata(state, parent.Id);
                Ensure(Capacity(state, parent) > 0, "Contents are inside an unusable container.");
                Ensure(metadata is null || metadata.AllowedContents.Count == 0 || metadata.AllowedContents.Contains(definition.Item.Kind), "Container rejects this item category.");
                Ensure(metadata is null || metadata.AllowNestedContainers || (definition.Item.Capacity == 0 && (Metadata(state, item.Id)?.Capacity ?? 0) == 0), "Container profile disallows nested storage.");
            }
            var children = state.Items.Where(i => i.Container == item.Id).ToArray();
            if (children.Length > 0)
                Ensure(children.Sum(child => Weight(state, child)) <= Capacity(state, item), "Recursive container capacity exceeded.");
            if (item.Reserved != 0)
                Ensure(state.Ledger.Reservations.Any(r => r.Id == item.Reserved && r.Active && r.Items.Contains(item.Id)), "Physical reservation is not backed by the common ledger.");
        }
        foreach (var person in state.People)
        {
            Ensure(person.Money >= 0 && person.Place is >= 0 and < 100, "Negative money or invalid person location.");
            Ensure(CarriedWeight(state, person) <= CarryCapacity(state, person), "Carrying capacity exceeded; use explicit ground storage.");
            foreach (var itemId in person.Equipment.Values.Distinct())
            {
                var item = ItemAt(state, itemId); var definition = InventoryDefinitions.Definition(item.Definition);
                var occupied = person.Equipment.Where(e => e.Value == itemId).Select(e => e.Key).Order().ToArray();
                Ensure(occupied.SequenceEqual(definition.OccupiedSlots.Order()) && item.Quantity == 1 && item.Container == 0 && item.Place == -1 &&
                    Location(state, item.Id) == person.Place && item.Damage < 100 &&
                    (item.Defects & (ItemDefect.BrokenCircuit | ItemDefect.LostCore)) == 0 && (item.BoundSoul == 0 || item.BoundSoul == person.SoulId), "Invalid equipment occupancy, location, or soul binding.");
                Ensure(item.Owner == person.Id || (Metadata(state, item.Id)?.UseRights.Contains(person.Id) ?? false), "Equipment lacks ownership or use rights.");
                Ensure(state.People.Count(p => p.Equipment.ContainsValue(itemId)) == 1, "An item is equipped by multiple people.");
                Ensure(item.Reserved == 0 || state.Ledger.Jobs.Any(j => j.Target == item.Id && j.Recipe == "repair" && j.Reservation == item.Reserved && j.Status is "reserved" or "processing"), "Equipped item is committed to another activity.");
            }
        }
        foreach (var reservation in state.Ledger.Reservations.Where(r => r.Active))
        {
            PersonAt(state, reservation.Actor, false);
            Ensure(reservation.Items.Distinct().Count() == reservation.Items.Count, "Duplicate reserved physical item.");
            foreach (var id in reservation.Items)
                Ensure(state.Items.Any(i => i.Id == id && i.Reserved == reservation.Id) ||
                    state.Ledger.Jobs.Any(j => j.Reservation == reservation.Id && j.Status == "processing" && j.Inputs.Contains(id)), "Active reservation lost its physical item.");
        }
        foreach (var labor in state.Ledger.Labor)
        {
            PersonAt(state, labor.Person, false);
            Ensure(labor.Start >= 0 && labor.End > labor.Start && labor.Units is > 0 and <= 100 && labor.FacilityCapacity > 0, "Invalid labor interval.");
        }
        foreach (var group in state.Ledger.Labor.Where(l => l.Active).GroupBy(l => l.Person)) CheckSchedule(group, 100);
        foreach (var group in state.Ledger.Labor.Where(l => l.Active && l.Facility != 0).GroupBy(l => l.Facility))
        {
            Ensure(group.Select(l => l.FacilityCapacity).Distinct().Count() == 1, "Facility commitments disagree about capacity.");
            CheckSchedule(group, group.First().FacilityCapacity);
        }
        foreach (var source in state.Ledger.Sources)
        {
            InventoryDefinitions.Definition(source.Definition);
            Ensure(source.Place is >= 0 and < 100 && source.Remaining >= 0 && source.Capacity >= source.Remaining && source.Regeneration >= 0 && source.EcologyWeight >= 0 &&
                source.LastMonth <= state.Month && !string.IsNullOrWhiteSpace(source.Origin), "Invalid finite natural stock.");
            var reserved = state.Ledger.Jobs.Where(j => j.Source == source.Id && j.Status is "reserved" or "processing").Sum(j => (long)j.OutputQuantity);
            Ensure(reserved <= source.Remaining, "Natural stock is overcommitted.");
        }
        var committedReservations = state.Ledger.Jobs.Where(j => j.Status is "reserved" or "processing").Select(j => j.Reservation)
            .Concat(state.Ledger.Contracts.Where(c => c.Status == "open").Select(c => c.Reservation)).ToArray();
        var committedLabor = state.Ledger.Jobs.Where(j => j.Status is "reserved" or "processing").Select(j => j.Labor)
            .Concat(state.Ledger.Contracts.Where(c => c.Status == "open" && c.Labor != 0).Select(c => c.Labor)).ToArray();
        Ensure(committedReservations.Distinct().Count() == committedReservations.Length && committedLabor.Distinct().Count() == committedLabor.Length,
            "An asset or labor reservation backs multiple independent commitments.");
        foreach (var job in state.Ledger.Jobs)
        {
            PersonAt(state, job.Actor, false);
            if (job.OutputDefinition.Length > 0) InventoryDefinitions.Definition(job.OutputDefinition);
            Ensure(InventoryDefinitions.Recipes.ContainsKey(job.Recipe) || job.Recipe is "repair" or "extraction" or "medical-treatment" or "appraisal" or "exploration" or "array-maintenance",
                "Unknown production process in saved state.");
            Ensure((job.Source != 0) == (job.Recipe == "extraction") && (job.Source == 0 || (job.OutputQuantity > 0 && state.Ledger.Sources.Any(s => s.Id == job.Source))),
                "Extraction lacks an actual finite source or positive quantity.");
            Ensure(job.Inputs.Distinct().Count() == job.Inputs.Count, "Production input was reserved twice.");
            Ensure(job.Status is "reserved" or "processing" or "completed" or "cancelled" or "failed", "Unknown production status.");
            Ensure(state.Ledger.Reservations.Any(r => r.Id == job.Reservation) && state.Ledger.Labor.Any(l => l.Id == job.Labor), "Production lacks reservations.");
            if (job.Status is "reserved" or "processing")
            {
                Ensure(state.Ledger.Reservations.Any(r => r.Id == job.Reservation && r.Active) && state.Ledger.Labor.Any(l => l.Id == job.Labor && l.Active), "Active production lost its commitments.");
                if (job.Status == "reserved") foreach (var id in job.Inputs) ItemAt(state, id);
            }
        }
        foreach (var contract in state.Ledger.Contracts)
        {
            PersonAt(state, contract.Seller, false); if (contract.Buyer != 0) PersonAt(state, contract.Buyer, false);
            if (contract.Carrier != 0) PersonAt(state, contract.Carrier, false);
            Ensure(contract.Kind is "sale" or "auction" or "transport" && contract.Escrow >= 0 && contract.Price >= 0 && contract.Fee >= 0, "Invalid funded contract.");
            Ensure(contract.PaidAmount is null or >= 0 && contract.PaidQuantity is null or > 0 && contract.PaidQuality is null or (>= 0 and <= 10000) &&
                (contract.EndedMonth is null || contract.EndedMonth >= 0 && contract.EndedMonth <= state.Month), "Invalid actual settlement history.");
            if (contract.Status == "open")
            {
                Ensure(contract.PaidAmount is null && contract.EndedMonth is null, "Open contract cannot have a completed payment.");
                var item = ItemAt(state, contract.Item);
                Ensure(item.Reserved == contract.Reservation && item.Owner == contract.Seller &&
                    state.Ledger.Reservations.Any(r => r.Id == contract.Reservation && r.Active), "Open contract lost cargo ownership or reservation.");
                if (contract.Kind == "transport") Ensure(contract.Escrow == contract.Fee &&
                    Metadata(state, item.Id)?.Carrier == contract.Carrier && state.Ledger.Labor.Any(l => l.Id == contract.Labor && l.Active), "Transport lacks physical custody, fee, or labor.");
                else Ensure(Location(state, item.Id) == contract.Destination, "Listed property left the market.");
                if (contract.Kind == "auction") Ensure(contract.Buyer == 0 ? contract.Escrow == 0 : contract.Escrow >= contract.Price, "Auction bid is unfunded.");
            }
            else Ensure(contract.Escrow == 0, "Terminal contract retained escrow.");
        }
        foreach (var array in state.Ledger.Arrays)
        {
            var item = ItemAt(state, array.Item);
            Ensure(item.Owner == array.Owner && item.Container == 0 && item.Place == array.Place &&
                InventoryDefinitions.Definition(item.Definition).Item.Kind == ItemKind.Array, "Installed array lost its actual physical item.");
        }
        var sequencedEvents = state.Ledger.Events.Where(e => e.Sequence > 0).ToArray();
        Ensure(state.Ledger.NextEventSequence > sequencedEvents.Select(e => e.Sequence).DefaultIfEmpty().Max() &&
            sequencedEvents.Select(e => e.Sequence).Distinct().Count() == sequencedEvents.Length, "Physical audit stream sequence was reused.");
        foreach (var audit in state.Ledger.Events.Where(e => e.Definition.Length > 0))
        {
            InventoryDefinitions.Definition(audit.Definition); PersonAt(state, audit.Owner, false);
            Ensure(audit.Sequence > 0 && audit.Quantity > 0 && audit.Place is >= 0 and < 100 && audit.Month <= state.Month &&
                audit.Kind == (audit.IsLoss ? "physical-loss" : "physical-consumption"), "Invalid typed physical decrement evidence.");
        }
        foreach (var binding in state.Ledger.Bindings)
        {
            var item = state.Items.SingleOrDefault(i => i.Id == binding.Item);
            Ensure(binding.Soul > 0 && binding.Established <= state.Month && !string.IsNullOrWhiteSpace(binding.Evidence) &&
                (item is null || item.BoundSoul == binding.Soul), "Formation contract lost its evidence or soul binding.");
        }
        Ensure(state.Ledger.Bindings.Select(b => b.Item).Distinct().Count() == state.Ledger.Bindings.Count, "One physical item has competing formation bindings.");
        Ensure(state.Ledger.Effects.Select(e => e.Cause).Distinct(StringComparer.Ordinal).Count() == state.Ledger.Effects.Count, "An effect cause was applied twice.");
        foreach (var material in state.Materials)
        {
            var flow = material.Value;
            Ensure(flow.Opening >= 0 && flow.Extraction >= 0 && flow.Production >= 0 && flow.Consumption >= 0 && flow.Loss >= 0, "Negative material flow.");
            Ensure(checked(flow.Opening + flow.Extraction + flow.Production - flow.Consumption - flow.Loss) ==
                state.Items.Where(i => i.Definition == material.Key).Sum(i => (long)i.Quantity), "Physical stock does not match its material flow.");
        }
        Ensure(state.Items.All(i => state.Materials.ContainsKey(i.Definition)), "Physical item lacks material provenance.");
    }
    private static void CheckSchedule(IEnumerable<LaborReservation> schedule, int capacity)
    {
        var used = 0;
        foreach (var change in schedule.SelectMany(l => new[] { (Month: l.Start, Units: l.Units), (Month: l.End, Units: -l.Units) })
            .GroupBy(e => e.Month).OrderBy(e => e.Key))
        {
            used = checked(used + change.Sum(e => e.Units)); Ensure(used <= capacity, "Shared person or facility labor capacity exceeded.");
        }
    }
}
