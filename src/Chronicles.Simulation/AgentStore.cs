using Chronicles.Core;

namespace Chronicles.Simulation;

internal sealed class AgentStore
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
        BirthTicks = new long[count];
        LifeNumbers = new int[count];
        BottleneckFailures = new ushort[count];
        Chronicles = new List<LifeChronicleEntry>[count];
        PersonalLogs = new List<PersonalLogEntry>[count];
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
        FoundationRoutes = new FoundationRoute[count];
        CultivationComponentValues = new ushort[checked(count * CultivationRules.RealmComponentSlotCount)];
        CultivationComponentLimits = new ushort[checked(count * CultivationRules.RealmComponentSlotCount)];
        FormationValues = new ushort[checked(count * CultivationRules.FormationSlotCount)];
        CultivationTraces = new CultivationTraceState[count];
        TechniqueQualities = new ushort[count];
        TechniqueMasteries = new ushort[count];
        TechniqueTrainingExperience = new long[count];
        TechniqueUnderstanding = new ushort[count];
        SpellQualities = new ushort[count];
        SpellMasteries = new ushort[count];
        SpellTrainingExperience = new long[count];
        SpellUnderstanding = new ushort[count];
        CombatExperience = new int[count];
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
        CombatActions = new byte[count];
        Guarding = new bool[count];
        ChallengesIssued = new int[count];
        SpiritStones = new long[count];
        GrowthPills = new int[count];
        BreakthroughPills = new int[count];
        MissionsCompleted = new int[count];
        ActiveMissionMaterials = new byte[count];
        MaterialInventory = new int[checked(count * EconomyRules.MaterialCount)];
        EquippedTreasureQualities = new ushort[checked(count * EconomyRules.TreasureSlotCount)];
        Power = new CombatPowerBreakdown[count];
        Array.Fill(Opponents, -1);
        Array.Fill(LastOpponents, -1);
        Array.Fill(ActiveMissionMaterials, byte.MaxValue);
        Array.Fill(EquippedTreasureQualities, EconomyRules.EmptyTreasureQuality);
        Array.Fill(LastActionMonths, -1);
        for (var index = 0; index < count; index++)
        {
            Chronicles[index] = [];
            PersonalLogs[index] = [];
        }
    }

    public int Count { get; }
    public string[] Names { get; }
    public ushort[] SpiritualRoots { get; }
    public ushort[] Bones { get; }
    public ushort[] Constitutions { get; }
    public ushort[] Comprehensions { get; }
    public ushort[] Minds { get; }
    public ushort[] Souls { get; }
    public long[] BirthTicks { get; }
    public int[] LifeNumbers { get; }
    public ushort[] BottleneckFailures { get; }
    public List<LifeChronicleEntry>[] Chronicles { get; }
    public List<PersonalLogEntry>[] PersonalLogs { get; }
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
    public FoundationRoute[] FoundationRoutes { get; }
    public ushort[] CultivationComponentValues { get; }
    public ushort[] CultivationComponentLimits { get; }
    public ushort[] FormationValues { get; }
    public CultivationTraceState[] CultivationTraces { get; }
    public ushort[] TechniqueQualities { get; }
    public ushort[] TechniqueMasteries { get; }
    public long[] TechniqueTrainingExperience { get; }
    public ushort[] TechniqueUnderstanding { get; }
    public ushort[] SpellQualities { get; }
    public ushort[] SpellMasteries { get; }
    public long[] SpellTrainingExperience { get; }
    public ushort[] SpellUnderstanding { get; }
    public int[] CombatExperience { get; }
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
    public byte[] CombatActions { get; }
    public bool[] Guarding { get; }
    public int[] ChallengesIssued { get; }
    public long[] SpiritStones { get; }
    public int[] GrowthPills { get; }
    public int[] BreakthroughPills { get; }
    public int[] MissionsCompleted { get; }
    public byte[] ActiveMissionMaterials { get; }
    public int[] MaterialInventory { get; }
    public ushort[] EquippedTreasureQualities { get; }
    public CombatPowerBreakdown[] Power { get; }

    public void Initialize(int index, DeterministicRandom random)
    {
        Souls[index] = CreateAptitude(random);
        LifeNumbers[index] = 1;
        LastActionMonths[index] = -1;
        ActionsTaken[index] = 0;
        InitializeBody(index, random, birthTick: 0);
    }

    public void Rebirth(int index, DeterministicRandom random, long birthTick)
    {
        LifeNumbers[index] = checked(LifeNumbers[index] + 1);
        InitializeBody(index, random, birthTick);
    }

    public Aptitudes GetAptitudes(int index) => new(
        new QualityValue(SpiritualRoots[index]),
        new QualityValue(Bones[index]),
        new QualityValue(Constitutions[index]),
        new QualityValue(Comprehensions[index]),
        new QualityValue(Minds[index]),
        new QualityValue(Souls[index]));

    public void RecalculatePower(int index)
    {
        Power[index] = CombatPowerFormula.Calculate(
            RealmStages[index],
            RealmLayers[index],
            GetAptitudes(index),
            new QualityValue(TechniqueQualities[index]),
            TechniqueMasteries[index],
            TechniqueUnderstanding[index],
            new QualityValue(SpellQualities[index]),
            SpellMasteries[index],
            SpellUnderstanding[index],
            CombatExperience[index],
            GetTreasurePower(index));
    }

    private void InitializeBody(int index, DeterministicRandom random, long birthTick)
    {
        Names[index] = CreateName(index, random);
        SpiritualRoots[index] = CreateAptitude(random);
        Bones[index] = CreateAptitude(random);
        Constitutions[index] = CreateAptitude(random);
        Comprehensions[index] = CreateAptitude(random);
        Minds[index] = CreateAptitude(random);
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
        FoundationRoutes[index] = FoundationRoute.None;
        CultivationTraces[index] = CultivationTraceState.Empty;
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
        TechniqueQualities[index] = CreateQuality(random);
        TechniqueMasteries[index] = checked((ushort)(500 + random.NextInt(2_501)));
        TechniqueTrainingExperience[index] = SkillProgressionRules.GetRequiredExperience(TechniqueMasteries[index]);
        TechniqueUnderstanding[index] = 0;
        SpellQualities[index] = CreateQuality(random);
        SpellMasteries[index] = checked((ushort)(400 + random.NextInt(2_201)));
        SpellTrainingExperience[index] = SkillProgressionRules.GetRequiredExperience(SpellMasteries[index]);
        SpellUnderstanding[index] = 0;
        CombatExperience[index] = 0;
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
        NextDecisionTicks[index] = checked(birthTick + 1 + random.NextInt(20));
        TargetRanks[index] = 1;
        Opponents[index] = -1;
        LastOpponents[index] = -1;
        LastCombatTicks[index] = birthTick;
        CombatStartedTicks[index] = 0;
        CombatActions[index] = 0;
        Guarding[index] = false;
        ChallengesIssued[index] = 0;
        SpiritStones[index] = 0;
        GrowthPills[index] = 0;
        BreakthroughPills[index] = 0;
        MissionsCompleted[index] = 0;
        ActiveMissionMaterials[index] = byte.MaxValue;
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

    public void RestoreCultivationArrays(
        int index,
        ushort[] componentValues,
        ushort[] componentLimits,
        ushort[] formationValues)
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
        Array.Copy(
            materials,
            0,
            MaterialInventory,
            index * EconomyRules.MaterialCount,
            EconomyRules.MaterialCount);
        Array.Copy(
            treasures,
            0,
            EquippedTreasureQualities,
            index * EconomyRules.TreasureSlotCount,
            EconomyRules.TreasureSlotCount);
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

        return checked(effectiveHundredths * 250 / 100);
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
                total += QualityRules.GetEffectiveWhole(new QualityValue(raw));
            }
        }

        return checked((int)Math.Min(int.MaxValue, total));
    }

    private static ushort CreateAptitude(DeterministicRandom random) => CreateQuality(random);

    private static ushort CreateQuality(DeterministicRandom random) =>
        checked((ushort)random.NextInt(QualityValue.MaximumHundredths + 1));

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
