namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private bool Accessible(Person p, long id, out Item item, ItemKind? kind = null)
    {
        if (items.TryGetValue(id, out var found) && found.Owner == p.Id && found.Reserved == 0 &&
            (found.Place == -1 || found.Place == p.Place) &&
            (kind is null || Catalog.Items[found.Definition].Kind == kind)) { item = found; return true; }
        item = null!; return false;
    }

    private int Inventory(long owner, string definition) => Owned(owner).Where(i =>
        i.Definition == definition && i.Reserved == 0 && (i.Place == -1 || i.Place == people[owner].Place)).Sum(i => i.Quantity);

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
        state.Items.Remove(item); items.Remove(item.Id); Owned(item.Owner).Remove(item);
        if (people.TryGetValue(item.Owner, out var owner))
            foreach (var slot in owner.Equipment.Where(x => x.Value == item.Id).Select(x => x.Key).ToArray()) owner.Equipment.Remove(slot);
    }

    private void Consume(Person p, string definition, int quantity)
    {
        foreach (var item in Owned(p.Id).Where(i => i.Definition == definition && i.Reserved == 0 &&
                     (i.Place == -1 || i.Place == p.Place)).OrderBy(i => i.Id).ToArray())
        {
            var take = Math.Min(quantity, item.Quantity); item.Quantity -= take; quantity -= take;
            Material(definition).Consumption += take;
            if (item.Quantity == 0) Remove(item);
            if (quantity == 0) return;
        }
        if (quantity != 0) throw new InvalidOperationException("검증된 소비량이 재고를 초과했습니다.");
    }

    private void Reserve(Person p, string definition, int quantity, long reservation)
    {
        foreach (var item in Owned(p.Id).Where(i => i.Definition == definition && i.Reserved == 0 &&
                     (i.Place == -1 || i.Place == p.Place)).OrderBy(i => i.Id).ToArray())
        {
            var take = Math.Min(quantity, item.Quantity); quantity -= take;
            if (take == item.Quantity) item.Reserved = reservation;
            else
            {
                item.Quantity -= take;
                var split = AddItem(p.Id, definition, take, item.Quality, $"분할 {item.Id}", "transfer")!;
                split.Reserved = reservation; split.Damage = item.Damage;
            }
            if (quantity == 0) return;
        }
        throw new InvalidOperationException("예약 전 재료 검사 실패");
    }

    private string? Equip(Person p, long id)
    {
        if (!Accessible(p, id, out var item) || item.Quantity != 1 || item.Container != 0 || item.Damage >= 100 || item.Quality < 1000)
            return "장착할 수 없는 실물입니다.";
        var slot = Catalog.Items[item.Definition].EquipmentSlot;
        if (slot < 0) return "장비 품목이 아닙니다.";
        if (item.BoundSoul != 0 && item.BoundSoul != p.SoulId) return "다른 영혼에 결속된 법보입니다.";
        p.Equipment[slot] = item.Id; item.Place = -1;
        return null;
    }

    private string? Store(Person p, long id, long containerId)
    {
        if (!Accessible(p, id, out var item) || p.Equipment.ContainsValue(id)) return "수납할 수 없는 실물입니다.";
        if (containerId == 0) { item.Container = 0; return null; }
        if (!Accessible(p, containerId, out var container) || Catalog.Items[container.Definition].Capacity == 0 || container.Damage >= 100)
            return "사용 가능한 용기가 아닙니다.";
        for (var cursor = container; ;)
        {
            if (cursor.Id == item.Id) return "용기 순환 수납은 허용하지 않습니다.";
            if (cursor.Container == 0) break;
            cursor = items[cursor.Container];
        }
        var occupied = state.Items.Where(x => x.Container == containerId && x.Id != item.Id)
            .Sum(x => (long)x.Quantity * Catalog.Items[x.Definition].Weight);
        if (occupied + (long)item.Quantity * Catalog.Items[item.Definition].Weight > Catalog.Items[container.Definition].Capacity)
            return "용기 용량이 부족합니다.";
        item.Container = containerId; item.Place = container.Place;
        return null;
    }

    private string? Use(Person p, long id)
    {
        if (!Accessible(p, id, out var item) || item.Damage >= 100 || item.Quality < 1000) return "사용할 수 없는 실물입니다.";
        var kind = Catalog.Items[item.Definition].Kind;
        if (kind == ItemKind.Pill) p.Health = Math.Min(100, p.Health + item.Quality / 200);
        else if (kind == ItemKind.Food) p.Health = Math.Min(100, p.Health + 1);
        else if (kind == ItemKind.Talisman)
        {
            if (item.Definition == "soulWard")
            {
                if (p.SoulPactUntil > Month) return "이미 보호 혼약이 있습니다.";
                p.SoulPactUntil = Month + Law("soul.pactDuration"); p.SoulPactSource = $"실물 {item.Id}·{item.Source}";
            }
            else p.Traits.Add(new("호신부", $"item:{item.Id}", Month, Month + 1, true));
        }
        else if (kind == ItemKind.Array)
        {
            var place = state.Places[p.Place];
            if (place.Owner != 0 && place.Owner != p.Id) return "설치 권한이 없습니다.";
            if (!place.Facilities.Contains("수행 진법")) place.Facilities.Add("수행 진법");
        }
        else return "사용 효과가 없는 품목입니다.";
        Material(item.Definition).Consumption++;
        item.Quantity--; if (item.Quantity == 0) Remove(item);
        Event("소비", p.Id, id, $"{Catalog.Items[item.Definition].Name} 사용", item.Source);
        return null;
    }

    private string? Repair(Person p, long id)
    {
        if (p.Action != ActionKind.Idle || !Accessible(p, id, out var item) || item.Damage == 0 || Inventory(p.Id, "ore") < 1)
            return "수리 조건이 부족합니다.";
        Consume(p, "ore", 1); item.Damage = Math.Max(0, item.Damage - 25);
        p.Action = ActionKind.Heal; p.Due = Month + 1;
        return null;
    }

    private string? Withdraw(Person p, long contractId)
    {
        var c = state.Contracts.FirstOrDefault(x => x.Id == contractId);
        if (c is null || c.Seller != p.Id || c.Status != "open" || c.Buyer != 0) return "철회할 수 없는 계약입니다.";
        c.Status = "cancelled"; if (items.TryGetValue(c.Item, out var item)) item.Reserved = 0;
        return null;
    }

    private string? Offer(Person p, long itemId, long price, string kind)
    {
        if (price <= 0 || price > 1000000000 || !Accessible(p, itemId, out var item) ||
            item.Container != 0 || p.Equipment.ContainsValue(itemId) || state.Items.Any(i => i.Container == itemId))
            return "출품할 수 없는 실물·가격입니다.";
        var c = new Contract { Id = Id(), Kind = kind == "auction" ? "auction" : "sale", Seller = p.Id, Item = itemId,
            Price = price, Destination = p.Place, Due = Month + 12 };
        item.Place = p.Place; item.Reserved = c.Id; state.Contracts.Add(c);
        if (!state.Places[p.Place].Facilities.Contains("시장")) state.Places[p.Place].Facilities.Add("시장");
        Event("출품", p.Id, c.Id, $"{Catalog.Items[item.Definition].Name} 출품·{price}영석", item.Source);
        return null;
    }

    private string? Trade(Person p, long contractId, long unused)
    {
        var c = state.Contracts.FirstOrDefault(x => x.Id == contractId);
        if (c is null || c.Kind != "sale" || c.Status != "open" || c.Seller == p.Id ||
            c.Destination != p.Place || p.Money < c.Price || !items.TryGetValue(c.Item, out var item) ||
            item.Reserved != c.Id) return "거래 조건을 만족하지 않습니다.";
        p.Money -= c.Price; people[c.Seller].Money += c.Price;
        Transfer(item, p.Id); item.Place = -1; item.Reserved = 0;
        c.Buyer = p.Id; c.Status = "completed";
        Event("거래", p.Id, c.Seller, $"{Catalog.Items[item.Definition].Name}·{c.Price}영석 정산", $"계약 {c.Id}");
        return null;
    }

    private string? Bid(Person p, long contractId, long amount)
    {
        var c = state.Contracts.FirstOrDefault(x => x.Id == contractId);
        if (c is null || c.Kind != "auction" || c.Status != "open" || c.Due <= Month || c.Seller == p.Id ||
            c.Destination != p.Place || amount < c.Price || (c.Buyer != 0 && amount <= c.Escrow)) return "입찰 조건을 만족하지 않습니다.";
        var available = p.Money + (c.Buyer == p.Id ? c.Escrow : 0);
        if (available < amount) return "입찰 예치금이 부족합니다.";
        if (c.Buyer != 0) people[c.Buyer].Money += c.Escrow;
        p.Money -= amount; c.Escrow = amount; c.Buyer = p.Id;
        return null;
    }

    private string? Transport(Person p, long itemId, long destination)
    {
        if (p.Action != ActionKind.Idle || destination < 0 || destination >= 100 ||
            !Accessible(p, itemId, out var item) || !p.KnownPlaces.Contains((int)destination) ||
            !state.Places[p.Place].Neighbors.Contains((int)destination)) return "운송 조건이 부족합니다.";
        var c = new Contract { Id = Id(), Kind = "transport", Seller = p.Id, Carrier = p.Id,
            Item = itemId, Destination = (int)destination, Due = Month + Law("economy.travelDuration") };
        item.Reserved = c.Id; state.Contracts.Add(c);
        p.Action = ActionKind.Guard; p.Due = c.Due; p.ActionTarget = c.Id;
        return null;
    }

    private void SettleContracts()
    {
        foreach (var c in state.Contracts.Where(x => x.Status == "open").OrderBy(x => x.Id))
        {
            if (!items.TryGetValue(c.Item, out var item)) throw new InvalidDataException("계약 실물이 없습니다.");
            if (people[c.Seller].Status != LifeStatus.Alive || (c.Buyer != 0 && people[c.Buyer].Status != LifeStatus.Alive))
            {
                if (c.Buyer != 0) people[c.Buyer].Money += c.Escrow;
                c.Escrow = 0; item.Reserved = 0; c.Status = "cancelled";
                continue;
            }
            if (c.Due > Month) continue;
            if (c.Kind == "transport")
            {
                people[c.Carrier].Place = c.Destination; item.Place = c.Destination;
                item.Reserved = 0; c.Status = "completed";
            }
            else if (c.Kind == "auction" && c.Buyer != 0)
            {
                people[c.Seller].Money += c.Escrow; c.Escrow = 0; Transfer(item, c.Buyer);
                item.Reserved = 0; c.Status = "completed";
            }
            else { item.Reserved = 0; c.Status = "expired"; }
        }
    }
}
