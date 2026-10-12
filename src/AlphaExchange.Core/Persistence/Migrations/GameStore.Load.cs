using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace AlphaExchange.Core;

public sealed partial class GameStore
{
    static void Backup(SqliteConnection source, string path)
    {
        string temp = path + ".tmp";
        if (File.Exists(temp)) File.Delete(temp);
        using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temp, Pooling = false }.ToString()))
        { destination.Open(); source.BackupDatabase(destination); }
        File.Move(temp, path, true);
    }
    GameEngine LoadDatabase(string path)
    {
        using var connection = Open(path, true); using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check";
        if ((string?)command.ExecuteScalar() != "ok") throw new InvalidDataException("통계 파일 무결성 오류");
        command.CommandText = "SELECT value FROM meta WHERE key='version'";
        if ((string?)command.ExecuteScalar() != "5") throw new InvalidDataException("통계 파일 버전 오류");
        command.CommandText = "SELECT id,hour,state,hash FROM runs WHERE id=(SELECT value FROM meta WHERE key='current')";
        using var reader = command.ExecuteReader();
        if (!reader.Read()) throw new InvalidDataException("게임 체크포인트가 없습니다.");
        var game = GameEngine.Deserialize(CheckedDecode((byte[])reader[2], reader.GetString(3)));
        if (game.State.RunId != reader.GetString(0) || game.State.CompletedHours != reader.GetInt64(1)) throw new InvalidDataException("통계/상태 시간 불일치");
        return game;
    }
    public GameEngine? Load(out string message)
    {
        message = "";
        if (File.Exists(SavePath))
        {
            try { var game = LoadDatabase(SavePath); activeRun = game.State.RunId; LastCommittedHour = game.State.CompletedHours; databaseReady = true; Attach(game); return game; }
            catch (Exception e) when (StorageException(e)) { message = "저장 파일을 읽지 못했습니다. 원본을 보존합니다."; }
            if (File.Exists(BackupPath))
            {
                try
                {
                    var game = LoadDatabase(BackupPath);
                    string preserved = SavePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                    lock (writeGate)
                    {
                        File.Move(SavePath, preserved);
                        foreach (string suffix in new[] { "-wal", "-shm" }) if (File.Exists(SavePath + suffix)) File.Move(SavePath + suffix, preserved + suffix);
                        File.Copy(BackupPath, SavePath);
                    }
                    activeRun = game.State.RunId; LastCommittedHour = game.State.CompletedHours; databaseReady = true; Attach(game); message = "정상 통계 백업에서 복구했습니다. 손상 원본도 보존했습니다."; return game;
                }
                catch (Exception e) when (StorageException(e)) { message = "저장과 백업을 읽지 못했습니다. 원본 보존 · 파일 가져오기로 복원할 수 있습니다."; }
            }
            return null;
        }
        foreach (string path in new[] { "market-v4.json", "market-v4.json.bak", "market-v3.json", "market-v3.json.bak", "season-v2.json" })
        {
            string full = Path.Combine(directory, path); if (!File.Exists(full)) continue;
            try
            {
                var game = GameEngine.Deserialize(File.ReadAllText(full));
                Attach(game);
                var archives = new List<ArchivedSnapshot>();
                string seasons = Path.Combine(directory, "seasons", game.State.RunId);
                if (Directory.Exists(seasons)) foreach (string file in Directory.EnumerateFiles(seasons, "season-*.json").Order())
                    {
                        var result = JsonSerializer.Deserialize<SeasonResult>(File.ReadAllText(file)) ?? throw new InvalidDataException("시즌 파일 오류");
                        if (result.Season < 1 || result.Season >= game.State.Season) continue;
                        foreach (var day in result.Days) AddPending(game.State.RunId, NormalizeLegacy(day, game));
                        if (result.Start is not null) AddPending(game.State.RunId, NormalizeLegacy(result.Start, game));
                        if (result.End is not null) AddPending(game.State.RunId, NormalizeLegacy(result.End, game));
                        for (int i = 0; i < result.CompanyReports.Count && i < game.State.Stocks.Count; i++)
                        {
                            var r = result.CompanyReports[i];
                            if (r.SecurityId.Length == 0) { r.SecurityId = game.State.Stocks[i].SecurityId; r.AccountingBasis = 4; r.AverageShares = r.OutstandingShares = game.State.Stocks[i].OutstandingShares; }
                        }
                        archives.Add(new ArchivedSnapshot(result.Season, file, JsonSerializer.Serialize(result)));
                    }
                foreach (var day in game.State.DailyHistory) AddPending(game.State.RunId, NormalizeLegacy(day, game));
                if (game.State.OpeningSnapshot is not null) AddPending(game.State.RunId, NormalizeLegacy(game.State.OpeningSnapshot, game));
                var snapshot = PrepareSave(game);
                snapshot = snapshot with { Seasons = archives.Concat(snapshot.Seasons).GroupBy(s => s.Season).Select(g => g.Last()).ToArray() };
                var staging = new GameStore(directory) { databaseName = "migration-" + game.State.RunId + ".sqlite", activeRun = game.State.RunId };
                try
                {
                    staging.WriteSnapshot(snapshot);
                    staging.LoadDatabase(staging.SavePath);
                    File.Move(staging.SavePath, SavePath);
                    if (File.Exists(staging.BackupPath)) File.Move(staging.BackupPath, BackupPath, true);
                }
                finally
                {
                    foreach (string suffix in new[] { "", "-wal", "-shm", ".bak" }) if (File.Exists(staging.SavePath + suffix)) File.Delete(staging.SavePath + suffix);
                }
                lock (pending) pending[game.State.RunId].RemoveAll(r => r.Hour <= snapshot.Hour);
                databaseReady = true; LastCommittedHour = snapshot.Hour; Acknowledge(game, snapshot);
                message = "v1.3으로 이전했습니다. 기존 현금·보유·일별 기록과 원본 파일을 보존합니다. 과거 미기록 시간은 채우지 않습니다.";
                return game;
            }
            catch (Exception e) when (StorageException(e)) { message = "이전 파일을 읽지 못했습니다. 원본을 보존합니다."; }
        }
        return null;
    }
    public static DailySnapshot NormalizeLegacy(DailySnapshot source, GameEngine game)
    {
        var snapshot = JsonSerializer.Deserialize<DailySnapshot>(JsonSerializer.Serialize(source))!;
        snapshot.Resolution = 24;
        if (snapshot.SecurityIds.Length == 0)
        {
            snapshot.LegacyNoFundamentals = true;
            int count = snapshot.StockPrices.Length;
            snapshot.SecurityIds = game.State.Stocks.Take(count).Select(s => s.SecurityId).ToArray();
            snapshot.StockSectors = game.State.Stocks.Take(count).Select(s => s.Sector).ToArray();
            snapshot.StockShares = game.State.LegacyShares.Length >= count ? game.State.LegacyShares.Take(count).ToArray() : game.State.Stocks.Take(count).Select(s => s.OutstandingShares).ToArray();
            snapshot.MarkPrices = snapshot.StockPrices.Select(p => (decimal)p).ToArray();
            snapshot.SplitFactors = Enumerable.Repeat(1.0, count).ToArray();
            snapshot.PriceIndex = snapshot.TotalReturnIndex = 1000.0 * snapshot.Capitalization / Math.Max(1, game.State.OpeningSnapshot?.Capitalization ?? snapshot.Capitalization);
            foreach (var t in snapshot.Institutions) t.ReturnIndex = (double)t.Equity / Math.Max(1, game.Owner(t.Id).OpeningEquity);
        }
        return snapshot;
    }
}
