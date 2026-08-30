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

public readonly record struct CombatAuthorityCheckpoint(
    int ActionCount,
    int TurnOwner,
    ushort InjuryPoints);

public static class CombatAttributeFormula
{
    public const int CurrentRevision = 1;

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
        int armorEffect)
    {
        var techniqueEffect = checked((int)Math.Min(1_000_000L, QualityRules.GetEffectiveWhole(techniqueQuality)));
        var spellEffect = checked((int)Math.Min(1_000_000L, QualityRules.GetEffectiveWhole(spellQuality)));
        var techniqueFactor = 10_000 + techniqueMastery + techniqueUnderstanding;
        var spellFactor = 10_000 + spellMastery + spellUnderstanding;
        var techniquePower = checked((int)Math.Min(int.MaxValue, (long)techniqueEffect * techniqueFactor / 10_000));
        var spellPower = checked((int)Math.Min(int.MaxValue, (long)spellEffect * spellFactor / 10_000));
        var stage = (int)realm + 1;
        var realmScale = checked((stage * stage * stage * 250) + (floor * stage * 30));
        var root = aptitudes.SpiritualRoot.Hundredths;
        var bone = aptitudes.Bone.Hundredths;
        var constitution = aptitudes.Constitution.Hundredths;
        var comprehension = aptitudes.Comprehension.Hundredths;
        var mind = aptitudes.Mind.Hundredths;
        var soul = aptitudes.Soul.Hundredths;

        var judgment = 500 + (comprehension / 5) + (mind / 10) + (spellUnderstanding / 4);
        var focus = 500 + (comprehension / 6) + (mind / 5) + (techniqueUnderstanding / 8);
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
            Attack: ClampToInt(50L + realmScale + spellPower + (root / 10) + weaponEffect),
            Defense: ClampToInt(40L + realmScale + (spellPower / 2) + (techniquePower / 2) + (constitution / 12) + armorEffect),
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

public static class CombatInjuryRules
{
    public const int CurrentRevision = 1;
    public const int MaximumInjuryPoints = 10_000;

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
        InjurySeverity.Severe => 200,
        InjurySeverity.Critical => 1_500,
        InjurySeverity.Fatal => 10_000,
        _ => 0,
    };
}
