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
        var gain = CultivationRules.GetEntryWorkGain(
            aptitude,
            _agents.TechniqueQualities[index],
            body,
            environmentFactor);
        if (_balance.DeveloperModeEnabled)
        {
            gain = WorldBalanceRules.ApplyPercent(gain, _balance.CultivationWorkPercent);
        }
        var after = Math.Min(CultivationRules.RequiredEntryWork, checked(before + gain));
        var appliedGain = after - before;
        _agents.CultivationProgress[index] = after;
        _agents.LastCultivationWorkBefore[index] = before;
        _agents.LastCultivationWorkGain[index] = appliedGain;
        var passMask = EvaluateEntryGatePassMask(index);
        _agents.EntryGatePassMasks[index] = passMask;
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
        for (var gateIndex = 0; gateIndex < CultivationRules.EntryGateSlotCount; gateIndex++)
        {
            if ((previousMask & (1 << gateIndex)) == 0)
            {
                _agents.AddEntryGateTempering(index, gateIndex, temperingGain);
            }
        }

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
        var bodyCondition = checked(
            _agents.Health[index] * 10_000 / Math.Max(1, _agents.MaximumHealth[index]));
        var environmentQuality = _atlas.GetCultivationEnvironmentQualityHundredths(
            _agents.LocationIds[index]);
        var environmentFactor = CultivationRules.GetEnvironmentFactorBasisPoints(
            environmentQuality);
        var gain = CultivationRules.GetMonthlyRealmWorkGain(
            aptitude,
            _agents.TechniqueQualities[index],
            environmentQuality,
            bodyCondition);
        if (_balance.DeveloperModeEnabled)
        {
            gain = WorldBalanceRules.ApplyPercent(gain, _balance.CultivationWorkPercent);
        }
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
            RecordBlockedTrace(index, BreakthroughClass.GreatPerfectionCompletion, blocker);
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
        CommitCultivationTrace(
            index,
            CultivationFormulaKind.BottleneckState,
            new CultivationTraceState(
                Tick.Value,
                CultivationRules.FormulaRevision,
                BreakthroughClass.GreatPerfectionCompletion,
                BreakthroughOutcome.Success,
                _agents.RealmStages[index],
                _agents.RealmLayers[index],
                _agents.RealmLayers[index],
                _agents.CultivationProgress[index],
                0,
                _agents.CultivationProgress[index],
                GetCultivationRequirement(index),
                _agents.PendingCultivationWork[index],
                10_000,
                0,
                10_000,
                -1,
                CultivationBottleneckCause.None));
    }

    private void ResolveBreakthroughPreflight(int index)
    {
        var attemptClass = CultivationRules.GetAttemptClass(
            _agents.RealmLayers[index],
            _agents.GreatPerfectionAchieved[index]);
        BindFoundationRouteIfNeeded(index, attemptClass);
        var blocker = EvaluateBreakthroughPreflight(index, attemptClass);
        if (blocker == CultivationBottleneckCause.None)
        {
            _agents.CultivationStates[index] = CultivationState.PreflightReady;
            _agents.CultivationBottleneckCauses[index] = CultivationBottleneckCause.None;
            RecordPreflightReadyTrace(index, attemptClass);
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
        BindFoundationRouteIfNeeded(index, attemptClass);
        var blocker = EvaluateBreakthroughPreflight(index, attemptClass);
        if (blocker != CultivationBottleneckCause.None)
        {
            _agents.CultivationStates[index] = CultivationState.Bottleneck;
            _agents.CultivationBottleneckCauses[index] = blocker;
            RecordBlockedTrace(index, attemptClass, blocker);
            return;
        }

        _agents.CultivationStates[index] = CultivationState.AttemptingBreakthrough;
        var effectResolution = CultivationAttemptEffectRegistry.Resolve(
            attemptClass,
            ReadOnlySpan<CultivationAttemptEffectCandidate>.Empty);
        var finalChance = CultivationRules.GetBreakthroughChanceBasisPoints(
            attemptClass,
            _agents.RealmStages[index],
            effectResolution,
            out var baseChance,
            out var modifier);
        var roll = _random.NextInt(10_000);
        var succeeded = roll < finalChance;
        var sourceRealm = _agents.RealmStages[index];
        var sourceFloor = _agents.RealmLayers[index];
        var destinationFloor = sourceFloor;
        var foundationSource = attemptClass == BreakthroughClass.MajorRealm
            && sourceRealm == RealmStage.QiRefining
                ? CreateFoundationSourceSnapshot(index, _agents.FoundationRoutes[index])
                : (FoundationSourceSnapshot?)null;
        CultivationFailureResult? failureResult = null;

        if (succeeded)
        {
            if (attemptClass == BreakthroughClass.MajorRealm)
            {
                CompleteMajorRealmBreakthrough(index, foundationSource);
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
            var failure = CultivationFailureRiskRegistry.Resolve(attemptClass);
            failureResult = failure;
            if (failure.Effect != CultivationFailureEffectKind.CleanFailure
                || failure.WorkLost != 0
                || failure.ComponentLostHundredths != 0
                || failure.InjuryPoints != 0
                || failure.FatalBodyLoss
                || failure.RetryBlockMonths != 0)
            {
                throw new InvalidOperationException("승인되지 않은 수행 실패 효과를 적용할 수 없습니다.");
            }

            if (_agents.BottleneckFailures[index] == 0)
            {
                AddChronicle(index, ChronicleEventKind.BreakthroughFailure, finalChance);
            }

            _agents.BottleneckFailures[index] = checked((ushort)(_agents.BottleneckFailures[index] + 1));
            _agents.CultivationStates[index] = CultivationState.PreparingBreakthrough;
            _agents.CultivationBottleneckCauses[index] = CultivationBottleneckCause.None;
            AddEvent(WorldEventKind.BreakthroughFailed, index, finalChance);
        }

        CommitCultivationTrace(
            index,
            CultivationFormulaKind.BreakthroughResult,
            new CultivationTraceState(
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
            CultivationBottleneckCause.None,
            AttemptEffectRegistryRevision: effectResolution.RegistryRevision,
            CandidateEffectCount: effectResolution.CandidateCount,
            SelectedEffectCount: effectResolution.SelectedCount,
            AttemptEffectAvailability: effectResolution.Availability,
            FailureEffect: failureResult?.Effect ?? CultivationFailureEffectKind.CleanFailure,
            FailureEffectAvailability: failureResult?.Availability
                ?? CultivationValueAvailability.NotApplicable,
            RngDomain: CultivationRngDomain.BreakthroughSuccess,
            AttemptSnapshotFrozen: true,
            FoundationProcedureSteps: foundationSource is null
                ? FoundationProcedureStep.None
                : succeeded
                    ? FoundationProcedureStep.Complete
                    : FoundationProcedureStep.AchievementConverged
                        | FoundationProcedureStep.RouteBound
                        | FoundationProcedureStep.SourceAligned
                        | FoundationProcedureStep.FormationAttempted));
    }

    private byte CompleteMinorStageBreakthrough(int index, byte sourceFloor)
    {
        if (sourceFloor == RealmRules.MinorStageCount)
        {
            _agents.CultivationProgress[index] = GetCultivationRequirement(index);
            _agents.PendingCultivationWork[index] = 0;
            _agents.FloorNineCompleted[index] = true;
            _agents.CultivationStates[index] = CultivationState.Stable;
            _agents.CultivationBottleneckCauses[index] = CultivationBottleneckCause.None;
            AddChronicle(index, ChronicleEventKind.FloorNineCompletion);
            AddEvent(
                WorldEventKind.FloorNineCompletion,
                index,
                EncodeRealm(_agents.RealmStages[index], sourceFloor));
            return sourceFloor;
        }

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

    private void CompleteMajorRealmBreakthrough(
        int index,
        FoundationSourceSnapshot? foundationSource)
    {
        if (_agents.RealmStages[index] >= RealmStage.HalfImmortal)
        {
            _agents.CultivationStates[index] = CultivationState.Completed;
            return;
        }

        var isFoundationFormation = _agents.RealmStages[index] == RealmStage.QiRefining;
        if (isFoundationFormation && foundationSource is null)
        {
            throw new InvalidOperationException("축기 형성 Source Snapshot이 없습니다.");
        }

        _agents.RealmStages[index]++;
        // 본명법보는 소유자와 함께 새 대경지 급으로 승급한다. 시장에서 산 법보와
        // 단약은 이전 대경지 전용이므로 새 대경지에서는 다시 마련해야 한다.
        _agents.SetTreasureQuality(
            index,
            TreasureSlot.DefenseTreasure,
            EconomyRules.EmptyTreasureQuality);
        _agents.SetTreasureQuality(
            index,
            TreasureSlot.AttackTreasure,
            EconomyRules.EmptyTreasureQuality);
        _agents.GrowthPills[index] = 0;
        _agents.BreakthroughPills[index] = 0;
        _agents.RealmLayers[index] = 1;
        _agents.CultivationProgress[index] = 0;
        _agents.PendingCultivationWork[index] = 0;
        _agents.FloorNineCompleted[index] = false;
        _agents.GreatPerfectionAchieved[index] = false;
        _agents.CultivationStates[index] = CultivationState.Stabilizing;
        _agents.CultivationBottleneckCauses[index] = CultivationBottleneckCause.None;
        InitializeRealmComponents(index);
        if (isFoundationFormation)
        {
            _agents.FoundationFormations.Commit(
                index,
                new FoundationFormationCheckpoint(
                    FoundationMethodRules.CurrentRevision,
                    FoundationProcedureStep.Complete,
                    foundationSource!.Value));
        }
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
                && (_agents.TechniqueQualities[index] == 0
                    || _agents.TechniqueMasteries[index]
                        < FoundationMethodRules.ProvisionalMasteryThresholdHundredths
                    || _agents.TechniqueUnderstanding[index]
                        < FoundationMethodRules.ProvisionalUnderstandingThresholdHundredths))
            {
                return CultivationBottleneckCause.Method;
            }

            if (_agents.RealmStages[index] == RealmStage.QiRefining
                && !IsFoundationSourceAvailable(index, _agents.FoundationRoutes[index]))
            {
                return CultivationBottleneckCause.Environment;
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

        var stabilized = componentCount == 0
            || _agents.GetCultivationComponentAverage(index, componentCount)
                >= CultivationRules.ProvisionalStabilizationThresholdHundredths;
        _agents.CultivationStates[index] = stabilized
            ? CultivationState.Stable
            : CultivationState.Stabilizing;
        _agents.CultivationBottleneckCauses[index] = stabilized
            ? CultivationBottleneckCause.None
            : CultivationBottleneckCause.Stability;
        CommitCultivationTrace(
            index,
            CultivationFormulaKind.BreakthroughResult,
            new CultivationTraceState(
                Tick.Value,
                CultivationRules.FormulaRevision,
                BreakthroughClass.MajorRealm,
                stabilized ? BreakthroughOutcome.Success : BreakthroughOutcome.Blocked,
                _agents.RealmStages[index],
                _agents.RealmLayers[index],
                _agents.RealmLayers[index],
                _agents.CultivationProgress[index],
                0,
                _agents.CultivationProgress[index],
                GetCultivationRequirement(index),
                _agents.PendingCultivationWork[index],
                10_000,
                0,
                10_000,
                -1,
                stabilized
                    ? CultivationBottleneckCause.None
                    : CultivationBottleneckCause.Stability));
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
        0 => _agents.GetDerivedAptitude(
            index,
            AptitudeDerivedAbility.SpiritualRootCultivation),
        1 => _agents.GetDerivedAptitude(
            index,
            AptitudeDerivedAbility.ComprehensionLearning),
        2 => (_agents.GetDerivedAptitude(index, AptitudeDerivedAbility.MindCultivation)
            + _agents.GetDerivedAptitude(index, AptitudeDerivedAbility.SoulGrowth)) / 2,
        _ => (_agents.GetDerivedAptitude(index, AptitudeDerivedAbility.BoneCultivation)
            + _agents.GetDerivedAptitude(index, AptitudeDerivedAbility.ConstitutionCultivation)) / 2,
    };

    private FoundationRoute SelectFoundationRoute(int index)
    {
        var aptitudes = _agents.GetContextAptitudes(index, combat: false);
        var human = (aptitudes.Bone.Hundredths + aptitudes.Constitution.Hundredths) / 2;
        var earth = (aptitudes.Mind.Hundredths + aptitudes.Soul.Hundredths) / 2;
        var heaven = (aptitudes.SpiritualRoot.Hundredths + aptitudes.Comprehension.Hundredths) / 2;
        var selected = FoundationRoute.Human;
        var selectedScore = human;
        if (IsFoundationSourceAvailable(index, FoundationRoute.Earth)
            && earth > selectedScore)
        {
            selected = FoundationRoute.Earth;
            selectedScore = earth;
        }
        if (IsFoundationSourceAvailable(index, FoundationRoute.Heaven)
            && heaven > selectedScore)
        {
            selected = FoundationRoute.Heaven;
        }

        return selected;
    }

    private void BindFoundationRouteIfNeeded(int index, BreakthroughClass attemptClass)
    {
        if (attemptClass == BreakthroughClass.MajorRealm
            && _agents.RealmStages[index] == RealmStage.QiRefining
            && _agents.FoundationRoutes[index] == FoundationRoute.None)
        {
            _agents.FoundationRoutes[index] = SelectFoundationRoute(index);
        }
    }

    private bool IsFoundationSourceAvailable(int index, FoundationRoute route)
    {
        var locationId = _agents.LocationIds[index];
        return route switch
        {
            FoundationRoute.Human => true,
            FoundationRoute.Earth => _atlas.GetRemainingSpiritStoneSupply(locationId) > 0,
            FoundationRoute.Heaven =>
                WorldMapRules.GetLocation(locationId).HeavenlyLayer >= 2
                || (WorldMapRules.GetLocation(locationId).Biome == BiomeType.Mountains
                    && _atlas.GetCultivationEnvironmentQualityHundredths(locationId) >= 5_500),
            _ => false,
        };
    }

    private FoundationSourceSnapshot CreateFoundationSourceSnapshot(
        int index,
        FoundationRoute route)
    {
        var locationId = _agents.LocationIds[index];
        var available = IsFoundationSourceAvailable(index, route);
        var kind = route switch
        {
            FoundationRoute.Human => FoundationSourceKind.InternalBodyAndSpiritualRoot,
            FoundationRoute.Earth => FoundationSourceKind.LocationSpiritVeins,
            FoundationRoute.Heaven => FoundationSourceKind.ManifestationEnvironmentWindow,
            _ => FoundationSourceKind.None,
        };
        var sourceCount = route switch
        {
            FoundationRoute.Human => 2,
            FoundationRoute.Earth => WorldAtlasRules.SpiritVeinsPerLocation,
            FoundationRoute.Heaven => 1,
            _ => 0,
        };
        var remainingSupply = route == FoundationRoute.Earth
            ? _atlas.GetRemainingSpiritStoneSupply(locationId)
            : 0;
        return new FoundationSourceSnapshot(
            route,
            kind,
            locationId,
            WorldAtlasRules.CurrentRevision,
            Tick.Value,
            sourceCount,
            remainingSupply,
            available
                ? CultivationValueAvailability.Actual
                : CultivationValueAvailability.Unavailable);
    }

    private byte EvaluateEntryGatePassMask(int index)
    {
        var aptitudes = _agents.GetContextAptitudes(index, combat: false);
        var gate1 = CultivationRules.EvaluateEntryGate(
            1,
            aptitudes.SpiritualRoot.Hundredths,
            (aptitudes.Comprehension.Hundredths
                + aptitudes.Mind.Hundredths
                + aptitudes.Soul.Hundredths) / 3,
            _agents.GetEntryGateTempering(index, 0));
        var gate2 = CultivationRules.EvaluateEntryGate(
            2,
            (aptitudes.Bone.Hundredths + aptitudes.Constitution.Hundredths) / 2,
            (aptitudes.SpiritualRoot.Hundredths + aptitudes.Mind.Hundredths) / 2,
            _agents.GetEntryGateTempering(index, 1));
        var gate3 = CultivationRules.EvaluateEntryGate(
            3,
            aptitudes.Comprehension.Hundredths,
            (aptitudes.SpiritualRoot.Hundredths + aptitudes.Soul.Hundredths) / 2,
            _agents.GetEntryGateTempering(index, 2));
        return checked((byte)((gate1.Passed ? 1 : 0)
            | (gate2.Passed ? 2 : 0)
            | (gate3.Passed ? 4 : 0)));
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
        _agents.CultivationTraces[index],
        _agents.CultivationAuthorityRevisions[index],
        CreateFoundationFormationProjection(index));

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
