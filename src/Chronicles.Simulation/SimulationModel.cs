using Chronicles.Core;

namespace Chronicles.Simulation;

[Flags]
public enum ObservationSections : ushort
{
    None = 0,
    StatisticsSummary = 1 << 0,
    MarketSummary = 1 << 1,
    Map = 1 << 2,
    WorldLog = 1 << 3,
    PersonalLog = 1 << 4,
    Chronicle = 1 << 5,
    CombatPowerRanking = 1 << 6,
    RealmRanking = 1 << 7,
    AptitudeRanking = 1 << 8,
    RealmDistribution = 1 << 9,
    CombatPowerDistribution = 1 << 10,
    MarketGoods = 1 << 11,
    WorldChronicle = 1 << 12,
    WealthRanking = 1 << 13,
    Statistics = StatisticsSummary | RealmDistribution | CombatPowerDistribution,
    Market = MarketSummary | MarketGoods,
    AllRankings = CombatPowerRanking | RealmRanking | AptitudeRanking | WealthRanking,
    All = Statistics | Market | Map | WorldLog | WorldChronicle | PersonalLog | Chronicle | AllRankings,
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
    Traveling,
    HuntingMonster,
    MiningSpiritStones,
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
    CultivationEntered,
    GreatPerfectionAchieved,
    TechniqueAcquired,
    SpellAcquired,
    CombatInjury,
    CombatDeath,
    TravelCompleted,
    MonsterHuntWon,
    MonsterHuntLost,
    SpiritStoneMined,
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
    CultivationEntered,
    GreatPerfectionAchieved,
    TechniqueAcquired,
    SpellAcquired,
    CombatDeath,
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
    Wealth,
}

