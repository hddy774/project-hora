namespace Chronicles.Game.Cultivation;

/// <summary>The eighth axis is actual maintenance cost: lower is better, unlike the first seven axes.</summary>
public readonly record struct CombatAxes(long OutputPenetration, long ReserveEndurance, long ControlPrecision,
    long ProtectionRecovery, long MovementSpace, long SensingConcealment, long CoordinationLoad, long MaintenanceCost)
{
    public long At(int axis) => axis switch
    {
        0 => OutputPenetration, 1 => ReserveEndurance, 2 => ControlPrecision, 3 => ProtectionRecovery,
        4 => MovementSpace, 5 => SensingConcealment, 6 => CoordinationLoad, 7 => MaintenanceCost,
        _ => throw new ArgumentOutOfRangeException(nameof(axis))
    };
    public static CombatAxes Uniform(long value) => new(value, value, value, value, value, value, value, value);
    public static CombatAxes From(IReadOnlyList<long> values) =>
        new(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7]);
}

/// <summary>One actual cause, with all of its axis effects. Aliases from traits/traces/equipment share SourceId.</summary>
public sealed record CombatSourceEffect(string SourceId, long Revision, CombatAxes Additive, CombatAxes Multipliers);
public sealed record CombatContribution(string SourceId, long Revision, CombatAxes Before, CombatAxes After);
public sealed record CombatSituation
{
    public string Name { get; init; } = "단독·중거리·중립 장소·1개월";
    public bool Moving { get; init; }
    public bool PreparedFormation { get; init; }
    public bool LowSupply { get; init; }
    public bool MentalInterference { get; init; }
    public bool CelestialWindow { get; init; }
    public bool ObservedRealmConditions { get; init; }
    public bool AccessiblePassage { get; init; }
    public int ActualAlliedRoles { get; init; }
    public int DurationMonths { get; init; } = 1;
}
public sealed record CombatProjection(CombatAxes Axes, long DisplayPower, CombatSituation Situation,
    IReadOnlyList<CombatContribution> Contributions, int ControlledSubspiritRoles, string Basis);
public sealed record RootProgressView(string Element, int InnateQuality, long Growth, long Required, int Layer, bool Complete, int Damage);
public sealed record CultivationStructureView(string Key, long Progress, long Required);
public sealed record CultivationInspection(IReadOnlyList<RootProgressView> Roots, IReadOnlyList<CultivationStructureView> Current,
    CultivationFormation? Formation, int Maturity, int Damage, int Load, string Path, string Auxiliary,
    AttemptPhase? Phase, PendingTribulation? Trial);
public sealed record PotentialRoute(string Path, Realm TargetRealm, int ParentQualityCarried,
    int CurrentMaturityCarried, long MinimumSupplyToStart, IReadOnlyList<CultivationBlocker> Missing, string Condition);
public sealed record CultivationPotential(int StructuralHeadroom, int CurrentDamage, bool HasActualRecoverySupply,
    IReadOnlyList<PotentialRoute> KnownNextRoutes, string InformationBasis);
public sealed record RetreatBoundary(long Month, string Kind);
public sealed record RetreatSegment(long EndMonth, string Reason, bool RequestedEndReached);

/// <summary>These projections consume no RNG and never mutate actor knowledge or domain metadata.</summary>
public static class CultivationQueries
{
    public static IReadOnlyList<string> AxisNames { get; } = Array.AsReadOnly(new[]
        { "출력·관통", "비축·지속", "제어·정밀", "방호·복원", "이동·공간", "감지·은폐", "협응·부하", "유지 비용" });
    private static readonly string[] ElementNames = ["금", "목", "수", "화", "토"];

    public static CultivationInspection Inspect(CultivationState state, CultivationActor actor, CultivationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(actor); ArgumentNullException.ThrowIfNull(policy);
        var rootTarget = CultivationEngine.RootTarget(state, policy);
        var roots = Enumerable.Range(0, 5).Select(i => new RootProgressView(ElementNames[i], (int)actor.InnateRoots.At(i),
            state.RootGrowth.At(i), rootTarget, (int)Math.Clamp(1 + state.RootGrowth.At(i) * 8 / rootTarget, 1, 9),
            state.RootGrowth.At(i) >= rootTarget, (int)state.RootDamage.At(i))).ToArray();
        var current = state.Structure is null ? [] : state.Structure.Measures.Select((v, i) => new CultivationStructureView(
            CultivationProfiles.GrowthKeys(actor.Realm)[i], v, Target(CultivationEngine.RealmTarget(state, policy), state.Structure.Measures.Count, i))).ToArray();
        return new(roots, current, state.Formations.Count == 0 ? null : CopyFormation(state.Formations[^1]),
            state.Structure is null ? 0 : CultivationEngine.Maturity(state, policy), state.Damage, state.Load,
            state.ActivePath, state.AuxiliaryPath, state.Attempt?.Phase, state.PendingTrial is null ? null : state.PendingTrial with { });
    }

