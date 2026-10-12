using System.Text.Json;

namespace AlphaExchange.Core;

// A copy travels with the market so future balance changes affect new runs only.
public sealed class MarketRules
{
    public string Version { get; set; } = "market-1.10";
    public long MarketCapitalization { get; set; }
    public long InvestorCapital { get; set; }
    public long RetailCapital { get; set; }
    public long BankCapital { get; set; }
    public long GovernmentCapital { get; set; }
    public long RealEconomyCapital { get; set; }
    public int InitialRetailUnits { get; set; }
    public int RetailTradeDivisor { get; set; }
    public int ReportIntervalHours { get; set; }
    public int ReportLifetimeHours { get; set; }
    public double AnalystWeight { get; set; }
    public double PlanBandMinimum { get; set; }
    public double PlanBandMaximum { get; set; }
    public double RetailHerdWeight { get; set; }
    public double RetailTakeLiquidityProbability { get; set; }
    static readonly Lazy<MarketRules> defaults = new(() =>
    {
        using var stream = typeof(MarketRules).Assembly.GetManifestResourceStream("AlphaExchange.Core.Rules.market-v1.json")!;
        var result = JsonSerializer.Deserialize<MarketRules>(stream)!;
        result.Validate();
        return result;
    });
    public static MarketRules Default() => defaults.Value.Copy();
    public MarketRules Copy() => (MarketRules)MemberwiseClone();
    public void Validate()
    {
        static bool Range(double x, double min, double max) => double.IsFinite(x) && x >= min && x <= max;
        if (Version != "market-1.10" || MarketCapitalization is < 100_000_000 or > 100_000_000_000_000 ||
            InvestorCapital is <= 0 or > 1_000_000_000_000 || RetailCapital is <= 0 or > 1_000_000_000_000 ||
            BankCapital is < 0 or > 100_000_000_000_000 || GovernmentCapital is < 0 or > 100_000_000_000_000 || RealEconomyCapital is < 0 or > 100_000_000_000_000 ||
            InitialRetailUnits is < 1 or > GameEngine.RetailCount || RetailTradeDivisor is < 1 or > 10000 ||
            ReportIntervalHours is < 1 or > 24 || ReportLifetimeHours < ReportIntervalHours || ReportLifetimeHours > 72 ||
            !Range(AnalystWeight, 0, 1) || !Range(PlanBandMinimum, .001, .1) || !Range(PlanBandMaximum, PlanBandMinimum, .5) ||
            !Range(RetailHerdWeight, 0, .4) || !Range(RetailTakeLiquidityProbability, 0, 1))
            throw new InvalidDataException("시장 규칙 손상");
    }
}
