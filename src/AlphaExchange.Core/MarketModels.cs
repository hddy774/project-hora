namespace AlphaExchange.Core;

public interface ICashAccount { long Cash { get; set; } }
public sealed record LedgerEntry(long Id, long Hour, string From, string To, string Reason, long Amount);

public sealed class RealEconomyState : ICashAccount
{
    public long Cash { get; set; } = 3_000_000_000;
    public long Consumption { get; set; }
    public long Wages { get; set; }
    public long CorporateSales { get; set; }
    public long CorporateCosts { get; set; }
    public long Investment { get; set; }
    public double Demand { get; set; } = 1;
}
public enum CorporateEventKind { Dividend, Split, ReverseSplit, Merger, Ipo, Issue, Buyback, Retirement }
public sealed class CorporateEvent
{
    public long Id { get; set; }
    public long Hour { get; set; }
    public string SecurityId { get; set; } = "";
    public string OtherSecurityId { get; set; } = "";
    public CorporateEventKind Kind { get; set; }
    public long Numerator { get; set; } = 1;
    public long Denominator { get; set; } = 1;
    public long Amount { get; set; }
    public long Shares { get; set; }
    public string Detail { get; set; } = "";
}
