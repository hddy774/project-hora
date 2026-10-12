using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace AlphaExchange.Core;
public sealed partial class GameStore
{
    static void WriteLifecycle(SqliteCommand command, SaveSnapshot snapshot)
    {
        foreach (var record in snapshot.Bankruptcies)
        {
            var failure = JsonSerializer.Deserialize<BankruptcyRecord>(record.Json) ?? throw new InvalidDataException("파산 기록 오류");
            if (failure.Id != record.Id || failure.Hour != record.Hour || !Enum.IsDefined(failure.Kind)) throw new InvalidDataException("파산 기록 범위 오류");
            WriteBankruptcy(command, snapshot.RunId, record, failure.EntityId, (int)failure.Kind);
            if (failure.Report is not null) WriteReport(command, snapshot.RunId, failure.Report);
        }
    }
    static void WriteBankruptcy(SqliteCommand command, string run, EventRecord record, string entity, int kind)
    {
        command.Parameters.Clear(); command.CommandText = "SELECT data FROM bankruptcies WHERE run=$run AND id=$id";
        command.Parameters.AddWithValue("$run", run); command.Parameters.AddWithValue("$id", record.Id);
        if (command.ExecuteScalar() is string old && old != record.Json) throw new InvalidDataException("동일 영속 기록 ID의 내용 불일치");
        command.CommandText = "INSERT INTO bankruptcies(run,id,hour,kind,entity,data,hash) VALUES($run,$id,$hour,$kind,$entity,$data,$hash) ON CONFLICT(run,id) DO NOTHING";
        command.Parameters.AddWithValue("$hour", record.Hour); command.Parameters.AddWithValue("$entity", entity);
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$data", record.Json); command.Parameters.AddWithValue("$hash", Hash(record.Json)); command.ExecuteNonQuery();
    }
    public IReadOnlyList<BankruptcyRecord> ReadBankruptcies(GameState state, int maximum = 60, long beforeId = long.MaxValue)
        => ReadStoredBankruptcies(state, maximum, beforeId).Concat(state.Bankruptcies.Where(b => b.Id < beforeId))
            .GroupBy(b => b.Id).Select(g => g.Last()).OrderByDescending(b => b.Id).Take(Math.Clamp(maximum, 1, 1000)).ToList();
    List<BankruptcyRecord> ReadStoredBankruptcies(GameState state, int maximum, long beforeId = long.MaxValue)
    {
        var result = new List<BankruptcyRecord>();
        if (File.Exists(SavePath))
        {
            using var connection = Open(SavePath, true); using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name=$table";
            command.Parameters.AddWithValue("$table", "bankruptcies");
            if ((long)command.ExecuteScalar()! > 0)
            {
                command.Parameters.Clear(); command.CommandText = "SELECT data,hash FROM bankruptcies WHERE run=$run AND id<$before ORDER BY id DESC LIMIT $max";
                command.Parameters.AddWithValue("$run", state.RunId); command.Parameters.AddWithValue("$max", Math.Clamp(maximum, 1, 1000));
                command.Parameters.AddWithValue("$before", beforeId);
                using var reader = command.ExecuteReader(); while (reader.Read())
                {
                    string json = reader.GetString(0); if (Hash(json) != reader.GetString(1)) throw new InvalidDataException("영속 기록 체크섬 불일치");
                    result.Add(JsonSerializer.Deserialize<BankruptcyRecord>(json)!);
                }
            }
        }
        EventRecord[] queued;
        lock (pending) queued = pendingBankruptcies.GetValueOrDefault(state.RunId, []).ToArray();
        foreach (var item in queued)
        {
            if (item.Id >= beforeId) continue;
            var value = JsonSerializer.Deserialize<BankruptcyRecord>(item.Json)!;
            result.Add(value);
        }
        return result;
    }
}
