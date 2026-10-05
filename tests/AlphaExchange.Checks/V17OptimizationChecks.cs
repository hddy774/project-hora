using AlphaExchange.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

static class V17OptimizationChecks
{
    public static void Run(Action<bool,string> check)
    {
        var queued=new GameEngine(1717); var direct=GameEngine.Deserialize(queued.Serialize());
        queued.QueueTime(.5,100);
        check(queued.State.CompletedMinutes==0 && queued.State.PendingClockMinutes==600,"UI enqueues every 100x minute without executing market work");
        direct.AdvanceTime(.5,100);
        while(queued.AdvanceQueuedMinute()) {}
        check(queued.Serialize()==direct.Serialize(),"Worker minute queue preserves synchronous decisions and every accepted minute");
        string encoded=queued.Serialize();
        check(JsonNode.Parse(encoded)!["Retail"]!["Encoding"]!.GetValue<string>()=="retail-columns-1","Retail storage uses versioned columns");
        var rows=GameEngine.Deserialize(JsonSerializer.Serialize(queued.State));
        check(rows.Serialize()==encoded,"Existing v8 row saves and new columns preserve all cash, costs, reservations and random state");
        var corrupt=JsonNode.Parse(encoded)!; corrupt["Retail"]!["Count"]=10001;
        Reject(corrupt,"Oversized retail column population rejected");
        corrupt=JsonNode.Parse(encoded)!; corrupt["Retail"]!.AsObject().Remove("Encoding");
        Reject(corrupt,"Missing storage fields use recoverable JSON errors");
        corrupt=JsonNode.Parse(encoded)!; corrupt["Retail"]!["Columns"]!["UnrecognizedCash"]=new JsonArray(123);
        Reject(corrupt,"Unknown cash columns cannot bypass ledger validation");
        var positions=new JsonArray(0,0,1,0,0,1);
        corrupt=JsonNode.Parse(encoded)!; corrupt["Retail"]!["Columns"]!["Shares"]=new JsonObject { ["Length"]=30,["Values"]=positions };
        Reject(corrupt,"Duplicate sparse holdings rejected");
        void Reject(JsonNode json,string message)
        {
            try { GameEngine.Deserialize(json.ToJsonString()); check(false,message); }
            catch(Exception e) when(e is JsonException or InvalidDataException) { check(true,message); }
        }
    }
}
