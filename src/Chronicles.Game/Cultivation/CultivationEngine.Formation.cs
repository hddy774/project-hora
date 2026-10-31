namespace Chronicles.Game.Cultivation;

public static partial class CultivationEngine
{
    private static CultivationResult Prepare(CultivationState state, CultivationActor actor, PrepareCultivation command, CultivationContext context)
    {
        var blockers = Preflight(state, actor, command.PathId, context, command.Conversion);
        if (blockers.Count > 0)
            return Rejected(state, blockers);
        var items = command.SupportItems ?? [];
        if (items.Distinct().Count() != items.Count)
            return Reject(state, "support", "중복 보조 물품", "실물 ID 한 번만 예약");
        var supports = items.Select(id => context.Supports.FirstOrDefault(s => s.ItemId == id)).ToArray();
        if (supports.Any(s => s is null || s.Quantity <= 0 || s.ReservedBy.Length != 0))
            return Reject(state, "reservation", "보조 실물이 없거나 이미 예약됨", "소유·사용 가능한 실제 물품 선택");
        var profile = CultivationProfiles.Find(command.PathId)!;
        var knowledge = PreparationKnowledge(state, actor, command.PathId, context, command.Conversion)!;
        var quality = FormationQuality(state, actor, knowledge, context);
        if (quality < context.Policy.MinimumFormation)
            return Reject(state, "formation", "실제 형성 기반 부족", "구조·정탁·육신·정신 보완");
        // Pills affect risk only; no missing prerequisite or unlearned chapter is manufactured.
        var pillBonus = supports.Where(s => s!.Kind == "pill").Sum(s => s!.Quality / 20);
        var risk = Math.Clamp(context.Policy.BaseRisk + profile.Definition.Risk + state.Damage / 5 +
            (100 - actor.Health) * 10 + knowledge.Error / 5 - pillBonus, 0, 10000);
        var kind = command.Conversion ? AttemptKind.RouteConversion : actor.Realm == Realm.Mortal ? AttemptKind.Entry : AttemptKind.RealmBreakthrough;
        var attempt = new CultivationAttempt(command.Id, kind, AttemptPhase.Prepared, profile.Definition.Realm, command.PathId,
            context.Month, context.LawRevision, knowledge with { }, items.ToArray(), quality, risk, context.Month, 0, ParentEvidence(state, actor, context.Policy))
        {
            TargetWork = command.Conversion ? RealmTarget(state, context.Policy) : context.Policy.RequiredWork(profile.Definition.Realm),
            TargetRootWork = profile.Definition.Realm == Realm.Qi ? context.Policy.RequiredWork(Realm.Qi) / 5 : RootTarget(state, context.Policy),
            LifespanAward = context.Policy.Lifespan(profile.Definition.Realm)
        };
        return Accept(state with { Attempt = attempt }, supports: items.Select(id => new SupportDelta(id, SupportOperation.Reserve, attempt.Id, 1)).ToArray(),
            notices: [new("돌파 준비", "자격 검사·실물 예약 완료, 아직 미개시", command.PathId)]);
    }

