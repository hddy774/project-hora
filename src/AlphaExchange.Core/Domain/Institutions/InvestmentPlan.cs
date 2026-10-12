namespace AlphaExchange.Core;

public enum PortfolioStyle { Defensive, Balanced, Growth, Opportunistic }
public sealed class AnalystReport
{
    public int OwnerId { get; set; }
    public string Sector { get; set; } = "";
    public long WrittenMinute { get; set; }
    public long ExpiresMinute { get; set; }
    public double ExpectedReturn { get; set; }
    public double Confidence { get; set; }
    public string Thesis { get; set; } = "";
}
public sealed class InvestmentPlan
{
    public string SecurityId { get; set; } = "";
    public long CreatedMinute { get; set; }
    public long ReviewMinute { get; set; }
    public long TargetMinute { get; set; }
    public long DurationMinutes { get; set; }
    public int LowerPrice { get; set; }
    public int UpperPrice { get; set; }
    public int StopPrice { get; set; }
    public int TakeProfitPrice { get; set; }
    public double ExpectedReturn { get; set; }
    public double Confidence { get; set; }
    public double EstimatedCost { get; set; }
    public double MaximumWeight { get; set; }
    public long LossBudget { get; set; }
    public string ExitCondition { get; set; } = "";
    public double TargetWeight { get; set; }
    public PortfolioStyle Style { get; set; }
    public InvestmentHorizon Horizon { get; set; }
    public string Thesis { get; set; } = "";
}

public enum CampaignPhase { Accumulate, AttractFollowers, Distribute, Retreat }
