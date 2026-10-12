using System.Text.Json;

namespace AlphaExchange.Core;

public sealed partial class GameStore
{
    sealed class RunQuery(GameStore store, string run) : IHistoryQuery
    {
        public GameStore Store => store;
        readonly object queryGate = new();
        readonly Dictionary<(long Start, long End, int Maximum), IReadOnlyList<DailySnapshot>> rangeCache = [];
        (long FileTime, long FileLength, long WalTime, long WalLength, int Pending, long First, long Last) cacheStamp;
        (long FileTime, long FileLength, long WalTime, long WalLength, int Pending, long First, long Last) Stamp()
        {
            var file = new FileInfo(store.SavePath); var wal = new FileInfo(store.SavePath + "-wal");
            lock (store.pending)
            {
                var rows = store.pending.GetValueOrDefault(run, []);
                return (file.Exists ? file.LastWriteTimeUtc.Ticks : 0, file.Exists ? file.Length : 0,
                    wal.Exists ? wal.LastWriteTimeUtc.Ticks : 0, wal.Exists ? wal.Length : 0,
                    rows.Count, rows.Count > 0 ? rows[0].Hour : -1, rows.Count > 0 ? rows[^1].Hour : -1);
            }
        }
        List<DailySnapshot> Pending(long start, long end)
        {
            HistoryRecord[] records;
            lock (store.pending) records = store.pending.GetValueOrDefault(run, []).Where(r => r.Hour >= start && r.Hour <= end).ToArray();
            return records.Select(r => JsonSerializer.Deserialize<DailySnapshot>(r.Json)!).ToList();
        }
        public DailySnapshot? At(long hour)
        {
            HistoryRecord? latest;
            lock (store.pending) latest = store.pending.GetValueOrDefault(run, []).Where(r => r.Hour <= hour)
                .OrderByDescending(r => r.Hour).ThenBy(r => r.Resolution).FirstOrDefault();
            var memory = latest is null ? null : JsonSerializer.Deserialize<DailySnapshot>(latest.Json);
            DailySnapshot? disk = null;
            if (store.databaseReady && File.Exists(store.SavePath))
            {
                using var connection = store.Open(store.SavePath, true); using var command = connection.CreateCommand();
                command.CommandText = "SELECT data,hash FROM hours WHERE run=$run AND hour<=$hour ORDER BY hour DESC,resolution ASC LIMIT 1";
                command.Parameters.AddWithValue("$run", run); command.Parameters.AddWithValue("$hour", hour);
                using var reader = command.ExecuteReader();
                if (reader.Read()) disk = JsonSerializer.Deserialize<DailySnapshot>(CheckedInflate((byte[])reader[0], reader.GetString(1), 2 * 1024 * 1024));
            }
            return memory is not null && (disk is null || memory.Hour >= disk.Hour) ? memory : disk;
        }
        public IReadOnlyList<DailySnapshot> Range(long start, long end, int maximumPoints = 500)
        {
            lock (queryGate)
            {
                var stamp = Stamp(); if (cacheStamp != stamp) { rangeCache.Clear(); cacheStamp = stamp; }
                var key = (start, end, Math.Clamp(maximumPoints, 16, 1000));
                if (!rangeCache.TryGetValue(key, out var points))
                {
                    points = RangeUncached(key.start, key.end, key.Item3);
                    if (stamp == Stamp()) { if (rangeCache.Count >= 3) rangeCache.Clear(); rangeCache[key] = points; }
                }
                // Callers can annotate/mutate a chart without altering the cache.
                return points.Select(CheckpointCopy.Daily).ToArray();
            }
        }
        IReadOnlyList<DailySnapshot> RangeUncached(long start, long end, int maximumPoints)
        {
            maximumPoints = Math.Clamp(maximumPoints, 16, 1000);
            var rows = new List<DailySnapshot>();
            if (store.databaseReady && File.Exists(store.SavePath))
            {
                using var connection = store.Open(store.SavePath, true);
                using var schema = connection.CreateCommand(); schema.CommandText = "PRAGMA table_info(hours)";
                var available = new HashSet<string>(); using (var reader = schema.ExecuteReader()) while (reader.Read()) available.Add(reader.GetString(1));
                var metrics = new[] { "cap", "price_index", "return_index", "investor_cash", "institution_equity", "retail_equity", "institution_income", "retail_income" }.Where(available.Contains).ToArray();
                int perBucket = 2 + metrics.Length * 2;
                long stride = end - start + 1 <= maximumPoints ? 1 : Math.Max(1, (long)Math.Ceiling((end - start + 1.0) / Math.Max(1, maximumPoints / perBucket)));
                var keys = new HashSet<long>();
                // Stream lightweight headers once. Bucket endpoints and every
                // metric's earliest extrema are retained without SQLite temp
                // tables, joins or one full-range sort per metric.
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT hour,{string.Join(',', metrics)} FROM hours WHERE run=$run AND hour BETWEEN $start AND $end ORDER BY hour";
                    command.Parameters.AddWithValue("$run", run); command.Parameters.AddWithValue("$start", start); command.Parameters.AddWithValue("$end", end);
                    using var reader = command.ExecuteReader();
                    long bucket = -1, first = 0, last = 0, lowCap = long.MaxValue, highCap = long.MinValue;
                    var lows = Enumerable.Repeat(double.PositiveInfinity, metrics.Length).ToArray();
                    var highs = Enumerable.Repeat(double.NegativeInfinity, metrics.Length).ToArray();
                    var lowHours = Enumerable.Repeat(-1L, metrics.Length).ToArray(); var highHours = (long[])lowHours.Clone();
                    void Flush()
                    {
                        if (bucket < 0) return; keys.Add(first); keys.Add(last);
                        for (int i = 0; i < metrics.Length; i++) { if (lowHours[i] >= 0) keys.Add(lowHours[i]); if (highHours[i] >= 0) keys.Add(highHours[i]); }
                    }
                    while (reader.Read())
                    {
                        long hour = reader.GetInt64(0), nextBucket = (hour - start) / stride;
                        if (nextBucket != bucket)
                        {
                            Flush(); bucket = nextBucket; first = hour; lowCap = long.MaxValue; highCap = long.MinValue;
                            Array.Fill(lows, double.PositiveInfinity); Array.Fill(highs, double.NegativeInfinity);
                            Array.Fill(lowHours, -1); Array.Fill(highHours, -1);
                        }
                        last = hour;
                        for (int i = 0; i < metrics.Length; i++)
                        {
                            if (reader.IsDBNull(i + 1)) continue;
                            if (metrics[i] == "cap")
                            {
                                long value = reader.GetInt64(i + 1);
                                if (value < lowCap || lowHours[i] < 0) { lowCap = value; lowHours[i] = hour; }
                                if (value > highCap || highHours[i] < 0) { highCap = value; highHours[i] = hour; }
                            }
                            else
                            {
                                double value = reader.GetDouble(i + 1);
                                if (value < lows[i]) { lows[i] = value; lowHours[i] = hour; }
                                if (value > highs[i]) { highs[i] = value; highHours[i] = hour; }
                            }
                        }
                    }
                    Flush();
                }
                if (keys.Count > 0)
                {
                    using var command = connection.CreateCommand();
                    string[] names = keys.Select((_, i) => "$h" + i).ToArray();
                    command.CommandText = $"SELECT data,hash FROM hours WHERE run=$run AND hour IN ({string.Join(',', names)}) ORDER BY hour,resolution DESC";
                    command.Parameters.AddWithValue("$run", run);
                    foreach (var (key, i) in keys.Select((key, i) => (key, i))) command.Parameters.AddWithValue(names[i], key);
                    using var reader = command.ExecuteReader();
                    while (reader.Read()) rows.Add(JsonSerializer.Deserialize<DailySnapshot>(CheckedInflate((byte[])reader[0], reader.GetString(1), 2 * 1024 * 1024))!);
                }
            }
            rows.AddRange(Pending(start, end));
            var sampled = Downsample(rows.GroupBy(s => s.Hour).Select(g => g.OrderBy(s => s.Resolution).First()).OrderBy(s => s.Hour).ToList(), maximumPoints);
            var gaps = Gaps(start, end);
            long previous = start;
            foreach (var point in sampled) { point.GapBefore = gaps.Any(g => g > previous && g <= point.Hour); previous = point.Hour; }
            return sampled;
        }
        bool HourlyCoverage(long start, long end)
        {
            if (end < start) return false;
            long diskEnd = start - 1;
            if (store.databaseReady && File.Exists(store.SavePath))
            {
                using var connection = store.Open(store.SavePath, true); using var command = connection.CreateCommand();
                command.CommandText = "SELECT hour FROM hours WHERE run=$run AND hour BETWEEN $start AND $end AND resolution=1 ORDER BY hour DESC LIMIT 1";
                command.Parameters.AddWithValue("$run", run); command.Parameters.AddWithValue("$start", start); command.Parameters.AddWithValue("$end", end);
                if (command.ExecuteScalar() is long last)
                {
                    command.CommandText = "SELECT count(*) FROM hours WHERE run=$run AND hour BETWEEN $start AND $last AND resolution=1";
                    command.Parameters.AddWithValue("$last", last);
                    if (Convert.ToInt64(command.ExecuteScalar()) != last - start + 1) return false;
                    diskEnd = last;
                }
            }
            if (diskEnd == end) return true;
            long next = diskEnd + 1;
            lock (store.pending)
                foreach (long hour in store.pending.GetValueOrDefault(run, []).Where(r => r.Resolution == 1 && r.Hour >= next && r.Hour <= end).Select(r => r.Hour).Distinct().Order())
                { if (hour != next) return false; next++; }
            return next == end + 1;
        }
        List<long> Gaps(long start, long end)
        {
            // The primary key proves there is exactly one hourly row for each
            // hour. Missing/mixed-resolution histories still take the full gap
            // path; no fabricated continuity or dropped gap markers.
            if (HourlyCoverage(start, end)) return [];
            var segments = new List<(long Start, long End, int First, int Last)>();
            if (store.databaseReady && File.Exists(store.SavePath))
            {
                using var connection = store.Open(store.SavePath, true); using var command = connection.CreateCommand();
                command.CommandText = """
                    WITH headers AS MATERIALIZED (
                        SELECT hour,min(resolution) AS resolution FROM hours
                        WHERE run=$run AND hour BETWEEN $start AND $end GROUP BY hour),
                    adjacent AS (
                        SELECT hour,resolution,lag(hour) OVER(ORDER BY hour) AS previous,
                        lag(resolution) OVER(ORDER BY hour) AS previous_resolution FROM headers)
                    SELECT hour,resolution,previous,previous_resolution,0 AS last_row FROM adjacent
                    WHERE previous IS NULL OR hour-previous>max(resolution,previous_resolution)
                    UNION ALL SELECT hour,resolution,NULL,NULL,1 FROM headers WHERE hour=(SELECT max(hour) FROM headers)
                    ORDER BY hour,last_row
                    """;
                command.Parameters.AddWithValue("$run", run); command.Parameters.AddWithValue("$start", start); command.Parameters.AddWithValue("$end", end);
                using var reader = command.ExecuteReader(); long first = 0; int firstResolution = 1;
                while (reader.Read())
                {
                    long hour = reader.GetInt64(0); int resolution = reader.GetInt32(1);
                    if (reader.GetInt32(4) == 1) { segments.Add((first, hour, firstResolution, resolution)); continue; }
                    if (!reader.IsDBNull(2)) segments.Add((first, reader.GetInt64(2), firstResolution, reader.GetInt32(3)));
                    first = hour; firstResolution = resolution;
                }
            }
            (long Hour, int Resolution)[] memory;
            lock (store.pending) memory = store.pending.GetValueOrDefault(run, []).Where(r => r.Hour >= start && r.Hour <= end)
                .Select(r => (r.Hour, r.Resolution)).ToArray();
            // Migration/retry can introduce old mixed-resolution records inside a
            // disk segment. Resolve those using every original header, as before.
            if (segments.Count > 0 && memory.Any(r => r.Hour < segments[^1].End)) return OriginalGaps(start, end);
            segments.AddRange(memory.Select(r => (r.Hour, r.Hour, r.Resolution, r.Resolution)));
            var gaps = new List<long>(); long previous = start; int previousResolution = 1; bool seen = false;
            foreach (var segment in segments.OrderBy(s => s.Start).ThenByDescending(s => s.End))
            {
                if (!seen && segment.Start > start || seen && segment.Start > previous && segment.Start - previous > Math.Max(previousResolution, segment.First)) gaps.Add(segment.Start);
                if (!seen || segment.End > previous) { previous = segment.End; previousResolution = segment.Last; }
                else if (segment.End == previous) previousResolution = Math.Min(previousResolution, segment.Last);
                seen = true;
            }
            if (!seen || previous < end) gaps.Add(end); return gaps;
        }
        List<long> OriginalGaps(long start, long end)
        {
            var headers = new List<(long Hour, int Resolution)>();
            if (store.databaseReady && File.Exists(store.SavePath))
            {
                using var connection = store.Open(store.SavePath, true); using var command = connection.CreateCommand();
                command.CommandText = "SELECT hour,min(resolution) FROM hours WHERE run=$run AND hour BETWEEN $start AND $end GROUP BY hour ORDER BY hour";
                command.Parameters.AddWithValue("$run", run); command.Parameters.AddWithValue("$start", start); command.Parameters.AddWithValue("$end", end);
                using var reader = command.ExecuteReader(); while (reader.Read()) headers.Add((reader.GetInt64(0), reader.GetInt32(1)));
            }
            lock (store.pending) headers.AddRange(store.pending.GetValueOrDefault(run, []).Where(r => r.Hour >= start && r.Hour <= end).Select(r => (r.Hour, r.Resolution)));
            var gaps = new List<long>(); long last = start; int resolution = 1; bool first = true;
            foreach (var record in headers.GroupBy(r => r.Hour).Select(g => g.MinBy(r => r.Resolution)).OrderBy(r => r.Hour))
            {
                if (first && record.Hour > start || !first && record.Hour - last > Math.Max(resolution, record.Resolution)) gaps.Add(record.Hour);
                first = false; last = record.Hour; resolution = record.Resolution;
            }
            if (first || last < end) gaps.Add(end);
            return gaps;
        }
        public bool Covers(long start, long end) => Gaps(start, end).Count == 0;

    }
}
