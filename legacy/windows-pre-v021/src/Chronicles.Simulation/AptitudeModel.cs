using Chronicles.Core;

namespace Chronicles.Simulation;

public enum AptitudeDomain : byte
{
    SpiritualRoot,
    Bone,
    Constitution,
    Comprehension,
    Mind,
    Soul,
}

public readonly record struct AptitudeComponentDefinition(
    string StableId,
    string KoreanName);

public readonly record struct AptitudeGroupDefinition(
    AptitudeDomain Domain,
    string StableId,
    string KoreanName,
    IReadOnlyList<AptitudeComponentDefinition> Components);

public readonly record struct AptitudeComponentSnapshot(
    string StableId,
    string KoreanName,
    QualityValue InnateValue);

public enum AptitudeDerivedAbility : byte
{
    SpiritualRootCultivation,
    SpiritualRootCombat,
    BoneCultivation,
    BoneCombat,
    ConstitutionCultivation,
    ConstitutionCombat,
    ComprehensionLearning,
    ComprehensionCombat,
    MindCultivation,
    MindCombat,
    SoulGrowth,
    SoulCombat,
}

public readonly record struct AptitudeDerivedAbilityDefinition(
    AptitudeDerivedAbility Ability,
    AptitudeDomain Domain,
    string StableId,
    string KoreanName,
    byte FirstCore,
    byte SecondCore,
    byte FirstSupport,
    byte SecondSupport);

public readonly record struct AptitudeDerivedAbilitySnapshot(
    string StableId,
    string KoreanName,
    string FormulaText,
    QualityValue Value);

public readonly record struct AptitudeRoleScores(
    int TechniqueEffectHundredths,
    int SpellEffectHundredths,
    int CombatMasteryHundredths,
    int CombatUnderstandingHundredths,
    int CombatExperienceGainHundredths);

public sealed record AptitudeGroupSnapshot(
    AptitudeDomain Domain,
    string StableId,
    string KoreanName,
    QualityValue Summary,
    IReadOnlyList<AptitudeComponentSnapshot> Components,
    IReadOnlyList<AptitudeDerivedAbilitySnapshot> DerivedAbilities);

public sealed record AptitudeDetailSnapshot(
    string FormulaId,
    int FormulaRevision,
    bool RequiresReview,
    AptitudeRoleScores RoleScores,
    IReadOnlyList<AptitudeGroupSnapshot> Groups);

public static class AptitudeRules
{
    public const string SummaryFormulaId = "F-TRAIT-002";
    public const int FormulaRevision = 1;
    public const int ComponentCountPerDomain = 10;
    public const int DomainCount = 6;
    public const int TotalComponentCount = DomainCount * ComponentCountPerDomain;

    // Alpha 1.1.3은 공통 기반값+제한 편차 구조만 확정했다. 편차 폭은 PC 임시값이며
    // UI·Notion 검토 원장에 RequiresReview 상태로 함께 노출한다.
    private const int ProvisionalDeviationHundredths = 1_250;

