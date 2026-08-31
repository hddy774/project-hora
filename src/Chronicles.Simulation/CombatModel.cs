using Chronicles.Core;

namespace Chronicles.Simulation;

public enum InjurySeverity : byte
{
    None,
    Light,
    Moderate,
    Severe,
    Critical,
    Fatal,
}

public readonly record struct CombatAttributes(
    int Attack,
    int Defense,
    int MaximumHealth,
    int MaximumQi,
    int AttackSpeedBasisPoints,
    int DefenseSpeedBasisPoints,
    int Penetration,
    int CriticalPowerBasisPoints,
    int CriticalChanceBasisPoints,
    int PenetrationDefense,
    int CriticalDefenseBasisPoints,
    int EvasionBasisPoints,
    int Judgment,
    int Focus,
    int SpellEfficiencyBasisPoints,
    int CultivationEfficiencyBasisPoints);

public readonly record struct AptitudeCombatBonuses(
    int TechniqueEffectBasisPoints,
    int SpellEffectBasisPoints,
    int CombatMasteryEffectBasisPoints,
    int CombatUnderstandingEffectBasisPoints,
    int CombatExperienceGainBasisPoints);

public readonly record struct CombatProgressCheckpoint(
    ushort UnderstandingHundredths);

public readonly record struct CombatAuthorityCheckpoint(
    int ActionCount,
    int TurnOwner,
    ushort InjuryPoints);

internal readonly record struct PowerInputSignature(
    RealmStage Realm,
    byte Floor,
    ushort TechniqueQuality,
    ushort TechniqueMastery,
    ushort TechniqueUnderstanding,
    ushort SpellQuality,
    ushort SpellMastery,
    ushort SpellUnderstanding,
    int CombatExperience,
    ushort CombatUnderstanding);

public static class CombatAttributeFormula
{
    public const int CurrentRevision = 4;

    public static CombatAttributes Calculate(
        RealmStage realm,
        int floor,
        Aptitudes aptitudes,
        QualityValue techniqueQuality,
        int techniqueMastery,
        int techniqueUnderstanding,
        QualityValue spellQuality,
        int spellMastery,
        int spellUnderstanding,
        int weaponEffect,
        int armorEffect) => Calculate(
            realm,
            floor,
            aptitudes,
            techniqueQuality,
            techniqueMastery,
            techniqueUnderstanding,
            spellQuality,
            spellMastery,
            spellUnderstanding,
            combatMastery: 0,
            combatUnderstanding: 0,
            weaponEffect,
            armorEffect);

    public static CombatAttributes Calculate(
        RealmStage realm,
        int floor,
        Aptitudes aptitudes,
        QualityValue techniqueQuality,
        int techniqueMastery,
        int techniqueUnderstanding,
        QualityValue spellQuality,
        int spellMastery,
        int spellUnderstanding,
        int combatMastery,
        int combatUnderstanding,
        int weaponEffect,
        int armorEffect) => Calculate(
            realm,
            floor,
            aptitudes,
            AptitudeCombatBonusFormula.Calculate(aptitudes),
            techniqueQuality,
            techniqueMastery,
            techniqueUnderstanding,
            spellQuality,
            spellMastery,
            spellUnderstanding,
            combatMastery,
            combatUnderstanding,
            weaponEffect,
            armorEffect);

