namespace Chronicles.Simulation;

public readonly record struct SkillProgressCheckpoint(
    long TechniqueExperience,
    ushort TechniqueUnderstanding,
    long SpellExperience,
    ushort SpellUnderstanding);

public static class SkillProgressionRules
{
    public const int CurrentRevision = 1;
    public const int MaximumHundredths = 10_000;
    public const long MaximumExperience = 100_000_000;

    public static int GetTechniqueExperienceGain(ushort comprehension, ushort bone) =>
        500 + ((comprehension + bone) / 20);

    public static int GetSpellExperienceGain(ushort comprehension, ushort spiritualRoot) =>
        500 + ((comprehension + spiritualRoot) / 20);

    public static ushort GetMasteryHundredths(long experience)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(experience);

        return checked((ushort)Math.Min(MaximumHundredths, IntegerSquareRoot(experience)));
    }

    public static long GetRequiredExperience(ushort masteryHundredths) =>
        checked((long)masteryHundredths * masteryHundredths);

    public static int GetEnlightenmentChanceBasisPoints(
        ushort comprehension,
        ushort understandingHundredths) =>
        Math.Clamp(100 + (comprehension / 20) - (understandingHundredths / 40), 25, 750);

    public static ushort ApplyUnderstandingGain(ushort current, ushort comprehension)
    {
        var gain = 50 + (comprehension / 100);
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
}
