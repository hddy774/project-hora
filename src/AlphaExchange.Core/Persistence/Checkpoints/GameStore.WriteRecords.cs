using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace AlphaExchange.Core;

public sealed partial class GameStore
{
    static void WriteHours(SqliteCommand command, string run, (HistoryRecord r, byte[] data, string hash, DailySnapshot point)[] hours)
    {
        foreach (var (record, data, hash, point) in hours)
        {
            command.CommandText = "SELECT hash FROM hours WHERE run=$run AND hour=$hour AND resolution=$res";
            command.Parameters.Clear(); command.Parameters.AddWithValue("$run", run);
            command.Parameters.AddWithValue("$hour", record.Hour); command.Parameters.AddWithValue("$res", record.Resolution);
            string? existing = (string?)command.ExecuteScalar();
            if (existing is not null && existing != hash) throw new InvalidDataException("동일 시간 통계가 다른 값으로 재기록되었습니다.");
            if (existing is not null) continue;
            command.CommandText = "INSERT INTO hours(run,hour,resolution,cap,data,hash,price_index,return_index,investor_cash,institution_equity,retail_equity,institution_income,retail_income) VALUES($run,$hour,$res,$cap,$data,$hash,$price,$return,$cash,$institution,$retail,$institution_income,$retail_income)";
            command.Parameters.AddWithValue("$cap", point.Capitalization); command.Parameters.AddWithValue("$price", point.PriceIndex);
            command.Parameters.AddWithValue("$return", point.TotalReturnIndex); command.Parameters.AddWithValue("$cash", point.InstitutionCash + point.RetailCash);
            command.Parameters.AddWithValue("$institution", point.InstitutionEquity); command.Parameters.AddWithValue("$retail", point.RetailEquity);
            command.Parameters.AddWithValue("$institution_income", point.CohortTradingBasis == 6 ? point.InstitutionTradingIncome : DBNull.Value);
            command.Parameters.AddWithValue("$retail_income", point.CohortTradingBasis == 6 ? point.RetailTradingIncome : DBNull.Value);
            command.Parameters.AddWithValue("$data", data); command.Parameters.AddWithValue("$hash", hash);
            command.ExecuteNonQuery();
        }
    }

    static void WriteSeasons(SqliteCommand command, string run, (ArchivedSnapshot r, byte[] data)[] seasons)
    {
        foreach (var (record, data) in seasons)
        {
            command.CommandText = "INSERT INTO seasons(run,season,data) VALUES($run,$season,$data) ON CONFLICT(run,season) DO UPDATE SET data=excluded.data";
            command.Parameters.Clear(); command.Parameters.AddWithValue("$run", run); command.Parameters.AddWithValue("$season", record.Season);
            command.Parameters.AddWithValue("$data", data); command.ExecuteNonQuery();
            var result = JsonSerializer.Deserialize<SeasonResult>(record.Json)!;
            foreach (var report in result.CompanyReports) if (report.SecurityId.Length > 0) WriteReport(command, run, report);
        }
    }

    static void WriteEvents(SqliteCommand command, string run, EventRecord[] events)
    {
        foreach (var record in events)
        {
            var e = JsonSerializer.Deserialize<CorporateEvent>(record.Json)!;
            command.Parameters.Clear(); command.CommandText = "SELECT data FROM events WHERE run=$run AND id=$id";
            command.Parameters.AddWithValue("$run", run); command.Parameters.AddWithValue("$id", e.Id);
            string? previousEvent = (string?)command.ExecuteScalar();
            if (previousEvent is not null && previousEvent != record.Json) throw new InvalidDataException("동일 기업행동 ID의 내용 불일치");
            command.CommandText = "INSERT INTO events(run,id,hour,security,other,kind,data) VALUES($run,$id,$hour,$security,$other,$kind,$data) ON CONFLICT(run,id) DO NOTHING";
            command.Parameters.Clear(); command.Parameters.AddWithValue("$run", run); command.Parameters.AddWithValue("$id", e.Id);
            command.Parameters.AddWithValue("$hour", e.Hour); command.Parameters.AddWithValue("$security", e.SecurityId);
            command.Parameters.AddWithValue("$other", e.OtherSecurityId); command.Parameters.AddWithValue("$kind", (int)e.Kind);
            command.Parameters.AddWithValue("$data", record.Json); command.ExecuteNonQuery();
        }
    }
}
