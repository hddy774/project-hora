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

    private string? Attack(Person p, long id)
        => ExecuteCombatAttack(p, id);
}
