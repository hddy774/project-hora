using Chronicles.Core;

namespace Chronicles.Simulation;

internal sealed partial class AgentStore
{
    private static readonly string[] FamilyNames =
    [
        "강", "진", "백", "유", "서", "하", "연", "남궁", "제갈", "모용", "독고", "천",
    ];

    private static readonly string[] GivenSyllables =
    [
        "하", "진", "연", "운", "설", "월", "천", "명", "현", "류", "백", "린", "소", "검", "우", "아",
    ];

    public AgentStore(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        Count = count;
        Names = new string[count];
        SpiritualRoots = new ushort[count];
        Bones = new ushort[count];
        Constitutions = new ushort[count];
        Comprehensions = new ushort[count];
        Minds = new ushort[count];
        Souls = new ushort[count];
        AptitudeComponents = new ushort[checked(count * AptitudeRules.TotalComponentCount)];
        AptitudeRoleScoreCache = new AptitudeRoleScores[count];
        CultivationAptitudeCache = new Aptitudes[count];
        CombatAptitudeCache = new Aptitudes[count];
        SoulIdentityIds = new SoulIdentityId[count];
        BodyIdentityIds = new BodyIdentityId[count];
        PersonIdentityIds = new PersonIdentityId[count];
        LifeStates = new PersonLifeState[count];
        Genders = new GenderIdentity[count];
        SoulEmbodimentStates = new SoulEmbodimentState[count];
        BodyLifeStates = new BodyLifeState[count];
        CurrentLifePhases = new CurrentLifePhase[count];
        AppearanceStates = new BodyAppearanceState[count];
        LifeAuthorityRevisions = new long[count];
        LatestLifeTransitions = new LifeTransitionResult[count];
        DecisionTraces = new AgentDecisionTraceState[count];
        DecisionAuthorityRevisions = new long[count];
        TeachingTraces = new TeachingTraceState[count];
        TeachingAuthorityRevisions = new long[count];
        LearningTraces = new TeachingTraceState[count];
        LearningAuthorityRevisions = new long[count];
        TechniqueMisunderstandingPoints = new ushort[count];
        SpellMisunderstandingPoints = new ushort[count];
        BirthTicks = new long[count];
        LifeNumbers = new int[count];
        BottleneckFailures = new ushort[count];
        Chronicles = new CompactChronicleStore(count);
        PersonalCenturyChronicles = new PersonalChronicleArchiveStore(count, Chronicles);
        PersonalLogs = new PersonalLogRingStore(count);
        RealmStages = new RealmStage[count];
        RealmLayers = new byte[count];
        CultivationProgress = new int[count];
        PendingCultivationWork = new int[count];
        LastCultivationWorkBefore = new int[count];
        LastCultivationWorkGain = new int[count];
        CultivationStates = new CultivationState[count];
        CultivationBottleneckCauses = new CultivationBottleneckCause[count];
        FloorNineCompleted = new bool[count];
        GreatPerfectionAchieved = new bool[count];
        EntryGatePassMasks = new byte[count];
        EntryGateTemperings = new ushort[checked(count * CultivationRules.EntryGateSlotCount)];
        FoundationRoutes = new FoundationRoute[count];
        CultivationComponentValues = new ushort[checked(count * CultivationRules.RealmComponentSlotCount)];
        CultivationComponentLimits = new ushort[checked(count * CultivationRules.RealmComponentSlotCount)];
        FormationValues = new ushort[checked(count * CultivationRules.FormationSlotCount)];
        FoundationFormations = new FoundationFormationAuthorityStore(count);
        CultivationTraces = new CultivationTraceState[count];
        CultivationAuthorityRevisions = new long[count];
        CultivationPlans = new CultivationActionPlanState[count];
        CultivationExperience = new long[count];
        FormationInstallationExperience = new ushort[count];
        TechniqueQualities = new ushort[count];
        TechniqueMasteries = new ushort[count];
        TechniqueTrainingExperience = new long[count];
        TechniqueUnderstanding = new ushort[count];
        SpellQualities = new ushort[count];
        SpellMasteries = new ushort[count];
        SpellTrainingExperience = new long[count];
        SpellUnderstanding = new ushort[count];
        CombatExperience = new int[count];
        CombatUnderstanding = new ushort[count];
        Wins = new int[count];
        Losses = new int[count];
        Health = new int[count];
        MaximumHealth = new int[count];
        Qi = new int[count];
        MaximumQi = new int[count];
        Activities = new AgentActivity[count];
        NextDecisionTicks = new long[count];
        LastActionMonths = new long[count];
        LastActionTicks = new long[count];
        ActionsTaken = new int[count];
        TargetRanks = new int[count];
        Opponents = new int[count];
        LastOpponents = new int[count];
        LastCombatTicks = new long[count];
        CombatStartedTicks = new long[count];
        CombatActions = new int[count];
        CombatTurnOwners = new int[count];
        InjuryPoints = new ushort[count];
        CombatAttributes = new CombatAttributes[count];
        Guarding = new bool[count];
        ChallengesIssued = new int[count];
        SpiritStones = new long[count];
        GrowthPills = new int[count];
        BreakthroughPills = new int[count];
        MissionsCompleted = new int[count];
        ActiveMissionMaterials = new byte[count];
        LocationIds = new ushort[count];
        TravelDestinationIds = new ushort[count];
        MonsterHunts = new int[count];
        MonsterVictories = new int[count];
        MaterialInventory = new int[checked(count * EconomyRules.MaterialCount)];
        EquippedTreasureQualities = new ushort[checked(count * EconomyRules.TreasureSlotCount)];
        Power = new CombatPowerBreakdown[count];
        Array.Fill(Opponents, -1);
        Array.Fill(LastOpponents, -1);
        Array.Fill(CombatTurnOwners, -1);
        Array.Fill(ActiveMissionMaterials, byte.MaxValue);
        Array.Fill(EquippedTreasureQualities, EconomyRules.EmptyTreasureQuality);
        Array.Fill(LastActionMonths, -1);
        Array.Fill(TeachingTraces, TeachingTraceState.Empty);
        Array.Fill(LearningTraces, TeachingTraceState.Empty);
        Array.Fill(CultivationPlans, CultivationActionPlanState.Empty);
    }