    private static readonly AptitudeGroupDefinition[] Definitions =
    [
        CreateGroup(
            AptitudeDomain.SpiritualRoot,
            "APT-SPIRITUAL-ROOT",
            "영근",
            ("root.size", "영근 크기"),
            ("root.channel_width", "통로 폭"),
            ("root.structure_density", "구조 밀도"),
            ("root.purity", "순도"),
            ("root.completeness", "완전성"),
            ("root.durability", "내구성"),
            ("root.stability", "안정성"),
            ("root.sensitivity", "감응도"),
            ("root.absorption_efficiency", "흡수 효율"),
            ("root.assimilation_efficiency", "체화 효율")),
        CreateGroup(
            AptitudeDomain.Bone,
            "APT-BONE",
            "근골",
            ("bone.skeletal_strength", "골격 강도"),
            ("bone.skeletal_density", "골격 밀도"),
            ("bone.joint_stability", "관절 안정성"),
            ("bone.joint_mobility", "관절 가동성"),
            ("bone.muscle_output", "근육 출력"),
            ("bone.muscle_endurance", "근육 지구력"),
            ("bone.muscle_growth", "근육 성장성"),
            ("bone.tendon_strength", "건인대 강도"),
            ("bone.meridian_durability", "경맥 내구성"),
            ("bone.meridian_flexibility", "경맥 유연성")),
        CreateGroup(
            AptitudeDomain.Constitution,
            "APT-CONSTITUTION",
            "체질",
            ("constitution.completeness", "체질 완전성"),
            ("constitution.purity", "체질 순도"),
            ("constitution.stability", "체질 안정성"),
            ("constitution.strength", "체질 강도"),
            ("constitution.capacity", "체질 수용력"),
            ("constitution.growth", "체질 성장성"),
            ("constitution.awakening", "체질 각성성"),
            ("constitution.adaptation", "체질 적응성"),
            ("constitution.fusion", "체질 융합성"),
            ("constitution.transformation", "체질 변성성")),
        CreateGroup(
            AptitudeDomain.Comprehension,
            "APT-COMPREHENSION",
            "오성",
            ("comprehension.memory", "기억력"),
            ("comprehension.understanding", "이해력"),
            ("comprehension.analysis", "분석력"),
            ("comprehension.reasoning", "추론력"),
            ("comprehension.calculation", "연산력"),
            ("comprehension.observation", "관찰력"),
            ("comprehension.focus", "집중력"),
            ("comprehension.application", "응용력"),
            ("comprehension.creativity", "창의력"),
            ("comprehension.insight", "통찰력")),
        CreateGroup(
            AptitudeDomain.Mind,
            "APT-MIND",
            "정신",
            ("mind.willpower", "의지력"),
            ("mind.stability", "정신 안정성"),
            ("mind.endurance", "정신 지속력"),
            ("mind.recovery", "정신 회복력"),
            ("mind.cultivation_control", "수행 통제력"),
            ("mind.precision_control", "정밀 제어력"),
            ("mind.parallel_control", "병렬 제어력"),
            ("mind.divine_sense_manifestation", "신식 발현력"),
            ("mind.divine_sense_expansion", "신식 확장력"),
            ("mind.divine_sense_sensitivity", "신식 감응력")),
        CreateGroup(
            AptitudeDomain.Soul,
            "APT-SOUL",
            "영혼",
            ("soul.strength", "영혼 강도"),
            ("soul.stability", "영혼 안정성"),
            ("soul.purity", "영혼 순도"),
            ("soul.recovery", "영혼 회복력"),
            ("soul.capacity", "영혼 수용력"),
            ("soul.growth", "영혼 성장성"),
            ("soul.resonance", "영혼 공명력"),
            ("soul.aptitude_imprint", "자질 각인력"),
            ("soul.aptitude_harmony", "자질 조화력"),
            ("soul.aptitude_manifestation", "자질 발현력")),
    ];

