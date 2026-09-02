using Chronicles.Core;

namespace Chronicles.Simulation;

/// <summary>
/// 같은 시각에 예정된 장기 수행 Work의 읽기 전용 입력과 순수 계산 결과를 보관한다.
/// 병렬 구간은 이 버퍼 외의 World 권위 상태, RNG, 시장, 관계 및 기록 저장소에 접근하지 않는다.
/// </summary>
internal sealed class CultivationParallelCoordinator
{
    internal const int DefaultMinimumBatchSize = 64;

    private readonly int[] _dueAgents;
    private readonly int[] _candidateAgents;
    private readonly int[] _resultSlotByAgent;
    private readonly CultivationWorkInput[] _inputs;
    private readonly CultivationWorkResult[] _results;
    private int _candidateCount;

    public CultivationParallelCoordinator(int population)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(population);
        _dueAgents = new int[population];
        _candidateAgents = new int[population];
        _resultSlotByAgent = new int[population];
        _inputs = new CultivationWorkInput[population];
        _results = new CultivationWorkResult[population];
        Array.Fill(_resultSlotByAgent, -1);
    }

    public int[] DueAgents => _dueAgents;

    public bool Enabled { get; set; } = true;

    public int MinimumBatchSize { get; set; } = DefaultMinimumBatchSize;

    public int WorkerCount { get; } = Math.Min(
        Math.Max(Environment.ProcessorCount - 1, 1),
        4);

    public long PreparedBatchCount { get; private set; }

    public long EligibleCandidateCount { get; private set; }

    public long ParallelBatchCount { get; private set; }

    public long ParallelCandidateCount { get; private set; }

    public long FingerprintFallbackCount { get; private set; }

    public bool CanPrepareBatch(int dueCount) =>
        Enabled && dueCount >= MinimumBatchSize;

    public void BeginBatch()
    {
        _candidateCount = 0;
    }

    public void AddCandidate(int agent, CultivationWorkInput input)
    {
        var slot = _candidateCount++;
        _candidateAgents[slot] = agent;
        _inputs[slot] = input;
        _resultSlotByAgent[agent] = slot;
    }

    public int CompleteBatch()
    {
        var candidateCount = _candidateCount;
        if (!Enabled || candidateCount == 0)
        {
            return 0;
        }

        PreparedBatchCount++;
        EligibleCandidateCount = checked(EligibleCandidateCount + candidateCount);
        var useParallel = WorkerCount > 1 && candidateCount >= MinimumBatchSize;
        if (!useParallel)
        {
            for (var slot = 0; slot < candidateCount; slot++)
            {
                _resultSlotByAgent[_candidateAgents[slot]] = -1;
            }
            return 0;
        }

        Parallel.For(
            0,
            candidateCount,
            new ParallelOptions { MaxDegreeOfParallelism = WorkerCount },
            slot => _results[slot] = CultivationWorkCalculator.Calculate(_inputs[slot]));
        ParallelBatchCount++;
        ParallelCandidateCount = checked(ParallelCandidateCount + candidateCount);

        return candidateCount;
    }

    public bool TryAcquire(
        int agent,
        CultivationWorkFingerprint currentFingerprint,
        out CultivationWorkResult result)
    {
        var slot = _resultSlotByAgent[agent];
        if (slot < 0)
        {
            result = default;
            return false;
        }

        _resultSlotByAgent[agent] = -1;
        if (_inputs[slot].Fingerprint != currentFingerprint)
        {
            FingerprintFallbackCount++;
            result = default;
            return false;
        }

        result = _results[slot];
        return true;
    }

    public bool HasCandidate(int agent) => _resultSlotByAgent[agent] >= 0;

    public void Discard(int agent)
    {
        _resultSlotByAgent[agent] = -1;
    }

    public CultivationParallelDiagnostics GetDiagnostics() => new(
        Enabled,
        WorkerCount,
        MinimumBatchSize,
        PreparedBatchCount,
        EligibleCandidateCount,
        ParallelBatchCount,
        ParallelCandidateCount,
        FingerprintFallbackCount);

}

internal readonly record struct CultivationWorkFingerprint(
    long DueTick,
    long LifeAuthorityRevision,
    PersonLifeState LifeState,
    AgentActivity Activity,
    long PlanAuthorityRevision,
    long PlanCompletedMonths,
    bool PlanIsActive,
    CultivationResourcePolicy ResourcePolicy,
    int FrozenConditionFactorBasisPoints,
    long CultivationAuthorityRevision,
    CultivationState CultivationState,
    RealmStage Realm,
    byte RealmLayer,
    bool FloorNineCompleted,
    int CultivationProgress,
    int PendingCultivationWork,
    int Health,
    int MaximumHealth,
    ushort LocationId,
    long EnvironmentQualityRevision,
    long NextDecisionTick,
    long LastActionMonth);

internal readonly record struct RealmComponentWorkInput(int Current, int RecomputedLimit);

