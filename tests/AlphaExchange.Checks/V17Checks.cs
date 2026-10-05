using AlphaExchange.Core;
using System.Text.Json;
using System.Diagnostics;

static class V17Checks
{
    public static void Run(Action<bool,string> check,Action<GameEngine> validate)
    {
        var g=new GameEngine(170); var s=g.State; var w=g.World;
        check(s.Version==8 && s.Stocks.Sum(x=>x.MarketCap)==1_000_000_000_000,"Exact initial trillion capitalization without repricing");
        check(s.Bots.All(t=>t.Cash==1_000_000_000) && s.Retail.Count==1000,"100 funded billion-won corporations and 1000 initial retail cohorts");
        check(s.Stocks.Select((x,i)=>x.TreasuryShares*2==x.TotalShares && s.Bank.ShareInventory[i]==x.TreasuryShares).All(x=>x),"Initial physical treasury/bank ownership 50/50");
        check(w.People.Count==200 && Enum.GetValues<PersonRole>().Select(r=>w.People.Count(p=>p.Role==r)).SequenceEqual(new[]{100,30,50,20}),"200 people with exact role counts");
        check(w.People.Select(p=>p.Name).Distinct().Count()==200 && w.People.Select(p=>p.PortraitId).Distinct().Count()==200,"Unique identities and original art IDs");
        check(w.People.Where(p=>p.Role==PersonRole.Politician).GroupBy(p=>p.Party).All(p=>p.Count()==25),"Two equal economic parties");
        long cash=g.SystemCash(); validate(g);
        for(int minute=0;minute<37;minute++) g.AdvanceMinute();
        check(s.CompletedMinutes==37 && s.CompletedHours==0 && s.Tape.Any(t=>t.Minute!=0),"Executions occur throughout a partial hour");
        check(s.News.All(n=>n.ActorId>0 && n.ActivityId>0),"Every new headline refers to an actual person's activity");
        check(g.SystemCash()==cash,"Minute execution conserves all corporate/personal/public cash"); validate(g);
        var copy=GameEngine.Deserialize(g.Serialize());
        for(int minute=0;minute<143;minute++) { g.AdvanceMinute(); copy.AdvanceMinute(); }
        check(g.Serialize()==copy.Serialize(),"Partial-minute actor schedule and random continuation survive reload exactly");
        var t=g.Owner(1);
        foreach(var job in Enum.GetValues<StaffJob>()) check(g.HireSpecialist(1,EmployeeGrade.D,job) is null,"Funded diversified staff job "+job);
        g.AdvanceHour();
        check(t.Development!.Employees.Select(e=>e.Job).Distinct().Count()==8,"Eight professional roles remain separate contracts");
        check(g.ConfidentialReports(2,1).Count==0 && g.ConfidentialPlans(2,1).Count==0,"Competing institutions cannot read private reports/plans");
        check(g.ConfidentialReports(1,1).Count==6 && g.ConfidentialReports(1,1).All(r=>r.OwnerId==1 && r.Confidence>0),"Owner's six sector reports drive investment research");
        check(g.ConfidentialPlans(1,1).Count==30 && g.ConfidentialPlans(1,1).All(p=>p.LowerPrice>0 && p.UpperPrice>=p.LowerPrice && p.DurationMinutes>=60),"Every security has a bounded dated investment plan");
        check(g.ConfidentialPlans(1,1).All(p=>p.Horizon switch { InvestmentHorizon.UltraShort=>p.DurationMinutes<1440,InvestmentHorizon.Short=>p.DurationMinutes is >=1440 and <14400,_=>p.DurationMinutes>=14400 }),"Plans respect ultra/short/long boundaries without forced maturity selling");
        w.PoliticalFundingAllowed=true;
        var before=t.Cash; long donorCash=g.Person(1).Cash,recipientCash=g.Person(131).Cash;
        check(g.FundPolitician(1,131,1000) is null && t.Cash==before && g.Person(1).Cash==donorCash-1000 && g.Person(131).Cash==recipientCash+1000,"Registered policy donation uses personal funds only");
        w.PoliticalFundingAllowed=false; before=t.Cash; long fines=g.Person(1).FinesPaid;
        check(g.FundPolitician(1,132,1000) is null && t.Cash==before && g.Person(1).FinesPaid>fines,"Current voted law controls funded personal fines");
        // Access counts all issued-share owners, not just institutions.
        var access=new GameEngine(174); TestFixtures.BuyFromBank(access,1,0,50); TestFixtures.BuyFromBank(access,2,0,40);
        check(access.CanViewBusiness(1,0) && !access.CanViewBusiness(2,0) && access.PrivateBusiness(2,0) is null,"Only top three actual holders can view internal business");
        int initialRetail=s.Retail.Count,leader=w.LeaderId,governor=w.GovernorId;
        string folder=Path.Combine(Path.GetTempPath(),"hora-v17-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new GameStore(folder); store.Attach(g); store.Save(g);
            while(s.CompletedHours<720) { g.AdvanceHour(); if(s.CompletedHours%120==0) { validate(g); store.Save(g); } }
            check(s.Retail.Count>initialRetail && s.Retail.Count<=10000 && s.CashFlows["retail-entry"]>0,"Retail population grows from real economy funding");
            check(w.LeaderId!=leader && w.GovernorId!=governor,"Government leader and bank governor change by 10–30 day elections");
            check(w.Votes.Any(v=>v.Kind==WorldVoteKind.EconomicPolicy) && w.Votes.All(v=>v.Ballots.Count==200 && v.Ballots.Select(b=>b.PersonId).Distinct().Count()==200),"All policy/law/election decisions retain every weighted ballot");
            check(w.Projects.Any(p=>p.Paid>0) && s.CashFlows["business-project-investment"]>0 && s.RealEconomy.CorporateSales>0,"Real business investment, delivery and monthly revenues circulate actual funds");
            check(s.Bots.All(b=>b.Development!.LastRewardPoints is >=1 and <=10) && s.Bots.Count(b=>b.Development!.LastRewardRank>=51 && b.Development.LastRewardPoints==1)==50,"Every ranked investor receives 1–10 points, including ranks 51–100");
            check(w.People.All(p=>p.VotingPower is >=1 and <=10) && w.LastRankingSeason==1,"Season overall rankings determine all voting powers");
            check(g.PeopleRanking().Count==200 && Enum.GetValues<PersonRole>().All(r=>g.PeopleRanking(r).Take(3).Count()==3),"Overall and all domain podiums are complete");
            store.Save(g); var load=store.Load(out var message)!;
            check(message.Length==0 && load.Serialize()==g.Serialize(),"All world/personal/corporate/analyst/minute state survives SQLite exactly");
            check(store.ReadActivities(s,1000).Count>w.Activities.Count && store.ReadWorldVotes(s).Count==w.Votes.Count,"Trimmed activity history and complete ballots persist outside display buffers");
            var frozen=store.PrepareSave(g); string encoded=frozen.Json;
            g.Person(1).Skills[0]++; t.Development!.Plans[0].UpperPrice++;
            check(frozen.Json==encoded,"Checkpoint owns profile skills and private plan objects");
            using var zip=new MemoryStream(); store.Export(zip); zip.Position=0;
            var imports=new GameStore(Path.Combine(folder,"imports")); var imported=imports.Import(zip);
            check(imported.Serialize()==load.Serialize(),"ZIP contains full character, world, private reports and all history tables");
            for(int i=0;i<19;i++) { imported.AdvanceMinute(); load.AdvanceMinute(); }
            check(imported.Serialize()==load.Serialize(),"Portable world continues with identical minute decisions");
            validate(imported);
        }
        finally { Directory.Delete(folder,true); }
        check(KoreanNumber.Compact(1_000_000_000_000)=="1조" && KoreanNumber.Compact(100_000_000)=="1억" && KoreanNumber.Compact(10000)=="1만" && KoreanNumber.Full(long.MinValue).StartsWith("-9,223"),"Exact and abbreviated Korean units handle trillion and signed extremes");
        var clock=new GameEngine(179); clock.AdvanceFrame(.5,100);
        check(clock.State.CompletedMinutes+clock.State.PendingClockMinutes==600,"100x accepts all 600 minutes per half second without dropping time");
        var restored=GameEngine.Deserialize(clock.Serialize()); clock.AdvanceTime(0,100); restored.AdvanceTime(0,100);
        check(clock.Serialize()==restored.Serialize() && clock.State.CompletedMinutes==600,"Minute backlog survives save and drains once");
        Console.WriteLine("PASS v1.7: trillion market, funded personal wallets, 200 roles, minute matching, confidential plans, staff professions, businesses, elections, 100-rank rewards and portable history");
    }
}
