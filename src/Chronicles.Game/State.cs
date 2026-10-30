namespace Chronicles.Game;

public enum Realm { Mortal, Qi, Foundation, GoldenCore, NascentSoul, Spirit, Void, Integration, Tribulation, Mahayana, HalfImmortal }
public enum LifeStatus { Alive, Dead, Ascended, Departed }
public enum ActionKind { Idle, Gather, Cultivate, Study, Martial, Travel, Craft, Heal, Explore, Teach, Guard, Farm, Breakthrough, Trial, Wait }
public enum GroupKind { Family, Sect, Dynasty }
public enum ItemKind { Food, Herb, Ore, Wood, Cloth, Paper, Ink, Metal, Pill, Tool, Talisman, Array, Book, Bag, Weapon, Armor }
[Flags]
public enum ItemDefect { None = 0, MissingPages = 1, BrokenCircuit = 2, Contamination = 4, LostCore = 8 }

public sealed record Formation(Realm Realm, string Path, int Quality, int Defect, long Month, string Source);
public sealed record Belief(string Key, string Value, int Confidence, long Observed, long Expires, string Source);
public sealed record TraitState(string Id, string Source, long Acquired, long? Expires, bool Active);
public sealed record Trace(int Preserved, int Accessible, int Integrated);
public readonly record struct PersonObservation(long Id, string Name, Realm Realm, int Layer, int Place, long Month,
    bool? ApparentGestates, bool AppearsAdult);
public readonly record struct PlaceObservation(int Place, int Source, int Qi, string Facilities, long Month,
    long? Sovereign = null, long? Owner = null, long? Operator = null, long? Delegate = null, long? BlockedUntil = null);
public sealed record GameEvent(long Id, long Month, string Kind, long Actor, long Target, string Text, string Source, string TargetKind = "person");
public sealed record LawChange(long Month, long Revision, string Key, long Before, long After);
public sealed record PlanStep(string Action, long Target, string Data);
public sealed record LessonRequest(long Pupil, string Knowledge, long Requested, long Expires);
public sealed record MonthlyCensus(long Month, int Living, int Mortal, int Martial, int Cultivators,
    int Births, int Deaths, long Money, long Food);
public sealed record CommandResult(string Id, bool Accepted, string Message, long Revision);
public sealed class MaterialFlow
{
    public long Opening { get; set; }
    public long Extraction { get; set; }
    public long Production { get; set; }
    public long Consumption { get; set; }
    public long Loss { get; set; }
}

