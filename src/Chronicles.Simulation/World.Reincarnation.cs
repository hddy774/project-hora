using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed partial class World
{
    private void BeginReincarnation(int index, bool combatDeath = false)
    {
        var opponent = _agents.Opponents[index];
        if (opponent >= 0 && _agents.Opponents[opponent] == index)
        {
            FinishCombat(opponent, index);
        }

        RecalculatePowerAndUpdatePeak(index);
        var traceCandidates = CreateReincarnationTraceCandidates(index);
        var journey = _reincarnation.BeginJourney(
            index,
            Tick.Value,
            combatDeath ? LifeEndKind.Combat : LifeEndKind.Lifespan,
            _agents.RealmStages[index],
            traceCandidates);

        var age = checked((int)Math.Min(int.MaxValue, GetAgeYears(index)));
        _totalDeaths++;
        if (combatDeath)
        {
            AddChronicle(index, ChronicleEventKind.CombatDeath, age);
            AddEvent(WorldEventKind.CombatDeath, index, age);
        }
        else
        {
            AddChronicle(index, ChronicleEventKind.Death, age);
            AddEvent(WorldEventKind.Death, index, age);
        }
        AddChronicle(index, ChronicleEventKind.ReincarnationEntered);
        AddEvent(WorldEventKind.ReincarnationEntered, index, _agents.LifeNumbers[index]);
        InterruptTeachingContexts(index);
        _relations.EndLife(index, Tick.Value);
        _agents.LifeStates[index] = PersonLifeState.ReincarnationRiver;
        SetActivityDecision(
            index,
            AgentActivity.Reincarnating,
            AgentGoalKind.CompleteReincarnation,
            DecisionReasonCode.ReincarnationHandoff,
            DecisionResolutionKind.Forced);
        _agents.Opponents[index] = -1;
        _agents.Health[index] = 0;
        _agents.Qi[index] = 0;
        ScheduleAgent(index, journey.NextTransitionTick);
    }

    private void ResolveReincarnationStage(int index)
    {
        var result = _reincarnation.AdvanceStage(index, Tick.Value);
        if (result.ReadyForRebirth)
        {
            CompleteRebirth(index);
            return;
        }

        if (result.CrossingCompleted)
        {
            AddChronicle(index, ChronicleEventKind.ReincarnationCrossingCompleted);
            AddEvent(
                WorldEventKind.ReincarnationCrossingCompleted,
                index,
                _agents.LifeNumbers[index]);
        }
        ScheduleAgent(index, result.NextTransitionTick);
    }

    private void CompleteRebirth(int index)
    {
        _reincarnation.EnsureReadyForRebirth(
            index,
            Tick.Value,
            _agents.SoulIdentityIds[index]);
        _agents.Rebirth(index, _random, Tick.Value);
        _reincarnation.CompleteRebirth(
            index,
            Tick.Value,
            _agents.SoulIdentityIds[index],
            _agents.BodyIdentityIds[index],
            _agents.PersonIdentityIds[index],
            _agents.Names[index],
            _agents.RealmStages[index],
            _agents.RealmLayers[index],
            _agents.Power[index].Total);
        _relations.StartNewLife(index, _agents.PersonIdentityIds[index]);
        _totalReincarnations++;
        AddChronicle(index, ChronicleEventKind.Rebirth);
        AddEvent(WorldEventKind.Rebirth, index, _agents.LifeNumbers[index]);
        ScheduleAgent(index, _agents.NextDecisionTicks[index]);
    }

    private ReincarnationTraceCandidate[] CreateReincarnationTraceCandidates(int index)
    {
        var soulGrowth = _agents.GetDerivedAptitude(index, AptitudeDerivedAbility.SoulGrowth);
        var comprehension = _agents.GetDerivedAptitude(
            index,
            AptitudeDerivedAbility.ComprehensionLearning);
        var mind = _agents.GetDerivedAptitude(index, AptitudeDerivedAbility.MindCultivation);
        var body = checked((
            _agents.GetDerivedAptitude(index, AptitudeDerivedAbility.BoneCombat)
            + _agents.GetDerivedAptitude(index, AptitudeDerivedAbility.ConstitutionCombat)) / 2);
        var ageAchievement = ClampTraceScore(
            checked((GetAgeYears(index) * 10_000L)
                / Math.Max(1, RealmRules.GetProfile(_agents.RealmStages[index]).MaximumLifespanYears)));
        var actionAchievement = ClampTraceScore(
            checked((long)_agents.ActionsTaken[index] * 25L
                + (_agents.Chronicles[index].Count * 120L)));
        var relationAchievement = ClampTraceScore(
            checked((long)_relations.GetRelationCount(index) * 1_000L
                + (_agents.MissionsCompleted[index] * 40L)));
        var realmAchievement = ClampTraceScore(
            checked((((long)(int)_agents.RealmStages[index] * RealmRules.MinorStageCount)
                + _agents.RealmLayers[index]) * 10_000L
                / (RealmRules.All.Count * RealmRules.MinorStageCount)));
        var techniqueAchievement = checked((
            _agents.TechniqueQualities[index]
            + _agents.TechniqueMasteries[index]
            + _agents.TechniqueUnderstanding[index]
            + _agents.SpellQualities[index]
            + _agents.SpellMasteries[index]
            + _agents.SpellUnderstanding[index]) / 6);
        var worldAchievement = ClampTraceScore(
            checked((long)_agents.MissionsCompleted[index] * 80L
                + (_agents.MonsterHunts[index] * 60L)
                + (_agents.LocationIds[index] * 150L)
                + Math.Min(2_000L, _agents.SpiritStones[index] / 10L)));
        var combatAchievement = ClampTraceScore(
            checked((long)CombatProgressionRules.GetMasteryHundredths(
                    _agents.CombatExperience[index])
                + _agents.CombatUnderstanding[index]
                + Math.Min(3_000L, (long)(_agents.Wins[index] + _agents.Losses[index]) * 60L)) / 3L);

        return
        [
            CreateTraceCandidate(
                ReincarnationTraceDomain.AutobiographicalMemory,
                Average(soulGrowth, mind),
                Average(actionAchievement, ageAchievement),
                Average(comprehension, mind)),
            CreateTraceCandidate(
                ReincarnationTraceDomain.RelationshipAndSocialMemory,
                Average(soulGrowth, mind),
                relationAchievement,
                Average(comprehension, mind)),
            CreateTraceCandidate(
                ReincarnationTraceDomain.CultivationPrinciplesAndRealmKnowledge,
                Average(soulGrowth, comprehension),
                realmAchievement,
                Average(comprehension, soulGrowth)),
            CreateTraceCandidate(
                ReincarnationTraceDomain.TechniqueSpellPillAndCraftingKnowledge,
                Average(soulGrowth, comprehension),
                techniqueAchievement,
                comprehension),
            CreateTraceCandidate(
                ReincarnationTraceDomain.WorldFactionAndEnvironmentKnowledge,
                Average(soulGrowth, mind),
                worldAchievement,
                Average(comprehension, mind)),
            CreateTraceCandidate(
                ReincarnationTraceDomain.EmbodiedAndActionTrace,
                Average(soulGrowth, body),
                combatAchievement,
                Average(mind, body)),
        ];
    }

    private static ReincarnationTraceCandidate CreateTraceCandidate(
        ReincarnationTraceDomain domain,
        int preservationAptitude,
        int achievement,
        int accessAptitude)
    {
        // PC-TEMP-REINCARNATION-TRACE-113: 정본이 열어 둔 세부 산식의 임시 고정소수점 구현.
        var preserved = ClampTraceScore(
            ((long)preservationAptitude * 6L + ((long)achievement * 4L)) / 10L);
        var accessible = Math.Min(
            preserved,
            ClampTraceScore(((long)accessAptitude * 5L + ((long)achievement * 5L)) / 10L));
        return new ReincarnationTraceCandidate(
            domain,
            checked((ushort)preserved),
            checked((ushort)accessible));
    }

    private static int Average(int left, int right) => checked((left + right) / 2);

    private static int ClampTraceScore(long value) => checked((int)Math.Clamp(
        value,
        0,
        QualityValue.MaximumHundredths));
}
