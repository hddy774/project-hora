
namespace AlphaExchange.Core;

public sealed class LimitOrder
{
    public bool EnforceAllocation { get; set; }
    public bool Liquidity { get; set; }
    public long CreatedMinute { get; set; }
    public long ExpiresMinute { get; set; }
    public long Id { get; set; }
    public int OwnerId { get; set; }
    public string SecurityId { get; set; } = "";
    public int StockIndex { get; set; }
    public bool Buy { get; set; }
    public bool Short { get; set; }
    public bool Cover { get; set; }
    public int CollateralPrice { get; set; }
    public int Price { get; set; }
    public int Remaining { get; set; }
    public long ExpiresAt { get; set; }
}
