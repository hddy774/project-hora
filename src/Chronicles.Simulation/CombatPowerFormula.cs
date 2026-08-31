using Chronicles.Core;

namespace Chronicles.Simulation;

public static class CombatPowerFormula
{
    public const int CurrentRevision = 4;

    public static CombatPowerBreakdown Calculate(
        RealmStage realmStage,
        int realmLayer,
        Aptitudes aptitudes,
        QualityValue techniqueQuality,
        int techniqueMastery,
        int techniqueUnderstanding,
        QualityValue spellQuality,
        int spellMastery,
        int spellUnderstanding,
        int combatExperience,
        long treasurePower) => Calculate(
            realmStage,
            realmLayer,
            aptitudes,
            techniqueQuality,
            techniqueMastery,
            techniqueUnderstanding,
            spellQuality,
            spellMastery,
            spellUnderstanding,
            combatExperience,
            combatUnderstanding: 0,
            treasurePower);

    public static CombatPowerBreakdown Calculate(
        RealmStage realmStage,
        int realmLayer,
        Aptitudes aptitudes,
        QualityValue techniqueQuality,
        int techniqueMastery,
        int techniqueUnderstanding,
        QualityValue spellQuality,
        int spellMastery,
        int spellUnderstanding,
        int combatExperience,
        int combatUnderstanding,
        long treasurePower) => Calculate(
            realmStage,
            realmLayer,
            aptitudes,
            AptitudeCombatBonusFormula.Calculate(aptitudes),
            techniqueQuality,
            techniqueMastery,
            techniqueUnderstanding,
            spellQuality,
            spellMastery,
            spellUnderstanding,
            combatExperience,
            combatUnderstanding,
            treasurePower);

    public static CombatPowerBreakdown Calculate(
        RealmStage realmStage,
        int realmLayer,
        Aptitudes aptitudes,
        AptitudeCombatBonuses aptitudeBonuses,
        QualityValue techniqueQuality,
        int techniqueMastery,
        int techniqueUnderstanding,
        QualityValue spellQuality,
        int spellMastery,
        int spellUnderstanding,
        int combatExperience,
        int combatUnderstanding,
        long treasurePower)
    {
        if (realmLayer is < 1 or > RealmRules.MinorStageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(realmLayer));
        }

        var stage = (int)realmStage + 1;
        var realm = checked(
            100L
            + (((long)stage * stage * stage - 1L) * 2_000L)
            + (((long)realmLayer - 1L) * stage * stage * 250L));
        const long aptitude = 0L;
        var technique = AptitudeCombatBonusFormula.Apply(
            SkillCombatScaling.GetDisplayContribution(
                techniqueQuality,
                10_000L + (techniqueMastery * 2L) + (techniqueUnderstanding * 3L)),
            aptitudeBonuses.TechniqueEffectBasisPoints);
        var spell = AptitudeCombatBonusFormula.Apply(
            SkillCombatScaling.GetDisplayContribution(
                spellQuality,
                (10_000L + (spellMastery * 2L) + (spellUnderstanding * 3L)) * 5L / 6L),
            aptitudeBonuses.SpellEffectBasisPoints);
        var combatMastery = CombatProgressionRules.GetMasteryHundredths(combatExperience);
        var experience = CombatProgressionRules.GetDisplayContribution(
            combatExperience,
            combatMastery,
            combatUnderstanding,
            aptitudeBonuses);
        return new CombatPowerBreakdown(realm, aptitude, technique, spell, experience, treasurePower);
    }
}
