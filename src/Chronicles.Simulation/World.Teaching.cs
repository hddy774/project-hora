using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private TeachingSnapshot CreateTeachingSnapshot(int agent) => new(
        TeachingRules.ContractId,
        TeachingRules.ContractRevision,
        RequiresReview: true,
        RelationRules.DirectTeachingCultivationSpeedBonusBasisPoints,
        _agents.TeachingTraces[agent],
        _agents.LearningTraces[agent],
        _agents.TechniqueMisunderstandingPoints[agent],
        _agents.SpellMisunderstandingPoints[agent],
        CreateTeachingParticipants(_agents.TeachingTraces[agent]),
        CreateTeachingParticipants(_agents.LearningTraces[agent]));

    private TeachingParticipantSnapshot? CreateTeachingParticipants(
        TeachingTraceState trace)
    {
        if (trace.AuthorityRevision == 0)
        {
            return null;
        }

        var teacher = RelationRules.GetOtherAgent(trace.RelationId, trace.LearnerAgent);
        if ((uint)teacher >= (uint)Population
            || (uint)trace.LearnerAgent >= (uint)Population)
        {
            return null;
        }

        return new TeachingParticipantSnapshot(
            new AgentId(teacher),
            GetPersonNameAtIdentity(teacher, trace.TeacherPersonId),
            new AgentId(trace.LearnerAgent),
            GetPersonNameAtIdentity(trace.LearnerAgent, trace.LearnerPersonId));
    }

    private string GetPersonNameAtIdentity(int agent, PersonIdentityId personId)
    {
        if (_agents.PersonIdentityIds[agent] == personId)
        {
            return _agents.Names[agent];
        }

        var lifeNumber = checked((int)(personId.Value >> 32));
        return _agents.Chronicles.TryGetPersonName(agent, personId, out var historicalName)
            ? historicalName
            : $"과거 제{lifeNumber:N0}생";
    }

    private bool TryPrepareTeaching(int teacher)
    {
        if (_agents.LifeStates[teacher] != PersonLifeState.Living
            || _agents.ActionsTaken[teacher] % TeachingRules.SelectionActionInterval
                != TeachingRules.SelectionActionPhase)
        {
            return false;
        }

        var relationCount = _relations.GetRelationCount(teacher);
        var bestLearner = -1;
        var bestTarget = TeachingTargetKind.Technique;
        var bestGap = TeachingRules.MinimumKnowledgeGap - 1;
        for (var relationIndex = 0; relationIndex < relationCount; relationIndex++)
        {
            var relation = _relations.GetRoleAdjacencyState(teacher, relationIndex);
            var learner = relation.OtherAgent;
            if (_agents.LifeStates[learner] != PersonLifeState.Living
                || _agents.LocationIds[learner] != _agents.LocationIds[teacher]
                || _agents.LearningTraces[learner].IsActive
                || _agents.Activities[learner] is AgentActivity.Fighting
                    or AgentActivity.Reincarnating)
            {
                continue;
            }

            if ((relation.Roles & RelationRole.MasterDisciple) != 0
                && relation.MasterAgent != teacher)
            {
                continue;
            }

            var techniqueGap = GetTeachingKnowledgeScore(teacher, TeachingTargetKind.Technique)
                - GetTeachingKnowledgeScore(learner, TeachingTargetKind.Technique);
            var spellGap = GetTeachingKnowledgeScore(teacher, TeachingTargetKind.Spell)
                - GetTeachingKnowledgeScore(learner, TeachingTargetKind.Spell);
            var target = techniqueGap >= spellGap
                ? TeachingTargetKind.Technique
                : TeachingTargetKind.Spell;
            var gap = Math.Max(techniqueGap, spellGap);
            if (gap < TeachingRules.MinimumKnowledgeGap
                || gap < bestGap
                || (gap == bestGap && learner > bestLearner && bestLearner >= 0))
            {
                continue;
            }

            bestLearner = learner;
            bestTarget = target;
            bestGap = gap;
        }

        if (bestLearner < 0
            || !_relations.TryEstablishMasterDisciple(
                teacher,
                bestLearner,
                _agents.PersonIdentityIds[teacher],
                _agents.PersonIdentityIds[bestLearner],
                Tick.Value,
                out var relationId))
        {
            return false;
        }

        var sourceQuality = bestTarget == TeachingTargetKind.Technique
            ? _agents.TechniqueQualities[teacher]
            : _agents.SpellQualities[teacher];
        var knowledgeRef = TeachingRules.CreateKnowledgeRef(
            _agents.PersonIdentityIds[teacher],
            bestTarget,
            sourceQuality);
        var provenance = TeachingRules.CreateProvenanceId(
            relationId,
            knowledgeRef,
            Tick.Value);
        var continuity = _relations.GetTeachingContinuity(
            relationId,
            bestTarget,
            knowledgeRef);
        _agents.BeginTeaching(
            teacher,
            bestLearner,
            relationId,
            bestTarget,
            sourceQuality,
            knowledgeRef,
            provenance,
            continuity.SessionsCompleted,
            continuity.TransmissionEstablished,
            Tick.Value);
        return true;
    }

    private void ResolveTeaching(int teacher)
    {
        var trace = _agents.TeachingTraces[teacher];
        if (!trace.IsActive)
        {
            return;
        }

        var learner = trace.LearnerAgent;
        if ((uint)learner >= (uint)Population
            || _agents.LifeStates[teacher] != PersonLifeState.Living
            || _agents.LifeStates[learner] != PersonLifeState.Living
            || _agents.PersonIdentityIds[teacher] != trace.TeacherPersonId
            || _agents.PersonIdentityIds[learner] != trace.LearnerPersonId
            || _agents.LocationIds[teacher] != _agents.LocationIds[learner]
            || _agents.Activities[learner] is AgentActivity.Fighting
                or AgentActivity.Reincarnating)
        {
            CompleteTeachingWithoutGrowth(
                teacher,
                TeachingResultKind.Interrupted,
                trace.MisunderstandingPoints);
            return;
        }

        if (!_relations.TryGetPerspective(
                learner,
                teacher,
                out var learnerRelation,
                out var roles,
                out var masterAgent)
            || (roles & RelationRole.MasterDisciple) == 0
            || masterAgent != teacher
            || learnerRelation.TrustDistrust <= -3_000
            || learnerRelation.Grievance >= 6_000)
        {
            CompleteTeachingWithoutGrowth(
                teacher,
                TeachingResultKind.Refused,
                trace.MisunderstandingPoints);
            return;
        }

        var currentMisunderstanding = trace.TargetKind == TeachingTargetKind.Technique
            ? _agents.TechniqueMisunderstandingPoints[learner]
            : _agents.SpellMisunderstandingPoints[learner];
        if (currentMisunderstanding > 0)
        {
            var corrected = checked((ushort)Math.Max(
                0,
                currentMisunderstanding - TeachingRules.CorrectionAmountHundredths));
            SetMisunderstanding(learner, trace.TargetKind, corrected);
            CompleteTeachingSession(
                teacher,
                TeachingResultKind.Correction,
                currentMisunderstanding - corrected,
                corrected,
                trace.TransmissionEstablished);
            return;
        }

        var gap = GetTeachingKnowledgeScore(teacher, trace.TargetKind)
            - GetTeachingKnowledgeScore(learner, trace.TargetKind);
        if (gap <= 0)
        {
            CompleteTeachingWithoutGrowth(
                teacher,
                TeachingResultKind.Refused,
                currentMisunderstanding);
            return;
        }

        var learnerComprehension = _agents.GetDerivedAptitude(
            learner,
            AptitudeDerivedAbility.ComprehensionLearning);
        var teacherUnderstanding = trace.TargetKind == TeachingTargetKind.Technique
            ? _agents.TechniqueUnderstanding[teacher]
            : _agents.SpellUnderstanding[teacher];
        var roll = TeachingRules.GetDedicatedRollBasisPoints(
            Seed,
            trace.ProvenanceId,
            Tick.Value,
            trace.SessionsCompleted + 1);
        var misunderstandingChance = TeachingRules.GetMisunderstandingChanceBasisPoints(
            teacherUnderstanding,
            learnerComprehension);
        if (roll < misunderstandingChance)
        {
            var nextMisunderstanding = checked((ushort)Math.Min(
                QualityValue.MaximumHundredths,
                currentMisunderstanding + TeachingRules.MisunderstandingAmountHundredths));
            SetMisunderstanding(learner, trace.TargetKind, nextMisunderstanding);
            CompleteTeachingSession(
                teacher,
                TeachingResultKind.Misunderstanding,
                gain: 0,
                nextMisunderstanding,
                trace.TransmissionEstablished);
            return;
        }

        var gain = ApplyTeachingGrowth(
            learner,
            trace.TargetKind,
            gap,
            teacherUnderstanding,
            learnerComprehension);
        var result = !trace.TransmissionEstablished
            ? TeachingResultKind.TransmissionEstablished
            : (trace.SessionsCompleted + 1) % TeachingRules.TransmissionUpdateInterval == 0
                ? TeachingResultKind.TransmissionUpdated
                : TeachingResultKind.LearningProgress;
        var transmissionEstablished = trace.TransmissionEstablished
            || result == TeachingResultKind.TransmissionEstablished;
        CompleteTeachingSession(
            teacher,
            result,
            gain,
            misunderstandingPoints: 0,
            transmissionEstablished);
        RecalculatePowerAndUpdatePeak(learner);
    }

    private int ApplyTeachingGrowth(
        int learner,
        TeachingTargetKind target,
        int knowledgeGap,
        int teacherUnderstanding,
        int learnerComprehension)
    {
        var roleScores = _agents.GetAptitudeRoleScores(learner);
        var experienceBonus = target == TeachingTargetKind.Technique
            ? AptitudeCombatBonusFormula.GetTechniqueEffectBasisPoints(roleScores)
            : AptitudeCombatBonusFormula.GetSpellEffectBasisPoints(roleScores);
        var experienceGain = SkillProgressionRules.ApplyExperienceBonus(
            TeachingRules.GetTrainingExperienceGain(knowledgeGap),
            experienceBonus);
        var understandingGain = TeachingRules.GetUnderstandingGainHundredths(
            teacherUnderstanding,
            learnerComprehension);
        if (target == TeachingTargetKind.Technique)
        {
            _agents.TechniqueTrainingExperience[learner] = Math.Min(
                SkillProgressionRules.MaximumExperience,
                checked(_agents.TechniqueTrainingExperience[learner] + experienceGain));
            _agents.TechniqueMasteries[learner] = SkillProgressionRules.GetMasteryHundredths(
                _agents.TechniqueTrainingExperience[learner]);
            _agents.TechniqueUnderstanding[learner] = checked((ushort)Math.Min(
                SkillProgressionRules.MaximumHundredths,
                _agents.TechniqueUnderstanding[learner] + understandingGain));
        }
        else
        {
            _agents.SpellTrainingExperience[learner] = Math.Min(
                SkillProgressionRules.MaximumExperience,
                checked(_agents.SpellTrainingExperience[learner] + experienceGain));
            _agents.SpellMasteries[learner] = SkillProgressionRules.GetMasteryHundredths(
                _agents.SpellTrainingExperience[learner]);
            _agents.SpellUnderstanding[learner] = checked((ushort)Math.Min(
                SkillProgressionRules.MaximumHundredths,
                _agents.SpellUnderstanding[learner] + understandingGain));
        }

        return understandingGain;
    }

    private void CompleteTeachingWithoutGrowth(
        int teacher,
        TeachingResultKind result,
        ushort misunderstandingPoints) => CompleteTeachingSession(
        teacher,
        result,
        gain: 0,
        misunderstandingPoints,
        _agents.TeachingTraces[teacher].TransmissionEstablished);

    private void CompleteTeachingSession(
        int teacher,
        TeachingResultKind result,
        int gain,
        ushort misunderstandingPoints,
        bool transmissionEstablished)
    {
        var trace = _agents.TeachingTraces[teacher];
        if (!trace.IsActive)
        {
            return;
        }

        _agents.CompleteTeaching(
            teacher,
            Tick.Value,
            result,
            gain,
            misunderstandingPoints,
            transmissionEstablished);
        _relations.RecordTeachingResult(
            trace.RelationId,
            Tick.Value,
            trace.TargetKind,
            trace.TargetKnowledgeRef,
            trace.ProvenanceId,
            result,
            _causal.GetCurrentPath(teacher).EventRecordId);
        _causal.RecordKnowledgeClaim(
            trace.LearnerAgent,
            _agents.LifeNumbers[trace.LearnerAgent],
            trace.TeacherPersonId,
            trace.LearnerPersonId,
            trace.TargetKind,
            result,
            trace.ProvenanceId,
            Tick.Value);
        _causal.RecordInteractionMemory(
            teacher,
            CreateCausalObservation(teacher),
            AgentMemoryKind.Teaching,
            trace.LearnerPersonId,
            trace.RelationId,
            isImportant: TeachingRules.IsTransmissionProgress(result));
        _causal.RecordInteractionMemory(
            trace.LearnerAgent,
            CreateCausalObservation(trace.LearnerAgent),
            AgentMemoryKind.Teaching,
            trace.TeacherPersonId,
            trace.RelationId,
            isImportant: TeachingRules.IsTransmissionProgress(result));
    }

    private void InterruptTeachingContexts(int agent)
    {
        if (_agents.TeachingTraces[agent].IsActive)
        {
            CompleteTeachingWithoutGrowth(
                agent,
                TeachingResultKind.Interrupted,
                _agents.TeachingTraces[agent].MisunderstandingPoints);
        }

        var learning = _agents.LearningTraces[agent];
        if (!learning.IsActive)
        {
            return;
        }

        var teacher = RelationRules.GetOtherAgent(learning.RelationId, agent);
        if ((uint)teacher < (uint)Population
            && _agents.TeachingTraces[teacher].IsActive
            && _agents.TeachingTraces[teacher].ProvenanceId == learning.ProvenanceId)
        {
            CompleteTeachingWithoutGrowth(
                teacher,
                TeachingResultKind.Interrupted,
                learning.MisunderstandingPoints);
            if (teacher != agent
                && _agents.Activities[teacher] == AgentActivity.Teaching)
            {
                SetActivityDecision(
                    teacher,
                    AgentActivity.Cultivating,
                    AgentGoalKind.AdvanceRealm,
                    DecisionReasonCode.TeachingInterrupted,
                    DecisionResolutionKind.Forced,
                    AgentActivity.Teaching);
            }
        }
    }

    private void SetMisunderstanding(
        int learner,
        TeachingTargetKind target,
        ushort value)
    {
        if (target == TeachingTargetKind.Technique)
        {
            _agents.TechniqueMisunderstandingPoints[learner] = value;
        }
        else
        {
            _agents.SpellMisunderstandingPoints[learner] = value;
        }
    }

    private int GetTeachingKnowledgeScore(int agent, TeachingTargetKind target) => target switch
    {
        TeachingTargetKind.Technique => checked(
            _agents.TechniqueQualities[agent]
            + _agents.TechniqueMasteries[agent]
            + _agents.TechniqueUnderstanding[agent]),
        TeachingTargetKind.Spell => checked(
            _agents.SpellQualities[agent]
            + _agents.SpellMasteries[agent]
            + _agents.SpellUnderstanding[agent]),
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };
}
