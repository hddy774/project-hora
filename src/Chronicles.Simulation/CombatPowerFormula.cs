using Chronicles.Core;

namespace Chronicles.Simulation;

public static class CombatPowerFormula
{
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
        var realm = checked((stage * stage * stage * 2_000) + (realmLayer * stage * stage * 250));
        var aptitude = checked(
            ((QualityRules.GetEffectiveHundredths(aptitudes.SpiritualRoot) * 15)
            + (QualityRules.GetEffectiveHundredths(aptitudes.Bone) * 14)
            + (QualityRules.GetEffectiveHundredths(aptitudes.Constitution) * 13)
            + (QualityRules.GetEffectiveHundredths(aptitudes.Comprehension) * 14)
            + (QualityRules.GetEffectiveHundredths(aptitudes.Mind) * 10)
            + (QualityRules.GetEffectiveHundredths(aptitudes.Soul) * 8)) / 100);
        var techniqueEffect = QualityRules.GetEffectiveHundredths(techniqueQuality);
        var technique = checked(
            techniqueEffect * (10_000L + (techniqueMastery * 2L) + (techniqueUnderstanding * 3L)) / 1_000);
        var spellEffect = QualityRules.GetEffectiveHundredths(spellQuality);
        var spell = checked(
            spellEffect * (10_000L + (spellMastery * 2L) + (spellUnderstanding * 3L)) / 1_200);
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
