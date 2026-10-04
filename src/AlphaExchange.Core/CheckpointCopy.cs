using System.Reflection;

namespace AlphaExchange.Core;

// Copy mutable state on the simulation thread; encode the isolated copy on the writer.
// Scalar fields and immutable strings/records can be shared. Every mutable collection
// and nested DTO reachable from a checkpoint is copied explicitly.
internal static class CheckpointCopy
{
    static readonly Func<object,object> Clone = typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic)!
        .CreateDelegate<Func<object,object>>();
    public static T Scalar<T>(T value) where T:class => (T)Clone(value);
    public static DailySnapshot Daily(DailySnapshot value)
    {
        var copy=Scalar(value); copy.CashFlows=new(value.CashFlows);
        copy.SecurityIds=(string[])value.SecurityIds.Clone(); copy.StockSectors=(string[])value.StockSectors.Clone();
        copy.StockShares=(long[])value.StockShares.Clone(); copy.FloatShares=(long[])value.FloatShares.Clone();
        copy.MarkPrices=(decimal[])value.MarkPrices.Clone(); copy.SplitFactors=(double[])value.SplitFactors.Clone();
        copy.StockVolumes=(long[])value.StockVolumes.Clone(); copy.StockTurnovers=(long[])value.StockTurnovers.Clone();
        copy.StockPrices=(int[])value.StockPrices.Clone(); copy.Institutions=value.Institutions.Select(Scalar).ToList(); return copy;
    }
    static Trader Trader(Trader value)
    {
        var copy=Scalar(value); copy.Shares=(long[])value.Shares.Clone(); copy.AverageCost=(double[])value.AverageCost.Clone();
        copy.ReservedShares=(long[])value.ReservedShares.Clone(); copy.ShortShares=(long[])value.ShortShares.Clone();
        copy.ShortAveragePrice=(double[])value.ShortAveragePrice.Clone(); copy.ReservedCovers=(long[])value.ReservedCovers.Clone();
        copy.Abilities=Scalar(value.Abilities); copy.OpeningSnapshot=Scalar(value.OpeningSnapshot); copy.SeasonSnapshot=Scalar(value.SeasonSnapshot);
        copy.EquityHistory=new(value.EquityHistory); copy.SeasonRanks=new(value.SeasonRanks); return copy;
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
    public static GameState Freeze(GameState value)
    {
        var copy=Scalar(value); copy.SecurityIds=new(value.SecurityIds); copy.LegacyShares=(long[])value.LegacyShares.Clone();
        copy.RealEconomy=Scalar(value.RealEconomy); copy.CashFlows=new(value.CashFlows); copy.Journal=new(value.Journal);
        copy.CorporateEvents=value.CorporateEvents.Select(Scalar).ToList(); copy.Stocks=value.Stocks.Select(Stock).ToList();
        copy.Bots=value.Bots.Select(Trader).ToList(); copy.Retail=value.Retail.Select(Trader).ToList();
        copy.Orders=value.Orders.Where(o=>o.Remaining>0).Select(Scalar).ToList(); copy.News=value.News.Select(Scalar).ToList();
        copy.Tape=value.Tape.Select(Scalar).ToList(); copy.PendingSeasons=value.PendingSeasons.Select(Season).ToList();
        copy.Bank=Scalar(value.Bank); copy.Bank.ShareInventory=(long[])value.Bank.ShareInventory.Clone();
        copy.Bank.ReservedLending=(long[])value.Bank.ReservedLending.Clone();
        copy.Government=Scalar(value.Government); copy.Government.Policy=Scalar(value.Government.Policy);
        copy.Government.History=value.Government.History.Select(Scalar).ToList(); copy.DailyHistory=value.DailyHistory.Select(Daily).ToList();
        copy.OpeningSnapshot=value.OpeningSnapshot is null ? null : Daily(value.OpeningSnapshot);
        copy.SeasonSnapshot=value.SeasonSnapshot is null ? null : Daily(value.SeasonSnapshot);
        copy.Operations=value.Operations.Select(Scalar).ToList(); return copy;
    }
}
