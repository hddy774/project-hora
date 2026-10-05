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
    void MakeBankMarket()
    {
        CancelOrders(0); var bank=BankAccount();
        State.Bank.MarketDecision=marketTrend<-.04 ? "하락 충격 완화·유동성 매수" : marketTrend>.06 ? "과열 완화·보유 주식 공급" : "가격·시간 우선 호가로 유동성 공급";
        for(int i=0;i<State.Stocks.Count;i++)
        {
            var s=State.Stocks[i]; if(!s.Active || s.WaitingForCapital) continue;
            bool falling=trends[i]<-.04,overvalued=s.Price>s.FairValue*1.35;
            bool hot=trends[i]>.06 || overvalued;
            long reserve=Math.Max(10,s.TotalShares/20);
            long available=State.Bank.ShareInventory[i]-State.Bank.ReservedLending[i]-reserve;
            int sellPrice=Quote(s.PreviousPrice*(overvalued ? .985 : hot ? 1.003 : 1.008),false);
            int sellQuantity=(int)Math.Min(Math.Max(0,available),hot ? 12 : 6);
            if(sellQuantity>0) SubmitOrder(0,i,false,sellPrice,sellQuantity,1);
            long budget=Math.Max(0,State.Bank.Cash-bank.ReservedCash-150_000_000);
            if(budget<s.PreviousPrice || s.Report.Equity<=0) continue;
            int buyPrice=Quote(s.PreviousPrice*(overvalued ? .965 : falling ? .999 : .990),true);
            int buyQuantity=(int)Math.Min(budget/Math.Max(1,buyPrice+ExchangeFee(buyPrice)),falling ? 8 : 2);
            if(buyQuantity>0) SubmitOrder(0,i,true,buyPrice,buyQuantity,1);
        }
    }
}
