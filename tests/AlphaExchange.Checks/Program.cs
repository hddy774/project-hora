using AlphaExchange.Core;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

long assertions = 0;
void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
void Near(double a, double b, string message, double tolerance = .02) => Check(Math.Abs(a - b) < tolerance, $"{message}: {a} != {b}");
void Validate(GameEngine game)
{
    var s = game.State;
    Check(s.Bots.Count == 100 && s.Retail.Count == 10000, "Participant counts");
    Check(game.Participants.Sum(t => t.Cash) + s.FeePool == s.InitialSystemCash, "Closed-system cash conservation");
    long[] shares = new long[8];
    foreach (var t in game.Participants)
    {
        Check(t.Cash >= 0 && t.ReservedCash >= 0 && t.ReservedCash <= t.Cash, "Cash collateral");
        for (int i = 0; i < 8; i++) { Check(t.Shares[i] >= t.ReservedShares[i] && t.ReservedShares[i] >= 0, "Share collateral"); shares[i] += t.Shares[i]; }
        var f = t.Financials(s.Stocks);
        Check(f.OpeningCash + f.NetCashFlow == t.Cash, "Cash-flow reconciliation");
        Near(f.OpeningEquity + f.NetIncome, f.Equity, "Income/equity reconciliation");
    }
    for (int i = 0; i < 8; i++)
    {
        Check(shares[i] == s.Stocks[i].TotalShares, "Share conservation");
        var bids = game.Orders(i, true); var asks = game.Orders(i, false);
        Check(bids.Count == 0 || asks.Count == 0 || bids[0].Price < asks[0].Price, "No crossed resting book");
        foreach (var book in new[] { bids, asks })
            for (int j = 1; j < book.Count; j++) Check(book[j - 1].Price == book[j].Price ? book[j - 1].Id < book[j].Id : book[j].Buy ? book[j - 1].Price > book[j].Price : book[j - 1].Price < book[j].Price, "Price/time priority");
    }
    Check(s.Tape.Count <= 160 && s.News.Count <= 40 && s.Stocks.All(x => x.History.Count <= 120) && s.Bots.All(t => t.EquityHistory.Count <= 120 && t.SeasonRanks.Count <= 12), "Bounded display buffers");
}
var match = new GameEngine(42);
Check(match.State.Bots.All(t => t.Equity(match.State.Stocks) == 10000000), "Institution capital");
Check(match.State.Retail.All(t => t.Equity(match.State.Stocks) == 100000), "Retail capital 1/100");
int opening = match.State.Stocks[0].Price;
Check(match.SubmitOrder(2, 0, false, 51000, 2) is null, "First ask");
Check(match.SubmitOrder(3, 0, false, 50500, 2) is null, "Better ask");
Check(match.State.Stocks[0].Price == opening && match.State.TotalMatches == 0, "No price change without execution");
Check(match.SubmitOrder(1, 0, true, 52000, 3) is null, "Marketable bid");
Check(match.State.Tape[1].SellerId == 3 && match.State.Tape[1].Quantity == 2 && match.State.Tape[1].Price == 50500, "Better price first");
Check(match.State.Tape[0].SellerId == 2 && match.State.Tape[0].Quantity == 1 && match.State.Stocks[0].Price == 51000, "Partial fill/resting price");
Check(match.Orders(0, false).Single().Remaining == 1, "Remaining quantity");
match.CancelOrders(2);
match.SubmitOrder(4, 0, false, 50900, 1); match.SubmitOrder(5, 0, false, 50900, 1); match.SubmitOrder(1, 0, true, 51000, 1);
Check(match.State.Tape[0].SellerId == 4, "FIFO equal price");
match.CancelOrders(5);
long count = match.State.TotalMatches;
match.SubmitOrder(1, 0, false, 50000, 1); match.SubmitOrder(1, 0, true, 50100, 1);
Check(match.State.TotalMatches == count, "Self-trade prevention"); match.CancelOrders(1);
Check(match.SubmitOrder(1, 0, true, 10000000, 1000000) is not null, "Reject uncovered buy");
Check(match.SubmitOrder(1, 0, false, 100, 1000000) is not null, "Reject uncovered sell");
Check(match.SubmitOrder(10101, 0, true, 100, 1) is not null, "Reject invalid owner");
Validate(match);
var timing = new GameEngine(8);
Check(timing.AdvanceTime(4.99) == 0 && timing.AdvanceTime(.01) == 1, "5 seconds/hour");
Check(timing.AdvanceTime(115) == 23 && timing.State.Day == 2 && timing.State.Hour == 0, "120 seconds/day");
Check(timing.AdvanceTime(6, 20) == 24, "20x: 6 seconds/day");
try { timing.AdvanceTime(1, 10); Check(false, "Unsupported speed"); } catch (ArgumentOutOfRangeException) { }
var expiry = new GameEngine(97);
expiry.SubmitOrder(101, 0, true, 100, 1, 1); long expiryId = expiry.State.NextOrderId - 1;
expiry.AdvanceHour(); expiry.AdvanceHour();
Check(!expiry.State.Orders.Any(o => o.Id == expiryId && o.Remaining > 0), "Order expiry");

