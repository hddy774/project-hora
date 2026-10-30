namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private string? Adopt(Person p, long id)
    {
        if (p.Action != ActionKind.Idle || Month - p.Born < PeopleMaturityMonths(p) ||
            !people.TryGetValue(id, out var child) || !p.KnownPeople.Contains(id) || child.Place != p.Place || child.Status != LifeStatus.Alive ||
            Month - child.Born >= PeopleMaturityMonths(child) || child.Id == p.Id) return "입양·양육 조건을 확인하세요.";
        if (child.Guardian != 0 && people[child.Guardian].Status == LifeStatus.Alive && child.Guardian != p.Id &&
            people[child.Guardian].Personality[4] < 50) return "현재 양육자가 동의하지 않았습니다.";
        child.Guardian = p.Id;
        if (groups.TryGetValue(child.Household, out var old)) old.Members.Remove(child.Id);
        child.Household = p.Household;
        if (groups.TryGetValue(p.Household, out var home)) home.Members.Add(child.Id);
        p.Action = ActionKind.Guard; p.Due = Month + 1;
        Event("입양", p.Id, child.Id, $"{p.Name} {child.Name} 양육 인계·생물학 부모 보존", "실제 동의·양육 책임 이전");
        return null;
    }

    public static long[] CombatAttributes(Person p, IReadOnlyDictionary<long, Item>? equipment = null)
    {
        var basis = 100L;
        for (var i = 0; i < (int)p.Realm; i++) basis = Math.Min(1000000000000, basis * 4);
        basis += p.Martial * p.Martial * 64;
        var path = p.Formations.LastOrDefault();
        var definition = path is null ? null : Catalog.Paths.FirstOrDefault(x => x.Id == path.Path);
        var attributes = Enumerable.Range(0, 8).Select(i => basis * (definition?.CombatFactors[i] ?? 100) / 100 * p.Health / 100).ToArray();
        if (equipment is not null)
            foreach (var (slot, itemId) in p.Equipment)
                if (equipment.TryGetValue(itemId, out var item) && item.Owner == p.Id && item.Damage < 100 &&
                    (item.Defects & (ItemDefect.BrokenCircuit | ItemDefect.LostCore)) == 0)
                {
                    var axis = slot switch { 0 => 0, 3 => 1, 5 => 2, 6 => 3, 7 => 7, _ => -1 };
                    if (axis >= 0) attributes[axis] += attributes[axis] * item.Quality * (100 - item.Damage) / 4000000;
                }
        return attributes;
    }

    private string? Attack(Person p, long id)
        => ExecuteCombatAttack(p, id);
}