internal readonly record struct RealmComponentWorkResult(int Value, int EffectiveLimit);

internal readonly record struct CultivationWorkInput(
    CultivationWorkFingerprint Fingerprint,
    int RequestedMonths,
    int AptitudeCompositeHundredths,
    ushort TechniqueQualityHundredths,
    int EnvironmentQualityHundredths,
    bool DeveloperModeEnabled,
    int CultivationWorkPercent,
    int Requirement,
    byte ComponentCount,
    RealmComponentWorkInput Component0,
    RealmComponentWorkInput Component1,
    RealmComponentWorkInput Component2,
    RealmComponentWorkInput Component3);

internal readonly record struct CultivationWorkResult(
    int ProcessedMonths,
    int WorkBefore,
    int TotalGain,
    int WorkAfter,
    int Carryover,
    int Requirement,
    int EnvironmentFactorBasisPoints,
    byte ComponentCount,
    RealmComponentWorkResult Component0,
    RealmComponentWorkResult Component1,
    RealmComponentWorkResult Component2,
    RealmComponentWorkResult Component3);

internal readonly record struct CultivationParallelDiagnostics(
    bool Enabled,
    int WorkerCount,
    int MinimumBatchSize,
    long PreparedBatchCount,
    long EligibleCandidateCount,
    long ParallelBatchCount,
    long ParallelCandidateCount,
    long FingerprintFallbackCount);

internal static class CultivationWorkCalculator
{
    public static CultivationWorkResult Calculate(CultivationWorkInput input)
    {
        var bodyCondition = CultivationPlanRules.GetBodyConditionBasisPoints(
            input.Fingerprint.Health,
            Math.Max(1, input.Fingerprint.MaximumHealth));
        var environmentFactor = CultivationRules.GetEnvironmentFactorBasisPoints(
            input.EnvironmentQualityHundredths);
        var monthlyGain = CultivationRules.GetMonthlyRealmWorkGain(
            input.AptitudeCompositeHundredths,
            input.TechniqueQualityHundredths,
            input.EnvironmentQualityHundredths,
            bodyCondition,
            input.Fingerprint.FrozenConditionFactorBasisPoints);
        if (input.DeveloperModeEnabled)
        {
            monthlyGain = WorldBalanceRules.ApplyPercent(
                monthlyGain,
                input.CultivationWorkPercent);
        }

        var remaining = Math.Max(1, input.Requirement - input.Fingerprint.CultivationProgress);
        var monthsToBoundary = checked((remaining + monthlyGain - 1) / monthlyGain);
        var processedMonths = Math.Min(input.RequestedMonths, monthsToBoundary);
        var totalGain = checked(monthlyGain * processedMonths);
        var rawAfter = checked(input.Fingerprint.CultivationProgress + totalGain);
        var after = Math.Min(input.Requirement, rawAfter);
        var carryover = Math.Max(0, rawAfter - input.Requirement);
        var componentGrowth = Math.Max(
            1,
            checked(monthlyGain * 1_200 / Math.Max(1, input.Requirement)));
        componentGrowth = checked(componentGrowth * processedMonths);

        return new CultivationWorkResult(
            processedMonths,
            input.Fingerprint.CultivationProgress,
            totalGain,
            after,
            carryover,
            input.Requirement,
            environmentFactor,
            input.ComponentCount,
            CalculateComponent(input.Component0, componentGrowth),
            CalculateComponent(input.Component1, componentGrowth),
            CalculateComponent(input.Component2, componentGrowth),
            CalculateComponent(input.Component3, componentGrowth));
    }

    private static RealmComponentWorkResult CalculateComponent(
        RealmComponentWorkInput input,
        int growth)
    {
        var effectiveLimit = Math.Max(input.Current, input.RecomputedLimit);
        return new RealmComponentWorkResult(
            Math.Min(effectiveLimit, checked(input.Current + growth)),
            effectiveLimit);
    }
}

public sealed partial class World
{
    private void PrepareParallelCultivationWork(
        ReadOnlySpan<int> dueAgents,
        long dueTick,
        long currentMonth)
    {
        if (!_cultivationParallel.CanPrepareBatch(dueAgents.Length))
        {
            return;
        }

        _cultivationParallel.BeginBatch();
        foreach (var agent in dueAgents)
        {
            if (TryCreateParallelCultivationInput(agent, dueTick, currentMonth) is { } input)
            {
                _cultivationParallel.AddCandidate(agent, input);
            }
        }

        _cultivationParallel.CompleteBatch();
    }