public sealed class Person
{
    public int Slot { get; set; } = -1;
    public uint SlotGeneration { get; set; }
    public long Id { get; set; }
    public long BodyId { get; set; }
    public long SoulId { get; set; }
    public string Name { get; set; } = "";
    public string Species { get; set; } = "human";
    public bool Gestates { get; set; }
    public LifeStatus Status { get; set; }
    public long Born { get; set; }
    public long? Ended { get; set; }
    public long Lifespan { get; set; }
    public long ParentOne { get; set; }
    public long ParentTwo { get; set; }
    public long Partner { get; set; }
    public long Household { get; set; }
    public long Sect { get; set; }
    public int Place { get; set; }
    public Realm Realm { get; set; }
    public int Layer { get; set; } = 1;
    public bool GreatPerfection { get; set; }
    public long Work { get; set; }
    public int Martial { get; set; }
    public int MartialWork { get; set; }
    public int Health { get; set; } = 100;
    public long Money { get; set; }
    public long NextBirth { get; set; }
    public string Goal { get; set; } = "자립";
    public string Reason { get; set; } = "창세 초기 조건";
    public List<PlanStep> Plan { get; set; } = [];
    public List<LessonRequest> LessonRequests { get; set; } = [];
    public List<GameEvent> Journal { get; set; } = [];
    public int PlanExpansions { get; set; }
    public long LastPeopleRevision { get; set; } = -1;
    public int ObservationCursor { get; set; }
    public int ObservationRemaining { get; set; }
    public ActionKind Action { get; set; }
    public long Due { get; set; }
    public long ActionTarget { get; set; }
    public string ActionData { get; set; } = "";
    public long ActionRevision { get; set; }
    public int[] Processes { get; set; } = new int[4];
    public int[] Components { get; set; } = new int[7];
    public bool Irreversible { get; set; }
    public int PendingLayer { get; set; }
    public int StructureDamage { get; set; }
    public int BondStability { get; set; } = 100;
    public long SealedUntil { get; set; }
    public bool Sealed { get; set; }
    public long SoulPactUntil { get; set; }
    public string SoulPactSource { get; set; } = "";
    public int[] Roots { get; set; } = new int[5];
    public int[] Aptitudes { get; set; } = new int[6];
    public int[] Personality { get; set; } = new int[14];
    public List<Formation> Formations { get; set; } = [];
    public List<TraitState> Traits { get; set; } = [];
    public SortedDictionary<string, int> Skills { get; set; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, int> Understanding { get; set; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, Belief> Beliefs { get; set; } = new(StringComparer.Ordinal);
    public SortedDictionary<long, PersonObservation> ObservedPeople { get; set; } = [];
    public SortedDictionary<int, PlaceObservation> ObservedPlaces { get; set; } = [];
    public List<int> KnownPlaces { get; set; } = [];
    public List<long> KnownPeople { get; set; } = [];
    public SortedDictionary<int, long> Equipment { get; set; } = [];
    public long Guardian { get; set; }
}

public sealed class Soul
{
    public int Integrity { get; set; } = 100;
    public long Id { get; set; }
    public long Parent { get; set; }
    public long Root { get; set; }
    public long CurrentPerson { get; set; }
    public long Ready { get; set; }
    public string Status { get; set; } = "embodied";
    public int Route { get; set; }
    public int SplitCount { get; set; }
    public Realm SourceRealm { get; set; }
    public int SourceLayer { get; set; }
    public string Evidence { get; set; } = "";
    public Trace[] Traces { get; set; } = Enumerable.Range(0, 6).Select(_ => new Trace(0, 0, 0)).ToArray();
}

public sealed class Pregnancy
{
    public long Id { get; set; }
    public long Mother { get; set; }
    public long Father { get; set; }
    public long Due { get; set; }
    public long LawRevision { get; set; }
    public long Recovery { get; set; }
    public int HealthCost { get; set; }
    public int[] Roots { get; set; } = [];
    public int[] Aptitudes { get; set; } = [];
    public bool Completed { get; set; }
    public long Child { get; set; }
}

public sealed class Place
{
    public int Id { get; set; }
    public int Province { get; set; }
    public string Name { get; set; } = "";
    public string Terrain { get; set; } = "";
    public int Element { get; set; }
    public int Source { get; set; }
    public int Qi { get; set; }
    public int Turbidity { get; set; }
    public List<int> Neighbors { get; set; } = [];
    public List<string> Facilities { get; set; } = [];
    public long Owner { get; set; }
    public long Operator { get; set; }
    public long Sovereign { get; set; }
    public long Delegate { get; set; }
    public long BlockedUntil { get; set; }
    public int DiscoveryRemaining { get; set; } = 1;
    public long PeopleRevision { get; set; }
}

public sealed class Item
{
    public long Id { get; set; }
    public string Definition { get; set; } = "";
    public long Owner { get; set; }
    public int Place { get; set; }
    public int Quantity { get; set; }
    public int Quality { get; set; }
    public int Damage { get; set; }
    public ItemDefect Defects { get; set; }
    public long Container { get; set; }
    public long Reserved { get; set; }
    public long BoundSoul { get; set; }
    public string Source { get; set; } = "";
}

public sealed class Group
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public GroupKind Kind { get; set; }
    public long Leader { get; set; }
    public long Overlord { get; set; }
    public long Money { get; set; }
    public long Founded { get; set; }
    public bool Dissolved { get; set; }
    public List<long> Members { get; set; } = [];
    public SortedDictionary<string, int> Policies { get; set; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, int> PendingPolicies { get; set; } = new(StringComparer.Ordinal);
    public long PolicyDue { get; set; }
    public long PolicyEscrow { get; set; }
    public List<int> Provinces { get; set; } = [];
    public long Jurisdiction { get; set; }
    public long Debt { get; set; }
}

public sealed class Contract
{
    public long Id { get; set; }
    public string Kind { get; set; } = "";
    public long Seller { get; set; }
    public long Buyer { get; set; }
    public long Carrier { get; set; }
    public long Item { get; set; }
    public int Destination { get; set; }
    public long Price { get; set; }
    public long Escrow { get; set; }
    public long Due { get; set; }
    public string Status { get; set; } = "open";
}

public sealed class GameState
{
    public int Schema { get; set; } = 1;
    public ulong Seed { get; set; }
    public ulong RandomState { get; set; }
    public long Month { get; set; }
    public long Revision { get; set; }
    public long LawRevision { get; set; } = 1;
    public long NextId { get; set; } = 1;
    public long Controlled { get; set; }
    public long ViewGeneration { get; set; }
    public long CreatedMoney { get; set; }
    public long ConsumedMoney { get; set; }
    public long BirthCount { get; set; }
    public long DeathCount { get; set; }
    public Knowledge.KnowledgeState Knowledge { get; set; } = new();
    public Inventory.InventoryLedger Inventory { get; set; } = new();
    public Politics.PoliticalState Politics { get; set; } = new();
    public People.PeopleState PeopleDomain { get; set; } = global::Chronicles.Game.People.PeopleState.Empty();
    public SortedDictionary<long, Cultivation.CultivationState> Cultivation { get; set; } = [];
    public SortedDictionary<int, Cultivation.Elements> CultivationSupply { get; set; } = [];
    public SortedDictionary<string, long> Laws { get; set; } = global::Chronicles.Game.Laws.CreateDefaults();
    public List<Person> People { get; set; } = [];
    public List<PopulationSlot> ActiveSlots { get; set; } = [];
    public List<Soul> Souls { get; set; } = [];
    public List<Pregnancy> Pregnancies { get; set; } = [];
    public List<Place> Places { get; set; } = [];
    public List<Item> Items { get; set; } = [];
    public SortedDictionary<string, MaterialFlow> Materials { get; set; } = new(StringComparer.Ordinal);
    public List<Group> Groups { get; set; } = [];
    public List<Contract> Contracts { get; set; } = [];
    public List<GameEvent> Events { get; set; } = [];
    public List<LawChange> LawHistory { get; set; } = [];
    public List<MonthlyCensus> Census { get; set; } = [];
    public SortedDictionary<string, CommandResult> Commands { get; set; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, string> CommandSignatures { get; set; } = new(StringComparer.Ordinal);
}

public sealed class PopulationSlot
{
    public long Person { get; set; }
    public uint Generation { get; set; } = 1;
}
