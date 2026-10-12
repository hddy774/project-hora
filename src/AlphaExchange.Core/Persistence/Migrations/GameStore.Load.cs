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
        if ((string?)command.ExecuteScalar() != "6") throw new InvalidDataException("통계 파일 버전 오류");
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
        if (HasLegacyMarket) message = "이전 시장은 보관했습니다. 15종목의 새 시장을 시작하세요.";
        return null;
    }
    public bool HasLegacyMarket => File.Exists(Path.Combine(directory, "history-v5.sqlite")) ||
        File.Exists(Path.Combine(directory, "market-v4.json")) || File.Exists(Path.Combine(directory, "market-v3.json")) ||
        File.Exists(Path.Combine(directory, "season-v2.json"));
    static DailySnapshot DailyHistoryRow(DailySnapshot source)
    {
        var snapshot = CheckpointCopy.Daily(source);
        snapshot.Resolution = 24;
        return snapshot;
    }
}
