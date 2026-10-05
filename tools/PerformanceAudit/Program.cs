using AlphaExchange.Core;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

string label = args.ElementAtOrDefault(0) ?? "measurement";
string output = args.ElementAtOrDefault(1) ?? Path.Combine(Path.GetTempPath(), "hora-performance-"+label+".json");
const uint seed = 20261004;
var folder = Path.Combine(Path.GetTempPath(), "hora-performance-"+Guid.NewGuid().ToString("N"));
string? input = args.ElementAtOrDefault(2);
// Both fixed-input versions exercise the same hot paths before measuring; loading
// a checkpoint alone does not warm tiered JIT like the original 168-hour creation.
if(input is not null && File.Exists(input))
{
    var warmup=new GameEngine(seed); for(int hour=0;hour<168;hour++) warmup.AdvanceHour();
}
var game = input is not null && File.Exists(input) ? GameEngine.Deserialize(File.ReadAllText(input)) : new GameEngine(seed);
if(input is null || !File.Exists(input)) for (int i=0;i<168;i++) game.AdvanceHour();
if(args.ElementAtOrDefault(3) is { } checkpoint) File.WriteAllText(checkpoint,game.Serialize());
var store = new GameStore(folder); store.Attach(game); store.Save(game);
var statsField = typeof(GameEngine).GetField("cachedStats",BindingFlags.Instance|BindingFlags.NonPublic)!;
Dictionary<string,object> results = new();

object Measure(string name,Action action,int repeats=9)
{
    action(); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    var time = new List<double>(); var allocations = new List<long>();
    for(int i=0;i<repeats;i++)
    {
        long allocated=GC.GetAllocatedBytesForCurrentThread(),tick=Stopwatch.GetTimestamp();
        action(); time.Add(Stopwatch.GetElapsedTime(tick).TotalMilliseconds);
        allocations.Add(GC.GetAllocatedBytesForCurrentThread()-allocated);
    }
    time.Sort(); allocations.Sort();
    var value=new { medianMs=time[time.Count/2], maximumMs=time[^1], allocatedMedianBytes=allocations[allocations.Count/2], samples=time };
    results[name]=value; Console.WriteLine(name+": "+JsonSerializer.Serialize(value)); return value;
}
try
{
    Measure("statistics",()=>{ statsField.SetValue(game,null); _=game.Statistics(); });
    Measure("depthProjection6000",()=>
    {
        for(int frame=0;frame<100;frame++) for(int i=0;i<game.State.Stocks.Count;i++)
        { _=game.Depth(i,true,5); _=game.Depth(i,false,5); }
    });
    SaveSnapshot? prepared=null;
    Measure("prepareSave",()=>{ prepared=store.PrepareSave(game); });
    if(prepared!.Json!=game.Serialize()) throw new Exception("Checkpoint capture differs from current state");
    Measure("serialize",()=>{ _=game.Serialize(); });
    Measure("writeSnapshot",()=>store.WriteSnapshot(store.PrepareSave(game)),5);
    results["checkpoint"]=new { hour=game.State.CompletedHours,encodedBytes=System.Text.Encoding.UTF8.GetByteCount(game.Serialize()) };
    var hourly = new GameEngine(seed); var timings=new List<double>();
    for(int i=0;i<720;i++) { long tick=Stopwatch.GetTimestamp(); hourly.AdvanceHour(); timings.Add(Stopwatch.GetElapsedTime(tick).TotalMilliseconds); }
    timings.Sort(); results["simulation"]=new { hours=720, medianMs=timings[360],p95Ms=timings[(int)(720*.95)],p99Ms=timings[(int)(720*.99)],matches=hourly.State.TotalMatches };
    Console.WriteLine("simulation: "+JsonSerializer.Serialize(results["simulation"]));

    // Storage-only synthetic history: five 360-day years, independent of simulation timings.
    var synthetic = new GameEngine(seed); var historyStore=new GameStore(Path.Combine(folder,"history"));
    var rows=new List<HistoryRecord>(); long last=0;
    for(int hour=0;hour<=43200;hour++)
    {
        synthetic.State.CompletedHours=hour; var point=synthetic.CaptureSnapshot();
        point.Capitalization+=hour%811*101; point.PriceIndex+=hour%97; point.TotalReturnIndex+=hour%137;
        point.InstitutionCash+=hour%173; point.RetailCash+=hour%193;
        point.InstitutionEquity+=hour%211; point.RetailEquity+=hour%223;
        rows.Add(new HistoryRecord(hour,1,JsonSerializer.Serialize(point))); last=hour;
        if(rows.Count<720 && hour<43200) continue;
        historyStore.WriteSnapshot(new SaveSnapshot(synthetic.State.RunId,hour,synthetic.Serialize(),[],rows.ToArray())); rows.Clear();
    }
    historyStore.Attach(synthetic);
    Measure("fiveYearRange",()=>
    {
        var points=synthetic.HistorySource!.Range(0,last,500);
        if(points.Count>500 || points[0].Hour!=0 || points[^1].Hour!=last) throw new Exception("History boundaries or point limit");
    },5);
    Measure("coverage",()=>{ if(!synthetic.HistorySource!.Covers(0,last)) throw new Exception("History missing"); },5);
    Measure("pendingAt",()=>
    {
        var point=synthetic.HistorySource!.At(last);
        if(point?.Hour!=last) throw new Exception("Point missing");
    });
    results["history"]=new { rows=43201, bytes=new FileInfo(historyStore.SavePath).Length };
    var report=new { label,seed,framework=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,processorCount=Environment.ProcessorCount,results };
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!); File.WriteAllText(output,JsonSerializer.Serialize(report,new JsonSerializerOptions { WriteIndented=true }));
    Console.WriteLine("Saved "+output);
}
finally { Directory.Delete(folder,true); }