    public int Count { get; }
    public string[] Names { get; }
    public ushort[] SpiritualRoots { get; }
    public ushort[] Bones { get; }
    public ushort[] Constitutions { get; }
    public ushort[] Comprehensions { get; }
    public ushort[] Minds { get; }
    public ushort[] Souls { get; }
    public ushort[] AptitudeComponents { get; }
    private AptitudeRoleScores[] AptitudeRoleScoreCache { get; }
    private Aptitudes[] CultivationAptitudeCache { get; }
    private Aptitudes[] CombatAptitudeCache { get; }
    public SoulIdentityId[] SoulIdentityIds { get; }
    public BodyIdentityId[] BodyIdentityIds { get; }
    public PersonIdentityId[] PersonIdentityIds { get; }
    public PersonLifeState[] LifeStates { get; }
    public GenderIdentity[] Genders { get; }
    public SoulEmbodimentState[] SoulEmbodimentStates { get; }
    public BodyLifeState[] BodyLifeStates { get; }
    public CurrentLifePhase[] CurrentLifePhases { get; }
    public BodyAppearanceState[] AppearanceStates { get; }
    public long[] LifeAuthorityRevisions { get; }
    public LifeTransitionResult[] LatestLifeTransitions { get; }
    public AgentDecisionTraceState[] DecisionTraces { get; }
    public long[] DecisionAuthorityRevisions { get; }
    public TeachingTraceState[] TeachingTraces { get; }
    public long[] TeachingAuthorityRevisions { get; }
    public TeachingTraceState[] LearningTraces { get; }
    public long[] LearningAuthorityRevisions { get; }
    public ushort[] TechniqueMisunderstandingPoints { get; }
    public ushort[] SpellMisunderstandingPoints { get; }
    public long[] BirthTicks { get; }
    public int[] LifeNumbers { get; }
    public ushort[] BottleneckFailures { get; }
    public CompactChronicleStore Chronicles { get; }
    public PersonalChronicleArchiveStore PersonalCenturyChronicles { get; }
    public PersonalLogRingStore PersonalLogs { get; }
    public RealmStage[] RealmStages { get; }
    public byte[] RealmLayers { get; }
    public int[] CultivationProgress { get; }
    public int[] PendingCultivationWork { get; }
    public int[] LastCultivationWorkBefore { get; }
    public int[] LastCultivationWorkGain { get; }
    public CultivationState[] CultivationStates { get; }
    public CultivationBottleneckCause[] CultivationBottleneckCauses { get; }
    public bool[] FloorNineCompleted { get; }
    public bool[] GreatPerfectionAchieved { get; }
    public byte[] EntryGatePassMasks { get; }
    public ushort[] EntryGateTemperings { get; }
    public FoundationRoute[] FoundationRoutes { get; }
    public ushort[] CultivationComponentValues { get; }
    public ushort[] CultivationComponentLimits { get; }
    public ushort[] FormationValues { get; }
    public FoundationFormationAuthorityStore FoundationFormations { get; }
    public CultivationTraceState[] CultivationTraces { get; }
    public long[] CultivationAuthorityRevisions { get; }
    public CultivationActionPlanState[] CultivationPlans { get; }
    public long[] CultivationExperience { get; }
    public ushort[] FormationInstallationExperience { get; }
    public ushort[] TechniqueQualities { get; }
    public ushort[] TechniqueMasteries { get; }
    public long[] TechniqueTrainingExperience { get; }
    public ushort[] TechniqueUnderstanding { get; }
    public ushort[] SpellQualities { get; }
    public ushort[] SpellMasteries { get; }
    public long[] SpellTrainingExperience { get; }
    public ushort[] SpellUnderstanding { get; }
    public int[] CombatExperience { get; }
    public ushort[] CombatUnderstanding { get; }
    public int[] Wins { get; }
    public int[] Losses { get; }
    public int[] Health { get; }
    public int[] MaximumHealth { get; }
    public int[] Qi { get; }
    public int[] MaximumQi { get; }
    public AgentActivity[] Activities { get; }
    public long[] NextDecisionTicks { get; }
    public long[] LastActionMonths { get; }
    public long[] LastActionTicks { get; }
    public int[] ActionsTaken { get; }
    public int[] TargetRanks { get; }
    public int[] Opponents { get; }
    public int[] LastOpponents { get; }
    public long[] LastCombatTicks { get; }
    public long[] CombatStartedTicks { get; }
    public int[] CombatActions { get; }
    public int[] CombatTurnOwners { get; }
    public ushort[] InjuryPoints { get; }
    public CombatAttributes[] CombatAttributes { get; }
    public bool[] Guarding { get; }
    public int[] ChallengesIssued { get; }
    public long[] SpiritStones { get; }
    public int[] GrowthPills { get; }
    public int[] BreakthroughPills { get; }
    public int[] MissionsCompleted { get; }
    public byte[] ActiveMissionMaterials { get; }
    public ushort[] LocationIds { get; }
    public ushort[] TravelDestinationIds { get; }
    public int[] MonsterHunts { get; }
    public int[] MonsterVictories { get; }
    public int[] MaterialInventory { get; }
    public ushort[] EquippedTreasureQualities { get; }
    public CombatPowerBreakdown[] Power { get; }

