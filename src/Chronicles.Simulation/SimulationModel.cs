using Chronicles.Core;

namespace Chronicles.Simulation;

public enum RealmStage : byte
{
    QiRefining,
    FoundationEstablishment,
    CoreFormation,
    NascentSoul,
    SpiritTransformation,
}

public enum AgentActivity : byte
{
    Cultivating,
    TrainingTechnique,
    TrainingSpell,
    Recovering,
    SeekingChallenge,
    Fighting,
}

public enum WorldEventKind : byte
{
    BreakthroughSucceeded,
    BreakthroughFailed,
    NewLeader,
}

public readonly record struct Aptitudes(
    byte SpiritualRoot,
    byte Bone,
    byte Constitution,
    byte Comprehension);

public readonly record struct CombatPowerBreakdown(
    int Realm,
    int Aptitude,
    int Technique,
    int Spell,
    int Experience)
{
    public int Total => checked(Realm + Aptitude + Technique + Spell + Experience);
}

public readonly record struct WorldEvent(
    long Sequence,
    SimulationTick Tick,
    WorldEventKind Kind,
    AgentId PrimaryAgent,
    AgentId? SecondaryAgent,
    int Value);

public sealed record AgentSnapshot(
    AgentId Id,
    string Name,
    int Rank,
    int TargetRank,
    RealmStage RealmStage,
    int RealmLayer,
    int CultivationProgress,
    int CultivationRequirement,
    Aptitudes Aptitudes,
    int TechniqueGrade,
    int TechniqueMastery,
    int SpellGrade,
    int SpellMastery,
    int CombatExperience,
    int Wins,
    int Losses,
    int Health,
    int MaximumHealth,
    int Qi,
    int MaximumQi,
    AgentActivity Activity,
    SimulationTick NextDecisionTick,
    CombatPowerBreakdown CombatPower);

public sealed record RankingEntry(
    int Rank,
    AgentId AgentId,
    string Name,
    RealmStage RealmStage,
    int RealmLayer,
    AgentActivity Activity,
    CombatPowerBreakdown CombatPower);

public sealed record WorldSnapshot(
    SimulationTick Tick,
    int Population,
    IReadOnlyList<RankingEntry> Rankings,
    IReadOnlyList<WorldEvent> RecentEvents,
    int TotalBreakthroughs,
    AgentId LeaderId);

public static class CombatPowerFormula
{
    public static CombatPowerBreakdown Calculate(
        RealmStage realmStage,
        int realmLayer,
        Aptitudes aptitudes,
        int techniqueGrade,
        int techniqueMastery,
        int spellGrade,
        int spellMastery,
        int combatExperience)
    {
        if (realmLayer is < 1 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(realmLayer));
        }

        var stage = (int)realmStage + 1;
        var realm = checked((stage * stage * stage * 2_000) + (realmLayer * stage * stage * 250));
        var aptitude = checked(
            (aptitudes.SpiritualRoot * 15)
            + (aptitudes.Bone * 14)
            + (aptitudes.Constitution * 13)
            + (aptitudes.Comprehension * 14));
        var technique = checked((techniqueGrade * 750) + (techniqueMastery * techniqueGrade * 3));
        var spell = checked((spellGrade * 600) + (spellMastery * spellGrade * 2));
        var experience = Math.Min(40_000, IntegerSquareRoot(Math.Max(0, combatExperience)) * 80);
        return new CombatPowerBreakdown(realm, aptitude, technique, spell, experience);
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
