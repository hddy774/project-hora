using Chronicles.Game.Cultivation;

namespace Chronicles.Game.Combat;

public sealed record CombatRules(int BaseDamage, int BaseExertion, int MinimumHit, int MaximumHit,
    int Months, int EquipmentWear, int MaximumSupport, long Revision);
public sealed record CombatParticipant(long Person, long Body, long Soul, CombatAxes Axes,
    int HealthBefore, int HealthAfter, int Exertion, long OccupiedUntil, IReadOnlyList<long> Equipment);
public sealed record CombatStrike(long Attacker, long Defender, int HitChance, int HitRoll, bool Hit,
    int Damage, int Coordination, int Sustainability, IReadOnlyList<long> Supporters);
public sealed record CombatReaction(long Person, string Choice, int EscapeChance, int EscapeRoll,
    int Destination, string Evidence);
public sealed record CombatExchange(long Id, long Month, int Place, CombatRules Rules,
    IReadOnlyList<CombatParticipant> Participants, IReadOnlyList<CombatStrike> Strikes,
    CombatReaction Reaction, string Source);
public sealed class CombatLedger
{
    public long NextId { get; set; } = 1;
    public long Pruned { get; set; }
    public List<CombatExchange> Exchanges { get; set; } = [];
}

/// <summary>Integer arithmetic for the live world's bounded exchange, without state, RNG or generated combatants.</summary>
public static class CombatMechanics
{
    public static int Ratio(long value, long against) => (int)((Int128)Math.Max(0, value) * 10000 /
        Int128.Max(1, (Int128)Math.Max(0, value) + Math.Max(0, against)));
    private static int Normalize(long value, long basis) => (int)Int128.Clamp((Int128)Math.Max(0, value) * 10000 /
        Math.Max(1, basis), 1, 100000);
    public static int Sustainability(CombatAxes axes, long flow) =>
        Ratio(Normalize(axes.ReserveEndurance, axes.OutputPenetration), Normalize(axes.MaintenanceCost, flow));
    public static int Exertion(CombatAxes axes, long flow, CombatRules rules) => (int)Int128.Clamp(
        ((Int128)rules.BaseExertion * Normalize(axes.MaintenanceCost, flow) + Normalize(axes.ReserveEndurance, axes.OutputPenetration) - 1) /
        Normalize(axes.ReserveEndurance, axes.OutputPenetration), 1, 100);
    public static int Coordination(CombatAxes axes, int actualRoles) =>
        10000 - (10000 - Ratio(axes.CoordinationLoad, axes.ControlPrecision)) * Math.Clamp(actualRoles, 1, 4) / 5;
    public static int HitChance(CombatAxes attack, CombatAxes defense, int coordination, CombatRules rules)
    {
        var control = Ratio(attack.ControlPrecision, defense.MovementSpace);
        var sensing = Ratio(attack.SensingConcealment, defense.SensingConcealment);
        var approach = Ratio(attack.MovementSpace, defense.ControlPrecision);
        var pressure = Ratio(attack.OutputPenetration, defense.ProtectionRecovery);
        var score = (control * 2 + sensing + approach) / 4 + (sensing - 5000) / 4 + (pressure - 5000) / 3;
        return Math.Clamp(score * coordination / 10000, rules.MinimumHit, rules.MaximumHit);
    }
    public static int Damage(CombatAxes attack, CombatAxes defense, long effectiveOutput, int sustainability, CombatRules rules) =>
        (int)Int128.Clamp((Int128)rules.BaseDamage * Math.Max(0, effectiveOutput) * (5000 + sustainability / 2) /
            Math.Max(1, defense.ProtectionRecovery) / 10000, 1, 100);
    public static int EscapeChance(CombatAxes escaping, CombatAxes pursuing) => Math.Clamp(
        (Ratio(escaping.MovementSpace, pursuing.MovementSpace) * 2 +
            Ratio(escaping.SensingConcealment, pursuing.SensingConcealment)) / 3, 0, 10000);