    public void Initialize(int index, DeterministicRandom random)
    {
        GenerateAptitudeGroup(index, AptitudeDomain.Soul, random);
        LifeNumbers[index] = 1;
        SoulIdentityIds[index] = PersonCausalRules.CreateSoulId(index);
        LastActionMonths[index] = -1;
        ActionsTaken[index] = 0;
        InitializeBody(index, random, birthTick: 0);
    }

    public void Rebirth(int index, DeterministicRandom random, long birthTick)
    {
        // 승패는 한 생의 육신 상태가 아니라 수선자 전체 일대기의 누계다.
        var accumulatedWins = Wins[index];
        var accumulatedLosses = Losses[index];
        var accumulatedMonsterHunts = MonsterHunts[index];
        var accumulatedMonsterVictories = MonsterVictories[index];
        LifeNumbers[index] = checked(LifeNumbers[index] + 1);
        InitializeBody(index, random, birthTick);
        Wins[index] = accumulatedWins;
        Losses[index] = accumulatedLosses;
        MonsterHunts[index] = accumulatedMonsterHunts;
        MonsterVictories[index] = accumulatedMonsterVictories;
    }

    public Aptitudes GetAptitudes(int index) => new(
        new QualityValue(SpiritualRoots[index]),
        new QualityValue(Bones[index]),
        new QualityValue(Constitutions[index]),
        new QualityValue(Comprehensions[index]),
        new QualityValue(Minds[index]),
        new QualityValue(Souls[index]));

    public PersonIdentitySnapshot GetPersonIdentity(int index) => new(
        SoulIdentityIds[index],
        BodyIdentityIds[index],
        PersonIdentityIds[index],
        LifeStates[index]);

    public AgentDecisionSnapshot GetDecisionSnapshot(int index) => new(
        PersonCausalRules.DecisionContractId,
        PersonCausalRules.DecisionContractRevision,
        RequiresReview: true,
        DecisionTraces[index]);

    public void BeginTeaching(
        int teacher,
        int learner,
        RelationId relationId,
        TeachingTargetKind targetKind,
        ushort sourceQuality,
        TeachingKnowledgeRef targetKnowledgeRef,
        TeachingProvenanceId provenanceId,
        int sessionsCompleted,
        bool transmissionEstablished,
        long tick)
    {
        var revision = checked(TeachingAuthorityRevisions[teacher] + 1);
        TeachingAuthorityRevisions[teacher] = revision;
        TeachingTraces[teacher] = new TeachingTraceState(
            revision,
            IsActive: true,
            learner,
            PersonIdentityIds[teacher],
            PersonIdentityIds[learner],
            relationId,
            targetKind,
            sourceQuality,
            targetKnowledgeRef,
            provenanceId,
            tick,
            LastResultTick: 0,
            TeachingResultKind.None,
            LastGain: 0,
            sessionsCompleted,
            targetKind == TeachingTargetKind.Technique
                ? TechniqueMisunderstandingPoints[learner]
                : SpellMisunderstandingPoints[learner],
            transmissionEstablished);
        var learningRevision = checked(LearningAuthorityRevisions[learner] + 1);
        LearningAuthorityRevisions[learner] = learningRevision;
        LearningTraces[learner] = TeachingTraces[teacher] with
        {
            AuthorityRevision = learningRevision,
        };
    }

    public void CompleteTeaching(
        int teacher,
        long tick,
        TeachingResultKind result,
        int gain,
        ushort misunderstandingPoints,
        bool transmissionEstablished)
    {
        var previous = TeachingTraces[teacher];
        if (!previous.IsActive)
        {
            return;
        }

        var revision = checked(TeachingAuthorityRevisions[teacher] + 1);
        TeachingAuthorityRevisions[teacher] = revision;
        TeachingTraces[teacher] = previous with
        {
            AuthorityRevision = revision,
            IsActive = false,
            LastResultTick = tick,
            LastResult = result,
            LastGain = gain,
            SessionsCompleted = TeachingRules.IsTransmissionProgress(result)
                ? checked(previous.SessionsCompleted + 1)
                : previous.SessionsCompleted,
            MisunderstandingPoints = misunderstandingPoints,
            TransmissionEstablished = transmissionEstablished,
        };
        var learner = previous.LearnerAgent;
        if ((uint)learner < (uint)Count
            && LearningTraces[learner].ProvenanceId == previous.ProvenanceId)
        {
            var learningRevision = checked(LearningAuthorityRevisions[learner] + 1);
            LearningAuthorityRevisions[learner] = learningRevision;
            LearningTraces[learner] = TeachingTraces[teacher] with
            {
                AuthorityRevision = learningRevision,
            };
        }
    }

    public void RestoreTeaching(
        int index,
        long authorityRevision,
        TeachingTraceState latest,
        long learningAuthorityRevision,
        TeachingTraceState latestLearning,
        ushort techniqueMisunderstanding,
        ushort spellMisunderstanding)
    {
        TeachingAuthorityRevisions[index] = authorityRevision;
        TeachingTraces[index] = latest;
        LearningAuthorityRevisions[index] = learningAuthorityRevision;
        LearningTraces[index] = latestLearning;
        TechniqueMisunderstandingPoints[index] = techniqueMisunderstanding;
        SpellMisunderstandingPoints[index] = spellMisunderstanding;
    }