    private static CultivationResult Start(CultivationState state, CultivationActor actor, CultivationContext context)
    {
        var attempt = state.Attempt;
        if (attempt?.Phase != AttemptPhase.Prepared)
            return Reject(state, "phase", "준비된 시도가 아님", "자격 검사·준비부터 진행");
        var path = CultivationProfiles.Find(attempt.Path)!;
        var blockers = new List<CultivationBlocker>();
        AddAccess(blockers, context.Site);
        AddPathConditions(blockers, path, actor, context);
        if (actor.Health < 50 || state.Damage > context.Policy.FailureDamage)
            blockers.Add(new("damage", "준비 이후 육신/구조 조건 악화", "개시 전 회복 또는 준비 취소"));
        if (!attempt.Knowledge.Memorized && !context.Knowledge.Any(k => k.WorkId == attempt.Knowledge.WorkId &&
            k.EditionId == attempt.Knowledge.EditionId && k.Revision == attempt.Knowledge.Revision && k.CanConsult))
            blockers.Add(new("consult", "채택 판본 현장 열람 불가", "동일 판본 접근 복구 또는 준비 취소"));
        if (blockers.Count > 0)
            return Rejected(state, blockers);
        if (attempt.ReservedItems.Any(id => !context.Supports.Any(s => s.ItemId == id && s.Quantity > 0 && s.ReservedBy == attempt.Id)))
            return Reject(state, "reservation", "실물 예약이 유효하지 않음", "원래 예약/실물 복구 또는 준비 취소");
        if (!TryDebit(context, context.Policy.FlowPerMonth * path.Definition.SupplyCost, out var debit))
            return Reject(state, "supply", "형성 개시 공급 부족", "실제 공급 확보");
        var next = state with
        {
            Attempt = attempt with
            {
                Phase = AttemptPhase.Started,
                Due = context.Month + (attempt.Kind == AttemptKind.RouteConversion ? context.Policy.ConversionMonths : 1)
            }
        };
        if (attempt.Kind == AttemptKind.RouteConversion)
            next = next with
            {
                Structure = Scale(next.Structure!, 10000 - context.Policy.ConversionLoss),
                AuxiliaryPath = "",
                Load = context.Policy.ConversionLoss
            };
        return Accept(next, attempt.Kind == AttemptKind.RouteConversion ? new(Layer: Layer(next, actor.Realm, context.Policy), GreatPerfection: false) : null,
            supply: debit, supports: attempt.ReservedItems.Select(id => new SupportDelta(id, SupportOperation.Consume, attempt.Id, 1)).ToArray(),
            notices: [new("개시", actor.Realm == Realm.GoldenCore && attempt.Kind != AttemptKind.RouteConversion ? "파단 전 개시, 아직 안전 취소 가능" : "형성/교정 공정 개시", attempt.Path)],
            flow: SpentFlow(debit));
    }

    private static CultivationResult Fracture(CultivationState state, CultivationActor actor, CultivationContext context)
    {
        var attempt = state.Attempt;
        if (actor.Realm != Realm.GoldenCore || attempt?.TargetRealm != Realm.NascentSoul || attempt.Phase != AttemptPhase.Started || attempt.Kind != AttemptKind.RealmBreakthrough)
            return Reject(state, "fracture", "파단 전 원영 형성 시도가 아님", "금단 완성·준비·개시 확인");
        if (context.Month < attempt.Due)
            return Reject(state, "time", "개시 공정 미완료", "개시 경계까지 진행");
        if (actor.Health < 50 || state.Damage > context.Policy.FailureDamage || actor.Mind < context.Policy.CompletionControl)
            return Reject(state, "fracture-condition", "파단 직전 성영 수용 부족", "파단 전 안전 취소·회복");
        return Accept(state with { Attempt = attempt with { Phase = AttemptPhase.Fractured, Due = context.Month + 1 } },
            notices: [new("금단 파단", "비가역 경계 확정, 이후 성영 실패/실제 포기는 사망", attempt.Path)]);
    }

