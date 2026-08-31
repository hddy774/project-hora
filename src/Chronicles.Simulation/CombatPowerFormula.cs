using Chronicles.Core;

namespace Chronicles.Simulation;

public static class CombatPowerFormula
{
    public const int CurrentRevision = 3;

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
        var aptitudeBonuses = AptitudeCombatBonusFormula.Calculate(aptitudes);
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
        var experience = Math.Min(40_000L, IntegerSquareRoot(Math.Max(0, combatExperience)) * 80L);
        return new CombatPowerBreakdown(realm, aptitude, technique, spell, experience, treasurePower);
    }

    private static int IntegerSquareRoot(int value)
    {
        if (value <= 0)
        {
            return 0;
        }

        var result = (int)Math.Sqrt(value);
        while ((long)(result + 1) * (result + 1) <= value)
        {
            result++;
        }

        while ((long)result * result > value)
        {
            result--;
        }

        return result;
    }
}
