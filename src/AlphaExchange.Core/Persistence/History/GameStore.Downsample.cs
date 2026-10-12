
namespace AlphaExchange.Core;

public sealed partial class GameStore
{
    static IReadOnlyList<DailySnapshot> Downsample(List<DailySnapshot> rows, int maximum)
    {
        if (rows.Count <= maximum) return rows;
        Func<DailySnapshot, double>[] metrics = [s=>s.Capitalization,s=>s.PriceIndex,s=>s.TotalReturnIndex,s=>s.InstitutionCash+s.RetailCash,
            s=>s.InstitutionEquity,s=>s.RetailEquity,s=>s.InstitutionTradingIncome,s=>s.RetailTradingIncome];
        int bucketSize = Math.Max(1, (int)Math.Ceiling((double)rows.Count / Math.Max(1, maximum / (2 + metrics.Length * 2))));
        var result = new HashSet<DailySnapshot> { rows[0], rows[^1] };
        void Add(DailySnapshot value) { if (result.Count < maximum) result.Add(value); }
        foreach (var bucket in rows.Chunk(bucketSize))
        {
            Add(bucket[0]); Add(bucket[^1]);
            foreach (var metric in metrics) { Add(bucket.MinBy(metric)!); Add(bucket.MaxBy(metric)!); }
        }
        return result.OrderBy(s => s.Hour).ToList();
    }
}
