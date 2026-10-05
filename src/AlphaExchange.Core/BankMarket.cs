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
        // Follow the tape and react to one's own visible paper gain. Simple
        // profit taking supplies stock during rallies instead of every holder
        // chasing the same positive tick forever. No fundamental/private data.
        double gain=t.Shares[index]>0 && t.AverageCost[index]>0 ?
            Math.Clamp((s.PreviousPrice/t.AverageCost[index]-1)/WorldRules.RetailProfitTakingReturn,0,1) : 0;
        double probability=.5+herd*(WorldRules.RetailHerdWeight+t.Risk*.08)-gain*WorldRules.RetailProfitTakingWeight;
        double aggression=Math.Max(.008+t.Risk*.008+Math.Abs(herd)*.014,gain*WorldRules.RetailProfitTakingAggression);
        return new(Math.Clamp(probability,.04,.96),aggression);
    }
    internal int RetailLimitPrice(int index,bool buy,double aggression)
    {
        var book=buy ? asks[index] : bids[index];
        // A naive market participant can accept an observed executable quote.
        // Guessing every limit from the previous print leaves both sides apart
        // after a valuation correction, even while funded liquidity is present.
        if(book.Count>0 && Next()<WorldRules.RetailTakeLiquidityProbability) return book[0].Price;
        return Quote(State.Stocks[index].PreviousPrice*(1+(buy ? aggression : -aggression)),buy);
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