    public static CombatAttributes Calculate(
        RealmStage realm,
        int floor,
        Aptitudes aptitudes,
        AptitudeCombatBonuses aptitudeBonuses,
        QualityValue techniqueQuality,
        int techniqueMastery,
        int techniqueUnderstanding,
        QualityValue spellQuality,
        int spellMastery,
        int spellUnderstanding,
        int combatMastery,
        int combatUnderstanding,
        int weaponEffect,
        int armorEffect)
    {
        var techniqueFactor = 10_000 + techniqueMastery + techniqueUnderstanding;
        var spellFactor = 10_000 + spellMastery + spellUnderstanding;
        var techniquePower = AptitudeCombatBonusFormula.Apply(
            SkillCombatScaling.GetAttributeContribution(techniqueQuality, techniqueFactor),
            aptitudeBonuses.TechniqueEffectBasisPoints);
        var spellPower = AptitudeCombatBonusFormula.Apply(
            SkillCombatScaling.GetAttributeContribution(spellQuality, spellFactor),
            aptitudeBonuses.SpellEffectBasisPoints);
        var stage = (int)realm + 1;
        var realmScale = checked((stage * stage * stage * 250) + (floor * stage * 30));
        var root = aptitudes.SpiritualRoot.Hundredths;
        var bone = aptitudes.Bone.Hundredths;
        var constitution = aptitudes.Constitution.Hundredths;
        var comprehension = aptitudes.Comprehension.Hundredths;
        var mind = aptitudes.Mind.Hundredths;
        var soul = aptitudes.Soul.Hundredths;
        var effectiveCombatMastery = AptitudeCombatBonusFormula.Apply(
            combatMastery,
            aptitudeBonuses.CombatMasteryEffectBasisPoints);
        var effectiveCombatUnderstanding = AptitudeCombatBonusFormula.Apply(
            combatUnderstanding,
            aptitudeBonuses.CombatUnderstandingEffectBasisPoints);

        var judgment = 500
            + (comprehension / 5)
            + (mind / 10)
            + (spellUnderstanding / 4)
            + (effectiveCombatUnderstanding / 3);
        var focus = 500
            + (comprehension / 6)
            + (mind / 5)
            + (techniqueUnderstanding / 8)
            + (effectiveCombatMastery / 3);
        var spellEfficiency = Math.Clamp(
            10_000 + ((root - 5_000) / 4) + ((comprehension - 5_000) / 3) + (spellMastery / 5),
            5_000,
            20_000);
        var cultivationEfficiency = Math.Clamp(
            10_000
                + ((root - 5_000) / 5)
                + ((comprehension - 5_000) / 3)
                + ((mind - 5_000) / 10),
            5_000,
            18_000);

        return new CombatAttributes(
            Attack: ClampToInt(50L + realmScale + spellPower + (root / 10) + (effectiveCombatMastery / 20) + (effectiveCombatUnderstanding / 25) + weaponEffect),
            Defense: ClampToInt(40L + realmScale + (spellPower / 2) + (techniquePower / 2) + (constitution / 12) + (effectiveCombatMastery / 25) + (effectiveCombatUnderstanding / 30) + armorEffect),
            MaximumHealth: Math.Max(1, ClampToInt(800L + (bone / 4) + (constitution / 3) + (techniquePower * 3L) + (realmScale * 4L))),
            MaximumQi: Math.Max(1, ClampToInt(500L + (root / 3) + (comprehension / 8) + (techniquePower * 2L) + (realmScale * 3L))),
            AttackSpeedBasisPoints: Math.Clamp(8_000 + (comprehension / 2) + (spellMastery / 3) + (judgment / 2), 2_500, 30_000),
            DefenseSpeedBasisPoints: Math.Clamp(8_000 + (mind / 2) + (techniqueMastery / 3) + (focus / 2), 2_500, 30_000),
            Penetration: 100 + (spellPower / 8) + (root / 20) + (spellUnderstanding / 5),
            CriticalPowerBasisPoints: Math.Clamp(15_000 + (spellUnderstanding / 2) + (soul / 4), 15_000, 30_000),
            CriticalChanceBasisPoints: Math.Clamp(300 + (judgment / 5) + (spellMastery / 10), 300, 5_000),
            PenetrationDefense: 100 + (techniquePower / 8) + (constitution / 20) + (techniqueUnderstanding / 5),
            CriticalDefenseBasisPoints: Math.Clamp(200 + (focus / 5) + (soul / 5), 200, 5_000),
            EvasionBasisPoints: Math.Clamp(200 + (judgment / 6) + (mind / 8), 200, 4_000),
            Judgment: judgment,
            Focus: focus,
            SpellEfficiencyBasisPoints: spellEfficiency,
            CultivationEfficiencyBasisPoints: cultivationEfficiency);
    }

    private static int ClampToInt(long value) => checked((int)Math.Clamp(value, 0, int.MaxValue));
}

