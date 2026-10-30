using System.Text.Json;

namespace Chronicles.Game.Inventory;

/// <summary>The inventory aggregate. Items and people are the same authoritative models used by GameState.</summary>
public sealed class InventoryState
{
    public string BaseFingerprint { get; set; } = "";
    public long Month { get; set; }
    public long NextId { get; set; } = 1;
    public long Revision { get; set; }
    public List<Item> Items { get; set; } = [];
    public List<Person> People { get; set; } = [];
    public SortedDictionary<string, MaterialFlow> Materials { get; set; } = new(StringComparer.Ordinal);
    public InventoryLedger Ledger { get; set; } = new();

    /// <summary>Copies world authority and the saved supplemental ledger into an isolated candidate.</summary>
    public static InventoryState FromWorld(GameState world, InventoryLedger? ledger = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        var state = Copy(new InventoryState { Month = world.Month, NextId = world.NextId, Revision = world.Revision,
            Items = world.Items, People = world.People, Materials = world.Materials, Ledger = ledger ?? new(),
            BaseFingerprint = InventoryEngine.WorldFingerprint(world) });
        InventoryEngine.Validate(state);
        return state;
    }

    /// <summary>Copies accepted authority back. The host must also save Ledger and rebuild its item/person indexes.</summary>
    public InventoryLedger ApplyTo(GameState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        InventoryEngine.Validate(this);
        if (BaseFingerprint.Length == 0 || BaseFingerprint != InventoryEngine.WorldFingerprint(world))
            throw new InvalidOperationException("Inventory candidate is stale; create it from the current world.");
        var snapshot = Copy(this);
        world.Items = snapshot.Items; world.People = snapshot.People; world.Materials = snapshot.Materials;
        world.NextId = snapshot.NextId; world.Revision = snapshot.Revision;
        return snapshot.Ledger;
    }

    internal static InventoryState Copy(InventoryState state)
    {
        var copy = JsonSerializer.Deserialize<InventoryState>(JsonSerializer.SerializeToUtf8Bytes(state))
            ?? throw new InvalidDataException("Empty inventory state.");
        copy.Materials = new(copy.Materials, StringComparer.Ordinal);
        copy.Ledger.Receipts = new(copy.Ledger.Receipts, StringComparer.Ordinal);
        return copy;
    }
}

/// <summary>Save this alongside GameState; it stores no second copy of physical items or money.</summary>
public sealed class InventoryLedger
{
    public int BaseCarryCapacity { get; set; } = 500;
    public int ProductionWork { get; set; } = 100;
    public int ProfessionalWork { get; set; } = 100;
    public int ExtractionWorkPerUnit { get; set; } = 25;
    public List<ItemMetadata> Metadata { get; set; } = [];
    public List<AssetReservation> Reservations { get; set; } = [];
    public List<LaborReservation> Labor { get; set; } = [];
    public List<ProductionJob> Jobs { get; set; } = [];
    public List<InventoryContract> Contracts { get; set; } = [];
    public List<NaturalStock> Sources { get; set; } = [];
    public List<InventoryEffect> Effects { get; set; } = [];
    public List<InventoryAppraisal> Appraisals { get; set; } = [];
    public List<SourceObservation> Observations { get; set; } = [];
    public List<ArrayInstallation> Arrays { get; set; } = [];
    public List<BindingContract> Bindings { get; set; } = [];
    public List<InventoryEvent> Events { get; set; } = [];
    public SortedDictionary<string, InventoryReceipt> Receipts { get; set; } = new(StringComparer.Ordinal);
}

public sealed class ItemMetadata
{
    public long Item { get; set; }
    public long Custodian { get; set; }
    public long Carrier { get; set; }
    public List<long> UseRights { get; set; } = [];
    public List<string> Provenance { get; set; } = [];
    public int? Capacity { get; set; }
    public List<ItemKind> AllowedContents { get; set; } = [];
    public bool AllowNestedContainers { get; set; } = true;
}

