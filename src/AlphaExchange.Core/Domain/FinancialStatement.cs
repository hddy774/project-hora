
namespace AlphaExchange.Core;

public sealed class FinancialStatement
{
    public long StaffCosts { get; set; }
    public long TaxDebt { get; set; }
    public long TaxesPaid { get; set; }
    public int Count { get; set; }
    public long Cash { get; set; }
    public long Holdings { get; set; }
    public double Cost { get; set; }
    public long OpeningCash { get; set; }
    public long OpeningEquity { get; set; }
    public double OpeningUnrealized { get; set; }
    public double RealizedProfit { get; set; }
    public long Fees { get; set; }
    public long Purchases { get; set; }
    public long Sales { get; set; }
    public long ReservedCash { get; set; }
    public long Trades { get; set; }
    public long ShortDebt { get; set; }
    public double ShortEntry { get; set; }
    public long LoanDebt { get; set; }
    public long FineDebt { get; set; }
    public long Borrowed { get; set; }
    public long Repaid { get; set; }
    public long InterestExpense { get; set; }
    public long InterestPaid { get; set; }
    public long BorrowFees { get; set; }
    public long Taxes { get; set; }
    public long Fines { get; set; }
    public long FinesPaid { get; set; }
    public long Subsidies { get; set; }
    public long DividendIncome { get; set; }
    public long DividendTax { get; set; }
    public long ShortDividendExpense { get; set; }
    public long ShortDividendPaid { get; set; }
    public long ShortDividendDebt { get; set; }
    public long WageIncome { get; set; }
    public long Consumption { get; set; }
    public long DebtRelief { get; set; }
    public long Assets => Cash + Holdings;
    public long Liabilities => ShortDebt + LoanDebt + FineDebt + ShortDividendDebt + TaxDebt;
    public long Equity => Assets - Liabilities;
    public double UnrealizedProfit => Holdings - Cost + ShortEntry - ShortDebt;
    public double ValuationChange => UnrealizedProfit - OpeningUnrealized;
    public double NetIncome => RealizedProfit + ValuationChange - Fees - Taxes - InterestExpense - BorrowFees - Fines + Subsidies + DebtRelief + DividendIncome - DividendTax - ShortDividendExpense + WageIncome - Consumption - StaffCosts;
    public long NetCashFlow => Sales - Purchases - Fees - TaxesPaid - InterestPaid - BorrowFees - FinesPaid + Subsidies + Borrowed - Repaid + DividendIncome - DividendTax - ShortDividendPaid + WageIncome - Consumption - StaffCosts;
    public void Add(FinancialStatement s)
    {
        TaxesPaid += s.TaxesPaid; TaxDebt += s.TaxDebt;
        Count += s.Count; Cash += s.Cash; Holdings += s.Holdings; Cost += s.Cost; OpeningCash += s.OpeningCash;
        OpeningEquity += s.OpeningEquity; OpeningUnrealized += s.OpeningUnrealized; RealizedProfit += s.RealizedProfit;
        Fees += s.Fees; Purchases += s.Purchases; Sales += s.Sales; ReservedCash += s.ReservedCash; Trades += s.Trades;
        ShortDebt += s.ShortDebt; ShortEntry += s.ShortEntry; LoanDebt += s.LoanDebt; FineDebt += s.FineDebt;
        Borrowed += s.Borrowed; Repaid += s.Repaid; InterestExpense += s.InterestExpense; InterestPaid += s.InterestPaid;
        BorrowFees += s.BorrowFees; Taxes += s.Taxes; Fines += s.Fines; FinesPaid += s.FinesPaid; Subsidies += s.Subsidies; DebtRelief += s.DebtRelief;
        DividendIncome += s.DividendIncome; DividendTax += s.DividendTax; ShortDividendExpense += s.ShortDividendExpense;
        ShortDividendPaid += s.ShortDividendPaid; ShortDividendDebt += s.ShortDividendDebt;
        WageIncome += s.WageIncome; Consumption += s.Consumption;
        StaffCosts += s.StaffCosts;
    }
}
