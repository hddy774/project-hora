using System.Runtime.InteropServices;

namespace AlphaExchange.Client;

// Owned by one renderer. Queries downsample first; never silently drop points.
internal sealed class ChartSeriesBuffer
{
    public const int MaximumPoints = 2048;
    readonly List<double> values = new(128);
    public int Count => values.Count;
    public double Low { get; private set; }
    public double High { get; private set; }
    public ReadOnlySpan<double> Values => CollectionsMarshal.AsSpan(values);

    public void Capture(IEnumerable<double> source)
    {
        values.Clear();
        if (source is double[] array)
        {
            CheckSize(array.Length);
            CollectionsMarshal.SetCount(values, array.Length);
            array.AsSpan().CopyTo(CollectionsMarshal.AsSpan(values));
        }
        else if (source is IReadOnlyList<double> list)
        {
            CheckSize(list.Count);
            for (int i = 0; i < list.Count; i++) values.Add(list[i]);
        }
        else
        {
            foreach (double value in source)
            {
                CheckSize(Count + 1);
                values.Add(value);
            }
        }
        if (Count == 0) values.Add(0);
        if (Count == 1) values.Add(values[0]);
        // .NET uses its vectorized span path for List<double>, including the
        // original Min/Max NaN semantics, without allocating another array.
        Low = values.Min();
        High = values.Max();
        if (Math.Abs(High - Low) < 1) { Low -= 1; High += 1; }
        double padding = (High - Low) * .15;
        Low -= padding;
        High += padding;
    }

    static void CheckSize(int count)
    {
        if (count > MaximumPoints)
            throw new InvalidOperationException("Downsample before drawing more than 2048 chart points.");
    }
}
