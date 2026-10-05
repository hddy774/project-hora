using AlphaExchange.Core;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

static class TestFixtures
{
    public static void BuyFromBank(GameEngine game,int owner,int stock,int quantity)
    {
        int price=game.State.Stocks[stock].Price;
        if(game.SubmitOrder(0,stock,false,price,quantity) is not null || game.SubmitOrder(owner,stock,true,price,quantity) is not null)
            throw new Exception("Fixture must acquire actual bank inventory with real cash");
    }
    public static void ClearLegacyTreasury(GameEngine game)
    {
        game.CancelAllOrders();
        foreach(var s in game.State.Stocks) { s.TreasuryShares=s.FounderShares=0; }
        game.State.CompanyVotes.Clear(); game.State.NextVoteId=1;
    }
}
static class V15Checks
{
    public static void Run(Action<bool,string> check,Action<GameEngine> validate)
    {
        var g=new GameEngine(150);
        check(g.State.Stocks.All(s=>s.TotalShares==2000 && s.TreasuryShares==1000 && s.FounderShares==0) && g.State.Bank.ShareInventory.All(q=>q==1000),"New market treasury/bank 50/50");
        check(g.Participants.All(t=>t.Shares.All(q=>q==0)),"Initial investors must acquire stock with cash");
        var retail=g.Owner(101); var reaction=g.RetailReaction(retail,0);
        g.State.Stocks[0].FairValue*=100; g.State.Stocks[0].Report.Revenue*=10; retail.Abilities.Valuation=100;
        check(g.RetailReaction(retail,0)==reaction,"Retail decisions do not use fundamentals or ability");
        g.State.Stocks[0].History=[60000,55000,52400];
        check(g.RetailReaction(retail,0).BuyProbability<reaction.BuyProbability,"Retail follows observed price declines");
        g=new GameEngine(151);
        int cheap=g.State.Stocks.FindIndex(s=>s.Symbol=="CLUD"); var company=g.State.Stocks[cheap];
        TestFixtures.BuyFromBank(g,1,cheap,600);
        var investor=g.Owner(1); investor.Disposition=Disposition.Analytical;
        long release=g.State.Bank.Cash-100_000_000; g.State.Bank.Cash-=release; g.State.RealEconomy.Cash+=release;
        check(g.ProposeVote(company.SecurityId,VoteKind.Strategy,1) is null,"Shareholder strategy proposal");
        var vote=g.State.CompanyVotes.Last(); g.ResolveVote(vote,cheap);
        check(vote.Status==VoteStatus.Passed && vote.ForShares==600 && vote.AgainstShares==400 && vote.EligibleShares==1000,"Majority is weighted by actual shares; treasury excluded");
        check(company.CompanyStrategy==CompanyStrategy.Value && vote.Ballots.Sum(b=>b.Shares)==company.OutstandingShares,"Approved policy applied without duplicated voting rights");
        check(g.ProposeVote(company.SecurityId,VoteKind.Management,0) is null,"Expansion proposal");
        var rejected=g.State.CompanyVotes.Last(); g.ResolveVote(rejected,cheap);
        check(rejected.Status==VoteStatus.Rejected && company.Management==ManagementPolicy.Efficiency,"Rejected proposal preserves approved management");
        validate(g);
        var shortVote=new GameEngine(152); string id=shortVote.State.Stocks[0].SecurityId;
        shortVote.SubmitOrder(1,0,false,52400,3,shortSale:true); shortVote.SubmitOrder(2,0,true,52400,3);
        shortVote.ProposeVote(id,VoteKind.Management,1); var sv=shortVote.State.CompanyVotes.Last(); shortVote.ResolveVote(sv,0);
        check(sv.EligibleShares==1000 && sv.Ballots.Sum(b=>b.Shares)==1000 && !sv.Ballots.Any(b=>b.OwnerId==1),"Short borrower receives no duplicate voting right"); validate(shortVote);
        var growth=new GameEngine(156); var defensive=GameEngine.Deserialize(growth.Serialize());
        growth.State.Stocks[0].CompanyStrategy=CompanyStrategy.Growth;
        defensive.State.Stocks[0].CompanyStrategy=CompanyStrategy.Defensive;
        growth.State.CompletedHours=defensive.State.CompletedHours=719;
        growth.AdvanceHour(); defensive.AdvanceHour();
        check(growth.State.Stocks[0].Report.Revenue>defensive.State.Stocks[0].Report.Revenue &&
            growth.State.Stocks[0].Report.OperatingCosts>defensive.State.Stocks[0].Report.OperatingCosts,"Governance strategy changes actual monthly sales and operating commitments");
        validate(growth); validate(defensive);
        string folder=Path.Combine(Path.GetTempPath(),"hora-v15-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new GameStore(folder); var world=new GameEngine(153); store.Attach(world);
            TestFixtures.BuyFromBank(world,2,0,2);
            world.SubmitOrder(1,0,false,52400,1,shortSale:true); world.SubmitOrder(3,0,true,52400,1);
            var doomed=world.State.Stocks[0]; string oldId=doomed.SecurityId;
            long matchedVolume=world.CaptureSnapshot().Volume,matchedTurnover=world.CaptureSnapshot().Turnover;
            doomed.Report=CheckpointCopy.Scalar(doomed.Report); doomed.Report.Debt=doomed.Report.Assets+1000;
            world.FailCompany(0);
            var born=world.State.Stocks[0];
            check(born.Generation==2 && born.SecurityId!=oldId && born.TotalShares==2000 && born.TreasuryShares==1000 && world.State.Bank.ShareInventory[0]==1000,"Bankrupt company replaced in bounded slot with distinct identity and 50/50 ownership");
            check(world.State.Stocks.Count==30 && world.Owner(2).Shares[0]==0 && world.Owner(1).ShortShares[0]==0,"Old shareholder and stock loan rights are settled before replacement");
            check(world.CaptureSnapshot().Volume==matchedVolume && world.CaptureSnapshot().Turnover==matchedTurnover &&
                world.State.SectorVolumes.Values.Sum()==matchedVolume,"Company replacement cannot reset market or sector cumulative activity");
            validate(world);
            world.SubmitOrder(1,1,true,1,2);
            world.SubmitOrder(5,1,true,2,3);
            world.CancelOrders(5); // A daily cancellation exists when a different trader fails.
            world.AssessFine(world.Owner(1),world.Owner(1).Cash+1000); world.FailTrader(world.Owner(1),"회귀 테스트 지급 불능");
            check(Enumerable.Range(0,30).All(i=>world.Orders(i,true).Concat(world.Orders(i,false)).All(o=>o.Remaining>0)),"Bankruptcy book rebuild excludes cancelled zero-quantity orders");
            check(world.Owner(1).Generation==2 && world.Owner(1).Cash==GameEngine.InitialCash && world.State.BankruptcyTotals.Institutions==1,"Institution replacement has funded capital and new generation");
            for(int i=101;i<171;i++) { world.AssessFine(world.Owner(i),world.Owner(i).Cash+1); world.FailTrader(world.Owner(i),"개인 자금 소진"); }
            check(world.State.Bankruptcies.Count==64 && world.State.BankruptcyTotals.Retail==70,"UI bankruptcy buffer bounded; cumulative counts retained");
            validate(world);
            world.ProposeVote(born.SecurityId,VoteKind.Strategy,1); world.ResolveVote(world.State.CompanyVotes.Last(),0);
            string exact=world.Serialize(); var snapshot=store.PrepareSave(world);
            world.State.CompanyVotes.Last().Ballots.Clear(); world.State.BankruptcyTotals.Retail++;
            check(snapshot.Json==exact,"Governance and lifecycle checkpoint is deeply isolated");
            store.WriteSnapshot(snapshot); var restored=store.Load(out _)!;
            check(restored.Serialize()==exact && GameEngine.Deserialize(exact).Serialize()==exact,"Compact v6 full round trip and database continuation"); validate(restored);
            check(store.ReadBankruptcies(restored.State,1000).Count==72 && store.ReadVotes(restored.State,born.SecurityId).Any(v=>v.Status!=VoteStatus.Open),"All failures and completed votes archived beyond bounded world buffer");
            check(store.ReadReports(restored.State.RunId,oldId,restored.State.Season).Any(),"Old company financial statement survives slot replacement");
            var bad=JsonNode.Parse(exact)!; bad["Bots"]![0]!["Generation"]=0;
            try { GameEngine.Deserialize(bad.ToJsonString()); check(false,"Reject invalid generation"); } catch(InvalidDataException) { }
            var changed=JsonNode.Parse(snapshot.Bankruptcies[0].Json)!; changed["Reason"]="다른 이유";
            var conflict=snapshot with { Bankruptcies=[snapshot.Bankruptcies[0] with { Json=changed.ToJsonString() }] };
            try { store.WriteSnapshot(conflict); check(false,"Reject contradictory persistent failure ID"); } catch(InvalidDataException) { }
            check(store.Load(out _)!.Serialize()==exact,"Conflicting lifecycle write rolls back checkpoint");
            using var archive=new MemoryStream(); store.Export(archive); archive.Position=0;
            var importedStore=new GameStore(Path.Combine(folder,"import")); var imported=importedStore.Import(archive);
            check(imported.Serialize()==exact && importedStore.ReadBankruptcies(imported.State,1000).Count==72,"ZIP preserves complete bankruptcy archive");
        }
        finally { Directory.Delete(folder,true); }
        var simulation=new GameEngine(154);
        for(int hour=0;hour<1440;hour++) { simulation.AdvanceHour(); if(hour%240==239) validate(simulation); }
        check(simulation.State.Bank.InterventionPurchases>0 && simulation.State.Bank.InterventionSales>0,"Stability bank trades actual bids and asks");
        check(simulation.State.CompanyVotes.Any(v=>v.Status==VoteStatus.Passed) && simulation.State.Stocks.Any(s=>s.TreasuryShares>1000),"Automatic monthly governance and funded treasury expansion");
        string json=simulation.Serialize(); var resumed=GameEngine.Deserialize(json);
        for(int hour=0;hour<48;hour++) { simulation.AdvanceHour(); resumed.AdvanceHour(); }
        check(simulation.Serialize()==resumed.Serialize(),"Retail/bank/governance deterministic continuation");
        Console.WriteLine("PASS v1.5: reactive retail, 50/50 issuance, bank market, weighted governance, company/investor settlement, generations, atomic archive and compact saves");
    }
}
