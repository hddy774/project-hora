namespace Chronicles.Game.Cultivation;

public static partial class CultivationEngine
{
    public static IReadOnlyList<CultivationBlocker> Preflight(CultivationState state, CultivationActor actor,
        string pathId, CultivationContext context, bool conversion = false)
    {
        var result = new List<CultivationBlocker>();
        var path = CultivationProfiles.Find(pathId);
        if (path is null)
        {
            result.Add(new("path", "알 수 없는 경로", "알려진 경로 선택"));
            return result;
        }

        var target = conversion ? actor.Realm : actor.Realm + 1;
        if (actor.Realm == Realm.HalfImmortal && !conversion)
            result.Add(new("ascension", "반선 완성은 비승이 아님", "실제 통로·세계 법칙·비승 명령 확인"));
        if (path.Definition.Realm != target)
            result.Add(new("path-realm", "목표 경지 공법이 아님", "목표 경지 지식 확보"));
        AddAccess(result, context.Site);
        var knowledge = PreparationKnowledge(state, actor, pathId, context, conversion);
        if (knowledge is null || !knowledge.Executable || knowledge.Understanding < context.Policy.MinimumUnderstanding)
            result.Add(new("knowledge", "실제 이해/암기 또는 현장 열람 부족", "해독·학습·접근권 확보"));
        if (knowledge is not null && knowledge.Error > 5000)
            result.Add(new("interpretation", "채택 해석에 큰 오류", "정정·실습 검증"));
        if (state.PendingTrial is not null)
            result.Add(new("trial", "실제 소경지 겁난 미종결", "겁난 대응"));
        if (ActiveAttempt(state))
            result.Add(new("attempt", "진행 중인 형성/안착", "현재 공정 종결"));
        if (state.Damage > context.Policy.FailureDamage || actor.Health < 50)
            result.Add(new("damage", "구조/육신 손상", "회복 후 준비"));
        if (conversion)
        {
            if (actor.Realm == Realm.Mortal || state.ActivePath == pathId)
                result.Add(new("conversion", "유효한 다른 현재 경로가 아님", "현재 경지의 다른 경로 선택"));
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

    // Entry adds a sensing source without replacing an independently learned Qi source. Both candidates
    // share every physical formation term; only their actual understanding/error evidence differs.
    // Rank the exact integer formation result (including clamping); an equal result keeps the explicit Qi work.
    // No qualified candidate retains the prior refusal evidence, rather than inventing usable knowledge.
    internal static KnowledgeEvidence? PreparationKnowledge(CultivationState state, CultivationActor actor, string path,
        CultivationContext context, bool conversion = false)
    {
        var method = Knowledge(context, path);
        if (actor.Realm != Realm.Mortal || conversion)
            return method;
        var entry = Knowledge(context, "entry");
        var methodEligible = QualifiedPreparationKnowledge(method, context.Policy);
        var entryEligible = QualifiedPreparationKnowledge(entry, context.Policy);
        if (!methodEligible)
            return entryEligible ? entry : entry ?? method;
        if (!entryEligible)
            return method;
        return FormationQuality(state, actor, entry!, context) > FormationQuality(state, actor, method!, context) ? entry : method;
    }

    private static bool QualifiedPreparationKnowledge(KnowledgeEvidence? knowledge, CultivationPolicy policy) =>
        knowledge is { Executable: true } && knowledge.Understanding >= policy.MinimumUnderstanding && knowledge.Error <= 5000;
    internal static KnowledgeEvidence? Knowledge(CultivationContext context, string path) => context.Knowledge.Where(k => k.WorkId == path)
        .OrderByDescending(k => k.Revision).ThenBy(k => k.EditionId, StringComparer.Ordinal).FirstOrDefault();
    private static void AddAccess(List<CultivationBlocker> blockers, CultivationSite site)
    {
        if (!site.HasUseRight)
            blockers.Add(new("right", "현재 장소 수행 사용권 없음", "사용권 협상·공개 자리·이주"));
        if (!site.HasReservedSeat && site.OccupiedSeats >= site.SeatCapacity)
            blockers.Add(new("seat", "실제 수행 자리 만석", "자리 예약·점유권 협상·이주"));
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
        _ = state;
        _ = policy;
        return CultivationProfiles.FormationKeys(realm).Select((key, i) => new FormationFactor(key,
            Math.Clamp((quality * 3 + (i % 3 == 0 ? actor.Body : i % 3 == 1 ? actor.Circulation : actor.Mind)) / 4, 0, 10000))).ToArray();
    }
}
