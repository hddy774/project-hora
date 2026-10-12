namespace AlphaExchange.Core;

public sealed class TraderSnapshot
{
    public int Generation { get; set; } = 1;
    public int Id { get; set; }
    public double ReturnIndex { get; set; } = 1;
    public long NetContribution { get; set; }
    public long Equity { get; set; }
    public long Cash { get; set; }
    public double NetIncome { get; set; }
    public long Volume { get; set; }
    public long Turnover { get; set; }
}
