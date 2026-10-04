using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace AlphaExchange.Core;

public sealed record ArchivedSnapshot(long Season, string Path, string Json);
public sealed record HistoryRecord(long Hour, int Resolution, string Json);
public sealed record EventRecord(long Id,long Hour,string Json);
public sealed record SaveSnapshot(string RunId, long Hour, string Json, ArchivedSnapshot[] Seasons, HistoryRecord[] History)
{
    public EventRecord[] Events { get; init; } = [];
    public CompanyReport[] Reports { get; init; } = [];
}

public sealed partial class GameStore
{
    readonly string directory;
    readonly object writeGate = new();
    readonly Dictionary<string, List<HistoryRecord>> pending = [];
    readonly Dictionary<string, List<EventRecord>> pendingEvents = [];
    readonly Dictionary<string,long> queuedDailyThrough = [];
    volatile bool databaseReady;
    long lastBackup;
    string databaseName = "history-v5.sqlite";
    string activeRun = "";
    public string SavePath => Path.Combine(directory, databaseName);
    public string BackupPath => SavePath + ".bak";
    public long LastCommittedHour { get; private set; }
    public int PendingCount { get { lock (pending) return pending.Values.Sum(p => p.Count); } }
    public GameStore(string directory) { this.directory = directory; Directory.CreateDirectory(directory); }
    public string SeasonPath(GameState state, long season) => Path.Combine(directory, "seasons", state.RunId, $"season-{season:D8}.json");