    private static CultivationResult Resolve(CultivationState state, CultivationActor actor, CultivationContext context, int sample)
    {
        var attempt = state.Attempt;
        if (attempt is null || attempt.Phase is not (AttemptPhase.Started or AttemptPhase.Fractured))
            return Reject(state, "phase", "개시/파단된 시도가 아님", "현재 공정 확인");
        if (attempt.TargetRealm == Realm.NascentSoul && attempt.Kind == AttemptKind.RealmBreakthrough && attempt.Phase != AttemptPhase.Fractured)
            return Reject(state, "fracture", "금단 파단 확정 전", "명시적 파단 공정 진행");
        if (context.Month < attempt.Due)
            return Reject(state, "time", "형성 공정 미완료", "다음 사건 경계까지 진행");
        if (sample < attempt.Risk)
        {
            if (attempt.Phase == AttemptPhase.Fractured)
                return Die(state, CultivationDeathCause.CoreFracture, "파단 후 원영 형성 실패");
            var failed = state with
            {
                Attempt = attempt with
                {
                    Phase = AttemptPhase.Recovering,
                    RecoveryRemaining = context.Policy.StabilizationMonths,
                    Due = context.Month + 1
                },
                Damage = Math.Min(10000, state.Damage + context.Policy.FailureDamage),
                Structure = state.Structure is null ? null : Scale(state.Structure, 9000)
            };
            return Accept(failed, new(Layer: Layer(failed, actor.Realm, context.Policy), HealthChange: -context.Policy.FailureHealthCost, GreatPerfection: false),
                notices: [new("형성 실패", "손상·회복 필요", attempt.Path)]);
        }

        if (attempt.Kind == AttemptKind.RouteConversion)
        {
            var conversion = new PathConversion(state.ActivePath, attempt.Path, attempt.PreparedMonth, context.Month, context.Policy.ConversionLoss, "실제 교정/재구축");
            return Accept(state with { ActivePath = attempt.Path, Conversions = [..state.Conversions, conversion],
                Attempt = attempt with { Phase = AttemptPhase.Stabilizing, RecoveryRemaining = context.Policy.StabilizationMonths, Due = context.Month + 1 } },
                new(GreatPerfection: false), notices: [new("경로 교정", "현재 경로 전환·과거 형성 기록 보존", attempt.Path)]);
        }

        var formation = new CultivationFormation(attempt.TargetRealm, attempt.Path, attempt.FormationQuality,
            FormationFactors(attempt.TargetRealm, state, actor, attempt.FormationQuality, context.Policy), context.Month, context.Site.PlaceId,
            attempt.LawRevision, attempt.Knowledge, attempt.Parent, "실제 형성");
        var next = state with
        {
            ActivePath = attempt.Path,
            AuxiliaryPath = "",
            Structure = CultivationProfiles.Empty(attempt.TargetRealm),
            GrowthBasis = new(attempt.LawRevision, attempt.TargetWork, attempt.TargetRootWork, EntryTarget(state, context.Policy)),
            Formations = [..state.Formations, formation],
            Load = 0,
            Attempt = attempt with
            {
                Phase = AttemptPhase.Stabilizing,
                RecoveryRemaining = context.Policy.StabilizationMonths,
                Due = context.Month + 1
            },
            PendingTrial = attempt.TargetRealm == Realm.Tribulation ? new(1, false, context.Month + 1) : null
        };
        return Accept(next, new(attempt.TargetRealm, 1, false, LifespanFloorMonths: attempt.LifespanAward),
            notices: [new("형성", "새 고유 구조 형성·안착 필요, 이전 구조 효과는 한 번 변환", attempt.Path)]);
    }

    private static CultivationResult Cancel(CultivationState state, CultivationActor actor, CultivationContext context)
    {
        _ = actor;
        _ = context;
        var attempt = state.Attempt;
        if (attempt is null || attempt.Phase is AttemptPhase.Cancelled or AttemptPhase.Completed or AttemptPhase.Failed)
            return Reject(state, "phase", "취소할 진행 시도 없음", "현재 상태 확인");
        if (attempt.Phase == AttemptPhase.Fractured)
            return Die(state, CultivationDeathCause.CoreFracture, "파단 후 성영의 실제 포기");
        if (attempt.Phase is AttemptPhase.Stabilizing or AttemptPhase.Recovering)
            return Reject(state, "aftercare", "이미 확정된 형성/손상은 취소로 되돌릴 수 없음", "안착·회복 진행");
        var releases = attempt.Phase == AttemptPhase.Prepared
            ? attempt.ReservedItems.Select(id => new SupportDelta(id, SupportOperation.Release, attempt.Id, 1)).ToArray() : [];
        return Accept(state with { Attempt = attempt with { Phase = AttemptPhase.Cancelled } }, supports: releases,
            notices: [new("취소", "비가역 경계 전 공정 중단", attempt.Path)]);
    }
}
