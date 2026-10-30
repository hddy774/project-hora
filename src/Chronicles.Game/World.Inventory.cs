namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private bool Accessible(Person p, long id, out Item item, ItemKind? kind = null)
        => InventoryAccessible(p, id, out item, kind);

    private int Inventory(long owner, string definition) => people.TryGetValue(owner, out var person)
        ? AvailableInventory(person, definition) : 0;

    private Item? AddItem(long owner, string definition, int quantity, int quality, string source, string flow = "production")
    {
        if (quantity <= 0) return null;
        if (flow != "transfer")
        {
            var movement = Material(definition);
            if (flow == "opening") movement.Opening += quantity;
            else if (flow == "extraction") movement.Extraction += quantity;
            else movement.Production += quantity;
        }
        var item = new Item { Id = Id(), Owner = owner, Definition = definition, Quantity = quantity,
            Quality = Math.Clamp(quality, 0, 10000), Place = -1, Source = source };
        state.Items.Add(item); items.Add(item.Id, item);
        if (!ownedItems.TryGetValue(owner, out var inventory)) ownedItems.Add(owner, inventory = []);
        inventory.Add(item); return item;
    }

    private List<Item> Owned(long owner) => ownedItems.GetValueOrDefault(owner) ?? [];
    private MaterialFlow Material(string definition)
    {
        if (!state.Materials.TryGetValue(definition, out var flow)) state.Materials.Add(definition, flow = new());
        return flow;
    }
    private void Transfer(Item item, long owner)
    {
        Owned(item.Owner).Remove(item); item.Owner = owner;
        if (!ownedItems.TryGetValue(owner, out var inventory)) ownedItems.Add(owner, inventory = []);
        inventory.Add(item);
    }

    private void Remove(Item item)
    {
        state.Inventory.Metadata.RemoveAll(m => m.Item == item.Id);
        foreach (var reservation in state.Inventory.Reservations.Where(r => r.Items.Remove(item.Id)))
            if (reservation.Items.Count == 0) reservation.Active = false;
        state.Items.Remove(item); items.Remove(item.Id); Owned(item.Owner).Remove(item);
        if (people.TryGetValue(item.Owner, out var owner))
            foreach (var slot in owner.Equipment.Where(x => x.Value == item.Id).Select(x => x.Key).ToArray()) owner.Equipment.Remove(slot);
    }

    private void Consume(Person p, string definition, int quantity)
    {
        if (quantity <= 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(quantity);
            return;
        }
        var consumed = quantity;
        foreach (var item in Owned(p.Id).Where(i => i.Definition == definition && InventoryAccessible(p, i.Id, out _))
                     .OrderBy(i => i.Id).ToArray())
        {
            var take = Math.Min(quantity, item.Quantity); item.Quantity -= take; quantity -= take;
            Material(definition).Consumption += take;
            if (item.Quantity == 0) Remove(item);
            if (quantity == 0) { RecordEcologyConsumption(p, definition, consumed); return; }
        }
        if (quantity != 0) throw new InvalidOperationException("검증된 소비량이 재고를 초과했습니다.");
    }

    private void Reserve(Person p, string definition, int quantity, long reservation)
    {
        foreach (var item in Owned(p.Id).Where(i => i.Definition == definition && InventoryAccessible(p, i.Id, out _))
                     .OrderBy(i => i.Id).ToArray())
        {
            var take = Math.Min(quantity, item.Quantity); quantity -= take;
            if (take == item.Quantity) item.Reserved = reservation;
            else
            {
                item.Quantity -= take;
                var split = AddItem(p.Id, definition, take, item.Quality, $"분할 {item.Id}", "transfer")!;
                split.Reserved = reservation; split.Damage = item.Damage; split.Defects = item.Defects;
                split.Container = item.Container; split.BoundSoul = item.BoundSoul; split.Place = item.Place;
            }
            if (quantity == 0) return;
        }
        throw new InvalidOperationException("예약 전 재료 검사 실패");
    }

}
