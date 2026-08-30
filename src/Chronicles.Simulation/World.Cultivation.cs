using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private void ResolveCultivation(int index)
    {
        switch (_agents.CultivationStates[index])
        {
            case CultivationState.PreCultivation:
                if (GetAgeYears(index) < CultivationRules.EntryAgeYears)
                {
                    return;
                }

                _agents.CultivationStates[index] = CultivationState.EntryTraining;
                AccumulateEntryWork(index);
                return;
            case CultivationState.EntryTraining:
                AccumulateEntryWork(index);
                return;
            case CultivationState.Completed:
                _agents.CultivationProgress[index] = GetCultivationRequirement(index);
                return;
            case CultivationState.Stabilizing:
                CompleteStabilization(index);
                return;
            case CultivationState.Stable:
                ResolveStableState(index);
                return;
            case CultivationState.PreparingBreakthrough:
                ResolveBreakthroughPreflight(index);
                return;
            case CultivationState.PreflightReady:
                ResolveBreakthroughAttempt(index);
                return;
            case CultivationState.Bottleneck:
                if (_agents.CultivationBottleneckCauses[index] == CultivationBottleneckCause.RealmComponent)
                {
                    GrowRealmComponents(index, CultivationRules.BaseMonthlyWork);
                }

                return;
            case CultivationState.AttemptingBreakthrough:
                throw new InvalidOperationException("돌파 시도 상태는 하나의 행동 안에서 종료되어야 합니다.");
            case CultivationState.RealmCultivation:
                AccumulateRealmWork(index);
                return;
            default:
                throw new InvalidOperationException("알 수 없는 수행 상태입니다.");
        }
    }

    private void AccumulateEntryWork(int index)
    {
        var before = _agents.CultivationProgress[index];
        var aptitude = GetInnateAptitudeComposite(index);
        var body = (_agents.Bones[index] + _agents.Constitutions[index]) / 2;
        var gain = CultivationRules.GetEntryWorkGain(
            aptitude,
            _agents.TechniqueQualities[index],
            body);
        var after = Math.Min(CultivationRules.RequiredEntryWork, checked(before + gain));
        _agents.CultivationProgress[index] = after;
        _agents.LastCultivationWorkBefore[index] = before;
        _agents.LastCultivationWorkGain[index] = gain;
        var passMask = EvaluateEntryGatePassMask(index);
        _agents.EntryGatePassMasks[index] = passMask;
        var outcome = BreakthroughOutcome.None;
        var blocker = CultivationBottleneckCause.None;

        if (after >= CultivationRules.RequiredEntryWork)
        {
            if (passMask == 0b111)
            {
                _agents.CultivationProgress[index] = 0;
                _agents.PendingCultivationWork[index] = 0;
                _agents.CultivationStates[index] = CultivationState.Stable;
                _agents.CultivationBottleneckCauses[index] = CultivationBottleneckCause.None;
                outcome = BreakthroughOutcome.Success;
                AddChronicle(index, ChronicleEventKind.CultivationEntered);
                AddEvent(WorldEventKind.CultivationEntered, index, aptitude);
            }
            else
            {
                outcome = BreakthroughOutcome.Blocked;
                blocker = CultivationBottleneckCause.EntryGate;
            }
        }

        _agents.CultivationTraces[index] = new CultivationTraceState(
            Tick.Value,
            CultivationRules.FormulaRevision,
            BreakthroughClass.Entry,
            outcome,
            RealmStage.QiRefining,
            0,
            1,
            before,
            gain,
            after,
            CultivationRules.RequiredEntryWork,
            0,
            10_000,
            0,
            10_000,
            -1,
            blocker);
    }

    private void AccumulateRealmWork(int index)
    {
        if (_agents.FloorNineCompleted[index])
        {
            GrowRealmComponents(index, CultivationRules.BaseMonthlyWork);
            TryCompleteGreatPerfection(index);
            return;
        }

        var before = _agents.CultivationProgress[index];
        var aptitude = GetInnateAptitudeComposite(index);
        var gain = CultivationRules.GetMonthlyRealmWorkGain(aptitude);
        if (_agents.GrowthPills[index] > 0)
        {
            _agents.GrowthPills[index]--;
            gain = checked(gain + Math.Max(1, gain / 2));
        }

        var requirement = GetCultivationRequirement(index);
        var rawAfter = checked(before + gain);
        var after = Math.Min(requirement, rawAfter);
        var carryover = Math.Max(0, rawAfter - requirement);
        _agents.CultivationProgress[index] = after;
        _agents.PendingCultivationWork[index] = carryover;
        _agents.LastCultivationWorkBefore[index] = before;
        _agents.LastCultivationWorkGain[index] = gain;
        GrowRealmComponents(index, gain);
        _agents.CultivationTraces[index] = new CultivationTraceState(
            Tick.Value,
            CultivationRules.FormulaRevision,
            BreakthroughClass.None,
            BreakthroughOutcome.None,
            _agents.RealmStages[index],
            _agents.RealmLayers[index],
            _agents.RealmLayers[index],
            before,
            gain,
            after,
            requirement,
            carryover,
            0,
            0,
            0,
            -1,
            CultivationBottleneckCause.None);
        if (after < requirement)
        {
            return;
        }

        if (_agents.RealmLayers[index] == RealmRules.MinorStageCount)
        {
            _agents.FloorNineCompleted[index] = true;
            _agents.PendingCultivationWork[index] = 0;
            _agents.CultivationStates[index] = CultivationState.Stable;
            return;
        }

        _agents.CultivationStates[index] = CultivationState.PreparingBreakthrough;
    }

    private void ResolveStableState(int index)
    {
        if (_agents.FloorNineCompleted[index])
        {
            if (!_agents.GreatPerfectionAchieved[index])
            {
                TryCompleteGreatPerfection(index);
                return;
            }

            if (_agents.RealmStages[index] == RealmStage.HalfImmortal)
            {
                _agents.CultivationStates[index] = CultivationState.Completed;
                return;
            }

            _agents.CultivationStates[index] = CultivationState.PreparingBreakthrough;
            return;
        }

        _agents.CultivationStates[index] = _agents.CultivationProgress[index] >= GetCultivationRequirement(index)
            ? CultivationState.PreparingBreakthrough
            : CultivationState.RealmCultivation;
    }

    private void TryCompleteGreatPerfection(int index)
    {
        var blocker = EvaluateComponentGate(index, greatPerfection: true);
        if (blocker != CultivationBottleneckCause.None)
        {
            _agents.CultivationStates[index] = CultivationState.Bottleneck;
            _agents.CultivationBottleneckCauses[index] = blocker;
            RecordBlockedTrace(index, BreakthroughClass.MajorRealm, blocker);
            return;
        }

        _agents.GreatPerfectionAchieved[index] = true;
        _agents.CultivationStates[index] = CultivationState.Stable;
        _agents.CultivationBottleneckCauses[index] = CultivationBottleneckCause.None;
        AddChronicle(index, ChronicleEventKind.GreatPerfectionAchieved);
        AddEvent(
            WorldEventKind.GreatPerfectionAchieved,
            index,
            EncodeRealm(_agents.RealmStages[index], _agents.RealmLayers[index]));
    }

    private void ResolveBreakthroughPreflight(int index)
    {
        var attemptClass = CultivationRules.GetAttemptClass(
            _agents.RealmLayers[index],
            _agents.GreatPerfectionAchieved[index]);
        var blocker = EvaluateBreakthroughPreflight(index, attemptClass);
        if (blocker == CultivationBottleneckCause.None)
        {
            _agents.CultivationStates[index] = CultivationState.PreflightReady;
            _agents.CultivationBottleneckCauses[index] = CultivationBottleneckCause.None;
            return;
        }

        _agents.CultivationStates[index] = CultivationState.Bottleneck;
        _agents.CultivationBottleneckCauses[index] = blocker;
        RecordBlockedTrace(index, attemptClass, blocker);
    }

    private void ResolveBreakthroughAttempt(int index)
    {
        var attemptClass = CultivationRules.GetAttemptClass(
            _agents.RealmLayers[index],
            _agents.GreatPerfectionAchieved[index]);
        var blocker = EvaluateBreakthroughPreflight(index, attemptClass);
        if (blocker != CultivationBottleneckCause.None)
        {
            _agents.CultivationStates[index] = CultivationState.Bottleneck;
            _agents.CultivationBottleneckCauses[index] = blocker;
            RecordBlockedTrace(index, attemptClass, blocker);
            return;
        }

        _agents.CultivationStates[index] = CultivationState.AttemptingBreakthrough;
        var componentCount = CultivationRules.GetComponents(_agents.RealmStages[index]).Count;
        var componentAverage = _agents.GetCultivationComponentAverage(index, componentCount);
        var finalChance = CultivationRules.GetBreakthroughChanceBasisPoints(
            attemptClass,
            _agents.RealmStages[index],
            GetInnateAptitudeComposite(index),
            componentAverage,
            out var baseChance,
            out var modifier);
        var roll = _random.NextInt(10_000);
        var succeeded = roll < finalChance;
        var sourceRealm = _agents.RealmStages[index];
        var sourceFloor = _agents.RealmLayers[index];
        var destinationFloor = sourceFloor;

        if (succeeded)
        {
            if (attemptClass == BreakthroughClass.MajorRealm)
            {
                CompleteMajorRealmBreakthrough(index);
                destinationFloor = 1;
            }
            else
            {
                destinationFloor = CompleteMinorStageBreakthrough(index, sourceFloor);
            }

            _agents.BottleneckFailures[index] = 0;
            _totalBreakthroughs++;
        }
        else
        {
            if (_agents.BottleneckFailures[index] == 0)
            {
                AddChronicle(index, ChronicleEventKind.BreakthroughFailure, finalChance);
            }

            _agents.BottleneckFailures[index] = checked((ushort)(_agents.BottleneckFailures[index] + 1));
            _agents.CultivationStates[index] = CultivationState.PreparingBreakthrough;
            _agents.CultivationBottleneckCauses[index] = CultivationBottleneckCause.None;
            AddEvent(WorldEventKind.BreakthroughFailed, index, finalChance);
        }

        _agents.CultivationTraces[index] = new CultivationTraceState(
            Tick.Value,
            CultivationRules.FormulaRevision,
            attemptClass,
            succeeded ? BreakthroughOutcome.Success : BreakthroughOutcome.Failure,
            sourceRealm,
            sourceFloor,
            destinationFloor,
            _agents.LastCultivationWorkBefore[index],
            _agents.LastCultivationWorkGain[index],
            _agents.CultivationProgress[index],
            RealmRules.GetMinorStageRequiredWork(sourceRealm, sourceFloor),
            _agents.PendingCultivationWork[index],
            baseChance,
            modifier,
            finalChance,
            roll,
            CultivationBottleneckCause.None);
    }

    private byte CompleteMinorStageBreakthrough(int index, byte sourceFloor)
    {
        var carryover = _agents.PendingCultivationWork[index];
        var policy = CultivationRules.GetCarryoverPolicy(sourceFloor);
        var destinationFloor = checked((byte)(sourceFloor + 1));
        var destinationRequirement = RealmRules.GetMinorStageRequiredWork(
            _agents.RealmStages[index],
            destinationFloor);
        if (policy == CarryoverPolicy.NoCarryover)
        {
            carryover = 0;
        }
        else if (policy == CarryoverPolicy.LimitedCarryover)
        {
            carryover = Math.Min(carryover, destinationRequirement * CultivationRules.LimitedCarryoverPercent / 100);
        }

        _agents.RealmLayers[index] = destinationFloor;
        _agents.CultivationProgress[index] = Math.Min(carryover, destinationRequirement);
        _agents.PendingCultivationWork[index] = Math.Max(0, carryover - destinationRequirement);
        _agents.CultivationStates[index] = CultivationState.Stable;
        _agents.CultivationBottleneckCauses[index] = CultivationBottleneckCause.None;
        AddChronicle(index, ChronicleEventKind.MinorStageBreakthrough);
        AddEvent(
            WorldEventKind.MinorStageBreakthrough,
            index,
            EncodeRealm(_agents.RealmStages[index], destinationFloor));
        if (sourceFloor is 3 or 6)
        {
            var failures = _agents.BottleneckFailures[index];
            AddChronicle(index, ChronicleEventKind.BottleneckBreakthrough, failures);
            AddEvent(WorldEventKind.BottleneckBreakthrough, index, failures);
        }

        return destinationFloor;
    }

    private void CompleteMajorRealmBreakthrough(int index)
    {
        if (_agents.RealmStages[index] >= RealmStage.HalfImmortal)
        {
            _agents.CultivationStates[index] = CultivationState.Completed;
            return;
        }

        if (_agents.RealmStages[index] == RealmStage.QiRefining)
        {
            _agents.FoundationRoutes[index] = SelectFoundationRoute(index);
        }

        _agents.RealmStages[index]++;
        _agents.RealmLayers[index] = 1;
        _agents.CultivationProgress[index] = 0;
        _agents.PendingCultivationWork[index] = 0;
        _agents.FloorNineCompleted[index] = false;
        _agents.GreatPerfectionAchieved[index] = false;
        _agents.CultivationStates[index] = CultivationState.Stabilizing;
        _agents.CultivationBottleneckCauses[index] = CultivationBottleneckCause.None;
        InitializeRealmComponents(index);
        AddChronicle(index, ChronicleEventKind.MajorRealmBreakthrough);
        AddEvent(
            WorldEventKind.MajorRealmBreakthrough,
            index,
            EncodeRealm(_agents.RealmStages[index], 1));
    }

    private CultivationBottleneckCause EvaluateBreakthroughPreflight(
        int index,
        BreakthroughClass attemptClass)
    {
        if (attemptClass == BreakthroughClass.MajorRealm)
        {
            if (!_agents.FloorNineCompleted[index] || !_agents.GreatPerfectionAchieved[index])
            {
                return CultivationBottleneckCause.Stability;
            }

            if (_agents.RealmStages[index] == RealmStage.QiRefining
                && _agents.TechniqueMasteries[index] < 250)
            {
                return CultivationBottleneckCause.Method;
            }
        }
        else if (_agents.CultivationProgress[index] < GetCultivationRequirement(index))
        {
            return CultivationBottleneckCause.Stability;
        }

        if (_agents.Health[index] * 2 < _agents.MaximumHealth[index])
        {
            return CultivationBottleneckCause.Body;
        }

        if (_agents.Qi[index] * 4 < _agents.MaximumQi[index])
        {
            return CultivationBottleneckCause.SpiritualRoot;
        }

        return EvaluateComponentGate(index, greatPerfection: false);
    }

    private CultivationBottleneckCause EvaluateComponentGate(int index, bool greatPerfection)
    {
        var componentCount = CultivationRules.GetComponents(_agents.RealmStages[index]).Count;
        if (componentCount == 0)
        {
            return CultivationBottleneckCause.None;
        }

        var threshold = CultivationRules.GetRequiredComponentThresholdHundredths(
            _agents.RealmLayers[index],
            greatPerfection);
        return _agents.GetCultivationComponentAverage(index, componentCount) >= threshold
            ? CultivationBottleneckCause.None
            : CultivationBottleneckCause.RealmComponent;
    }

    private void CompleteStabilization(int index)
    {
        // 고정 연수·Work 없이 최소 한 번의 실제 평가와 commit을 보장한다.
        var componentCount = CultivationRules.GetComponents(_agents.RealmStages[index]).Count;
        for (var component = 0; component < componentCount; component++)
        {
            var value = _agents.GetCultivationComponentValue(index, component);
            var limit = _agents.GetCultivationComponentLimit(index, component);
            _agents.SetCultivationComponent(index, component, Math.Min(limit, value + 50), limit);
        }

        _agents.CultivationStates[index] = CultivationState.Stable;
    }

    private void GrowRealmComponents(int index, int workGain)
    {
        var components = CultivationRules.GetComponents(_agents.RealmStages[index]);
        if (components.Count == 0)
        {
            return;
        }

        var requirement = GetCultivationRequirement(index);
        var growth = Math.Max(1, checked(workGain * 1_200 / Math.Max(1, requirement)));
        for (var component = 0; component < components.Count; component++)
        {
            var current = _agents.GetCultivationComponentValue(index, component);
            var recomputedLimit = CalculateComponentLimit(index, component);
            var effectiveLimit = Math.Max(current, recomputedLimit);
            _agents.SetCultivationComponent(
                index,
                component,
                Math.Min(effectiveLimit, current + growth),
                effectiveLimit);
        }
    }

    private void InitializeRealmComponents(int index)
    {
        var components = CultivationRules.GetComponents(_agents.RealmStages[index]);
        for (var component = 0; component < CultivationRules.RealmComponentSlotCount; component++)
        {
            if (component >= components.Count)
            {
                _agents.SetCultivationComponent(index, component, 0, 0);
                continue;
            }

            var source = GetComponentSourceAptitude(index, component);
            var limit = CalculateComponentLimit(index, component);
            var initial = Math.Clamp(3_000 + (source / 5), 2_500, limit);
            _agents.SetCultivationComponent(index, component, initial, limit);
        }

        var formationCount = _agents.RealmStages[index] switch
        {
            RealmStage.FoundationEstablishment => 10,
            RealmStage.GoldenCore => 7,
            _ => 0,
        };
        var formationStart = index * CultivationRules.FormationSlotCount;
        for (var element = 0; element < CultivationRules.FormationSlotCount; element++)
        {
            _agents.FormationValues[formationStart + element] = checked((ushort)(element < formationCount
                ? Math.Clamp(
                    (GetComponentSourceAptitude(index, element % Math.Max(1, components.Count))
                        + GetInnateAptitudeComposite(index)) / 2,
                    0,
                    QualityValue.MaximumHundredths)
                : 0));
        }
    }

    private int CalculateComponentLimit(int index, int component)
    {
        var source = GetComponentSourceAptitude(index, component);
        return Math.Clamp(6_000 + ((source - 5_000) / 2), 4_000, 10_000);
    }

    private int GetComponentSourceAptitude(int index, int component) => component switch
    {
        0 => _agents.SpiritualRoots[index],
        1 => _agents.Comprehensions[index],
        2 => (_agents.Minds[index] + _agents.Souls[index]) / 2,
        _ => (_agents.Bones[index] + _agents.Constitutions[index]) / 2,
    };

    private FoundationRoute SelectFoundationRoute(int index)
    {
        var human = (_agents.Bones[index] + _agents.Constitutions[index]) / 2;
        var earth = (_agents.Minds[index] + _agents.Souls[index]) / 2;
        var heaven = (_agents.SpiritualRoots[index] + _agents.Comprehensions[index]) / 2;
        return human >= earth && human >= heaven
            ? FoundationRoute.Human
            : earth >= heaven ? FoundationRoute.Earth : FoundationRoute.Heaven;
    }

    private byte EvaluateEntryGatePassMask(int index)
    {
        var gate1 = CultivationRules.EvaluateEntryGate(
            1,
            _agents.SpiritualRoots[index],
            (_agents.Comprehensions[index] + _agents.Minds[index] + _agents.Souls[index]) / 3);
        var gate2 = CultivationRules.EvaluateEntryGate(
            2,
            (_agents.Bones[index] + _agents.Constitutions[index]) / 2,
            (_agents.SpiritualRoots[index] + _agents.Minds[index]) / 2);
        var gate3 = CultivationRules.EvaluateEntryGate(
            3,
            _agents.Comprehensions[index],
            (_agents.SpiritualRoots[index] + _agents.Souls[index]) / 2);
        return checked((byte)((gate1.Passed ? 1 : 0)
            | (gate2.Passed ? 2 : 0)
            | (gate3.Passed ? 4 : 0)));
    }

    private CultivationGateSnapshot[] CreateEntryGateSnapshots(int index) =>
    [
        CultivationRules.EvaluateEntryGate(
            1,
            _agents.SpiritualRoots[index],
            (_agents.Comprehensions[index] + _agents.Minds[index] + _agents.Souls[index]) / 3),
        CultivationRules.EvaluateEntryGate(
            2,
            (_agents.Bones[index] + _agents.Constitutions[index]) / 2,
            (_agents.SpiritualRoots[index] + _agents.Minds[index]) / 2),
        CultivationRules.EvaluateEntryGate(
            3,
            _agents.Comprehensions[index],
            (_agents.SpiritualRoots[index] + _agents.Souls[index]) / 2),
    ];

    private RealmComponentSnapshot[] CreateRealmComponentSnapshots(int index)
    {
        var definitions = CultivationRules.GetComponents(_agents.RealmStages[index]);
        var result = new RealmComponentSnapshot[definitions.Count];
        for (var component = 0; component < result.Length; component++)
        {
            result[component] = new RealmComponentSnapshot(
                definitions[component].StableId,
                definitions[component].KoreanName,
                _agents.GetCultivationComponentValue(index, component),
                _agents.GetCultivationComponentLimit(index, component));
        }

        return result;
    }

    private CultivationReadProjection CreateCultivationProjection(int index) => new(
        GetCultivationLifecyclePhase(_agents.CultivationStates[index]),
        _agents.RealmLayers[index],
        _agents.LastCultivationWorkBefore[index],
        _agents.LastCultivationWorkGain[index],
        _agents.CultivationProgress[index],
        GetCultivationRequirement(index),
        _agents.PendingCultivationWork[index],
        GetInnateAptitudeComposite(index),
        RealmRules.GetProfile(_agents.RealmStages[index]).TargetCultivationYears,
        _agents.FloorNineCompleted[index],
        _agents.GreatPerfectionAchieved[index],
        _agents.FoundationRoutes[index],
        CreateEntryGateSnapshots(index),
        CreateRealmComponentSnapshots(index),
        _agents.CultivationTraces[index]);

    private static CultivationLifecyclePhase GetCultivationLifecyclePhase(CultivationState state) => state switch
    {
        CultivationState.PreCultivation => CultivationLifecyclePhase.PreCultivation,
        CultivationState.EntryTraining => CultivationLifecyclePhase.EntryTraining,
        CultivationState.RealmCultivation => CultivationLifecyclePhase.RealmCultivation,
        CultivationState.Bottleneck => CultivationLifecyclePhase.Bottleneck,
        CultivationState.PreparingBreakthrough or CultivationState.PreflightReady =>
            CultivationLifecyclePhase.BreakthroughPreparation,
        CultivationState.AttemptingBreakthrough => CultivationLifecyclePhase.Attempt,
        CultivationState.Stabilizing => CultivationLifecyclePhase.Stabilizing,
        _ => CultivationLifecyclePhase.Stable,
    };

    private void RecordBlockedTrace(
        int index,
        BreakthroughClass attemptClass,
        CultivationBottleneckCause blocker)
    {
        _agents.CultivationTraces[index] = new CultivationTraceState(
            Tick.Value,
            CultivationRules.FormulaRevision,
            attemptClass,
            BreakthroughOutcome.Blocked,
            _agents.RealmStages[index],
            _agents.RealmLayers[index],
            _agents.RealmLayers[index],
            _agents.LastCultivationWorkBefore[index],
            _agents.LastCultivationWorkGain[index],
            _agents.CultivationProgress[index],
            GetCultivationRequirement(index),
            _agents.PendingCultivationWork[index],
            0,
            0,
            0,
            -1,
            blocker);
    }

    private void TryResolveBottleneckAfterAction(int index, AgentActivity resolvedActivity)
    {
        if (_agents.CultivationStates[index] != CultivationState.Bottleneck)
        {
            return;
        }

        var attemptClass = CultivationRules.GetAttemptClass(
            _agents.RealmLayers[index],
            _agents.GreatPerfectionAchieved[index]);
        var blocker = _agents.FloorNineCompleted[index]
            && !_agents.GreatPerfectionAchieved[index]
            ? EvaluateComponentGate(index, greatPerfection: true)
            : EvaluateBreakthroughPreflight(index, attemptClass);
        if (blocker != CultivationBottleneckCause.None)
        {
            _agents.CultivationBottleneckCauses[index] = blocker;
            return;
        }

        _agents.CultivationStates[index] = _agents.FloorNineCompleted[index]
            && !_agents.GreatPerfectionAchieved[index]
            ? CultivationState.Stable
            : CultivationState.PreparingBreakthrough;
        _agents.CultivationBottleneckCauses[index] = CultivationBottleneckCause.None;
    }

    private int GetInnateAptitudeComposite(int index) =>
        CultivationRules.GetInnateAptitudeCompositeHundredths(
            _agents.SpiritualRoots[index],
            _agents.Bones[index],
            _agents.Constitutions[index],
            _agents.Comprehensions[index],
            _agents.Minds[index],
            _agents.Souls[index]);

    private int GetCultivationRequirement(int index) =>
        _agents.CultivationStates[index] is CultivationState.PreCultivation or CultivationState.EntryTraining
            ? CultivationRules.RequiredEntryWork
            : RealmRules.GetMinorStageRequiredWork(
                _agents.RealmStages[index],
                _agents.RealmLayers[index]);
}
