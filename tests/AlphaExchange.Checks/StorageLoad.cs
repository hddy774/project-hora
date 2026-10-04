using System.Diagnostics;
using System.Text.Json;
using AlphaExchange.Core;
using Microsoft.Data.Sqlite;

static class StorageLoad
{
    public static void Run()
    {
        string directory=Path.Combine(Path.GetTempPath(),"hora-five-year-"+Guid.NewGuid().ToString("N"));
        var engine=new GameEngine(5); var store=new GameStore(directory); store.Save(engine);
        var point=engine.CaptureSnapshot(); int openingPrice=point.StockPrices[0]; long openingCap=point.Capitalization;
        const int hours=5*360*24;
        var watch=Stopwatch.StartNew(); long maxManaged=0, maxRetained=0;
        try
        {
            for(int month=1;month<=60;month++)
            {
                var rows=new List<HistoryRecord>(720);
                for(int h=(month-1)*720+1;h<=month*720;h++)
                {
                    point.Hour=h; point.StockPrices[0]=openingPrice+h%400; point.MarkPrices[0]=point.StockPrices[0];
                    point.Capitalization=openingCap+(long)(h%400)*point.StockShares[0]; point.Volume=h*500L; point.Turnover=h*10_000_000L;
                    rows.Add(new HistoryRecord(h,1,JsonSerializer.Serialize(point)));
                }
                engine.State.CompletedHours=month*720;
                store.WriteSnapshot(new SaveSnapshot(engine.State.RunId,engine.State.CompletedHours,engine.Serialize(),[],rows.ToArray()));
                maxManaged=Math.Max(maxManaged,GC.GetTotalMemory(false));
                if(month%12==0) { maxRetained=Math.Max(maxRetained,GC.GetTotalMemory(true)); Console.WriteLine($"Synthetic storage year {month/12}: hours={month*720:N0}; bytes={new FileInfo(store.SavePath).Length:N0}"); }
            }
            double writeSeconds=watch.Elapsed.TotalSeconds; watch.Restart();
            var points=engine.HistorySource!.Range(0,hours,500); double queryMs=watch.Elapsed.TotalMilliseconds;
            if(points.Count>504 || points[0].Hour!=0 || points[^1].Hour!=hours || !engine.HistorySource.Covers(0,hours) || engine.HistorySource.At(1)?.Hour!=1)
                throw new Exception("Five-year history coverage/query limit failed");
            var original=store.Load(out _)!; if(original.State.CompletedHours!=hours) throw new Exception("Five-year checkpoint mismatch");
            using(var connection=new SqliteConnection($"Data Source={store.SavePath};Mode=ReadOnly;Pooling=False"))
            {
                connection.Open(); using var command=connection.CreateCommand(); command.CommandText="SELECT count(*) FROM hours WHERE resolution=1";
                if(Convert.ToInt64(command.ExecuteScalar())!=hours+1) throw new Exception("Missing original hourly rows");
            }
            using var backup=File.Create(Path.Combine(directory,"five-year.zip")); watch.Restart(); store.Export(backup); double exportSeconds=watch.Elapsed.TotalSeconds;
            Console.WriteLine(JsonSerializer.Serialize(new { fixture="synthetic-storage-only",hours,originalRows=hours+1,chartPoints=points.Count,databaseBytes=new FileInfo(store.SavePath).Length,
                maxManagedBytes=maxManaged,maxRetainedBytes=maxRetained,writeSeconds,queryMilliseconds=queryMs,exportSeconds },new JsonSerializerOptions { WriteIndented=true }));
            Console.WriteLine("PASS five-year synthetic storage; economic simulation is covered separately");
        }
        finally { Directory.Delete(directory,true); }
    }
}