public enum RankingMovementPeriod : byte
{
    OneYear,
    TenYears,
    HundredYears,
    ThousandYears,
    TenThousandYears,
    All,
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

public readonly record struct CombatPowerFormulaDetails(
    int FormulaRevision,
    int RealmStageFactor,
    int RealmLayer,
    long RealmContribution,
    long DirectAptitudeContribution,
    long TechniqueBeforeAptitudeBonus,
    int TechniqueAptitudeBonusBasisPoints,
    long TechniqueContribution,
    int TechniqueMastery,
    int TechniqueUnderstanding,
    long SpellBeforeAptitudeBonus,
    int SpellAptitudeBonusBasisPoints,
    long SpellContribution,
    int SpellMastery,
    int SpellUnderstanding,
    int CombatExperience,
    int CombatMastery,
    int CombatUnderstanding,
    int CombatMasteryBonusBasisPoints,
    int CombatUnderstandingBonusBasisPoints,
    int CombatExperienceGainBonusBasisPoints,
    long CombatExperienceContribution,
    long TreasureContribution,
    CombatPowerBreakdown Breakdown);

public readonly record struct WorldEvent(
    long Sequence,
    SimulationTick Tick,
    WorldEventKind Kind,
    AgentId PrimaryAgent,
    AgentId? SecondaryAgent,
    long Value,
    string? PrimaryName = null,
    string? SecondaryName = null);

public readonly record struct WorldEventNotification(
    WorldEvent Event,
    string PrimaryName,
    string? SecondaryName,
    bool IsPermanentChronicle);

public readonly record struct WorldEventFeed(
    IReadOnlyList<WorldEventNotification> Events,
    int DroppedCount);

public readonly record struct WorldCenturyChronicleFeed(
    IReadOnlyList<WorldCenturyChronicleEntry> Entries,
    int DroppedCount);

public readonly record struct WorldChronicleLeader(
    AgentId AgentId,
    string Name);

public sealed record WorldCenturyChronicleEntry(
    long Sequence,
    long CenturyNumber,
    long StartYear,
    long EndYear,
    int[] RealmBreakthroughCounts,
    int[] RealmBreakthroughUniqueCounts,
    long[] CombatPowerPositionChanges,
    long[] RealmPositionChanges,
    long[] AptitudePositionChanges,
    long[] WealthPositionChanges,
    WorldChronicleLeader CombatPowerLeader,
    WorldChronicleLeader RealmLeader,
    WorldChronicleLeader AptitudeLeader,
    WorldChronicleLeader WealthLeader,
    int DeathCount,
    int UniqueDeathCount,
    int ReincarnationEntryCount,
    int UniqueReincarnationEntryCount,
    int RebirthCount,
    int UniqueRebirthCount,
    int ReincarnationRiverResidentsAtEnd,
    long CombatsCompleted,
    long MonsterHunts,
    long MissionsCompleted,
    long SpiritStonesMined,
    long TreasuresCrafted,
    long PillsCrafted);

public readonly record struct ActiveMissionSnapshot(
    MaterialId Material,
    int RequiredCount,
    int HeldCount);

public readonly record struct MaterialHoldingSnapshot(
    MaterialId Material,
    int Count);

public readonly record struct EquippedTreasureSnapshot(
    TreasureSlot Slot,
    QualityValue? Quality,
    RealmStage? RealmGrade = null);

public readonly record struct RealmDistributionEntry(
    RealmStage? Realm,
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

public readonly record struct EliteRankingMovementSnapshot(
    int TopCount,
    long CombatPowerCutoff,
    RealmStage RealmCutoff,
    int RealmLayerCutoff,
    int CombatPowerChangedCount,
    int RealmChangedCount,
    int CombatPowerChangeBasisPoints,
    int RealmChangeBasisPoints);

public readonly record struct RankingMovementWindowSnapshot(
    RankingMovementPeriod Period,
    SimulationTick FromTick,
    SimulationTick ToTick,
    int TopCount,
    int ComparedCount,
    long ObservationIntervalCount,
    long CombatPowerCutoff,
    bool HasRealmCutoff,
    RealmStage RealmCutoff,
    int RealmLayerCutoff,
    long WealthCutoff,
    long CombatPowerChangedCount,
    long RealmChangedCount,
    long WealthChangedCount,
    int CombatPowerChangeBasisPoints,
    int RealmChangeBasisPoints,
    int WealthChangeBasisPoints);

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
    long TotalSpiritStones,
    IReadOnlyList<EliteRankingMovementSnapshot>? EliteRankingMovements = null,
    IReadOnlyList<RankingMovementWindowSnapshot>? RankingMovementWindows = null)
{
    public static WorldStatistics Empty { get; } = new(
        Array.Empty<RealmDistributionEntry>(),
        Array.Empty<CombatPowerDistributionEntry>(),
        new RankingMovementStatistics(default, default, 0, 0, 0, 0),
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0);
}

public readonly record struct MarketGoodSnapshot(
    int GoodId,
    MarketGoodKind Kind,
    MaterialId? Material,
    RealmStage? Realm,
    long CityBasePrice,
    long CurrentPrice,
    int Stock,
    long Supply,
    long Demand,
    QualityValue? AverageQuality = null);

public readonly record struct CityProductionSnapshot(
    int GoodId,
    MarketGoodKind Kind,
    RealmStage? Realm,
    int CompletedMonths,
    int RequiredMonths,
    MaterialId? RequiredMaterial,
    int MaterialCost,
    long SpiritStoneCost,
    QualityValue? Quality);

public sealed record CityMarketSnapshot(
    long TreasurySpiritStones,
    long PurchaseTransactions,
    long SaleTransactions,
    long MissionDeliveries,
    long MaterialsProcessed,
    long PillsProduced,
    long TreasuresProduced,
    long TechniqueBooksProduced,
    long SpellBooksProduced,
    IReadOnlyList<MarketGoodSnapshot> Goods,
    long SpiritStonesMined = 0,
    long RemainingSpiritStoneOre = 0,
    long MiningMissionCount = 0,
    IReadOnlyList<CityProductionSnapshot>? ProductionQueue = null,
    long TotalStock = 0,
    long MaterialStock = 0,
    long ProcessedStock = 0,
    long TotalSupply = 0,
    long TotalDemand = 0,
    long UnmetDemand = 0,
    int ProductionCapacity = EconomyRules.MaximumConcurrentProduction)
{
    public static CityMarketSnapshot Empty { get; } = new(
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        Array.Empty<MarketGoodSnapshot>());
}

public sealed record AgentSnapshot(
    AgentId Id,
    string Name,
    int Rank,
    int RealmRank,
    int AptitudeRank,
    int WealthRank,
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
    long TechniqueTrainingExperience,
    int TechniqueUnderstanding,
    QualityValue SpellQuality,
    int SpellMastery,
    long SpellTrainingExperience,
    int SpellUnderstanding,
    int CombatExperience,
    int CombatMastery,
    int CombatUnderstanding,
    int Wins,
    int Losses,
    int Health,
    int MaximumHealth,
    int Qi,
    int MaximumQi,
    CombatAttributes CombatAttributes,
    int InjuryPoints,
    InjurySeverity InjurySeverity,
    AgentActivity Activity,
    SimulationTick NextDecisionTick,
    AgentId? OpponentId,
    SimulationTick LastCombatTick,
    int ChallengesIssued,
    int ActionsTaken,
    GameDate? LastActionDate,
    ushort LocationId,
    ushort TravelDestinationId,
    int MonsterHunts,
    int MonsterVictories,
    long SpiritStones,
    int GrowthPills,
    int BreakthroughPills,
    int MissionsCompleted,
    ActiveMissionSnapshot? ActiveMission,
    int InventoryItemCount,
    IReadOnlyList<MaterialHoldingSnapshot> Materials,
    IReadOnlyList<EquippedTreasureSnapshot> EquippedTreasures,
    CombatPowerBreakdown CombatPower,
    CultivationReadProjection Cultivation,
    CombatPowerFormulaDetails CombatPowerDetails);

public sealed record RankingEntry(
    RankingCategory Category,
    int Rank,
    AgentId AgentId,
    string Name,
    bool HasEnteredCultivation,
    RealmStage RealmStage,
    int RealmLayer,
    bool GreatPerfectionAchieved,
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
    IReadOnlyList<RankingEntry> WealthRankings,
    WorldStatistics Statistics,
    CityMarketSnapshot Market,
    WorldMapSnapshot Map,
    IReadOnlyDictionary<int, string> AgentNames,
    IReadOnlyList<WorldEvent> RecentEvents,
    IReadOnlyList<WorldCenturyChronicleEntry> PermanentWorldChronicle,
    int PermanentWorldChronicleCount,
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
