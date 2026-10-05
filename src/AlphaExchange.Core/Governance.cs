namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    public static readonly string[] CompanyStrategyNames=["성장·연구", "가치·수익성", "방어·안정"];
    public static readonly string[] ManagementNames=["설비 확장", "비용 효율", "현금·부채 관리"];
    public static readonly string[] AllocationNames=["재투자", "자사주 확대", "현금 배당"];
    public event Action<CompanyVote>? VoteRecorded;
    public static string VoteOptionName(VoteKind kind,int option) => (kind switch
    { VoteKind.Strategy=>CompanyStrategyNames,VoteKind.Management=>ManagementNames,_=>AllocationNames })[option];

    public string? ProposeVote(string id,VoteKind kind,int option,int hours=72)
    {
        int index=SecurityIndex(id);
        if(index<0 || !State.Stocks[index].Active || !Enum.IsDefined(kind) || option is <0 or >2 || hours is <1 or >720)
            return "주총 안건이 올바르지 않습니다.";
        var stock=State.Stocks[index];
        foreach(var prior in State.CompanyVotes.Where(v=>v.SecurityId==id && v.Kind==kind && v.Status==VoteStatus.Open))
        { prior.Status=VoteStatus.Cancelled; prior.CloseHour=State.CompletedHours; RecordVote(prior); }
        State.CompanyVotes.Add(new CompanyVote { Id=State.NextVoteId++,SecurityId=id,CompanyName=stock.Name,
            OpenHour=State.CompletedHours,CloseHour=State.CompletedHours+hours,Kind=kind,Option=option });
        TrimVotes(); return null;
    }
    void TrimVotes()
    {
        int max=Math.Max(180,State.Stocks.Count*6);
        while(State.CompanyVotes.Count>max)
        { int i=State.CompanyVotes.FindIndex(v=>v.Status!=VoteStatus.Open); if(i<0) break; State.CompanyVotes.RemoveAt(i); }
    }
    void OpenSeasonVotes()
    {
        foreach(var stock in State.Stocks)
            OpenCompanyVotes(stock);
    }
    void OpenCompanyVotes(Stock stock)
    {
            if(!stock.Active || stock.WaitingForCapital) return;
            int strategy=stock.Report.NetIncome<0 || stock.Report.DebtRatio>.55 ? 2 : stock.Report.Margin>.15 ? 0 : 1;
            int management=stock.Report.DebtRatio>.35 || stock.Report.Cash<stock.BaseRevenue ? 2 : 1;
            int capital=stock.Report.Cash<stock.BaseRevenue*2 ? 0 : stock.TotalShares>0 && (double)stock.TreasuryShares/stock.TotalShares<stock.TreasuryTarget ? 1 : 2;
            ProposeVote(stock.SecurityId,VoteKind.Strategy,strategy);
            ProposeVote(stock.SecurityId,VoteKind.Management,management);
            ProposeVote(stock.SecurityId,VoteKind.Capital,capital);
    }
    void RecordVote(CompanyVote vote)
    { VoteRecorded?.Invoke(CheckpointCopy.Vote(vote)); cachedStats=null; }
    void UpdateGovernance()
    {
        foreach(var vote in State.CompanyVotes)
        {
            if(vote.Status!=VoteStatus.Open) continue;
            int index=SecurityIndex(vote.SecurityId);
            if(index<0 || !State.Stocks[index].Active)
            { vote.Status=VoteStatus.Cancelled; vote.CloseHour=State.CompletedHours; RecordVote(vote); continue; }
            if(vote.CloseHour<=State.CompletedHours) ResolveVote(vote,index);
        }
        if(State.CompletedHours%720==0) OpenSeasonVotes();
    }
    internal void ResolveVote(CompanyVote vote,int index)
    {
        if(vote.Status!=VoteStatus.Open || !State.Stocks[index].Active || vote.SecurityId!=State.Stocks[index].SecurityId) return;
        var stock=State.Stocks[index]; vote.EligibleShares=stock.OutstandingShares;
        vote.ForShares=vote.AgainstShares=vote.AbstainShares=0; vote.Ballots.Clear();
        void Add(int id,int generation,string name,long shares,VoteChoice choice)
        {
            if(shares==0) return;
            vote.Ballots.Add(new(id,generation,name,shares,choice));
            if(choice==VoteChoice.For) vote.ForShares+=shares;
            else if(choice==VoteChoice.Against) vote.AgainstShares+=shares;
            else vote.AbstainShares+=shares;
        }
        foreach(var t in State.Bots) if(t.Shares[index]>0)
            Add(t.Id,t.Generation,t.Name,t.Shares[index],InstitutionVote(t,stock,vote));
        long retailFor=0,retailAgainst=0,retailAbstain=0;
        foreach(var t in State.Retail)
        {
            long shares=t.Shares[index]; if(shares==0) continue;
            double response=RetailReaction(t,index).BuyProbability;
            bool optimistic=response>=.5;
            bool supports=vote.Kind switch
            { VoteKind.Strategy=>vote.Option==(optimistic ? 0 : 2),VoteKind.Management=>vote.Option==(optimistic ? 0 : 2),_=>vote.Option==(optimistic ? 1 : 2) };
            if(Math.Abs(response-.5)<.015) retailAbstain+=shares;
            else if(supports) retailFor+=shares; else retailAgainst+=shares;
        }
        Add(-1,0,"개인 찬성 합계",retailFor,VoteChoice.For);
        Add(-1,0,"개인 반대 합계",retailAgainst,VoteChoice.Against);
        Add(-1,0,"개인 기권 합계",retailAbstain,VoteChoice.Abstain);
        Add(0,1,"시장안정은행",State.Bank.ShareInventory[index],BankVote(stock,vote));
        Add(-2,stock.Generation,"창업자",stock.FounderShares,VoteChoice.For);
        if(vote.ForShares+vote.AgainstShares+vote.AbstainShares!=vote.EligibleShares)
            throw new InvalidDataException("주총 의결권과 실제 상장 주식 수가 다릅니다.");
        long cast=vote.ForShares+vote.AgainstShares;
        bool quorum=cast*3>=vote.EligibleShares;
        bool passed=vote.EligibleShares>0 && quorum && vote.ForShares>vote.AgainstShares && vote.ForShares*4>=vote.EligibleShares;
        vote.Status=passed ? VoteStatus.Passed : VoteStatus.Rejected; vote.CloseHour=State.CompletedHours;
        if(passed)
        {
            if(vote.Kind==VoteKind.Strategy) stock.CompanyStrategy=(CompanyStrategy)vote.Option;
            else if(vote.Kind==VoteKind.Management) stock.Management=(ManagementPolicy)vote.Option;
            else stock.Allocation=(CapitalAllocation)vote.Option;
        }
        RecordVote(vote);
    }
    VoteChoice InstitutionVote(Trader t,Stock stock,CompanyVote vote)
    {
        var a=Capabilities(t).Effective;
        bool risk=stock.Report.DebtRatio>.5 || stock.Report.Equity<=0;
        int wanted=vote.Kind switch
        {
            VoteKind.Strategy=>risk && a.RiskManagement>=40 ? 2 : t.Disposition==Disposition.Aggressive && stock.Report.Margin>.1 ? 0 : 1,
            VoteKind.Management=>risk ? 2 : a.Valuation>=45 ? 1 : 0,
            _=>risk ? 0 : t.Disposition==Disposition.Cautious ? 2 : stock.Price<stock.FairValue && a.Valuation>=50 ? 1 : 0
        };
        return vote.Option==wanted ? VoteChoice.For : VoteChoice.Against;
    }
    VoteChoice BankVote(Stock stock,CompanyVote vote)
    {
        bool risk=stock.Report.DebtRatio>.5 || stock.Report.Equity<=0 || State.Bank.Cash<150_000_000;
        int wanted=vote.Kind switch
        {
            VoteKind.Strategy=>risk || marketTrend<-.04 ? 2 : State.RealEconomy.Demand>1.04 && State.Government.Policy.BaseRate<.03 ? 0 : 1,
            VoteKind.Management=>risk ? 2 : 1,
            _=>risk ? 0 : stock.TotalShares>0 && (double)stock.TreasuryShares/stock.TotalShares>=.75 ? 2 : 1
        };
        return vote.Option==wanted ? VoteChoice.For : VoteChoice.Against;
    }
    void ApplyCompanyPolicies(Stock stock,out double revenue,out double cost,out double investment,out double payout,out double reserve)
    {
        (revenue,cost,investment,reserve)=stock.CompanyStrategy switch
        { CompanyStrategy.Growth=>(1.10,1.06,.35,1.2),CompanyStrategy.Defensive=>(.93,.85,.10,2.0),_=>(1.02,.97,.20,1.5) };
        if(stock.Management==ManagementPolicy.Expansion) { revenue*=1.03; cost*=1.02; investment+=.10; }
        if(stock.Management==ManagementPolicy.Efficiency) cost*=.94;
        if(stock.Management==ManagementPolicy.Liquidity) { investment*=.5; reserve=Math.Max(2,reserve); }
        payout=stock.Allocation switch { CapitalAllocation.Dividend=>.45,CapitalAllocation.Buyback=>.10,_=>.08 };
        if(stock.Allocation==CapitalAllocation.Reinvestment) investment+=.15;
    }
    void SeekTreasuryOwnership(Stock stock)
    {
        if(!stock.Active || stock.TotalShares<=0 || stock.Report.Equity<=0 || (double)stock.TreasuryShares/stock.TotalShares>=stock.TreasuryTarget) return;
        ApplyCompanyPolicies(stock,out _,out _,out _,out _,out double reserve);
        long free=Math.Max(0,stock.Report.Cash-(long)(stock.BaseRevenue*reserve));
        long budget=(long)(free*(stock.Allocation==CapitalAllocation.Buyback ? .08 : .015));
        long need=Math.Max(0,(long)Math.Ceiling(stock.TotalShares*stock.TreasuryTarget)-stock.TreasuryShares);
        long quantity=Math.Min(need,budget/Math.Max(1,stock.Price)); if(quantity==0) return;
        int index=SecurityIndex(stock.SecurityId); CancelOrders(0);
        long bankShares=Math.Max(0,State.Bank.ShareInventory[index]-State.Bank.ReservedLending[index]-Math.Max(10,stock.TotalShares/20));
        long fromBank=Math.Min(quantity,bankShares);
        if(fromBank>0 && Buyback(stock.SecurityId,0,fromBank) is null) quantity-=fromBank;
        if(quantity==0) return;
        // Voluntary board tender: obtain real holders' stock with company cash.
        foreach(var t in State.Bots)
        {
            if(quantity==0) break;
            long offered=Math.Min(quantity,Math.Max(0,t.Shares[index]-t.ReservedShares[index]));
            if(offered>0 && t.AverageCost[index]<=stock.Price && Buyback(stock.SecurityId,t.Id,offered) is null) quantity-=offered;
        }
    }
}
