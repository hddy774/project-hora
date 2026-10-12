namespace AlphaExchange.Client;

// Owned by one renderer. Queries downsample first; never silently drop points.
internal sealed class ChartSeriesBuffer
{
    public const int MaximumPoints = 2048;
    double[] values = new double[128];
    public int Count { get; private set; }
    public double Low { get; private set; }
    public double High { get; private set; }
    public ReadOnlySpan<double> Values => values.AsSpan(0, Count);

    public void Capture(IEnumerable<double> source)
    {
        Count = 0;
        Low = double.PositiveInfinity;
        High = double.NegativeInfinity;
        if (source is IReadOnlyList<double> list)
        {
            for (int i = 0; i < list.Count; i++) Add(list[i]);
        }
        else
        {
            foreach (double value in source) Add(value);
        }
        if (Count == 0) Add(0);
        if (Count == 1) Add(values[0]);
        if (Math.Abs(High - Low) < 1) { Low -= 1; High += 1; }
        double padding = (High - Low) * .15;
        Low -= padding;
        High += padding;
    }

    void Add(double value)
    {
        if (Count == MaximumPoints)
            throw new InvalidOperationException("Downsample before drawing more than 2048 chart points.");
        if (Count == values.Length) Array.Resize(ref values, Math.Min(MaximumPoints, values.Length * 2));
        values[Count++] = value;
        if (value < Low || double.IsNaN(value)) Low = value;
        if (value > High || double.IsNaN(High)) High = value;
    }
}