    private static readonly AptitudeDerivedAbilityDefinition[] DerivedDefinitions =
    [
        new(AptitudeDerivedAbility.SpiritualRootCultivation, AptitudeDomain.SpiritualRoot,
            "derived.root.cultivation", "영기 수행 기반", 7, 8, 9, 1),
        new(AptitudeDerivedAbility.SpiritualRootCombat, AptitudeDomain.SpiritualRoot,
            "derived.root.combat", "영력 전투 운용", 1, 6, 5, 3),
        new(AptitudeDerivedAbility.BoneCultivation, AptitudeDomain.Bone,
            "derived.bone.cultivation", "육체 수행 효율", 6, 9, 1, 8),
        new(AptitudeDerivedAbility.BoneCombat, AptitudeDomain.Bone,
            "derived.bone.combat", "육체 전투 기반", 4, 0, 7, 3),
        new(AptitudeDerivedAbility.ConstitutionCultivation, AptitudeDomain.Constitution,
            "derived.constitution.cultivation", "체질 수행 수용", 5, 4, 2, 7),
        new(AptitudeDerivedAbility.ConstitutionCombat, AptitudeDomain.Constitution,
            "derived.constitution.combat", "체질 전투 내구", 3, 2, 0, 7),
        new(AptitudeDerivedAbility.ComprehensionLearning, AptitudeDomain.Comprehension,
            "derived.comprehension.learning", "학습·이해 능력", 1, 2, 0, 6),
        new(AptitudeDerivedAbility.ComprehensionCombat, AptitudeDomain.Comprehension,
            "derived.comprehension.combat", "전투 판단 능력", 2, 5, 7, 9),
        new(AptitudeDerivedAbility.MindCultivation, AptitudeDomain.Mind,
            "derived.mind.cultivation", "수행 통제 능력", 4, 5, 1, 2),
        new(AptitudeDerivedAbility.MindCombat, AptitudeDomain.Mind,
            "derived.mind.combat", "전투 제어 능력", 5, 6, 0, 9),
        new(AptitudeDerivedAbility.SoulGrowth, AptitudeDomain.Soul,
            "derived.soul.growth", "영혼 성장 기반", 5, 8, 7, 6),
        new(AptitudeDerivedAbility.SoulCombat, AptitudeDomain.Soul,
            "derived.soul.combat", "영혼 전투 기반", 0, 1, 6, 9),
    ];

    public static IReadOnlyList<AptitudeGroupDefinition> All => Definitions;

    public static IReadOnlyList<AptitudeDerivedAbilityDefinition> AllDerivedAbilities =>
        DerivedDefinitions;

    public static IReadOnlyList<ProvisionalBalanceDefinition> ProvisionalBalances { get; } =
    [
        new(
            "PC-TEMP-TRAIT-DIST-113",
            FormulaRevision,
            "두 균등 난수 평균 기반값 + Component별 ±12.50 제한 편차",
            "10,000명 분포와 극단값 빈도는 다음 버전에서 재검토",
            true),
        new(
            "PC-TEMP-TRAIT-ROLE-113",
            FormulaRevision,
            "공법·법술·전투 숙련·이해·경험 역할 가중치",
            "장기 밸런스 결과와 Notion Formula Registry 대조 후 다음 버전에서 재검토",
            true),
        new(
            "PC-TEMP-SPIRIT-ROOT-113",
            FormulaRevision,
            "영근 공통 Component 10개",
            "오행별 10개 분리 여부는 다음 버전 기획 검토안으로 이관",
            true),
    ];

    public static AptitudeGroupDefinition GetGroup(AptitudeDomain domain)
    {
        if (!Enum.IsDefined(domain))
        {
            throw new ArgumentOutOfRangeException(nameof(domain));
        }

        return Definitions[(int)domain];
    }

    public static int GetComponentOffset(AptitudeDomain domain)
    {
        if (!Enum.IsDefined(domain))
        {
            throw new ArgumentOutOfRangeException(nameof(domain));
        }

        return checked((int)domain * ComponentCountPerDomain);
    }

    public static string GetDerivedAbilityFormulaText(AptitudeDerivedAbility ability)
    {
        if (!Enum.IsDefined(ability))
        {
            throw new ArgumentOutOfRangeException(nameof(ability));
        }

        var definition = DerivedDefinitions[(int)ability];
        var components = GetGroup(definition.Domain).Components;
        return $"{components[definition.FirstCore].KoreanName} 30% + "
            + $"{components[definition.SecondCore].KoreanName} 30% + "
            + $"{components[definition.FirstSupport].KoreanName} 20% + "
            + $"{components[definition.SecondSupport].KoreanName} 20%";
    }

    public static int CalculateSummaryHundredths(
        ReadOnlySpan<ushort> allComponents,
        AptitudeDomain domain)
    {
        if (allComponents.Length != TotalComponentCount)
        {
            throw new ArgumentException("세부 자질 배열 길이가 잘못되었습니다.", nameof(allComponents));
        }

        var start = GetComponentOffset(domain);
        var total = 0;
        for (var component = 0; component < ComponentCountPerDomain; component++)
        {
            total += allComponents[start + component];
        }

        return total / ComponentCountPerDomain;
    }

