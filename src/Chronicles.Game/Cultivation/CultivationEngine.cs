namespace Chronicles.Game.Cultivation;

/// <summary>Pure, deterministic transition authority. The caller commits the returned metadata and deltas together.</summary>
public static class CultivationEngine
{
    public static CultivationResult Execute(CultivationState state, CultivationActor actor, CultivationCommand command,
        CultivationContext context, int randomSample)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(context);
        if (randomSample is < 0 or >= 10000) return Reject(state, "rng", "난수 표본은 0..9999", "명시적 세계 stream 표본 사용");
        if (string.IsNullOrWhiteSpace(command.Id) || command.ExpectedRevision != state.Revision)
            return Reject(state, "revision", "중복 또는 오래된 명령", "현재 revision으로 새 명령 제출");
        if (actor.Status != LifeStatus.Alive || actor.BodyId != state.BodyId || actor.SoulId != state.SoulId)
            return Reject(state, "body", "현재 살아 있는 육신/혼과 불일치", "현재 육신 상태 사용");
        if (!ValidInput(state, actor, context)) return Reject(state, "invalid", "수행 입력 범위/구조 불일치", "저장/어댑터 불변식 확인");
        return command switch
        {
            PracticeCultivation practice => Practice(state, actor, practice, context),
            PrepareCultivation prepare => Prepare(state, actor, prepare, context),
            StartCultivation => Start(state, actor, context),
            FractureGoldenCore => Fracture(state, actor, context),
            ResolveCultivation => Resolve(state, actor, context, randomSample),
            CancelCultivation => Cancel(state, actor, context),
            StabilizeCultivation => Stabilize(state, actor, context),
            RecoverCultivation recover => Recover(state, actor, recover, context),
            DamageCultivation damage => Damage(state, actor, damage, context),
            ResolveMinorTribulation => Trial(state, actor, context, randomSample),
            TrainMartial martial => MartialTrain(state, actor, martial, context),
            BreakthroughMartial => MartialBreakthrough(state, actor, context, randomSample),
            ConfigureAuxiliaryCultivation auxiliary => Auxiliary(state, actor, auxiliary, context),
            _ => Reject(state, "command", "지원하지 않는 수행 명령", "형식화된 명령 사용")
        };
    }

    public static IReadOnlyList<CultivationBlocker> Preflight(CultivationState state, CultivationActor actor,
        string pathId, CultivationContext context, bool conversion = false)
    {
        var result = new List<CultivationBlocker>();
        var path = CultivationProfiles.Find(pathId);
        if (path is null) { result.Add(new("path", "알 수 없는 경로", "알려진 경로 선택")); return result; }
        var target = conversion ? actor.Realm : actor.Realm + 1;
        if (actor.Realm == Realm.HalfImmortal && !conversion)
            result.Add(new("ascension", "반선 완성은 비승이 아님", "실제 통로·세계 법칙·비승 명령 확인"));
        if (path.Definition.Realm != target) result.Add(new("path-realm", "목표 경지 공법이 아님", "목표 경지 지식 확보"));
        AddAccess(result, context.Site);
        var knowledge = PreparationKnowledge(state, actor, pathId, context, conversion);
        if (knowledge is null || !knowledge.Executable || knowledge.Understanding < context.Policy.MinimumUnderstanding)
            result.Add(new("knowledge", "실제 이해/암기 또는 현장 열람 부족", "해독·학습·접근권 확보"));
        if (knowledge is not null && knowledge.Error > 5000) result.Add(new("interpretation", "채택 해석에 큰 오류", "정정·실습 검증"));
        if (state.PendingTrial is not null) result.Add(new("trial", "실제 소경지 겁난 미종결", "겁난 대응"));
        if (ActiveAttempt(state)) result.Add(new("attempt", "진행 중인 형성/안착", "현재 공정 종결"));
        if (state.Damage > context.Policy.FailureDamage || actor.Health < 50)
            result.Add(new("damage", "구조/육신 손상", "회복 후 준비"));
        if (conversion)
        {
            if (actor.Realm == Realm.Mortal || state.ActivePath == pathId) result.Add(new("conversion", "유효한 다른 현재 경로가 아님", "현재 경지의 다른 경로 선택"));
            if (knowledge is not null && knowledge.Practice < context.Policy.MinimumUnderstanding)
                result.Add(new("practice", "전환 경로 실습 부족", "보조 공정으로 실제 실습"));
        }
        else if (actor.Realm == Realm.Mortal)
        {
            if (state.EntryWork < EntryTarget(state, context.Policy) || actor.Mind < context.Policy.MinimumControl || actor.InnateRoots.Maximum == 0)
                result.Add(new("sensing", "입문 토납·신식 감응 준비 부족", "감응 전 토납·육체/정신 단련"));
            if (QualifiedPreparationKnowledge(knowledge, context.Policy) && FormationQuality(state, actor, knowledge!, context) < context.Policy.MinimumFormation)
                result.Add(new("formation", "실제 형성 기반 부족", "구조·정탁·육신·정신 보완"));
        }
        else
        {
            if (!actor.GreatPerfection || !Complete(state, actor, context.Policy))
                result.Add(new("completion", "현재 고유 기반 전체 완성 부족", "남은 구조·다섯 영근 선택 보완"));
            if (actor.Circulation < context.Policy.CompletionControl || actor.Body < context.Policy.CompletionControl || actor.Mind < context.Policy.CompletionControl)
                result.Add(new("control", "완성 기반의 순환/육신/정신 수용 부족", "운기·육신·정신 훈련"));
        }
        AddPathConditions(result, path, actor, context);
        return result;
    }

    private static CultivationResult Practice(CultivationState state, CultivationActor actor, PracticeCultivation command, CultivationContext context)
    {
        if (command.Intensity is <= 0 or > 20000 || !command.Allocation.Nonnegative || command.Allocation.Total == 0)
            return Reject(state, "allocation", "오행 배분/강도 범위 오류", "양수 배분·강도 1..20000 사용");
        if (ActiveAttempt(state) || state.PendingTrial is not null) return Reject(state, "boundary", "돌파·안착·겁난 경계", "현재 공정 종결");
        if (context.Month <= state.LastTrainingMonth) return Reject(state, "time", "이미 처리한 수행 월", "다음 정수 월까지 진행");
        var profile = CultivationProfiles.Find(command.PathId);
        if (profile is null || profile.Definition.Realm != (actor.Realm == Realm.Mortal ? Realm.Qi : actor.Realm))
            return Reject(state, "path", "현재 경지와 경로 불일치", "현재 경지 경로 선택");
        if (state.ActivePath.Length > 0 && state.ActivePath != command.PathId)
            return Reject(state, "conversion", "주 경로 전환에는 재구축 필요", "준비→부분 정지→교정→검사→안착");
        var knowledge = Knowledge(context, actor.Realm == Realm.Mortal ? "entry" : command.PathId)
            ?? Knowledge(context, command.PathId);
        if (knowledge is null || !knowledge.Executable) return Reject(state, "knowledge", "배우지 않은 공법", "실제 학습·암기/열람");
        var access = new List<CultivationBlocker>(); AddAccess(access, context.Site);
        if (access.Count > 0) return Rejected(state, access);
        if (actor.Realm == Realm.Mortal)
        {
            // Deliberate circulation is not an entry prerequisite. Pre-sensing breathing trains the entry itself.
            var work = Math.Max(1, (long)(actor.Body + actor.Mind) / 2);
            var target = EntryTarget(state, context.Policy);
            return Accept(state with { EntryWork = Math.Min(target, state.EntryWork + work),
                GrowthBasis = state.GrowthBasis ?? new(context.LawRevision, 0, 0, target),
                EntryLastMonth = context.Month, LastTrainingMonth = context.Month }, notices: [new("감응 준비", "감응 전 토납·신식 준비", knowledge.Source)]);
        }
        var auxiliary = state.AuxiliaryPath.Length == 0 ? null : Knowledge(context, state.AuxiliaryPath);
        if (state.AuxiliaryPath.Length > 0 && (auxiliary is null || !auxiliary.Executable))
            return Reject(state, "auxiliary", "보조 공정 지식/열람 불가", "보조 해제 또는 접근 복구");

        var p = context.Policy;
        var costs = profile.Definition.SupplyCost + (auxiliary is null ? 0 : 1);
        var flow = p.FlowPerMonth * costs;
        var request = Elements.Allocate(flow * command.Intensity / 10000, command.Allocation);
        var fair = Elements.Map(context.Site.Available, value => value / Math.Max(1, context.Site.Claimants));
        var absorbCapacity = Elements.Allocate(flow * actor.Absorption / 5000 *
            (command.Focus == TrainingFocus.Absorption ? 12500 : 10000) / 10000, command.Allocation);
        absorbCapacity = Elements.Zip(absorbCapacity, actor.InnateRoots, (amount, root) => amount * root / 5000);
        absorbCapacity = Elements.Zip(absorbCapacity, state.RootDamage, (amount, damage) => amount * (10000 - damage) / 10000);
        var absorbed = Elements.Limit(Elements.Limit(request, fair), absorbCapacity);
        var before = state.Raw + state.Refined + state.Circulating;
        var raw = state.Raw + absorbed;
        var refined = state.Refined;
        var circulating = state.Circulating;
        long Capacity(int ability, TrainingFocus focus) => flow * ability / 5000 * actor.Health / 100 *
            (command.Focus == focus ? 12500 : 10000) / 10000;
        var toRefine = Take(raw, Capacity(actor.Refinement, TrainingFocus.Refinement)); raw -= toRefine;
        var purity = Math.Clamp(10000 - context.Site.Turbidity - knowledge.Error / 2, 0, 10000);
        var purified = Elements.Map(toRefine, v => v * purity / 10000); refined += purified;
        var purged = toRefine - purified;
        var toCirculate = Take(refined, Capacity(actor.Circulation, TrainingFocus.Circulation) * (10000 - state.Damage) / 10000);
        refined -= toCirculate; circulating += toCirculate;
        var assimilated = Take(circulating, Capacity(actor.Assimilation, TrainingFocus.Assimilation)); circulating -= assimilated;
        var maximumHeld = p.FlowPerMonth * p.ReserveLimitMonths;
        var rawKept = Elements.Map(raw, v => Math.Min(v, maximumHeld));
        var refinedKept = Elements.Map(refined, v => Math.Min(v, maximumHeld));
        var circulationKept = Elements.Map(circulating, v => Math.Min(v, maximumHeld));
        var overflow = raw - rawKept + refined - refinedKept + circulating - circulationKept;
        purged += overflow;
        var process = new ProcessFlow(absorbed, purified, toCirculate, assimilated, purged, before, rawKept + refinedKept + circulationKept);
        if (!process.Conserved) throw new InvalidOperationException("Elemental process conservation failed.");
        // Safely filtered turbidity is a material loss, not energy retained beyond structural control.
        var pressure = Math.Max(0, process.HeldAfter.Total - process.HeldBefore.Total + overflow.Total);
        var excess = (int)Math.Min(1000, pressure * 10000 / Math.Max(1, flow) / 20);
        var rootDamage = Elements.Zip(state.RootDamage, Elements.Allocate(excess * 5L, command.Allocation), (x, y) => Math.Min(10000, x + y));
        var next = state with
        {
            Raw = rawKept, Refined = refinedKept, Circulating = circulationKept, RootDamage = rootDamage,
            Damage = Math.Min(10000, state.Damage + excess), Load = (int)Math.Min(10000, pressure * 10000 / Math.Max(1, flow)),
            ActivePath = command.PathId, LastTrainingMonth = context.Month
        };
        var blockers = new List<CultivationBlocker>();
        if (absorbed.Total == 0 && before.Total == 0) blockers.Add(new("supply", "실제 공급/영근 감응 부족", "영맥·오행·자리·영근 손상 확인"));
        if (toCirculate.Total < purified.Total) blockers.Add(new("circulation", "정제량보다 순환 수용이 낮음", "흡수 조절·운기 교정·경맥 회복"));
        var progressLimit = ProgressLimit(actor, knowledge, p, blockers);
        if (actor.Realm == Realm.Qi)
        {
            var perRoot = RootTarget(state, p);
            var limit = Math.Min(perRoot, perRoot * progressLimit / 10000);
            next = next with { RootGrowth = Elements.Zip(next.RootGrowth, assimilated, (old, added) => Math.Max(old, Math.Min(limit, old + added))) };
        }
        var required = RealmTarget(state, p);
        var structureLimit = required * progressLimit / 10000;
        // Tribulation never crosses more than the very next unperformed encounter in one interval.
        if (actor.Realm == Realm.Tribulation) structureLimit = Math.Min(structureLimit, (required * actor.Layer + 7) / 8);
        var weights = command.Focus == TrainingFocus.Structure ? next.Structure!.Measures.Select((value, i) =>
            (int)Math.Max(1, 10000 - value * 10000 / Target(required, next.Structure.Measures.Count, i))).ToArray() : profile.GrowthWeights;
        next = next with { Structure = Grow(next.Structure!, assimilated.Total, weights, structureLimit, required, context.Site) };
        var layer = Layer(next, actor.Realm, p);
        var complete = actor.Realm == Realm.Tribulation ? Maturity(next, p) == 10000 : Complete(next, actor, p);
        if (actor.Realm == Realm.Tribulation && (layer > actor.Layer || (complete && !actor.GreatPerfection)))
        {
            next = next with { PendingTrial = new(Math.Min(9, actor.Layer + 1), actor.Layer == 9, context.Month + 1) };
            layer = actor.Layer; complete = false;
        }
        if (actor.Realm == Realm.Void && next.Structure is VoidStructure v && v.ClearedDefects > v.RebuiltChannels)
            next = next with { Load = Math.Max(next.Load, (int)Math.Min(10000, (v.ClearedDefects - v.RebuiltChannels) * 10000 / Math.Max(1, required / 4))) };
        return Accept(next, new(Layer: layer, GreatPerfection: complete, HealthChange: excess > 0 ? -1 : 0), absorbed,
            notices: [new("공정 실습", $"흡수 {absorbed.Total}→정제 {purified.Total}→순환 {toCirculate.Total}→체화 {assimilated.Total}", knowledge.WorkId)],
            blockers: blockers, flow: process);
    }

    private static CultivationResult Prepare(CultivationState state, CultivationActor actor, PrepareCultivation command, CultivationContext context)
    {
        var blockers = Preflight(state, actor, command.PathId, context, command.Conversion);
        if (blockers.Count > 0) return Rejected(state, blockers);
        var items = command.SupportItems ?? [];
        if (items.Distinct().Count() != items.Count) return Reject(state, "support", "중복 보조 물품", "실물 ID 한 번만 예약");
        var supports = items.Select(id => context.Supports.FirstOrDefault(s => s.ItemId == id)).ToArray();
        if (supports.Any(s => s is null || s.Quantity <= 0 || s.ReservedBy.Length != 0))
            return Reject(state, "reservation", "보조 실물이 없거나 이미 예약됨", "소유·사용 가능한 실제 물품 선택");
        var profile = CultivationProfiles.Find(command.PathId)!;
        var knowledge = PreparationKnowledge(state, actor, command.PathId, context, command.Conversion)!;
        var quality = FormationQuality(state, actor, knowledge, context);
        if (quality < context.Policy.MinimumFormation) return Reject(state, "formation", "실제 형성 기반 부족", "구조·정탁·육신·정신 보완");
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
        if (attempt?.Phase != AttemptPhase.Prepared) return Reject(state, "phase", "준비된 시도가 아님", "자격 검사·준비부터 진행");
        var path = CultivationProfiles.Find(attempt.Path)!;
        var blockers = new List<CultivationBlocker>(); AddAccess(blockers, context.Site); AddPathConditions(blockers, path, actor, context);
        if (actor.Health < 50 || state.Damage > context.Policy.FailureDamage)
            blockers.Add(new("damage", "준비 이후 육신/구조 조건 악화", "개시 전 회복 또는 준비 취소"));
        if (!attempt.Knowledge.Memorized && !context.Knowledge.Any(k => k.WorkId == attempt.Knowledge.WorkId &&
            k.EditionId == attempt.Knowledge.EditionId && k.Revision == attempt.Knowledge.Revision && k.CanConsult))
            blockers.Add(new("consult", "채택 판본 현장 열람 불가", "동일 판본 접근 복구 또는 준비 취소"));
        if (blockers.Count > 0) return Rejected(state, blockers);
        if (attempt.ReservedItems.Any(id => !context.Supports.Any(s => s.ItemId == id && s.Quantity > 0 && s.ReservedBy == attempt.Id)))
            return Reject(state, "reservation", "실물 예약이 유효하지 않음", "원래 예약/실물 복구 또는 준비 취소");
        if (!TryDebit(context, context.Policy.FlowPerMonth * path.Definition.SupplyCost, out var debit))
            return Reject(state, "supply", "형성 개시 공급 부족", "실제 공급 확보");
        var next = state with { Attempt = attempt with { Phase = AttemptPhase.Started,
            Due = context.Month + (attempt.Kind == AttemptKind.RouteConversion ? context.Policy.ConversionMonths : 1) } };
        if (attempt.Kind == AttemptKind.RouteConversion)
            next = next with { Structure = Scale(next.Structure!, 10000 - context.Policy.ConversionLoss), AuxiliaryPath = "", Load = context.Policy.ConversionLoss };
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
        if (context.Month < attempt.Due) return Reject(state, "time", "개시 공정 미완료", "개시 경계까지 진행");
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
        if (context.Month < attempt.Due) return Reject(state, "time", "형성 공정 미완료", "다음 사건 경계까지 진행");
        if (sample < attempt.Risk)
        {
            if (attempt.Phase == AttemptPhase.Fractured) return Die(state, CultivationDeathCause.CoreFracture, "파단 후 원영 형성 실패");
            var failed = state with { Attempt = attempt with { Phase = AttemptPhase.Recovering, RecoveryRemaining = context.Policy.StabilizationMonths, Due = context.Month + 1 },
                Damage = Math.Min(10000, state.Damage + context.Policy.FailureDamage), Structure = state.Structure is null ? null : Scale(state.Structure, 9000) };
            return Accept(failed, new(Layer: Layer(failed, actor.Realm, context.Policy), HealthChange: -context.Policy.FailureHealthCost, GreatPerfection: false),
                notices: [new("형성 실패", "손상·회복 필요", attempt.Path)]);
        }
        if (attempt.Kind == AttemptKind.RouteConversion)
        {
            var conversion = new PathConversion(state.ActivePath, attempt.Path, attempt.PreparedMonth, context.Month, context.Policy.ConversionLoss, "실제 교정/재구축");
            return Accept(state with { ActivePath = attempt.Path, Conversions = [.. state.Conversions, conversion],
                Attempt = attempt with { Phase = AttemptPhase.Stabilizing, RecoveryRemaining = context.Policy.StabilizationMonths, Due = context.Month + 1 } },
                new(GreatPerfection: false), notices: [new("경로 교정", "현재 경로 전환·과거 형성 기록 보존", attempt.Path)]);
        }
        var formation = new CultivationFormation(attempt.TargetRealm, attempt.Path, attempt.FormationQuality,
            FormationFactors(attempt.TargetRealm, state, actor, attempt.FormationQuality, context.Policy), context.Month, context.Site.PlaceId,
            attempt.LawRevision, attempt.Knowledge, attempt.Parent, "실제 형성");
        var next = state with
        {
            ActivePath = attempt.Path, AuxiliaryPath = "", Structure = CultivationProfiles.Empty(attempt.TargetRealm),
            GrowthBasis = new(attempt.LawRevision, attempt.TargetWork, attempt.TargetRootWork, EntryTarget(state, context.Policy)),
            Formations = [.. state.Formations, formation], Load = 0,
            Attempt = attempt with { Phase = AttemptPhase.Stabilizing, RecoveryRemaining = context.Policy.StabilizationMonths, Due = context.Month + 1 },
            PendingTrial = attempt.TargetRealm == Realm.Tribulation ? new(1, false, context.Month + 1) : null
        };
        return Accept(next, new(attempt.TargetRealm, 1, false, LifespanFloorMonths: attempt.LifespanAward),
            notices: [new("형성", "새 고유 구조 형성·안착 필요, 이전 구조 효과는 한 번 변환", attempt.Path)]);
    }

    private static CultivationResult Cancel(CultivationState state, CultivationActor actor, CultivationContext context)
    {
        _ = actor; _ = context;
        var attempt = state.Attempt;
        if (attempt is null || attempt.Phase is AttemptPhase.Cancelled or AttemptPhase.Completed or AttemptPhase.Failed)
            return Reject(state, "phase", "취소할 진행 시도 없음", "현재 상태 확인");
        if (attempt.Phase == AttemptPhase.Fractured) return Die(state, CultivationDeathCause.CoreFracture, "파단 후 성영의 실제 포기");
        if (attempt.Phase is AttemptPhase.Stabilizing or AttemptPhase.Recovering)
            return Reject(state, "aftercare", "이미 확정된 형성/손상은 취소로 되돌릴 수 없음", "안착·회복 진행");
        var releases = attempt.Phase == AttemptPhase.Prepared
            ? attempt.ReservedItems.Select(id => new SupportDelta(id, SupportOperation.Release, attempt.Id, 1)).ToArray() : [];
        return Accept(state with { Attempt = attempt with { Phase = AttemptPhase.Cancelled } }, supports: releases,
            notices: [new("취소", "비가역 경계 전 공정 중단", attempt.Path)]);
    }

    private static CultivationResult Stabilize(CultivationState state, CultivationActor actor, CultivationContext context)
    {
        var attempt = state.Attempt;
        if (attempt?.Phase is not (AttemptPhase.Stabilizing or AttemptPhase.Recovering))
            return Reject(state, "phase", "안착/회복 공정 없음", "현재 상태 확인");
        if (context.Month < attempt.Due || context.Month <= state.LastTrainingMonth)
            return Reject(state, "time", "안착 월 경계 미도달", "다음 정수 월까지 진행");
        if (!TryDebit(context, context.Policy.FlowPerMonth, out var debit)) return Reject(state, "supply", "유지·안착 공급 부족", "실제 공급 확보");
        var remaining = Math.Max(0, attempt.RecoveryRemaining - 1);
        var phase = remaining == 0 ? AttemptPhase.Completed : attempt.Phase;
        return Accept(state with { Attempt = attempt with { Phase = phase, RecoveryRemaining = remaining, Due = context.Month + 1 },
            LastTrainingMonth = context.Month, Damage = Math.Max(0, state.Damage - context.Policy.RecoveryPerMonth), Load = 0 },
            new(HealthChange: actor.Health < 100 ? 1 : 0), debit,
            notices: [new("안착", remaining == 0 ? "안착/회복 경계 완료" : "유지 순환·손상 회복", attempt.Path)], flow: SpentFlow(debit));
    }

    private static CultivationResult Recover(CultivationState state, CultivationActor actor, RecoverCultivation command, CultivationContext context)
    {
        if (ActiveAttempt(state)) return Reject(state, "attempt", "진행 중 공정은 일반 휴식으로 전환 불가", "전용 안착/회복 또는 안전 취소");
        if (context.Month <= state.LastTrainingMonth) return Reject(state, "time", "이미 처리한 수행 월", "다음 월까지 진행");
        Elements debit = default;
        var structuralRecovery = state.Structure is not null && (state.Damage > 0 || state.RootDamage.Total > 0);
        if (structuralRecovery && !TryDebit(context, context.Policy.FlowPerMonth, out debit)) return Reject(state, "supply", "구조 회복 공급 부족", "실제 공급 확보");
        var treatment = command.MedicalTreatment ? context.Supports.FirstOrDefault(s => s.Kind == "pill" && s.Quantity > 0 && s.ReservedBy.Length == 0) : null;
        if (command.MedicalTreatment && treatment is null) return Reject(state, "medicine", "실제 치료 물품 없음", "물품 확보 또는 자연 회복");
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
        if (command.Severity is <= 0 or > 10000) return Reject(state, "damage", "손상은 1..10000", "실제 사건 손상 사용");
        if (state.Attempt?.Phase == AttemptPhase.Fractured && command.Severity >= 10000 - state.Damage)
            return Die(state, CultivationDeathCause.CoreFracture, "비가역 성영 중 구조 붕괴");
        var damage = Math.Min(10000, state.Damage + command.Severity);
        var next = state with { Damage = damage, Structure = state.Structure is null ? null : Scale(state.Structure, 10000 - command.Severity),
            RootDamage = command.Cause is CultivationDamageCause.OverAbsorption or CultivationDamageCause.WrongCirculation
                ? Elements.Map(state.RootDamage, value => Math.Min(10000, value + command.Severity)) : state.RootDamage,
            RootGrowth = actor.Realm == Realm.Qi ? Elements.Map(state.RootGrowth, value => value * (10000 - command.Severity) / 10000) : state.RootGrowth };
        if (damage == 10000 && actor.Realm >= Realm.Void)
            return Die(next, command.Cause == CultivationDamageCause.BondConflict && actor.Realm >= Realm.Integration
                ? CultivationDeathCause.BondCollapse : CultivationDeathCause.StructureCollapse, command.Cause.ToString());
        return Accept(next, new(Layer: Math.Max(1, Layer(next, actor.Realm, context.Policy)), GreatPerfection: false,
            HealthChange: command.Cause == CultivationDamageCause.Neglect ? 0 : -(command.Severity / 500)),
            notices: [new("퇴행", "현재 구조 손상·형성 품질/과거 이력 보존", command.Cause.ToString())]);
    }

    private static CultivationResult Trial(CultivationState state, CultivationActor actor, CultivationContext context, int sample)
    {
        var trial = state.PendingTrial;
        if (actor.Realm != Realm.Tribulation || trial is null || state.Structure is not TribulationStructure structure)
            return Reject(state, "trial", "미종결 실제 겁난 없음", "소경지 경계까지 수행");
        if (context.Month < trial.Due) return Reject(state, "time", "겁난 경계 미도달", "다음 사건까지 진행");
        if (state.Attempt?.Phase is AttemptPhase.Prepared or AttemptPhase.Started or AttemptPhase.Fractured)
            return Reject(state, "attempt", "다른 비가역 형성 공정 진행 중", "공정 경계 확인");
        var bit = trial.Completion ? 9 : trial.TargetLayer - 1;
        if ((structure.CompletedTrials & (1 << bit)) != 0) return Reject(state, "trial-replay", "같은 소경지 겁난 재실행", "저장/명령 revision 확인");
        var risk = Math.Clamp(context.Policy.TrialRisk + state.Damage / 5 + (100 - actor.Health) * 10, 0, 10000);
        if (sample < risk) return Die(state, CultivationDeathCause.Tribulation, "소경지 실제 겁난 실패");
        if (actor.Health <= context.Policy.TrialHealthCost)
        {
            var passed = state with { PendingTrial = null, Structure = structure with { CompletedTrials = structure.CompletedTrials | (1 << bit) } };
            return Die(passed, CultivationDeathCause.PostTribulationCollapse, "겁후 육신 회복 불가") with
            { Actor = new(Layer: trial.TargetLayer, GreatPerfection: trial.Completion, Death: CultivationDeathCause.PostTribulationCollapse) };
        }
        return Accept(state with { PendingTrial = null, Structure = structure with { CompletedTrials = structure.CompletedTrials | (1 << bit) },
            Damage = Math.Min(10000, state.Damage + context.Policy.RecoveryPerMonth), Load = 0 },
            new(Layer: trial.TargetLayer, GreatPerfection: trial.Completion, HealthChange: -context.Policy.TrialHealthCost),
            notices: [new("겁난 통과", trial.Completion ? "대원만 완성 겁난 통과" : $"{trial.TargetLayer}층 실제 겁난 통과", "소경지 실제 사건")]);
    }

    private static CultivationResult MartialTrain(CultivationState state, CultivationActor actor, TrainMartial command, CultivationContext context)
    {
        if (command.Intensity is <= 0 or > 20000 || context.Month <= state.Martial.LastMonth)
            return Reject(state, "martial-time", "무예 강도/월 경계 오류", "다음 월·유효 강도로 단련");
        if (ActiveAttempt(state) || state.PendingTrial is not null || context.Month <= state.LastTrainingMonth)
            return Reject(state, "capacity", "동일 월의 수행/형성 활동 용량 사용 중", "현재 공정 종결 또는 다음 월");
        if (actor.MartialStage == 10) return Reject(state, "martial-complete", "무인 독립 10경지 완성", "실전·회복·수선 등 다른 행동");
        // No root, realm, spiritual supply, bloodline or spiritual quality enters martial training.
        var work = (long)(actor.Body + actor.Mind) / 2 * command.Intensity / 10000 * actor.Health / 100;
        var injury = Math.Clamp(state.Martial.Injury + Math.Max(0, command.Intensity - 10000) / 10 - context.Policy.RecoveryPerMonth / 10, 0, 10000);
        var required = context.Policy.MartialMonthsPerStage * (actor.MartialStage + 1L) * context.Policy.FlowPerMonth;
        return Accept(state with { Martial = new(Math.Min(required, state.Martial.Work + work * (10000 - injury) / 10000), injury, context.Month),
            LastTrainingMonth = context.Month }, new(HealthChange: injury > state.Martial.Injury ? -1 : 0),
            notices: [new("무예 단련", "영근 비사용·독립 단계 단련", "실제 육신·정신 훈련")]);
    }

    private static CultivationResult MartialBreakthrough(CultivationState state, CultivationActor actor, CultivationContext context, int sample)
    {
        if (ActiveAttempt(state) || state.PendingTrial is not null || actor.MartialStage >= 10)
            return Reject(state, "martial", "현재 무예 돌파 불가", "현재 공정/단계 확인");
        var required = context.Policy.MartialMonthsPerStage * (actor.MartialStage + 1L) * context.Policy.FlowPerMonth;
        if (state.Martial.Work < required || actor.Health < 50 || state.Martial.Injury > context.Policy.FailureDamage)
            return Reject(state, "martial-foundation", "단련·육신·부상 조건 부족", "실제 단련·회복");
        if (sample < context.Policy.BaseRisk + state.Martial.Injury / 5)
            return Accept(state with { Martial = state.Martial with { Work = state.Martial.Work * 9 / 10,
                Injury = Math.Min(10000, state.Martial.Injury + context.Policy.FailureDamage) } }, new(HealthChange: -context.Policy.FailureHealthCost),
                notices: [new("무예 돌파 실패", "부상·재단련 필요", "실제 단련 판정")]);
        var stage = actor.MartialStage + 1;
        var years = Math.Min(context.Policy.MartialMaximumYears, 80 + (context.Policy.MartialMaximumYears - 80) * stage / 10);
        return Accept(state with { Martial = state.Martial with { Work = 0 } }, new(MartialStage: stage, LifespanFloorMonths: years * 12L),
            notices: [new("무예 돌파", $"무인 {stage}/10·기본수명 상한 {context.Policy.MartialMaximumYears}년, 수선 경지/출산 자격과 별도", "실제 단련 판정")]);
    }

    private static CultivationResult Auxiliary(CultivationState state, CultivationActor actor, ConfigureAuxiliaryCultivation command, CultivationContext context)
    {
        if (ActiveAttempt(state) || state.PendingTrial is not null) return Reject(state, "boundary", "현재 공정 중 보조 전환 불가", "공정 종결");
        if (command.PathId.Length == 0) return Accept(state with { AuxiliaryPath = "" });
        var profile = CultivationProfiles.Find(command.PathId); var knowledge = Knowledge(context, command.PathId);
        if (profile is null || profile.Definition.Realm != actor.Realm || command.PathId == state.ActivePath ||
            knowledge is null || !knowledge.Executable || knowledge.Understanding < context.Policy.MinimumUnderstanding)
            return Reject(state, "compatibility", "호환되는 학습된 보조 공정이 아님", "현재 경지·다른 경로 지식 확인");
        return Accept(state with { AuxiliaryPath = command.PathId }, notices: [new("보조 공정", "실제 공급/부하를 더 지불해 실습, 배타적 주 구조 효과 합산 없음", command.PathId)]);
    }

    internal static bool Complete(CultivationState state, CultivationActor actor, CultivationPolicy policy)
    {
        if (state.Structure is null) return false;
        var complete = Maturity(state, policy) == 10000;
        if (actor.Realm == Realm.Qi) complete &= state.RootGrowth.Minimum >= RootTarget(state, policy);
        if (actor.Realm == Realm.Tribulation) complete &= state.Structure is TribulationStructure { CompletedTrials: 1023 };
        return complete;
    }

    internal static long RealmTarget(CultivationState state, CultivationPolicy policy) =>
        state.GrowthBasis is { RealmWork: > 0 } basis ? basis.RealmWork : policy.RequiredWork(state.Structure?.Realm ?? Realm.Mortal);
    internal static long RootTarget(CultivationState state, CultivationPolicy policy) =>
        state.GrowthBasis is { RootWork: > 0 } basis ? basis.RootWork : policy.RequiredWork(Realm.Qi) / 5;
    internal static long EntryTarget(CultivationState state, CultivationPolicy policy) =>
        state.GrowthBasis is { EntryWork: > 0 } basis ? basis.EntryWork : policy.RequiredWork(Realm.Mortal);
    internal static int Maturity(CultivationState state, CultivationPolicy policy)
    {
        if (state.Structure is null) return 0;
        var required = RealmTarget(state, policy);
        var values = state.Structure.Measures;
        return (int)Math.Clamp(Enumerable.Range(0, values.Count).Min(i => values[i] * 10000 / Target(required, values.Count, i)), 0, 10000);
    }

    internal static bool ActiveAttempt(CultivationState state) => state.Attempt is { Phase: not (AttemptPhase.Cancelled or AttemptPhase.Completed or AttemptPhase.Failed) };
    // Entry adds a sensing source without replacing an independently learned Qi source. Both candidates
    // share every physical formation term; only their actual understanding/error evidence differs.
    // Rank the exact integer formation result (including clamping); an equal result keeps the explicit Qi work.
    // No qualified candidate retains the prior refusal evidence, rather than inventing usable knowledge.
    internal static KnowledgeEvidence? PreparationKnowledge(CultivationState state, CultivationActor actor, string path,
        CultivationContext context, bool conversion = false)
    {
        var method = Knowledge(context, path);
        if (actor.Realm != Realm.Mortal || conversion) return method;
        var entry = Knowledge(context, "entry");
        var methodEligible = QualifiedPreparationKnowledge(method, context.Policy);
        var entryEligible = QualifiedPreparationKnowledge(entry, context.Policy);
        if (!methodEligible) return entryEligible ? entry : entry ?? method;
        if (!entryEligible) return method;
        return FormationQuality(state, actor, entry!, context) > FormationQuality(state, actor, method!, context) ? entry : method;
    }
    private static bool QualifiedPreparationKnowledge(KnowledgeEvidence? knowledge, CultivationPolicy policy) =>
        knowledge is { Executable: true } && knowledge.Understanding >= policy.MinimumUnderstanding && knowledge.Error <= 5000;
    internal static KnowledgeEvidence? Knowledge(CultivationContext context, string path) => context.Knowledge.Where(k => k.WorkId == path)
        .OrderByDescending(k => k.Revision).ThenBy(k => k.EditionId, StringComparer.Ordinal).FirstOrDefault();

    private static int Layer(CultivationState state, Realm realm, CultivationPolicy policy)
    {
        if (realm == Realm.Mortal || state.Structure is null) return 1;
        if (realm == Realm.Qi) return (int)Math.Clamp(1 + state.RootGrowth.Maximum * 8 / Math.Max(1, RootTarget(state, policy)), 1, 9);
        var values = state.Structure.Measures; var required = RealmTarget(state, policy); var layer = 1;
        for (var next = 2; next <= 9; next++)
            if (Enumerable.Range(0, values.Count).All(i => values[i] >= (Target(required, values.Count, i) * (next - 1) + 7) / 8)) layer = next;
        return layer;
    }

    private static long Target(long required, int count, int index) => required / count + (index < required % count ? 1 : 0);
    private static RealmStructure Grow(RealmStructure structure, long work, IReadOnlyList<int> weights, long limit, long required, CultivationSite site)
    {
        var values = structure.Measures.ToArray();
        var caps = Enumerable.Range(0, values.Length).Select(i => (Target(required, values.Length, i) * limit + required - 1) / required).ToArray();
        // Upper structures demand their real preceding process, not generic experience renamed.
        if (structure is SpiritStructure && values[0] < Target(required, values.Length, 0) / 2) caps[1] = 0;
        if (structure is VoidStructure && values[0] == 0) caps[1] = 0;
        if (structure is IntegrationStructure && values[0] + values[1] == 0) caps[2] = caps[3] = 0;
        if (structure is HalfImmortalStructure)
        {
            if (!site.ObservedRealmConditions) caps[1] = values[1];
            if (!site.RealPassageAccessible) caps[2] = values[2];
        }
        var active = Enumerable.Range(0, values.Length).Where(i => values[i] < caps[i]).ToArray();
        // A single interval spends each unit once. Excess at a completed field can service remaining fields.
        while (work > 0 && active.Length > 0)
        {
            var weight = active.Sum(i => weights[i]); var before = work;
            foreach (var i in active)
            {
                var added = Math.Min(caps[i] - values[i], Math.Max(1, before * weights[i] / weight));
                added = Math.Min(added, work); values[i] += added; work -= added;
            }
            active = active.Where(i => values[i] < caps[i]).ToArray();
        }
        return structure.WithMeasures(values);
    }
    private static RealmStructure Scale(RealmStructure structure, int amount) => structure.WithMeasures(structure.Measures.Select(v => v * amount / 10000).ToArray());
    private static Elements Take(Elements held, long capacity) => held.Total == 0 || capacity <= 0 ? default : Elements.Allocate(Math.Min(capacity, held.Total), held);
    private static int ProgressLimit(CultivationActor actor, KnowledgeEvidence knowledge, CultivationPolicy policy, List<CultivationBlocker> blockers)
    {
        if (actor.Circulation < policy.MinimumControl)
        { blockers.Add(new("gate-3-4", "3→4층 운기/경맥 병목", "순환 제어·경맥 실습")); return 3749; }
        if (actor.Circulation < policy.UpperControl || actor.Body < policy.UpperControl || actor.Mind < policy.UpperControl)
        { blockers.Add(new("gate-6-7", "6→7층 육신·정신·순환 병목", "육신/정신 수용·운기 교정")); return 7499; }
        if (actor.Circulation < policy.CompletionControl || actor.Body < policy.CompletionControl || actor.Mind < policy.CompletionControl || knowledge.Understanding < policy.MinimumUnderstanding)
        { blockers.Add(new("gate-completion", "대원만 완성 조건 부족", "완성 기반·지식·정신 수용 보완")); return 9999; }
        return 10000;
    }
    private static void AddAccess(List<CultivationBlocker> blockers, CultivationSite site)
    {
        if (!site.HasUseRight) blockers.Add(new("right", "현재 장소 수행 사용권 없음", "사용권 협상·공개 자리·이주"));
        if (!site.HasReservedSeat && site.OccupiedSeats >= site.SeatCapacity) blockers.Add(new("seat", "실제 수행 자리 만석", "자리 예약·점유권 협상·이주"));
    }
    private static void AddPathConditions(List<CultivationBlocker> blockers, CultivationPathProfile profile, CultivationActor actor, CultivationContext context)
    {
        if (profile.Definition.Realm == Realm.Foundation && profile.Variant == 1 &&
            (!context.Site.FormationOperational || context.Site.FormationQuality < context.Policy.MinimumControl))
            blockers.Add(new("formation-site", "지도 기반의 실제 진법/연결 부족", "진법 복구·유지 또는 경로 변경"));
        if (profile.Definition.Realm == Realm.Foundation && profile.Variant == 2 && !context.Site.CelestialWindow)
            blockers.Add(new("window", "천도 형성의 실제 시기 조건 부족", "관측한 현상/시기 대기"));
        if (profile.Definition.Realm == Realm.Foundation && profile.Variant == 0 && actor.Circulation < context.Policy.MinimumControl)
            blockers.Add(new("self-control", "인도 기반의 자기 제어 부족", "자기 교정·순환 훈련"));
    }
    private static bool TryDebit(CultivationContext context, long amount, out Elements debit)
    {
        var blockers = new List<CultivationBlocker>(); AddAccess(blockers, context.Site);
        var fair = Elements.Map(context.Site.Available, v => v / Math.Max(1, context.Site.Claimants));
        debit = Take(fair, amount);
        return blockers.Count == 0 && debit.Total == amount;
    }
    private static int FormationQuality(CultivationState state, CultivationActor actor, KnowledgeEvidence knowledge, CultivationContext context)
    {
        var harmony = Math.Clamp(10000 - (state.RootGrowth.Maximum - state.RootGrowth.Minimum) * 10000 / Math.Max(1, RootTarget(state, context.Policy)), 0, 10000);
        var parentQuality = state.Formations.Count == 0 ? (int)(actor.InnateRoots.Total / 5) : state.Formations[^1].Quality;
        return (int)Math.Clamp(((long)parentQuality + actor.Circulation + actor.Body + actor.Mind + knowledge.Understanding + harmony) / 6 - state.Damage / 2 - knowledge.Error / 2, 0, 10000);
    }
    private static ParentStructureEvidence ParentEvidence(CultivationState state, CultivationActor actor, CultivationPolicy policy) =>
        new(actor.Realm, state.Formations.Count == 0 ? 0 : state.Formations[^1].Quality, state.Structure is null ? 0 : Maturity(state, policy), state.Damage, state.ActivePath);
    private static FormationFactor[] FormationFactors(Realm realm, CultivationState state, CultivationActor actor, int quality, CultivationPolicy policy)
    {
        _ = state; _ = policy;
        return CultivationProfiles.FormationKeys(realm).Select((key, i) => new FormationFactor(key,
            Math.Clamp((quality * 3 + (i % 3 == 0 ? actor.Body : i % 3 == 1 ? actor.Circulation : actor.Mind)) / 4, 0, 10000))).ToArray();
    }
    private static bool ValidInput(CultivationState state, CultivationActor actor, CultivationContext context) =>
        actor.Realm is >= Realm.Mortal and <= Realm.HalfImmortal && actor.Layer is >= 1 and <= 9 && actor.Health is >= 0 and <= 100 && actor.MartialStage is >= 0 and <= 10 &&
        actor.InnateRoots.Nonnegative && actor.InnateRoots.Maximum <= 10000 &&
        new[] { actor.Absorption, actor.Refinement, actor.Circulation, actor.Assimilation, actor.Body, actor.Mind }.All(v => v is >= 0 and <= 10000) &&
        (actor.Realm == Realm.Mortal ? state.Structure is null : state.Structure?.Realm == actor.Realm) &&
        (actor.Realm == Realm.Mortal || state.GrowthBasis is { RealmWork: > 0, RootWork: > 0 }) &&
        state.RootGrowth.Nonnegative && state.RootDamage.Nonnegative && state.RootDamage.Maximum <= 10000 &&
        state.Raw.Nonnegative && state.Refined.Nonnegative && state.Circulating.Nonnegative && state.Damage is >= 0 and <= 10000 &&
        context.Month >= 0 && context.Site.Available.Nonnegative && context.Site.Claimants > 0 && context.Site.Turbidity is >= 0 and <= 10000 &&
        ValidPolicy(context.Policy) &&
        context.Knowledge.All(k => k.Revision >= 0 && k.Understanding is >= 0 and <= 10000 && k.Practice is >= 0 and <= 10000 && k.Error is >= 0 and <= 10000);
    private static bool ValidPolicy(CultivationPolicy policy) => policy.Revision > 0 && policy.FlowPerMonth is > 0 and <= 1000000 &&
        policy.EntryMonths is > 0 and <= 100000000 && policy.QiMonths is >= 1 and <= 100000000 && policy.FoundationMonths is >= 1 and <= 100000000 &&
        policy.UpperDurationPercent is >= 100 and <= 1000 && policy.StabilizationMonths is >= 1 and <= 1200 && policy.ConversionMonths is >= 1 and <= 1200 &&
        policy.MartialMonthsPerStage is > 0 and <= 100000000 && policy.MartialMaximumYears is >= 80 and <= 600 && policy.ReserveLimitMonths is >= 0 and <= 1200 &&
        policy.MinimumUnderstanding is > 0 and <= 10000 && policy.MinimumFormation is >= 0 and <= 10000 &&
        policy.MinimumControl is >= 0 and <= 10000 && policy.UpperControl >= policy.MinimumControl && policy.CompletionControl >= policy.UpperControl && policy.CompletionControl <= 10000 &&
        policy.BaseRisk is >= 0 and <= 10000 && policy.TrialRisk is >= 0 and <= 10000 && policy.FailureDamage is >= 0 and <= 10000 &&
        policy.RecoveryPerMonth is >= 0 and <= 10000 && policy.ConversionLoss is >= 0 and <= 10000 && policy.TrialHealthCost is >= 0 and <= 100 && policy.FailureHealthCost is >= 0 and <= 100 &&
        policy.UpperLifespanPercent is >= 100 and <= 1000 &&
        policy.QiLifespanMonths > 0 && policy.FoundationLifespanMonths > 0 && policy.GoldenCoreLifespanMonths > 0 && policy.HalfImmortalLifespanMonths > 0;
    private static ProcessFlow SpentFlow(Elements spent) => new(spent, spent, spent, spent, default, default, default);
    private static CultivationResult Die(CultivationState state, CultivationDeathCause cause, string detail) =>
        Accept(state with { Attempt = state.Attempt is null ? null : state.Attempt with { Phase = AttemptPhase.Failed }, PendingTrial = null },
            new(Death: cause), notices: [new("사망 원인", detail, "원인 직전 현재 경지·층·대원만은 호출자가 보존")]);
    private static CultivationResult Accept(CultivationState state, ActorCultivationDelta? actor = null, Elements supply = default,
        IReadOnlyList<SupportDelta>? supports = null, IReadOnlyList<CultivationNotice>? notices = null,
        IReadOnlyList<CultivationBlocker>? blockers = null, ProcessFlow? flow = null) =>
        new(true, state with { Revision = state.Revision + 1 }, actor ?? new(), supply, supports ?? [], notices ?? [], blockers ?? [], flow);
    private static CultivationResult Reject(CultivationState state, string code, string detail, string remedy) => Rejected(state, [new(code, detail, remedy)]);
    private static CultivationResult Rejected(CultivationState state, IReadOnlyList<CultivationBlocker> blockers) => new(false, state, new(), default, [], [], blockers);
}
