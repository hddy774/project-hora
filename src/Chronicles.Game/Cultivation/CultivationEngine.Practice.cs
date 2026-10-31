namespace Chronicles.Game.Cultivation;

public static partial class CultivationEngine
{
    private static CultivationResult Practice(CultivationState state, CultivationActor actor, PracticeCultivation command, CultivationContext context)
    {
        if (command.Intensity is <= 0 or > 20000 || !command.Allocation.Nonnegative || command.Allocation.Total == 0)
            return Reject(state, "allocation", "오행 배분/강도 범위 오류", "양수 배분·강도 1..20000 사용");
        if (ActiveAttempt(state) || state.PendingTrial is not null)
            return Reject(state, "boundary", "돌파·안착·겁난 경계", "현재 공정 종결");
        if (context.Month <= state.LastTrainingMonth)
            return Reject(state, "time", "이미 처리한 수행 월", "다음 정수 월까지 진행");
        var profile = CultivationProfiles.Find(command.PathId);
        if (profile is null || profile.Definition.Realm != (actor.Realm == Realm.Mortal ? Realm.Qi : actor.Realm))
            return Reject(state, "path", "현재 경지와 경로 불일치", "현재 경지 경로 선택");
        if (state.ActivePath.Length > 0 && state.ActivePath != command.PathId)
            return Reject(state, "conversion", "주 경로 전환에는 재구축 필요", "준비→부분 정지→교정→검사→안착");
        var knowledge = Knowledge(context, actor.Realm == Realm.Mortal ? "entry" : command.PathId)
            ?? Knowledge(context, command.PathId);
        if (knowledge is null || !knowledge.Executable)
            return Reject(state, "knowledge", "배우지 않은 공법", "실제 학습·암기/열람");
        var access = new List<CultivationBlocker>();
        AddAccess(access, context.Site);
        if (access.Count > 0)
            return Rejected(state, access);
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
        var toRefine = Take(raw, Capacity(actor.Refinement, TrainingFocus.Refinement));
        raw -= toRefine;
        var purity = Math.Clamp(10000 - context.Site.Turbidity - knowledge.Error / 2, 0, 10000);
        var purified = Elements.Map(toRefine, v => v * purity / 10000);
        refined += purified;
        var purged = toRefine - purified;
        var toCirculate = Take(refined, Capacity(actor.Circulation, TrainingFocus.Circulation) * (10000 - state.Damage) / 10000);
        refined -= toCirculate;
        circulating += toCirculate;
        var assimilated = Take(circulating, Capacity(actor.Assimilation, TrainingFocus.Assimilation));
        circulating -= assimilated;
        var maximumHeld = p.FlowPerMonth * p.ReserveLimitMonths;
        var rawKept = Elements.Map(raw, v => Math.Min(v, maximumHeld));
        var refinedKept = Elements.Map(refined, v => Math.Min(v, maximumHeld));
        var circulationKept = Elements.Map(circulating, v => Math.Min(v, maximumHeld));
        var overflow = raw - rawKept + refined - refinedKept + circulating - circulationKept;
        purged += overflow;
        var process = new ProcessFlow(absorbed, purified, toCirculate, assimilated, purged, before, rawKept + refinedKept + circulationKept);
        if (!process.Conserved)
            throw new InvalidOperationException("Elemental process conservation failed.");
        // Safely filtered turbidity is a material loss, not energy retained beyond structural control.
        var pressure = Math.Max(0, process.HeldAfter.Total - process.HeldBefore.Total + overflow.Total);
        var excess = (int)Math.Min(1000, pressure * 10000 / Math.Max(1, flow) / 20);
        var rootDamage = Elements.Zip(state.RootDamage, Elements.Allocate(excess * 5L, command.Allocation), (x, y) => Math.Min(10000, x + y));
        var next = state with
        {
            Raw = rawKept,
            Refined = refinedKept,
            Circulating = circulationKept,
            RootDamage = rootDamage,
            Damage = Math.Min(10000, state.Damage + excess),
            Load = (int)Math.Min(10000, pressure * 10000 / Math.Max(1, flow)),
            ActivePath = command.PathId,
            LastTrainingMonth = context.Month
        };
        var blockers = new List<CultivationBlocker>();
        if (absorbed.Total == 0 && before.Total == 0)
            blockers.Add(new("supply", "실제 공급/영근 감응 부족", "영맥·오행·자리·영근 손상 확인"));
        if (toCirculate.Total < purified.Total)
            blockers.Add(new("circulation", "정제량보다 순환 수용이 낮음", "흡수 조절·운기 교정·경맥 회복"));
        var progressLimit = ProgressLimit(actor, knowledge, p, blockers);
        if (actor.Realm == Realm.Qi)
        {
            var perRoot = RootTarget(state, p);
            var limit = Math.Min(perRoot, perRoot * progressLimit / 10000);
            next = next with
            {
                RootGrowth = Elements.Zip(next.RootGrowth, assimilated, (old, added) => Math.Max(old, Math.Min(limit, old + added)))
            };
        }

        var required = RealmTarget(state, p);
        var structureLimit = required * progressLimit / 10000;
        // Tribulation never crosses more than the very next unperformed encounter in one interval.
        if (actor.Realm == Realm.Tribulation)
            structureLimit = Math.Min(structureLimit, (required * actor.Layer + 7) / 8);
        var weights = command.Focus == TrainingFocus.Structure ? next.Structure!.Measures.Select((value, i) =>
            (int)Math.Max(1, 10000 - value * 10000 / Target(required, next.Structure.Measures.Count, i))).ToArray() : profile.GrowthWeights;
        next = next with
        {
            Structure = Grow(next.Structure!, assimilated.Total, weights, structureLimit, required, context.Site)
        };
        var layer = Layer(next, actor.Realm, p);
        var complete = actor.Realm == Realm.Tribulation ? Maturity(next, p) == 10000 : Complete(next, actor, p);
        if (actor.Realm == Realm.Tribulation && (layer > actor.Layer || (complete && !actor.GreatPerfection)))
        {
            next = next with
            {
                PendingTrial = new(Math.Min(9, actor.Layer + 1), actor.Layer == 9, context.Month + 1)
            };
            layer = actor.Layer;
            complete = false;
        }

        if (actor.Realm == Realm.Void && next.Structure is VoidStructure v && v.ClearedDefects > v.RebuiltChannels)
            next = next with
            {
                Load = Math.Max(next.Load, (int)Math.Min(10000, (v.ClearedDefects - v.RebuiltChannels) * 10000 / Math.Max(1, required / 4)))
            };
        return Accept(next, new(Layer: layer, GreatPerfection: complete, HealthChange: excess > 0 ? -1 : 0), absorbed,
            notices: [new("공정 실습", $"흡수 {absorbed.Total}→정제 {purified.Total}→순환 {toCirculate.Total}→체화 {assimilated.Total}", knowledge.WorkId)],
            blockers: blockers, flow: process);
    }

