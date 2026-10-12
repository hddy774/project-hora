namespace AlphaExchange.Core;

public enum OperationStatus { Active, Completed, Failed }

public sealed class InstitutionOperation
{
    public string LeaderName { get; set; } = "";
    public string PartnerName { get; set; } = "";
    public long Id { get; set; }
    public int LeaderId { get; set; }
    public int PartnerId { get; set; }
    public string SecurityId { get; set; } = "";
    public int StockIndex { get; set; }
    public long StartedHour { get; set; }
    public long EndsHour { get; set; }
    public int OpeningPrice { get; set; }
    public OperationStatus Status { get; set; }
    public long Fine { get; set; }
    public string Detail { get; set; } = "대표 간 공동 매집 제안";
    public double TargetReturn { get; set; } = .008;
}
