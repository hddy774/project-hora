using System.Globalization;

namespace Chronicles.Core;

public static class KoreanNumberFormatter
{
    private static readonly (decimal Divisor, string Unit)[] Units =
    [
        (10_000_000_000_000_000m, "경"),
        (1_000_000_000_000m, "조"),
        (100_000_000m, "억"),
        (10_000m, "만"),
    ];

    public static string Format(long value)
    {
        var absolute = Math.Abs((decimal)value);
        foreach (var (divisor, unit) in Units)
        {
            if (absolute < divisor)
            {
                continue;
            }

            var scaled = absolute / divisor;
            var sign = value < 0 ? "-" : string.Empty;
            return $"{sign}{scaled.ToString("0.##", CultureInfo.CurrentCulture)}{unit}";
        }

        return value.ToString("N0", CultureInfo.CurrentCulture);
    }
}
