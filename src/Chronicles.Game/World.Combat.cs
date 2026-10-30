using System.Globalization;
using Chronicles.Game.Combat;
using Chronicles.Game.Cultivation;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private sealed class CombatFrame(Person person, CombatProjection projection, long[] equipment)
    {
        public Person Person { get; } = person;
        public CombatProjection Projection { get; } = projection;
        public int Before { get; } = person.Health;
        public long[] Equipment { get; } = equipment;
        public int Exertion { get; set; }
        public long OccupiedUntil { get; set; }
        public CombatParticipant Receipt() => new(Person.Id, Person.BodyId, Person.SoulId, Projection.Axes,
            Before, Person.Health, Exertion, OccupiedUntil, Equipment);
    }

    internal static void ValidateCombatState(GameState snapshot) => CombatMechanics.Validate(snapshot);

    internal string? ValidateCombatGuard(Person person, long target)
    {
        if (target == 0) return null;
        if (!people.TryGetValue(target, out var protectedPerson) || target == person.Id || protectedPerson.Status != LifeStatus.Alive ||
            !person.KnownPeople.Contains(target) || !person.ObservedPeople.TryGetValue(target, out var observation) ||
            observation.Place != person.Place || protectedPerson.Place != person.Place)
            return "직접 관측하고 같은 장소에 있는 상대만 자발적으로 경계할 수 있습니다.";
        return PeopleActionError(person, "martial");
    }

    internal bool TryCancelCombat(Person person, out string? error)
    {
        error = null;
        if (person.Action is not (ActionKind.Guard or ActionKind.Travel) ||
            !person.ActionData.StartsWith("combat:", StringComparison.Ordinal) ||
            !long.TryParse(person.ActionData.AsSpan(7), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            return false;
        var occupiedUntil = state.Combat.Exchanges.FirstOrDefault(e => e.Id == id)?.Participants
            .FirstOrDefault(p => p.Person == person.Id && p.Body == person.BodyId)?.OccupiedUntil ?? 0;
        if (occupiedUntil <= Month) return false;
        error = "이미 치른 교전·지원의 행동 시간은 완료 시점까지 사용 중입니다.";
        return true;
    }

    private CombatRules CombatRulesForWorld() => new(IntLaw("combat.baseDamage"), IntLaw("combat.baseExertion"),
        IntLaw("combat.hitMinimum"), IntLaw("combat.hitMaximum"), IntLaw("combat.exchangeMonths"),
        IntLaw("combat.equipmentWear"), IntLaw("combat.maximumSupport"), state.LawRevision);

    private string? ExecuteCombatAttack(Person attacker, long target)
    {
        if (attacker.Action != ActionKind.Idle || !people.TryGetValue(target, out var defender) ||
            defender.Status != LifeStatus.Alive || defender.Health <= 0 || defender.Id == attacker.Id || defender.Place != attacker.Place ||
            !attacker.KnownPeople.Contains(defender.Id)) return "전투 조건을 만족하지 않습니다.";
        if (PeopleActionError(attacker, "martial") is { } capabilityError) return capabilityError;
        var rules = CombatRulesForWorld(); var until = checked(Month + rules.Months);
        if (!PoliticsLaborAvailable(attacker, Month, until)) return "이미 실제 공동 작업에 참여 중입니다.";
        var frames = new Dictionary<long, CombatFrame>();
        CombatFrame Frame(Person person)
        {
            if (!frames.TryGetValue(person.Id, out var frame))
            {
                frame = new(person, CultivationCombatFor(person), person.Equipment.Where(e => e.Key <= 7)
                    .Select(e => e.Value).Where(id => items.TryGetValue(id, out var item) && item.Owner == person.Id && item.Reserved == 0 &&
                        item.Damage < 100 && (item.Defects & (ItemDefect.BrokenCircuit | ItemDefect.LostCore)) == 0).Distinct().Order().ToArray());
                frames.Add(person.Id, frame);
            }
            return frame;
        }
        var attackFrame = Frame(attacker); var defenseFrame = Frame(defender);
        var cost = CombatMechanics.Exertion(attackFrame.Projection.Axes, CultivationPolicyForWorld().FlowPerMonth, rules);
        if (attacker.Health <= cost) return "이번 교전의 실제 소모를 감당할 건강이 부족합니다.";
        var receiptId = state.Combat.NextId;
        var source = "combat:" + receiptId.ToString(CultureInfo.InvariantCulture);
        var strikes = new List<CombatStrike>();
        void Commit(CombatFrame frame)
        {
            frame.OccupiedUntil = until;
            frame.Person.Action = ActionKind.Guard; frame.Person.ActionTarget = receiptId;
            frame.Person.ActionData = source; frame.Person.ActionRevision = rules.Revision; frame.Person.Due = until;
        }
        void Pay(CombatFrame frame, int amount)
        { frame.Person.Health -= amount; frame.Exertion += amount; }
        List<CombatFrame> Support(Person principal, Person opponent)
        {
            var selected = new List<CombatFrame>();
            foreach (var id in principal.KnownPeople.Order())
            {
                if (selected.Count >= rules.MaximumSupport) break;
                if (id == principal.Id || id == opponent.Id || !people.TryGetValue(id, out var person) ||
                    person.Status != LifeStatus.Alive || person.Place != principal.Place ||
                    person.Action != ActionKind.Guard || person.ActionTarget != principal.Id || person.Due < until ||
                    person.ActionData.Length != 0 || ValidateCombatGuard(person, principal.Id) is not null ||
                    state.Politics.Labor.Any(r => r.Person == person.Id && r.From < until && Month < r.Until)) continue;
                var supportCost = CombatMechanics.Exertion(CultivationCombatFor(person).Axes, CultivationPolicyForWorld().FlowPerMonth, rules);
                if (person.Health <= supportCost) continue;
                var support = Frame(person);
                Pay(support, supportCost); Commit(support); selected.Add(support);
            }
            return selected;
        }
        void Strike(Person from, Person to)
        {
            var initiator = Frame(from); var recipient = Frame(to);
            var axes = from.Id == attacker.Id ? initiator.Projection.Axes : CultivationCombatFor(from).Axes;
            var defense = to.Id == defender.Id ? recipient.Projection.Axes : CultivationCombatFor(to).Axes;
            var supporters = Support(from, to);
            var roles = 1 + supporters.Count + initiator.Projection.ControlledSubspiritRoles + Math.Max(0, initiator.Equipment.Length - 1);
            var coordination = CombatMechanics.Coordination(axes, roles);
            Int128 output = axes.OutputPenetration;
            foreach (var helper in supporters) output += helper.Projection.Axes.OutputPenetration / 4;
            var effectiveOutput = (long)Int128.Clamp(output * coordination / 10000, 0, long.MaxValue);
            var sustainability = CombatMechanics.Sustainability(axes, CultivationPolicyForWorld().FlowPerMonth);
            var chance = CombatMechanics.HitChance(axes, defense, coordination, rules); var roll = Roll(10000);
            var hit = roll < chance;
            var damage = hit ? CombatMechanics.Damage(axes, defense, effectiveOutput, sustainability, rules) : 0;
            if (hit && to.Traits.FirstOrDefault(t => t.Id == "호신부" && t.Active) is { } ward)
            { damage = Math.Max(1, damage / 2); to.Traits.Remove(ward); }
            to.Health = Math.Max(0, to.Health - damage);
            strikes.Add(new(from.Id, to.Id, chance, roll, hit, damage, coordination, sustainability, supporters.Select(s => s.Person.Id).ToArray()));
            Gain(from, "combat", hit ? 10 : 3); Gain(to, "combat", hit ? 5 : 2);
            Remember(from, $"combat:{to.Id}", hit ? $"실제 명중·피해 {damage}" : "실제 빗나감", source, 100);
            Remember(to, $"combat:{from.Id}", hit ? $"실제 피격·피해 {damage}" : "실제 회피", source, 100);
        }
        Pay(attackFrame, cost); Commit(attackFrame); Strike(attacker, defender);
        // The attacked actor now has an actual current witness, without acquiring hidden paths, stock or aptitudes.
        if (!defender.KnownPeople.Contains(attacker.Id)) { defender.KnownPeople.Add(attacker.Id); defender.KnownPeople.Sort(); }
        var witnessed = new PersonObservation(attacker.Id, attacker.Name, attacker.Realm, attacker.Layer, attacker.Place,
            Month, attacker.Gestates, Month - attacker.Born >= PeopleMaturityMonths(attacker));
        defender.ObservedPeople[attacker.Id] = witnessed;
        TrimCognition(defender);
        var reaction = new CombatReaction(defender.Id, "guard", 0, -1, -1, "실제로 목격한 교전과 자기 상태");
        if (defender.Health == 0) reaction = reaction with { Choice = "incapacitated" };
        else if (state.Controlled == defender.Id) reaction = reaction with { Choice = "controlled", Evidence = "조작 인물의 후속 선택을 보존" };
        else if (defender.Action != ActionKind.Idle || !PoliticsLaborAvailable(defender, Month, until))
            reaction = reaction with { Choice = "busy", Evidence = "기존 실제 작업을 덮어쓰지 않는 수동 방어" };
        else
        {
            var observed = witnessed;
            var wantsRetreat = defender.Health < defenseFrame.Before - defenseFrame.Before / 4 ||
                defender.Health < 35 || observed.Realm > defender.Realm || defender.Personality[6] + defender.Personality[10] < 100;
            var destination = defender.KnownPlaces.Order().FirstOrDefault(id => state.Places[defender.Place].Neighbors.Contains(id) &&
                defender.ObservedPlaces.ContainsKey(id), -1);
            var defenseCost = CombatMechanics.Exertion(CultivationCombatFor(defender).Axes, CultivationPolicyForWorld().FlowPerMonth, rules);
            if (wantsRetreat && destination >= 0 && !defender.Sealed && defender.Health > defenseCost && PeopleActionError(defender, "travel") is null)
            {
                var chance = CombatMechanics.EscapeChance(CultivationCombatFor(defender).Axes, CultivationCombatFor(attacker).Axes);
                var roll = Roll(10000); Pay(defenseFrame, defenseCost); Commit(defenseFrame);
                reaction = new(defender.Id, "escape-failed", chance, roll, destination, "실제 피격·자기 여력·현장 관측한 통로의 이탈 판정");
                if (roll < chance)
                {
                    defender.Action = ActionKind.Idle; defender.Due = 0;
                    if (Start(defender, "travel", destination, source) is null)
                    { defender.Due = Math.Max(defender.Due, until); reaction = reaction with { Choice = "retreat" }; }
                    else Commit(defenseFrame);
                }
            }
            else if (!wantsRetreat && defender.Health > defenseCost && PeopleActionError(defender, "martial") is null)
            {
                Pay(defenseFrame, defenseCost); Commit(defenseFrame); Strike(defender, attacker);
                reaction = reaction with { Choice = "counter", Evidence = "자기 건강·성향·실제로 목격한 공격에 대한 한 번의 독립 반격" };
            }
            else Commit(defenseFrame);
        }
        foreach (var frame in frames.Values.Where(f => f.Exertion > 0 || strikes.Any(s => s.Defender == f.Person.Id && s.Hit)))
            foreach (var id in frame.Equipment)
                if (items.TryGetValue(id, out var item) && item.Owner == frame.Person.Id && item.Reserved == 0 && item.Damage < 100)
                    item.Damage = Math.Min(100, item.Damage + rules.EquipmentWear);
        NormalizeInventoryPhysicalState();
        var receipt = new CombatExchange(receiptId, Month, attacker.Place, rules, frames.Values.OrderBy(f => f.Person.Id).Select(f => f.Receipt()).ToArray(),
            strikes.ToArray(), reaction, source);
        state.Combat.Exchanges.Add(receipt); state.Combat.NextId = checked(receiptId + 1);
        foreach (var victim in frames.Values.Select(f => f.Person).Where(p => p.Health == 0).OrderBy(p => p.Id).ToArray())
            EndLife(victim, 2, "전투 사망");
        Event("전투", attacker.Id, defender.Id, $"{attacker.Name}→{defender.Name} 실제 교전·피해 {strikes[0].Damage}·" +
            $"{(strikes[0].Hit ? "명중" : "빗나감")}·반응 {reaction.Choice}", source);
        CombatMechanics.Validate(state);
        return null;
    }
}