    public void RecordDecision(
        int index,
        long tick,
        AgentGoalKind supportingGoal,
        AgentActivity selectedActivity,
        DecisionReasonCode selectedReason,
        DecisionResolutionKind resolution,
        AgentActivity? alternativeActivity = null)
    {
        var revision = checked(DecisionAuthorityRevisions[index] + 1);
        DecisionAuthorityRevisions[index] = revision;
        DecisionTraces[index] = new AgentDecisionTraceState(
            tick,
            revision,
            AgentGoalKind.ReachRankOne,
            supportingGoal,
            selectedActivity,
            selectedReason,
            alternativeActivity,
            resolution);
    }

    public void RestoreDecision(
        int index,
        long authorityRevision,
        AgentDecisionTraceState latest)
    {
        DecisionAuthorityRevisions[index] = authorityRevision;
        DecisionTraces[index] = latest;
    }

    public AptitudeDetailSnapshot CreateAptitudeDetailSnapshot(int index)
    {
        var groups = new AptitudeGroupSnapshot[AptitudeRules.DomainCount];
        var sourceStart = checked(index * AptitudeRules.TotalComponentCount);
        var allComponents = AptitudeComponents.AsSpan(
            sourceStart,
            AptitudeRules.TotalComponentCount);
        for (var domainIndex = 0; domainIndex < groups.Length; domainIndex++)
        {
            var domain = (AptitudeDomain)domainIndex;
            var definition = AptitudeRules.GetGroup(domain);
            var components = new AptitudeComponentSnapshot[AptitudeRules.ComponentCountPerDomain];
            var groupStart = sourceStart + AptitudeRules.GetComponentOffset(domain);
            for (var component = 0; component < components.Length; component++)
            {
                var componentDefinition = definition.Components[component];
                components[component] = new AptitudeComponentSnapshot(
                    componentDefinition.StableId,
                    componentDefinition.KoreanName,
                    new QualityValue(AptitudeComponents[groupStart + component]));
            }

            var derivedAbilities = new AptitudeDerivedAbilitySnapshot[2];
            for (var abilityOffset = 0; abilityOffset < derivedAbilities.Length; abilityOffset++)
            {
                var ability = AptitudeRules.AllDerivedAbilities[(domainIndex * 2) + abilityOffset];
                derivedAbilities[abilityOffset] = new AptitudeDerivedAbilitySnapshot(
                    ability.StableId,
                    ability.KoreanName,
                    AptitudeRules.GetDerivedAbilityFormulaText(ability.Ability),
                    QualityValue.FromHundredths(
                        AptitudeRules.CalculateDerivedAbilityHundredths(
                            allComponents,
                            ability.Ability)));
            }

            groups[domainIndex] = new AptitudeGroupSnapshot(
                domain,
                definition.StableId,
                definition.KoreanName,
                GetAptitudeSummary(index, domain),
                components,
                derivedAbilities);
        }

        return new AptitudeDetailSnapshot(
            AptitudeRules.SummaryFormulaId,
            AptitudeRules.FormulaRevision,
            RequiresReview: true,
            AptitudeRoleScoreCache[index],
            groups);
    }

    public AptitudeRoleScores GetAptitudeRoleScores(int index) =>
        AptitudeRoleScoreCache[index];

    public Aptitudes GetContextAptitudes(int index, bool combat) =>
        combat ? CombatAptitudeCache[index] : CultivationAptitudeCache[index];

    public int GetDerivedAptitude(int index, AptitudeDerivedAbility ability) => ability switch
    {
        AptitudeDerivedAbility.SpiritualRootCultivation =>
            CultivationAptitudeCache[index].SpiritualRoot.Hundredths,
        AptitudeDerivedAbility.SpiritualRootCombat =>
            CombatAptitudeCache[index].SpiritualRoot.Hundredths,
        AptitudeDerivedAbility.BoneCultivation => CultivationAptitudeCache[index].Bone.Hundredths,
        AptitudeDerivedAbility.BoneCombat => CombatAptitudeCache[index].Bone.Hundredths,
        AptitudeDerivedAbility.ConstitutionCultivation =>
            CultivationAptitudeCache[index].Constitution.Hundredths,
        AptitudeDerivedAbility.ConstitutionCombat =>
            CombatAptitudeCache[index].Constitution.Hundredths,
        AptitudeDerivedAbility.ComprehensionLearning =>
            CultivationAptitudeCache[index].Comprehension.Hundredths,
        AptitudeDerivedAbility.ComprehensionCombat =>
            CombatAptitudeCache[index].Comprehension.Hundredths,
        AptitudeDerivedAbility.MindCultivation => CultivationAptitudeCache[index].Mind.Hundredths,
        AptitudeDerivedAbility.MindCombat => CombatAptitudeCache[index].Mind.Hundredths,
        AptitudeDerivedAbility.SoulGrowth => CultivationAptitudeCache[index].Soul.Hundredths,
        AptitudeDerivedAbility.SoulCombat => CombatAptitudeCache[index].Soul.Hundredths,
        _ => throw new ArgumentOutOfRangeException(nameof(ability)),
    };

    public ushort[] CopyAptitudeComponents(int index)
    {
        var copy = new ushort[AptitudeRules.TotalComponentCount];
        Array.Copy(
            AptitudeComponents,
            checked(index * AptitudeRules.TotalComponentCount),
            copy,
            0,
            copy.Length);
        return copy;
    }

