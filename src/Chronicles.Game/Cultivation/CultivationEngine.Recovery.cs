namespace Chronicles.Game.Cultivation;

public static partial class CultivationEngine
{
    private static CultivationResult Stabilize(CultivationState state, CultivationActor actor, CultivationContext context)
    {
        var attempt = state.Attempt;
        if (attempt?.Phase is not (AttemptPhase.Stabilizing or AttemptPhase.Recovering))
            return Reject(state, "phase", "안착/회복 공정 없음", "현재 상태 확인");
        if (context.Month < attempt.Due || context.Month <= state.LastTrainingMonth)
            return Reject(state, "time", "안착 월 경계 미도달", "다음 정수 월까지 진행");
        if (!TryDebit(context, context.Policy.FlowPerMonth, out var debit))
            return Reject(state, "supply", "유지·안착 공급 부족", "실제 공급 확보");
        var remaining = Math.Max(0, attempt.RecoveryRemaining - 1);
        var phase = remaining == 0 ? AttemptPhase.Completed : attempt.Phase;
        return Accept(state with { Attempt = attempt with { Phase = phase, RecoveryRemaining = remaining, Due = context.Month + 1 },
            LastTrainingMonth = context.Month, Damage = Math.Max(0, state.Damage - context.Policy.RecoveryPerMonth), Load = 0 },
            new(HealthChange: actor.Health < 100 ? 1 : 0), debit,
            notices: [new("안착", remaining == 0 ? "안착/회복 경계 완료" : "유지 순환·손상 회복", attempt.Path)], flow: SpentFlow(debit));
    }

    private static CultivationResult Recover(CultivationState state, CultivationActor actor, RecoverCultivation command, CultivationContext context)
    {
        if (ActiveAttempt(state))
            return Reject(state, "attempt", "진행 중 공정은 일반 휴식으로 전환 불가", "전용 안착/회복 또는 안전 취소");
        if (context.Month <= state.LastTrainingMonth)
            return Reject(state, "time", "이미 처리한 수행 월", "다음 월까지 진행");
        Elements debit = default;
        var structuralRecovery = state.Structure is not null && (state.Damage > 0 || state.RootDamage.Total > 0);
        if (structuralRecovery && !TryDebit(context, context.Policy.FlowPerMonth, out debit))
            return Reject(state, "supply", "구조 회복 공급 부족", "실제 공급 확보");
        var treatment = command.MedicalTreatment ? context.Supports.FirstOrDefault(s => s.Kind == "pill" && s.Quantity > 0 && s.ReservedBy.Length == 0) : null;
        if (command.MedicalTreatment && treatment is null)
            return Reject(state, "medicine", "실제 치료 물품 없음", "물품 확보 또는 자연 회복");
        var rate = context.Policy.RecoveryPerMonth * (treatment is null ? 1 : 2);
        return Accept(state with { Damage = Math.Max(0, state.Damage - rate), Load = 0,
            RootDamage = Elements.Map(state.RootDamage, value => Math.Max(0, value - rate)), LastTrainingMonth = context.Month,
            Martial = state.Martial with { Injury = Math.Max(0, state.Martial.Injury - rate) } },
            new(HealthChange: Math.Min(100 - actor.Health, treatment is null ? 2 : 10)), debit,
            supports: treatment is null ? [] : [new(treatment.ItemId, SupportOperation.Consume, "", 1)],
            notices: [new("회복", "손상 교정, 퇴행한 현재 성장은 자원/시간을 들여 재수행", command.MedicalTreatment ? "실제 치료 실물" : "유지·휴식")], flow: SpentFlow(debit));
    }

    private static CultivationResult Damage(CultivationState state, CultivationActor actor, DamageCultivation command, CultivationContext context)
    {
        if (command.Severity is <= 0 or > 10000)
            return Reject(state, "damage", "손상은 1..10000", "실제 사건 손상 사용");
        if (state.Attempt?.Phase == AttemptPhase.Fractured && command.Severity >= 10000 - state.Damage)
            return Die(state, CultivationDeathCause.CoreFracture, "비가역 성영 중 구조 붕괴");
        var damage = Math.Min(10000, state.Damage + command.Severity);
        var next = state with
        {
            Damage = damage,
            Structure = state.Structure is null ? null : Scale(state.Structure, 10000 - command.Severity),
            RootDamage = command.Cause is CultivationDamageCause.OverAbsorption or CultivationDamageCause.WrongCirculation
                ? Elements.Map(state.RootDamage, value => Math.Min(10000, value + command.Severity)) : state.RootDamage,
            RootGrowth = actor.Realm == Realm.Qi ? Elements.Map(state.RootGrowth, value => value * (10000 - command.Severity) / 10000) : state.RootGrowth
        };
        if (damage == 10000 && actor.Realm >= Realm.Void)
            return Die(next, command.Cause == CultivationDamageCause.BondConflict && actor.Realm >= Realm.Integration
                ? CultivationDeathCause.BondCollapse : CultivationDeathCause.StructureCollapse, command.Cause.ToString());
        return Accept(next, new(Layer: Math.Max(1, Layer(next, actor.Realm, context.Policy)), GreatPerfection: false,
            HealthChange: command.Cause == CultivationDamageCause.Neglect ? 0 : -(command.Severity / 500)),
            notices: [new("퇴행", "현재 구조 손상·형성 품질/과거 이력 보존", command.Cause.ToString())]);
    }
}
