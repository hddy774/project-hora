using System.Globalization;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private void Regenerate()
    {
        foreach (var place in state.Places)
        {
            var regeneration = IntLaw("ecology.regrowth");
            var cap = IntLaw("ecology.sourceCapacity");
            place.Source = (int)Math.Min(cap, (long)place.Source + regeneration);
            place.Qi = (int)Math.Min(cap, (long)place.Qi + regeneration);
            if (place.Facilities.Contains("수행 진법") && place.Qi > 0) place.Qi--;
            if (Month % 12 == 0 && Roll(10000) < Law("ecology.disasterChance"))
            {
                place.Source /= 2; place.Qi /= 2; place.BlockedUntil = Month + 3;
                foreach (var p in state.People.Where(p => p.Status == LifeStatus.Alive && p.Place == place.Id))
                {
                    p.Health = Math.Max(0, p.Health - 10);
                    if (p.Health == 0) EndLife(p, 3, "재해로 육신 붕괴");
                }
                Event("재해", 0, place.Id, $"{place.Name} 자원 감소·통로 임시 봉쇄", "실제 지역 재해");
            }
        }
        if (Month % 12 == 0)
            foreach (var item in state.Items.Where(i => i.Definition == "food" && i.Reserved == 0).ToArray())
            {
                item.Quantity -= item.Quantity * IntLaw("ecology.spoilage") / 100;
                if (item.Quantity == 0) Remove(item);
            }
    }

    private void Maintain(Person p)
    {
        var foodNeed = IntLaw("ecology.foodUse");
        var provider = p;
        if (Inventory(p.Id, "food") < foodNeed && p.Guardian != 0 && people.TryGetValue(p.Guardian, out var guardian) &&
            guardian.Status == LifeStatus.Alive && guardian.Place == p.Place) provider = guardian;
        if (Inventory(provider.Id, "food") >= foodNeed)
        {
            Consume(provider, "food", foodNeed);
            p.Health = Math.Min(100, p.Health + IntLaw("cultivation.recovery"));
        }
        else
        {
            p.Health = Math.Max(0, p.Health - 2);
            if (p.Health == 0) { EndLife(p, 3, "실제 식량 부족·육신 붕괴"); return; }
        }
        foreach (var key in p.Beliefs.Where(x => x.Value.Expires <= Month).Select(x => x.Key).ToArray()) p.Beliefs.Remove(key);
        p.Traits.RemoveAll(t => t.Expires <= Month);
        var soul = souls[p.SoulId];
        if (soul.Route > 0 && Month - p.Born > Law("birth.maturity"))
            soul.Traces = soul.Traces.Select(t => t with { Accessible = Math.Min(t.Preserved, t.Accessible + 1) }).ToArray();
    }

    private void Complete(Person p)
    {
        var action = p.Action; var target = p.ActionTarget; var data = p.ActionData;
        p.Action = ActionKind.Idle; p.Due = 0;
        var place = state.Places[p.Place];
        switch (action)
        {
            case ActionKind.Gather:
            case ActionKind.Farm:
                var definition = data is "herb" or "ore" or "wood" ? data : "food";
                var requested = IntLaw("economy.gatherYield");
                if (p.Equipment.ContainsKey(8)) requested += requested / 4;
                var yield = Math.Min(place.Source, requested);
                place.Source -= yield;
                AddItem(p.Id, definition, yield, 3000 + place.Element * 250, $"지점 {place.Id}·월 {Month}·실제 채집");
                Gain(p, action == ActionKind.Farm ? "farm" : "gather", yield);
                if (yield == 0) Remember(p, $"depleted:{place.Id}", "원천 고갈", "실제 채집 결과", 100);
                if (action == ActionKind.Farm && !place.Facilities.Contains("경작지")) place.Facilities.Add("경작지");
                break;
            case ActionKind.Cultivate: Cultivate(p, data); break;
            case ActionKind.Breakthrough: Breakthrough(p, data); break;
            case ActionKind.Martial:
                p.MartialWork += 1 + Catalog.Talent(p, 9) / 5000;
                if (p.Martial < 10 && p.MartialWork >= Law("martial.period") * (p.Martial + 1))
                {
                    p.Martial++; p.MartialWork = 0;
                    p.Lifespan = Math.Max(p.Lifespan, 960 + p.Martial * 624);
                    Event("무예", p.Id, p.Martial, $"{p.Name} 무예 {p.Martial}경지·영근 비사용", "실제 단련");
                }
                break;
            case ActionKind.Study:
                if (Accessible(p, target, out var book, ItemKind.Book))
                {
                    var gain = Math.Max(1, Catalog.Talent(p, 21) / 100);
                    if (book.Damage >= 100 || book.Quality < 1000) gain = 0;
                    p.Understanding[book.Definition] = Math.Min(10000, p.Understanding.GetValueOrDefault(book.Definition) + gain);
                    Remember(p, $"book:{book.Id}", $"이해 {p.Understanding[book.Definition]}", $"매체 {book.Id}·{book.Source}", book.Quality / 100);
                }
                break;
            case ActionKind.Travel:
                if (state.Places[(int)target].BlockedUntil <= Month) { p.Place = (int)target; RefreshPopulationIndices(); Observe(p); }
                else Remember(p, $"blocked:{target}", "통로 봉쇄", "실제 이동 실패", 100);
                break;
            case ActionKind.Explore:
                foreach (var id in place.Neighbors) if (!p.KnownPlaces.Contains(id)) p.KnownPlaces.Add(id);
                p.KnownPlaces.Sort();
                if (place.DiscoveryRemaining > 0)
                {
                    place.DiscoveryRemaining--;
                    AddItem(p.Id, "herb", 3, 6000, $"지점 {place.Id} 유한 탐색 보상");
                    Event("발견", p.Id, place.Id, $"{p.Name} 숨은 약초 발견·지점 보상 소진", "실제 탐색");
                }
                break;
            case ActionKind.Craft:
                var recipe = Catalog.Recipes.First(r => r.Id == data);
                var materials = state.Items.Where(i => i.Reserved == target).ToArray();
                var quality = materials.Length == 0 ? 0 : materials.Sum(i => i.Quality * i.Quantity) / materials.Sum(i => i.Quantity);
                foreach (var material in materials) Remove(material);
                var proficiency = p.Skills.GetValueOrDefault(recipe.Skill);
                var resultQuality = Math.Clamp((quality + proficiency + Catalog.Talent(p, recipe.Skill == "alchemy" ? 14 : 15)) / 3 + Roll(401) - 200, 0, 10000);
                var product = AddItem(p.Id, recipe.Output, recipe.Quantity, resultQuality, $"제작 {p.Id}·예약 {target}·{recipe.Id}");
                Gain(p, recipe.Skill, 25);
                Event("제작", p.Id, product?.Id ?? 0, $"{recipe.Name}·{Catalog.Quality(resultQuality)}", $"예약 {target} 소비 완료");
                break;
            case ActionKind.Heal: p.Health = Math.Min(100, p.Health + IntLaw("cultivation.recovery") * 2); break;
            case ActionKind.Teach:
                if (people.TryGetValue(target, out var student) && student.Status == LifeStatus.Alive && student.Place == p.Place)
                {
                    var ceiling = p.Understanding.GetValueOrDefault(data);
                    student.Understanding[data] = Math.Min(ceiling, student.Understanding.GetValueOrDefault(data) +
                        Math.Max(1, Catalog.Talent(student, 11) / 50));
                    Remember(student, $"teaching:{p.Id}:{data}", $"전수 이해 {student.Understanding[data]}", $"스승 {p.Id} 실제 교육", 80);
                    student.Traits.RemoveAll(t => t.Id == "사은미보" && t.Source == $"teacher:{p.Id}");
                    student.Traits.Add(new("사은미보", $"teacher:{p.Id}", Month, null, true));
                }
                break;
        }
        if (p.Action == ActionKind.Idle) { p.ActionTarget = 0; p.ActionData = ""; }
    }

    private static void Gain(Person p, string skill, int amount) =>
        p.Skills[skill] = Math.Min(10000, p.Skills.GetValueOrDefault(skill) + amount);

    private long Period(Person p)
    {
        if (p.Realm == Realm.Mortal) return Law("cultivation.entry");
        if (p.Realm == Realm.Qi) return Law("cultivation.qi");
        var duration = Law("cultivation.foundation");
        for (var i = 2; i < (int)p.Realm; i++) duration = Math.Min(100000000, duration * Law("cultivation.upper") / 100);
        return duration;
    }

    private void Cultivate(Person p, string chosenPath)
    {
        if (p.Realm == Realm.Mortal && p.Understanding.GetValueOrDefault("entry") == 0)
        { p.Reason = "尚未 습득한 입문 지식이 필요"; return; }
        var place = state.Places[p.Place];
        var cost = IntLaw("cultivation.sourceCost");
        if (place.Qi < cost) { p.Reason = "영맥 공급 고갈"; return; }
        place.Qi -= cost;
        var aptitude = p.Aptitudes.Sum() / 6;
        var harmony = 10000 - (p.Roots.Max() - p.Roots.Min()) / 2;
        p.Processes[0] = cost;
        p.Processes[1] = cost * (100 - place.Turbidity / 2) / 100;
        p.Processes[2] = Math.Min(p.Processes[1], Math.Max(1, Catalog.Talent(p, 7) / 2000));
        p.Processes[3] = p.Processes[2] * p.Health / 100;
        if (cost > 0 && p.Processes[3] == 0) { p.Reason = "순환·체화 기반 부족"; return; }
        var path = Catalog.Paths.FirstOrDefault(x => x.Id == chosenPath) ??
            Catalog.Paths.First(x => x.Realm == (p.Realm == Realm.Mortal ? Realm.Qi : p.Realm));
        var flow = cost == 0 ? 10000 : Math.Min(10000, p.Processes[3] * 10000 / cost);
        var work = (long)aptitude * harmony / 10000 * flow / 10000;
        if (p.Realm == Realm.HalfImmortal) { p.Work += work; return; }
        p.Work += work;
        for (var i = 0; i < p.Components.Length; i++)
            p.Components[i] = Math.Min(10000, p.Components[i] + (int)(work / (i + 1) / Math.Max(1, Period(p) / 2)));
        p.Layer = Math.Min(9, 1 + (int)(p.Work * 8 / (Period(p) * 5000)));
        Gain(p, path.Id, 1);
        if (p.Work >= Period(p) * 5000)
        {
            p.Action = ActionKind.Breakthrough; p.Due = Month + 1; p.ActionData = path.Id;
            p.Irreversible = p.Realm == Realm.GoldenCore;
        }
    }

    private void Breakthrough(Person p, string chosenPath)
    {
        var path = Catalog.Paths.FirstOrDefault(x => x.Id == chosenPath) ?? Catalog.Paths.First(x => x.Realm == Realm.Qi);
        var failRisk = IntLaw("cultivation.breakthroughRisk") + path.Risk + (100 - p.Health) * 10;
        var failed = Roll(10000) < failRisk;
        p.Irreversible = false;
        if (failed)
        {
            if (p.Realm == Realm.GoldenCore) { EndLife(p, 3, "金丹 파단 후 원영 형성 실패"); return; }
            if (p.Realm == Realm.Tribulation) { EndLife(p, 8, "겁난 실패"); return; }
            p.Health = Math.Max(1, p.Health - 20); p.Work = p.Work * 9 / 10;
            Event("돌파 실패", p.Id, 0, $"{p.Name} 기반 손상·회복 필요", "돌파 실제 판정");
            return;
        }
        if (p.Realm < Realm.HalfImmortal) p.Realm++;
        p.Layer = 1; p.Work = 0;
        var quality = p.Aptitudes.Sum() / 6;
        p.Formations.Add(new(p.Realm, path.Id, quality, Math.Max(0, 100 - p.Health), Month, "실제 형성"));
        p.Lifespan = p.Realm switch
        {
            Realm.Qi => Law("life.qi"), Realm.Foundation => Law("life.foundation"), Realm.GoldenCore => Law("life.core"),
            Realm.HalfImmortal => Law("founder.lifespan"), _ => Math.Min(12000000, p.Lifespan * Law("life.upperMultiplier") / 100)
        };
        p.Lifespan = Math.Max(p.Lifespan, Month - p.Born + 12);
        Event("돌파", p.Id, 0, $"{p.Name} {Catalog.RealmNames[(int)p.Realm]} 진입·안착", path.Id);
    }

    private void Remember(Person p, string key, string value, string source, int confidence)
    {
        p.Beliefs[key] = new(key, value, confidence, Month, Month + Law("ai.beliefLifetime"), source);
        while (p.Beliefs.Count > Law("ai.beliefBudget"))
        {
            var oldest = p.Beliefs.Values.OrderBy(b => b.Observed).ThenBy(b => b.Confidence).ThenBy(b => b.Key, StringComparer.Ordinal).First();
            p.Beliefs.Remove(oldest.Key);
        }
    }

    private void Observe(Person p)
    {
        if (!p.KnownPlaces.Contains(p.Place)) { p.KnownPlaces.Add(p.Place); p.KnownPlaces.Sort(); }
        var local = residents.GetValueOrDefault(p.Place) ?? [];
        var count = Math.Min(local.Length, 12);
        for (var n = 0; n < count; n++)
        {
            var q = local[(int)((p.Id + Month + n) % local.Length)];
            if (q.Status != LifeStatus.Alive || q.Place != p.Place) continue;
            if (!p.KnownPeople.Contains(q.Id)) p.KnownPeople.Add(q.Id);
            Remember(p, $"person:{q.Id}", $"{q.Name}|{(int)q.Realm}|{q.Layer}|{q.Place}", "현장 관측", 100);
        }
        Remember(p, $"place:{p.Place}", $"{state.Places[p.Place].Source}|{state.Places[p.Place].Qi}|{string.Join(',', state.Places[p.Place].Facilities)}", "현장 관측", 100);
        Remember(p, "self", $"{p.Health}|{p.Money}|{p.Work}", "자기 감각", 100);
    }

    private void Decide(Person p)
    {
        var age = Month - p.Born;
        if (age < 60) { p.Goal = "성장·양육"; return; }
        if (p.Health < 25) { p.Goal = "회복"; p.Reason = "자기 건강 부족"; Start(p, "heal", 0, ""); return; }
        var children = dependents.GetValueOrDefault(p.Id) ?? [];
        var childCount = children.Count(q => q.Status == LifeStatus.Alive && Month - q.Born < Law("birth.maturity"));
        if (Inventory(p.Id, "food") < (childCount + 1) * 12)
        {
            p.Goal = "가계 생계"; p.Reason = "자기 재고와 실제 부양 가족 수요";
            if (p.Beliefs.TryGetValue($"depleted:{p.Place}", out var b) && b.Expires > Month)
            {
                var next = state.Places[p.Place].Neighbors.FirstOrDefault(id => p.KnownPlaces.Contains(id), -1);
                if (next >= 0 && Start(p, "travel", next, "") is null) return;
            }
            Start(p, "gather", 0, "food"); return;
        }
        if (age >= Law("birth.maturity") && p.Partner == 0)
        {
            var q = p.KnownPeople.Where(people.ContainsKey).Select(id => people[id]).FirstOrDefault(q => q.Id != p.Id &&
                q.Status == LifeStatus.Alive && q.Place == p.Place && q.Partner == 0 && q.Gestates != p.Gestates &&
                Month - q.Born >= Law("birth.maturity") && q.Personality[4] + q.Personality[5] >= 60 &&
                (Law("birth.kinship") != 0 || !Related(p, q)));
            if (q is not null) Partner(p, q.Id);
        }
        if (p.Gestates && p.Partner != 0 && BirthQualified(p) && Roll(10000) < Law("birth.chance")) Conceive(p, p.Partner);
        var child = children.FirstOrDefault(q => q.Status == LifeStatus.Alive &&
            q.Place == p.Place && Month - q.Born >= 60 && q.Understanding.GetValueOrDefault("entry") < 1000);
        if (child is not null && p.Understanding.GetValueOrDefault("entry") > 0)
        { p.Goal = "양육·교육"; p.Reason = "후손 입문 지식 전수"; Start(p, "teach", child.Id, "entry"); return; }
        if (p.Understanding.GetValueOrDefault("entry") > 0 && p.Personality[1] >= p.Personality[13])
        { p.Goal = "구도"; p.Reason = "자기 지식·경지·건강과 구도 성향"; Start(p, "cultivate", 0, ""); }
        else { p.Goal = "무예"; p.Reason = "자기 기반과 경쟁 성향"; Start(p, "martial", 0, ""); }
    }
}
