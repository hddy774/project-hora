namespace Chronicles.Game;

/// <summary>Checks are shared by command commit and save/restore, before a candidate is published.</summary>
public static class Invariants
{
    private static void Require(bool condition, string invariant, string message)
    { if (!condition) throw new InvalidDataException($"{invariant}: {message}"); }

    public static void Validate(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Require(state.Schema == 1 && state.Month >= 0 && state.Revision >= 0 && state.LawRevision >= 1, "INV-004", "저장/시간 버전");
        Require(Laws.Validate(state.Laws) is null, "INV-003", "법칙 범위");
        Require(state.Places.Count == 100 && state.Places.Select(x => x.Id).SequenceEqual(Enumerable.Range(0, 100)), "INV-002", "정확히 100지점");
        Require(state.Places.GroupBy(x => x.Province).Count() == 10 && state.Places.GroupBy(x => x.Province).All(g => g.Count() == 10), "INV-002", "10주×10지점");
        foreach (var place in state.Places)
        {
            Require(place.Province == place.Id / 10 && place.Source >= 0 && place.Qi >= 0 && place.DiscoveryRemaining is 0 or 1, "INV-018", "유한 원천");
            Require(place.Neighbors.Distinct().Count() == place.Neighbors.Count && place.Neighbors.All(n => n >= 0 && n < 100 && state.Places[n].Neighbors.Contains(place.Id)), "INV-002", "양방향 통로");
        }
        var ids = new HashSet<long>();
        void Unique(long id) => Require(id > 0 && id < state.NextId && ids.Add(id), "INV-001", "중복/잘못된 안정 ID");
        foreach (var p in state.People) { Unique(p.Id); Unique(p.BodyId); }
        foreach (var s in state.Souls) Unique(s.Id);
        foreach (var i in state.Items) Unique(i.Id);
        foreach (var g in state.Groups) Unique(g.Id);
        foreach (var c in state.Contracts) Unique(c.Id);
        foreach (var e in state.Events) Unique(e.Id);
        foreach (var b in state.Pregnancies) Unique(b.Id);
        var people = state.People.ToDictionary(x => x.Id); var souls = state.Souls.ToDictionary(x => x.Id);
        var items = state.Items.ToDictionary(x => x.Id); var groups = state.Groups.ToDictionary(x => x.Id);
        foreach (var p in state.People)
        {
            Require(p.Place >= 0 && p.Place < 100 && Enum.IsDefined(p.Status) && Enum.IsDefined(p.Realm), "INV-002", "인물의 실제 위치/경지");
            Require(p.Born <= state.Month && p.Lifespan > 0 && p.Health is >= 0 and <= 100 && p.Money >= 0 && p.Work >= 0 && p.Layer is >= 1 and <= 9 && p.Martial is >= 0 and <= 10, "INV-011", "생애 값 범위");
            Require(p.Roots.Length == 5 && p.Aptitudes.Length == 6 && p.Personality.Length == 14 && p.Components.Length == 7 && p.Processes.Length == 4 &&
                p.Roots.Concat(p.Aptitudes).All(x => x is >= 0 and <= 10000), "INV-015", "자질/공정 구조");
            Require(p.Personality.All(x => x is >= 0 and <= 100), "INV-015", "성격 범위");
            Require(souls.ContainsKey(p.SoulId), "INV-014", "영혼 참조");
            Require(p.ParentOne == 0 || (people.ContainsKey(p.ParentOne) && p.ParentOne < p.Id && people[p.ParentOne].Born < p.Born), "INV-012", "생물학 부모 DAG");
            Require(p.ParentTwo == 0 || (people.ContainsKey(p.ParentTwo) && p.ParentTwo < p.Id && people[p.ParentTwo].Born < p.Born), "INV-012", "생물학 부모 DAG");
            Require(p.ParentOne == 0 || p.ParentOne != p.ParentTwo, "INV-012", "동일 부모 중복");
            Require(p.Partner == 0 || (people.TryGetValue(p.Partner, out var partner) && partner.Partner == p.Id && partner.Status == LifeStatus.Alive && p.Status == LifeStatus.Alive), "INV-011", "상호 도려");
            Require(p.Guardian == 0 || people.ContainsKey(p.Guardian), "INV-012", "양육자 참조");
            Require(p.Household == 0 || groups.ContainsKey(p.Household), "INV-019", "가계 참조");
            Require(p.Sect == 0 || (groups.TryGetValue(p.Sect, out var sect) && sect.Kind == GroupKind.Sect && !sect.Dissolved && sect.Members.Contains(p.Id)), "INV-019", "종문 소속");
            Require(p.Beliefs.Count <= state.Laws["ai.beliefBudget"] && p.Beliefs.All(b => b.Key == b.Value.Key && b.Value.Observed <= state.Month && b.Value.Confidence is >= 0 and <= 100), "INV-008", "개인 인지 예산/출처");
            Require(p.KnownPlaces.Distinct().Count() == p.KnownPlaces.Count && p.KnownPlaces.All(n => n >= 0 && n < 100) && p.KnownPeople.All(people.ContainsKey), "INV-009", "개인 지식 참조");
            Require(p.Action == ActionKind.Idle || (Enum.IsDefined(p.Action) && p.Due > state.Month && p.Status == LifeStatus.Alive), "INV-021", "행동 단일 용량/시각");
            Require(!p.Irreversible || (p.Action == ActionKind.Breakthrough && p.Realm == Realm.GoldenCore), "INV-023", "원영 파단 경계");
            Require(p.Skills.Values.Concat(p.Understanding.Values).All(v => v is >= 0 and <= 10000), "INV-024", "개인 숙련/이해 범위");
            foreach (var (slot, itemId) in p.Equipment)
                Require(slot >= 0 && slot < 12 && items.TryGetValue(itemId, out var item) && item.Owner == p.Id &&
                    item.Container == 0 && item.Quantity == 1 && Catalog.Items[item.Definition].EquipmentSlot == slot, "INV-016", "단일 실물 장착");
        }
        foreach (var soul in state.Souls)
        {
            Require(souls.ContainsKey(soul.Root) && (soul.Parent == 0 || (souls.ContainsKey(soul.Parent) && soul.Parent < soul.Id)), "INV-014", "혼 계보 DAG");
            Require(soul.CurrentPerson == 0 || (people.TryGetValue(soul.CurrentPerson, out var p) && p.SoulId == soul.Id && p.Status == LifeStatus.Alive), "INV-014", "현생 영혼 일대일");
            Require(soul.Route is >= 0 and <= 13 && soul.Traces.Length == 6 && soul.Traces.All(t => t.Integrated >= 0 && t.Integrated <= t.Accessible && t.Accessible <= t.Preserved && t.Preserved <= 100), "INV-013", "윤회/혼흔 단계");
            Require(soul.Route == 0 || !string.IsNullOrWhiteSpace(soul.Evidence), "INV-013", "원인 직전 증거");
        }
        Require(state.People.Where(p => p.Status == LifeStatus.Alive).Select(p => p.SoulId).Distinct().Count() == state.People.Count(p => p.Status == LifeStatus.Alive), "INV-014", "동시 현생 중복");
        foreach (var p in state.Pregnancies)
        {
            Require(people.ContainsKey(p.Mother) && people.ContainsKey(p.Father) && p.Mother != p.Father && p.LawRevision <= state.LawRevision && p.Roots.Length == 5 && p.Aptitudes.Length == 6, "INV-011", "임신 출처");
            Require(p.Child == 0 || (p.Completed && people.TryGetValue(p.Child, out var child) && child.ParentOne == p.Mother && child.ParentTwo == p.Father), "INV-005", "출생 단일 결과");
        }
        Require(state.Pregnancies.Where(x => !x.Completed).GroupBy(x => x.Mother).All(g => g.Count() == 1), "INV-011", "중복 임신");
        foreach (var item in state.Items)
        {
            Require(people.ContainsKey(item.Owner) && Catalog.Items.ContainsKey(item.Definition) && item.Quantity > 0 && item.Quality is >= 0 and <= 10000 && item.Damage is >= 0 and <= 100 && item.Place is >= -1 and < 100, "INV-016", "실물·소유·수량");
            var seen = new HashSet<long> { item.Id }; var containerId = item.Container;
            while (containerId != 0)
            {
                Require(seen.Add(containerId) && items.ContainsKey(containerId), "INV-016", "수납 DAG");
                var c = items[containerId];
                Require(c.Owner == item.Owner && Catalog.Items[c.Definition].Capacity > 0, "INV-016", "용기 권리");
                containerId = c.Container;
            }
            if (Catalog.Items[item.Definition].Capacity > 0)
                Require(state.Items.Where(x => x.Container == item.Id).Sum(x => (long)x.Quantity * Catalog.Items[x.Definition].Weight) <= Catalog.Items[item.Definition].Capacity, "INV-016", "용기 용량");
            if (item.Reserved != 0)
                Require(state.Contracts.Any(c => c.Id == item.Reserved && c.Status == "open" && c.Item == item.Id) ||
                    state.People.Any(p => p.Action == ActionKind.Craft && p.ActionTarget == item.Reserved && p.Id == item.Owner), "INV-017", "예약 소유자");
        }
        foreach (var group in state.Groups)
        {
            Require(group.Money >= 0 && group.PolicyEscrow >= 0 && group.Members.Distinct().Count() == group.Members.Count && group.Members.All(people.ContainsKey), "INV-018", "조직 금고·구성원");
            Require(group.Leader == 0 || (people.ContainsKey(group.Leader) && group.Members.Contains(group.Leader)), "INV-020", "직책 소유자");
            var seen = new HashSet<long> { group.Id }; var parent = group.Overlord;
            while (parent != 0)
            {
                Require(seen.Add(parent) && groups.ContainsKey(parent), "INV-019", "봉신 DAG"); parent = groups[parent].Overlord;
            }
            Require(group.Policies.Keys.Concat(group.PendingPolicies.Keys).All(Catalog.PolicyNames.Contains), "INV-020", "정책 분류");
        }
        foreach (var place in state.Places)
            Require(place.Sovereign == 0 || (groups.TryGetValue(place.Sovereign, out var sovereign) && !sovereign.Dissolved && sovereign.Provinces.Contains(place.Province)), "INV-019", "주권 실제 근거");
        foreach (var c in state.Contracts)
        {
            Require(people.ContainsKey(c.Seller) && (c.Buyer == 0 || people.ContainsKey(c.Buyer)) && c.Escrow >= 0, "INV-017", "계약 당사자/예치");
            Require(c.Status != "open" || (items.TryGetValue(c.Item, out var i) && i.Reserved == c.Id), "INV-017", "계약 실물 예약");
            Require(c.Status == "open" || c.Escrow == 0, "INV-005", "종결 예치 정산");
        }
        var money = state.People.Sum(p => p.Money) + state.Groups.Sum(g => g.Money + g.PolicyEscrow) + state.Contracts.Sum(c => c.Escrow);
        Require(money == state.CreatedMoney - state.ConsumedMoney, "INV-018", "영석 보존");
        Require(state.Controlled == 0 || (people.TryGetValue(state.Controlled, out var controlled) && controlled.Status == LifeStatus.Alive), "INV-009", "조작권");
        Require(state.Commands.Keys.SequenceEqual(state.CommandSignatures.Keys) && state.Commands.All(x => x.Key == x.Value.Id && x.Value.Accepted && x.Value.Revision <= state.Revision), "INV-005", "명령 멱등 원장");
    }
}
