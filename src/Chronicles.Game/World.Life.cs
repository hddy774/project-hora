namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private string? Partner(Person p, long target)
    {
        if (!people.TryGetValue(target, out var q) || !p.KnownPeople.Contains(target) || p.Id == q.Id ||
            q.Status != LifeStatus.Alive || p.Place != q.Place || p.Partner != 0 || q.Partner != 0 ||
            Month - p.Born < Law("birth.maturity") || Month - q.Born < Law("birth.maturity"))
            return "도려 관계를 맺을 조건이 아닙니다.";
        if (Law("birth.kinship") == 0 && Related(p, q)) return "현재 혈연 법칙에서 허용하지 않습니다.";
        // The other character makes an independent, reproducible choice from their disposition.
        if (q.Personality[4] + q.Personality[5] < 60) return "상대가 제안을 거절했습니다.";
        p.Partner = q.Id; q.Partner = p.Id;
        Event("도려", p.Id, q.Id, $"{p.Name}·{q.Name} 상호 도려 성립", "상호 의사");
        return null;
    }

    private bool Related(Person p, Person q)
    {
        var ancestors = new HashSet<long>();
        void Add(long id)
        {
            if (id == 0 || !ancestors.Add(id) || !people.TryGetValue(id, out var a)) return;
            Add(a.ParentOne); Add(a.ParentTwo);
        }
        Add(p.Id);
        var pending = new Stack<long>(); pending.Push(q.Id);
        var seen = new HashSet<long>();
        while (pending.TryPop(out var id))
        {
            if (id == 0 || !seen.Add(id)) continue;
            if (ancestors.Contains(id)) return true;
            if (people.TryGetValue(id, out var a)) { pending.Push(a.ParentOne); pending.Push(a.ParentTwo); }
        }
        return false;
    }

    private bool BirthQualified(Person p)
    {
        var age = Month - p.Born;
        return p.Status == LifeStatus.Alive && age >= Law("birth.maturity") &&
            age * 100 <= p.Lifespan * Law("birth.fertility") && p.Health >= 30 &&
            ((int)p.Realm >= Law("birth.minimumRealm") ||
                (Law("birth.martialEquivalent") != 0 && p.Martial == 10));
    }

    private string? Conceive(Person p, long target)
    {
        if (Law("birth.enabled") == 0 || !people.TryGetValue(target, out var q) ||
            p.Partner != q.Id || q.Partner != p.Id || p.Place != q.Place || p.Gestates == q.Gestates)
            return "출산 조건을 만족하지 않습니다.";
        var mother = p.Gestates ? p : q; var father = p.Gestates ? q : p;
        if (!BirthQualified(mother) || (Law("birth.bothParents") != 0 && !BirthQualified(father)) ||
            father.Status != LifeStatus.Alive || Month < mother.NextBirth ||
            state.Pregnancies.Any(x => x.Mother == mother.Id && !x.Completed)) return "출산 자격·회복 기간을 확인하세요.";
        if (Law("birth.kinship") == 0 && Related(mother, father)) return "현재 혈연 법칙에서 허용하지 않습니다.";
        int[] Mix(int[] a, int[] b) => a.Select((v, i) => Math.Clamp((v + b[i]) / 2 + Roll(401) - 200, 0, 10000)).ToArray();
        var pregnancy = new Pregnancy { Id = Id(), Mother = mother.Id, Father = father.Id,
            Due = Month + Law("birth.gestation"), LawRevision = state.LawRevision,
            Recovery = Law("birth.interval"), HealthCost = IntLaw("birth.healthCost"),
            Roots = Mix(mother.Roots, father.Roots), Aptitudes = Mix(mother.Aptitudes, father.Aptitudes) };
        state.Pregnancies.Add(pregnancy);
        Event("잉태", mother.Id, father.Id, $"{mother.Name} 잉태·유전 원형 확정", $"법칙 r{state.LawRevision}");
        return null;
    }

    private void Birth(Pregnancy pregnancy)
    {
        pregnancy.Completed = true;
        var mother = people[pregnancy.Mother]; var father = people[pregnancy.Father];
        if (mother.Status != LifeStatus.Alive)
        {
            Event("임신 종결", mother.Id, father.Id, "출산자 부재로 임신 종결", $"임신 {pregnancy.Id}"); return;
        }
        // Eligibility and genetics were captured at conception; subsequent law edits do not reroll a fetus.
        var soul = Law("soul.waitingFirst") != 0 ? state.Souls.Where(s => s.Status == "waiting" && s.Ready <= Month)
            .OrderBy(s => s.Ready).ThenBy(s => s.Id).FirstOrDefault() : null;
        if (soul is null)
        {
            soul = new Soul { Id = Id(), Evidence = $"출생 {pregnancy.Id} 신규 영혼" };
            soul.Root = soul.Id; state.Souls.Add(soul); souls.Add(soul.Id, soul);
        }
        var p = new Person { Id = Id(), BodyId = Id(), SoulId = soul.Id,
            Name = $"{mother.Name[0]}씨 {state.BirthCount + 1}세손", Gestates = Roll(2) == 1,
            Born = Month, Lifespan = Law("life.mortal"), ParentOne = mother.Id, ParentTwo = father.Id,
            Guardian = mother.Id, Household = mother.Household, Place = mother.Place,
            Roots = pregnancy.Roots.ToArray(), Aptitudes = pregnancy.Aptitudes.ToArray(),
            Personality = Enumerable.Range(0, 14).Select(_ => 20 + Roll(61)).ToArray(),
            KnownPlaces = [mother.Place], KnownPeople = [mother.Id, father.Id] };
        soul.Status = "embodied"; soul.CurrentPerson = p.Id;
        soul.Traces = soul.Traces.Select(t => t with { Accessible = 0, Integrated = 0 }).ToArray();
        if (soul.Route > 0) p.Traits.Add(new("윤회유흔", $"Soul:{soul.Id}", Month, null, true));
        state.People.Add(p); people.Add(p.Id, p); pregnancy.Child = p.Id;
        if (groups.TryGetValue(p.Household, out var home)) home.Members.Add(p.Id);
        mother.NextBirth = Month + pregnancy.Recovery;
        mother.Health = Math.Max(1, mother.Health - pregnancy.HealthCost);
        state.BirthCount++;
        mother.KnownPeople.Add(p.Id); father.KnownPeople.Add(p.Id);
        Event("출생", p.Id, mother.Id, $"{p.Name} 탄생·실제 부모/새 육신·단일 영혼 배정", $"임신 {pregnancy.Id}·법칙 r{pregnancy.LawRevision}");
    }

    private void EndLife(Person p, int cause, string evidence, LifeStatus outcome = LifeStatus.Dead)
    {
        if (p.Status != LifeStatus.Alive) return;
        var sourceRealm = p.Realm; var sourceLayer = p.Layer;
        p.Irreversible = false;
        if (p.Action != ActionKind.Idle) Cancel(p);
        p.Status = outcome; p.Ended = Month;
        if (outcome == LifeStatus.Dead) state.DeathCount++;
        if (p.Partner != 0 && people.TryGetValue(p.Partner, out var partner)) partner.Partner = 0;
        p.Partner = 0;
        if (state.Controlled == p.Id) { state.Controlled = 0; state.ViewGeneration++; }
        var soul = souls[p.SoulId]; soul.CurrentPerson = 0;
        var min = (Realm)Law("soul.minimumRealm");
        var routeMin = cause switch { 4 or 5 => Realm.Void, 6 or 7 => Realm.Integration,
            8 or 9 => Realm.Tribulation, 10 or 11 => Realm.Mahayana, 12 => Realm.HalfImmortal, _ => min };
        var needsComplete = cause is 7 or 9 or 11 || sourceRealm == min;
        var enabled = cause is >= 1 and <= 12 && (Law("soul.enabledRoutes") & (1L << (cause - 1))) != 0;
        var eligible = sourceRealm >= min && sourceRealm >= routeMin && (!needsComplete || p.GreatPerfection);
        soul.SourceRealm = sourceRealm; soul.SourceLayer = sourceLayer;
        soul.Evidence = $"현생 {p.Id} 종료 직전 {Catalog.RealmNames[(int)sourceRealm]} {sourceLayer}층·{evidence}·법칙 r{state.LawRevision}";
        soul.Route = enabled && eligible ? cause : 0;
        soul.Status = soul.Route > 0 ? "waiting" : "ended";
        soul.Ready = Month + Law("soul.crossing");
        if (soul.Route > 0)
            soul.Traces = Enumerable.Range(0, 6).Select(i => new Trace(Math.Min(100, p.Understanding.Count * 2 + i), 0, 0)).ToArray();
        var heir = state.People.Where(x => x.Status == LifeStatus.Alive && (x.ParentOne == p.Id || x.ParentTwo == p.Id))
            .OrderBy(x => x.Born).ThenBy(x => x.Id).FirstOrDefault();
        heir ??= state.People.FirstOrDefault(x => x.Status == LifeStatus.Alive && x.Household == p.Household);
        if (heir is not null)
        {
            heir.Money += p.Money; p.Money = 0;
            foreach (var item in Owned(p.Id).Where(i => i.Reserved == 0).ToArray())
            { Transfer(item, heir.Id); item.Container = 0; item.Place = p.Place; }
            p.Equipment.Clear();
        }
        foreach (var g in state.Groups.Where(g => !g.Dissolved && g.Leader == p.Id))
        {
            g.Leader = g.Members.Where(id => people.TryGetValue(id, out var x) && x.Status == LifeStatus.Alive &&
                Month - x.Born >= Law("birth.maturity")).Order().FirstOrDefault();
            if (g.Leader == 0) Event("공석", 0, g.Id, $"{g.Name} 수장 공석", evidence);
        }
        Event(outcome == LifeStatus.Dead ? "사망" : "세계 이탈", p.Id, soul.Route, $"{p.Name} {evidence}·{(soul.Route == 0 ? "윤회 미적격" : $"RC-{soul.Route:00} 대기")}", soul.Evidence);
    }

    private string? Ascend(Person p)
    {
        if (p.Realm != Realm.HalfImmortal || !p.GreatPerfection || p.Health < 90 || p.Action != ActionKind.Idle ||
            Month < Law("founder.ascensionProtection") || p.Place != 99 || p.Understanding.GetValueOrDefault("path.10.2") == 0)
            return "비승 통로·기반·시기 조건이 부족합니다.";
        p.Status = LifeStatus.Ascended; p.Ended = Month;
        if (p.Partner != 0 && people.TryGetValue(p.Partner, out var partner)) partner.Partner = 0;
        p.Partner = 0;
        foreach (var group in state.Groups.Where(g => !g.Dissolved && g.Leader == p.Id))
            group.Leader = group.Members.Where(id => people[id].Status == LifeStatus.Alive).Order().FirstOrDefault();
        var root = souls[p.SoulId]; root.Status = "ascended"; root.CurrentPerson = 0;
        if (state.Controlled == p.Id) { state.Controlled = 0; state.ViewGeneration++; }
        if ((Law("soul.enabledRoutes") & (1L << 12)) != 0 && root.SplitCount < Law("soul.splitLimit"))
        {
            root.SplitCount++;
            var split = new Soul { Id = Id(), Parent = root.Id, Root = root.Root, Route = 13, Status = "waiting",
                Ready = Month + Law("soul.crossing"), SourceRealm = p.Realm, SourceLayer = p.Layer,
                Evidence = $"실제 비승 {p.Id}·독립 분혼 {root.SplitCount}·법칙 r{state.LawRevision}",
                Traces = root.Traces.Select(t => new Trace(t.Preserved * (100 - IntLaw("soul.splitCost")) / 100, 0, 0)).ToArray() };
            state.Souls.Add(split); souls.Add(split.Id, split);
        }
        Event("비승", p.Id, 0, $"{p.Name} 비승·사망 집계 없음·재산/직책 자동 복제 없음", "실제 통로 99");
        return null;
    }
}
