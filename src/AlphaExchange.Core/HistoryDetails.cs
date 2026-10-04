using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace AlphaExchange.Core;

public sealed partial class GameStore
{
    static void WriteReport(SqliteCommand command,string run,CompanyReport report)
    {
        command.CommandText = "INSERT INTO reports(run,security,season,basis,opening,data) VALUES($run,$security,$season,$basis,$opening,$data) ON CONFLICT(run,security,season,basis,opening) DO UPDATE SET data=excluded.data";
        command.Parameters.Clear(); command.Parameters.AddWithValue("$run",run); command.Parameters.AddWithValue("$security",report.SecurityId);
        command.Parameters.AddWithValue("$season",report.Season); command.Parameters.AddWithValue("$basis",report.AccountingBasis);
        command.Parameters.AddWithValue("$opening",report.IsOpening ? 1 : 0); command.Parameters.AddWithValue("$data",Compress(JsonSerializer.Serialize(report)));
        command.ExecuteNonQuery();
    }
    public IReadOnlyList<CompanyReport> ReadReports(string run,string securityId,long beforeSeason,int maximum=500)
    {
        if (!File.Exists(SavePath)) return [];
        using var connection = Open(SavePath,true); using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM reports WHERE run=$run AND security=$security AND season<=$season";
        command.Parameters.AddWithValue("$run",run); command.Parameters.AddWithValue("$security",securityId); command.Parameters.AddWithValue("$season",beforeSeason);
        int count = Convert.ToInt32(command.ExecuteScalar());
        int chunkSize = Math.Max(1,(int)Math.Ceiling((double)count / Math.Max(1,maximum/4)));
        command.CommandText = "SELECT data FROM reports WHERE run=$run AND security=$security AND season<=$season ORDER BY season,basis,opening DESC";
        var result = new List<CompanyReport>(); var chunk = new List<CompanyReport>();
        void Flush()
        {
            if (chunk.Count == 0) return;
            result.Add(chunk[0]); result.Add(chunk.MinBy(r => r.Equity)!); result.Add(chunk.MaxBy(r => r.Equity)!); result.Add(chunk[^1]); chunk.Clear();
        }
        using var reader = command.ExecuteReader();
        while (reader.Read()) { chunk.Add(JsonSerializer.Deserialize<CompanyReport>(Decode((byte[])reader[0],2*1024*1024))!); if (chunk.Count >= chunkSize) Flush(); }
        Flush();
        return result.DistinctBy(r => (r.Season,r.AccountingBasis,r.IsOpening)).OrderBy(r => r.Season).ToList();
    }
    public IReadOnlyList<CorporateEvent> ReadEvents(GameState state,string? securityId=null,long beforeId=long.MaxValue,int maximum=60)
    {
        var result = new List<CorporateEvent>();
        if (File.Exists(SavePath))
        {
            using var connection = Open(SavePath,true); using var command = connection.CreateCommand();
            command.CommandText = "SELECT data FROM events WHERE run=$run AND id<$before AND ($security='' OR security=$security OR other=$security) ORDER BY id DESC LIMIT $maximum";
            command.Parameters.AddWithValue("$run",state.RunId); command.Parameters.AddWithValue("$before",beforeId);
            command.Parameters.AddWithValue("$security",securityId ?? ""); command.Parameters.AddWithValue("$maximum",maximum);
            using var reader = command.ExecuteReader(); while (reader.Read()) result.Add(JsonSerializer.Deserialize<CorporateEvent>(reader.GetString(0))!);
        }
        lock (pending) foreach (var e in pendingEvents.GetValueOrDefault(state.RunId,[]))
        {
            var record = JsonSerializer.Deserialize<CorporateEvent>(e.Json)!;
            if (record.Id < beforeId && (securityId is null || record.SecurityId == securityId || record.OtherSecurityId == securityId)) result.Add(record);
        }
        result.AddRange(state.CorporateEvents.Where(e => e.Id < beforeId && (securityId is null || e.SecurityId == securityId || e.OtherSecurityId == securityId)));
        return result.DistinctBy(e => e.Id).OrderByDescending(e => e.Id).Take(maximum).ToArray();
    }
}
