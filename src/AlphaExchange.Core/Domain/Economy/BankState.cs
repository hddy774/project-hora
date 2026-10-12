namespace AlphaExchange.Core;

public sealed class BankState : ICashAccount
{
    public Trader MarketAccount { get; set; } = new() { Id = 0, Name = "시장안정은행" };
    public long InterventionPurchases { get; set; }
    public long InterventionSales { get; set; }
    public long LoanWriteOffs { get; set; }
    public long ShortWriteOffs { get; set; }
    public string MarketDecision { get; set; } = "초기 유동성 공급";
    public long Cash { get; set; } = 1_000_000_000;
    public long InterestIncome { get; set; }
    public long OperatingSpending { get; set; }
    public long DividendIncome { get; set; }
    public long CorporateInterest { get; set; }
    public long BorrowFeeIncome { get; set; }
    public long[] ShareInventory { get; set; } = new long[MarketDefaults.StockCount];
    public long[] ReservedLending { get; set; } = new long[MarketDefaults.StockCount];
}

public sealed record LoanOffer(CreditRating Rating, double AssetRatio, double AnnualRate, long Limit, long Available);