    SqliteConnection Open(string path, bool readOnly = false)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString());
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
            CREATE TABLE IF NOT EXISTS reports(run TEXT NOT NULL,security TEXT NOT NULL,season INTEGER NOT NULL,basis INTEGER NOT NULL,
                opening INTEGER NOT NULL,data BLOB NOT NULL,PRIMARY KEY(run,security,season,basis,opening));
            CREATE TABLE IF NOT EXISTS seasons(run TEXT NOT NULL,season INTEGER NOT NULL,data BLOB NOT NULL,PRIMARY KEY(run,season));
            INSERT OR IGNORE INTO meta(key,value) VALUES('version','5');
            """;
        cmd.ExecuteNonQuery();
        cmd.CommandText = "SELECT value FROM meta WHERE key='version'";
        if ((string?)cmd.ExecuteScalar() != "5") throw new InvalidDataException("통계 파일 버전을 지원하지 않습니다.");
        cmd.CommandText="PRAGMA table_info(hours)";
        var columns=new HashSet<string>(); using(var reader=cmd.ExecuteReader()) while(reader.Read()) columns.Add(reader.GetString(1));
        foreach(string name in new[] { "price_index","return_index","investor_cash","institution_equity","retail_equity" })
            if(!columns.Contains(name)) { cmd.CommandText=$"ALTER TABLE hours ADD COLUMN {name} REAL"; cmd.ExecuteNonQuery(); }
    }
    static byte[] Compress(string json)
    {
        using var destination = new MemoryStream();
        using (var compressor = new BrotliStream(destination, CompressionLevel.Fastest, true))
        { byte[] bytes = Encoding.UTF8.GetBytes(json); compressor.Write(bytes); }
        return destination.ToArray();
    }
    static string Decode(byte[] data, int maximum = 128 * 1024 * 1024)
    {
        using var source = new MemoryStream(data); using var decompressor = new BrotliStream(source, CompressionMode.Decompress);
        using var destination = new MemoryStream(); byte[] buffer = new byte[32768]; int length;
        while ((length = decompressor.Read(buffer)) > 0)
        { if (destination.Length + length > maximum) throw new InvalidDataException("저장 데이터 크기 제한"); destination.Write(buffer,0,length); }
        return Encoding.UTF8.GetString(destination.ToArray());
    }
    static string Hash(string json) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    static string CheckedDecode(byte[] bytes, string hash, int limit = 128 * 1024 * 1024)
    { string json = Decode(bytes,limit); if (Hash(json) != hash) throw new InvalidDataException("저장 체크섬 불일치"); return json; }

    public void Attach(GameEngine engine)
    {
        if (engine.HistorySource is RunQuery query && query.Store == this) return;
        string run = engine.State.RunId; activeRun = run;
        lock (pending) if (!pending.ContainsKey(run)) pending[run] = [];
        engine.HourRecorded += snapshot =>
        {
            string json = JsonSerializer.Serialize(snapshot);
            lock (pending) pending[run].Add(new HistoryRecord(snapshot.Hour,1,json));
        };
        lock (pending) if (!pendingEvents.ContainsKey(run)) pendingEvents[run] = [];
        engine.CorporateEventRecorded += e => { lock (pending) pendingEvents[run].Add(new EventRecord(e.Id,e.Hour,JsonSerializer.Serialize(e))); };
        lock (pending) foreach (var e in engine.State.CorporateEvents)
            if (!pendingEvents[run].Any(r => r.Id == e.Id)) pendingEvents[run].Add(new EventRecord(e.Id,e.Hour,JsonSerializer.Serialize(e)));
        engine.HistorySource = new RunQuery(this,run);
        // Initial or migrated state is part of the first atomic save.
        AddPending(run, engine.CaptureSnapshot());
    }
    void AddPending(string run, DailySnapshot snapshot)
    {
        var record = new HistoryRecord(snapshot.Hour,snapshot.Resolution,JsonSerializer.Serialize(snapshot));
        lock (pending)
        {
            if (!pending.TryGetValue(run,out var records)) pending[run] = records = [];
            if (!records.Any(r => r.Hour == record.Hour && r.Resolution == record.Resolution)) records.Add(record);
        }
    }
    public SaveSnapshot PrepareSave(GameEngine engine)
    {
        Attach(engine);
        var state = engine.State;
        var seasons = state.PendingSeasons.OrderBy(r => r.Season)
            .Select(r => new ArchivedSnapshot(r.Season,SeasonPath(state,r.Season),JsonSerializer.Serialize(r))).ToArray();
        long through = queuedDailyThrough.GetValueOrDefault(state.RunId,-1);
        foreach (var daily in state.DailyHistory.Where(d => d.Hour > through)) AddPending(state.RunId,NormalizeLegacy(daily,engine));
        queuedDailyThrough[state.RunId] = state.DailyHistory.Max(d => d.Hour);
        HistoryRecord[] records;
        lock (pending) records = pending[state.RunId].Where(r => r.Hour <= state.CompletedHours).ToArray();
        EventRecord[] events;
        lock (pending) events = pendingEvents[state.RunId].Where(e => e.Hour <= state.CompletedHours).ToArray();
        return new SaveSnapshot(state.RunId,state.CompletedHours,engine.Serialize(),seasons,records)
        { Events = events, Reports = state.Stocks.SelectMany(s => s.Reports).Select(r => JsonSerializer.Deserialize<CompanyReport>(JsonSerializer.Serialize(r))!).ToArray() };
    }
    public void WriteSnapshot(SaveSnapshot snapshot)
    {
        // All payloads are immutable. Compression and I/O do not read the live engine.
        using (var header=JsonDocument.Parse(snapshot.Json))
            if(header.RootElement.GetProperty("RunId").GetString()!=snapshot.RunId || header.RootElement.GetProperty("CompletedHours").GetInt64()!=snapshot.Hour ||
                snapshot.History.Any(r=>r.Hour<0 || r.Hour>snapshot.Hour || r.Resolution is not (1 or 24)) || snapshot.Events.Any(e=>e.Hour<0 || e.Hour>snapshot.Hour))
                throw new InvalidDataException("체크포인트 기록 범위 불일치");
        var hours = snapshot.History.Select(r => (r, data:Compress(r.Json), hash:Hash(r.Json),
            point:JsonSerializer.Deserialize<DailySnapshot>(r.Json)!)).ToArray();
        byte[] state = Compress(snapshot.Json);
        var seasons = snapshot.Seasons.Select(r => (r,data:Compress(r.Json))).ToArray();
        lock (writeGate)
        {
            using var connection = Open(SavePath); Schema(connection); databaseReady = true;
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText="SELECT hour FROM runs WHERE id=$run"; command.Parameters.AddWithValue("$run",snapshot.RunId);
            if(command.ExecuteScalar() is long committed && committed>snapshot.Hour) throw new InvalidDataException("과거 체크포인트로 되돌릴 수 없습니다.");
            foreach (var (record,data,hash,point) in hours)
            {
                command.CommandText = "SELECT hash FROM hours WHERE run=$run AND hour=$hour AND resolution=$res";
                command.Parameters.Clear(); command.Parameters.AddWithValue("$run",snapshot.RunId);
                command.Parameters.AddWithValue("$hour",record.Hour); command.Parameters.AddWithValue("$res",record.Resolution);
                string? existing = (string?)command.ExecuteScalar();
                if (existing is not null && existing != hash) throw new InvalidDataException("동일 시간 통계가 다른 값으로 재기록되었습니다.");
                if (existing is not null) continue;
                command.CommandText = "INSERT INTO hours(run,hour,resolution,cap,data,hash,price_index,return_index,investor_cash,institution_equity,retail_equity) VALUES($run,$hour,$res,$cap,$data,$hash,$price,$return,$cash,$institution,$retail)";
                command.Parameters.AddWithValue("$cap",point.Capitalization); command.Parameters.AddWithValue("$price",point.PriceIndex);
                command.Parameters.AddWithValue("$return",point.TotalReturnIndex); command.Parameters.AddWithValue("$cash",point.InstitutionCash+point.RetailCash);
                command.Parameters.AddWithValue("$institution",point.InstitutionEquity); command.Parameters.AddWithValue("$retail",point.RetailEquity);
                command.Parameters.AddWithValue("$data",data); command.Parameters.AddWithValue("$hash",hash);
                command.ExecuteNonQuery();
            }
            foreach (var (record,data) in seasons)
            {
                command.CommandText = "INSERT INTO seasons(run,season,data) VALUES($run,$season,$data) ON CONFLICT(run,season) DO UPDATE SET data=excluded.data";
                command.Parameters.Clear(); command.Parameters.AddWithValue("$run",snapshot.RunId); command.Parameters.AddWithValue("$season",record.Season);
                command.Parameters.AddWithValue("$data",data); command.ExecuteNonQuery();
                var result = JsonSerializer.Deserialize<SeasonResult>(record.Json)!;
                foreach (var report in result.CompanyReports) if (report.SecurityId.Length > 0) WriteReport(command,snapshot.RunId,report);
            }
            foreach (var record in snapshot.Events)
            {
                var e = JsonSerializer.Deserialize<CorporateEvent>(record.Json)!;
                command.Parameters.Clear(); command.CommandText = "SELECT data FROM events WHERE run=$run AND id=$id";
                command.Parameters.AddWithValue("$run",snapshot.RunId); command.Parameters.AddWithValue("$id",e.Id);
                string? previousEvent = (string?)command.ExecuteScalar();
                if (previousEvent is not null && previousEvent != record.Json) throw new InvalidDataException("동일 기업행동 ID의 내용 불일치");
                command.CommandText = "INSERT INTO events(run,id,hour,security,other,kind,data) VALUES($run,$id,$hour,$security,$other,$kind,$data) ON CONFLICT(run,id) DO NOTHING";
                command.Parameters.Clear(); command.Parameters.AddWithValue("$run",snapshot.RunId); command.Parameters.AddWithValue("$id",e.Id);
                command.Parameters.AddWithValue("$hour",e.Hour); command.Parameters.AddWithValue("$security",e.SecurityId);
                command.Parameters.AddWithValue("$other",e.OtherSecurityId); command.Parameters.AddWithValue("$kind",(int)e.Kind);
                command.Parameters.AddWithValue("$data",record.Json); command.ExecuteNonQuery();
            }
            foreach (var report in snapshot.Reports) WriteReport(command,snapshot.RunId,report);
            command.CommandText = "INSERT INTO runs(id,hour,state,hash) VALUES($run,$hour,$state,$hash) ON CONFLICT(id) DO UPDATE SET hour=excluded.hour,state=excluded.state,hash=excluded.hash WHERE excluded.hour>=runs.hour";
            command.Parameters.Clear(); command.Parameters.AddWithValue("$run",snapshot.RunId); command.Parameters.AddWithValue("$hour",snapshot.Hour);
            command.Parameters.AddWithValue("$state",state); command.Parameters.AddWithValue("$hash",Hash(snapshot.Json)); command.ExecuteNonQuery();
            if (activeRun == "" || activeRun == snapshot.RunId)
            {
                command.CommandText = "INSERT INTO meta(key,value) VALUES('current',$run) ON CONFLICT(key) DO UPDATE SET value=excluded.value";
                command.Parameters.Clear(); command.Parameters.AddWithValue("$run",snapshot.RunId); command.ExecuteNonQuery();
            }
            transaction.Commit();
            if (activeRun == "" || activeRun == snapshot.RunId) LastCommittedHour = Math.Max(LastCommittedHour,snapshot.Hour);
            lock (pending)
            {
                if (pending.TryGetValue(snapshot.RunId,out var records)) records.RemoveAll(r => r.Hour <= snapshot.Hour);
                if (pendingEvents.TryGetValue(snapshot.RunId,out var events)) events.RemoveAll(e => e.Hour <= snapshot.Hour);
            }
            if (!File.Exists(BackupPath) || Environment.TickCount64 - lastBackup >= 60000 || snapshot.Hour % 720 == 0)
            { Backup(connection,BackupPath); lastBackup = Environment.TickCount64; }
        }
    }
    public static void Acknowledge(GameEngine engine, SaveSnapshot snapshot)
    {
        if (engine.State.RunId != snapshot.RunId || snapshot.Seasons.Length == 0) return;
        long last = snapshot.Seasons.Max(s => s.Season);
        engine.State.LastArchivedSeason = Math.Max(engine.State.LastArchivedSeason,last);
        engine.State.PendingSeasons.RemoveAll(s => s.Season <= last);
    }
    public void Save(GameEngine engine) { var snapshot = PrepareSave(engine); WriteSnapshot(snapshot); Acknowledge(engine,snapshot); }
    static void Backup(SqliteConnection source, string path)
    {
        string temp = path + ".tmp";
        if (File.Exists(temp)) File.Delete(temp);
        using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=temp,Pooling=false }.ToString()))
        { destination.Open(); source.BackupDatabase(destination); }
        File.Move(temp,path,true);
    }
    GameEngine LoadDatabase(string path)
    {
        using var connection = Open(path,true); using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check";
        if ((string?)command.ExecuteScalar() != "ok") throw new InvalidDataException("통계 파일 무결성 오류");
        command.CommandText = "SELECT value FROM meta WHERE key='version'";
        if ((string?)command.ExecuteScalar() != "5") throw new InvalidDataException("통계 파일 버전 오류");
        command.CommandText = "SELECT id,hour,state,hash FROM runs WHERE id=(SELECT value FROM meta WHERE key='current')";
        using var reader = command.ExecuteReader();
        if (!reader.Read()) throw new InvalidDataException("게임 체크포인트가 없습니다.");
        var game = GameEngine.Deserialize(CheckedDecode((byte[])reader[2],reader.GetString(3)));
        if (game.State.RunId != reader.GetString(0) || game.State.CompletedHours != reader.GetInt64(1)) throw new InvalidDataException("통계/상태 시간 불일치");
        LastCommittedHour = game.State.CompletedHours; databaseReady = true;
        return game;
    }
    public GameEngine? Load(out string message)
    {
        message = "";
        if (File.Exists(SavePath))
        {
            try { var game = LoadDatabase(SavePath); Attach(game); return game; }
            catch (Exception e) when (StorageException(e)) { message = "저장 파일을 읽지 못했습니다. 원본을 보존합니다."; }
            if (File.Exists(BackupPath))
            {
                try
                {
                    var game = LoadDatabase(BackupPath);
                    string preserved = SavePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                    lock (writeGate)
                    {
                        File.Move(SavePath,preserved);
                        foreach (string suffix in new[] { "-wal","-shm" }) if (File.Exists(SavePath+suffix)) File.Move(SavePath+suffix,preserved+suffix);
                        File.Copy(BackupPath,SavePath);
                    }
                    Attach(game); message = "정상 통계 백업에서 복구했습니다. 손상 원본도 보존했습니다."; return game;
                }
                catch (Exception e) when (StorageException(e)) { message = "저장과 백업을 읽지 못했습니다. 원본 보존 · 파일 가져오기로 복원할 수 있습니다."; }
            }
            return null;
        }
        foreach (string path in new[] { "market-v4.json","market-v4.json.bak","market-v3.json","market-v3.json.bak","season-v2.json" })
        {
            string full = Path.Combine(directory,path); if (!File.Exists(full)) continue;
            try
            {
                var game = GameEngine.Deserialize(File.ReadAllText(full));
                Attach(game);
                var archives = new List<ArchivedSnapshot>();
                string seasons = Path.Combine(directory,"seasons",game.State.RunId);
                if (Directory.Exists(seasons)) foreach (string file in Directory.EnumerateFiles(seasons,"season-*.json").Order())
                {
                    var result = JsonSerializer.Deserialize<SeasonResult>(File.ReadAllText(file)) ?? throw new InvalidDataException("시즌 파일 오류");
                    if (result.Season < 1 || result.Season >= game.State.Season) continue;
                    foreach (var day in result.Days) AddPending(game.State.RunId,NormalizeLegacy(day,game));
                    if (result.Start is not null) AddPending(game.State.RunId,NormalizeLegacy(result.Start,game));
                    if (result.End is not null) AddPending(game.State.RunId,NormalizeLegacy(result.End,game));
                    for (int i = 0; i < result.CompanyReports.Count && i < game.State.Stocks.Count; i++)
                    {
                        var r = result.CompanyReports[i];
                        if (r.SecurityId.Length == 0) { r.SecurityId = game.State.Stocks[i].SecurityId; r.AccountingBasis = 4; r.AverageShares = r.OutstandingShares = game.State.Stocks[i].OutstandingShares; }
                    }
                    archives.Add(new ArchivedSnapshot(result.Season,file,JsonSerializer.Serialize(result)));
                }
                foreach (var day in game.State.DailyHistory) AddPending(game.State.RunId,NormalizeLegacy(day,game));
                if (game.State.OpeningSnapshot is not null) AddPending(game.State.RunId,NormalizeLegacy(game.State.OpeningSnapshot,game));
                var snapshot = PrepareSave(game);
                snapshot = snapshot with { Seasons = archives.Concat(snapshot.Seasons).GroupBy(s => s.Season).Select(g => g.Last()).ToArray() };
                var staging = new GameStore(directory) { databaseName = "migration-" + game.State.RunId + ".sqlite", activeRun = game.State.RunId };
                try
                {
                    staging.WriteSnapshot(snapshot);
                    staging.LoadDatabase(staging.SavePath);
                    File.Move(staging.SavePath,SavePath);
                    if (File.Exists(staging.BackupPath)) File.Move(staging.BackupPath,BackupPath,true);
                }
                finally
                {
                    foreach (string suffix in new[] { "","-wal","-shm",".bak" }) if (File.Exists(staging.SavePath+suffix)) File.Delete(staging.SavePath+suffix);
                }
                lock (pending) pending[game.State.RunId].RemoveAll(r => r.Hour <= snapshot.Hour);
                databaseReady = true; LastCommittedHour = snapshot.Hour; Acknowledge(game,snapshot);
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
            snapshot.SplitFactors = Enumerable.Repeat(1.0,count).ToArray();
            snapshot.PriceIndex = snapshot.TotalReturnIndex = 1000.0 * snapshot.Capitalization / Math.Max(1,game.State.OpeningSnapshot?.Capitalization ?? snapshot.Capitalization);
            foreach (var t in snapshot.Institutions) t.ReturnIndex = (double)t.Equity / Math.Max(1,game.Owner(t.Id).OpeningEquity);
        }
        return snapshot;
    }
    public SeasonResult? ReadSeason(GameState state, long season)
    {
        if (season < 1 || season >= state.Season) return null;
        var pendingSeason = state.PendingSeasons.FirstOrDefault(s => s.Season == season); if (pendingSeason is not null) return pendingSeason;
        if (File.Exists(SavePath))
        {
            using var connection = Open(SavePath,true); using var command = connection.CreateCommand();
            command.CommandText = "SELECT data FROM seasons WHERE run=$run AND season=$season";
            command.Parameters.AddWithValue("$run",state.RunId); command.Parameters.AddWithValue("$season",season);
            if (command.ExecuteScalar() is byte[] data) return JsonSerializer.Deserialize<SeasonResult>(Decode(data));
        }
        string path = SeasonPath(state,season);
        return File.Exists(path) ? JsonSerializer.Deserialize<SeasonResult>(File.ReadAllText(path)) : null;
    }
    public static bool StorageException(Exception e) => e is IOException or InvalidDataException or JsonException
        or InvalidOperationException or ArgumentException or NullReferenceException or OverflowException or SqliteException;

    sealed class RunQuery(GameStore store, string run) : IHistoryQuery
    {
        public GameStore Store => store;
        List<DailySnapshot> Pending(long start,long end)
        {
            lock (store.pending) return store.pending.GetValueOrDefault(run,[]).Where(r => r.Hour >= start && r.Hour <= end)
                .Select(r => JsonSerializer.Deserialize<DailySnapshot>(r.Json)!).ToList();
        }
        public DailySnapshot? At(long hour)
        {
            var memory = Pending(0,hour).OrderByDescending(s => s.Hour).ThenBy(s => s.Resolution).FirstOrDefault();
            DailySnapshot? disk = null;
            if (store.databaseReady && File.Exists(store.SavePath))
            {
                using var connection = store.Open(store.SavePath,true); using var command = connection.CreateCommand();
                command.CommandText = "SELECT data,hash FROM hours WHERE run=$run AND hour<=$hour ORDER BY hour DESC,resolution ASC LIMIT 1";
                command.Parameters.AddWithValue("$run",run); command.Parameters.AddWithValue("$hour",hour);
                using var reader = command.ExecuteReader();
                if (reader.Read()) disk = JsonSerializer.Deserialize<DailySnapshot>(CheckedDecode((byte[])reader[0],reader.GetString(1),2*1024*1024));
            }
            return memory is not null && (disk is null || memory.Hour >= disk.Hour) ? memory : disk;
        }
        public IReadOnlyList<DailySnapshot> Range(long start,long end,int maximumPoints=500)
        {
            maximumPoints = Math.Clamp(maximumPoints,16,1000);
            var rows = new List<DailySnapshot>();
            if (store.databaseReady && File.Exists(store.SavePath))
            {
                using var connection = store.Open(store.SavePath,true);
                using var schema=connection.CreateCommand(); schema.CommandText="PRAGMA table_info(hours)";
                var available=new HashSet<string>(); using(var reader=schema.ExecuteReader()) while(reader.Read()) available.Add(reader.GetString(1));
                var metrics=new[] { "cap","price_index","return_index","investor_cash","institution_equity","retail_equity" }.Where(available.Contains).ToArray();
                int perBucket=2+metrics.Length*2;
                long stride = end-start+1<=maximumPoints ? 1 : Math.Max(1,(long)Math.Ceiling((end-start+1.0)/Math.Max(1,maximumPoints/perBucket)));
                var keys = new HashSet<long>();
                foreach (string selection in new[] { "min(hour)","max(hour)" }.Concat(metrics.SelectMany(metric=>new[] { $"hour,min({metric})",$"hour,max({metric})" })))
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = $"SELECT {selection} FROM hours WHERE run=$run AND hour BETWEEN $start AND $end GROUP BY (hour-$start)/$stride";
                    command.Parameters.AddWithValue("$run",run); command.Parameters.AddWithValue("$start",start);
                    command.Parameters.AddWithValue("$end",end); command.Parameters.AddWithValue("$stride",stride);
                    using var reader = command.ExecuteReader(); while (reader.Read()) keys.Add(reader.GetInt64(0));
                }
                if (keys.Count > 0)
                {
                    using var command = connection.CreateCommand();
                    string[] names = keys.Select((_,i) => "$h"+i).ToArray();
                    command.CommandText = $"SELECT data,hash FROM hours WHERE run=$run AND hour IN ({string.Join(',',names)}) ORDER BY hour,resolution DESC";
                    command.Parameters.AddWithValue("$run",run);
                    foreach (var (key,i) in keys.Select((key,i) => (key,i))) command.Parameters.AddWithValue(names[i],key);
                    using var reader = command.ExecuteReader();
                    while (reader.Read()) rows.Add(JsonSerializer.Deserialize<DailySnapshot>(CheckedDecode((byte[])reader[0],reader.GetString(1),2*1024*1024))!);
                }
            }
            rows.AddRange(Pending(start,end));
            var sampled = Downsample(rows.GroupBy(s => s.Hour).Select(g => g.OrderBy(s => s.Resolution).First()).OrderBy(s => s.Hour).ToList(),maximumPoints);
            var gaps = Gaps(start,end);
            long previous = start;
            foreach (var point in sampled) { point.GapBefore = gaps.Any(g => g > previous && g <= point.Hour); previous = point.Hour; }
            return sampled;
        }
        List<long> Gaps(long start,long end)
        {
            var headers = new List<(long Hour,int Resolution)>();
            if (store.databaseReady && File.Exists(store.SavePath))
            {
                using var connection = store.Open(store.SavePath,true); using var command = connection.CreateCommand();
                command.CommandText = "SELECT hour,min(resolution) FROM hours WHERE run=$run AND hour BETWEEN $start AND $end GROUP BY hour ORDER BY hour";
                command.Parameters.AddWithValue("$run",run); command.Parameters.AddWithValue("$start",start); command.Parameters.AddWithValue("$end",end);
                using var reader = command.ExecuteReader(); while (reader.Read()) headers.Add((reader.GetInt64(0),reader.GetInt32(1)));
            }
            lock (store.pending) headers.AddRange(store.pending.GetValueOrDefault(run,[]).Where(r => r.Hour >= start && r.Hour <= end).Select(r => (r.Hour,r.Resolution)));
            var gaps = new List<long>(); long last = start; int resolution = 1; bool first = true;
            foreach (var record in headers.GroupBy(r => r.Hour).Select(g => g.MinBy(r => r.Resolution)).OrderBy(r => r.Hour))
            {
                if (first && record.Hour > start || !first && record.Hour-last > Math.Max(resolution,record.Resolution)) gaps.Add(record.Hour);
                first = false; last = record.Hour; resolution = record.Resolution;
            }
            if (first || last < end) gaps.Add(end);
            return gaps;
        }
        public bool Covers(long start,long end) => Gaps(start,end).Count == 0;

    }
    static IReadOnlyList<DailySnapshot> Downsample(List<DailySnapshot> rows,int maximum)
    {
        if (rows.Count <= maximum) return rows;
        int bucketSize = Math.Max(1,(int)Math.Ceiling((double)rows.Count / Math.Max(1,maximum/4)));
        var result = new List<DailySnapshot>();
        foreach (var bucket in rows.Chunk(bucketSize))
        {
            result.Add(bucket[0]); result.Add(bucket.MinBy(s => s.Capitalization)!);
            result.Add(bucket.MaxBy(s => s.Capitalization)!); result.Add(bucket[^1]);
        }
        return result.DistinctBy(s => s.Hour).OrderBy(s => s.Hour).ToList();
    }
}
