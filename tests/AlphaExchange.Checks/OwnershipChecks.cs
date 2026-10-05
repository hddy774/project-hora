using AlphaExchange.Core;
using Microsoft.Data.Sqlite;
using System.Text.Json;

static class OwnershipChecks
{
    public static void Run(Action<bool,string> check,Action<GameEngine> validate)
    {
        var game=new GameEngine(140); CheckAll(game,check);
        var stock=game.State.Stocks[0]; string id=stock.SecurityId;
        var first=game.Ownership(0); check(ReferenceEquals(first,game.Ownership(0)),"Unchanged ownership uses immutable cached result");
        long bank=game.State.Bank.ShareInventory[0];
        check(game.SubmitOrder(1,0,false,stock.Price,2,shortSale:true) is null,"Reserve borrowed shares");
        check(game.Ownership(0).ReservedLending==2 && game.Ownership(0).BorrowedShares==0,"Reserved shorts are not borrowed or new equity");
        game.CancelOrders(1); check(game.Ownership(0).ReservedLending==0,"Cancelling lending invalidates ownership cache");
        check(game.SubmitOrder(1,0,false,stock.Price,2,shortSale:true) is null && game.SubmitOrder(2,0,true,stock.Price,2) is null,"Borrowed shares change physical owners");
        var after=game.Ownership(0);
        check(!ReferenceEquals(first,after) && after.BorrowedShares==2 && after.Slices.Single(s=>s.Category==HolderCategory.Bank).Shares==bank-2,"Loans shown separately without double-counting bank equity");
        check(first.BorrowedShares==0 && first.Slices.Single(s=>s.Category==HolderCategory.Bank).Shares==bank,"Cached ownership snapshot never mutates");
        CheckAll(game,check); validate(game);
        check(game.SplitShares(id,2) is null,"Ownership split");
        check(game.Ownership(0).BorrowedShares==4 && game.Ownership(0).IssuedShares==first.IssuedShares*2,"Split rescales holdings and borrowed obligations");
        check(game.SplitShares(id,1,2) is null,"Ownership reverse split");
        check(game.SubscribeIssue(3,id,3) is null,"Ownership funded subscription");
        check(game.Buyback(id,3,1) is null,"Ownership treasury repurchase");
        var treasury=game.Ownership(0);
        check(treasury.IssuedShares-treasury.OutstandingShares==1001 && treasury.Slices.Single(s=>s.Category==HolderCategory.Treasury).Shares==1001,"Issued and outstanding denominators distinguish treasury");
        check(Math.Abs(treasury.Slices.Where(s=>s.Category!=HolderCategory.Treasury).Sum(s=>treasury.OutstandingRatio(s.Shares))-1)<1e-12,"Outstanding shareholder percentages sum to 100%");
        CheckAll(game,check); validate(game);
        int target=game.State.Stocks.FindIndex(s=>s.Sector==stock.Sector && s.SecurityId!=id);
        check(game.MergeCompanies(id,game.State.Stocks[target].SecurityId) is null,"Ownership merger");
        check(!game.Ownership(0).Active && game.Ownership(0).IssuedShares==0 && game.Ownership(0).Institutions.Count==0,"Merged security is an empty tombstone rather than duplicated ownership");
        CheckAll(game,check); validate(game);
        CheckpointIsolation(check,validate); HistoricalQueries(check); LazyPeriods(check);
        Console.WriteLine("PASS ownership/performance: physical ownership, treasury denominators, lender obligations, cache invalidation, frozen checkpoints, full JSON validation, extrema and mixed-resolution gaps");
    }
    static void CheckAll(GameEngine game,Action<bool,string> check)
    {
        for(int i=0;i<game.State.Stocks.Count;i++)
        {
            var ownership=game.Ownership(i); var stock=game.State.Stocks[i];
            check(ownership.Slices.Sum(s=>s.Shares)==stock.TotalShares,"Ownership reconciles issued stock");
            check(ownership.Slices.Single(s=>s.Category==HolderCategory.Institutions).Shares==game.State.Bots.Sum(t=>t.Shares[i]) && ownership.Slices.Single(s=>s.Category==HolderCategory.Retail).Shares==game.State.Retail.Sum(t=>t.Shares[i]),"Ownership category counts match actual accounts");
            check(ownership.Institutions.Sum(h=>h.Shares)==game.State.Bots.Sum(t=>t.Shares[i]) && ownership.Institutions.All(h=>h.Shares>0),"Major owners include every positive institution holder exactly once");
            check(ownership.Institutions.Zip(ownership.Institutions.Skip(1)).All(p=>p.First.Shares>p.Second.Shares || p.First.Shares==p.Second.Shares && p.First.TraderId<p.Second.TraderId),"Major holders sorted by shares with stable ties");
        }
    }
    static void CheckpointIsolation(Action<bool,string> check,Action<GameEngine> validate)
    {
        string folder=Path.Combine(Path.GetTempPath(),"hora-freeze-"+Guid.NewGuid().ToString("N"));
        try
        {
            var game=new GameEngine(141); for(int i=0;i<719;i++) game.AdvanceHour();
            var store=new GameStore(folder); store.Attach(game);
            string original=game.Serialize(); var frozen=store.PrepareSave(game);
            var writer=Task.Run(()=>store.WriteSnapshot(frozen));
            for(int i=0;i<4;i++) game.AdvanceHour(); // includes month close, policy, reports and history mutations
            writer.GetAwaiter().GetResult();
            check(frozen.Json==original,"Deferred encoding is isolated from live month-end progression");
            var loaded=store.Load(out _)!;
            check(loaded.Serialize()==original && loaded.State.CompletedHours==719,"Writer checkpoint and history end remain atomic while simulation advances"); validate(loaded);
            var detached=store.PrepareSave(game); string expected=game.Serialize();
            game.State.Bots[0].Abilities.Valuation++;
            game.State.Bots[0].AverageCost[0]++;
            game.State.Bank.ShareInventory[0]++;
            game.State.Stocks[0].Reports[0].Cash++;
            game.State.Government.History[0].Name="mutated";
            game.State.DailyHistory[0].StockPrices[0]++;
            game.State.DailyHistory[0].Institutions[0].Cash++;
            game.State.CashFlows["mutated"]=1;
            check(detached.Json==expected,"Frozen mutable arrays, reports, abilities, policies, snapshots and dictionaries do not share live data");
            string previous=store.Load(out _)!.Serialize(); int pending=store.PendingCount;
            foreach(string invalid in new[] { frozen.Json+" trailing",frozen.Json[..^1],frozen.Json.Replace("\"CompletedHours\":719","\"CompletedHours\":718") })
            {
                try { store.WriteSnapshot(frozen with { Json=invalid }); check(false,"Malformed checkpoint rejected"); }
                catch(Exception e) when(e is InvalidDataException or JsonException) { }
                check(store.Load(out _)!.Serialize()==previous && store.PendingCount>=pending,"Invalid checkpoint preserves world and queued history");
            }
        }
        finally { Directory.Delete(folder,true); }
    }
    static void HistoricalQueries(Action<bool,string> check)
    {
        string folder=Path.Combine(Path.GetTempPath(),"hora-query-"+Guid.NewGuid().ToString("N"));
        try
        {
            var game=new GameEngine(142); var store=new GameStore(folder);
            var headers=new[] { (0,24),(24,24),(48,24),(49,1),(50,1),(54,1),(55,1),(78,24),(102,24),(103,1),(105,1),(106,1),(130,24),(154,24),(155,1),(156,1),(180,1) };
            var rows=new List<HistoryRecord>();
            foreach(var (hour,resolution) in headers)
            {
                game.State.CompletedHours=hour; var point=game.CaptureSnapshot(); point.Resolution=resolution;
                if(hour==49) point.PriceIndex=9000;
                if(hour==105) point.TotalReturnIndex=7000;
                if(hour==103) point.InstitutionTradingIncome=8_000_000;
                if(hour==106) point.RetailTradingIncome=-9_000_000;
                rows.Add(new HistoryRecord(hour,resolution,JsonSerializer.Serialize(point)));
            }
            store.WriteSnapshot(new SaveSnapshot(game.State.RunId,180,game.Serialize(),[],rows.ToArray())); store.Attach(game);
            foreach(var (start,end) in new[] { (0,180),(0,48),(48,55),(55,102),(103,130),(155,180),(1,25),(56,77),(181,182) })
            {
                var selected=headers.Where(h=>h.Item1>=start && h.Item1<=end).ToArray(); var gaps=new List<long>();
                for(int i=0;i<selected.Length;i++) if(i==0 && selected[i].Item1>start || i>0 && selected[i].Item1-selected[i-1].Item1>Math.Max(selected[i].Item2,selected[i-1].Item2)) gaps.Add(selected[i].Item1);
                if(selected.Length==0 || selected[^1].Item1<end) gaps.Add(end);
                check(game.HistorySource!.Covers(start,end)==(gaps.Count==0),"Optimized coverage agrees with original mixed-resolution header semantics");
                long previous=start;
                foreach(var point in game.HistorySource.Range(start,end,500))
                { check(point.GapBefore==gaps.Any(h=>h>previous && h<=point.Hour),"Gap flags match actual missing records"); previous=point.Hour; }
            }
            var range=game.HistorySource!.Range(0,180,16);
            check(range.Count<=16 && range[0].Hour==0 && range[^1].Hour==180 && range.Any(p=>p.PriceIndex==9000) && range.Any(p=>p.TotalReturnIndex==7000),"Joined extrema query retains boundaries, price and total-return extrema");
            check(range.Any(p=>p.InstitutionTradingIncome==8_000_000) && range.Any(p=>p.RetailTradingIncome==-9_000_000),"New cohort charts retain investment income extrema within bounded historical query");
            for(int i=0;i<32;i++) game.AdvanceHour();
            check(game.HistorySource.At(205)!.Hour==205 && game.HistorySource.At(200)!.Hour==200,"Point lookup selects exact hour from multiple pending snapshots");
            using var connection=new SqliteConnection($"Data Source={store.SavePath};Pooling=False"); connection.Open();
            using var command=connection.CreateCommand(); command.CommandText="DELETE FROM hours WHERE hour=24"; command.ExecuteNonQuery();
            check(!game.HistorySource.Covers(0,48),"External missing historical row is detected without stale coverage cache");
        }
        finally { Directory.Delete(folder,true); }
    }
    sealed class CountingQuery(GameEngine game):IHistoryQuery
    {
        public int RangeCalls;
        public DailySnapshot? At(long hour) => game.State.OpeningSnapshot;
        public bool Covers(long start,long end) => true;
        public IReadOnlyList<DailySnapshot> Range(long start,long end,int maximumPoints=500)
        {
            RangeCalls++; var middle=game.CaptureSnapshot(); middle.Hour=(start+end)/2;
            return [game.State.OpeningSnapshot!,middle,game.CaptureSnapshot()];
        }
    }
    static void LazyPeriods(Action<bool,string> check)
    {
        var game=new GameEngine(143); for(int i=0;i<10;i++) game.AdvanceHour();
        var query=new CountingQuery(game); game.HistorySource=query;
        var summary=game.PeriodSummary(ComparisonPeriod.All);
        check(query.RangeCalls==0 && summary.Start.Hour==0 && summary.End.Hour==10,"Ranking and overview summary avoid chart range loading");
        var chart=game.Period(ComparisonPeriod.All);
        check(query.RangeCalls==1 && chart.Points.Count==3 && chart.Change==summary.Change,"Visible chart loads intermediate history with matching summary");
        check(ReferenceEquals(chart,game.Period(ComparisonPeriod.All)) && query.RangeCalls==1,"Repeated graphs reuse one full range");
        game.AdvanceHour(); var next=game.PeriodSummary(ComparisonPeriod.All);
        check(query.RangeCalls==1 && next.End.Hour==11,"Live summary refreshes without decoding chart records");
        check(game.Period(ComparisonPeriod.All).End.Hour==11 && query.RangeCalls==2,"Next live chart refreshes actual completed-hour records");
    }
}
