namespace Chronicles.Game.Cultivation;

public static partial class CultivationEngine
{
    private static CultivationResult Trial(CultivationState state, CultivationActor actor, CultivationContext context, int sample)
    {
        var trial = state.PendingTrial;
        if (actor.Realm != Realm.Tribulation || trial is null || state.Structure is not TribulationStructure structure)
            return Reject(state, "trial", "미종결 실제 겁난 없음", "소경지 경계까지 수행");
        if (context.Month < trial.Due)
            return Reject(state, "time", "겁난 경계 미도달", "다음 사건까지 진행");
        if (state.Attempt?.Phase is AttemptPhase.Prepared or AttemptPhase.Started or AttemptPhase.Fractured)
            return Reject(state, "attempt", "다른 비가역 형성 공정 진행 중", "공정 경계 확인");
        var bit = trial.Completion ? 9 : trial.TargetLayer - 1;
        if ((structure.CompletedTrials & (1 << bit)) != 0)
            return Reject(state, "trial-replay", "같은 소경지 겁난 재실행", "저장/명령 revision 확인");
        var risk = Math.Clamp(context.Policy.TrialRisk + state.Damage / 5 + (100 - actor.Health) * 10, 0, 10000);
        if (sample < risk)
            return Die(state, CultivationDeathCause.Tribulation, "소경지 실제 겁난 실패");
        if (actor.Health <= context.Policy.TrialHealthCost)
        {
            var passed = state with
            {
                PendingTrial = null,
                Structure = structure with
                {
                    CompletedTrials = structure.CompletedTrials | (1 << bit)
                }
            };
            return Die(passed, CultivationDeathCause.PostTribulationCollapse, "겁후 육신 회복 불가")with
            {
                Actor = new(Layer: trial.TargetLayer, GreatPerfection: trial.Completion, Death: CultivationDeathCause.PostTribulationCollapse)
            };
        }

        return Accept(state with { PendingTrial = null, Structure = structure with { CompletedTrials = structure.CompletedTrials | (1 << bit) },
            Damage = Math.Min(10000, state.Damage + context.Policy.RecoveryPerMonth), Load = 0 },
            new(Layer: trial.TargetLayer, GreatPerfection: trial.Completion, HealthChange: -context.Policy.TrialHealthCost),
            notices: [new("겁난 통과", trial.Completion ? "대원만 완성 겁난 통과" : $"{trial.TargetLayer}층 실제 겁난 통과", "소경지 실제 사건")]);
    }

    internal static bool Complete(CultivationState state, CultivationActor actor, CultivationPolicy policy)
    {
        if (state.Structure is null)
            return false;
        var complete = Maturity(state, policy) == 10000;
        if (actor.Realm == Realm.Qi)
            complete &= state.RootGrowth.Minimum >= RootTarget(state, policy);
        if (actor.Realm == Realm.Tribulation)
            complete &= state.Structure is TribulationStructure { CompletedTrials: 1023 };
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
        if (state.Structure is null)
            return 0;
        var required = RealmTarget(state, policy);
        var values = state.Structure.Measures;
        return (int)Math.Clamp(Enumerable.Range(0, values.Count).Min(i => values[i] * 10000 / Target(required, values.Count, i)), 0, 10000);
    }

    internal static bool ActiveAttempt(CultivationState state) => state.Attempt is { Phase: not (AttemptPhase.Cancelled or AttemptPhase.Completed or AttemptPhase.Failed) };
    private static int Layer(CultivationState state, Realm realm, CultivationPolicy policy)
    {
        if (realm == Realm.Mortal || state.Structure is null)
            return 1;
        if (realm == Realm.Qi)
            return (int)Math.Clamp(1 + state.RootGrowth.Maximum * 8 / Math.Max(1, RootTarget(state, policy)), 1, 9);
        var values = state.Structure.Measures;
        var required = RealmTarget(state, policy);
        var layer = 1;
        for (var next = 2; next <= 9; next++)
            if (Enumerable.Range(0, values.Count).All(i => values[i] >= (Target(required, values.Count, i) * (next - 1) + 7) / 8))
                layer = next;
        return layer;
    }

    private static long Target(long required, int count, int index) => required / count + (index < required % count ? 1 : 0);
    private static RealmStructure Grow(RealmStructure structure, long work, IReadOnlyList<int> weights, long limit, long required, CultivationSite site)
    {
        var values = structure.Measures.ToArray();
        var caps = Enumerable.Range(0, values.Length).Select(i => (Target(required, values.Length, i) * limit + required - 1) / required).ToArray();
        // Upper structures demand their real preceding process, not generic experience renamed.
        if (structure is SpiritStructure && values[0] < Target(required, values.Length, 0) / 2)
            caps[1] = 0;
        if (structure is VoidStructure && values[0] == 0)
            caps[1] = 0;
        if (structure is IntegrationStructure && values[0] + values[1] == 0)
            caps[2] = caps[3] = 0;
        if (structure is HalfImmortalStructure)
        {
            if (!site.ObservedRealmConditions)
                caps[1] = values[1];
            if (!site.RealPassageAccessible)
                caps[2] = values[2];
        }

        var active = Enumerable.Range(0, values.Length).Where(i => values[i] < caps[i]).ToArray();
        // A single interval spends each unit once. Excess at a completed field can service remaining fields.
        while (work > 0 && active.Length > 0)
        {
            var weight = active.Sum(i => weights[i]);
            var before = work;
            foreach (var i in active)
            {
                var added = Math.Min(caps[i] - values[i], Math.Max(1, before * weights[i] / weight));
                added = Math.Min(added, work);
                values[i] += added;
                work -= added;
            }

            active = active.Where(i => values[i] < caps[i]).ToArray();
        }

        return structure.WithMeasures(values);
    }

    private static RealmStructure Scale(RealmStructure structure, int amount) => structure.WithMeasures(structure.Measures.Select(v => v * amount / 10000).ToArray());
}