    public void RestoreAptitudeComponents(int index, ushort[] components)
    {
        if (components.Length != AptitudeRules.TotalComponentCount)
        {
            throw new InvalidDataException("세부 자질 체크포인트 길이가 잘못되었습니다.");
        }

        Array.Copy(
            components,
            0,
            AptitudeComponents,
            checked(index * AptitudeRules.TotalComponentCount),
            components.Length);
        RefreshAptitudeSummaries(index);
        RefreshAptitudeDerived(index);
    }

    public void RestoreLegacyAptitudeComponents(int index, Aptitudes aptitudes) =>
        RestoreAptitudeComponents(index, AptitudeRules.CreateUniformComponents(aptitudes));

    public void RecalculatePower(int index)
    {
        var previousMaximumHealth = MaximumHealth[index];
        var previousMaximumQi = MaximumQi[index];
        var combatAptitudes = GetContextAptitudes(index, combat: true);
        var aptitudeBonuses = AptitudeCombatBonusFormula.Calculate(GetAptitudeRoleScores(index));
        var combatMastery = CombatProgressionRules.GetMasteryHundredths(CombatExperience[index]);
        var attributes = CombatAttributeFormula.Calculate(
            RealmStages[index],
            RealmLayers[index],
            combatAptitudes,
            aptitudeBonuses,
            new QualityValue(TechniqueQualities[index]),
            TechniqueMasteries[index],
            TechniqueUnderstanding[index],
            new QualityValue(SpellQualities[index]),
            SpellMasteries[index],
            SpellUnderstanding[index],
            combatMastery,
            CombatUnderstanding[index],
            GetTreasureCombatEffect(index, attack: true),
            GetTreasureCombatEffect(index, attack: false));
        CombatAttributes[index] = attributes;
        MaximumHealth[index] = attributes.MaximumHealth;
        MaximumQi[index] = attributes.MaximumQi;
        Health[index] = Health[index] <= 0
            ? 0
            : Math.Clamp(
                checked(Health[index] + attributes.MaximumHealth - previousMaximumHealth),
                1,
                attributes.MaximumHealth);
        Qi[index] = Qi[index] <= 0
            ? 0
            : Math.Clamp(
                checked(Qi[index] + attributes.MaximumQi - previousMaximumQi),
                1,
                attributes.MaximumQi);
        Power[index] = CombatPowerFormula.Calculate(
            RealmStages[index],
            RealmLayers[index],
            combatAptitudes,
            aptitudeBonuses,
            new QualityValue(TechniqueQualities[index]),
            TechniqueMasteries[index],
            TechniqueUnderstanding[index],
            new QualityValue(SpellQualities[index]),
            SpellMasteries[index],
            SpellUnderstanding[index],
            CombatExperience[index],
            combatMastery,
            CombatUnderstanding[index],
            GetTreasurePower(index));
    }