string folder = Path.Combine(Path.GetTempPath(), "alpha-checks-" + Guid.NewGuid().ToString("N"));
var store = new GameStore(folder); var game = new GameEngine(20261004);
var times = new List<double>(); var watch = Stopwatch.StartNew();
const int seasons = 16;
for (int hour = 0; hour < seasons * 720; hour++)
{
    long tick = Stopwatch.GetTimestamp(); game.AdvanceHour(); times.Add(Stopwatch.GetElapsedTime(tick).TotalMilliseconds);
    if ((hour + 1) % 120 == 0) Validate(game);
    if ((hour + 1) % 720 == 0)
    {
        long season = game.State.Season - 1;
        var r = game.State.PendingSeasons.Single();
        Check(r.Season == season && r.Standings.Count == 100 && r.Standings.Select(x => x.TraderId).Distinct().Count() == 100, "Season standings completeness");
        Check(r.Standings.Select(x => x.Rank).SequenceEqual(Enumerable.Range(1, 100)), "Season rank ordering");
        Check(r.Standings.Zip(r.Standings.Skip(1)).All(x => x.First.Return >= x.Second.Return), "Rank by season return");
        Check(game.State.Bots.All(t => t.Return(game.State.Stocks) == 0), "Next season opening basis");
        store.Save(game); Check(game.State.PendingSeasons.Count == 0, "Archive flush");
        Check(store.ReadSeason(game.State, season)!.Standings.Count == 100, "Disk archive");
        Console.WriteLine($"Season {season}: matches={game.State.TotalMatches:N0}, feePool={game.State.FeePool:N0}, latest matches={r.Matches:N0}");
    }
}
watch.Stop();
Check(game.State.Season == 17 && !game.State.Finished, "Continuous seasons");
Check(store.ReadSeason(game.State, 1)!.Standings.Count == 100 && game.State.Bots.All(t => t.SeasonRanks.Count == 12), "Old full archive, bounded recent per-AI ranks");
Check(game.State.Retail.All(t => t.Trades > 0), "All 10000 retail participants actually trade");
var stats = game.Statistics();
Near(stats.Institutions.NetIncome + stats.Institutions.OpeningEquity, stats.Institutions.Equity, "Institution aggregate");
Near(stats.Retail.NetIncome + stats.Retail.OpeningEquity, stats.Retail.Equity, "Retail aggregate");
Check(stats.Sectors.Count == 6 && stats.Sectors.Sum(x => x.Capitalization) == stats.Capitalization && stats.Sectors.Sum(x => x.Volume) == stats.DayVolume, "Sector rollup");
string saved = game.Serialize(); var restored = GameEngine.Deserialize(saved);
Check(saved == restored.Serialize(), "Exact save round trip");
for (int i = 0; i < 12; i++) { game.AdvanceHour(); restored.AdvanceHour(); }
Check(game.Serialize() == restored.Serialize(), "Deterministic continuation including book and RNG");
Validate(restored);
store.Save(game); File.WriteAllText(store.SavePath, "{broken");
Check(store.Load(out var recovery) is not null && recovery.Contains("복구"), "Corruption backup recovery");
var legacy = JsonNode.Parse(new GameEngine(123).Serialize())!.AsObject();
legacy["Version"] = 2; legacy["CompletedHours"] = 720; legacy["Retail"] = new JsonArray(); legacy["Orders"] = new JsonArray();
foreach (var node in legacy["Bots"]!.AsArray()) { node!["Fees"] = 1234; node["SeasonOpeningEquity"] = 0; }
var migration = GameEngine.Deserialize(legacy.ToJsonString());
Check(migration.State.MigratedFromV1 && migration.State.Retail.Count == 10000 && migration.State.Season == 2, "v1 finished season migration");
Check(migration.State.Bots.All(t => t.LegacyFees == 1234 && t.Fees == 0) && migration.State.PendingSeasons.Single().Season == 1, "Legacy fees and final ranks preserved");
Validate(migration);
var corrupt = JsonNode.Parse(saved)!; corrupt["Orders"] = new JsonArray(JsonSerializer.SerializeToNode(new LimitOrder { Id = 1, OwnerId = 1, StockIndex = 0, Buy = true, Price = 10000000, Remaining = 1000000, ExpiresAt = game.State.CompletedHours + 3 }));
try { GameEngine.Deserialize(corrupt.ToJsonString()); Check(false, "Reject corrupt reserves"); } catch (InvalidDataException) { }
var saveWatch = Stopwatch.StartNew(); string json = game.Serialize(); saveWatch.Stop();
times.Sort();
Console.WriteLine($"PASS {assertions:N0} assertions; {seasons} seasons; {game.State.TotalMatches:N0} matched trades");
Console.WriteLine($"Simulation wall time {watch.Elapsed.TotalSeconds:F2}s; hour p50={times[times.Count/2]:F2}ms p95={times[(int)(times.Count*.95)]:F2}ms p99={times[(int)(times.Count*.99)]:F2}ms (20x budget 250ms/hour)");
Console.WriteLine($"Save bytes {System.Text.Encoding.UTF8.GetByteCount(json):N0}; serialize {saveWatch.Elapsed.TotalMilliseconds:F1}ms; pending={game.State.PendingSeasons.Count}; active orders={game.State.Orders.Count}");
if (args.Contains("--keep-fixture")) Console.WriteLine($"Fixture directory: {folder}"); else Directory.Delete(folder, true);