    public static int CalculateDerivedAbilityHundredths(
        ReadOnlySpan<ushort> allComponents,
        AptitudeDerivedAbility ability)
    {
        if (allComponents.Length != TotalComponentCount)
        {
            throw new ArgumentException("세부 자질 배열 길이가 잘못되었습니다.", nameof(allComponents));
        }

        if (!Enum.IsDefined(ability))
        {
            throw new ArgumentOutOfRangeException(nameof(ability));
        }

        var definition = DerivedDefinitions[(int)ability];
        var start = GetComponentOffset(definition.Domain);
        return checked((int)(
            ((long)allComponents[start + definition.FirstCore] * 30)
            + ((long)allComponents[start + definition.SecondCore] * 30)
            + ((long)allComponents[start + definition.FirstSupport] * 20)
            + ((long)allComponents[start + definition.SecondSupport] * 20)) / 100);
    }

    public static AptitudeRoleScores CalculateRoleScores(ReadOnlySpan<ushort> allComponents)
    {
        var rootCultivation = CalculateDerivedAbilityHundredths(
            allComponents,
            AptitudeDerivedAbility.SpiritualRootCultivation);
        var boneCultivation = CalculateDerivedAbilityHundredths(
            allComponents,
            AptitudeDerivedAbility.BoneCultivation);
        var boneCombat = CalculateDerivedAbilityHundredths(
            allComponents,
            AptitudeDerivedAbility.BoneCombat);
        var constitutionCultivation = CalculateDerivedAbilityHundredths(
            allComponents,
            AptitudeDerivedAbility.ConstitutionCultivation);
        var constitutionCombat = CalculateDerivedAbilityHundredths(
            allComponents,
            AptitudeDerivedAbility.ConstitutionCombat);
        var comprehensionLearning = CalculateDerivedAbilityHundredths(
            allComponents,
            AptitudeDerivedAbility.ComprehensionLearning);
        var comprehensionCombat = CalculateDerivedAbilityHundredths(
            allComponents,
            AptitudeDerivedAbility.ComprehensionCombat);
        var mindCultivation = CalculateDerivedAbilityHundredths(
            allComponents,
            AptitudeDerivedAbility.MindCultivation);
        var mindCombat = CalculateDerivedAbilityHundredths(
            allComponents,
            AptitudeDerivedAbility.MindCombat);
        var soulGrowth = CalculateDerivedAbilityHundredths(
            allComponents,
            AptitudeDerivedAbility.SoulGrowth);
        var soulCombat = CalculateDerivedAbilityHundredths(
            allComponents,
            AptitudeDerivedAbility.SoulCombat);

        return new AptitudeRoleScores(
            WeightedAverage(
                boneCultivation, 35,
                constitutionCultivation, 35,
                mindCultivation, 15,
                soulGrowth, 15),
            WeightedAverage(
                rootCultivation, 40,
                comprehensionLearning, 35,
                mindCultivation, 15,
                soulGrowth, 10),
            WeightedAverage(
                boneCombat, 20,
                comprehensionCombat, 30,
                mindCombat, 30,
                soulCombat, 20),
            WeightedAverage(
                comprehensionCombat, 45,
                mindCombat, 35,
                soulCombat, 20,
                0, 0),
            WeightedAverage(
                comprehensionLearning, 25,
                mindCombat, 35,
                soulGrowth, 25,
                constitutionCombat, 15));
    }