    private void InitializeBody(int index, DeterministicRandom random, long birthTick)
    {
        Names[index] = CreateName(index, random);
        GenerateAptitudeGroup(index, AptitudeDomain.SpiritualRoot, random);
        GenerateAptitudeGroup(index, AptitudeDomain.Bone, random);
        GenerateAptitudeGroup(index, AptitudeDomain.Constitution, random);
        GenerateAptitudeGroup(index, AptitudeDomain.Comprehension, random);
        GenerateAptitudeGroup(index, AptitudeDomain.Mind, random);
        RefreshAptitudeDerived(index);
        BodyIdentityIds[index] = PersonCausalRules.CreateBodyId(index, LifeNumbers[index]);
        PersonIdentityIds[index] = PersonCausalRules.CreatePersonId(index, LifeNumbers[index]);
        LifeStates[index] = PersonLifeState.Living;
        InitializeLifeAuthority(index, birthTick);
        BirthTicks[index] = birthTick;
        BottleneckFailures[index] = 0;
        RealmStages[index] = RealmStage.QiRefining;
        RealmLayers[index] = 1;
        CultivationProgress[index] = 0;
        PendingCultivationWork[index] = 0;
        LastCultivationWorkBefore[index] = 0;
        LastCultivationWorkGain[index] = 0;
        CultivationStates[index] = CultivationState.PreCultivation;
        CultivationBottleneckCauses[index] = CultivationBottleneckCause.None;
        FloorNineCompleted[index] = false;
        GreatPerfectionAchieved[index] = false;
        EntryGatePassMasks[index] = 0;
        Array.Clear(
            EntryGateTemperings,
            index * CultivationRules.EntryGateSlotCount,
            CultivationRules.EntryGateSlotCount);
        FoundationRoutes[index] = FoundationRoute.None;
        FoundationFormations.Reset(index);
        CultivationTraces[index] = CultivationTraceState.Empty;
        CultivationAuthorityRevisions[index] = 0;
        CultivationPlans[index] = CultivationActionPlanState.Empty;
        CultivationExperience[index] = 0;
        FormationInstallationExperience[index] = 0;
        Array.Clear(
            CultivationComponentValues,
            index * CultivationRules.RealmComponentSlotCount,
            CultivationRules.RealmComponentSlotCount);
        Array.Clear(
            CultivationComponentLimits,
            index * CultivationRules.RealmComponentSlotCount,
            CultivationRules.RealmComponentSlotCount);
        Array.Clear(
            FormationValues,
            index * CultivationRules.FormationSlotCount,
            CultivationRules.FormationSlotCount);
        TechniqueQualities[index] = CreateSkillQuality(random);
        TechniqueMasteries[index] = checked((ushort)(500 + random.NextInt(2_501)));
        TechniqueTrainingExperience[index] = SkillProgressionRules.GetRequiredExperience(TechniqueMasteries[index]);
        TechniqueUnderstanding[index] = 0;
        SpellQualities[index] = CreateSkillQuality(random);
        SpellMasteries[index] = checked((ushort)(400 + random.NextInt(2_201)));
        SpellTrainingExperience[index] = SkillProgressionRules.GetRequiredExperience(SpellMasteries[index]);
        SpellUnderstanding[index] = 0;
        CombatExperience[index] = 0;
        CombatUnderstanding[index] = 0;
        TechniqueMisunderstandingPoints[index] = 0;
        SpellMisunderstandingPoints[index] = 0;
        TeachingAuthorityRevisions[index] = 0;
        TeachingTraces[index] = TeachingTraceState.Empty;
        LearningAuthorityRevisions[index] = 0;
        LearningTraces[index] = TeachingTraceState.Empty;
        Wins[index] = 0;
        Losses[index] = 0;
        MaximumHealth[index] = 800
            + (GetEffectiveWhole(Constitutions[index]) * 8)
            + (GetEffectiveWhole(Bones[index]) * 3);
        Health[index] = MaximumHealth[index];
        MaximumQi[index] = 500
            + (GetEffectiveWhole(SpiritualRoots[index]) * 10)
            + (GetEffectiveWhole(Comprehensions[index]) * 2);
        Qi[index] = MaximumQi[index];
        Activities[index] = AgentActivity.Cultivating;
        RecordDecision(
            index,
            birthTick,
            AgentGoalKind.EnterCultivation,
            AgentActivity.Cultivating,
            DecisionReasonCode.InitialLifeCultivation,
            DecisionResolutionKind.Forced);
        NextDecisionTicks[index] = checked(birthTick + 1 + random.NextInt(20));
        TargetRanks[index] = 1;
        Opponents[index] = -1;
        LastOpponents[index] = -1;
        LastCombatTicks[index] = birthTick;
        CombatStartedTicks[index] = 0;
        CombatActions[index] = 0;
        CombatTurnOwners[index] = -1;
        InjuryPoints[index] = 0;
        Guarding[index] = false;
        ChallengesIssued[index] = 0;
        SpiritStones[index] = 0;
        GrowthPills[index] = 0;
        BreakthroughPills[index] = 0;
        MissionsCompleted[index] = 0;
        ActiveMissionMaterials[index] = byte.MaxValue;
        LocationIds[index] = WorldMapRules.CityLocationId;
        TravelDestinationIds[index] = WorldMapRules.CityLocationId;
        MonsterHunts[index] = 0;
        MonsterVictories[index] = 0;
        Array.Clear(MaterialInventory, index * EconomyRules.MaterialCount, EconomyRules.MaterialCount);
        Array.Fill(
            EquippedTreasureQualities,
            EconomyRules.EmptyTreasureQuality,
            index * EconomyRules.TreasureSlotCount,
            EconomyRules.TreasureSlotCount);
        RecalculatePower(index);
    }

    public int GetMaterialCount(int index, MaterialId material) =>
        MaterialInventory[GetMaterialOffset(index, material)];

    public int GetCultivationComponentValue(int index, int component) =>
        CultivationComponentValues[GetCultivationComponentOffset(index, component)];

    public int GetCultivationComponentLimit(int index, int component) =>
        CultivationComponentLimits[GetCultivationComponentOffset(index, component)];

    public void SetCultivationComponent(int index, int component, int value, int limit)
    {
        if (value is < 0 or > QualityValue.MaximumHundredths
            || limit is < 0 or > QualityValue.MaximumHundredths
            || value > limit)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        var offset = GetCultivationComponentOffset(index, component);
        CultivationComponentValues[offset] = checked((ushort)value);
        CultivationComponentLimits[offset] = checked((ushort)limit);
    }

    public int GetCultivationComponentAverage(int index, int count)
    {
        if (count == 0)
        {
            return 0;
        }

        var start = index * CultivationRules.RealmComponentSlotCount;
        var total = 0;
        for (var component = 0; component < count; component++)
        {
            total += CultivationComponentValues[start + component];
        }

        return total / count;
    }

    public ushort[] CopyCultivationComponentValues(int index) =>
        CopyCultivationArray(
            CultivationComponentValues,
            index,
            CultivationRules.RealmComponentSlotCount);

    public ushort[] CopyCultivationComponentLimits(int index) =>
        CopyCultivationArray(
            CultivationComponentLimits,
            index,
            CultivationRules.RealmComponentSlotCount);

    public ushort[] CopyFormationValues(int index) =>
        CopyCultivationArray(FormationValues, index, CultivationRules.FormationSlotCount);

    public int GetEntryGateTempering(int index, int gateIndex) =>
        EntryGateTemperings[GetEntryGateTemperingOffset(index, gateIndex)];

