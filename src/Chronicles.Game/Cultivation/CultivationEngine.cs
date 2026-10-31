namespace Chronicles.Game.Cultivation;

/// <summary>Pure, deterministic transition authority. The caller commits the returned metadata and deltas together.</summary>
public static partial class CultivationEngine
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
