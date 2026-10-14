using AlphaExchange.Core;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Text.Json.Nodes;

string mode = args[0], folder = Path.GetFullPath(args[1]);
if (mode is "generate" or "generate-v9")
{
    Directory.CreateDirectory(folder);
    var game = new GameEngine(17026);
    int expected = mode == "generate" ? 8 : 9;
    if (game.State.Version != expected) throw new Exception("Fixture must use its actual released core");
    var store = new GameStore(folder); store.Attach(game); store.Save(game);
    for (int hour = 0; hour < 168; hour++) { game.AdvanceHour(); if (game.State.CompletedHours % 24 == 0) store.Save(game); }
    using var db = new SqliteConnection("Data Source=" + store.SavePath); db.Open();
    using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT hour,resolution,hash FROM hours WHERE run=$run ORDER BY hour,resolution";
    cmd.Parameters.AddWithValue("$run", game.State.RunId); using var reader = cmd.ExecuteReader();
    var rows = new List<object>(); while (reader.Read()) rows.Add(new { hour = reader.GetInt64(0), resolution = reader.GetInt32(1), hash = reader.GetString(2) });
    File.WriteAllText(Path.Combine(folder, "baseline.json"), JsonSerializer.Serialize(new { version = expected, run = game.State.RunId, hour = 168, rows, random = game.State.RandomState,
        prices = game.State.Stocks.Select(s => s.Price), cash = game.Participants.Select(t => t.Cash), shares = game.Participants.Select(t => t.Shares) }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"True v{expected} fixture: {rows.Count} immutable rows;previous market;168 hours");
}
else if (mode == "inspect-upgrade")
{
    var store = new GameStore(folder);
    var game = store.Load(out string message) ?? throw new Exception(message);
    var baseline = JsonNode.Parse(File.ReadAllText(args[2]))!;
    var cash = baseline["cash"]!.AsArray().Select(x => x!.GetValue<long>());
    var shares = baseline["shares"]!.AsArray().SelectMany(row => row!.AsArray().Select(x => x!.GetValue<long>()));
    if (game.State.Version != 10 || game.State.RunId != baseline["run"]!.GetValue<string>() || game.State.CompletedMinutes != 168 * 60 ||
        game.State.RandomState != baseline["random"]!.GetValue<uint>() ||
        !game.Participants.Select(t => t.Cash).SequenceEqual(cash) || !game.Participants.SelectMany(t => t.Shares).SequenceEqual(shares) ||
        !game.State.Stocks.Select(s => s.Price).SequenceEqual(baseline["prices"]!.AsArray().Select(x => x!.GetValue<int>())))
        throw new Exception("Native v9 upgrade changed the existing ledger or clock");
    Console.WriteLine("PASS actual v9 native upgrade: exact cash, holdings, prices, RNG, clock and RunId");
}
else if (mode == "inspect")
{
    var store = new GameStore(folder); var game = store.Load(out string message) ?? throw new Exception("Native save missing: " + message);
    if (message.Length != 0) throw new Exception("Native save required fallback: " + message);
    var json = JsonNode.Parse(game.Serialize())!;
    if (game.State.Version != 10 || json["World"] is not null || game.State.Bots.Count != 100 ||
        game.State.Stocks.Count != 15 || game.State.Stocks.Select(s => s.Sector).Distinct().Count() != 5 ||
        game.State.Retail.Count != 10000 || game.SystemCash() != game.State.InitialSystemCash)
        throw new Exception("Native investor market, catalog or cash invalid");
    if (game.State.CompletedHours <= 0 || !game.HistorySource!.Covers(0, game.State.CompletedHours)) throw new Exception("Native hourly history has gaps or failed to advance");
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        game.State.Version,
        game.State.RunId,
        game.State.CompletedHours,
        minutes = json["CompletedMinutes"]!.GetValue<long>(),
        pending = json["PendingClockMinutes"]!.GetValue<int>(),
        investors = game.State.Bots.Count,
        stocks = game.State.Stocks.Count,
        retail = game.State.Retail.Count,
        cashDifference = game.SystemCash() - game.State.InitialSystemCash
    }));
}
else throw new ArgumentException("generate or inspect required");
