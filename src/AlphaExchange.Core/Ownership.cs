namespace AlphaExchange.Core;

public enum HolderCategory { Institutions, Retail, Founders, Bank, Treasury }
public sealed record OwnershipSlice(HolderCategory Category, string Name, long Shares, int Holders);
public sealed record Shareholder(int TraderId, string Name, long Shares);
public sealed record OwnershipStructure(string SecurityId, long Hour, bool Active, long IssuedShares,
    long OutstandingShares, long FloatShares, long BorrowedShares, long ReservedLending,
    IReadOnlyList<OwnershipSlice> Slices, IReadOnlyList<Shareholder> Institutions)
{
    public double IssuedRatio(long shares) => IssuedShares > 0 ? (double)shares/IssuedShares : 0;
    public double OutstandingRatio(long shares) => OutstandingShares > 0 ? (double)shares/OutstandingShares : 0;
}

public sealed partial class GameEngine
{
    OwnershipStructure? cachedOwnership;
    (GameState State,int Stock,long Hour,long Transactions,long Events,long Issued,long Treasury,long Founders,long Bank,long Reserved,bool Active) ownershipKey;

    public OwnershipStructure Ownership(int stockIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(stockIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(stockIndex,State.Stocks.Count);
        var stock=State.Stocks[stockIndex];
        var key=(State,stockIndex,State.CompletedHours,State.NextTransactionId,State.NextCorporateEventId,
            stock.TotalShares,stock.TreasuryShares,stock.FounderShares,State.Bank.ShareInventory[stockIndex],State.Bank.ReservedLending[stockIndex],stock.Active);
        if(cachedOwnership is not null && ownershipKey==key) return cachedOwnership;
        long institution=0,retail=0,borrowed=0; int retailHolders=0;
        var holders=new List<Shareholder>(AiCount);
        foreach(var trader in State.Bots)
        {
            long shares=trader.Shares[stockIndex]; institution=checked(institution+shares);
            borrowed=checked(borrowed+trader.ShortShares[stockIndex]);
            if(shares>0) holders.Add(new Shareholder(trader.Id,trader.Name,shares));
        }
        foreach(var trader in State.Retail)
        {
            long shares=trader.Shares[stockIndex]; retail=checked(retail+shares);
            borrowed=checked(borrowed+trader.ShortShares[stockIndex]);
            if(shares>0) retailHolders++;
        }
        holders.Sort((a,b)=>a.Shares==b.Shares ? a.TraderId.CompareTo(b.TraderId) : b.Shares.CompareTo(a.Shares));
        var slices=new[] { new OwnershipSlice(HolderCategory.Institutions,"기관",institution,holders.Count),
            new OwnershipSlice(HolderCategory.Retail,"개인",retail,retailHolders),
            new OwnershipSlice(HolderCategory.Founders,"창업자",stock.FounderShares,stock.FounderShares>0 ? 1 : 0),
            new OwnershipSlice(HolderCategory.Bank,"은행 보유",State.Bank.ShareInventory[stockIndex],State.Bank.ShareInventory[stockIndex]>0 ? 1 : 0),
            new OwnershipSlice(HolderCategory.Treasury,"자사주",stock.TreasuryShares,stock.TreasuryShares>0 ? 1 : 0) };
        if(slices.Sum(s=>s.Shares)!=stock.TotalShares) throw new InvalidDataException("지분 합계와 발행 주식 수가 다릅니다.");
        ownershipKey=key;
        return cachedOwnership=new OwnershipStructure(stock.SecurityId,State.CompletedHours,stock.Active,stock.TotalShares,
            stock.OutstandingShares,stock.FloatShares,borrowed,State.Bank.ReservedLending[stockIndex],
            Array.AsReadOnly(slices),holders.AsReadOnly());
    }
}
