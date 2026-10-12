using System.Text.Json;

namespace AlphaExchange.Core;

public sealed partial class GameStore
{
    public void Attach(GameEngine engine)
    {
        if (engine.HistorySource is RunQuery query && query.Store == this) return;
        string run = engine.State.RunId; if (activeRun != run) LastCommittedHour = 0; activeRun = run;
        lock (pending) if (!pending.ContainsKey(run)) pending[run] = [];
        engine.HourRecorded += snapshot =>
        {
            string json = JsonSerializer.Serialize(snapshot);
            lock (pending) pending[run].Add(new HistoryRecord(snapshot.Hour, 1, json));
        };
        lock (pending) if (!pendingEvents.ContainsKey(run)) pendingEvents[run] = [];
        engine.CorporateEventRecorded += e => { lock (pending) pendingEvents[run].Add(new EventRecord(e.Id, e.Hour, JsonSerializer.Serialize(e))); };
        lock (pending) foreach (var e in engine.State.CorporateEvents)
                if (!pendingEvents[run].Any(r => r.Id == e.Id)) pendingEvents[run].Add(new EventRecord(e.Id, e.Hour, JsonSerializer.Serialize(e)));
        lock (pending)
        {
            pendingBankruptcies.TryAdd(run, []); pendingReports.TryAdd(run, []);
            foreach (var b in engine.State.Bankruptcies)
                if (!pendingBankruptcies[run].Any(e => e.Id == b.Id)) pendingBankruptcies[run].Add(new(b.Id, b.Hour, JsonSerializer.Serialize(b)));
        }
        engine.BankruptcyRecorded += b => { lock (pending) pendingBankruptcies[run].Add(new(b.Id, b.Hour, JsonSerializer.Serialize(b))); };
        engine.CompanyReportRecorded += r => { lock (pending) pendingReports[run].Add((++reportSequence, CheckpointCopy.Scalar(r))); };
        engine.HistorySource = new RunQuery(this, run);
        // A loaded v5 database contains immutable JSON without the fields added in v6.
        // Never re-encode committed history at attach: defaults/new measurements would
        // change its checksum. Queue only genuinely missing initial/daily records.
        var committed = new HashSet<(long Hour, int Resolution)>();
        if (databaseReady)
        {
            using var connection = Open(SavePath, true); using var command = connection.CreateCommand();
            var hours = engine.State.DailyHistory.Select(d => d.Hour).Append(engine.State.CompletedHours).Distinct().ToArray();
            var names = hours.Select((_, i) => "$h" + i).ToArray();
            command.CommandText = $"SELECT hour,resolution FROM hours WHERE run=$run AND hour IN ({string.Join(',', names)})";
            command.Parameters.AddWithValue("$run", run);
            for (int i = 0; i < hours.Length; i++) command.Parameters.AddWithValue(names[i], hours[i]);
            using var reader = command.ExecuteReader();
            while (reader.Read()) committed.Add((reader.GetInt64(0), reader.GetInt32(1)));
        }
        if (!committed.Contains((engine.State.CompletedHours, 1)) && engine.State.Minute == 0) AddPending(run, engine.CaptureSnapshot());
        foreach (var daily in engine.State.DailyHistory)
            if (!committed.Contains((daily.Hour, 1)) && !committed.Contains((daily.Hour, 24))) AddPending(run, DailyHistoryRow(daily));
        queuedDailyThrough[run] = Math.Max(queuedDailyThrough.GetValueOrDefault(run, -1), engine.State.DailyHistory.Max(d => d.Hour));
    }
    void AddPending(string run, DailySnapshot snapshot)
    {
        var record = new HistoryRecord(snapshot.Hour, snapshot.Resolution, JsonSerializer.Serialize(snapshot));
        lock (pending)
        {
            if (!pending.TryGetValue(run, out var records)) pending[run] = records = [];
            if (!records.Any(r => r.Hour == record.Hour && r.Resolution == record.Resolution)) records.Add(record);
        }
    }
    public SaveSnapshot PrepareSave(GameEngine engine)
    {
        Attach(engine);
        var state = engine.State;
        var seasons = state.PendingSeasons.OrderBy(r => r.Season)
            .Select(r => new ArchivedSnapshot(r.Season, SeasonPath(state, r.Season), JsonSerializer.Serialize(r))).ToArray();
        long through = queuedDailyThrough.GetValueOrDefault(state.RunId, -1);
        foreach (var daily in state.DailyHistory.Where(d => d.Hour > through)) AddPending(state.RunId, DailyHistoryRow(daily));
        queuedDailyThrough[state.RunId] = state.DailyHistory.Max(d => d.Hour);
        HistoryRecord[] records;
        lock (pending) records = pending[state.RunId].Where(r => r.Hour <= state.CompletedHours).ToArray();
        EventRecord[] events;
        lock (pending) events = pendingEvents[state.RunId].Where(e => e.Hour <= state.CompletedHours).ToArray();
        lock (pending) return new SaveSnapshot(CheckpointCopy.Freeze(state, true), seasons, records)
        {
            Events = events,
            Bankruptcies = pendingBankruptcies[state.RunId].ToArray(),
            ReportThrough = reportSequence,
            Reports = state.Stocks.SelectMany(s => s.Reports).Concat(pendingReports[state.RunId].Select(p => p.Report))
            .GroupBy(r => (r.SecurityId, r.Season, r.AccountingBasis, r.IsOpening)).Select(g => CheckpointCopy.Scalar(g.Last())).ToArray()
        };
    }
    static void CheckCheckpoint(byte[] bytes, string run, long hour, long minute)
    {
        // Validate the whole JSON without allocating a DOM for every participant/position.
        var reader = new Utf8JsonReader(bytes); string? actualRun = null; long? actualHour = null, actualMinute = null; int version = 0;
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) throw new InvalidDataException("체크포인트 JSON 오류");
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) throw new InvalidDataException("체크포인트 JSON 오류");
            bool isRun = reader.ValueTextEquals("RunId"), isHour = reader.ValueTextEquals("CompletedHours"), isMinute = reader.ValueTextEquals("CompletedMinutes"), isVersion = reader.ValueTextEquals("Version");
            if (!reader.Read()) throw new InvalidDataException("체크포인트 JSON 오류");
            if (isRun) actualRun = reader.GetString();
            else if (isHour) actualHour = reader.GetInt64();
            else if (isMinute) actualMinute = reader.GetInt64(); else if (isVersion) version = reader.GetInt32();
            reader.Skip();
        }
        if (reader.TokenType != JsonTokenType.EndObject || reader.Read() || actualRun != run || actualHour != hour || version >= 8 && actualMinute != minute || minute / 60 != hour)
            throw new InvalidDataException("체크포인트 기록 범위 불일치");
    }
}