    public static void Validate(GameState state)
    {
        var ledger = state.Combat;
        if (ledger is null || ledger.NextId < 1 || ledger.Pruned < 0 || ledger.Exchanges is null ||
            ledger.Pruned != ledger.NextId - 1 - ledger.Exchanges.Count ||
            ledger.Exchanges.Select(e => e.Id).Distinct().Count() != ledger.Exchanges.Count ||
            ledger.NextId <= ledger.Exchanges.Select(e => e.Id).DefaultIfEmpty().Max())
            throw new InvalidDataException("전투 영수증 식별자가 올바르지 않습니다.");
        foreach (var exchange in ledger.Exchanges)
        {
            if (exchange.Id <= 0 || exchange.Month < 0 || exchange.Month > state.Month || exchange.Place is < 0 or >= 100 ||
                exchange.Rules is null || exchange.Rules.Revision <= 0 || exchange.Rules.Revision > state.LawRevision ||
                exchange.Rules.BaseDamage is < 1 or > 100 || exchange.Rules.BaseExertion is < 1 or > 30 ||
                exchange.Rules.MinimumHit is < 0 or > 10000 || exchange.Rules.MaximumHit < exchange.Rules.MinimumHit || exchange.Rules.MaximumHit > 10000 ||
                exchange.Rules.Months is < 1 or > 120 || exchange.Rules.EquipmentWear is < 0 or > 100 || exchange.Rules.MaximumSupport is < 0 or > 3 ||
                string.IsNullOrWhiteSpace(exchange.Source) || exchange.Participants is null || exchange.Strikes is null || exchange.Reaction is null ||
                exchange.Participants.Count is < 2 or > 8 || exchange.Strikes.Count is < 1 or > 2 ||
                exchange.Participants.Select(p => p.Person).Distinct().Count() != exchange.Participants.Count)
                throw new InvalidDataException("실제 전투의 신원·시각·법칙·범위 영수증이 올바르지 않습니다.");
            foreach (var p in exchange.Participants)
            {
                var person = state.People.SingleOrDefault(x => x.Id == p.Person);
                if (person is null || person.BodyId != p.Body || person.SoulId != p.Soul || p.HealthBefore is < 1 or > 100 ||
                    p.HealthAfter < 0 || p.HealthAfter > p.HealthBefore || p.Exertion < 0 || p.Exertion >= p.HealthBefore ||
                    p.OccupiedUntil != 0 && p.OccupiedUntil != exchange.Month + exchange.Rules.Months ||
                    p.Equipment is null || p.Equipment.Distinct().Count() != p.Equipment.Count || Enumerable.Range(0, 8).Any(i => p.Axes.At(i) < 0))
                    throw new InvalidDataException("전투 참여자의 실제 육신·건강·소모 증거가 올바르지 않습니다.");
                if (p.OccupiedUntil > state.Month && person.Status == LifeStatus.Alive &&
                    (person.Action is not (ActionKind.Guard or ActionKind.Travel) || person.Due < p.OccupiedUntil || person.ActionData != exchange.Source))
                    throw new InvalidDataException("확정된 전투의 실제 행동 시간이 해제되었습니다.");
                if (person.Status == LifeStatus.Alive && person.Action == ActionKind.Travel && person.ActionData == exchange.Source &&
                    (exchange.Reaction.Choice != "retreat" || exchange.Reaction.Person != p.Person ||
                        exchange.Reaction.Destination != person.ActionTarget || person.Due < p.OccupiedUntil))
                    throw new InvalidDataException("전투 이탈의 실제 이동 경로와 영수증이 일치하지 않습니다.");
            }
            foreach (var strike in exchange.Strikes)
            {
                if (strike.Attacker == strike.Defender || !exchange.Participants.Any(p => p.Person == strike.Attacker) ||
                    !exchange.Participants.Any(p => p.Person == strike.Defender) || strike.HitChance < exchange.Rules.MinimumHit ||
                    strike.HitChance > exchange.Rules.MaximumHit || strike.HitRoll is < 0 or >= 10000 || strike.Hit != (strike.HitRoll < strike.HitChance) ||
                    strike.Damage < 0 || strike.Damage > 100 || strike.Damage == 0 && strike.Hit || strike.Damage != 0 && !strike.Hit ||
                    strike.Coordination is < 0 or > 10000 || strike.Sustainability is < 0 or > 10000 || strike.Supporters is null ||
                    strike.Supporters.Count > exchange.Rules.MaximumSupport || strike.Supporters.Distinct().Count() != strike.Supporters.Count ||
                    strike.Supporters.Any(id => id == strike.Attacker || id == strike.Defender || !exchange.Participants.Any(p => p.Person == id)))
                    throw new InvalidDataException("전투 타격·명중·자발적 지원 영수증이 올바르지 않습니다.");
            }
            foreach (var p in exchange.Participants)
            {
                var incoming = exchange.Strikes.Where(s => s.Defender == p.Person).Sum(s => s.Damage);
                if (Math.Max(0, p.HealthBefore - p.Exertion - incoming) != p.HealthAfter)
                    throw new InvalidDataException("전투 건강 지출과 실제 피해의 합이 일치하지 않습니다.");
            }
            if (!exchange.Participants.Any(p => p.Person == exchange.Reaction.Person) ||
                exchange.Reaction.Choice is not ("guard" or "busy" or "retreat" or "escape-failed" or "counter" or "incapacitated" or "controlled") ||
                exchange.Reaction.EscapeChance is < 0 or > 10000 || exchange.Reaction.EscapeRoll is < -1 or >= 10000 ||
                exchange.Reaction.Destination is < -1 or >= 100 || string.IsNullOrWhiteSpace(exchange.Reaction.Evidence))
                throw new InvalidDataException("전투 반응의 실제 판단·경로 증거가 올바르지 않습니다.");
        }
    }
}
