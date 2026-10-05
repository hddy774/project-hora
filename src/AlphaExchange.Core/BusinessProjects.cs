namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    void StartBusinessProject(Stock stock)
    {
        int index=State.Stocks.IndexOf(stock); if(index is <0 or >=30 || !stock.Active) return;
        var manager=Person(101+index);
        var project=new BusinessProject { SecurityId=stock.SecurityId,StartHour=State.CompletedHours,
            DurationDays=WorldRules.BusinessDurationDays[0]+(int)(Next()*(WorldRules.BusinessDurationDays[1]-WorldRules.BusinessDurationDays[0]+1)),
            Budget=Math.Max(0,(long)(stock.Report.Cash*WorldRules.BusinessBudgetRatio)),SalesTarget=stock.BaseRevenue,
            Quality=Math.Clamp(.7+manager.Skills.Average()/100*.5,.5,1.3),Detail=stock.Business+" · "+WorldRules.BusinessStages[0] };
        World.Projects.RemoveAll(p=>p.SecurityId==stock.SecurityId); World.Projects.Add(project);
        Activity(manager.Id,0,stock.SecurityId,"business-start",$"{stock.Name} {stock.Business} 사업 착수 · 공개 사업 방향 발표");
    }
    void AdvanceBusinesses()
    {
        foreach(var stock in State.Stocks.Where(s=>s.Active).ToArray())
        {
            var project=World.Projects.Find(p=>p.SecurityId==stock.SecurityId);
            if(project is null || project.Completed) { StartBusinessProject(stock); continue; }
            int index=State.Stocks.IndexOf(stock); if(index>=30) continue;
            var manager=Person(101+index); int age=(int)((State.CompletedHours-project.StartHour)/24);
            long scheduled=project.Budget*Math.Min(age,project.DurationDays)/project.DurationDays;
            long pay=Math.Min(Math.Max(0,stock.Report.Cash-stock.BaseRevenue/4),Math.Max(0,scheduled-project.Paid));
            TransferCash(stock.Report,State.RealEconomy,pay,"business-project-investment"); stock.Report.FixedAssets+=pay;
            stock.PendingInvestingFlow-=pay; State.RealEconomy.Investment+=pay; project.Paid+=pay;
            int stage=Math.Min(2,age*3/project.DurationDays);
            project.Detail=$"{stock.Business} · {WorldRules.BusinessStages[stage]} · {age}/{project.DurationDays}일";
            if(stage!=project.Stage)
            {
                project.Stage=stage;
                Activity(manager.Id,0,stock.SecurityId,"business-stage",$"{stock.Name} {WorldRules.BusinessStages[stage]} 단계 진행 공시",impact:.003*(project.Quality-1));
            }
            if(age<project.DurationDays) continue;
            project.Completed=true; double funded=project.Budget==0 ? 0 : (double)project.Paid/project.Budget;
            project.Quality=Math.Clamp(project.Quality*funded*World.EconomicActivity,.35,1.4);
            stock.BusinessSalesFactor=Math.Clamp(.75+project.Quality*.25,.7,1.2);
            manager.Performance=Math.Clamp(50+(project.Quality-1)*100,0,100);
            manager.Reputation=Math.Clamp(manager.Reputation+(project.Quality-1)*4,0,100);
            int weakest=Array.IndexOf(manager.Skills,manager.Skills.Min()); manager.Skills[weakest]=Math.Min(100,manager.Skills[weakest]+1);
            Activity(manager.Id,0,stock.SecurityId,"business-delivery",$"{stock.Name} 납품·사업 검증 {(project.Quality>=.95 ? "완료" : "지연·수익성 점검")} · 월 결산에 반영",impact:Math.Clamp((project.Quality-1)*.04,-.025,.025));
        }
    }
    public bool CanViewBusiness(int investorId,int stockIndex)
    {
        if(investorId is <1 or >100 || stockIndex<0 || stockIndex>=State.Stocks.Count) return false;
        var stock=State.Stocks[stockIndex]; long held=Owner(investorId).Shares[stockIndex]; if(held==0) return false;
        int ahead=0;
        if(stock.TreasuryShares>=held && stock.TreasuryShares>0) ahead++;
        if(State.Bank.ShareInventory[stockIndex]>=held && State.Bank.ShareInventory[stockIndex]>0) ahead++;
        if(stock.FounderShares>=held && stock.FounderShares>0) ahead++;
        foreach(var t in Participants) if(t.Id!=investorId && (t.Shares[stockIndex]>held || t.Shares[stockIndex]==held && t.Id<investorId)) ahead++;
        return ahead<3;
    }
    public BusinessProject? PrivateBusiness(int investorId,int stockIndex)
        => CanViewBusiness(investorId,stockIndex) ? World.Projects.Find(p=>p.SecurityId==State.Stocks[stockIndex].SecurityId) : null;
}
