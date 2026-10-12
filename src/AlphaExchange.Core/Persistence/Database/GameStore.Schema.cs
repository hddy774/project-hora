using System.Text;
using Microsoft.Data.Sqlite;

namespace AlphaExchange.Core;

public sealed partial class GameStore
{
    SqliteConnection Open(string path, bool readOnly = false)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        connection.Open();
        using var command = connection.CreateCommand(); command.CommandText = "PRAGMA busy_timeout=5000;"; command.ExecuteNonQuery();
        return connection;
    }
    void Schema(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=FULL;
            CREATE TABLE IF NOT EXISTS meta(key TEXT PRIMARY KEY,value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS runs(id TEXT PRIMARY KEY,hour INTEGER NOT NULL,state BLOB NOT NULL,hash TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS hours(run TEXT NOT NULL,hour INTEGER NOT NULL,resolution INTEGER NOT NULL,
                cap INTEGER NOT NULL,data BLOB NOT NULL,hash TEXT NOT NULL,PRIMARY KEY(run,hour,resolution));
            CREATE TABLE IF NOT EXISTS events(run TEXT NOT NULL,id INTEGER NOT NULL,hour INTEGER NOT NULL,security TEXT NOT NULL,
                other TEXT NOT NULL,kind INTEGER NOT NULL,data TEXT NOT NULL,PRIMARY KEY(run,id));
            CREATE INDEX IF NOT EXISTS event_security ON events(run,security,id);
            CREATE TABLE IF NOT EXISTS votes(run TEXT NOT NULL,id INTEGER NOT NULL,hour INTEGER NOT NULL,security TEXT NOT NULL,
                data TEXT NOT NULL,hash TEXT NOT NULL,PRIMARY KEY(run,id));
            CREATE INDEX IF NOT EXISTS vote_security ON votes(run,security,id);
            CREATE TABLE IF NOT EXISTS bankruptcies(run TEXT NOT NULL,id INTEGER NOT NULL,hour INTEGER NOT NULL,kind INTEGER NOT NULL,
                entity TEXT NOT NULL,data TEXT NOT NULL,hash TEXT NOT NULL,PRIMARY KEY(run,id));
            CREATE TABLE IF NOT EXISTS reports(run TEXT NOT NULL,security TEXT NOT NULL,season INTEGER NOT NULL,basis INTEGER NOT NULL,
                opening INTEGER NOT NULL,data BLOB NOT NULL,PRIMARY KEY(run,security,season,basis,opening));
            CREATE TABLE IF NOT EXISTS seasons(run TEXT NOT NULL,season INTEGER NOT NULL,data BLOB NOT NULL,PRIMARY KEY(run,season));
            CREATE TABLE IF NOT EXISTS activities(run TEXT NOT NULL,id INTEGER NOT NULL,hour INTEGER NOT NULL,data TEXT NOT NULL,hash TEXT NOT NULL,PRIMARY KEY(run,id));
            CREATE TABLE IF NOT EXISTS world_votes(run TEXT NOT NULL,id INTEGER NOT NULL,hour INTEGER NOT NULL,data TEXT NOT NULL,hash TEXT NOT NULL,PRIMARY KEY(run,id));
            INSERT OR IGNORE INTO meta(key,value) VALUES('version','5');
            """;
        cmd.ExecuteNonQuery();
        cmd.CommandText = "SELECT value FROM meta WHERE key='version'";
        if ((string?)cmd.ExecuteScalar() != "5") throw new InvalidDataException("통계 파일 버전을 지원하지 않습니다.");
        cmd.CommandText = "PRAGMA table_info(hours)";
        var columns = new HashSet<string>(); using (var reader = cmd.ExecuteReader()) while (reader.Read()) columns.Add(reader.GetString(1));
        foreach (string name in new[] { "price_index", "return_index", "investor_cash", "institution_equity", "retail_equity", "institution_income", "retail_income" })
            if (!columns.Contains(name)) { cmd.CommandText = $"ALTER TABLE hours ADD COLUMN {name} REAL"; cmd.ExecuteNonQuery(); }
        cmd.CommandText = "PRAGMA table_info(runs)";
        bool minute = false; using (var reader = cmd.ExecuteReader()) while (reader.Read()) if (reader.GetString(1) == "minute") minute = true;
        if (!minute) { cmd.CommandText = "ALTER TABLE runs ADD COLUMN minute INTEGER NOT NULL DEFAULT 0"; cmd.ExecuteNonQuery(); }
    }
}
