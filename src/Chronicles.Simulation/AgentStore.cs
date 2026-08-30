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
        RealmStages = new RealmStage[count];
        RealmLayers = new byte[count];
        CultivationProgress = new int[count];
        TechniqueQualities = new ushort[count];
        TechniqueMasteries = new ushort[count];
        SpellQualities = new ushort[count];
        SpellMasteries = new ushort[count];
        CombatExperience = new int[count];
        Wins = new int[count];
        Losses = new int[count];
        Health = new int[count];
        MaximumHealth = new int[count];
        Qi = new int[count];
        MaximumQi = new int[count];
        Activities = new AgentActivity[count];
        NextDecisionTicks = new long[count];
        TargetRanks = new int[count];
        Opponents = new int[count];
        LastOpponents = new int[count];
        LastCombatTicks = new long[count];
        CombatStartedTicks = new long[count];
        CombatActions = new byte[count];
        Guarding = new bool[count];
        ChallengesIssued = new int[count];
        Power = new CombatPowerBreakdown[count];
        Array.Fill(Opponents, -1);
        Array.Fill(LastOpponents, -1);
        for (var index = 0; index < count; index++)
        {
            Chronicles[index] = [];
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
    public RealmStage[] RealmStages { get; }
    public byte[] RealmLayers { get; }
    public int[] CultivationProgress { get; }
    public ushort[] TechniqueQualities { get; }
    public ushort[] TechniqueMasteries { get; }
    public ushort[] SpellQualities { get; }
    public ushort[] SpellMasteries { get; }
    public int[] CombatExperience { get; }
    public int[] Wins { get; }
    public int[] Losses { get; }
    public int[] Health { get; }
    public int[] MaximumHealth { get; }
    public int[] Qi { get; }
    public int[] MaximumQi { get; }
    public AgentActivity[] Activities { get; }
    public long[] NextDecisionTicks { get; }
    public int[] TargetRanks { get; }
    public int[] Opponents { get; }
    public int[] LastOpponents { get; }
    public long[] LastCombatTicks { get; }
    public long[] CombatStartedTicks { get; }
    public byte[] CombatActions { get; }
    public bool[] Guarding { get; }
    public int[] ChallengesIssued { get; }
    public CombatPowerBreakdown[] Power { get; }

    public void Initialize(int index, DeterministicRandom random)
    {
        Souls[index] = CreateAptitude(random);
        LifeNumbers[index] = 1;
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
            new QualityValue(SpellQualities[index]),
            SpellMasteries[index],
            CombatExperience[index]);
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
        TechniqueQualities[index] = CreateQuality(random);
        TechniqueMasteries[index] = (ushort)(50 + random.NextInt(251));
        SpellQualities[index] = CreateQuality(random);
        SpellMasteries[index] = (ushort)(40 + random.NextInt(221));
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
        RecalculatePower(index);
    }

    private static ushort CreateAptitude(DeterministicRandom random) => CreateQuality(random);

    private static ushort CreateQuality(DeterministicRandom random) =>
        checked((ushort)random.NextInt(QualityValue.MaximumHundredths + 1));

    private static int GetEffectiveWhole(ushort rawQuality) =>
        QualityRules.GetEffectiveWhole(new QualityValue(rawQuality));

    private static string CreateName(int index, DeterministicRandom random)
    {
        var family = FamilyNames[random.NextInt(FamilyNames.Length)];
        var first = GivenSyllables[random.NextInt(GivenSyllables.Length)];
        var second = GivenSyllables[random.NextInt(GivenSyllables.Length)];
        return $"{family}{first}{second}·{index + 1:00000}";
    }
}
