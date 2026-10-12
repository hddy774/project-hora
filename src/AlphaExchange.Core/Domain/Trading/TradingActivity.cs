namespace AlphaExchange.Core;

public sealed class TradingActivity
{
    public long SubmittedOrders { get; set; }
    public long AggressiveOrders { get; set; }
    public long CancelledOrders { get; set; }
    public long InstitutionMatches { get; set; }
    public long RetailMatches { get; set; }
    public long CrossGroupMatches { get; set; }
    public long CrossGroupTurnover { get; set; }
    public long CashToInstitutions { get; set; }
    public long CashToRetail { get; set; }
}
