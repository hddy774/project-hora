using System.Text;
using System.Text.Json;

namespace AlphaExchange.Core;

public sealed partial class GameStore
{
    public void WriteSnapshot(SaveSnapshot snapshot)
    {
        // All payloads are immutable. Compression and I/O do not read the live engine.
        byte[] stateBytes = snapshot.EncodedBytes; CheckCheckpoint(stateBytes, snapshot.RunId, snapshot.Hour, snapshot.Minute);
        if (snapshot.History.Any(r => r.Hour < 0 || r.Hour > snapshot.Hour || r.Resolution is not (1 or 24)) ||
            snapshot.Events.Concat(snapshot.Bankruptcies).Any(e => e.Hour < 0 || e.Hour > snapshot.Hour))
            throw new InvalidDataException("체크포인트 기록 범위 불일치");
        var hours = snapshot.History.Select(r =>
        {
            byte[] bytes = Encoding.UTF8.GetBytes(r.Json);
            return (r, data: Compress(bytes), hash: Hash(bytes), point: JsonSerializer.Deserialize<DailySnapshot>(bytes)!);
        }).ToArray();
        byte[] state = Compress(stateBytes); string stateHash = Hash(stateBytes);
        var seasons = snapshot.Seasons.Select(r => (r, data: Compress(r.Json))).ToArray();
        lock (writeGate)
        {
            using var connection = Open(SavePath); Schema(connection); databaseReady = true;
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "SELECT minute FROM runs WHERE id=$run"; command.Parameters.AddWithValue("$run", snapshot.RunId);
            if (command.ExecuteScalar() is long committed && committed > snapshot.Minute) throw new InvalidDataException("과거 체크포인트로 되돌릴 수 없습니다.");
            WriteHours(command, snapshot.RunId, hours);
            WriteSeasons(command, snapshot.RunId, seasons);
            WriteEvents(command, snapshot.RunId, snapshot.Events);
            WriteLifecycle(command, snapshot);
            foreach (var report in snapshot.Reports) WriteReport(command, snapshot.RunId, report);
            command.CommandText = "INSERT INTO runs(id,hour,minute,state,hash) VALUES($run,$hour,$minute,$state,$hash) ON CONFLICT(id) DO UPDATE SET hour=excluded.hour,minute=excluded.minute,state=excluded.state,hash=excluded.hash WHERE excluded.minute>=runs.minute";
            command.Parameters.Clear(); command.Parameters.AddWithValue("$run", snapshot.RunId); command.Parameters.AddWithValue("$hour", snapshot.Hour);
            command.Parameters.AddWithValue("$minute", snapshot.Minute); command.Parameters.AddWithValue("$state", state); command.Parameters.AddWithValue("$hash", stateHash); command.ExecuteNonQuery();
            if (activeRun == "" || activeRun == snapshot.RunId)
            {
                command.CommandText = "INSERT INTO meta(key,value) VALUES('current',$run) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
                command.Parameters.Clear(); command.Parameters.AddWithValue("$run", snapshot.RunId); command.ExecuteNonQuery();
            }
            transaction.Commit();
            if (activeRun == "" || activeRun == snapshot.RunId) LastCommittedHour = Math.Max(LastCommittedHour, snapshot.Hour);
            lock (pending)
            {
                if (pending.TryGetValue(snapshot.RunId, out var records)) records.RemoveAll(r => r.Hour <= snapshot.Hour);
                if (pendingEvents.TryGetValue(snapshot.RunId, out var events)) events.RemoveAll(e => snapshot.Events.Any(saved => saved.Id == e.Id));
                if (pendingBankruptcies.TryGetValue(snapshot.RunId, out var bankruptcies)) bankruptcies.RemoveAll(e => snapshot.Bankruptcies.Any(saved => saved.Id == e.Id));
                if (pendingReports.TryGetValue(snapshot.RunId, out var reports)) reports.RemoveAll(r => r.Sequence <= snapshot.ReportThrough);
            }
            if (!File.Exists(BackupPath) || Environment.TickCount64 - lastBackup >= 60000 || snapshot.Hour % 720 == 0)
            { Backup(connection, BackupPath); lastBackup = Environment.TickCount64; }
        }
    }
    public static void Acknowledge(GameEngine engine, SaveSnapshot snapshot)
    {
        if (engine.State.RunId != snapshot.RunId || snapshot.Seasons.Length == 0) return;
        long last = snapshot.Seasons.Max(s => s.Season);
        engine.State.LastArchivedSeason = Math.Max(engine.State.LastArchivedSeason, last);
        engine.State.PendingSeasons.RemoveAll(s => s.Season <= last);
    }
    public void Save(GameEngine engine) { var snapshot = PrepareSave(engine); WriteSnapshot(snapshot); Acknowledge(engine, snapshot); }
}
