namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    void CapitalizeMarket()
    {
        var stocks=State.Stocks; long target=State.World?.Rules?.MarketCapitalization ?? stocks.Sum(s=>(long)s.Price*1000);
        var shares=stocks.Select(s=>State.World?.Rules?.InitialFloatShares is >0 ? State.World.Rules.InitialFloatShares : target/stocks.Count/s.Price).ToArray();
        long remainder=target-stocks.Select((s,i)=>(long)s.Price*shares[i]).Sum(); bool resolved=remainder==0;
        for(int i=0;i<stocks.Count && !resolved;i++) for(int j=i+1;j<stocks.Count && !resolved;j++)
        {
            long a=stocks[i].Price,b=stocks[j].Price;
            static (long g,long x,long y) Egcd(long a,long b)
            { if(b==0) return (a,1,0); var (g,x,y)=Egcd(b,a%b); return (g,y,x-a/b*y); }
            var (g,x,y)=Egcd(a,b); if(remainder%g!=0) continue;
            long dx=x*(remainder/g),dy=y*(remainder/g);
            long shift=(long)Math.Round(-dx/(double)(b/g)); dx+=shift*(b/g); dy-=shift*(a/g);
            if(shares[i]+dx<1 || shares[j]+dy<1) continue;
            shares[i]+=dx; shares[j]+=dy; resolved=true;
        }
        if(!resolved) throw new InvalidDataException("초기 주식 수로 시가총액을 표현할 수 없습니다.");
        for(int i=0;i<stocks.Count;i++)
        { State.Bank.ShareInventory[i]=shares[i]; stocks[i].TreasuryShares=shares[i]; stocks[i].TotalShares=checked(shares[i]*2); }
        if(stocks.Sum(s=>s.MarketCap)!=target) throw new InvalidDataException("초기 시가총액 불일치");
    }
    void ConfigureNewCapital()
    {
        var w=State.World!.Rules!;
        State.Bank.Cash=w.BankCapital; State.Government.Cash=w.GovernmentCapital; State.RealEconomy.Cash=w.RealEconomyCapital;
        Rules.Liquidity.CashBuffer=w.BankCapital*3/10;
        Rules.Liquidity.MinimumQuantity=w.LiquidityMinimumQuantity; Rules.Liquidity.MaximumQuantity=w.LiquidityMaximumQuantity;
        Rules.Employees.Grades=Rules.Employees.Grades.Select(g=>g with { MonthlySalary=g.MonthlySalary*w.StaffCostScale,HiringCost=g.HiringCost*w.StaffCostScale }).ToArray();
    }
}
