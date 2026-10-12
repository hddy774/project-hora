namespace AlphaExchange.Core;

public enum MarketEventKind { Company, Policy, Finance, Sector }

public sealed class MarketEnvironment
{
    public MarketRules Rules { get; set; } = MarketRules.Default();
    public double EconomicActivity { get; set; } = 1;
    public double Inflation { get; set; } = .02;
    public int NumberFormat { get; set; }
    public long NextEventMinute { get; set; }
    public long EventsPublished { get; set; }
}
