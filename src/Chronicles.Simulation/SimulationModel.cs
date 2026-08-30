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

public enum CultivationState : byte
{
    Progressing,
    PreparingBreakthrough,
    PreflightReady,
    AttemptingBreakthrough,
    Bottleneck,
    Stabilizing,
    Completed,
}

public enum CultivationBottleneckCause : byte
{
    None,
    Knowledge,
    Body,
    SpiritualRoot,
    Resources,
    Stability,
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
    DoingCityMission,
    TradingInCity,
    CraftingTreasure,
    CraftingPill,
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
    MissionCompleted,
    TreasureCrafted,
    PillCrafted,
    PillPurchased,
    MaterialSold,
    MaterialPurchased,
    TreasurePurchased,
    TechniqueBookPurchased,
    SpellBookPurchased,
}

public readonly record struct Aptitudes(
    QualityValue SpiritualRoot,
    QualityValue Bone,
    QualityValue Constitution,
    QualityValue Comprehension,
    QualityValue Mind,
    QualityValue Soul)
{
    public int RankingScore =>
        SpiritualRoot.Hundredths
        + Bone.Hundredths
        + Constitution.Hundredths
        + Comprehension.Hundredths
        + Mind.Hundredths
        + Soul.Hundredths;
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

public enum PersonalLogKind : byte
{
    ActionCompleted,
    WorldEvent,
    Chronicle,
}

public readonly record struct PersonalLogEntry(
    long Sequence,
    SimulationTick Tick,
    int LifeNumber,
    PersonalLogKind Kind,
    AgentActivity? Activity,
    WorldEventKind? WorldEventKind,
    ChronicleEventKind? ChronicleEventKind,
    bool IsPrimaryActor,
    AgentId? RelatedAgent,
    long Value);

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
    long Realm,
    long Aptitude,
    long Technique,
    long Spell,
    long Experience,
    long Treasure)
{
    public long Total => checked(Realm + Aptitude + Technique + Spell + Experience + Treasure);
}

public readonly record struct WorldEvent(
    long Sequence,
    SimulationTick Tick,
    WorldEventKind Kind,
    AgentId PrimaryAgent,
    AgentId? SecondaryAgent,
    long Value);

public readonly record struct ActiveMissionSnapshot(
    MaterialId Material,
    int RequiredCount,
    int HeldCount);

public readonly record struct EquippedTreasureSnapshot(
    TreasureSlot Slot,
    QualityValue? Quality);

public readonly record struct RealmDistributionEntry(
    RealmStage Realm,
    int Count);

public readonly record struct CombatPowerDistributionEntry(
    long MinimumInclusive,
    long? MaximumExclusive,
    int Count);

public readonly record struct RankingMovementStatistics(
    SimulationTick FromTick,
    SimulationTick ToTick,
    int RisenCount,
    int FallenCount,
    int UnchangedCount,
    long TotalAbsoluteMovement)
{
    public int ChangedCount => checked(RisenCount + FallenCount);
}

public sealed record WorldStatistics(
    IReadOnlyList<RealmDistributionEntry> RealmDistribution,
    IReadOnlyList<CombatPowerDistributionEntry> CombatPowerDistribution,
    RankingMovementStatistics RankingMovement,
    int TotalDeaths,
    long TotalWins,
    long TotalLosses,
    int TotalReincarnations,
    int TotalBreakthroughs,
    int TotalMissionsCompleted,
    int TotalTreasuresCrafted,
    int TotalPillsCrafted,
    int TotalMaterialsSold,
    long TotalSpiritStones);

public readonly record struct MarketGoodSnapshot(
    int GoodId,
    MarketGoodKind Kind,
    MaterialId? Material,
    int CityBasePrice,
    int CurrentPrice,
    int Stock,
    long Supply,
    long Demand);

public sealed record CityMarketSnapshot(
    long TreasurySpiritStones,
    IReadOnlyList<MarketGoodSnapshot> Goods);

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
    CultivationState CultivationState,
    CultivationBottleneckCause BottleneckCause,
    Aptitudes Aptitudes,
    QualityValue TechniqueQuality,
    int TechniqueMastery,
    QualityValue SpellQuality,
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
    int ActionsTaken,
    GameDate? LastActionDate,
    long SpiritStones,
    int GrowthPills,
    int BreakthroughPills,
    int MissionsCompleted,
    ActiveMissionSnapshot? ActiveMission,
    int InventoryItemCount,
    IReadOnlyList<EquippedTreasureSnapshot> EquippedTreasures,
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
    WorldStatistics Statistics,
    CityMarketSnapshot Market,
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
    IReadOnlyList<PersonalLogEntry> SelectedPersonalLog,
    IReadOnlyList<LifeChronicleEntry> SelectedChronicle);

public static class CombatPowerFormula
{
    public static CombatPowerBreakdown Calculate(
        RealmStage realmStage,
        int realmLayer,
        Aptitudes aptitudes,
        QualityValue techniqueQuality,
        int techniqueMastery,
        QualityValue spellQuality,
        int spellMastery,
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
        var technique = checked(techniqueEffect * (750L + (techniqueMastery * 3L)) / 100);
        var spellEffect = QualityRules.GetEffectiveHundredths(spellQuality);
        var spell = checked(spellEffect * (600L + (spellMastery * 2L)) / 100);
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
