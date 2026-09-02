using Chronicles.Core;

namespace Chronicles.Simulation;

/// <summary>
/// 병목 사전검증, 소·대경지 돌파, 안착과 축기 경로 전이를 소유한다.
/// 월별 Work 축적 hot path와 저빈도 경계 전이 작업집합을 분리한다.
/// </summary>
public sealed partial class World
{
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

        if ((long)_agents.Health[index] * 2L < _agents.MaximumHealth[index])
        {
            return CultivationBottleneckCause.Body;
        }

        if ((long)_agents.Qi[index] * 4L < _agents.MaximumQi[index])
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
                _generation.Map.GetLocation(locationId).HeavenlyLayer >= 2
                || (_generation.Map.GetLocation(locationId).Biome == BiomeType.Mountains
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
        var temperings = _agents.GetEntryGateTemperings(index);
        var gate1 = CultivationRules.IsEntryGatePassed(
            aptitudes.SpiritualRoot.Hundredths,
            (aptitudes.Comprehension.Hundredths
                + aptitudes.Mind.Hundredths
                + aptitudes.Soul.Hundredths) / 3,
            temperings[0]);
        var gate2 = CultivationRules.IsEntryGatePassed(
            (aptitudes.Bone.Hundredths + aptitudes.Constitution.Hundredths) / 2,
            (aptitudes.SpiritualRoot.Hundredths + aptitudes.Mind.Hundredths) / 2,
            temperings[1]);
        var gate3 = CultivationRules.IsEntryGatePassed(
            aptitudes.Comprehension.Hundredths,
            (aptitudes.SpiritualRoot.Hundredths + aptitudes.Soul.Hundredths) / 2,
            temperings[2]);
        return checked((byte)((gate1 ? 1 : 0) | (gate2 ? 2 : 0) | (gate3 ? 4 : 0)));
    }

}
