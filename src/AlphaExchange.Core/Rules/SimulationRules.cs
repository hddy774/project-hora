using System.Text.Json;

namespace AlphaExchange.Core;

// Editable offline rule script. A complete snapshot travels with each save so
// tuning future new markets never silently changes an existing market's rules.
public sealed class SimulationRules
{
    public string Version { get; set; } = "";
    public RepresentativeRules Representative { get; set; } = new();
    public RewardRule[] Rewards { get; set; } = [];
    public HorizonRule[] Horizons { get; set; } = [];
    public InvestmentRules Investment { get; set; } = new();
    public PerformanceRules Performance { get; set; } = new();
    static readonly Lazy<SimulationRules> defaults = new(() =>
    {
        using var stream = typeof(SimulationRules).Assembly.GetManifestResourceStream("AlphaExchange.InstitutionRules")
            ?? throw new InvalidDataException("기관 규칙 스크립트가 없습니다.");
        return FromJson(new StreamReader(stream).ReadToEnd());
    });
    public static SimulationRules Default() => defaults.Value.Copy();
    public SimulationRules Copy() => FromJson(JsonSerializer.Serialize(this));
    public static SimulationRules FromJson(string json)
    {
        var result = JsonSerializer.Deserialize<SimulationRules>(json,
            new JsonSerializerOptions { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow })
            ?? throw new InvalidDataException("비어 있는 기관 규칙");
        result.Validate(); return result;
    }
    public void Validate()
    {
        static bool Finite(double v, double low, double high) => double.IsFinite(v) && v >= low && v <= high;
        var r = Representative; var i = Investment; var p = Performance;
        if (string.IsNullOrWhiteSpace(Version) || Version.Length > 64 || r is null || i is null || p is null ||
            r.InitialAbility is < 1 or > 100 || r.AbilityMaximum != 100 || r.PointPriorities is null || r.PointPriorities.Length != 10 ||
            r.PointPriorities.Any(x => !Finite(x, .01, 10)) || r.MaximumCreditBonus is < 0 or > 30 || !Finite(r.CreditRetention, 0, 1) ||
            Rewards is null || Rewards.Length != 9 || !Rewards.Select(x => x?.MaximumRank ?? 0).SequenceEqual(new[] { 1, 2, 3, 5, 10, 20, 30, 40, 50 }) ||
            Rewards.Any(x => x is null || x.AbilityPoints is < 0 or > 100 || x.CreditBonus is < 0 or > 30) ||
            Rewards.Zip(Rewards.Skip(1)).Any(x => x.First.AbilityPoints < x.Second.AbilityPoints || x.First.CreditBonus < x.Second.CreditBonus))
            throw new InvalidDataException("대표·보상 규칙 손상");
        if (Horizons is null || Horizons.Length != 3 || Horizons.Any(x => x is null) || Horizons[0].MinimumHours != 1 || Horizons[0].MaximumHours != 24 ||
            Horizons[1].MinimumHours != 24 || Horizons[1].MaximumHours != 240 || Horizons[2].MinimumHours != 240 || Horizons[2].MaximumHours is not null ||
            Horizons.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.ReviewHours is < 1 or > 720 ||
                x.ExpectedHours < x.MinimumHours || !Finite(x.ValueWeight, 0, 2) || !Finite(x.TrendWeight, 0, 2) || !Finite(x.FastWeight, -2, 2) ||
                !Finite(x.NewsWeight, 0, 2) || !Finite(x.DividendWeight, 0, 2)))
            throw new InvalidDataException("투자 기간 규칙 손상");
        if (i.MinimumCash != .05 || i.MaximumCash != .30 || !Finite(i.BaseCash, i.MinimumCash, i.MaximumCash) ||
            i.DispositionCash is null || i.DispositionCash.Length != 5 || i.DispositionCash.Any(x => !Finite(x, -.25, .25)) ||
            !Finite(i.RiskCashEffect, 0, .25) || !Finite(i.DefensiveCash, 0, .25) || !Finite(i.CashTolerance, 0, .1) ||
            !Finite(i.StockLimit, .05, .5) || !Finite(i.DiversificationStockEffect, 0, i.StockLimit - .01) ||
            !Finite(i.SectorLimit, i.StockLimit, 1) || !Finite(i.DiversificationSectorEffect, 0, i.SectorLimit - i.StockLimit) ||
            i.OrderAttempts is < 1 or > 10 || !Finite(i.OrderRatio, .001, .1) || !Finite(i.OrderSkillRatio, 0, .1) ||
            !Finite(i.BaseSpread, 0, .05) ||
            !Finite(i.TakeLiquidityScore, 0, 1) || !Finite(i.ThesisBreakValue, -1, 0) ||
            !Finite(i.LongDiscount, 0, 1) || !Finite(i.FastMove, 0, 1) || !Finite(i.MaterialNews, 0, 1) ||
            !Finite(i.FallingMarket, -1, 0) || !Finite(i.FallingStock, -1, 0) || !Finite(i.VolatilityRisk, 0, 1) || !Finite(i.DrawdownRisk, 0, 1) ||
            i.CreditBorrowMinimum is < 0 or > 99 || !Finite(i.MaximumLoanRatio, 0, .3) || !Finite(i.BorrowScore, 0, 1) ||
            !Finite(i.MaximumShortRatio, 0, .15) || !Finite(i.ShortScore, -1, 0) || !Finite(i.ShortExitReturn, 0, 1) || !Finite(i.SkillFloor, 0, 1) ||
            p.TickMilliseconds is < 16 or > 1000 || p.FrameMilliseconds < p.TickMilliseconds || p.FrameMilliseconds > 1000 ||
            p.MaximumCatchupHours is < 1 or > 100 || p.TickBudgetMilliseconds is < 1 or > 40)
            throw new InvalidDataException("투자·유동성·실행 규칙 손상");
    }
}
public sealed class RepresentativeRules
{
    public int InitialAbility { get; set; }
    public int AbilityMaximum { get; set; }
    public double[] PointPriorities { get; set; } = [];
    public int MaximumCreditBonus { get; set; }
    public double CreditRetention { get; set; }
}
public sealed record RewardRule(int MaximumRank, int AbilityPoints, int CreditBonus);
public sealed class HorizonRule
{
    public string Name { get; set; } = "";
    public int MinimumHours { get; set; }
    public int? MaximumHours { get; set; }
    public int ReviewHours { get; set; }
    public int ExpectedHours { get; set; }
    public double ValueWeight { get; set; }
    public double TrendWeight { get; set; }
    public double FastWeight { get; set; }
    public double NewsWeight { get; set; }
    public double DividendWeight { get; set; }
}
public sealed class InvestmentRules
{
    public double MinimumCash { get; set; }
    public double MaximumCash { get; set; }
    public double BaseCash { get; set; }
    public double RiskCashEffect { get; set; }
    public double[] DispositionCash { get; set; } = [];
    public double DefensiveCash { get; set; }
    public double CashTolerance { get; set; }
    public double StockLimit { get; set; }
    public double DiversificationStockEffect { get; set; }
    public double SectorLimit { get; set; }
    public double DiversificationSectorEffect { get; set; }
    public int OrderAttempts { get; set; }
    public double OrderRatio { get; set; }
    public double OrderSkillRatio { get; set; }
    public double BaseSpread { get; set; }
    public double TakeLiquidityScore { get; set; }
    public double ThesisBreakValue { get; set; }
    public double LongDiscount { get; set; }
    public double FastMove { get; set; }
    public double MaterialNews { get; set; }
    public double FallingMarket { get; set; }
    public double FallingStock { get; set; }
    public double VolatilityRisk { get; set; }
    public double DrawdownRisk { get; set; }
    public int CreditBorrowMinimum { get; set; }
    public double MaximumLoanRatio { get; set; }
    public double BorrowScore { get; set; }
    public double MaximumShortRatio { get; set; }
    public double ShortScore { get; set; }
    public double ShortExitReturn { get; set; }
    public double SkillFloor { get; set; }
}
public sealed class PerformanceRules
{
    public int TickMilliseconds { get; set; }
    public int FrameMilliseconds { get; set; }
    public int MaximumCatchupHours { get; set; }
    public int TickBudgetMilliseconds { get; set; }
}
