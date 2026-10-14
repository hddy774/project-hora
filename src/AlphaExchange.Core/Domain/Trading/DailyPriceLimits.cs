namespace AlphaExchange.Core;

public readonly record struct PriceBand(int Lower, int Upper)
{
    public bool Contains(int price) => price >= Lower && price <= Upper;
    public int Clamp(int price) => Math.Clamp(price, Lower, Upper);
}

public static class DailyPriceLimits
{
    public const decimal Rate = .30m;

    public static PriceBand For(Stock stock)
    {
        decimal reference = stock.LimitReferencePrice > 0 ? stock.LimitReferencePrice : stock.DayOpenPrice;
        decimal lower = Math.Max(1, reference * (1 - Rate));
        decimal upper = Math.Min(10_000_000, reference * (1 + Rate));
        int lowerTick = lower < 1000 ? 1 : 10;
        int upperTick = upper < 1000 ? 1 : 10;
        int low = (int)(decimal.Ceiling(lower / lowerTick) * lowerTick);
        int high = (int)(decimal.Floor(upper / upperTick) * upperTick);
        return new(low, Math.Max(low, high));
    }
}