public static class AptitudeCombatBonusFormula
{
    public const int CurrentRevision = 2;
    public const int MinimumEffectBasisPoints = 5_000;
    public const int NeutralEffectBasisPoints = 10_000;
    public const int MaximumEffectBasisPoints = 20_000;

    public static AptitudeCombatBonuses Calculate(Aptitudes aptitudes) => new(
        GetTechniqueEffectBasisPoints(aptitudes),
        GetSpellEffectBasisPoints(aptitudes),
        GetCombatMasteryEffectBasisPoints(aptitudes),
        GetCombatUnderstandingEffectBasisPoints(aptitudes),
        GetCombatExperienceGainBasisPoints(aptitudes));

    // 자질 품질의 큰 경계 배율은 직접 전투력에 사용하지 않는다. 원시 0.00~100.00을
    // 역할별로 가중하고 50.00을 100% 기준으로 삼아 50%~200% 안에서 보정한다.
    public static int GetTechniqueEffectBasisPoints(Aptitudes aptitudes) =>
        ToEffectBasisPoints(WeightedAverage(
            aptitudes.Bone.Hundredths, 35,
            aptitudes.Constitution.Hundredths, 35,
            aptitudes.Mind.Hundredths, 15,
            aptitudes.Soul.Hundredths, 15));

    public static int GetSpellEffectBasisPoints(Aptitudes aptitudes) =>
        ToEffectBasisPoints(WeightedAverage(
            aptitudes.SpiritualRoot.Hundredths, 40,
            aptitudes.Comprehension.Hundredths, 35,
            aptitudes.Mind.Hundredths, 15,
            aptitudes.Soul.Hundredths, 10));

    public static int GetCombatMasteryEffectBasisPoints(Aptitudes aptitudes) =>
        ToEffectBasisPoints(WeightedAverage(
            aptitudes.Bone.Hundredths, 20,
            aptitudes.Comprehension.Hundredths, 30,
            aptitudes.Mind.Hundredths, 30,
            aptitudes.Soul.Hundredths, 20));

    public static int GetCombatUnderstandingEffectBasisPoints(Aptitudes aptitudes) =>
        ToEffectBasisPoints(WeightedAverage(
            aptitudes.Comprehension.Hundredths, 45,
            aptitudes.Mind.Hundredths, 35,
            aptitudes.Soul.Hundredths, 20,
            aptitudes.SpiritualRoot.Hundredths, 0));

    public static int GetCombatExperienceGainBasisPoints(Aptitudes aptitudes) =>
        ToEffectBasisPoints(WeightedAverage(
            aptitudes.Comprehension.Hundredths, 25,
            aptitudes.Mind.Hundredths, 35,
            aptitudes.Soul.Hundredths, 25,
            aptitudes.Constitution.Hundredths, 15));

    public static long Apply(long value, int effectBasisPoints)
    {
        ValidateEffectBasisPoints(effectBasisPoints);
        return checked(value * effectBasisPoints / NeutralEffectBasisPoints);
    }

    public static int Apply(int value, int effectBasisPoints)
    {
        ValidateEffectBasisPoints(effectBasisPoints);
        return checked((int)((long)value * effectBasisPoints / NeutralEffectBasisPoints));
    }

    private static int WeightedAverage(
        int firstValue,
        int firstWeight,
        int secondValue,
        int secondWeight,
        int thirdValue,
        int thirdWeight,
        int fourthValue,
        int fourthWeight) =>
        checked((int)(
            (((long)firstValue * firstWeight)
            + ((long)secondValue * secondWeight)
            + ((long)thirdValue * thirdWeight)
            + ((long)fourthValue * fourthWeight))
            / (firstWeight + secondWeight + thirdWeight + fourthWeight)));

    private static int ToEffectBasisPoints(int aptitudeCompositeHundredths)
    {
        var effect = aptitudeCompositeHundredths <= 5_000
            ? MinimumEffectBasisPoints + aptitudeCompositeHundredths
            : NeutralEffectBasisPoints + checked((aptitudeCompositeHundredths - 5_000) * 2);
        return Math.Clamp(effect, MinimumEffectBasisPoints, MaximumEffectBasisPoints);
    }

