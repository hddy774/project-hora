using Chronicles.Game.Inventory;

namespace Chronicles.Game.Ecology;

public enum EcologicalFacilityKind { HouseholdCamp, Market, Workshop, Settlement, ExploredSite }
public enum EcologicalChangeKind { Depletion, Recovery, Disaster, RouteClosed, RouteRecovered, Monopoly, EducationLoss, HouseholdLoss, FertilityBottleneck, FacilityOpened, FacilityClosed }

public sealed record EcologyRules
{
    public int HistoryMonths { get; init; } = 24;
    public int MaximumChanges { get; init; } = 512;
    public int FacilityActivityMonths { get; init; } = 6;
    public int FoodReserveMonths { get; init; } = 3;
    public int MaximumSaleUnits { get; init; } = 12;
    public long DefaultUnitPrice { get; init; } = 1;
}

public sealed record EcologyConsumption(long Id, long Month, long Person, long Household, long Organization,
    int Place, string Definition, int Quantity, string Source);
public sealed record EcologyMaterialLoss(long Id, long Month, long Owner, int Place, string Definition, int Quantity, string Source);
public sealed record EcologyStockSnapshot(long Source, int Place, string Definition, long Remaining, long Capacity, long Month);
public sealed record EcologyPlaceSnapshot(int Place, int Residents, long[] Households, long[] Teachers,
    int Qi, long BlockedUntil, long Month);
public sealed record EcologyProduction(long Job, long Person, int Place, string Definition, int Quantity, int Quality,
    long Month, string Source);
public sealed record EcologyTrade(long Contract, int Place, string Definition, int Quantity, long Price,
    int? Quality, long Seller, long Buyer, long Month, string Source);
public sealed record EcologyDemand(long Month, int Place, string Definition, string Sector, long Subject,
    long Needed, long Consumed, long AccessibleStock, long AvailableMoney, long CarryUnits, string Source);
public sealed record RegionalEconomySample(long Month, int Place, int Province, string Definition,
    long Needed, long Consumed, long Produced, long PhysicalStock, long PublicSupply, int Sellers,
    long TradedQuantity, long Turnover, long? AskPerUnitMilli, long? PaidPerUnitMilli,
    long? QualityAdjustedPaidPerUnitMilli, long? IndicativePriceMilli, int ConcentrationBasisPoints,
    IReadOnlyList<string> Sources);
public sealed record NaturalFlowSample(long Month, int Place, long Opening, long Regeneration, long Extraction,
    long Loss, long Closing, long ObservedAdjustment, int QiBefore, int QiAfter, long BlockedUntil,
    IReadOnlyList<long> NaturalSources, IReadOnlyList<string> Sources);
public sealed record EcologyChange(long Id, long Month, int Place, EcologicalChangeKind Kind,
    long Before, long After, string Source);
public sealed record EcologicalFacility(long Id, int Place, EcologicalFacilityKind Kind, string Name,
    long Founded, long LastActivity, bool Active, bool Public, IReadOnlyList<long> Operators,
    IReadOnlyList<string> Sources);

/// <summary>Evidence and projections only. Inventory, people, money, source stocks and work remain under their existing authorities.</summary>
public sealed class EcologyState
{
    public int Schema { get; set; } = 1;
    public bool Initialized { get; set; }
    public long NextId { get; set; } = 1;
    public long LastSettledMonth { get; set; } = -1;
    public int InventoryEventCursor { get; set; }
    public long ConsumptionCursor { get; set; }
    public List<EcologyConsumption> Consumption { get; set; } = [];
    public List<EcologyMaterialLoss> Losses { get; set; } = [];
    public List<EcologyStockSnapshot> Stocks { get; set; } = [];
    public List<EcologyPlaceSnapshot> Places { get; set; } = [];
    public List<EcologyProduction> Production { get; set; } = [];
    public List<EcologyTrade> Trades { get; set; } = [];
    public List<long> RecordedJobs { get; set; } = [];
    public List<long> RecordedContracts { get; set; } = [];
    public List<EcologyDemand> Demand { get; set; } = [];
    public List<RegionalEconomySample> Markets { get; set; } = [];
    public List<NaturalFlowSample> NaturalFlows { get; set; } = [];
    public List<EcologyChange> Changes { get; set; } = [];
    public List<EcologicalFacility> Facilities { get; set; } = [];
}

public sealed record EcologyBirthObservation(bool Enabled, int MinimumRealm, long Maturity, bool BothParents, bool MartialEquivalent);
public sealed record EcologyContext(long Month, long Revision, IReadOnlyList<Person> People, IReadOnlyList<Group> Groups,
    IReadOnlyList<Place> Places, IReadOnlyList<Item> Items, InventoryLedger Inventory, IReadOnlyList<GameEvent> Events, long FoodUse, EcologyRules Rules,
    EcologyBirthObservation? Birth = null);
public sealed record PublicMarketListing(long Contract, string Kind, long Seller, int Place, string Definition,
    string Name, int Quantity, long Price, long Until, long Observed, string Source);
public sealed record PublicPriceSample(long Month, int Place, string Definition, long PublicSupply,
    long TradedQuantity, long? AskPerUnitMilli, long? PaidPerUnitMilli, IReadOnlyList<string> Sources);
public sealed record EconomyView(long Month, bool Observer, IReadOnlyList<RegionalEconomySample> Regions,
    IReadOnlyList<EcologyDemand> Demand, IReadOnlyList<PublicMarketListing> Listings,
    IReadOnlyList<PublicPriceSample> PublicPrices, IReadOnlyList<EcologyConsumption> Consumption,
    IReadOnlyList<EcologyMaterialLoss> Losses, IReadOnlyList<NaturalFlowSample> NaturalFlows, IReadOnlyList<EcologyChange> Changes,
    IReadOnlyList<EcologicalFacility> Facilities, IReadOnlyList<SourceObservation> KnownSources);

public sealed record EcologicalOwnStock(long Item, string Definition, int Quantity, int? KnownQuality,
    int Weight, bool Available, bool Equipped, bool Saleable);
public sealed record EcologicalKnownSource(long Source, int Place, string Definition, long? ObservedRemaining,
    long Observed, string SourceEvidence);
public sealed record EcologicalKnownPlace(int Place, int? ObservedSource, long Observed, bool Adjacent, bool KnownBlocked);
public sealed record EcologicalRecipe(string Id, string Output, int Quantity, IReadOnlyDictionary<string, int> Inputs);
public sealed record EcologicalWorkNeed(string Definition, int Quantity, string Recipe);
/// <summary>This is the complete information boundary for economic AI. It contains no private counterpart inventory, wealth or truth.</summary>
public sealed record EcologyDecisionInput(long Person, long Month, int Place, long Money, long FreeCarryWeight,
    long FoodNeed, IReadOnlyList<EcologicalOwnStock> Stock, IReadOnlyList<PublicMarketListing> Listings,
    IReadOnlyList<PublicPriceSample> PublicPrices, IReadOnlyList<EcologicalKnownSource> Sources,
    IReadOnlyList<EcologicalKnownPlace> Places, IReadOnlyList<EcologicalRecipe> Recipes, EcologyRules Rules,
    IReadOnlyList<EcologicalWorkNeed>? WorkNeeds = null);
public sealed record EcologyPlan(string Kind, long Target, int Quantity, long Price, string Definition,
    string Recipe, string Goal, string Evidence);
