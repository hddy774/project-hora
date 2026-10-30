using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Chronicles.Game.Inventory;

public static partial class InventoryEngine
{
    private sealed class RuleViolation(string message) : Exception(message);
    private static void Require(bool condition, string message)
    { if (!condition) throw new RuleViolation(message); }

    internal static string WorldFingerprint(GameState world) => Convert.ToHexStringLower(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new { world.Month, world.NextId, world.Revision, world.Items, world.People, world.Materials })));

    /// <summary>Never mutates input. Only install State when Accepted; a replay returns the current input unchanged.</summary>
    public static InventoryResult Apply(InventoryState input, InventoryCommand command)
    {
        ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(command);
        var signature = command.GetType().Name + ":" + JsonSerializer.Serialize(command, command.GetType());
        if (string.IsNullOrWhiteSpace(command.Id)) return new(false, "Command ID is required.", input);
        if (input.Ledger.Receipts.TryGetValue(command.Id, out var receipt))
            return receipt.Signature == signature ? new(true, receipt.Message, input, receipt.Subject, true)
                : new(false, "Command ID already belongs to a different request.", input);
        try
        {
            Validate(input);
            var state = InventoryState.Copy(input);
            if (command.Actor != 0) PersonAt(state, command.Actor);
            var subject = command switch
            {
                SplitInventoryItem c => Split(state, c), MergeStacks c => Merge(state, c), StoreItem c => Store(state, c),
                TransferOwnership c => Transfer(state, c), GrantUse c => Grant(state, c), SetCustodian c => Custody(state, c),
                EquipItem c => Equip(state, c), UnequipItem c => Unequip(state, c), BindItem c => Bind(state, c),
                DamageItem c => Damage(state, c), ReserveLabor c => Labor(state, c), ReleaseLabor c => Release(state, c),
                StartProduction c => StartJob(state, c), BeginProduction c => BeginJob(state, c),
                FinishProduction c => FinishJob(state, c), CancelProduction c => CancelJob(state, c),
                ExtractResource c => Extract(state, c), ConsumeItem c => Consume(state, c), RepairItem c => Repair(state, c),
                InstallArray c => Install(state, c), MaintainArray c => Maintain(state, c), DismantleArray c => Dismantle(state, c),
                OfferItem c => Offer(state, c), WithdrawOffer c => Withdraw(state, c), BuyItem c => Buy(state, c),
                PlaceBid c => Bid(state, c), BeginTransport c => Transport(state, c), FinishTransport c => Delivery(state, c),
                SettleInventory => Settle(state), _ => throw new RuleViolation("Unknown inventory command.")
            };
            state.Revision = checked(state.Revision + 1);
            state.Ledger.Receipts.Add(command.Id, new(signature, subject, "Accepted."));
            Validate(state);
            Require(TotalMoney(input) == TotalMoney(state), "Inventory transition changed total money without a real counterparty.");
            return new(true, "Accepted.", state, subject);
        }
        catch (Exception error) when (error is RuleViolation or InvalidDataException or OverflowException)
        { return new(false, error.Message, input); }
    }

    private static long TotalMoney(InventoryState state) => checked(state.People.Sum(p => p.Money) + state.Ledger.Contracts.Sum(c => c.Escrow));
    private static long Id(InventoryState state) => checked(state.NextId++);
    private static Person PersonAt(InventoryState state, long id, bool alive = true)
    {
        var person = state.People.SingleOrDefault(p => p.Id == id);
        Require(person is not null && (!alive || person.Status == LifeStatus.Alive), "Person is absent or no longer alive.");
        return person!;
    }
    private static Item ItemAt(InventoryState state, long id) => state.Items.SingleOrDefault(i => i.Id == id)
        ?? throw new RuleViolation("Physical item does not exist.");
    private static ItemMetadata? Metadata(InventoryState state, long id) => state.Ledger.Metadata.SingleOrDefault(x => x.Item == id);
    private static ItemMetadata Detail(InventoryState state, Item item)
    {
        var detail = Metadata(state, item.Id);
        if (detail is not null) return detail;
        detail = new() { Item = item.Id, Custodian = item.Owner, Provenance = [item.Source] };
        state.Ledger.Metadata.Add(detail); return detail;
    }
    private static List<Item> Tree(InventoryState state, Item root)
    {
        var result = new List<Item>(); var pending = new Queue<Item>(); pending.Enqueue(root);
        while (pending.TryDequeue(out var item))
        {
            Require(result.All(i => i.Id != item.Id), "Container cycle detected."); result.Add(item);
            foreach (var child in state.Items.Where(i => i.Container == item.Id).OrderBy(i => i.Id)) pending.Enqueue(child);
        }
        return result;
    }
    private static Item Root(InventoryState state, Item item)
    {
        var visited = new HashSet<long>();
        while (item.Container != 0)
        {
            Require(visited.Add(item.Id), "Container cycle detected."); item = ItemAt(state, item.Container);
        }
        return item;
    }
    public static int Location(InventoryState state, long itemId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var root = Root(state, ItemAt(state, itemId));
        if (root.Place >= 0) return root.Place;
        var detail = Metadata(state, root.Id);
        var holder = detail?.Carrier > 0 ? detail.Carrier : detail?.Custodian > 0 ? detail.Custodian : root.Owner;
        return PersonAt(state, holder, false).Place;
    }
    private static bool Equipped(InventoryState state, long item) => state.People.Any(p => p.Equipment.ContainsValue(item));
    private static void Uncommitted(InventoryState state, Item item)
    {
        foreach (var entry in Tree(state, item))
        {
            Require(entry.Reserved == 0, "An item is already reserved.");
            Require(!Equipped(state, entry.Id), "An equipped item cannot be reused.");
            Require(!state.Ledger.Arrays.Any(a => a.Item == entry.Id), "An installed array cannot be reused.");
        }
        for (var cursor = item; cursor.Container != 0;)
        { cursor = ItemAt(state, cursor.Container); Require(cursor.Reserved == 0, "An ancestor container is reserved."); }
    }
    private static Item Accessible(InventoryState state, long actor, long id, bool owner = true, bool free = true)
    {
        var item = ItemAt(state, id); var person = PersonAt(state, actor);
        Require(item.Owner == actor || (!owner && (Metadata(state, id)?.UseRights.Contains(actor) ?? false)), "No ownership or use right.");
        Require(Location(state, id) == person.Place, "The physical item is at another location.");
        var root = Root(state, item); var metadata = Metadata(state, root.Id);
        var holder = metadata?.Carrier > 0 ? metadata.Carrier : metadata?.Custodian > 0 ? metadata.Custodian : root.Owner;
        Require(root.Place >= 0 || holder == actor || (metadata?.UseRights.Contains(actor) ?? false), "Custody does not permit access.");
        if (free) Uncommitted(state, item);
        return item;
    }
    private static int Capacity(InventoryState state, Item item) => item.Damage >= 100 ||
        (item.Defects & (ItemDefect.BrokenCircuit | ItemDefect.LostCore)) != 0 ? 0 :
        Metadata(state, item.Id)?.Capacity ?? InventoryDefinitions.Definition(item.Definition).Item.Capacity;
    private static long Weight(InventoryState state, Item item) => Tree(state, item).Sum(i =>
        checked((long)i.Quantity * InventoryDefinitions.Definition(i.Definition).Item.Weight));
    private static void Move(InventoryState state, Item item, Person actor, long container, bool ground)
    {
        Require(!ground || container == 0, "Ground storage cannot also have a container.");
        if (container != 0)
        {
            var destination = Accessible(state, actor.Id, container, false);
            Require(destination.Id != item.Id && Tree(state, item).All(i => i.Id != destination.Id), "Container cycle rejected.");
            Require(Capacity(state, destination) > 0 && destination.Quantity == 1, "Destination is not an intact container.");
        }
        item.Container = container;
        var place = container == 0 ? ground ? actor.Place : -1 : ItemAt(state, container).Place;
        foreach (var entry in Tree(state, item)) entry.Place = place;
        if (container == 0) { var detail = Detail(state, item); detail.Custodian = actor.Id; detail.Carrier = 0; }
    }
    private static long Split(InventoryState state, SplitInventoryItem command)
    {
        var item = Accessible(state, command.Actor, command.Item);
        Require(command.Quantity > 0 && command.Quantity < item.Quantity, "Split quantity must leave both stacks nonempty.");
        Require(InventoryDefinitions.Definition(item.Definition).Item.Capacity == 0, "Containers are individual physical items.");
        return SplitItem(state, item, command.Quantity).Id;
    }
    private static Item SplitItem(InventoryState state, Item item, int quantity)
    {
        item.Quantity -= quantity;
        var split = new Item { Id = Id(state), Definition = item.Definition, Owner = item.Owner, Place = item.Place,
            Quantity = quantity, Quality = item.Quality, Damage = item.Damage, Defects = item.Defects, Container = item.Container,
            Reserved = item.Reserved, BoundSoul = item.BoundSoul, Source = item.Source };
        state.Items.Add(split);
        if (Metadata(state, item.Id) is { } detail)
            state.Ledger.Metadata.Add(new() { Item = split.Id, Custodian = detail.Custodian, Carrier = detail.Carrier,
                UseRights = [.. detail.UseRights], Provenance = [.. detail.Provenance], Capacity = detail.Capacity,
                AllowedContents = [.. detail.AllowedContents], AllowNestedContainers = detail.AllowNestedContainers });
        return split;
    }
    private static string MetadataKey(InventoryState state, Item item)
    {
        var detail = Metadata(state, item.Id);
        return JsonSerializer.Serialize(new { Custodian = detail?.Custodian ?? item.Owner, Carrier = detail?.Carrier ?? 0,
            UseRights = detail?.UseRights.Order().ToArray() ?? [], Provenance = detail?.Provenance.ToArray() ?? [item.Source],
            detail?.Capacity, Contents = detail?.AllowedContents.Order().ToArray() ?? [], Nested = detail?.AllowNestedContainers ?? true });
    }
    private static long Merge(InventoryState state, MergeStacks command)
    {
        var target = Accessible(state, command.Actor, command.Target); var source = Accessible(state, command.Actor, command.Source);
        Require(target.Id != source.Id && InventoryDefinitions.Definition(target.Definition).Item.Capacity == 0 &&
            target.Definition == source.Definition && target.Owner == source.Owner && target.Place == source.Place &&
            target.Container == source.Container && target.Quality == source.Quality && target.Damage == source.Damage &&
            target.Defects == source.Defects && target.BoundSoul == source.BoundSoul && target.Source == source.Source &&
            MetadataKey(state, target) == MetadataKey(state, source), "Distinct physical state cannot be merged.");
        target.Quantity = checked(target.Quantity + source.Quantity); Remove(state, source); return target.Id;
    }
    private static void Remove(InventoryState state, Item item)
    {
        Require(state.Items.All(i => i.Container != item.Id), "A nonempty container cannot be deleted.");
        state.Items.Remove(item); state.Ledger.Metadata.RemoveAll(x => x.Item == item.Id);
        foreach (var person in state.People)
            foreach (var slot in person.Equipment.Where(x => x.Value == item.Id).Select(x => x.Key).ToArray()) person.Equipment.Remove(slot);
    }
    private static long Store(InventoryState state, StoreItem command)
    {
        var item = Accessible(state, command.Actor, command.Item);
        Move(state, item, PersonAt(state, command.Actor), command.Container, command.Ground); return item.Id;
    }
    private static long Transfer(InventoryState state, TransferOwnership command)
    {
        var item = Accessible(state, command.Actor, command.Item); var recipient = PersonAt(state, command.Recipient);
        Require(recipient.Place == Location(state, item.Id), "Recipient is at another location.");
        Require(item.BoundSoul == 0, "A soul-bound item cannot be transferred as ordinary property.");
        _ = Detail(state, item); item.Owner = recipient.Id; return item.Id;
    }
    private static long Grant(InventoryState state, GrantUse command)
    {
        var item = Accessible(state, command.Actor, command.Item); PersonAt(state, command.User);
        var detail = Detail(state, item); detail.UseRights.Remove(command.User);
        if (command.Allowed) { detail.UseRights.Add(command.User); detail.UseRights.Sort(); }
        return item.Id;
    }
    private static long Custody(InventoryState state, SetCustodian command)
    {
        var item = Accessible(state, command.Actor, command.Item); var holder = PersonAt(state, command.Custodian);
        Require(holder.Place == Location(state, item.Id) && item.Container == 0, "Custody changes require local root storage.");
        Detail(state, item).Custodian = holder.Id;
        foreach (var entry in Tree(state, item)) entry.Place = -1;
        return item.Id;
    }
    private static void ClearEquipment(Person actor, long item)
    { foreach (var slot in actor.Equipment.Where(x => x.Value == item).Select(x => x.Key).ToArray()) actor.Equipment.Remove(slot); }
    private static long Equip(InventoryState state, EquipItem command)
    {
        var item = Accessible(state, command.Actor, command.Item, false); var actor = PersonAt(state, command.Actor);
        var definition = InventoryDefinitions.Definition(item.Definition);
        Require(item.Quantity == 1 && item.Container == 0 && definition.OccupiedSlots.Count > 0 && item.Damage < 100 &&
            (item.Defects & (ItemDefect.BrokenCircuit | ItemDefect.LostCore)) == 0, "Item is not usable equipment.");
        Require(item.BoundSoul == 0 || item.BoundSoul == actor.SoulId, "Another soul owns the binding.");
        var displaced = definition.OccupiedSlots.Where(actor.Equipment.ContainsKey).Select(slot => actor.Equipment[slot]).Distinct().ToArray();
        foreach (var oldId in displaced)
        {
            var old = ItemAt(state, oldId); Require(old.Reserved == 0, "Displaced equipment is reserved.");
            ClearEquipment(actor, oldId); Move(state, old, actor, command.ReturnContainer, command.ReturnToGround);
        }
        Move(state, item, actor, 0, false);
        foreach (var slot in definition.OccupiedSlots) actor.Equipment[slot] = item.Id;
        return item.Id;
    }
    private static long Unequip(InventoryState state, UnequipItem command)
    {
        var actor = PersonAt(state, command.Actor); var item = ItemAt(state, command.Item);
        Require(actor.Equipment.ContainsValue(item.Id) && item.Reserved == 0, "Equipment is absent or reserved.");
        ClearEquipment(actor, item.Id); Move(state, item, actor, command.ReturnContainer, command.ReturnToGround); return item.Id;
    }
    private static long Bind(InventoryState state, BindItem command)
    {
        var item = Accessible(state, command.Actor, command.Item, true, false); var actor = PersonAt(state, command.Actor);
        Require(item.Reserved == 0 && item.Quantity == 1 && item.BoundSoul == 0 &&
            InventoryDefinitions.Definition(item.Definition).OccupiedSlots.Count > 0 && !string.IsNullOrWhiteSpace(command.Evidence), "Binding requires unique equipment and formation evidence.");
        item.BoundSoul = actor.SoulId; var id = Id(state);
        state.Ledger.Bindings.Add(new(id, item.Id, actor.SoulId, state.Month, command.Evidence)); return id;
    }
    private static long Damage(InventoryState state, DamageItem command)
    {
        var item = Accessible(state, command.Actor, command.Item, true, false);
        Require(command.Damage is >= 0 and <= 100 && item.Reserved == 0, "Damage must be between zero and one hundred on unreserved property.");
        var location = Location(state, item.Id); item.Damage = command.Damage;
        if (item.Damage >= 100)
        {
            foreach (var person in state.People) ClearEquipment(person, item.Id);
            foreach (var child in state.Items.Where(i => i.Container == item.Id).OrderBy(i => i.Id).ToArray())
            { child.Container = 0; foreach (var entry in Tree(state, child)) entry.Place = location; }
            SpillOverflow(state, command.Actor);
        }
        return item.Id;
    }
    private static void SpillOverflow(InventoryState state, long actorId)
    {
        var actor = PersonAt(state, actorId);
        foreach (var root in CarriedRoots(state, actor).Where(i => !actor.Equipment.ContainsValue(i.Id)).OrderByDescending(i => i.Id).ToArray())
        {
            if (CarriedWeight(state, actor) <= CarryCapacity(state, actor)) break;
            foreach (var entry in Tree(state, root)) entry.Place = actor.Place;
        }
    }
    private static IEnumerable<Item> CarriedRoots(InventoryState state, Person person) => state.Items.Where(i => i.Container == 0 && i.Place == -1 &&
        ((Metadata(state, i.Id)?.Carrier is > 0 ? Metadata(state, i.Id)!.Carrier : Metadata(state, i.Id)?.Custodian is > 0 ? Metadata(state, i.Id)!.Custodian : i.Owner) == person.Id));
    private static long CarriedWeight(InventoryState state, Person person) => CarriedRoots(state, person).Sum(i => Weight(state, i));
    private static long CarryCapacity(InventoryState state, Person person) => state.Ledger.BaseCarryCapacity + person.Equipment.Values.Distinct()
        .Sum(id => (long)Capacity(state, ItemAt(state, id)));
    private static long Labor(InventoryState state, ReserveLabor command) => AddLabor(state, command.Actor, command.Purpose,
        command.Start, command.End, command.Units, command.Facility, command.FacilityCapacity);
    private static long AddLabor(InventoryState state, long actor, string purpose, long start, long end, int units, long facility = 0, int facilityCapacity = 100)
    {
        PersonAt(state, actor);
        Require(start >= state.Month && end > start && units is > 0 and <= 100 && !string.IsNullOrWhiteSpace(purpose) &&
            facility >= 0 && facilityCapacity > 0, "Invalid shared labor reservation.");
        var id = Id(state);
        state.Ledger.Labor.Add(new() { Id = id, Person = actor, Purpose = purpose, Start = start, End = end, Units = units,
            Facility = facility, FacilityCapacity = facilityCapacity }); return id;
    }
    private static long Release(InventoryState state, ReleaseLabor command)
    {
        var labor = state.Ledger.Labor.SingleOrDefault(l => l.Id == command.Reservation);
        Require(labor is { Active: true } && labor.Person == command.Actor &&
            !state.Ledger.Jobs.Any(j => j.Labor == labor.Id && j.Status is "reserved" or "processing") &&
            !state.Ledger.Contracts.Any(c => c.Labor == labor.Id && c.Status == "open"), "Only an uncommitted labor reservation can be released.");
        labor!.Active = false; return labor.Id;
    }
    private static void Event(InventoryState state, string kind, long actor, long subject, string detail) =>
        state.Ledger.Events.Add(new(state.Month, kind, actor, subject, detail));
    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
