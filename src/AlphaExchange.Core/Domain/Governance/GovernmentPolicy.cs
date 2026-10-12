namespace AlphaExchange.Core;

public sealed class GovernmentPolicy
{
    public long Season { get; set; } = 1;
    public string Name { get; set; } = "균형 성장";
    public double TaxRate { get; set; } = .10;
    public int FeeBasisPoints { get; set; } = 15;
    public double BaseRate { get; set; } = .025;
    public bool ShortSellingAllowed { get; set; } = true;
    public double ShortExposureLimit { get; set; } = .15;
    public double LoanLimitMultiplier { get; set; } = 1;
    public double SubsidyRate { get; set; } = .00005;
    public bool LossCarryForward { get; set; } = true;
    public double SpendingRatio { get; set; } = .65;
    public double DividendTaxRate { get; set; } = .08;
    public double Enforcement { get; set; } = .5;
}
