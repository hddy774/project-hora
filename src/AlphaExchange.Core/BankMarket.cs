namespace AlphaExchange.Core;

public readonly record struct RetailMarketReaction(double BuyProbability,double Aggression);
public sealed partial class GameEngine
{
    public RetailMarketReaction RetailReaction(Trader t,int index)
    {
        var s=State.Stocks[index]; int n=s.History.Count;
        double shortMove=s.PreviousPrice/s.History[Math.Max(0,n-3)]-1;
        double dayMove=s.PreviousPrice/s.History[Math.Max(0,n-24)]-1;
        double herd=Math.Clamp(shortMove*15+dayMove*7,-1,1);
        // Only observed prices and activity. No valuation, statements, dividends,
        // macro model, portfolio target or institutional risk management.
        return new(Math.Clamp(.5+herd*(.30+t.Risk*.12),.04,.96),.008+t.Risk*.008+Math.Abs(herd)*.014);
    }
    Trader BankAccount()
    {
        var t=State.Bank.MarketAccount;
        t.Id=0; t.IsRetail=false; t.Cash=State.Bank.Cash; t.Shares=State.Bank.ShareInventory;
        int count=State.Stocks.Count;
        if(t.AverageCost.Length!=count || t.ReservedShares.Length!=count || t.ShortShares.Length!=count || t.ReservedCovers.Length!=count)
        {
            t.AverageCost=Resize(t.AverageCost,count); t.ReservedShares=Resize(t.ReservedShares,count);
            t.ShortShares=new long[count]; t.ShortAveragePrice=new double[count]; t.ReservedCovers=new long[count];
        }
        return t;
    }
    void MakeBankMarket() => ReplenishLiquidity(true);
}
