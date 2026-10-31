namespace Chronicles.Game.Politics;

public enum PoliticalAccountKind { Person, Group }
public readonly record struct PoliticalAccount(PoliticalAccountKind Kind, long Id)
{
    public static PoliticalAccount Person(long id) => new(PoliticalAccountKind.Person, id);
    public static PoliticalAccount Group(long id) => new(PoliticalAccountKind.Group, id);
}

public enum PoliticalRight { Ownership, Operation, Administration, Passage, Sovereignty }
public enum PolicyStage { Adopted, InsufficientFunds, InsufficientLabor, Preparing, Implemented, Cancelled }
public enum PoliticalEffectKind { Teaching, CultivationGrant, IndustryGrant, Salary, Fine, Defense, Relief }

public sealed record TerritorialClaim(long Id, long Group, int Province, long Created, string Basis, bool Active = true);
public sealed record PoliticalObligation(long Id, PoliticalAccount Creditor, PoliticalAccount Debtor,
    long Enforcer, int Province, long Outstanding, string Basis, long Created);
public sealed record PoliticalPayment(long Month, PoliticalAccount From, PoliticalAccount To, long Amount,
    string Kind, long Reference);
public sealed record PoliticalOffice(long Group, string Name, long Holder, long Appointed);
public sealed record PoliticalService(long Group, long Person, long Contribution, int Loyalty);
public sealed record PassageGrant(long Id, long Grantor, PoliticalAccount Holder, int Place, long Until);
public sealed record DiplomaticAccord(long Id, long FirstGroup, long SecondGroup, int Level, long Created);
public sealed record SectCharter(long Id, long Sect, long Sovereign, int Province, bool Teaching,
    bool Industry, long Granted, long Until, bool Active = true);
public sealed record PoliticalLaborReservation(long Id, long Group, long Person, long From, long Until,
    string Purpose, long Reference);
public sealed record PoliticalEffect(long Id, PoliticalEffectKind Kind, long Group, long Person, long Other,
    int Place, long Amount, string Subject, long From, long Until);
public sealed record PoliticalPlaceRights(int Place, long LocalSovereign, long HighestOverlord,
    long Administrator, long Owner, long Operator, IReadOnlyList<TerritorialClaim> Claims);
public sealed record PoliticalAccountBalance(PoliticalAccount Account, long AvailableMoney,
    long ReservedMoney, long Receivables, long Debt);
public sealed record PolicyHistory(long Month, long Preparation, long Group, string Category, int Before,
    int After, PolicyStage Stage, string Reason, long LawRevision);

public sealed class PolicyPreparation
{
    public long Id { get; set; }
    public long Group { get; set; }
    public long Author { get; set; }
    public string Category { get; set; } = "";
    public int Choice { get; set; }
    public int Place { get; set; }
    public long Jurisdiction { get; set; }
    public long LawRevision { get; set; }
    public long PreparationLawRevision { get; set; }
    public long Adopted { get; set; }
    public long Due { get; set; }
    public long Escrow { get; set; }
    public int RequiredWorkers { get; set; }
    public PolicyStage Stage { get; set; }
    public List<long> Workers { get; set; } = [];
}

/// <summary>Serializable political facts. Existing people, groups and places remain the authoritative entities.</summary>
public sealed class PoliticalState
{
    public int Schema { get; set; } = 1;
    public long NextId { get; set; } = 1;
    public List<TerritorialClaim> Claims { get; set; } = [];
    public List<PoliticalObligation> Obligations { get; set; } = [];
    public List<PoliticalPayment> Payments { get; set; } = [];
    public List<PoliticalOffice> Offices { get; set; } = [];
    public List<PoliticalService> Service { get; set; } = [];
    public List<PassageGrant> Passage { get; set; } = [];
    public List<DiplomaticAccord> Accords { get; set; } = [];
    public List<SectCharter> Charters { get; set; } = [];
    public List<PoliticalLaborReservation> Labor { get; set; } = [];
    public List<PoliticalEffect> Effects { get; set; } = [];
    public List<long> RoutedEffects { get; set; } = [];
    public List<PolicyPreparation> Preparations { get; set; } = [];
    public List<PolicyHistory> PolicyHistory { get; set; } = [];
    public List<long> TaxPeriods { get; set; } = [];
    public long ConsumedMoney { get; set; }
}

/// <summary>Adjustable developer defaults; these values do not assert additional user policy decisions.</summary>
public sealed record PoliticalRules
{
    public int PlacesPerProvince { get; init; } = 10;
    public long MaturityMonths { get; init; } = 216;
    public int SectLimit { get; init; } = 10;
    public long FoundingTreasury { get; init; } = 100;
    public int FamilyFounderMinimumRealm { get; init; }
    public long PolicyPreparationCost { get; init; } = 10;
    public long PreparationMonths { get; init; } = 1;
    public int PreparationWorkers { get; init; } = 1;
    public int TaxBasisPoints { get; init; } = 100;
    public int TributeBasisPoints { get; init; } = 1000;
    public long BaseSalary { get; init; } = 1;
    public long ActivityGrant { get; init; } = 5;
    public long ReliefThreshold { get; init; } = 10;
}

public sealed record PoliticalContext(long Month, long Revision, long LawRevision,
    IReadOnlyList<Person> People, IReadOnlyList<Group> Groups, IReadOnlyList<Place> Places, PoliticalRules Rules,
    IReadOnlyDictionary<long, long>? MaturityByPerson = null);

/// <summary>
/// An atomic instruction set. The host must revalidate immediately before replacing these entities and
/// NextState together, then add ConsumedMoney to its ledger. Planning and validation never mutate inputs.
/// </summary>
public sealed record PoliticalTransition(string Precondition, PoliticalState NextState,
    IReadOnlyList<Person> People, IReadOnlyList<Group> Groups, IReadOnlyList<Place> Places,
    long ConsumedMoney, string Reason);

public sealed record PoliticalResult(bool Accepted, string Message, PoliticalTransition? Transition)
{
    public static PoliticalResult Rejected(string message) => new(false, message, null);
}

public sealed record PolicyActivity(long Actor, long Group, string Category, long Subject,
    long Counterparty = 0, string Knowledge = "", int Place = -1);
