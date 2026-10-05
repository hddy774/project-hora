using AlphaExchange.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

static class V16Checks
{
    public static void Run(Action<bool,string> check,Action<GameEngine> validate)
    {
        var rules=SimulationRules.Default();
        check(rules.Representative.InitialAbility==50 && rules.Representative.Weight==.5,"Equal 50-point representatives and half influence");
        check(rules.Employees.Grades.All(g=>g.Skill<50),"Every employee including S starts below a representative");
        var flexible=new GameEngine(167); flexible.State.Stocks[0].FairValue=flexible.State.Stocks[0].Price*1.5;
        foreach(var disposition in Enum.GetValues<Disposition>())
        {
            flexible.Owner(1).Disposition=disposition;
            check(flexible.Analyze(flexible.Owner(1),0).Horizon==InvestmentHorizon.Long,"Every disposition including aggressive can choose a profitable discounted long thesis");
        }
        foreach(string invalid in new[]{"UnknownRule","bad grade","negative cost","invalid horizon"})
        {
            var node=JsonNode.Parse(JsonSerializer.Serialize(rules))!.AsObject();
            switch(invalid)
            {
                case "UnknownRule": node[invalid]=true; break;
                case "bad grade": node["Employees"]!["Grades"]![0]!["Skill"]=60; break;
                case "negative cost": node["Employees"]!["Grades"]![0]!["MonthlySalary"]=-1; break;
                default: node["Horizons"]![2]!["MaximumHours"]=720; break;
            }
            try { SimulationRules.FromJson(node.ToJsonString()); check(false,"Invalid script rejected: "+invalid); }
            catch(Exception e) when(e is InvalidDataException or JsonException) { check(true,"Invalid script rejected: "+invalid); }
        }
        var g=new GameEngine(160);
        check(g.State.Bots.All(t=>t.Abilities.Values().All(a=>a==50) && t.Development!.PointsEarned==0 && t.Development.EmployeeCount==0),"All institutions start with equal judgement and zero earned points");
        check(g.State.Bots.All(t=>{ var exposure=g.Analyze(t,0).TargetExposure; return exposure>=.7 && exposure<=.95; }),"Every institution cash target is between five and thirty percent");
        check(g.HoldingHorizon(0)==InvestmentHorizon.UltraShort && g.HoldingHorizon(23)==InvestmentHorizon.UltraShort &&
            g.HoldingHorizon(24)==InvestmentHorizon.Short && g.HoldingHorizon(239)==InvestmentHorizon.Short &&
            g.HoldingHorizon(240)==InvestmentHorizon.Long && g.HoldingHorizon(720)==InvestmentHorizon.Long,"24/240-hour boundaries and no long maturity");
        var t=g.Owner(1); var before=g.Capabilities(t); long cash=g.SystemCash(),opening=t.Cash,economy=g.State.RealEconomy.Cash;
        check(g.HireEmployee(1,EmployeeGrade.S,AbilityKind.Valuation) is null,"An institution can fund an S researcher");
        var skills=g.Capabilities(t); long cost=g.Rules.Employees.Grades[0].HiringCost;
        check(t.Cash==opening-cost && g.State.RealEconomy.Cash==economy+cost && t.StaffCosts==cost && g.SystemCash()==cash,"Hiring cost moves actual cash and reconciles expense");
        check(skills.Effective.Valuation>before.Effective.Valuation && skills.Team.Valuation>skills.Team.Execution &&
            Math.Abs(skills.Effective.Valuation-(t.Abilities.Valuation+skills.Team.Valuation)*.5)<=1,"Role expertise and 50/50 capability composition are used");
        check(g.HireEmployee(1,EmployeeGrade.D,AbilityKind.Execution,2) is null,"Lower-grade staff can contribute by headcount");
        skills=g.Capabilities(t); check(skills.Effective.Execution>before.Effective.Execution && t.Development!.EmployeeCount==3,"Staff count increases effective institution execution");
        check(g.HireEmployee(1,EmployeeGrade.S,AbilityKind.Valuation,40) is not null,"Headcount and payroll budget are protected");
        validate(g);
        for(int hour=0;hour<48;hour++) g.AdvanceHour();
        check(t.StaffCosts>cost && g.State.CashFlows.GetValueOrDefault("staff-payroll")>0,"Daily actual payroll is recorded in persistent cash flows"); validate(g);
        check(g.State.Bots.All(b=>b.Development!.TargetCashRatio is >=.05 and <=.30),"Adaptive cash targets retain bounds after trading and hiring");
        int twoSided=0;
        for(int i=0;i<g.State.Stocks.Count;i++)
        {
            var bid=g.Depth(i,true); var ask=g.Depth(i,false);
            if(bid.Count>0 && ask.Count>0) { twoSided++; check(bid[0].Price<ask[0].Price,"Replenished book is uncrossed"); }
            foreach(bool buy in new[]{true,false})
            {
                var depth=g.Depth(i,buy);
                foreach(var level in depth)
                {
                    var orders=g.Orders(i,buy).Where(o=>o.Price==level.Price && o.Remaining>0).ToArray();
                    check(level.Quantity==orders.Sum(o=>(long)o.Remaining) && level.BankQuantity==orders.Where(o=>o.OwnerId==0).Sum(o=>(long)o.Remaining),"Visible depth contains actual reserved orders only");
                }
            }
        }
        check(twoSided>=25,"Normal liquid market displays both sides for most companies");
        var valuationBank=new GameEngine(166); valuationBank.State.CompletedHours=1;
        var hotStock=valuationBank.State.Stocks[0]; hotStock.FairValue=hotStock.Price*.5;
        valuationBank.PrepareMarketSignals(); valuationBank.ReplenishLiquidity(true);
        check(valuationBank.Depth(0,true)[0].Price<hotStock.Price*.97 && valuationBank.Depth(0,false)[0].Price<hotStock.Price*.99,"Funded bank discounts bids and supplies offers during valuation excess without forcing the trade price");
        validate(valuationBank);
        var patient=new GameEngine(165); var longInvestor=patient.Owner(1);
        longInvestor.Disposition=Disposition.Analytical;
        for(int i=0;i<patient.State.Stocks.Count;i++)
        {
            var stock=patient.State.Stocks[i];
            TestFixtures.BuyFromBank(patient,1,i,Math.Max(1,(int)(8_000_000L/patient.State.Stocks.Count/stock.Price)));
            stock.FairValue=stock.Price*1.4; stock.PreviousPrice=stock.Price; stock.History=Enumerable.Repeat((double)stock.Price,120).ToList();
        }
        patient.State.CompletedHours=1; patient.PrepareMarketSignals(); patient.DecideInstitution(longInvestor);
        var longDevelopment=longInvestor.Development!;
        check(longDevelopment.NextReviewHours.All(h=>h>=240) && longDevelopment.NextDecisionHour>=240,"Full portfolio review advances every held security including unselected order candidates: "+string.Join(",",longDevelopment.NextReviewHours));
        long reviews=longDevelopment.PortfolioReviews;
        patient.State.CompletedHours=2; patient.DecideInstitution(longInvestor);
        check(longDevelopment.PortfolioReviews==reviews,"Stable long portfolio does not repeat full analysis every hour");
        patient.State.Stocks[0].Sentiment=.1; patient.DecideInstitution(longInvestor);
        check(longDevelopment.PortfolioReviews==reviews+1,"Material news immediately overrides a long review schedule");
        patient.State.Stocks[0].Sentiment=0; patient.State.CompletedHours=721;
        patient.PrepareMarketSignals(); patient.DecideInstitution(longInvestor);
        check(longInvestor.Shares.All(q=>q>0) && patient.PositionTiming(longInvestor,0).Actual==InvestmentHorizon.Long,"Positive long theses can remain held over thirty days without forced maturity sale"); validate(patient);
        var empty=new GameEngine(161); empty.State.CompletedHours=1;
        for(int id=1;id<=10;id++) TestFixtures.BuyFromBank(empty,id,0,100);
        empty.ReplenishLiquidity();
        check(empty.State.Bank.ShareInventory[0]==0 && empty.Depth(0,false).Count==0 && empty.EmptyBookReason(0,false).Contains("공급"),"No invented asks when the bank has no physical stock");
        empty.CancelAllOrders(); empty.Rules.Liquidity.CashBuffer=empty.State.Bank.Cash; empty.ReplenishLiquidity();
        check(empty.Depth(0,true).Count==0 && empty.EmptyBookReason(0,true).Contains("현금"),"No invented bid when the bank must protect cash"); validate(empty);
        var rewards=new GameEngine(162); rewards.State.CompletedHours=720;
        int[] ranks=[1,2,3,5,10,20,30,40,50,51]; int[] points=[24,20,16,12,10,8,6,4,2,0];
        for(int i=0;i<ranks.Length;i++)
        {
            var bot=rewards.Owner(i+1); var reward=rewards.AwardSeasonGrowth(bot,1,ranks[i])!;
            check(reward.AbilityPoints==points[i] && bot.Development!.PointsEarned==points[i] &&
                bot.Development.PointsSpent==points[i] && bot.Abilities.Values().Sum()==500+points[i],"Relative tier grants one reward and accounts for every point");
            check(bot.Development!.AllocatedPoints.Sum()==points[i] && reward.AllocatedPoints.Sum()==points[i],"AI allocation is stored per ability");
            int oldCredit=bot.CreditScore; string exact=JsonSerializer.Serialize(bot.Development);
            check(rewards.AwardSeasonGrowth(bot,1,ranks[i]) is null && bot.CreditScore==oldCredit && JsonSerializer.Serialize(bot.Development)==exact,"A season cannot grant growth or credit twice");
        }
        var late=rewards.Owner(11); late.BirthHour=400;
        check(rewards.AwardSeasonGrowth(late,1,1)!.AbilityPoints==0,"A partial-season successor cannot earn a full-season reward");
        var capped=rewards.Owner(12); capped.Abilities=Abilities.Uniform(100);
        rewards.AwardSeasonGrowth(capped,1,1);
        check(capped.Development!.AvailablePoints==24 && capped.Development.PointsSpent==0 && capped.Abilities.Values().All(a=>a==100),"Maximum abilities preserve unused points");
        var restored=GameEngine.Deserialize(rewards.Serialize());
        check(restored.Owner(1).Development!.PointsEarned==24 && restored.Owner(1).Development!.CreditBonus==9 &&
            restored.AwardSeasonGrowth(restored.Owner(1),1,1) is null,"Growth and credit markers survive restore without regranting"); validate(restored);
        string folder=Path.Combine(Path.GetTempPath(),"hora-v16-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new GameStore(folder); store.Attach(g); string frozen=g.Serialize(); var frozenWorld=GameEngine.Deserialize(frozen); var prepared=store.PrepareSave(g);
            g.Owner(1).Development!.Employees[0].Count++; g.Owner(1).Development!.AllocatedPoints[0]++;
            g.Owner(1).Development!.LastNewsSignals[0]+=.1; g.Owner(1).Development!.PositionOpenedHours[0]=0;
            g.Rules.Liquidity.Spread+=.001;
            check(prepared.Json==frozen,"Frozen save owns every growth, roster, timing and rule field");
            store.WriteSnapshot(prepared);
            var loaded=store.Load(out var message)!;
            check(message.Length==0 && loaded.Serialize()==frozen,"Isolated full v7 state survives SQLite exactly"); validate(loaded);
            using var zip=new MemoryStream(); store.Export(zip); zip.Position=0;
            var zipStore=new GameStore(Path.Combine(folder,"import"));
            var imported=zipStore.Import(zip); check(imported.Serialize()==loaded.Serialize(),"Rules, staff and progress survive ZIP portability");
            for(int hour=0;hour<12;hour++) { loaded.AdvanceHour(); imported.AdvanceHour(); }
            check(loaded.Serialize()==imported.Serialize(),"Portfolio schedules and growth continue deterministically after import"); validate(imported);
            var old=JsonNode.Parse(frozen)!.AsObject(); old["Version"]=6; old.Remove("Rules"); old.Remove("PendingClockHours");
            foreach(var node in old["Bots"]!.AsArray()) { node!["Development"]=null; node["Abilities"]!["Valuation"]=73; }
            // Preserve actual recorded staff expense in this synthetic v6 shape;
            // the independent v1.5 checkpoint/native fixture has no new expenses.
            var migrated=GameEngine.Deserialize(old.ToJsonString());
            check(migrated.State.Version==7 && migrated.Owner(1).Cash==frozenWorld.Owner(1).Cash &&
                migrated.Owner(1).Shares.SequenceEqual(frozenWorld.Owner(1).Shares) && migrated.Owner(1).Abilities.Values().All(a=>a==50) &&
                migrated.Owner(1).Development!.LegacyAbilities[0]==73,"v6 shape preserves finance/ownership and records old abilities while starting fair growth"); validate(migrated);
            var month=new GameEngine(163); var monthStore=new GameStore(Path.Combine(folder,"month")); monthStore.Attach(month);
            for(int hour=0;hour<720;hour++) month.AdvanceHour();
            monthStore.Save(month); var season=monthStore.ReadSeason(month.State,1)!;
            check(season.GrowthRewards.Count==100 && season.GrowthRewards.Count(r=>r.AbilityPoints>0)==50 &&
                season.GrowthRewards.Where(r=>r.AbilityPoints>0).All(r=>r.Rank<=50),"Full relative reward archive records all 100 results and only top 50 payouts");
            var continuation=monthStore.Load(out message)!;
            check(continuation.State.Bots.All(b=>b.Development!.LastRewardSeason==1 &&
                JsonSerializer.Serialize(b.Development)==JsonSerializer.Serialize(month.Owner(b.Id).Development)) &&
                monthStore.ReadSeason(continuation.State,1)!.GrowthRewards.Count==100,"Archived reward and participant progression commit together"); validate(continuation);
            var clock=new GameEngine(164); int processed=clock.AdvanceFrame(.5,100);
            check(processed>0 && processed+clock.State.PendingClockHours==clock.Rules.Performance.MaximumCatchupHours,"Frame work is bounded and carries every accepted pending hour");
            var clockRestore=GameEngine.Deserialize(clock.Serialize());
            clock.AdvanceTime(0,100); clockRestore.AdvanceTime(0,100);
            check(clock.Serialize()==clockRestore.Serialize() && clock.State.CompletedHours==clock.Rules.Performance.MaximumCatchupHours,"Accepted clock backlog persists and drains exactly once"); validate(clockRestore);
        }
        finally { Directory.Delete(folder,true); }
        Console.WriteLine("PASS v1.6: equal growth, relative rewards, credit, funded employees, 50/50 capabilities, flexible horizons, real bilateral depth and v7 continuation");
    }
}