public sealed class AssetReservation
{
    public long Id { get; set; }
    public long Actor { get; set; }
    public string Purpose { get; set; } = "";
    public List<long> Items { get; set; } = [];
    public bool Active { get; set; } = true;
}

/// <summary>Units are hundredths of one person's time per month; every activity uses this same budget.</summary>
public sealed class LaborReservation
{
    public long Id { get; set; }
    public long Person { get; set; }
    public string Purpose { get; set; } = "";
    public long Start { get; set; }
    public long End { get; set; }
    public int Units { get; set; }
    public long Facility { get; set; }
    public int FacilityCapacity { get; set; } = 100;
    public bool Active { get; set; } = true;
}

public sealed class ProductionJob
{
    public long Id { get; set; }
    public long Actor { get; set; }
    public string Recipe { get; set; } = "";
    public string Status { get; set; } = "reserved";
    public int Place { get; set; }
    public long Due { get; set; }
    public long Reservation { get; set; }
    public long Labor { get; set; }
    public long Target { get; set; }
    public long Source { get; set; }
    public int OutputQuantity { get; set; }
    public List<long> Inputs { get; set; } = [];
    public List<ConsumedMaterial> Consumed { get; set; } = [];
    public int Quality { get; set; }
    public int Damage { get; set; }
    public ItemDefect Defects { get; set; }
    public long BoundSoul { get; set; }
    public List<string> Provenance { get; set; } = [];
}

public sealed record ConsumedMaterial(string Definition, int Quantity);
public sealed class NaturalStock
{
    public long Id { get; set; }
    public int Place { get; set; }
    public string Definition { get; set; } = "";
    public string Activity { get; set; } = "";
    public long Remaining { get; set; }
    public long Capacity { get; set; }
    public int Regeneration { get; set; }
    public long LastMonth { get; set; }
    public string Origin { get; set; } = "";
}

public sealed class InventoryContract
{
    public long Id { get; set; }
    public string Kind { get; set; } = "";
    public string Status { get; set; } = "open";
    public long Seller { get; set; }
    public long Buyer { get; set; }
    public long Carrier { get; set; }
    public long Item { get; set; }
    public long Reservation { get; set; }
    public long Labor { get; set; }
    public long Price { get; set; }
    public long Escrow { get; set; }
    public long Fee { get; set; }
    public long Due { get; set; }
    public int Origin { get; set; }
    public int Destination { get; set; }
}

public sealed record InventoryAppraisal(long Appraiser, long Item, string Definition, int EstimatedQuality, int Damage, ItemDefect DetectedDefects, long Month);
public sealed record SourceObservation(long Observer, long Source, int Place, long Remaining, long Month, string Evidence);
public sealed record EquipmentContribution(long Item, int Quality, int Damage, IReadOnlyList<int> Slots);
public sealed record InventoryEffect(string Cause, long Target, string Kind, int Magnitude, long Start, long End);
public sealed class ArrayInstallation
{
    public long Id { get; set; }
    public long Item { get; set; }
    public long Owner { get; set; }
    public int Place { get; set; }
    public int Range { get; set; } = 1;
    public int Condition { get; set; } = 100;
    public bool Active { get; set; }
    public long PaidUntil { get; set; }
}
public sealed record BindingContract(long Id, long Item, long Soul, long Established, string Evidence);
public sealed record InventoryEvent(long Month, string Kind, long Actor, long Subject, string Detail);
public sealed record InventoryReceipt(string Signature, long Subject, string Message);
public sealed record InventoryResult(bool Accepted, string Message, InventoryState State, long Subject = 0, bool Replayed = false);

