namespace Chronicles.Application;

/// <summary>
/// Calculates a stable card column count without depending on WPF. The desktop panel uses
/// this policy so narrow windows and enlarged UI scales wrap cards instead of clipping text.
/// </summary>
public static class ResponsiveCardLayoutPolicy
{
    public static int CalculateColumns(
        double availableWidth,
        double minimumCardWidth,
        double columnGap,
        int itemCount,
        int maximumColumns)
    {
        if (!double.IsFinite(availableWidth) || availableWidth < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(availableWidth));
        }
        if (!double.IsFinite(minimumCardWidth) || minimumCardWidth <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumCardWidth));
        }
        if (!double.IsFinite(columnGap) || columnGap < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(columnGap));
        }
        ArgumentOutOfRangeException.ThrowIfNegative(itemCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumColumns);

        if (itemCount == 0)
        {
            return 0;
        }

        var widthBound = Math.Max(
            1,
            (int)Math.Floor((availableWidth + columnGap) / (minimumCardWidth + columnGap)));
        return Math.Min(itemCount, Math.Min(maximumColumns, widthBound));
    }
}