    private static CultivationResult Auxiliary(CultivationState state, CultivationActor actor, ConfigureAuxiliaryCultivation command, CultivationContext context)
    {
        if (ActiveAttempt(state) || state.PendingTrial is not null)
            return Reject(state, "boundary", "현재 공정 중 보조 전환 불가", "공정 종결");
        if (command.PathId.Length == 0)
            return Accept(state with { AuxiliaryPath = "" });
        var profile = CultivationProfiles.Find(command.PathId);
        var knowledge = Knowledge(context, command.PathId);
        if (profile is null || profile.Definition.Realm != actor.Realm || command.PathId == state.ActivePath ||
            knowledge is null || !knowledge.Executable || knowledge.Understanding < context.Policy.MinimumUnderstanding)
            return Reject(state, "compatibility", "호환되는 학습된 보조 공정이 아님", "현재 경지·다른 경로 지식 확인");
        return Accept(state with { AuxiliaryPath = command.PathId }, notices: [new("보조 공정", "실제 공급/부하를 더 지불해 실습, 배타적 주 구조 효과 합산 없음", command.PathId)]);
    }

    private static Elements Take(Elements held, long capacity) => held.Total == 0 || capacity <= 0 ? default : Elements.Allocate(Math.Min(capacity, held.Total), held);
    private static int ProgressLimit(CultivationActor actor, KnowledgeEvidence knowledge, CultivationPolicy policy, List<CultivationBlocker> blockers)
    {
        if (actor.Circulation < policy.MinimumControl)
        {
            blockers.Add(new("gate-3-4", "3→4층 운기/경맥 병목", "순환 제어·경맥 실습"));
            return 3749;
        }

        if (actor.Circulation < policy.UpperControl || actor.Body < policy.UpperControl || actor.Mind < policy.UpperControl)
        {
            blockers.Add(new("gate-6-7", "6→7층 육신·정신·순환 병목", "육신/정신 수용·운기 교정"));
            return 7499;
        }

        if (actor.Circulation < policy.CompletionControl || actor.Body < policy.CompletionControl || actor.Mind < policy.CompletionControl || knowledge.Understanding < policy.MinimumUnderstanding)
        {
            blockers.Add(new("gate-completion", "대원만 완성 조건 부족", "완성 기반·지식·정신 수용 보완"));
            return 9999;
        }

        return 10000;
    }

    private static bool TryDebit(CultivationContext context, long amount, out Elements debit)
    {
        var blockers = new List<CultivationBlocker>();
        AddAccess(blockers, context.Site);
        var fair = Elements.Map(context.Site.Available, v => v / Math.Max(1, context.Site.Claimants));
        debit = Take(fair, amount);
        return blockers.Count == 0 && debit.Total == amount;
    }

    private static ProcessFlow SpentFlow(Elements spent) => new(spent, spent, spent, spent, default, default, default);
}
