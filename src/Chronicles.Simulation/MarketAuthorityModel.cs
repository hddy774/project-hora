using Chronicles.Core;

namespace Chronicles.Simulation;

public enum EconomicOwnerKind : byte
{
    Agent,
    City,
    Organization,
}

public readonly record struct EconomicOwnerRef(EconomicOwnerKind Kind, long Id);

public enum AssetHolderKind : byte
{
    PersonalInventory,
    CityWarehouse,
    OrganizationWarehouse,
}

public readonly record struct AssetHolderRef(
    AssetHolderKind Kind,
    long OwnerId,
    ushort LocationId);

public enum MarketTradeChannel : byte
{
    CityOpenMarket,
    Barter,
    SectInternal,
    Alliance,
}

public enum MarketOfferSide : byte
{
    Buy,
    Sell,
}

public enum MarketOfferStatus : byte
{
    Active,
    Filled,
    Cancelled,
    Expired,
}

public enum MarketReservationStatus : byte
{
    Reserved,
    Settled,
    Cancelled,
    Expired,
    Revalidate,
}

public enum MarketContractStatus : byte
{
    Reserved,
    Settled,
    OfferUnavailable,
    ChannelUnavailable,
    OutOfStock,
    InsufficientFunds,
    DuplicateSource,
    Expired,
    OwnerUnavailable,
    Revalidate,
    CapacityExceeded,
}

public readonly record struct MarketOfferSnapshot(
    MarketOfferId Id,
    long Revision,
    EconomicOwnerRef Owner,
    AssetHolderRef Holder,
    MarketTradeChannel Channel,
    MarketOfferSide Side,
    int GoodId,
    int AvailableCount,
    long UnitPrice,
    long CreatedMonth,
    long ExpiresMonth,
    MarketOfferStatus Status);

public readonly record struct MarketReservationSnapshot(
    MarketReservationId Id,
    MarketOfferId OfferId,
    EconomicOwnerRef Requester,
    int GoodId,
    int Count,
    long UnitPrice,
    long CreatedMonth,
    long ExpiresMonth,
    MarketReservationStatus Status,
    long SourceResultId);

public readonly record struct MarketContractReceipt(
    MarketContractStatus Status,
    MarketOfferId? OfferId = null,
    MarketReservationId? ReservationId = null,
    long TotalSpiritStones = 0)
{
    public bool IsReserved => Status == MarketContractStatus.Reserved;
    public bool IsSettled => Status == MarketContractStatus.Settled;
}

public readonly record struct MarketOfferAuthorityCheckpoint(
    long OfferId,
    long Revision,
    int GoodId,
    MarketTradeChannel Channel,
    MarketOfferSide Side,
    long CreatedMonth,
    long ExpiresMonth,
    MarketOfferStatus Status);

public readonly record struct MarketReservationAuthorityCheckpoint(
    long ReservationId,
    long OfferId,
    int AgentIndex,
    int GoodId,
    int Count,
    long UnitPrice,
    long CreatedMonth,
    long ExpiresMonth,
    MarketReservationStatus Status,
    long SourceResultId);

public sealed record MarketAuthorityCheckpoint(
    long NextOfferId,
    long NextReservationId,
    int[] ReservedStock,
    MarketOfferAuthorityCheckpoint[] Offers,
    MarketReservationAuthorityCheckpoint[] Reservations,
    long LastExpiryMonth);