    public static Aptitudes CalculateContextAptitudes(
        ReadOnlySpan<ushort> allComponents,
        bool combat) => new(
        QualityValue.FromHundredths(CalculateDerivedAbilityHundredths(
            allComponents,
            combat
                ? AptitudeDerivedAbility.SpiritualRootCombat
                : AptitudeDerivedAbility.SpiritualRootCultivation)),
        QualityValue.FromHundredths(CalculateDerivedAbilityHundredths(
            allComponents,
            combat ? AptitudeDerivedAbility.BoneCombat : AptitudeDerivedAbility.BoneCultivation)),
        QualityValue.FromHundredths(CalculateDerivedAbilityHundredths(
            allComponents,
            combat
                ? AptitudeDerivedAbility.ConstitutionCombat
                : AptitudeDerivedAbility.ConstitutionCultivation)),
        QualityValue.FromHundredths(CalculateDerivedAbilityHundredths(
            allComponents,
            combat
                ? AptitudeDerivedAbility.ComprehensionCombat
                : AptitudeDerivedAbility.ComprehensionLearning)),
        QualityValue.FromHundredths(CalculateDerivedAbilityHundredths(
            allComponents,
            combat ? AptitudeDerivedAbility.MindCombat : AptitudeDerivedAbility.MindCultivation)),
        QualityValue.FromHundredths(CalculateDerivedAbilityHundredths(
            allComponents,
            combat ? AptitudeDerivedAbility.SoulCombat : AptitudeDerivedAbility.SoulGrowth)));

    public static ushort[] CreateUniformComponents(Aptitudes aptitudes)
    {
        var components = new ushort[TotalComponentCount];
        FillGroup(components, AptitudeDomain.SpiritualRoot, aptitudes.SpiritualRoot.Hundredths);
        FillGroup(components, AptitudeDomain.Bone, aptitudes.Bone.Hundredths);
        FillGroup(components, AptitudeDomain.Constitution, aptitudes.Constitution.Hundredths);
        FillGroup(components, AptitudeDomain.Comprehension, aptitudes.Comprehension.Hundredths);
        FillGroup(components, AptitudeDomain.Mind, aptitudes.Mind.Hundredths);
        FillGroup(components, AptitudeDomain.Soul, aptitudes.Soul.Hundredths);
        return components;
    }

    internal static ushort GenerateGroup(
        Span<ushort> destination,
        DeterministicRandom random)
    {
        if (destination.Length != ComponentCountPerDomain)
        {
            throw new ArgumentException("세부 자질 그룹 길이가 잘못되었습니다.", nameof(destination));
        }

        // 두 균등 난수의 평균은 중간값이 흔하고 극단값이 드문 삼각 분포를 만든다.
        var baseValue = (
            random.NextInt(QualityValue.MaximumHundredths + 1)
            + random.NextInt(QualityValue.MaximumHundredths + 1)) / 2;
        var total = 0;
        for (var component = 0; component < destination.Length; component++)
        {
            var deviation = random.NextInt((ProvisionalDeviationHundredths * 2) + 1)
                - ProvisionalDeviationHundredths;
            var value = Math.Clamp(
                baseValue + deviation,
                0,
                QualityValue.MaximumHundredths);
            destination[component] = checked((ushort)value);
            total += value;
        }

        return checked((ushort)(total / destination.Length));
    }

    private static AptitudeGroupDefinition CreateGroup(
        AptitudeDomain domain,
        string stableId,
        string koreanName,
        params (string StableId, string KoreanName)[] components)
    {
        if (components.Length != ComponentCountPerDomain)
        {
            throw new InvalidOperationException($"{stableId} 세부 자질은 정확히 10개여야 합니다.");
        }

        return new AptitudeGroupDefinition(
            domain,
            stableId,
            koreanName,
            components
                .Select(item => new AptitudeComponentDefinition(item.StableId, item.KoreanName))
                .ToArray());
    }

    private static void FillGroup(
        Span<ushort> components,
        AptitudeDomain domain,
        int value)
    {
        var start = GetComponentOffset(domain);
        components.Slice(start, ComponentCountPerDomain).Fill(checked((ushort)value));
    }

    private static int WeightedAverage(
        int firstValue,
        int firstWeight,
        int secondValue,
        int secondWeight,
        int thirdValue,
        int thirdWeight,
        int fourthValue,
        int fourthWeight) => checked((int)(
            (((long)firstValue * firstWeight)
            + ((long)secondValue * secondWeight)
            + ((long)thirdValue * thirdWeight)
            + ((long)fourthValue * fourthWeight))
            / (firstWeight + secondWeight + thirdWeight + fourthWeight)));
}
