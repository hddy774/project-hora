namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    // Replenish actual orders after the actors/finance/actions. Every displayed
    // level owns bank inventory/cash and passes the same matching/reservation path.
    internal void ReplenishLiquidity(bool refresh=false)
    {
        if(refresh) CancelOrders(0);
        var bank=BankAccount(); var r=Rules.Liquidity;
        State.Bank.MarketDecision=marketTrend<Rules.Investment.FallingMarket ? "양방향 호가 · 하락 충격 완화" :
            marketTrend> -Rules.Investment.FallingMarket ? "양방향 호가 · 과열 공급" : "실재고·현금으로 양방향 유동성 보충";
        for(int i=0;i<State.Stocks.Count;i++)
        {
            var s=State.Stocks[i]; if(!s.Active || s.WaitingForCapital || s.Report.Equity<=0) continue;
            bool needAsk=refresh || asks[i].Count==0,needBid=refresh || bids[i].Count==0;
            if(!needAsk && !needBid) continue;
            double inventory=(double)State.Bank.ShareInventory[i]/Math.Max(1,s.TotalShares);
            double skew=Math.Clamp((inventory-r.InventoryTarget)*r.InventorySkew,-r.InventorySkew,r.InventorySkew);
            bool overvalued=s.Price>s.FairValue*r.OvervaluationRatio;
            bool falling=i<trends.Length && trends[i]<Rules.Investment.FallingMarket;
            int baseBid=Quote(s.Price*(1-(overvalued ? r.OvervaluedBidDiscount : falling ? r.FallingBidSpread : r.Spread)-skew),true);
            int baseAsk=Quote(s.Price*(overvalued ? 1-r.OvervaluedAskDiscount : 1+r.Spread-skew),false);
            int tick=s.Price<1000 ? 1 : 10;
            // An overpriced market may receive executable funded supply. Normal
            // liquidity remains passive; the bank never bids up a speculative peak.
            if(!overvalued) baseAsk=Math.Clamp(Math.Max(baseAsk,(bids[i].Count==0 ? baseBid : bids[i][0].Price)+tick),1,10_000_000);
            baseBid=Math.Clamp(Math.Min(baseBid,(asks[i].Count==0 ? baseAsk : asks[i][0].Price)-tick),1,10_000_000);
            if(baseBid>=baseAsk) continue;
            int quantity=(int)Math.Clamp(Math.Ceiling(s.Volume*r.RecentVolumeWeight/r.Levels),r.MinimumQuantity,r.MaximumQuantity);
            for(int level=0;level<r.Levels;level++)
            {
                if(needAsk)
                {
                    int price=Quote(baseAsk*(1+level*r.Spread),false);
                    long available=State.Bank.ShareInventory[i]-State.Bank.ReservedLending[i]-bank.ReservedShares[i];
                    int q=(int)Math.Min(Math.Max(0,available),quantity);
                    if(q>0) SubmitOrder(0,i,false,price,q,1);
                }
                if(needBid)
                {
                    int price=Quote(baseBid*(1-level*r.Spread),true);
                    long budget=Math.Max(0,State.Bank.Cash-bank.ReservedCash-r.CashBuffer);
                    int q=(int)Math.Min(quantity,budget/(price+ExchangeFee(price)));
                    if(q>0) SubmitOrder(0,i,true,price,q,1);
                }
            }
        }
    }
    public string EmptyBookReason(int index,bool buy)
    {
        var s=State.Stocks[index];
        if(!s.Active || s.WaitingForCapital) return "거래 중단";
        if(State.CompletedHours==0) return "첫 시간 호가 준비 중";
        if(s.Report.Equity<=0) return "기업 지급 불능";
        if(buy && State.Bank.Cash-State.Bank.MarketAccount.ReservedCash<=Rules.Liquidity.CashBuffer) return "은행 현금 완충 보호";
        if(!buy && State.Bank.ShareInventory[index]-State.Bank.ReservedLending[index]-State.Bank.MarketAccount.ReservedShares[index]<=0) return "공급 가능 주식 없음";
        return "현재 미체결 주문 없음";
    }
}
