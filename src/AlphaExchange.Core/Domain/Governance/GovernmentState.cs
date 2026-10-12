namespace AlphaExchange.Core;

public sealed class GovernmentState : ICashAccount
{
    public long TaxWriteOffs { get; set; }
    public long FineWriteOffs { get; set; }
    public long Cash { get; set; } = 100_000_000;
    public long Taxes { get; set; }
    public long Fines { get; set; }
    public long TaxEscrow { get; set; }
    public long Spending { get; set; }
    public long CorporateTaxes { get; set; }
    public long DividendTaxes { get; set; }
    public long TaxRefunds { get; set; }
    public long Subsidies { get; set; }
    public GovernmentPolicy Policy { get; set; } = new();
    public List<GovernmentPolicy> History { get; set; } = [];
}
