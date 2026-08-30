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
        SpiritualRoots = new byte[count];
        Bones = new byte[count];
        Constitutions = new byte[count];
        Comprehensions = new byte[count];
        RealmStages = new RealmStage[count];
        RealmLayers = new byte[count];
        CultivationProgress = new int[count];
        TechniqueGrades = new byte[count];
        TechniqueMasteries = new ushort[count];
        SpellGrades = new byte[count];
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
        Power = new CombatPowerBreakdown[count];
    }

    public int Count { get; }
    public string[] Names { get; }
    public byte[] SpiritualRoots { get; }
    public byte[] Bones { get; }
    public byte[] Constitutions { get; }
    public byte[] Comprehensions { get; }
    public RealmStage[] RealmStages { get; }
    public byte[] RealmLayers { get; }
    public int[] CultivationProgress { get; }
    public byte[] TechniqueGrades { get; }
    public ushort[] TechniqueMasteries { get; }
    public byte[] SpellGrades { get; }
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
    public CombatPowerBreakdown[] Power { get; }

    public void Initialize(int index, DeterministicRandom random)
    {
        Names[index] = CreateName(index, random);
        SpiritualRoots[index] = CreateAptitude(random);
        Bones[index] = CreateAptitude(random);
        Constitutions[index] = CreateAptitude(random);
        Comprehensions[index] = CreateAptitude(random);
        RealmStages[index] = RealmStage.QiRefining;
        RealmLayers[index] = 1;
        TechniqueGrades[index] = (byte)(1 + random.NextInt(3));
        TechniqueMasteries[index] = (ushort)(50 + random.NextInt(251));
        SpellGrades[index] = (byte)(1 + random.NextInt(3));
        SpellMasteries[index] = (ushort)(40 + random.NextInt(221));
        MaximumHealth[index] = 800 + (Constitutions[index] * 8) + (Bones[index] * 3);
        Health[index] = MaximumHealth[index];
        MaximumQi[index] = 500 + (SpiritualRoots[index] * 10) + (Comprehensions[index] * 2);
        Qi[index] = MaximumQi[index];
        Activities[index] = AgentActivity.Cultivating;
        NextDecisionTicks[index] = 1 + random.NextInt(20);
        TargetRanks[index] = 1;
        RecalculatePower(index);
    }

    public Aptitudes GetAptitudes(int index) => new(
        SpiritualRoots[index],
        Bones[index],
        Constitutions[index],
        Comprehensions[index]);

    public void RecalculatePower(int index)
    {
        Power[index] = CombatPowerFormula.Calculate(
            RealmStages[index],
            RealmLayers[index],
            GetAptitudes(index),
            TechniqueGrades[index],
            TechniqueMasteries[index],
            SpellGrades[index],
            SpellMasteries[index],
            CombatExperience[index]);
    }

    private static byte CreateAptitude(DeterministicRandom random) =>
        (byte)(15 + ((random.NextInt(86) + random.NextInt(86)) / 2));

    private static string CreateName(int index, DeterministicRandom random)
    {
        var family = FamilyNames[random.NextInt(FamilyNames.Length)];
        var first = GivenSyllables[random.NextInt(GivenSyllables.Length)];
        var second = GivenSyllables[random.NextInt(GivenSyllables.Length)];
        return $"{family}{first}{second}·{index + 1:00000}";
    }
}
