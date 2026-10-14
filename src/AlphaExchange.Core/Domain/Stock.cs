using System.Text.Json.Serialization;

namespace AlphaExchange.Core;

public sealed class Stock
{
    public double BusinessSalesFactor { get; set; } = 1;
    public int Generation { get; set; } = 1;
    public int InsolventMonths { get; set; }
    public bool WaitingForCapital { get; set; }
    public CompanyStrategy CompanyStrategy { get; set; } = CompanyStrategy.Value;
    public ManagementPolicy Management { get; set; } = ManagementPolicy.Efficiency;
    public CapitalAllocation Allocation { get; set; } = CapitalAllocation.Buyback;
    public double TreasuryTarget { get; set; } = .65;
    public string SecurityId { get; set; } = "";
    public bool Active { get; set; } = true;
    public string Business { get; set; } = "";
    public string Driver { get; set; } = "";
    public string NewsExposure { get; set; } = "";
    public string CapitalPolicy { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string Sector { get; set; } = "";
    public int Price { get; set; }
    public int PreviousPrice { get; set; }
    public decimal LimitReferencePrice { get; set; }
    public long NextCompanyNewsMinute { get; set; }
    public long CompanyNewsCount { get; set; }
    public int DayOpenPrice { get; set; }
    public int StartPrice { get; set; }
    public double FairValue { get; set; }
    public double Volatility { get; set; }
    public double Sentiment { get; set; }
    public decimal FractionalMark { get; set; }
    [JsonIgnore] public decimal MarkPrice => Price + FractionalMark;
    public long TreasuryShares { get; set; }
    public long FounderShares { get; set; }
    public long ParValue { get; set; } = 100;
    public double SplitFactor { get; set; } = 1;
    public double CostRatio { get; set; } = .78;
    public double DemandSensitivity { get; set; } = 1;
    public double DividendPayout { get; set; } = .33;
    public bool FundedIpo { get; set; }
    public long ListedSeason { get; set; } = 1;
    public long BaseRevenue { get; set; }
    public long InitialFixedAssets { get; set; }
    public long PendingDividends { get; set; }
    public long PendingDividendPerShare { get; set; }
    public long PendingCapitalChange { get; set; }
    public long PendingFinancingFlow { get; set; }
    public long PendingInvestingFlow { get; set; }
    public long ShareHourSum { get; set; }
    public long ShareHours { get; set; }
    public int LastTradePrice { get; set; }
    public long LastDividendPerShare { get; set; }
    public long TotalDividends { get; set; }
    [JsonIgnore] public long OutstandingShares => TotalShares - TreasuryShares;
    [JsonIgnore] public long FloatShares => Math.Max(0, OutstandingShares - FounderShares);
    [JsonIgnore] public long MarketCap => Active ? Value(OutstandingShares) : 0;
    [JsonIgnore] public long FloatMarketCap => Active ? Value(FloatShares) : 0;
    public long Value(long quantity) => FractionalMark == 0 ? ShareValuation.Whole(Price, quantity)
        : ShareValuation.Fractional(MarkPrice, quantity);
    [JsonIgnore] public double DividendYield => (double)Reports.Where(r => r.AccountingBasis == 5).Sum(r => r.DividendsPerShare * r.SplitFactor / SplitFactor) / Math.Max(1, Price);
    [JsonIgnore] public double TtmEps => Reports.Where(r => r.AccountingBasis == 5 && !r.IsOpening).Sum(r => (double)r.NetIncome / Math.Max(1, r.AverageShares) * r.SplitFactor / SplitFactor);
    [JsonIgnore] public int EpsMonths => Reports.Count(r => r.AccountingBasis == 5 && !r.IsOpening);
    [JsonIgnore]
    public double AnnualEps => EpsMonths == 0 ? (double)Report.NetIncome * 12 / Math.Max(1, OutstandingShares)
        : EpsMonths < 12 ? TtmEps * 12 / EpsMonths : TtmEps;
    public long TotalShares { get; set; }
    public int HourlyBidParticipants { get; set; }
    public int HourlyAskParticipants { get; set; }
    public long LastCompetitionHour { get; set; } = -1;
    public long Volume { get; set; }
    public long DayVolume { get; set; }
    public long DayTurnover { get; set; }
    public long TotalVolume { get; set; }
    public long TotalTurnover { get; set; }
    public List<double> History { get; set; } = [];
    public CompanyReport Report { get; set; } = new();
    public List<CompanyReport> Reports { get; set; } = [];
    public double SeasonNewsImpact { get; set; }
    public int SeasonNewsCount { get; set; }
    [JsonIgnore] public double Change => (double)Price / DayOpenPrice - 1;
    [JsonIgnore] public double HourlyChange => (double)Price / PreviousPrice - 1;
}