    private static void ValidateEffectBasisPoints(int value)
    {
        if (value is < MinimumEffectBasisPoints or > MaximumEffectBasisPoints)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}

public static class CombatProgressionRules
{
    public const int CurrentRevision = 1;
    public const int MaximumHundredths = 10_000;

    public static ushort GetMasteryHundredths(int experience)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(experience);
        return checked((ushort)Math.Min(
            MaximumHundredths,
            IntegerSquareRoot(checked((long)experience * 100L))));
    }

    public static int ApplyExperienceGain(int current, int baseGain, int gainBasisPoints)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(current);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseGain);
        var gain = Math.Max(1L, AptitudeCombatBonusFormula.Apply((long)baseGain, gainBasisPoints));
        return checked((int)Math.Min(int.MaxValue, (long)current + gain));
    }

    public static ushort ApplyUnderstandingGain(
        ushort current,
        int baseGain,
        int effectBasisPoints)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseGain);
        var gain = Math.Max(1L, AptitudeCombatBonusFormula.Apply((long)baseGain, effectBasisPoints));
        return checked((ushort)Math.Min(MaximumHundredths, current + gain));
    }

    public static long GetDisplayContribution(
        int experience,
        int mastery,
        int understanding,
        AptitudeCombatBonuses bonuses)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(experience);
        var experienceContribution = Math.Min(20_000L, IntegerSquareRoot(experience) * 40L);
        var masteryContribution = AptitudeCombatBonusFormula.Apply(
            checked((long)mastery * 2L),
            bonuses.CombatMasteryEffectBasisPoints);
        var understandingContribution = AptitudeCombatBonusFormula.Apply(
            checked((long)understanding * 2L),
            bonuses.CombatUnderstandingEffectBasisPoints);
        return Math.Min(60_000L, checked(experienceContribution + masteryContribution + understandingContribution));
    }

    private static long IntegerSquareRoot(long value)
    {
        if (value <= 0)
        {
            return 0;
        }

        var result = (long)Math.Sqrt(value);
        while (checked((result + 1) * (result + 1)) <= value)
        {
            result++;
        }

        while (checked(result * result) > value)
        {
            result--;
        }

        return result;
    }
}

public static class SkillCombatScaling
{
    public const int CurrentRevision = 2;
    private const long DisplayDivisor = 10_000_000L;
    private const long AttributeDivisor = 100_000_000L;
    private const long TreasureDisplayDivisor = 10_000L;
    private const long TreasureAttributeDivisor = 10_000L;

    public static long GetDisplayContribution(QualityValue quality, long progressionFactor) =>
        checked(QualityRules.GetEffectiveHundredths(quality) * progressionFactor / DisplayDivisor);

    public static int GetAttributeContribution(QualityValue quality, int progressionFactor) =>
        checked((int)Math.Min(
            int.MaxValue,
            QualityRules.GetEffectiveHundredths(quality) * progressionFactor / AttributeDivisor));

    public static long GetTreasureDisplayContribution(long effectiveHundredths) =>
        checked(effectiveHundredths * 250L / 100L / TreasureDisplayDivisor);

    public static int GetTreasureAttributeContribution(long effectiveHundredths) =>
        checked((int)Math.Min(int.MaxValue, effectiveHundredths / TreasureAttributeDivisor));
}

public static class CombatInjuryRules
{
    public const int CurrentRevision = 2;
    public const int MaximumInjuryPoints = 10_000;
    public const int KnockoutInjuryPoints = 9_000;

    public static InjurySeverity GetSeverity(int injuryPoints) => injuryPoints switch
    {
        <= 0 => InjurySeverity.None,
        < 2_500 => InjurySeverity.Light,
        < 5_000 => InjurySeverity.Moderate,
        < 7_500 => InjurySeverity.Severe,
        < 10_000 => InjurySeverity.Critical,
        _ => InjurySeverity.Fatal,
    };

    public static int GetFatalChanceBasisPoints(InjurySeverity severity) => severity switch
    {
        InjurySeverity.Severe => 25,
        InjurySeverity.Critical => 250,
        InjurySeverity.Fatal => 10_000,
        _ => 0,
    };
}
