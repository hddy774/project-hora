using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private void ResolveCultivation(
        int index,
        long currentMonth,
        CultivationWorkResult? precomputedWork = null)
    {
        if (precomputedWork is { } result)
        {
            ApplyParallelCultivationWork(index, result);
            AdvanceCultivationPlanProgress(index, result.ProcessedMonths);
            return;
        }

        var pulseMonths = GetCultivationPulseMonths(index, currentMonth);
        if (pulseMonths > 1
            && TryAccumulateRealmWorkBulk(index, pulseMonths, out var processedMonths))
        {
            AdvanceCultivationPlanProgress(index, processedMonths);
            return;
        }

        for (var month = 0; month < pulseMonths; month++)
        {
            ResolveCultivationMonth(index);
            if (_agents.CultivationStates[index] == CultivationState.PreCultivation
                && !HasReachedAge(index, CultivationRules.EntryAgeYears))
            {
                break;
            }

            AdvanceCultivationPlanProgress(index, 1);
            if (_agents.CultivationStates[index] is CultivationState.PreparingBreakthrough
                or CultivationState.PreflightReady
                or CultivationState.AttemptingBreakthrough
                or CultivationState.Stabilizing
                or CultivationState.Completed)
            {
                break;
            }
        }
    }

    private void ResolveCultivationMonth(int index)
    {
        switch (_agents.CultivationStates[index])
        {
            case CultivationState.PreCultivation:
                if (!HasReachedAge(index, CultivationRules.EntryAgeYears))
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
                if (_agents.CultivationBottleneckCauses[index] == CultivationBottleneckCause.EntryGate)
                {
                    TemperEntryGates(index);
                }
                else if (_agents.CultivationBottleneckCauses[index] == CultivationBottleneckCause.RealmComponent)
                {
                    GrowRealmComponents(
                        index,
                        _balance.DeveloperModeEnabled
                            ? WorldBalanceRules.ApplyPercent(
                                CultivationRules.BaseMonthlyWork,
                                _balance.CultivationWorkPercent)
                            : CultivationRules.BaseMonthlyWork);
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
        var cultivationAptitudes = _agents.GetContextAptitudes(index, combat: false);
        var body = (
            cultivationAptitudes.Bone.Hundredths
            + cultivationAptitudes.Constitution.Hundredths) / 2;
        var environmentFactor = CultivationRules.GetEnvironmentFactorBasisPoints(
            _atlas.GetCultivationEnvironmentQualityHundredths(_agents.LocationIds[index]));
        var planFactor = ConsumeCultivationPlanResourcesAndGetFactor(index);
        var gain = CultivationRules.GetEntryWorkGain(
            aptitude,
            _agents.TechniqueQualities[index],
            body,
            environmentFactor,
            planFactor);
        if (_balance.DeveloperModeEnabled)
        {
            gain = WorldBalanceRules.ApplyPercent(gain, _balance.CultivationWorkPercent);
        }
        var after = Math.Min(CultivationRules.RequiredEntryWork, checked(before + gain));
        var appliedGain = after - before;
        _agents.CultivationProgress[index] = after;
        _agents.LastCultivationWorkBefore[index] = before;
        _agents.LastCultivationWorkGain[index] = appliedGain;
        var passMask = before == 0
            ? EvaluateEntryGatePassMask(index)
            : _agents.EntryGatePassMasks[index];
        if (before == 0)
        {
            _agents.EntryGatePassMasks[index] = passMask;
        }
        var outcome = BreakthroughOutcome.None;
        var blocker = CultivationBottleneckCause.None;

        if (after >= CultivationRules.RequiredEntryWork)
        {
            if (passMask == 0b111)
            {
                CompleteCultivationEntry(index, aptitude);
                outcome = BreakthroughOutcome.Success;
            }
            else
            {
                _agents.CultivationStates[index] = CultivationState.Bottleneck;
                _agents.CultivationBottleneckCauses[index] = CultivationBottleneckCause.EntryGate;
                outcome = BreakthroughOutcome.Blocked;
                blocker = CultivationBottleneckCause.EntryGate;
            }
        }

        CommitCultivationTrace(
            index,
            CultivationFormulaKind.ProgressAccumulation,
            new CultivationTraceState(
            Tick.Value,
            CultivationRules.FormulaRevision,
            BreakthroughClass.Entry,
            outcome,
            RealmStage.QiRefining,
            0,
            1,
            before,
            appliedGain,
            after,
            CultivationRules.RequiredEntryWork,
            0,
            10_000,
            0,
            10_000,
            -1,
            blocker,
            EnvironmentFactorBasisPoints: environmentFactor,
            EnvironmentAvailability: CultivationValueAvailability.Actual));
    }

    private void TemperEntryGates(int index)
    {
        var previousMask = _agents.EntryGatePassMasks[index];
        var temperingGain = CultivationRules.BaseEntryGateTemperingGainHundredths
            + (_agents.GetDerivedAptitude(
                index,
                AptitudeDerivedAbility.ComprehensionLearning) / 100);
        _agents.AddFailedEntryGateTemperings(index, previousMask, temperingGain);

        var passMask = EvaluateEntryGatePassMask(index);
        _agents.EntryGatePassMasks[index] = passMask;
        _agents.LastCultivationWorkBefore[index] = CultivationRules.RequiredEntryWork;
        _agents.LastCultivationWorkGain[index] = 0;
        var outcome = BreakthroughOutcome.Blocked;
        var blocker = CultivationBottleneckCause.EntryGate;
        if (passMask == 0b111)
        {
            CompleteCultivationEntry(index, GetInnateAptitudeComposite(index));
            outcome = BreakthroughOutcome.Success;
            blocker = CultivationBottleneckCause.None;
        }

        CommitCultivationTrace(
            index,
            CultivationFormulaKind.BottleneckState,
            new CultivationTraceState(
            Tick.Value,
            CultivationRules.FormulaRevision,
            BreakthroughClass.Entry,
            outcome,
            RealmStage.QiRefining,
            0,
            1,
            CultivationRules.RequiredEntryWork,
            0,
            CultivationRules.RequiredEntryWork,
            CultivationRules.RequiredEntryWork,
            0,
            10_000,
            0,
            10_000,
            -1,
            blocker));
    }

    private void CompleteCultivationEntry(int index, int aptitude)
    {
        _agents.CultivationProgress[index] = 0;
        _agents.PendingCultivationWork[index] = 0;
        _agents.CultivationStates[index] = CultivationState.Stable;
        _agents.CultivationBottleneckCauses[index] = CultivationBottleneckCause.None;
        AddChronicle(index, ChronicleEventKind.CultivationEntered);
        AddEvent(WorldEventKind.CultivationEntered, index, aptitude);
    }

    private void AccumulateRealmWork(int index)
    {
        if (_agents.FloorNineCompleted[index])
        {
            GrowRealmComponents(
                index,
                _balance.DeveloperModeEnabled
                    ? WorldBalanceRules.ApplyPercent(
                        CultivationRules.BaseMonthlyWork,
                        _balance.CultivationWorkPercent)
                    : CultivationRules.BaseMonthlyWork);
            TryCompleteGreatPerfection(index);
            return;
        }

        var before = _agents.CultivationProgress[index];
        var aptitude = GetInnateAptitudeComposite(index);
        var bodyCondition = CultivationPlanRules.GetBodyConditionBasisPoints(
            _agents.Health[index],
            Math.Max(1, _agents.MaximumHealth[index]));
        var environmentQuality = _atlas.GetCultivationEnvironmentQualityHundredths(
            _agents.LocationIds[index]);
        var environmentFactor = CultivationRules.GetEnvironmentFactorBasisPoints(
            environmentQuality);
        var planFactor = ConsumeCultivationPlanResourcesAndGetFactor(index);
        var gain = CultivationRules.GetMonthlyRealmWorkGain(
            aptitude,
            _agents.TechniqueQualities[index],
            environmentQuality,
            bodyCondition,
            planFactor);
        if (_balance.DeveloperModeEnabled)
        {
            gain = WorldBalanceRules.ApplyPercent(gain, _balance.CultivationWorkPercent);
        }
        if (!_agents.CultivationPlans[index].IsActive
            && _agents.GrowthPills[index] > 0)
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
            before,
            gain,
            after,
            requirement,
            carryover,
            0,
            0,
            0,
            -1,
            CultivationBottleneckCause.None,
            EnvironmentFactorBasisPoints: environmentFactor,
            EnvironmentAvailability: CultivationValueAvailability.Actual));
        if (after < requirement)
        {
            return;
        }

        _agents.CultivationStates[index] = CultivationState.PreparingBreakthrough;
    }

    private bool TryAccumulateRealmWorkBulk(
        int index,
        int requestedMonths,
        out int processedMonths)
    {
        processedMonths = 0;
        var plan = _agents.CultivationPlans[index];
        if (_agents.CultivationStates[index] != CultivationState.RealmCultivation
            || _agents.FloorNineCompleted[index]
            || !plan.IsActive
            || plan.ResourcePolicy != CultivationResourcePolicy.None)
        {
            return false;
        }

        var before = _agents.CultivationProgress[index];
        var aptitude = GetInnateAptitudeComposite(index);
        var bodyCondition = CultivationPlanRules.GetBodyConditionBasisPoints(
            _agents.Health[index],
            Math.Max(1, _agents.MaximumHealth[index]));
        var environmentQuality = _atlas.GetCultivationEnvironmentQualityHundredths(
            _agents.LocationIds[index]);
        var environmentFactor = CultivationRules.GetEnvironmentFactorBasisPoints(
            environmentQuality);
        var monthlyGain = CultivationRules.GetMonthlyRealmWorkGain(
            aptitude,
            _agents.TechniqueQualities[index],
            environmentQuality,
            bodyCondition,
            plan.FrozenConditionFactorBasisPoints);
        if (_balance.DeveloperModeEnabled)
        {
            monthlyGain = WorldBalanceRules.ApplyPercent(
                monthlyGain,
                _balance.CultivationWorkPercent);
        }

        var requirement = GetCultivationRequirement(index);
        var remaining = Math.Max(1, requirement - before);
        var monthsToBoundary = checked((remaining + monthlyGain - 1) / monthlyGain);
        processedMonths = Math.Min(requestedMonths, monthsToBoundary);
        var totalGain = checked(monthlyGain * processedMonths);
        var rawAfter = checked(before + totalGain);
        var after = Math.Min(requirement, rawAfter);
        var carryover = Math.Max(0, rawAfter - requirement);
        _agents.CultivationProgress[index] = after;
        _agents.PendingCultivationWork[index] = carryover;
        _agents.LastCultivationWorkBefore[index] = before;
        _agents.LastCultivationWorkGain[index] = totalGain;
        GrowRealmComponentsBulk(index, monthlyGain, processedMonths, requirement);
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
                before,
                totalGain,
                after,
                requirement,
                carryover,
                0,
                0,
                0,
                -1,
                CultivationBottleneckCause.None,
                EnvironmentFactorBasisPoints: environmentFactor,
                EnvironmentAvailability: CultivationValueAvailability.Actual));
        if (after >= requirement)
        {
            _agents.CultivationStates[index] = CultivationState.PreparingBreakthrough;
        }

        return true;
    }

    private void GrowRealmComponentsBulk(
        int index,
        int monthlyWorkGain,
        int months,
        int requirement)
    {
        var components = CultivationRules.GetComponents(_agents.RealmStages[index]);
        if (components.Count == 0)
        {
            return;
        }

        var monthlyGrowth = Math.Max(
            1,
            checked(monthlyWorkGain * 1_200 / Math.Max(1, requirement)));
        var growth = checked(monthlyGrowth * months);
        for (var component = 0; component < components.Count; component++)
        {
            var current = _agents.GetCultivationComponentValue(index, component);
            var recomputedLimit = CalculateComponentLimit(index, component);
            var effectiveLimit = Math.Max(current, recomputedLimit);
            _agents.SetCultivationComponent(
                index,
                component,
                Math.Min(effectiveLimit, checked(current + growth)),
                effectiveLimit);
        }
    }

    private CultivationGateSnapshot[] CreateEntryGateSnapshots(int index)
    {
        var aptitudes = _agents.GetContextAptitudes(index, combat: false);
        return
        [
            CultivationRules.EvaluateEntryGate(
                1,
                aptitudes.SpiritualRoot.Hundredths,
                (aptitudes.Comprehension.Hundredths
                    + aptitudes.Mind.Hundredths
                    + aptitudes.Soul.Hundredths) / 3,
                _agents.GetEntryGateTempering(index, 0)),
            CultivationRules.EvaluateEntryGate(
                2,
                (aptitudes.Bone.Hundredths + aptitudes.Constitution.Hundredths) / 2,
                (aptitudes.SpiritualRoot.Hundredths + aptitudes.Mind.Hundredths) / 2,
                _agents.GetEntryGateTempering(index, 1)),
            CultivationRules.EvaluateEntryGate(
                3,
                aptitudes.Comprehension.Hundredths,
                (aptitudes.SpiritualRoot.Hundredths + aptitudes.Soul.Hundredths) / 2,
                _agents.GetEntryGateTempering(index, 2)),
        ];
    }

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

    private CultivationReadProjection CreateCultivationProjection(
        int index,
        CultivationProjectionSections sections)
    {
        var phase = GetCultivationLifecyclePhase(_agents.CultivationStates[index]);
        var includePipeline = (sections & CultivationProjectionSections.Pipeline) != 0;
        return new CultivationReadProjection(
            phase,
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
            includePipeline ? CreateEntryGateSnapshots(index) : [],
            includePipeline ? CreateRealmComponentSnapshots(index) : [],
            _agents.CultivationTraces[index],
            _agents.CultivationAuthorityRevisions[index],
            includePipeline ? CreateFoundationFormationProjection(index) : null,
            _agents.CultivationExperience[index],
            includePipeline ? CreateCultivationPlanSnapshot(index) : null,
            (sections & CultivationProjectionSections.DetailProgress) != 0
                ? CultivationDetailProgressRules.Create(
                    phase,
                    _agents.RealmStages[index],
                    _agents.RealmLayers[index],
                    _agents.CultivationProgress[index],
                    _agents.FloorNineCompleted[index],
                    _agents.GreatPerfectionAchieved[index])
                : null);
    }

    private FoundationFormationSnapshot? CreateFoundationFormationProjection(int index)
    {
        if (_agents.FoundationFormations.CreateCheckpoint(index) is not { } formation)
        {
            return null;
        }

        var start = index * CultivationRules.FormationSlotCount;
        var quality = new int[CultivationRules.FormationSlotCount];
        for (var component = 0; component < quality.Length; component++)
        {
            quality[component] = _agents.FormationValues[start + component];
        }

        return new FoundationFormationSnapshot(
            FoundationMethodRules.StableId,
            formation.MethodRevision,
            formation.CompletedSteps,
            formation.Source,
            quality,
            RequiresStabilization: true);
    }

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
        CommitCultivationTrace(
            index,
            CultivationFormulaKind.BottleneckState,
            new CultivationTraceState(
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
            blocker));
    }

    private void RecordPreflightReadyTrace(int index, BreakthroughClass attemptClass)
    {
        CommitCultivationTrace(
            index,
            CultivationFormulaKind.BreakthroughReadiness,
            new CultivationTraceState(
                Tick.Value,
                CultivationRules.FormulaRevision,
                attemptClass,
                BreakthroughOutcome.None,
                _agents.RealmStages[index],
                _agents.RealmLayers[index],
                _agents.RealmLayers[index],
                _agents.CultivationProgress[index],
                0,
                _agents.CultivationProgress[index],
                GetCultivationRequirement(index),
                _agents.PendingCultivationWork[index],
                0,
                0,
                0,
                -1,
                CultivationBottleneckCause.None));
    }

    private void CommitCultivationTrace(
        int index,
        CultivationFormulaKind formulaKind,
        CultivationTraceState trace)
    {
        var revisionBefore = _agents.CultivationAuthorityRevisions[index];
        var revisionAfter = checked(revisionBefore + 1);
        _agents.CultivationAuthorityRevisions[index] = revisionAfter;
        _agents.CultivationTraces[index] = trace with
        {
            FormulaKind = formulaKind,
            AuthorityRevisionBefore = revisionBefore,
            AuthorityRevisionAfter = revisionAfter,
        };
    }

    private void TryResolveBottleneckAfterAction(int index, AgentActivity resolvedActivity)
    {
        if (_agents.CultivationStates[index] != CultivationState.Bottleneck)
        {
            return;
        }

        if (_agents.CultivationBottleneckCauses[index] == CultivationBottleneckCause.EntryGate)
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

    private bool HasEnteredCultivation(int index) =>
        _agents.CultivationStates[index] is not (
            CultivationState.PreCultivation or CultivationState.EntryTraining)
        && !(_agents.CultivationStates[index] == CultivationState.Bottleneck
            && _agents.CultivationBottleneckCauses[index] == CultivationBottleneckCause.EntryGate);

    private int GetCultivationRequirement(int index) =>
        _agents.CultivationStates[index] is CultivationState.PreCultivation or CultivationState.EntryTraining
            || (_agents.CultivationStates[index] == CultivationState.Bottleneck
                && _agents.CultivationBottleneckCauses[index] == CultivationBottleneckCause.EntryGate)
            ? CultivationRules.RequiredEntryWork
            : RealmRules.GetMinorStageRequiredWork(
                _agents.RealmStages[index],
                _agents.RealmLayers[index]);
}
