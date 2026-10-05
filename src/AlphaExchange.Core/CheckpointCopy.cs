using System.Reflection;

namespace AlphaExchange.Core;

// Copy mutable state on the simulation thread; encode the isolated copy on the writer.
// Scalar fields and immutable strings/records can be shared. Every encoded mutable
// collection/DTO is copied; retail UI fields excluded by v6 JSON need no storage copy.
internal static class CheckpointCopy
{
    static readonly Func<object,object> Clone = typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic)!
        .CreateDelegate<Func<object,object>>();
    public static T Scalar<T>(T value) where T:class => (T)Clone(value);
    public static DailySnapshot Daily(DailySnapshot value)
    {
        var copy=Scalar(value); copy.CashFlows=new(value.CashFlows);
        copy.SectorVolumes=new(value.SectorVolumes); copy.SectorTurnovers=new(value.SectorTurnovers);
        copy.SecurityIds=(string[])value.SecurityIds.Clone(); copy.StockSectors=(string[])value.StockSectors.Clone();
        copy.StockShares=(long[])value.StockShares.Clone(); copy.FloatShares=(long[])value.FloatShares.Clone();
        copy.MarkPrices=(decimal[])value.MarkPrices.Clone(); copy.SplitFactors=(double[])value.SplitFactors.Clone();
        copy.StockVolumes=(long[])value.StockVolumes.Clone(); copy.StockTurnovers=(long[])value.StockTurnovers.Clone();
        copy.StockPrices=(int[])value.StockPrices.Clone(); copy.Institutions=value.Institutions.Select(Scalar).ToList(); return copy;
    }
    public static CompanyVote Vote(CompanyVote value)
    { var copy=Scalar(value); copy.Ballots=new(value.Ballots); return copy; }
    public static BankruptcyRecord Bankruptcy(BankruptcyRecord value)
    { var copy=Scalar(value); copy.Report=value.Report is null ? null : Scalar(value.Report); return copy; }
    static T[] ArrayCopy<T>(T[] value,bool compact) where T:struct
        => compact && value.All(v=>EqualityComparer<T>.Default.Equals(v,default)) ? [] : (T[])value.Clone();
    static Trader Trader(Trader value,bool storage=false)
    {
        bool compact=storage && value.IsRetail;
        var copy=Scalar(value); copy.Shares=ArrayCopy(value.Shares,compact); copy.AverageCost=ArrayCopy(value.AverageCost,compact);
        copy.ReservedShares=ArrayCopy(value.ReservedShares,compact); copy.ShortShares=ArrayCopy(value.ShortShares,compact);
        copy.ShortAveragePrice=ArrayCopy(value.ShortAveragePrice,compact); copy.ReservedCovers=ArrayCopy(value.ReservedCovers,compact);
        copy.Abilities=Scalar(value.Abilities);
        if(!compact)
        { copy.OpeningSnapshot=Scalar(value.OpeningSnapshot); copy.SeasonSnapshot=Scalar(value.SeasonSnapshot);
          copy.EquityHistory=new(value.EquityHistory); copy.SeasonRanks=new(value.SeasonRanks); }
        return copy;
    }
    static Stock Stock(Stock value)
    {
        var copy=Scalar(value); copy.History=new(value.History); copy.Report=Scalar(value.Report);
        copy.Reports=value.Reports.Select(Scalar).ToList(); return copy;
    }
    static SeasonResult Season(SeasonResult value)
    {
        var copy=Scalar(value); copy.Standings=new(value.Standings); copy.Days=value.Days.Select(Daily).ToList();
        copy.Start=value.Start is null ? null : Daily(value.Start); copy.End=value.End is null ? null : Daily(value.End);
        copy.Policy=value.Policy is null ? null : Scalar(value.Policy); copy.CompanyReports=value.CompanyReports.Select(Scalar).ToList(); return copy;
    }
    public static GameState Freeze(GameState value,bool forStorage=false)
    {
        var copy=Scalar(value); copy.SecurityIds=new(value.SecurityIds); copy.LegacyShares=(long[])value.LegacyShares.Clone();
        copy.RealEconomy=Scalar(value.RealEconomy); copy.CashFlows=new(value.CashFlows); copy.Journal=new(value.Journal);
        copy.SectorVolumes=new(value.SectorVolumes); copy.SectorTurnovers=new(value.SectorTurnovers);
        copy.CorporateEvents=value.CorporateEvents.Select(Scalar).ToList(); copy.Stocks=value.Stocks.Select(Stock).ToList();
        copy.Bots=value.Bots.Select(t=>Trader(t)).ToList(); copy.Retail=value.Retail.Select(t=>Trader(t,forStorage)).ToList();
        copy.CompanyVotes=value.CompanyVotes.Select(Vote).ToList(); copy.Bankruptcies=value.Bankruptcies.Select(Bankruptcy).ToList();
        copy.BankruptcyTotals=Scalar(value.BankruptcyTotals);
        copy.Orders=value.Orders.Where(o=>o.Remaining>0).Select(Scalar).ToList(); copy.News=value.News.Select(Scalar).ToList();
        copy.Tape=value.Tape.Select(Scalar).ToList(); copy.PendingSeasons=value.PendingSeasons.Select(Season).ToList();
        copy.Bank=Scalar(value.Bank); copy.Bank.ShareInventory=(long[])value.Bank.ShareInventory.Clone();
        copy.Bank.ReservedLending=(long[])value.Bank.ReservedLending.Clone();
        copy.Bank.MarketAccount=Trader(value.Bank.MarketAccount);
        copy.Government=Scalar(value.Government); copy.Government.Policy=Scalar(value.Government.Policy);
        copy.Government.History=value.Government.History.Select(Scalar).ToList(); copy.DailyHistory=value.DailyHistory.Select(Daily).ToList();
        copy.OpeningSnapshot=value.OpeningSnapshot is null ? null : Daily(value.OpeningSnapshot);
        copy.SeasonSnapshot=value.SeasonSnapshot is null ? null : Daily(value.SeasonSnapshot);
        copy.Operations=value.Operations.Select(Scalar).ToList(); return copy;
    }
}
