using System.Globalization;

namespace AlphaExchange.Core;
public static class KoreanNumber
{
    public static string Full(long amount) => amount.ToString("N0",CultureInfo.InvariantCulture);
    public static string Compact(long amount)
    {
        decimal value=amount,absolute=Math.Abs(value);
        return absolute>=1_000_000_000_000 ? (value/1_000_000_000_000).ToString("0.##",CultureInfo.InvariantCulture)+"조" :
            absolute>=100_000_000 ? (value/100_000_000).ToString("0.##",CultureInfo.InvariantCulture)+"억" :
            absolute>=10_000 ? (value/10_000).ToString("0.##",CultureInfo.InvariantCulture)+"만" : Full(amount);
    }
    public static string Dual(long amount) => Compact(amount)+" · "+Full(amount);
}