    public void AddEntryGateTempering(int index, int gateIndex, int gain)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gain);
        var offset = GetEntryGateTemperingOffset(index, gateIndex);
        EntryGateTemperings[offset] = checked((ushort)Math.Min(
            QualityValue.MaximumHundredths,
            EntryGateTemperings[offset] + gain));
    }

    public ushort[] CopyEntryGateTemperings(int index) =>
        CopyCultivationArray(EntryGateTemperings, index, CultivationRules.EntryGateSlotCount);

    public void RestoreCultivationArrays(
        int index,
        ushort[] componentValues,
        ushort[] componentLimits,
        ushort[] formationValues,
        ushort[]? entryGateTemperings = null)
    {
        Array.Copy(
            componentValues,
            0,
            CultivationComponentValues,
            index * CultivationRules.RealmComponentSlotCount,
            CultivationRules.RealmComponentSlotCount);
        Array.Copy(
            componentLimits,
            0,
            CultivationComponentLimits,
            index * CultivationRules.RealmComponentSlotCount,
            CultivationRules.RealmComponentSlotCount);
        Array.Copy(
            formationValues,
            0,
            FormationValues,
            index * CultivationRules.FormationSlotCount,
            CultivationRules.FormationSlotCount);
        if (entryGateTemperings is not null)
        {
            Array.Copy(
                entryGateTemperings,
                0,
                EntryGateTemperings,
                index * CultivationRules.EntryGateSlotCount,
                CultivationRules.EntryGateSlotCount);
        }
    }

    public void AddMaterial(int index, MaterialId material, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var offset = GetMaterialOffset(index, material);
        MaterialInventory[offset] = checked(MaterialInventory[offset] + count);
    }

    public void ConsumeMaterial(int index, MaterialId material, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        var offset = GetMaterialOffset(index, material);
        if (MaterialInventory[offset] < count)
        {
            throw new InvalidOperationException("보유하지 않은 재료를 소비할 수 없습니다.");
        }

        MaterialInventory[offset] -= count;
    }

    public int GetInventoryItemCount(int index)
    {
        var total = 0;
        var start = index * EconomyRules.MaterialCount;
        for (var material = 0; material < EconomyRules.MaterialCount; material++)
        {
            total = checked(total + MaterialInventory[start + material]);
        }

        return total;
    }

    public int[] CopyMaterialInventory(int index)
    {
        var copy = new int[EconomyRules.MaterialCount];
        Array.Copy(
            MaterialInventory,
            index * EconomyRules.MaterialCount,
            copy,
            0,
            copy.Length);
        return copy;
    }

    public ushort[] CopyEquippedTreasureQualities(int index)
    {
        var copy = new ushort[EconomyRules.TreasureSlotCount];
        Array.Copy(
            EquippedTreasureQualities,
            index * EconomyRules.TreasureSlotCount,
            copy,
            0,
            copy.Length);
        return copy;
    }

    public void RestoreEconomyState(int index, int[] materials, ushort[] treasures)
    {
        var materialStart = index * EconomyRules.MaterialCount;
        if (materials.Length == EconomyRules.MaterialCount)
        {
            Array.Copy(materials, 0, MaterialInventory, materialStart, EconomyRules.MaterialCount);
        }
        else if (materials.Length == EconomyRules.LegacyMaterialCount)
        {
            MaterialInventory[materialStart + (int)MaterialId.TreasureMaterial] = checked(
                Sum(materials.AsSpan(0, 10))
                + Sum(materials.AsSpan(20, 5)));
            MaterialInventory[materialStart + (int)MaterialId.PillMaterial] = checked(
                Sum(materials.AsSpan(10, 10))
                + Sum(materials.AsSpan(25, 5)));
        }
        else
        {
            throw new InvalidDataException("지원하지 않는 재료 인벤토리 형식입니다.");
        }

        var treasureStart = index * EconomyRules.TreasureSlotCount;
        if (treasures.Length == EconomyRules.TreasureSlotCount)
        {
            Array.Copy(treasures, 0, EquippedTreasureQualities, treasureStart, EconomyRules.TreasureSlotCount);
        }
        else if (treasures.Length == EconomyRules.LegacyTreasureSlotCount)
        {
            EquippedTreasureQualities[treasureStart + (int)TreasureSlot.NatalTreasure] = treasures[0];
            EquippedTreasureQualities[treasureStart + (int)TreasureSlot.DefenseTreasure] =
                GetBestLegacyTreasure(treasures.AsSpan(1, 3));
            EquippedTreasureQualities[treasureStart + (int)TreasureSlot.AttackTreasure] =
                GetBestLegacyTreasure(treasures.AsSpan(4, 3));
        }
        else
        {
            throw new InvalidDataException("지원하지 않는 법보 장착 형식입니다.");
        }
    }

    private static int Sum(ReadOnlySpan<int> values)
    {
        var total = 0;
        foreach (var value in values)
        {
            total = checked(total + value);
        }

        return total;
    }

    public ushort GetTreasureQuality(int index, TreasureSlot slot) =>
        EquippedTreasureQualities[GetTreasureOffset(index, slot)];

    public void SetTreasureQuality(int index, TreasureSlot slot, ushort quality) =>
        EquippedTreasureQualities[GetTreasureOffset(index, slot)] = quality;

    public long GetTreasurePower(int index)
    {
        long effectiveHundredths = 0;
        for (var slot = 0; slot < EconomyRules.TreasureSlotCount; slot++)
        {
            var raw = EquippedTreasureQualities[(index * EconomyRules.TreasureSlotCount) + slot];
            if (raw != EconomyRules.EmptyTreasureQuality)
            {
                effectiveHundredths = checked(
                    effectiveHundredths + QualityRules.GetEffectiveHundredths(new QualityValue(raw)));
            }
        }

        return SkillCombatScaling.GetTreasureDisplayContribution(effectiveHundredths);
    }

    public int GetTreasureCombatEffect(int index, bool attack)
    {
        long total = 0;
        for (var slotIndex = 0; slotIndex < EconomyRules.TreasureSlotCount; slotIndex++)
        {
            var slot = (TreasureSlot)slotIndex;
            if (slot != TreasureSlot.NatalTreasure
                && (attack ? !EconomyRules.IsWeapon(slot) : !EconomyRules.IsArmor(slot)))
            {
                continue;
            }

            var raw = GetTreasureQuality(index, slot);
            if (raw != EconomyRules.EmptyTreasureQuality)
            {
                total = checked(
                    total + QualityRules.GetEffectiveHundredths(new QualityValue(raw)));
            }
        }

        return SkillCombatScaling.GetTreasureAttributeContribution(total);
    }

    private static ushort CreateSkillQuality(DeterministicRandom random) =>
        checked((ushort)random.NextInt(EconomyRules.InitialSkillMaximumHundredths + 1));

    private void GenerateAptitudeGroup(
        int index,
        AptitudeDomain domain,
        DeterministicRandom random)
    {
        var offset = checked(
            (index * AptitudeRules.TotalComponentCount)
            + AptitudeRules.GetComponentOffset(domain));
        var summary = AptitudeRules.GenerateGroup(
            AptitudeComponents.AsSpan(offset, AptitudeRules.ComponentCountPerDomain),
            random);
        SetAptitudeSummary(index, domain, summary);
    }

    private void RefreshAptitudeSummaries(int index)
    {
        var start = checked(index * AptitudeRules.TotalComponentCount);
        var components = AptitudeComponents.AsSpan(start, AptitudeRules.TotalComponentCount);
        for (var domainIndex = 0; domainIndex < AptitudeRules.DomainCount; domainIndex++)
        {
            var domain = (AptitudeDomain)domainIndex;
            SetAptitudeSummary(
                index,
                domain,
                checked((ushort)AptitudeRules.CalculateSummaryHundredths(components, domain)));
        }
    }

    private void RefreshAptitudeDerived(int index)
    {
        var components = GetAptitudeComponents(index);
        AptitudeRoleScoreCache[index] = AptitudeRules.CalculateRoleScores(components);
        CultivationAptitudeCache[index] = AptitudeRules.CalculateContextAptitudes(
            components,
            combat: false);
        CombatAptitudeCache[index] = AptitudeRules.CalculateContextAptitudes(
            components,
            combat: true);
    }

    private QualityValue GetAptitudeSummary(int index, AptitudeDomain domain) =>
        new(domain switch
        {
            AptitudeDomain.SpiritualRoot => SpiritualRoots[index],
            AptitudeDomain.Bone => Bones[index],
            AptitudeDomain.Constitution => Constitutions[index],
            AptitudeDomain.Comprehension => Comprehensions[index],
            AptitudeDomain.Mind => Minds[index],
            AptitudeDomain.Soul => Souls[index],
            _ => throw new ArgumentOutOfRangeException(nameof(domain)),
        });

    private void SetAptitudeSummary(int index, AptitudeDomain domain, ushort value)
    {
        switch (domain)
        {
            case AptitudeDomain.SpiritualRoot:
                SpiritualRoots[index] = value;
                break;
            case AptitudeDomain.Bone:
                Bones[index] = value;
                break;
            case AptitudeDomain.Constitution:
                Constitutions[index] = value;
                break;
            case AptitudeDomain.Comprehension:
                Comprehensions[index] = value;
                break;
            case AptitudeDomain.Mind:
                Minds[index] = value;
                break;
            case AptitudeDomain.Soul:
                Souls[index] = value;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(domain));
        }
    }

    private ReadOnlySpan<ushort> GetAptitudeComponents(int index) =>
        AptitudeComponents.AsSpan(
            checked(index * AptitudeRules.TotalComponentCount),
            AptitudeRules.TotalComponentCount);

    private static ushort GetBestLegacyTreasure(ReadOnlySpan<ushort> qualities)
    {
        var best = EconomyRules.EmptyTreasureQuality;
        foreach (var quality in qualities)
        {
            if (quality != EconomyRules.EmptyTreasureQuality
                && (best == EconomyRules.EmptyTreasureQuality || quality > best))
            {
                best = quality;
            }
        }

        return best;
    }

    private static int GetEffectiveWhole(ushort rawQuality) =>
        QualityRules.GetEffectiveWhole(new QualityValue(rawQuality));

    private static int GetMaterialOffset(int index, MaterialId material) =>
        checked((index * EconomyRules.MaterialCount) + (int)material);

    private static int GetTreasureOffset(int index, TreasureSlot slot) =>
        checked((index * EconomyRules.TreasureSlotCount) + (int)slot);

    private static int GetCultivationComponentOffset(int index, int component)
    {
        if ((uint)component >= CultivationRules.RealmComponentSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(component));
        }

        return checked((index * CultivationRules.RealmComponentSlotCount) + component);
    }

    private static int GetEntryGateTemperingOffset(int index, int gateIndex)
    {
        if (gateIndex is < 0 or >= CultivationRules.EntryGateSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(gateIndex));
        }

        return checked((index * CultivationRules.EntryGateSlotCount) + gateIndex);
    }

    private static ushort[] CopyCultivationArray(ushort[] source, int index, int width)
    {
        var copy = new ushort[width];
        Array.Copy(source, index * width, copy, 0, width);
        return copy;
    }

    private static string CreateName(int index, DeterministicRandom random)
    {
        var family = FamilyNames[random.NextInt(FamilyNames.Length)];
        var first = GivenSyllables[random.NextInt(GivenSyllables.Length)];
        var second = GivenSyllables[random.NextInt(GivenSyllables.Length)];
        return $"{family}{first}{second}·{index + 1:00000}";
    }
}
