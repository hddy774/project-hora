namespace AlphaExchange.Core;

public sealed partial class GameEngine
{
    public EconomyWorld World => State.World ?? throw new InvalidOperationException("인물 경제가 초기화되지 않았습니다.");
    public WorldRules WorldRules => World.Rules!;
    void InitializeWorld(bool migration=false)
    {
        State.World=new EconomyWorld { Rules=State.World?.Rules ?? AlphaExchange.Core.WorldRules.Default() };
        World.RetailUnitPeople=WorldRules.PeoplePerRetailUnit;
        var used=new HashSet<string>(StringComparer.Ordinal);
        for(int id=1;id<=200;id++)
        {
            var role=id<=100 ? PersonRole.Investor : id<=130 ? PersonRole.Executive : id<=180 ? PersonRole.Politician : PersonRole.Financier;
            string name=id<=100 ? Representatives.Name(Owner(id).PortraitId) : WorldRules.Surnames[(id-101)%WorldRules.Surnames.Length]+WorldRules.GivenNames[((id-101)/WorldRules.Surnames.Length+id)%WorldRules.GivenNames.Length];
            if(!used.Add(name)) { name+=" "+id; used.Add(name); }
            int organization=role==PersonRole.Investor ? id : role==PersonRole.Executive ? id-101 : 0;
            string office=role switch { PersonRole.Investor=>Owner(id).Name+" 대표",PersonRole.Executive=>State.Stocks[organization].Name+" 대표",
                PersonRole.Politician=>id==131 ? "국가 지도자" : id<140 ? "경제 부처 장관" : "경제 의회 의원",_=>id==181 ? "시장안정은행 총재" : "금융·통화 위원" };
            var person=new Person { Id=id,Name=name,Role=role,OrganizationId=organization,Office=office,Party=role==PersonRole.Politician ? (id-131)%2 : -1,
                PortraitId=id<=100 ? Owner(id).PortraitId : id,Cash=migration ? 0 : WorldRules.PersonalCapital,OpeningCash=migration ? 0 : WorldRules.PersonalCapital,
                Skills=Enumerable.Repeat(50,10).ToArray() };
            World.People.Add(person);
        }
        ScheduleElection(true); ScheduleElection(false);
        foreach(var stock in State.Stocks.Where(s=>s.Active)) StartBusinessProject(stock);
        if(!migration) Activity(131,0,"","constitution","200명 경제 의회 출범 · 양당 각 25명 · 기본 1표");
    }
    public Person Person(int id) => id is >=1 and <=200 ? World.People[id-1] : throw new ArgumentOutOfRangeException(nameof(id));
    public double PersonScore(Person p)
    {
        var w=WorldRules.CompositeWeights;
        double skill=p.Role==PersonRole.Investor ? Owner(p.OrganizationId).Abilities.Values().Average() : p.Skills.Average();
        double performance=p.Role==PersonRole.Investor ? Math.Clamp(50+Owner(p.OrganizationId).Return(State.Stocks)*100,0,100) : p.Performance;
        double finance=Math.Clamp(50+Math.Log2(Math.Max(1,p.Cash)/(double)Math.Max(1,p.OpeningCash))*10,0,100);
        return skill*w[0]+performance*w[1]+finance*w[2]+p.Reputation*w[3];
    }
    public List<PersonStanding> PeopleRanking(PersonRole? role=null)
        => World.People.Where(p=>role is null || p.Role==role).Select(p=>(p,score:PersonScore(p))).OrderByDescending(x=>x.score).ThenBy(x=>x.p.Id)
            .Select((x,i)=>new PersonStanding(x.p.Id,x.p.Role,i+1,x.score,x.p.VotingPower)).ToList();
    void AwardVotingPower(SeasonResult season)
    {
        if(World.LastRankingSeason>=season.Season) return;
        foreach(var row in PeopleRanking())
        {
            var p=Person(row.PersonId); p.LastOverallRank=row.Rank;
            p.VotingPower=Math.Clamp(11-(int)Math.Ceiling(row.Rank/20.0),1,10);
            season.PersonStandings.Add(row with { VotingPower=p.VotingPower });
        }
        World.LastRankingSeason=season.Season;
        Activity(World.LeaderId,0,"","voting-power",$"시즌 {season.Season} 종합 순위에 따라 200명의 다음 시즌 투표권 배분");
    }
    void ScheduleElection(bool government)
    {
        long next=State.CompletedHours+24*(WorldRules.MinimumElectionDays+(int)(Next()*(WorldRules.MaximumElectionDays-WorldRules.MinimumElectionDays+1)));
        if(government) World.NextGovernmentElectionHour=next; else World.NextGovernorElectionHour=next;
    }
    void HoldElection(bool government)
    {
        int incumbent=government ? World.LeaderId : World.GovernorId;
        var role=government ? PersonRole.Politician : PersonRole.Financier;
        var candidates=World.People.Where(p=>p.Role==role && p.Id!=incumbent).OrderByDescending(PersonScore).Take(4).ToArray();
        var vote=NewVote(government ? WorldVoteKind.GovernmentElection : WorldVoteKind.GovernorElection,incumbent,government ? "경제 정부 지도자 선거" : "은행 총재 선거");
        foreach(var voter in World.People)
        {
            var selected=candidates.MaxBy(p=>PersonScore(p)+(voter.Party>=0 && p.Party==voter.Party ? 8 : 0)+Next()*8)!;
            vote.Ballots.Add(new(voter.Id,voter.VotingPower,selected.Id));
        }
        vote.WinnerId=WinningCandidate(vote);
        WorldVoteRecorded?.Invoke(vote);
        if(government) { Person(incumbent).Office="경제 의회 의원"; World.LeaderId=vote.WinnerId; Person(vote.WinnerId).Office="국가 지도자"; }
        else { Person(incumbent).Office="금융·통화 위원"; World.GovernorId=vote.WinnerId; Person(vote.WinnerId).Office="시장안정은행 총재"; }
        Activity(vote.WinnerId,incumbent,"","election",$"{vote.Title} 당선 · {vote.Ballots.Where(b=>b.CandidateId==vote.WinnerId).Sum(b=>b.Weight)}표");
        ScheduleElection(government);
        if(government) VoteFundingLaw();
    }
    WorldVote NewVote(WorldVoteKind kind,int proposer,string title)
    {
        var vote=new WorldVote { Id=World.NextVoteId++,Minute=State.CompletedMinutes,Kind=kind,ProposerId=proposer,Title=title };
        Append(World.Votes,vote,32); return vote;
    }
    static int WinningCandidate(WorldVote vote) => vote.Ballots.GroupBy(b=>b.CandidateId).OrderByDescending(g=>g.Sum(b=>b.Weight)).ThenBy(g=>g.Key).First().Key;
    void VoteFundingLaw()
    {
        var vote=NewVote(WorldVoteKind.FundingLaw,World.LeaderId,"정책 후원·접촉 허용 법률");
        foreach(var p in World.People)
        {
            bool allow=p.Role==PersonRole.Investor ? Owner(p.Id).Integrity<.75 : p.Role==PersonRole.Executive ? State.Stocks[p.OrganizationId].CompanyStrategy==CompanyStrategy.Growth : p.Party==Person(World.LeaderId).Party;
            vote.Ballots.Add(new(p.Id,p.VotingPower,allow ? 1 : 0));
        }
        vote.WinnerId=WinningCandidate(vote); World.PoliticalFundingAllowed=vote.WinnerId==1;
        WorldVoteRecorded?.Invoke(vote);
        Activity(World.LeaderId,0,"","law",World.PoliticalFundingAllowed ? "경제 의회 투표: 등록 정책 후원 허용" : "경제 의회 투표: 정책 대가성 후원 금지");
    }
    void VoteEconomicPolicy()
    {
        CancelAllOrders(); var vote=NewVote(WorldVoteKind.EconomicPolicy,World.LeaderId,"시즌 경제 정책 표결");
        foreach(var p in World.People)
        {
            int mode=World.Inflation>.045 ? 2 : World.EconomicActivity<.98 ? 0 : 1;
            if(p.Role==PersonRole.Investor && Owner(p.Id).Return(State.Stocks)<0) mode=0;
            if(p.Role==PersonRole.Politician)
            {
                if(p.Party==0) mode=Math.Max(0,mode-1);
                bool funded=World.Activities.Any(a=>a.Kind=="funding" && a.RelatedPersonId==p.Id && a.Minute>=State.CompletedMinutes-30*1440);
                if(funded) mode=0;
            }
            vote.Ballots.Add(new(p.Id,p.VotingPower,mode));
        }
        int winner=WinningCandidate(vote); vote.WinnerId=winner; World.PolicyMode=winner;
        var rule=WorldRules.Policies[winner];
        State.Government.Policy=new GovernmentPolicy { Season=State.Season,Name=rule.Name,TaxRate=rule.TaxRate,FeeBasisPoints=rule.FeeBasisPoints,
            BaseRate=rule.BaseRate,SubsidyRate=rule.SubsidyRate,SpendingRatio=rule.SpendingRatio,Enforcement=rule.Enforcement,
            ShortSellingAllowed=rule.ShortSellingAllowed,ShortExposureLimit=winner==2 ? .08 : .15,LoanLimitMultiplier=winner==2 ? .8 : 1,
            DividendTaxRate=rule.TaxRate*.8 };
        Append(State.Government.History,State.Government.Policy,12); WorldVoteRecorded?.Invoke(vote); cachedStats=null;
        Activity(World.LeaderId,World.GovernorId,"","policy",$"{rule.Name} 의결 · 금리 {rule.BaseRate:P1} · 수수료 {rule.FeeBasisPoints}bp · 법인세 {rule.TaxRate:P0}");
    }
    public string? FundPolitician(int donorId,int recipientId,long amount)
    {
        if(donorId is <1 or >130 || recipientId is <131 or >180 || amount<=0) return "후원 대상이 올바르지 않습니다.";
        var donor=Person(donorId); var recipient=Person(recipientId);
        if(amount>donor.Cash || amount>Math.Max(1,donor.Cash*WorldRules.DonationRatio)) return "개인 후원 한도 또는 개인 현금이 부족합니다.";
        TransferCash(donor,recipient,amount,"personal-policy-funding"); donor.DonationsPaid+=amount; recipient.DonationsReceived+=amount;
        if(!World.PoliticalFundingAllowed)
        {
            long fine=Math.Min(donor.Cash,Math.Max(amount,(long)(donor.Cash*WorldRules.FundingFineRatio)));
            TransferCash(donor,State.Government,fine,"personal-funding-fine"); donor.FinesPaid+=fine; State.Government.Fines+=fine; donor.Reputation=Math.Max(0,donor.Reputation-5);
            Activity(World.LeaderId,donor.Id,"","funding-fine",$"{donor.Name}의 금지된 정책 후원 적발 · 개인 벌금 {KoreanNumber.Full(fine)}원",fine);
        }
        else Activity(donor.Id,recipient.Id,"","funding",$"{recipient.Name}에게 등록 정책 후원 · 투자 지원 정책 면담",amount);
        return null;
    }
    void ServicePeople()
    {
        foreach(var p in World.People)
        {
            ICashAccount source; long capital;
            if(p.Role==PersonRole.Investor) { var t=Owner(p.OrganizationId); source=t; capital=Math.Max(0,t.Equity(State.Stocks)); }
            else if(p.Role==PersonRole.Executive) { var s=State.Stocks[p.OrganizationId]; source=s.Report; capital=Math.Max(0,s.Report.Equity); }
            else if(p.Role==PersonRole.Politician) { source=State.Government; capital=State.Government.Cash/50; }
            else { source=State.Bank; capital=State.Bank.Cash/20; }
            long salary=Math.Min(source.Cash/100,Math.Max(0,(long)(capital*WorldRules.SalaryRatio/30)));
            if(source is Trader trader) { salary=Math.Min(salary,AvailableCash(trader)); trader.StaffCosts+=salary; }
            if(source is CompanyReport report) State.Stocks[p.OrganizationId].PendingManagementPay+=salary;
            if(source is GovernmentState govt) govt.Spending+=salary;
            if(source is BankState bank) bank.OperatingSpending+=salary;
            TransferCash(source,p,salary,"person-salary"); p.Earnings+=salary;
            if(p.Role==PersonRole.Investor) p.Skills=Owner(p.Id).Abilities.Values();
            if(p.Role==PersonRole.Politician) p.Performance=Math.Clamp(50+(World.EconomicActivity-1)*100-(World.Inflation-.02)*200,0,100);
            if(p.Role==PersonRole.Financier) p.Performance=Math.Clamp(60-Math.Abs(World.Inflation-.02)*300-State.Bank.LoanWriteOffs/(double)Math.Max(1,State.Bank.Cash)*100,0,100);
        }
        if(State.Day%5==0)
        {
            int id=1+(int)(Next()*130),recipient=131+(int)(Next()*50); var donor=Person(id);
            if(donor.Cash>1000 && (World.PoliticalFundingAllowed || donor.Reputation<55)) FundPolitician(id,recipient,Math.Max(1,(long)(donor.Cash*WorldRules.DonationRatio*.5)));
        }
    }
    void UpdateWorldDay()
    {
        if(World.NextGovernmentElectionHour<=State.CompletedHours) HoldElection(true);
        if(World.NextGovernorElectionHour<=State.CompletedHours) HoldElection(false);
        AdvanceBusinesses(); ServicePeople(); GrowRetailPopulation();
        double quality=World.Projects.Count==0 ? 1 : World.Projects.Average(p=>p.Quality);
        double creditCost=State.Government.Policy.BaseRate;
        World.EconomicActivity=Math.Clamp(World.EconomicActivity*.95+(.98+quality*.03-creditCost*.3)*.05,.65,1.25);
        World.Inflation=Math.Clamp(World.Inflation*.97+(.012+(World.EconomicActivity-.95)*.15-creditCost*.06)*.03,-.01,.12);
        State.RealEconomy.Demand=World.EconomicActivity;
        if(State.Day%5==0) Activity(World.GovernorId,World.LeaderId,"","macro",$"은행 경기 분석 공개 · 실물 수요 {World.EconomicActivity:P1} · 물가 {World.Inflation:P1}");
    }
    void GrowRetailPopulation()
    {
        int count=Math.Min(WorldRules.DailyRetailGrowth,WorldRules.MaximumRetailUnits-State.Retail.Count);
        for(int i=0;i<count;i++)
        {
            if(State.RealEconomy.Cash<WorldRules.RetailCapital) break;
            var t=new Trader { Id=AiCount+State.Retail.Count+1,IsRetail=true,Risk=.15+Next()*.8,Patience=.2+Next()*.6,BirthHour=State.CompletedHours };
            Endow(t,0); TransferCash(State.RealEconomy,t,WorldRules.RetailCapital,"retail-entry");
            t.OpeningCash=t.OpeningEquity=t.SeasonOpeningEquity=t.LastReturnEquity=t.Cash;
            State.Retail.Add(t); World.PopulationAdded++; cachedStats=null;
        }
    }
    public event Action<PersonActivity>? ActivityRecorded;
    public event Action<WorldVote>? WorldVoteRecorded;
    PersonActivity Activity(int actor,int other,string security,string kind,string detail,long amount=0,double impact=0)
    {
        var a=new PersonActivity(World.NextActivityId++,State.CompletedMinutes,actor,other,security,kind,detail,amount);
        Append(World.Activities,a,240); ActivityRecorded?.Invoke(a);
        int index=State.Stocks.FindIndex(s=>s.SecurityId==security);
        if(index>=0 && impact!=0) { State.Stocks[index].Sentiment=Math.Clamp(State.Stocks[index].Sentiment+impact,-.12,.12); ApplyNewsImpact(index,impact); }
        State.News.Insert(0,new MarketEvent { Season=State.Season,Day=State.Day,Hour=State.Hour,Minute=State.Minute,ActorId=actor,ActivityId=a.Id,
            StockIndex=index,SecurityId=security,Headline=$"{Person(actor).Name} · {detail}",Detail=$"실제 활동 #{a.Id} · {WorldRules.Roles[(int)Person(actor).Role].Name} · {Person(actor).Office}",Impact=impact });
        if(State.News.Count>40) State.News.RemoveRange(40,State.News.Count-40);
        return a;
    }
}
