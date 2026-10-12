namespace AlphaExchange.Core;

public sealed class CompanyReport : ICashAccount
{
    public long TradePayables { get; set; }
    public string SecurityId { get; set; } = "";
    public bool IsOpening { get; set; }
    public int AccountingBasis { get; set; } = 5;
    public long AverageShares { get; set; }
    public long OutstandingShares { get; set; }
    public double SplitFactor { get; set; } = 1;
    public long DividendsPerShare { get; set; }
    public long Depreciation { get; set; }
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
    public long OperatingProfit => Revenue - OperatingCosts - Depreciation;
    public long NetIncome => OperatingProfit - Interest - Tax;
    public long Assets => Cash + Receivables + Inventory + FixedAssets;
    public long Liabilities => Debt + TradePayables;
    public long Equity => Assets - Liabilities;
    public double Margin => (double)NetIncome / Math.Max(1, Revenue);
    public double DebtRatio => (double)Liabilities / Math.Max(1, Assets);
    public double ReturnOnEquity => (double)NetIncome / Math.Max(1, OpeningEquity);
}
