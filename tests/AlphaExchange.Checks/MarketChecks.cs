using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO.Compression;
using AlphaExchange.Core;
using Microsoft.Data.Sqlite;

static class MarketChecks
{
    public static void Run(Action<bool,string> check,Action<GameEngine> validate,Action<double,double,string,double> near)
    {
        var g=new GameEngine(130);
        check(g.State.Stocks.Count==30 && g.State.Stocks.GroupBy(s=>s.Sector).All(sector=>sector.Count()==5) && g.State.SecurityIds.Distinct().Count()==30,"Six sectors, five distinct companies each");
        check(g.Participants.All(t=>t.Cash>=0) && g.State.Stocks.Any(s=>s.CostRatio!=g.State.Stocks[0].CostRatio),"Budgeted endowment and distinct business cost profiles");
        var s=g.State.Stocks[0]; string id=s.SecurityId;
        check(g.SubmitOrder(1,0,false,s.Price,2,shortSale:true) is null && g.SubmitOrder(2,0,true,s.Price,2) is null,"Dividend test borrows and sells two bank shares");
        long issuer=s.Report.Cash,bankIncome=g.State.Bank.DividendIncome,paid=g.State.MarketDividends;
        long outShares=s.OutstandingShares,bankShares=g.State.Bank.ShareInventory[0],gross=g.Owner(2).Shares[0]*7,shortExpense=g.Owner(1).ShortDividendExpense;
        long beforeVolume=s.TotalVolume,oldTrade=s.LastTradePrice;
        check(g.PayDividend(id,outShares*7)==outShares*7 && s.Report.Cash==issuer-outShares*7,"Company funds exactly one dividend per outstanding share");
        check(g.Owner(2).DividendIncome==gross && g.Owner(1).ShortDividendExpense-shortExpense==14 && g.State.Bank.DividendIncome-bankIncome==bankShares*7+14,"Buyer owns dividend; lender receives replacement from short seller");
        check(g.State.MarketDividends-paid==outShares*7 && s.TotalVolume==beforeVolume && s.LastTradePrice==oldTrade,"Dividend has no synthetic execution"); validate(g);
        long cap=s.MarketCap,q=g.Owner(2).Shares[0],shorts=g.Owner(1).ShortShares[0]; double cost=g.Owner(2).AverageCost[0],divisorCap=g.State.Stocks.Sum(x=>x.MarketCap)/g.State.IndexDivisor;
        check(g.SplitShares(id,2) is null,"Two-for-one split"); s=g.State.Stocks[0];
        check(s.MarketCap==cap && g.Owner(2).Shares[0]==q*2 && g.Owner(1).ShortShares[0]==shorts*2 && s.TotalVolume==beforeVolume,"Split preserves capitalization, loans and historical volume");
        near(g.Owner(2).AverageCost[0],cost/2,"Split adjusts cost basis",.0001); validate(g);
        check(g.SplitShares(id,1,2) is null && g.Owner(2).Shares[0]==q,"Reverse split restores exact lots"); validate(g);
        string before=g.Serialize(); check(g.SplitShares(id,20) is null,"Valid funded split");
        string rollback=g.Serialize(); check(g.SplitShares(id,20) is null,"Second funded split");
        string invalidBefore=g.Serialize(); check(g.SplitShares(id,20) is null,"Third funded split");
        invalidBefore=g.Serialize(); check(g.SplitShares(id,20) is not null && g.Serialize()==invalidBefore,"Invalid fractional price action is atomic");
        g=GameEngine.Deserialize(before); s=g.State.Stocks[0];
        double neutral=g.State.Stocks.Sum(x=>x.MarketCap)/g.State.IndexDivisor; long investorCash=g.Owner(3).Cash,companyCash=s.Report.Cash,issued=s.TotalShares;
        check(g.SubscribeIssue(3,id,3) is null && g.Owner(3).Cash==investorCash-3*s.Price && s.Report.Cash==companyCash+3*s.Price && s.TotalShares==issued+3,"Primary subscription is a funded capital transfer");
        near(g.State.Stocks.Sum(x=>x.MarketCap)/g.State.IndexDivisor,neutral,"Issuance is neutral in price index",.001); validate(g);
        long outstanding=s.OutstandingShares;
        check(g.Buyback(id,3,1,false) is null && s.TreasuryShares==1 && s.OutstandingShares==outstanding-1,"Treasury stock excluded from capitalization"); validate(g);
        check(g.Buyback(id,3,1,true) is null && s.TotalShares==issued+2,"Funded repurchase and retirement"); validate(g);
        int target=g.State.Stocks.FindIndex(x=>x.Sector==s.Sector && x.SecurityId!=id); string targetId=g.State.Stocks[target].SecurityId;
        check(g.MergeCompanies(id,targetId) is null,"Same-sector stock exchange merger");
        check(!g.State.Stocks[0].Active && g.Participants.All(t=>t.Shares[0]==0 && t.ShortShares[0]==0) && g.State.Stocks.Count==30,"Merger keeps historical security ID as a tombstone"); validate(g);
        for(int i=0;i<720;i++) g.AdvanceHour();
        check(g.State.Stocks.Count(x=>x.Active)==30 && g.State.Stocks.Where(x=>x.Active).GroupBy(x=>x.Sector).All(group=>group.Count()==5),"Paid IPO replenishes sector after merger");
        foreach(var stock in g.State.Stocks.Where(x=>x.Active))
        { var r=stock.Report; check(r.OpeningCash+r.OperatingCashFlow+r.InvestingCashFlow+r.FinancingCashFlow==r.Cash,"Post-action monthly cash flow"); check(r.OpeningEquity+r.NetIncome-r.Dividends+r.CapitalChange==r.Equity,"Post-action monthly equity"); }
        validate(g);
        var taxed=new GameEngine(19); int price=taxed.State.Stocks[0].Price;
        long taxQuantity=Math.Min(3,taxed.Owner(1).Shares[0]);
        check(taxed.SubmitOrder(1,0,false,price+1000,(int)taxQuantity) is null && taxed.SubmitOrder(2,0,true,price+1000,(int)taxQuantity) is null,"Taxable gain executes");
        long taxPaid=taxed.Owner(1).Taxes;
        check(taxPaid>0 && taxed.State.Government.TaxEscrow>=taxPaid,"Tax withheld into refundable escrow");
        check(taxed.SubmitOrder(1,0,false,1,1) is null && taxed.SubmitOrder(3,0,true,1,1) is null,"Loss offsets earlier gain");
        check(taxed.Owner(1).Taxes==0 && taxed.State.Government.TaxRefunds==taxPaid,"Same-season net loss refunds earlier tax"); validate(taxed);
        StorageChecks(check,validate);
        Console.WriteLine("PASS market: 30 companies, real dividends, lender compensation, share actions, funded capital, index neutrality, post-action statements, hourly storage, migration, recovery and file portability");
    }
    static void StorageChecks(Action<bool,string> check,Action<GameEngine> validate)
    {
        string folder=Path.Combine(Path.GetTempPath(),"hora-storage-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new GameStore(folder); var g=new GameEngine(301); store.Attach(g); store.Save(g);
            int elapsed=0;
            foreach(int speed in GameEngine.Speeds) elapsed+=g.AdvanceTime(5,speed);
            check(store.PendingCount==elapsed,"Every completed hour is queued at all six speeds");
            var first=store.PrepareSave(g); long firstHour=g.State.CompletedHours; g.AdvanceHour(); var latest=store.PrepareSave(g);
            store.WriteSnapshot(first); store.WriteSnapshot(latest); GameStore.Acknowledge(g,latest);
            check(g.HistorySource!.Covers(0,g.State.CompletedHours),"Coalesced saves preserve every hour");
            check(g.HistorySource.Range(0,g.State.CompletedHours,1000).Count==g.State.CompletedHours+1,"Every original hourly record remains queryable");
            var loaded=store.Load(out _)!; check(loaded.State.CompletedHours==g.State.CompletedHours && loaded.HistorySource!.At(firstHour)?.Hour==firstHour,"Checkpoint and statistics resume at the same hour");
            var duplicate=latest.History.First(h=>h.Resolution==1); var altered=JsonNode.Parse(duplicate.Json)!; altered["Capitalization"]=(long)altered["Capitalization"]!+1;
            var bad=latest with { History=[duplicate with { Json=altered.ToJsonString() }] };
            try { store.WriteSnapshot(bad); check(false,"Reject conflicting duplicate"); } catch(InvalidDataException) { check(true,"Conflicting duplicate rolls back"); }
            check(store.Load(out _)!.State.CompletedHours==g.State.CompletedHours,"Failed transaction preserves previous checkpoint");
            store.WriteSnapshot(latest);
            try { store.WriteSnapshot(first); check(false,"Reject stale checkpoint"); } catch(InvalidDataException) { check(true,"Stale checkpoint cannot replace new world"); }
            using var backup=new MemoryStream(); store.Export(backup); backup.Position=0;
            using(var zip=new ZipArchive(backup,ZipArchiveMode.Read,true)) { using var reader=new StreamReader(zip.GetEntry("market.csv")!.Open()); check(reader.ReadToEnd().Split('\n',StringSplitOptions.RemoveEmptyEntries).Length==g.State.CompletedHours+2,"CSV contains header and every actual hour"); }
            backup.Position=0; var freshStore=new GameStore(Path.Combine(folder,"reinstalled")); var imported=freshStore.Import(backup);
            check(imported.Serialize()==store.Load(out _)!.Serialize() && imported.HistorySource!.Covers(0,imported.State.CompletedHours),"Export/import restores game and full history across installations"); validate(imported);
            var old=V4Fixture(); string original=old.Serialize(); string legacyFolder=Path.Combine(folder,"legacy"); Directory.CreateDirectory(legacyFolder); File.WriteAllText(Path.Combine(legacyFolder,"market-v4.json"),original);
            var migratedStore=new GameStore(legacyFolder); var migrated=migratedStore.Load(out var message)!;
            check(migrated.State.MigratedFromV4 && message.Contains("이전") && File.ReadAllText(Path.Combine(legacyFolder,"market-v4.json"))==original,"Legacy migration preserves original file");
            check(old.Participants.Zip(migrated.Participants).All(pair=>pair.First.Cash==pair.Second.Cash && pair.First.Shares.SequenceEqual(pair.Second.Shares.Take(10))),"v4 cash and all original ten holdings are unchanged");
            check(migrated.State.Stocks.Count==30 && migrated.State.Stocks.Skip(10).All(s=>s.FundedIpo) && migrated.State.OpeningSnapshot!.PriceIndex==1000,"Migration funds new IPOs and normalizes historical opening index"); validate(migrated);
            var legacyPoints=migrated.HistorySource!.Range(0,old.State.CompletedHours);
            check(legacyPoints.All(s=>s.Hour==old.State.CompletedHours || s.Resolution==24) && legacyPoints.Any(s=>s.LegacyNoFundamentals),"Old daily data stays daily; missing hourly/financial data is not invented");
            var ipo=migrated.State.Stocks[10]; int ipoIndex=10;
            check(migrated.SubscribeIssue(1,ipo.SecurityId,2) is null,"Funded odd-lot IPO shares");
            check(migrated.SubmitOrder(3,ipoIndex,false,ipo.Price,1,shortSale:true) is null && migrated.SubmitOrder(2,ipoIndex,true,ipo.Price,1) is null,"Odd-lot short shares");
            long longCash=migrated.Owner(1).Cash,shortBefore=migrated.Owner(3).ShortShares[ipoIndex],ipoVolume=ipo.TotalVolume;
            check(migrated.SplitShares(ipo.SecurityId,1,3) is null && migrated.Owner(1).Shares[ipoIndex]==0 && migrated.Owner(1).Cash==longCash+2*ipo.Price/3,"Reverse split pays long fractional rights");
            check(shortBefore==1 && migrated.Owner(3).ShortShares[ipoIndex]==0 && ipo.TotalVolume==ipoVolume,"Reverse split settles borrower fraction without fake trades"); validate(migrated);
            migrated.AdvanceHour(); migratedStore.Save(migrated);
            check(migratedStore.ReadEvents(migrated.State,ipo.SecurityId).Any(e=>e.Kind==CorporateEventKind.ReverseSplit),"Corporate events persist outside bounded world cache");
            store.Save(loaded); File.WriteAllText(store.SavePath,"corrupt");
            check(store.Load(out message) is not null && message.Contains("복구") && Directory.GetFiles(folder,"*.corrupt-*").Length>0,"Healthy backup recovers without discarding damaged original");
            backup.Position=0; using var wrong=new MemoryStream(); using(var zip=new ZipArchive(wrong,ZipArchiveMode.Create,true)) { using var writer=new StreamWriter(zip.CreateEntry("../history-v5.sqlite").Open()); writer.Write("invalid"); }
            wrong.Position=0; try { freshStore.Import(wrong); check(false,"Invalid import"); } catch(InvalidDataException) { check(true,"Invalid import preserves existing database"); }
            check(freshStore.Load(out _)!.State.CompletedHours==imported.State.CompletedHours,"Failed import leaves game intact");
        }
        finally { Directory.Delete(folder,true); }
    }
    static GameEngine V4Fixture()
    {
        var g=new GameEngine(47);
        foreach(var t in g.Participants)
        {
            for(int i=10;i<g.State.Stocks.Count;i++) t.Cash+=g.State.Stocks[i].Value(t.Shares[i]);
            t.Shares=t.Shares.Take(10).ToArray(); t.AverageCost=t.AverageCost.Take(10).ToArray(); t.ReservedShares=new long[10]; t.ShortShares=new long[10]; t.ShortAveragePrice=new double[10]; t.ReservedCovers=new long[10];
            t.OpeningCash=t.Cash;
        }
        g.State.Stocks.RemoveRange(10,20); g.State.SecurityIds=g.State.Stocks.Select(s=>s.SecurityId).ToList(); g.State.News.Clear();
        g.State.Bank.ShareInventory=g.State.Bank.ShareInventory.Take(10).ToArray(); g.State.Bank.ReservedLending=new long[10];
        for(int i=0;i<10;i++) g.State.Stocks[i].TotalShares=g.Participants.Sum(t=>t.Shares[i])+g.State.Bank.ShareInventory[i];
        // Original v4 book scope excluded companies and real-economy cash.
        g.State.InitialSystemCash=g.Participants.Sum(t=>t.Cash)+g.State.Bank.Cash+g.State.Government.Cash+g.State.FeePool;
        var json=JsonNode.Parse(g.Serialize())!; json["Version"]=4; json["CompletedHours"]=48;
        var opening=g.CaptureSnapshot(); opening.Capitalization=g.State.Stocks.Sum(s=>s.MarketCap); opening.SecurityIds=[]; opening.StockShares=[]; opening.StockSectors=[]; opening.MarkPrices=[];
        var day=JsonSerializer.Deserialize<DailySnapshot>(JsonSerializer.Serialize(opening))!; day.Hour=24;
        json["OpeningSnapshot"]=JsonSerializer.SerializeToNode(opening); json["SeasonSnapshot"]=JsonSerializer.SerializeToNode(opening);
        json["DailyHistory"]=new JsonArray(JsonSerializer.SerializeToNode(opening),JsonSerializer.SerializeToNode(day));
        // Do not deserialize before emitting an actual legacy fixture.
        return LegacyEnvelope(json.ToJsonString());
    }
    static GameEngine LegacyEnvelope(string json)
    {
        // Keep the fixture in its unupgraded representation through the public state object.
        var game=new GameEngine(47); var state=JsonSerializer.Deserialize<GameState>(json)!;
        typeof(GameEngine).GetProperty(nameof(GameEngine.State))!.SetValue(game,state); return game;
    }
}
