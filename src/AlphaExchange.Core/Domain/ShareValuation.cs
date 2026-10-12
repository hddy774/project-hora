namespace AlphaExchange.Core;

// Immutable price capture for a bulk valuation. Fractional marks use the same
// decimal midpoint rounding as individual holdings; integer prices stay integer.
internal readonly struct ShareValuation
{
    readonly int price;
    readonly decimal mark;
    readonly bool integral;

    public ShareValuation(Stock stock)
    {
        price = stock.Price;
        integral = stock.FractionalMark == 0;
        mark = integral ? 0 : stock.MarkPrice;
    }

    public long Value(long quantity) => integral ? Whole(price, quantity) : Fractional(mark, quantity);
    public static long Whole(int price, long quantity) => checked((long)price * quantity);
    public static long Fractional(decimal mark, long quantity) => checked((long)decimal.Round(mark * quantity));
}
