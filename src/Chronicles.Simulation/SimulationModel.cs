using Chronicles.Core;

namespace Chronicles.Simulation;

public enum RealmStage : byte
{
    QiRefining,
    FoundationEstablishment,
    GoldenCore,
    NascentSoul,
    SpiritTransformation,
    VoidRefinement,
    BodyIntegration,
    TribulationCrossing,
    Mahayana,
    HalfImmortal,
}

public enum MinorStage : byte
{
    Early = 1,
    Middle,
    Late,
    GreatCompletion,
}

public enum AgentActivity : byte
{
    Cultivating,
    TrainingTechnique,
    TrainingSpell,
    Recovering,
    SeekingChallenge,
    Fighting,
    Reincarnating,
}

public enum WorldEventKind : byte
{
    BreakthroughSucceeded,
    BreakthroughFailed,
    NewLeader,
    CombatStarted,
    CombatPhysicalAttack,
    CombatSpellCast,
    CombatGuarded,
    CombatRetreated,
    CombatEnded,
    MinorStageBreakthrough,
    BottleneckBreakthrough,
    MajorRealmBreakthrough,
    TechniqueEnlightenment,
    SpellEnlightenment,
    Death,
    ReincarnationEntered,
    Rebirth,
}

public readonly record struct Aptitudes(
    byte SpiritualRoot,
    byte Bone,
    byte Constitution,
    byte Comprehension,
    byte Mind,
    byte Soul)
{
    public int RankingScore =>
        SpiritualRoot + Bone + Constitution + Comprehension + Mind + Soul;
}

public enum ChronicleEventKind : byte
{
    Birth,
    MinorStageBreakthrough,
    BottleneckBreakthrough,
    MajorRealmBreakthrough,
    BreakthroughFailure,
    TechniqueEnlightenment,
    SpellEnlightenment,
    FortuitousEncounter,
    Death,
    ReincarnationEntered,
    Rebirth,
}

public enum RankingCategory : byte
{
    CombatPower,
    Realm,
    Aptitude,
}

public readonly record struct LifeChronicleEntry(
    long Sequence,
    SimulationTick Tick,
    int LifeNumber,
    long AgeYears,
    string PersonName,
    ChronicleEventKind Kind,
    RealmStage RealmStage,
    MinorStage MinorStage,
    int Value);

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
    int RealmRank,
    int AptitudeRank,
    int TargetRank,
    long AgeYears,
    long MaximumLifespanYears,
    int LifeNumber,
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
    AgentId? OpponentId,
    SimulationTick LastCombatTick,
    int ChallengesIssued,
    CombatPowerBreakdown CombatPower);

public sealed record RankingEntry(
    RankingCategory Category,
    int Rank,
    AgentId AgentId,
    string Name,
    RealmStage RealmStage,
    int RealmLayer,
    AgentActivity Activity,
    CombatPowerBreakdown CombatPower,
    int AptitudeScore,
    long Score);

public sealed record WorldSnapshot(
    SimulationTick Tick,
    int Population,
    IReadOnlyList<RankingEntry> CombatPowerRankings,
    IReadOnlyList<RankingEntry> RealmRankings,
    IReadOnlyList<RankingEntry> AptitudeRankings,
    IReadOnlyDictionary<int, string> AgentNames,
    IReadOnlyList<WorldEvent> RecentEvents,
    int TotalBreakthroughs,
    int TotalChallenges,
    int TotalCombatsStarted,
    int TotalCombatsCompleted,
    int ActiveCombats,
    int TotalDeaths,
    int TotalReincarnations,
    AgentId LeaderId)
{
    public IReadOnlyList<RankingEntry> Rankings => CombatPowerRankings;
}

public sealed record WorldObservation(
    WorldSnapshot World,
    AgentSnapshot SelectedAgent,
    IReadOnlyList<LifeChronicleEntry> SelectedChronicle);

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
        if (realmLayer is < 1 or > RealmRules.MinorStageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(realmLayer));
        }

        var stage = (int)realmStage + 1;
        var realm = checked((stage * stage * stage * 2_000) + (realmLayer * stage * stage * 250));
        var aptitude = checked(
            (aptitudes.SpiritualRoot * 15)
            + (aptitudes.Bone * 14)
            + (aptitudes.Constitution * 13)
            + (aptitudes.Comprehension * 14)
            + (aptitudes.Mind * 10)
            + (aptitudes.Soul * 8));
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
