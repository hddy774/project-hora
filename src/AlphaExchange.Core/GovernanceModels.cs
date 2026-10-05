namespace AlphaExchange.Core;

public enum CompanyStrategy { Growth, Value, Defensive }
public enum ManagementPolicy { Expansion, Efficiency, Liquidity }
public enum CapitalAllocation { Reinvestment, Buyback, Dividend }
public enum VoteKind { Strategy, Management, Capital }
public enum VoteStatus { Open, Passed, Rejected, Cancelled }
public enum VoteChoice { For, Against, Abstain }
public sealed record ShareholderVote(int OwnerId,int Generation,string Name,long Shares,VoteChoice Choice);
public sealed class CompanyVote
{
    public long Id { get; set; }
    public string SecurityId { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public long OpenHour { get; set; }
    public long CloseHour { get; set; }
    public VoteKind Kind { get; set; }
    public int Option { get; set; }
    public VoteStatus Status { get; set; }
    public long EligibleShares { get; set; }
    public long ForShares { get; set; }
    public long AgainstShares { get; set; }
    public long AbstainShares { get; set; }
    public List<ShareholderVote> Ballots { get; set; } = [];
}
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
    public long Total => Companies+Institutions+Retail;
}
