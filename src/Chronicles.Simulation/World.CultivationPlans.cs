using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private void EnsureCultivationPlan(int index, long currentMonth)
    {
        var existing = _agents.CultivationPlans[index];
        if (existing.IsActive
            && _agents.CultivationStates[index] == CultivationState.PreCultivation
            && HasReachedAge(index, CultivationRules.EntryAgeYears))
        {
            FinishCultivationPlan(index);
            existing = _agents.CultivationPlans[index];
        }
        if (existing.IsActive)
        {
            return;
        }

        var intensity = Population <= 2
            ? CultivationPlanIntensity.Leisurely
            : SelectCultivationIntensity(index);
        var plannedMonths = GetPlannedCultivationMonths(index, currentMonth, intensity);
        var resourcePolicy = SelectCultivationResourcePolicy(index);
        var formationKnowledge = _agents.TechniqueUnderstanding[index];
        var formationExperience = _agents.FormationInstallationExperience[index];
        var hasExistingFormation = WorldMapRules.IsCity(_agents.LocationIds[index]);
        ResolveCultivationRelationContext(
            index,
            searchFormationAssistant: !hasExistingFormation,
            out var hasFormationAssistant,
            out var teachingRole,
            out var teachingPartner,
            out var teachingBonus);
        var formationSupport = SelectFormationSupport(
            index,
            formationKnowledge,
            formationExperience,
            hasExistingFormation,
            hasFormationAssistant,
            out var formationSucceeded);
        var conditionFactor = CultivationPlanRules.CombineConditionFactors(
            intensity,
            formationSupport,
            teachingBonus);
        var previousRevision = _agents.CultivationPlans[index].AuthorityRevision;
        _agents.CultivationPlans[index] = new CultivationActionPlanState(
            AuthorityRevision: checked(previousRevision + 1),
            StartedTick: Tick.Value,
            PlannedEndTick: GameCalendar.GetMonthStart(
                checked(currentMonth + plannedMonths)).Value,
            PlannedMonths: plannedMonths,
            CompletedMonths: 0,
            LocationId: _agents.LocationIds[index],
            intensity,
            resourcePolicy,
            SpiritStoneBudget: resourcePolicy is CultivationResourcePolicy.SpiritStones
                or CultivationResourcePolicy.Mixed
                    ? Math.Min(Math.Max(0, _agents.SpiritStones[index] - 100), plannedMonths)
                    : 0,
            SpiritStonesConsumed: 0,
            GrowthPillBudget: resourcePolicy is CultivationResourcePolicy.GrowthPills
                or CultivationResourcePolicy.Mixed
                    ? Math.Min(_agents.GrowthPills[index], checked((int)Math.Min(int.MaxValue, plannedMonths)))
                    : 0,
            GrowthPillsConsumed: 0,
            formationSupport,
            formationKnowledge,
            formationExperience,
            formationSucceeded,
            teachingRole,
            teachingPartner,
            teachingBonus,
            conditionFactor,
            IsActive: true);
    }

    private long GetPlannedCultivationMonths(
        int index,
        long currentMonth,
        CultivationPlanIntensity intensity)
    {
        if (_agents.CultivationStates[index] == CultivationState.PreCultivation)
        {
            var entryTick = checked(
                _agents.BirthTicks[index]
                + ((long)CultivationRules.EntryAgeYears * GameCalendar.TicksPerYear));
            var entryMonth = GameCalendar.GetMonthIndex(new SimulationTick(entryTick));
            return Math.Clamp(
                entryMonth - currentMonth,
                CultivationPlanRules.MinimumPlanMonths,
                CultivationPlanRules.MaximumPlanMonths);
        }

        var defaultMonths = CultivationPlanRules.GetDefaultDurationMonths(
            _agents.RealmStages[index],
            _agents.CultivationStates[index]);
        // 두 명 이하의 소형 검증 세계는 경쟁·관계 테스트가 계획에 장기간 막히지 않도록
        // 월 단위 결정을 유지한다. 실제 기본 세계에서는 여유 수행도 장기 계획이다.
        if (Population <= 2)
        {
            return CultivationPlanRules.MinimumPlanMonths;
        }

        return intensity switch
        {
            CultivationPlanIntensity.Leisurely => Math.Max(
                CultivationPlanRules.MinimumPlanMonths,
                defaultMonths / 10),
            CultivationPlanIntensity.Steady => Math.Max(
                CultivationPlanRules.MinimumPlanMonths,
                defaultMonths / 3),
            CultivationPlanIntensity.ClosedDoor => defaultMonths,
            _ => throw new ArgumentOutOfRangeException(nameof(intensity)),
        };
    }

    private CultivationPlanIntensity SelectCultivationIntensity(int index)
    {
        var healthBasisPoints = CultivationPlanRules.GetBodyConditionBasisPoints(
            _agents.Health[index],
            Math.Max(1, _agents.MaximumHealth[index]));
        if (healthBasisPoints < 8_000 || _agents.InjuryPoints[index] > 0)
        {
            return CultivationPlanIntensity.Leisurely;
        }

        var focus = checked((
            _agents.GetDerivedAptitude(index, AptitudeDerivedAbility.ComprehensionLearning)
            + _agents.GetDerivedAptitude(index, AptitudeDerivedAbility.MindCultivation)) / 2);
        return focus switch
        {
            >= 7_500 => CultivationPlanIntensity.ClosedDoor,
            < 4_500 => CultivationPlanIntensity.Leisurely,
            _ => CultivationPlanIntensity.Steady,
        };
    }

    private CultivationResourcePolicy SelectCultivationResourcePolicy(int index)
    {
        var hasPills = _agents.GrowthPills[index] > 0;
        // 시장 구매·임무·법보 제작용 최소 유동성은 남겨 둔다.
        var hasSpiritStones = _agents.SpiritStones[index] > 100;
        return (hasPills, hasSpiritStones) switch
        {
            (true, true) => CultivationResourcePolicy.Mixed,
            (true, false) => CultivationResourcePolicy.GrowthPills,
            (false, true) => CultivationResourcePolicy.SpiritStones,
            _ => CultivationResourcePolicy.None,
        };
    }

    private CultivationFormationSupport SelectFormationSupport(
        int index,
        int knowledgeHundredths,
        int experienceHundredths,
        bool hasExistingFormation,
        bool hasAssistant,
        out bool installationSucceeded)
    {
        installationSucceeded = false;
        if (hasExistingFormation)
        {
            return CultivationFormationSupport.ExistingAtLocation;
        }

        if (hasAssistant)
        {
            installationSucceeded = true;
            return CultivationFormationSupport.ExternalAssistance;
        }

        if (knowledgeHundredths < CultivationPlanRules.MinimumSelfInstallationKnowledgeHundredths)
        {
            return CultivationFormationSupport.None;
        }

        var successChance = CultivationPlanRules.GetSelfInstallationChanceBasisPoints(
            knowledgeHundredths,
            experienceHundredths);
        installationSucceeded = _random.Chance(successChance);
        _agents.FormationInstallationExperience[index] = checked((ushort)Math.Min(
            QualityValue.MaximumHundredths,
            experienceHundredths + (installationSucceeded ? 100 : 50)));
        return installationSucceeded
            ? CultivationFormationSupport.SelfInstalled
            : CultivationFormationSupport.None;
    }

    private void ResolveCultivationRelationContext(
        int index,
        bool searchFormationAssistant,
        out bool hasFormationAssistant,
        out CultivationTeachingRole role,
        out int partner,
        out int bonusBasisPoints)
    {
        hasFormationAssistant = false;
        role = CultivationTeachingRole.None;
        partner = -1;
        bonusBasisPoints = CultivationPlanRules.NeutralFactorBasisPoints;
        if (!searchFormationAssistant && !_relations.HasMasterDiscipleRelation(index))
        {
            return;
        }

        var relationCount = _relations.GetRelationCount(index);
        for (var relationIndex = 0; relationIndex < relationCount; relationIndex++)
        {
            var relation = _relations.GetRoleAdjacencyState(index, relationIndex);
            var isTeachingRelation = (relation.Roles & RelationRole.MasterDisciple) != 0;
            if (!searchFormationAssistant && !isTeachingRelation)
            {
                continue;
            }

            var related = relation.OtherAgent;
            if (_agents.LifeStates[related] != PersonLifeState.Living
                || _agents.LocationIds[related] != _agents.LocationIds[index])
            {
                continue;
            }

            if (searchFormationAssistant && !hasFormationAssistant)
            {
                hasFormationAssistant = _agents.TechniqueUnderstanding[related]
                    >= CultivationPlanRules.MinimumSelfInstallationKnowledgeHundredths;
            }
            if (!isTeachingRelation
                || _agents.Activities[related] != AgentActivity.Cultivating)
            {
                continue;
            }

            if (relation.MasterAgent == related)
            {
                role = CultivationTeachingRole.Learner;
                partner = related;
                var teacherKnowledge = checked((
                    _agents.GetDerivedAptitude(
                        related,
                        AptitudeDerivedAbility.ComprehensionLearning)
                    + _agents.TechniqueUnderstanding[related]
                    + Math.Min(10_000L, _agents.CultivationExperience[related] / 10)) / 3);
                var learnerUptake = _agents.GetDerivedAptitude(
                    index,
                    AptitudeDerivedAbility.ComprehensionLearning);
                bonusBasisPoints = Math.Clamp(
                    CultivationPlanRules.NeutralFactorBasisPoints
                    + checked((int)((teacherKnowledge + learnerUptake) / 8)),
                    CultivationPlanRules.NeutralFactorBasisPoints,
                    CultivationPlanRules.MaximumTeachingBonusBasisPoints);
                return;
            }

            if (relation.MasterAgent == index && role == CultivationTeachingRole.None)
            {
                role = CultivationTeachingRole.Instructor;
                partner = related;
            }
        }
    }

    private int ConsumeCultivationPlanResourcesAndGetFactor(int index)
    {
        var plan = _agents.CultivationPlans[index];
        if (!plan.IsActive)
        {
            return CultivationPlanRules.NeutralFactorBasisPoints;
        }

        var factor = plan.FrozenConditionFactorBasisPoints;
        var stonesConsumed = plan.SpiritStonesConsumed;
        var pillsConsumed = plan.GrowthPillsConsumed;
        if (plan.ResourcePolicy is CultivationResourcePolicy.GrowthPills
                or CultivationResourcePolicy.Mixed
            && pillsConsumed < plan.GrowthPillBudget
            && _agents.GrowthPills[index] > 0)
        {
            _agents.GrowthPills[index]--;
            pillsConsumed++;
            factor = checked(factor * CultivationPlanRules.GrowthPillFactorBasisPoints
                / CultivationPlanRules.NeutralFactorBasisPoints);
        }
        else if (plan.ResourcePolicy is CultivationResourcePolicy.SpiritStones
                    or CultivationResourcePolicy.Mixed
            && stonesConsumed < plan.SpiritStoneBudget
            && _agents.SpiritStones[index] > 0)
        {
            _agents.SpiritStones[index]--;
            stonesConsumed++;
            factor = checked(factor * CultivationPlanRules.SpiritStoneFactorBasisPoints
                / CultivationPlanRules.NeutralFactorBasisPoints);
        }

        _agents.CultivationPlans[index] = plan with
        {
            SpiritStonesConsumed = stonesConsumed,
            GrowthPillsConsumed = pillsConsumed,
        };
        return Math.Clamp(factor, 5_000, 20_000);
    }

    private int GetCultivationPulseMonths(int index, long currentMonth)
    {
        var plan = _agents.CultivationPlans[index];
        if (!plan.IsActive
            || (_agents.CultivationStates[index] == CultivationState.PreCultivation
                && !HasReachedAge(index, CultivationRules.EntryAgeYears)))
        {
            return 1;
        }

        var startedMonth = GameCalendar.GetMonthIndex(new SimulationTick(plan.StartedTick));
        var elapsedMonths = Math.Max(1L, checked(currentMonth - startedMonth + 1));
        var newlyElapsed = Math.Max(1L, elapsedMonths - plan.CompletedMonths);
        var remaining = Math.Max(1L, plan.PlannedMonths - plan.CompletedMonths);
        return checked((int)Math.Min(newlyElapsed, remaining));
    }

    private void AdvanceCultivationPlanProgress(int index, int months)
    {
        var plan = _agents.CultivationPlans[index];
        if (!plan.IsActive || months <= 0)
        {
            return;
        }

        var applied = Math.Min((long)months, plan.PlannedMonths - plan.CompletedMonths);
        _agents.CultivationPlans[index] = plan with
        {
            CompletedMonths = checked(plan.CompletedMonths + applied),
        };
        if (plan.TeachingRole == CultivationTeachingRole.Instructor
            && plan.TeachingPartnerAgent >= 0)
        {
            _agents.CultivationExperience[index] = checked(
                _agents.CultivationExperience[index]
                + (applied * CultivationPlanRules.InstructorExperiencePerMonth));
        }
    }

    private bool TryContinueCultivationPlan(int index, long currentMonth)
    {
        var plan = _agents.CultivationPlans[index];
        if (!plan.IsActive || _agents.Activities[index] != AgentActivity.Cultivating)
        {
            return false;
        }

        var pipelineBoundary = _agents.CultivationStates[index] is
            CultivationState.PreparingBreakthrough
            or CultivationState.PreflightReady
            or CultivationState.AttemptingBreakthrough
            or CultivationState.Stabilizing
            or CultivationState.Completed;
        if (plan.CompletedMonths >= plan.PlannedMonths
            || Tick.Value >= plan.PlannedEndTick
            || _agents.LocationIds[index] != plan.LocationId
            || pipelineBoundary)
        {
            FinishCultivationPlan(index);
            return false;
        }

        long nextTick;
        if (_agents.CultivationStates[index] == CultivationState.PreCultivation
            && !HasReachedAge(index, CultivationRules.EntryAgeYears))
        {
            nextTick = Math.Min(
                plan.PlannedEndTick,
                checked(_agents.BirthTicks[index]
                    + ((long)CultivationRules.EntryAgeYears * GameCalendar.TicksPerYear)));
        }
        else
        {
            var pulseIntervalMonths = plan.Intensity switch
            {
                CultivationPlanIntensity.Leisurely => 6L,
                CultivationPlanIntensity.Steady => 12L,
                CultivationPlanIntensity.ClosedDoor => 24L,
                _ => throw new InvalidOperationException("알 수 없는 수행 계획 강도입니다."),
            };
            var remainingMonths = Math.Max(1L, plan.PlannedMonths - plan.CompletedMonths);
            nextTick = GameCalendar.GetMonthStart(checked(
                currentMonth + Math.Min(pulseIntervalMonths, remainingMonths))).Value;
        }

        ScheduleAgent(index, nextTick);
        return true;
    }

    private void FinishCultivationPlan(int index)
    {
        var plan = _agents.CultivationPlans[index];
        if (!plan.IsActive)
        {
            return;
        }

        _agents.CultivationPlans[index] = plan with
        {
            AuthorityRevision = checked(plan.AuthorityRevision + 1),
            CompletedMonths = Math.Min(plan.PlannedMonths, plan.CompletedMonths),
            IsActive = false,
        };
    }

    private CultivationActionPlanSnapshot? CreateCultivationPlanSnapshot(int index)
    {
        var plan = _agents.CultivationPlans[index];
        if (plan.AuthorityRevision == 0)
        {
            return null;
        }

        return new CultivationActionPlanSnapshot(
            plan.AuthorityRevision,
            new SimulationTick(plan.StartedTick),
            new SimulationTick(plan.PlannedEndTick),
            plan.PlannedMonths,
            plan.CompletedMonths,
            plan.LocationId,
            plan.Intensity,
            plan.ResourcePolicy,
            plan.SpiritStoneBudget,
            plan.SpiritStonesConsumed,
            plan.GrowthPillBudget,
            plan.GrowthPillsConsumed,
            plan.FormationSupport,
            plan.FormationKnowledgeHundredths,
            plan.FormationInstallationExperienceHundredths,
            plan.FormationInstallationSucceeded,
            plan.TeachingRole,
            plan.TeachingPartnerAgent >= 0
                ? new AgentId(plan.TeachingPartnerAgent)
                : null,
            plan.FrozenTeachingBonusBasisPoints,
            plan.FrozenConditionFactorBasisPoints,
            plan.IsActive,
            RequiresReview: true);
    }
}
