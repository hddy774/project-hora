namespace AlphaExchange.Core;
public sealed record SectorStatistics(string Name, int Companies, long Capitalization, double Change, long Volume, long Turnover, long InstitutionShares, long RetailShares);
public sealed class MarketStatistics
{
    public FinancialStatement Institutions { get; set; } = new();
    public FinancialStatement Retail { get; set; } = new();
    public List<SectorStatistics> Sectors { get; set; } = [];
    public long SuccessfulInvestments { get; set; }
    public long CompletedInvestments { get; set; }
    public int AllocationRecoveryCount { get; set; }
    public int[] CashDistribution { get; } = new int[5];
    public List<CompanyLiquidity> CompanyBooks { get; } = [];
    public int TwoSidedCompanies { get; set; }
    public double AverageSpread { get; set; }
    public long Capitalization { get; set; }
    public double Change { get; set; }
    public long DayVolume { get; set; }
    public long DayTurnover { get; set; }
    public long BidQuantity { get; set; }
    public long AskQuantity { get; set; }
    public int Rising { get; set; }
    public int Falling { get; set; }
    public int Unchanged { get; set; }
    public double InstitutionTradingIncome { get; set; }
    public double RetailTradingIncome { get; set; }
}
