namespace Chronicles.Simulation;

public readonly record struct SkillProgressCheckpoint(
    long TechniqueExperience,
    ushort TechniqueUnderstanding,
    long SpellExperience,
    ushort SpellUnderstanding);

public static class SkillProgressionRules
{
    public const int CurrentRevision = 3;
    public const int MaximumHundredths = 10_000;
    public const long MaximumExperience = 100_000_000;

    public static int GetTechniqueExperienceGain(int comprehension, int bone)
    {
        ValidateAptitude(comprehension, nameof(comprehension));
        ValidateAptitude(bone, nameof(bone));
        return 500 + ((comprehension + bone) / 20);
    }

    public static int GetSpellExperienceGain(int comprehension, int spiritualRoot)
    {
        ValidateAptitude(comprehension, nameof(comprehension));
        ValidateAptitude(spiritualRoot, nameof(spiritualRoot));
        return 500 + ((comprehension + spiritualRoot) / 20);
    }

    public static int ApplyExperienceBonus(int baseGain, int effectBasisPoints)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseGain);
        return checked((int)Math.Max(
            1L,
            AptitudeCombatBonusFormula.Apply((long)baseGain, effectBasisPoints)));
    }

    public static ushort GetMasteryHundredths(long experience)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(experience);

        return checked((ushort)Math.Min(MaximumHundredths, IntegerSquareRoot(experience)));
    }

    public static long GetRequiredExperience(ushort masteryHundredths) =>
        checked((long)masteryHundredths * masteryHundredths);

    public static int GetEnlightenmentChanceBasisPoints(
        int comprehension,
        ushort understandingHundredths)
    {
        ValidateAptitude(comprehension, nameof(comprehension));
        return Math.Clamp(100 + (comprehension / 20) - (understandingHundredths / 40), 25, 750);
    }

    public static ushort ApplyUnderstandingGain(
        ushort current,
        int comprehension,
        int effectBasisPoints = AptitudeCombatBonusFormula.NeutralEffectBasisPoints)
    {
        ValidateAptitude(comprehension, nameof(comprehension));
        var baseGain = 50 + (comprehension / 100);
        var gain = Math.Max(
            1L,
            AptitudeCombatBonusFormula.Apply((long)baseGain, effectBasisPoints));
        return checked((ushort)Math.Min(MaximumHundredths, current + gain));
    }

    private static long IntegerSquareRoot(long value)
    {
        if (value <= 0)
        {
            return 0;
        }

        var result = (long)Math.Sqrt(value);
        while (result < 3_037_000_499L && (result + 1) * (result + 1) <= value)
        {
            result++;
        }

        while (result * result > value)
        {
            result--;
        }

        return result;
    }

    private static void ValidateAptitude(int value, string parameterName)
    {
        if (value is < 0 or > MaximumHundredths)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
