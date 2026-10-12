
namespace AlphaExchange.Core;

public sealed class TradeRecord
{
    public int Minute { get; set; }
    public long AbsoluteMinute { get; set; }
    public long Season { get; set; }
    public int Day { get; set; }
    public int Hour { get; set; }
    public int BuyerId { get; set; }
    public int SellerId { get; set; }
    public string SecurityId { get; set; } = "";
    public int StockIndex { get; set; }
    public int Quantity { get; set; }
    public int Price { get; set; }
    public bool ShortSale { get; set; }
    public bool ShortCover { get; set; }
}
