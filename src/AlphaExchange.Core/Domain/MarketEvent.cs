
namespace AlphaExchange.Core;

public sealed class MarketEvent
{
    public int Minute { get; set; }
    public int ActorId { get; set; }
    public long ActivityId { get; set; }
    public long Season { get; set; }
    public int Day { get; set; }
    public int Hour { get; set; }
    public string SecurityId { get; set; } = "";
    public int StockIndex { get; set; }
    public string Headline { get; set; } = "";
    public string Detail { get; set; } = "";
    public double Impact { get; set; }
}
