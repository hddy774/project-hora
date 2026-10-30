namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private string? Found(Person p, string kind)
    {
        if (p.Action != ActionKind.Idle || Month - p.Born < Law("birth.maturity") || p.Money < Law("politics.foundCost"))
            return "창설할 인력·자격·재원이 부족합니다.";
        if (kind != "sect" && kind != "family") return "가문 또는 종문을 선택하세요.";
        if (kind == "sect" && (p.Sect != 0 || state.Groups.Count(g => g.Kind == GroupKind.Sect && !g.Dissolved) >= Law("politics.sectLimit")))
            return "현재 종문 창설 조건이 아닙니다.";
        var g = new Group { Id = Id(), Kind = kind == "sect" ? GroupKind.Sect : GroupKind.Family,
            Name = $"{p.Name}{(kind == "sect" ? "종" : "가")}", Leader = p.Id, Founded = Month,
            Members = [p.Id], Money = Law("politics.foundCost"),
            Policies = new(Catalog.PolicyNames.ToDictionary(n => n, _ => 1, StringComparer.Ordinal), StringComparer.Ordinal) };
        p.Money -= g.Money; state.Groups.Add(g); groups.Add(g.Id, g);
        if (g.Kind == GroupKind.Sect)
        {
            p.Sect = g.Id; p.Traits.Add(new("창종지조", $"group:{g.Id}", Month, null, true));
            g.Jurisdiction = state.Places[p.Place].Sovereign;
        }
        else
        {
            if (groups.TryGetValue(p.Household, out var old)) old.Members.Remove(p.Id);
            p.Household = g.Id;
        }
        p.Action = ActionKind.Guard; p.Due = Month + 1;
        Event("창설", p.Id, g.Id, $"{g.Name} 창설·실제 창설금 이전", "명시 창설");
        return null;
    }

    private string? Join(Person p, long id)
    {
        if (!groups.TryGetValue(id, out var g) || g.Dissolved || g.Leader == 0 || g.Kind != GroupKind.Sect || p.Sect != 0 ||
            people[g.Leader].Place != p.Place || !p.KnownPeople.Contains(g.Leader)) return "가입 조건을 만족하지 않습니다.";
        if (g.Policies.GetValueOrDefault("입문·선발") == 2 && p.Realm == Realm.Mortal) return "종문의 선발 기준에 미달합니다.";
        if (p.Personality[4] + people[g.Leader].Personality[4] < 50) return "입문 제안이 수락되지 않았습니다.";
        p.Sect = g.Id; g.Members.Add(p.Id);
        Event("입문", p.Id, g.Id, $"{p.Name} {g.Name} 입문", "상호 선택");
        return null;
    }

    private string? Policy(Person p, long id, string policy, long value)
    {
        if (!groups.TryGetValue(id, out var g) || g.Dissolved || g.Leader != p.Id ||
            !Catalog.PolicyNames.Contains(policy) || value is < 0 or > 2 ||
            g.PendingPolicies.Count != 0 || g.Money < Law("politics.policyCost") || p.Action != ActionKind.Idle)
            return "정책 권한·준비금·노동 용량을 확인하세요.";
        g.PolicyEscrow = Law("politics.policyCost"); g.Money -= g.PolicyEscrow;
        g.PendingPolicies[policy] = (int)value; g.PolicyDue = Month + 1;
        p.Action = ActionKind.Guard; p.Due = Month + 1;
        Event("정책 준비", p.Id, g.Id, $"{g.Name} {policy} 준비·예산 예약", $"법칙 r{state.LawRevision}");
        return null;
    }

    private string? Govern(Person p, long id, long province)
    {
        if (!groups.TryGetValue(id, out var g) || g.Dissolved || g.Leader != p.Id ||
            g.Kind == GroupKind.Sect || province is < 0 or >= 10 || p.Action != ActionKind.Idle)
            return "통치 권한을 확인하세요.";
        var places = state.Places.Where(x => x.Province == province).ToArray();
        if (places.Any(x => !p.KnownPlaces.Contains(x.Id)) ||
            !g.Members.Any(x => people[x].Status == LifeStatus.Alive && people[x].Place / 10 == province))
            return "실제 관측과 피통치 가계·행정 거점이 필요합니다.";
        if (places.Any(x => x.Sovereign != 0 && x.Sovereign != id)) return "다른 주권이 있는 지역입니다.";
        if (g.Provinces.Contains((int)province)) return "이미 통치 중인 주입니다.";
        foreach (var place in places) place.Sovereign = g.Id;
        g.Provinces.Add((int)province); g.Provinces.Sort(); g.Kind = GroupKind.Dynasty;
        p.Action = ActionKind.Guard; p.Due = Month + 1;
        p.Traits.Add(new("개국지군", $"group:{g.Id}", Month, null, true));
        Event("주권", p.Id, g.Id, $"{g.Name} {province + 1}주 독립 통치·왕조 성립", "실제 1주 행정·가계");
        return null;
    }

    private string? Vassal(Person p, long id, long overlord)
    {
        if (!groups.TryGetValue(id, out var g) || g.Dissolved || g.Leader != p.Id || g.Kind == GroupKind.Sect ||
            (overlord != 0 && (!groups.TryGetValue(overlord, out var top) || top.Dissolved || top.Kind != GroupKind.Dynasty)))
            return "봉신 관계 조건을 확인하세요.";
        var cursor = overlord;
        while (cursor != 0)
        {
            if (cursor == g.Id) return "봉신 관계 순환은 허용하지 않습니다.";
            cursor = groups[cursor].Overlord;
        }
        g.Overlord = overlord;
        Event("봉신", p.Id, g.Id, overlord == 0 ? $"{g.Name} 독립" : $"{g.Name} 봉신 관계 성립", "실제 통치 관계");
        return null;
    }

    private string? Dissolve(Person p, long id)
    {
        if (!groups.TryGetValue(id, out var g) || g.Dissolved || g.Leader != p.Id || g.Debt != 0)
            return "해산 권한·채무를 확인하세요.";
        g.Money += g.PolicyEscrow; g.PolicyEscrow = 0; g.PendingPolicies.Clear();
        p.Money += g.Money; g.Money = 0; g.Dissolved = true;
        foreach (var member in g.Members.Select(id => people[id]))
        {
            if (member.Sect == g.Id) member.Sect = 0;
            if (member.Household == g.Id) member.Household = 0;
        }
        foreach (var place in state.Places)
        {
            if (place.Sovereign == g.Id) place.Sovereign = 0;
            if (place.Delegate == g.Id) place.Delegate = 0;
        }
        foreach (var vassal in state.Groups.Where(x => x.Overlord == g.Id)) vassal.Overlord = 0;
        g.Provinces.Clear();
        Event("해산", p.Id, g.Id, $"{g.Name} 해산·잔여 재산 인계·역사 보존", "실제 조직 종결");
        return null;
    }

    private void SettleGroups()
    {
        foreach (var g in state.Groups.Where(x => !x.Dissolved))
        {
            if (g.PendingPolicies.Count > 0 && g.PolicyDue <= Month)
            {
                if (g.Leader != 0 && people[g.Leader].Status == LifeStatus.Alive)
                {
                    foreach (var (key, value) in g.PendingPolicies) g.Policies[key] = value;
                    state.ConsumedMoney += g.PolicyEscrow;
                    Event("정책 시행", g.Leader, g.Id, $"{g.Name} 정책 시행·현 수장/관할 재검사", "예약 준비금 소비");
                }
                else g.Money += g.PolicyEscrow;
                g.PolicyEscrow = 0; g.PendingPolicies.Clear();
            }
            if (g.Kind == GroupKind.Sect)
                foreach (var p in g.Members.Select(id => people[id]).Where(p => p.Status == LifeStatus.Alive))
                {
                    var pay = Math.Min(g.Money, Law("politics.salary"));
                    g.Money -= pay; p.Money += pay;
                }
            if (Month % 12 == 0 && g.Kind == GroupKind.Dynasty && g.Provinces.Count > 0)
            {
                foreach (var p in state.People.Where(p => p.Status == LifeStatus.Alive && state.Places[p.Place].Sovereign == g.Id))
                {
                    var tax = p.Money / 100; p.Money -= tax; g.Money += tax;
                }
                if (g.Overlord != 0)
                {
                    var tribute = g.Money / 10; g.Money -= tribute; groups[g.Overlord].Money += tribute;
                }
            }
        }
    }

    public static long[] CombatAttributes(Person p)
    {
        var basis = 100L;
        for (var i = 0; i < (int)p.Realm; i++) basis = Math.Min(1000000000000, basis * 4);
        basis += p.Martial * p.Martial * 200;
        var path = p.Formations.LastOrDefault();
        var definition = path is null ? null : Catalog.Paths.FirstOrDefault(x => x.Id == path.Path);
        return Enumerable.Range(0, 8).Select(i => basis * (definition?.CombatFactors[i] ?? 100) / 100 * p.Health / 100).ToArray();
    }

    private string? Attack(Person p, long id)
    {
        if (p.Action != ActionKind.Idle || !people.TryGetValue(id, out var q) || q.Status != LifeStatus.Alive ||
            q.Id == p.Id || q.Place != p.Place || !p.KnownPeople.Contains(q.Id)) return "전투 조건을 만족하지 않습니다.";
        var attack = CombatAttributes(p); var defence = CombatAttributes(q);
        var damage = (int)Math.Clamp(attack[0] * 25 / Math.Max(1, defence[1]), 1, 100);
        var ward = q.Traits.FirstOrDefault(t => t.Id == "호신부" && t.Active);
        if (ward is not null) { damage /= 2; q.Traits.Remove(ward); }
        if (q.Equipment.ContainsKey(3)) damage = damage * 3 / 4;
        if (p.Equipment.ContainsKey(0)) damage = Math.Min(100, damage + damage / 4);
        q.Health = Math.Max(0, q.Health - damage);
        Gain(p, "combat", 10); Gain(q, "combat", 5);
        p.Action = ActionKind.Guard; p.Due = Month + 1;
        if (q.Health == 0) EndLife(q, 2, "전투 사망");
        else if (q.Personality[6] < 50)
        {
            var escape = state.Places[q.Place].Neighbors.FirstOrDefault(n => q.KnownPlaces.Contains(n) && state.Places[n].BlockedUntil <= Month, -1);
            if (escape >= 0 && q.Action == ActionKind.Idle) Start(q, "travel", escape, "");
        }
        Event("전투", p.Id, q.Id, $"{p.Name}→{q.Name} 피해 {damage}·{q.Health} 건강", "실제 전투력/방어");
        return null;
    }
}
