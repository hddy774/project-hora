namespace AlphaExchange.Core;

public enum BankruptcyKind { Company, Institution, Retail }
public sealed class BankruptcyRecord
{
    public long Id { get; set; }
    public long Hour { get; set; }
    public BankruptcyKind Kind { get; set; }
    public string EntityId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Generation { get; set; }
    public string SuccessorId { get; set; } = "";
    public string Reason { get; set; } = "";
    public long Assets { get; set; }
    public long Liabilities { get; set; }
    public long Equity { get; set; }
    public long LoanWriteOff { get; set; }
    public long TradeWriteOff { get; set; }
    public long ShortWriteOff { get; set; }
    public long FineWriteOff { get; set; }
    public long TaxWriteOff { get; set; }
    public long ReplacementCapital { get; set; }
    public double TradingIncome { get; set; }
    public CompanyReport? Report { get; set; }
}
public sealed class BankruptcyTotals
{
    public long Companies { get; set; }
    public long Institutions { get; set; }
    public long Retail { get; set; }
    public long CompanyCapital { get; set; }
    public long InstitutionCapital { get; set; }
    public long RetailCapital { get; set; }
    public double ClosedInstitutionTradingIncome { get; set; }
    public double ClosedRetailTradingIncome { get; set; }
    public long Total => Companies + Institutions + Retail;
}
