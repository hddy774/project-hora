namespace AlphaExchange.Core;

public sealed class GovernmentPolicy
{
    public long Season { get; set; } = 1;
    public string Name { get; set; } = "균형 성장";
    public double TaxRate { get; set; } = .10;
    public int FeeBasisPoints { get; set; } = 15;
    public double BaseRate { get; set; } = .025;
    public bool ShortSellingAllowed { get; set; } = true;
    public double ShortExposureLimit { get; set; } = .15;
    public double LoanLimitMultiplier { get; set; } = 1;
    public double SubsidyRate { get; set; } = .00005;
    public double Enforcement { get; set; } = .5;
}
public sealed class GovernmentState
{
    public long Cash { get; set; } = 100_000_000;
    public long Taxes { get; set; }
    public long Fines { get; set; }
    public long Subsidies { get; set; }
    public GovernmentPolicy Policy { get; set; } = new();
    public List<GovernmentPolicy> History { get; set; } = [];
}
public sealed class BankState
{
    public long Cash { get; set; } = 1_000_000_000;
    public long InterestIncome { get; set; }
    public long BorrowFeeIncome { get; set; }
    public int[] ShareInventory { get; set; } = new int[GameEngine.StockCount];
    public int[] ReservedLending { get; set; } = new int[GameEngine.StockCount];
}
public sealed record LoanOffer(CreditRating Rating, double AssetRatio, double AnnualRate, long Limit, long Available);
public sealed class CompanyReport
{
    public long Season { get; set; } = 1;
    public long Revenue { get; set; }
    public long OperatingCosts { get; set; }
    public long Interest { get; set; }
    public long Tax { get; set; }
    public long Cash { get; set; }
    public long Receivables { get; set; }
    public long Inventory { get; set; }
    public long FixedAssets { get; set; }
    public long Debt { get; set; }
    public long OpeningCash { get; set; }
    public long OpeningEquity { get; set; }
    public long OperatingCashFlow { get; set; }
    public long InvestingCashFlow { get; set; }
    public long FinancingCashFlow { get; set; }
    public long Dividends { get; set; }
    public long CapitalChange { get; set; }
    public int NewsCount { get; set; }
    public double NewsImpact { get; set; }
    public long OperatingProfit => Revenue - OperatingCosts;
    public long NetIncome => OperatingProfit - Interest - Tax;
    public long Assets => Cash + Receivables + Inventory + FixedAssets;
    public long Equity => Assets - Debt;
    public double Margin => (double)NetIncome / Math.Max(1, Revenue);
    public double DebtRatio => (double)Debt / Math.Max(1, Assets);
    public double ReturnOnEquity => (double)NetIncome / Math.Max(1, OpeningEquity);
}
public enum OperationStatus { Active, Completed, Failed }
public sealed class InstitutionOperation
{
    public long Id { get; set; }
    public int LeaderId { get; set; }
    public int PartnerId { get; set; }
    public int StockIndex { get; set; }
    public long StartedHour { get; set; }
    public long EndsHour { get; set; }
    public int OpeningPrice { get; set; }
    public OperationStatus Status { get; set; }
    public long Fine { get; set; }
    public string Detail { get; set; } = "대표 간 공동 매집 제안";
    public double TargetReturn { get; set; } = .008;
}
public sealed class TraderSnapshot
{
    public int Id { get; set; }
    public long Equity { get; set; }
    public long Cash { get; set; }
    public double NetIncome { get; set; }
    public long Volume { get; set; }
    public long Turnover { get; set; }
}
public sealed class DailySnapshot
{
    public long Hour { get; set; }
    public long Capitalization { get; set; }
    public long Volume { get; set; }
    public long Turnover { get; set; }
    public long InstitutionEquity { get; set; }
    public long RetailEquity { get; set; }
    public long InstitutionCash { get; set; }
    public long RetailCash { get; set; }
    public List<TraderSnapshot> Institutions { get; set; } = [];
    public long[] StockVolumes { get; set; } = [];
    public long[] StockTurnovers { get; set; } = [];
    public int[] StockPrices { get; set; } = [];
}
public sealed record PeriodStatistics(DailySnapshot Start, DailySnapshot End, IReadOnlyList<DailySnapshot> Points, bool Partial)
{
    public long Volume => End.Volume - Start.Volume;
    public long Turnover => End.Turnover - Start.Turnover;
    public double Change => (double)End.Capitalization / Math.Max(1, Start.Capitalization) - 1;
}