    public static CombatProjection Combat(CultivationState state, CultivationActor actor, CultivationPolicy policy,
        CombatSituation situation, IReadOnlyList<CombatSourceEffect>? actualEffects = null)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(actor); ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(situation);
        if (situation.DurationMonths < 1 || situation.ActualAlliedRoles < 0) throw new ArgumentOutOfRangeException(nameof(situation));
        var spiritBase = actor.Realm == Realm.Mortal ? 100 : 1000L;
        for (var realm = 1; realm < (int)actor.Realm; realm++) spiritBase *= 8;
        var martialBase = 64000L * actor.MartialStage * actor.MartialStage / 100;
        var spiritual = spiritBase >= martialBase && state.Structure is not null;
        var basePower = Math.Max(spiritBase, martialBase);
        var normalized = spiritual ? ActualAttributes(state.Structure!, CultivationEngine.RealmTarget(state, policy)) : CombatAxes.Uniform(actor.MartialStage * 1000);
        var quality = spiritual && state.Formations.Count > 0 ? state.Formations[^1].Quality : 10000;
        var injury = spiritual ? state.Damage : state.Martial.Injury;
        var settling = state.Attempt?.Phase is AttemptPhase.Stabilizing or AttemptPhase.Recovering ? 7000 : 10000;
        var condition = (long)actor.Health * (10000 - injury) / 100 * settling / 10000;
        var values = new long[8];
        for (var axis = 0; axis < 7; axis++)
            values[axis] = basePower * (5000 + normalized.At(axis) / 2) / 10000 * (5000 + quality / 2) / 10000 * condition / 10000;
        var path = CultivationProfiles.Find(state.ActivePath);
        var maturity = spiritual ? CultivationEngine.Maturity(state, policy) : 0;
        if (spiritual && path is not null)
        {
            // A path label contributes nothing until its actual current structure has been trained.
            for (var axis = 0; axis < 7; axis++)
                values[axis] = values[axis] * (10000 + (path.Definition.CombatFactors[axis] - 100L) * maturity / 100) / 10000;
        }
        values[7] = spiritual ? policy.FlowPerMonth * (path?.Definition.SupplyCost ?? 1) *
            (10000 + injury + state.Load / 2) / 10000 : policy.FlowPerMonth;
        if (spiritual && path is not null)
        {
            values[7] = values[7] * (10000 + (path.Definition.CombatFactors[7] - 100L) * maturity / 100) / 10000;
            ApplyOperatingConditions(values, actor.Realm, path.Variant, situation, state, maturity);
        }
        if (situation.MentalInterference) { values[2] = values[2] * 3 / 4; values[6] = values[6] * 2 / 3; }
        if (situation.LowSupply && situation.DurationMonths > 1)
        {
            values[0] /= 2; values[1] /= 2; values[7] = values[7] * 3 / 2;
        }
        var contributions = new List<CombatContribution>
        { new(spiritual ? "current-structure" : "martial-body", state.Revision, default, CombatAxes.From(values)) };
        var effects = (actualEffects ?? []).GroupBy(e => e.SourceId, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);
        foreach (var group in effects)
        {
            if (string.IsNullOrWhiteSpace(group.Key)) throw new ArgumentException("Combat effects require a stable actual cause ID.", nameof(actualEffects));
            var revision = group.Max(e => e.Revision);
            var versions = group.Where(e => e.Revision == revision).Distinct().ToArray();
            if (versions.Length != 1) throw new ArgumentException("Conflicting combat effects for one source/revision.", nameof(actualEffects));
            var effect = versions[0]; var before = CombatAxes.From(values);
            for (var axis = 0; axis < 8; axis++)
            {
                if (effect.Multipliers.At(axis) is < 0 or > 100000) throw new ArgumentOutOfRangeException(nameof(actualEffects));
                values[axis] = Math.Max(0, checked(values[axis] + effect.Additive.At(axis)) * effect.Multipliers.At(axis) / 10000);
            }
            contributions.Add(new(effect.SourceId, revision, before, CombatAxes.From(values)));
        }
        // Use an explicit reference engagement, with endurance weighted more for a longer encounter.
        int[] weights = [22, situation.DurationMonths > 1 ? 36 : 18, 15, 15, situation.Moving ? 20 : 10, 10, 10];
        long sum = 0; for (var axis = 0; axis < 7; axis++) sum += values[axis] * weights[axis];
        var power = sum / weights.Sum() * policy.FlowPerMonth / Math.Max(policy.FlowPerMonth / 2, values[7]);
        var roles = spiritual && state.Structure is SpiritStructure spirit
            ? (int)Math.Min(3, Math.Min(spirit.SubspiritFormation, spirit.ControlCapacity) * 12 / Math.Max(1, CultivationEngine.RealmTarget(state, policy))) : 0;
        return new(CombatAxes.From(values), power, situation with { }, contributions.ToArray(), roles,
            "실제 현재 구조·형성·건강·숙련/장비 효과의 조건부 투영; 승률/최종 도달 경지 예측 아님");
    }

    public static CultivationPotential Potential(CultivationState state, CultivationActor actor, CultivationContext knownContext)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(actor); ArgumentNullException.ThrowIfNull(knownContext);
        var quality = state.Formations.Count == 0 ? 0 : state.Formations[^1].Quality;
        var maturity = state.Structure is null ? 0 : CultivationEngine.Maturity(state, knownContext.Policy);
        var next = actor.Realm + 1;
        var sensing = actor.Realm == Realm.Mortal && CultivationEngine.Knowledge(knownContext, "entry") is { Executable: true };
        var paths = actor.Realm == Realm.HalfImmortal ? [] : CultivationProfiles.Paths
            .Where(p => p.Definition.Realm == next && (knownContext.Knowledge.Any(k => k.WorkId == p.Definition.Id && k.Executable) || sensing && p.Variant == 0))
            .Select(p => new PotentialRoute(p.Definition.Id, next, quality / 6, maturity,
                knownContext.Policy.FlowPerMonth * p.Definition.SupplyCost,
                CultivationEngine.Preflight(state, actor, p.Definition.Id, knownContext),
                (sensing ? "실제 입문 지식·토납 준비에 근거한 감응 시도·연기 운기 공법은 입문 후 별도 학습; " : "") +
                $"{p.NextRequirement}; 부모 형성 품질은 새 형성 계산에 한 번만 전달, 현재 능력/성공 보장 아님")).ToArray();
        return new(10000 - maturity, state.Damage, knownContext.Site.HasUseRight && knownContext.Site.Available.Total >= knownContext.Policy.FlowPerMonth,
            paths, "호출자가 제공한 자기 지식/실제 관측만 사용; 미학습 경로·미확인 미래는 제시하지 않음");
    }

    /// <summary>Retreat is a sequence of intervals; no bulk step may jump over an external or internal boundary.</summary>
    public static RetreatSegment NextRetreatSegment(CultivationState state, long month, long requestedEnd,
        IReadOnlyList<RetreatBoundary>? externalBoundaries = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requestedEnd, month);
        var boundaries = new List<RetreatBoundary> { new(month + 1, "공급·양육·부하의 다음 정수 월 경계") };
        if (state.PendingTrial is not null) boundaries.Add(new(Math.Max(month, state.PendingTrial.Due), "실제 소경지 겁난"));
        if (CultivationEngine.ActiveAttempt(state)) boundaries.Add(new(Math.Max(month, state.Attempt!.Due), state.Attempt.Phase.ToString()));
        boundaries.AddRange((externalBoundaries ?? []).Where(b => b.Month >= month));
        var first = boundaries.OrderBy(b => b.Month).ThenBy(b => b.Kind, StringComparer.Ordinal).First();
        return new(Math.Min(requestedEnd, first.Month), requestedEnd <= first.Month ? "계획 종료" : first.Kind, requestedEnd <= first.Month);
    }

    private static CombatAxes ActualAttributes(RealmStructure structure, long required)
    {
        var measures = structure.Measures;
        var n = measures.Select((v, i) => Math.Clamp(v * 10000 / Target(required, measures.Count, i), 0, 10000)).ToArray();
        long Get(int i) => n[i % n.Length];
        return structure switch
        {
            QiStructure => new(Get(0), Get(2), Get(1), Get(2), Get(1), Get(3), Math.Min(Get(1), Get(3)), 0),
            FoundationStructure => new(Get(1), Get(1), Get(2), Get(3), Get(2), Get(0), Get(4), 0),
            GoldenCoreStructure => new((Get(1) + Get(2)) / 2, Get(0), Get(3), Get(2), Get(3), Get(1), Get(3), 0),
            NascentSoulStructure => new(Get(0), Get(1), Get(2), Get(1), Get(3), Get(2), Get(3), 0),
            SpiritStructure => new(Get(0), Get(0), Get(2), Get(3), Get(3), Get(1), Math.Min(Get(1), Get(2)), 0),
            VoidStructure => new(Get(3), Get(3), Get(1), Get(0), Get(1), Get(2), Get(2), 0),
            IntegrationStructure => new(Math.Min(Get(0), Get(1)), Get(3), Get(2), Get(3), Get(2), Get(1), Get(2), 0),
            TribulationStructure => new(Get(0), Get(1), Get(2), Get(1), Get(2), Get(2), Get(2), 0),
            MahayanaStructure => new(Get(0), Get(1), Get(0), Get(1), Get(2), Get(2), Get(1), 0),
            HalfImmortalStructure => new(Get(0), Get(0), Get(1), Get(0), Get(2), Get(1), Get(2), 0),
            _ => throw new ArgumentOutOfRangeException(nameof(structure))
        };
    }

    private static void ApplyOperatingConditions(long[] values, Realm realm, int variant, CombatSituation situation, CultivationState state, int maturity)
    {
        void Scale(int axis, int basis) => values[axis] = values[axis] * (10000 + (basis - 10000L) * maturity / 10000) / 10000;
        if (realm == Realm.Foundation)
        {
            if (variant == 0 && situation.Moving) { Scale(2, 11500); Scale(4, 12500); }
            if (variant == 1) { Scale(1, situation.PreparedFormation && !situation.Moving ? 16000 : 6500); Scale(7, situation.PreparedFormation ? 11000 : 16000); }
            if (variant == 2) { Scale(6, situation.CelestialWindow ? 16000 : 8000); Scale(2, situation.CelestialWindow ? 12000 : 8500); }
        }
        else if (realm == Realm.GoldenCore)
        {
            if (variant == 0) { Scale(1, situation.DurationMonths > 1 ? 13500 : 10500); Scale(2, 9000); }
            if (variant == 1) { Scale(0, situation.DurationMonths == 1 ? 13500 : 9500); Scale(1, 8500); }
            if (variant == 2) { Scale(4, situation.Moving ? 13000 : 10500); Scale(2, state.Damage > 0 ? 7000 : 12000); }
        }
        else if (realm == Realm.HalfImmortal)
        {
            if (variant == 0 && situation.LowSupply) { Scale(1, 14000); Scale(7, 7000); Scale(0, 8500); }
            if (variant == 1) Scale(2, situation.ObservedRealmConditions ? 13500 : 7500);
            if (variant == 2) { Scale(4, situation.AccessiblePassage ? 15000 : 7500); Scale(7, 14000); }
        }
        else
        {
            if (variant == 0) { Scale(0, situation.DurationMonths == 1 ? 12000 : 9500); Scale(6, 8500); }
            if (variant == 1) { Scale(1, situation.DurationMonths > 1 ? 13500 : 9000); Scale(7, 12500); }
            if (variant == 2) { Scale(4, situation.Moving ? 12500 : 9500); Scale(6, situation.MentalInterference ? 6500 : 12000); Scale(7, 14000); }
        }
        if (state.Structure is VoidStructure v && v.ClearedDefects > v.RebuiltChannels) { Scale(0, 6000); Scale(3, 6000); }
    }

    private static long Target(long required, int count, int index) => required / count + (index < required % count ? 1 : 0);
    private static CultivationFormation CopyFormation(CultivationFormation formation) => formation with
    {
        Factors = formation.Factors.Select(f => f with { }).ToArray(), Knowledge = formation.Knowledge is null ? null : formation.Knowledge with { },
        Parent = formation.Parent is null ? null : formation.Parent with { }
    };
}
