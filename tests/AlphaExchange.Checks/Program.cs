using AlphaExchange.Core;

long checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
var engine = new GameEngine(12345);
Check(engine.State.Bots.Count == 100, "Exactly 100 AI traders");
Check(engine.State.Bots.Select(t => t.Id).Distinct().Count() == 100, "Unique identities");
Check(engine.Ranking().Count == 100 && engine.Ranking().All(t => t.Id > 0), "Only 100 AIs, no player");
Check(engine.State.Bots.Select(t => t.Strategy).Distinct().Count() == 6, "Six strategies");
var bot = engine.State.Bots[0];
long initial = bot.Cash;
int price = engine.State.Stocks[0].Price;
Check(engine.Trade(bot, 0, 10, true) is null, "AI buy succeeds");
Check(bot.Cash == initial - price * 10 - GameEngine.Fee(price * 10), "Buy fee exact");
Check(bot.Shares[0] == 10, "Buy holdings");
long cashBeforeInvalid = bot.Cash;
Check(engine.Trade(bot, 0, 11, false) is not null, "No overselling");
Check(engine.Trade(bot, 0, int.MaxValue, true) is not null, "No unaffordable buys");
Check(engine.Trade(bot, -1, 1, true) is not null, "No invalid index");
Check(engine.Trade(bot, 0, -1, true) is not null, "No negative quantity");
Check(bot.Cash == cashBeforeInvalid, "Invalid trades atomic");
Check(engine.Trade(bot, 0, 10, false) is null, "AI sell succeeds");
Check(bot.Cash == initial - 2 * GameEngine.Fee(price * 10), "Round trip only loses fees");
Check(bot.AverageCost[0] == 0, "Cost basis reset");
int max = engine.MaxBuy(bot, 0);
Check(engine.Trade(bot, 0, max, true) is null, "Max affordable quantity correct");
Check(engine.MaxBuy(bot, 0) == 0, "No hidden buying power");

// User's exact timing contract, including fractional ticks and speed changes.
var clock = new GameEngine(8);
Check(clock.AdvanceTime(4.9) == 0 && clock.State.CompletedHours == 0, "No hour before five seconds");
Check(clock.AdvanceTime(.1) == 1 && clock.State.Hour == 1, "Exactly five seconds per hour");
Check(clock.AdvanceTime(115) == 23 && clock.State.CompletedDays == 1 && clock.State.Day == 2 && clock.State.Hour == 0, "120 seconds = one day");
var fractional = new GameEngine(9);
for (int i = 0; i < 1200; i++) fractional.AdvanceTime(.1);
Check(fractional.State.CompletedHours == 24, "Timer tick accumulation has no drift");
var faster = new GameEngine(10);
faster.AdvanceTime(24, 5);
Check(faster.State.CompletedHours == 24, "5x is 24 real seconds per day");
var speedChange = new GameEngine(11);
speedChange.AdvanceTime(2.5); speedChange.AdvanceTime(1.25, 2);
Check(speedChange.State.CompletedHours == 1, "Changing speed preserves partial hour");

engine.AdvanceTime(67.25);
var restored = GameEngine.Deserialize(engine.Serialize());
for (int i = 0; i < 30; i++) { engine.AdvanceTime(5); restored.AdvanceTime(5); }
Check(engine.Serialize() == restored.Serialize(), "Save restores fractional clock, portfolios, RNG");
Check(engine.State.TotalAiTrades > 100, "AI trading activity");

long totalTrades = 0;
for (uint seed = 1; seed <= 40; seed++)
{
    var game = new GameEngine(seed);
    for (int hour = 0; hour < 720; hour++)
    {
        game.AdvanceHour();
        foreach (var trader in game.State.Bots)
        {
            Check(trader.Cash >= 0, "Cash nonnegative");
            Check(trader.Shares.All(q => q >= 0), "Holdings nonnegative");
            Check(trader.Equity(game.State.Stocks) > 0, "Equity positive");
        }
        Check(game.State.Stocks.All(s => s.Price >= 100 && double.IsFinite(s.FairValue)), "Prices finite");
        Check(game.State.Stocks.All(s => Math.Abs(s.HourlyChange) < .041), "Hourly volatility bounded");
    }
    Check(game.State.Finished && game.State.Day == 30, "Season ends at 720 hours");
    Check(game.State.Bots.All(t => t.Trades > 0), "Every AI trades");
    Check(game.State.Bots.All(t => t.EquityHistory.Count == 721), "Every AI records hourly equity");
    string snapshot = game.Serialize(); game.AdvanceTime(999); game.AdvanceHour();
    Check(game.Serialize() == snapshot, "Finished state frozen");
    Check(GameEngine.Deserialize(snapshot).Serialize() == snapshot, "Finished state save roundtrip");
    var ranks = game.Ranking();
    Check(ranks.Zip(ranks.Skip(1)).All(pair => pair.First.Equity(game.State.Stocks) >= pair.Second.Equity(game.State.Stocks)), "Ranking correctly sorted");
    totalTrades += game.State.TotalAiTrades;
}
var sixty = new GameEngine(12); sixty.AdvanceTime(3600);
Check(sixty.State.Finished, "Full 30-day season = 60 real minutes at 1x");
try { GameEngine.Deserialize("{}"); throw new Exception("Corrupt save accepted"); } catch (InvalidDataException) { checks++; }
Console.WriteLine($"PASS · {checks:N0} assertions · 40 full 720-hour seasons · {totalTrades:N0} AI trades");
Console.WriteLine("PASS · 5 seconds/hour · 120 seconds/day · fractional timing · 1x/2x/5x · deterministic save/resume");