public abstract record InventoryCommand(string Id, long Actor);
public sealed record SplitInventoryItem(string Id, long Actor, long Item, int Quantity) : InventoryCommand(Id, Actor);
public sealed record MergeStacks(string Id, long Actor, long Target, long Source) : InventoryCommand(Id, Actor);
public sealed record StoreItem(string Id, long Actor, long Item, long Container = 0, bool Ground = false) : InventoryCommand(Id, Actor);
public sealed record TransferOwnership(string Id, long Actor, long Item, long Recipient) : InventoryCommand(Id, Actor);
public sealed record GrantUse(string Id, long Actor, long Item, long User, bool Allowed) : InventoryCommand(Id, Actor);
public sealed record SetCustodian(string Id, long Actor, long Item, long Custodian) : InventoryCommand(Id, Actor);
public sealed record EquipItem(string Id, long Actor, long Item, long ReturnContainer = 0, bool ReturnToGround = false) : InventoryCommand(Id, Actor);
public sealed record UnequipItem(string Id, long Actor, long Item, long ReturnContainer = 0, bool ReturnToGround = false) : InventoryCommand(Id, Actor);
public sealed record BindItem(string Id, long Actor, long Item, string Evidence) : InventoryCommand(Id, Actor);
public sealed record DamageItem(string Id, long Actor, long Item, int Damage) : InventoryCommand(Id, Actor);
public sealed record ReserveLabor(string Id, long Actor, string Purpose, long Start, long End, int Units, long Facility = 0,
    int FacilityCapacity = 100) : InventoryCommand(Id, Actor);
public sealed record ReleaseLabor(string Id, long Actor, long Reservation) : InventoryCommand(Id, Actor);
public sealed record StartProduction(string Id, long Actor, string Recipe, long Due, int LaborUnits = 100, long Facility = 0,
    int FacilityCapacity = 100) : InventoryCommand(Id, Actor);
public sealed record BeginProduction(string Id, long Actor, long Job) : InventoryCommand(Id, Actor);
public sealed record FinishProduction(string Id, long Actor, long Job, bool Succeeded = true) : InventoryCommand(Id, Actor);
public sealed record CancelProduction(string Id, long Actor, long Job) : InventoryCommand(Id, Actor);
public sealed record ExtractResource(string Id, long Actor, long Source, int Quantity, long Due, int LaborUnits = 100) : InventoryCommand(Id, Actor);
public sealed record ConsumeItem(string Id, long Actor, long Item) : InventoryCommand(Id, Actor);
public sealed record RepairItem(string Id, long Actor, long Item, long Due) : InventoryCommand(Id, Actor);
public sealed record TreatPerson(string Id, long Actor, long Patient, long Due, bool PatientConsented = false) : InventoryCommand(Id, Actor);
public sealed record AppraisePhysical(string Id, long Actor, long Item, long Due) : InventoryCommand(Id, Actor);
public sealed record ExploreNaturalSource(string Id, long Actor, long Source, long Due) : InventoryCommand(Id, Actor);
public sealed record InstallArray(string Id, long Actor, long Item, bool HasInstallationRight, int Range = 1) : InventoryCommand(Id, Actor);
public sealed record MaintainArray(string Id, long Actor, long Installation, long Due) : InventoryCommand(Id, Actor);
public sealed record DismantleArray(string Id, long Actor, long Installation) : InventoryCommand(Id, Actor);
public sealed record OfferItem(string Id, long Actor, long Item, long Price, long Due, bool Auction = false) : InventoryCommand(Id, Actor);
public sealed record WithdrawOffer(string Id, long Actor, long Contract) : InventoryCommand(Id, Actor);
public sealed record BuyItem(string Id, long Actor, long Contract) : InventoryCommand(Id, Actor);
public sealed record PlaceBid(string Id, long Actor, long Contract, long Amount) : InventoryCommand(Id, Actor);
public sealed record BeginTransport(string Id, long Actor, long Item, long Carrier, long Recipient, int Destination,
    long Due, long Fee, bool RouteAllowed, bool CarrierConsented = false, int LaborUnits = 100) : InventoryCommand(Id, Actor);
public sealed record FinishTransport(string Id, long Actor, long Contract, bool Delivered) : InventoryCommand(Id, Actor);
/// <summary>System command: call after updating Month and life/place authority; settles all due offers deterministically.</summary>
public sealed record SettleInventory(string Id) : InventoryCommand(Id, 0);