    private CultivationWorkInput? TryCreateParallelCultivationInput(
        int index,
        long dueTick,
        long currentMonth)
    {
        var plan = _agents.CultivationPlans[index];
        if (_agents.NextDecisionTicks[index] != dueTick
            || _agents.LifeStates[index] != PersonLifeState.Living
            || _agents.Activities[index] != AgentActivity.Cultivating
            || _agents.CultivationStates[index] != CultivationState.RealmCultivation
            || _agents.FloorNineCompleted[index]
            || !plan.IsActive
            || plan.ResourcePolicy != CultivationResourcePolicy.None)
        {
            return null;
        }

        var requestedMonths = GetCultivationPulseMonths(index, currentMonth);
        if (requestedMonths <= 1)
        {
            return null;
        }

        var components = CultivationRules.GetComponents(_agents.RealmStages[index]);
        var componentCount = checked((byte)Math.Min(
            components.Count,
            CultivationRules.RealmComponentSlotCount));
        var fingerprint = CreateCultivationWorkFingerprint(index, dueTick);
        var environmentQuality = _atlas.GetCultivationEnvironmentQualityHundredths(
            _agents.LocationIds[index]);
        return new CultivationWorkInput(
            fingerprint,
            requestedMonths,
            GetInnateAptitudeComposite(index),
            _agents.TechniqueQualities[index],
            environmentQuality,
            _balance.DeveloperModeEnabled,
            _balance.CultivationWorkPercent,
            GetCultivationRequirement(index),
            componentCount,
            CreateComponentInput(index, 0, componentCount),
            CreateComponentInput(index, 1, componentCount),
            CreateComponentInput(index, 2, componentCount),
            CreateComponentInput(index, 3, componentCount));
    }

    private CultivationWorkFingerprint CreateCultivationWorkFingerprint(int index, long dueTick)
    {
        var plan = _agents.CultivationPlans[index];
        return new CultivationWorkFingerprint(
            dueTick,
            _agents.LifeAuthorityRevisions[index],
            _agents.LifeStates[index],
            _agents.Activities[index],
            plan.AuthorityRevision,
            plan.CompletedMonths,
            plan.IsActive,
            plan.ResourcePolicy,
            plan.FrozenConditionFactorBasisPoints,
            _agents.CultivationAuthorityRevisions[index],
            _agents.CultivationStates[index],
            _agents.RealmStages[index],
            _agents.RealmLayers[index],
            _agents.FloorNineCompleted[index],
            _agents.CultivationProgress[index],
            _agents.PendingCultivationWork[index],
            _agents.Health[index],
            _agents.MaximumHealth[index],
            _agents.LocationIds[index],
            _atlas.GetCultivationEnvironmentQualityRevision(_agents.LocationIds[index]),
            _agents.NextDecisionTicks[index],
            _agents.LastActionMonths[index]);
    }

    private RealmComponentWorkInput CreateComponentInput(int index, int component, byte count) =>
        component < count
            ? new RealmComponentWorkInput(
                _agents.GetCultivationComponentValue(index, component),
                CalculateComponentLimit(index, component))
            : default;

    private void ApplyParallelCultivationWork(int index, CultivationWorkResult result)
    {
        _agents.CultivationProgress[index] = result.WorkAfter;
        _agents.PendingCultivationWork[index] = result.Carryover;
        _agents.LastCultivationWorkBefore[index] = result.WorkBefore;
        _agents.LastCultivationWorkGain[index] = result.TotalGain;
        ApplyComponentResult(index, 0, result.ComponentCount, result.Component0);
        ApplyComponentResult(index, 1, result.ComponentCount, result.Component1);
        ApplyComponentResult(index, 2, result.ComponentCount, result.Component2);
        ApplyComponentResult(index, 3, result.ComponentCount, result.Component3);
        CommitCultivationTrace(
            index,
            CultivationFormulaKind.ProgressAccumulation,
            new CultivationTraceState(
                Tick.Value,
                CultivationRules.FormulaRevision,
                BreakthroughClass.None,
                BreakthroughOutcome.None,
                _agents.RealmStages[index],
                _agents.RealmLayers[index],
                _agents.RealmLayers[index],
                result.WorkBefore,
                result.TotalGain,
                result.WorkAfter,
                result.Requirement,
                result.Carryover,
                0,
                0,
                0,
                -1,
                CultivationBottleneckCause.None,
                EnvironmentFactorBasisPoints: result.EnvironmentFactorBasisPoints,
                EnvironmentAvailability: CultivationValueAvailability.Actual));
        if (result.WorkAfter >= result.Requirement)
        {
            _agents.CultivationStates[index] = CultivationState.PreparingBreakthrough;
        }
    }

    private void ApplyComponentResult(
        int index,
        int component,
        byte count,
        RealmComponentWorkResult result)
    {
        if (component < count)
        {
            _agents.SetCultivationComponent(index, component, result.Value, result.EffectiveLimit);
        }
    }

    internal void ConfigureCultivationParallelismForTesting(
        bool enabled,
        int minimumBatchSize = CultivationParallelCoordinator.DefaultMinimumBatchSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumBatchSize);
        _cultivationParallel.Enabled = enabled;
        _cultivationParallel.MinimumBatchSize = minimumBatchSize;
    }

    internal CultivationParallelDiagnostics GetCultivationParallelDiagnostics() =>
        _cultivationParallel.GetDiagnostics();
}
