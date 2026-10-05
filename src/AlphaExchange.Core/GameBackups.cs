using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AlphaExchange.Core;

public sealed record BackupManifest(int Version,string ApplicationId,string RunId,long Hour,long Bytes,string Sha256);
public sealed partial class GameStore
{
    const long MaxBackupBytes = 2L * 1024 * 1024 * 1024;
    public void Export(Stream destination, bool includeCsv = true)
    {
        string temp = Path.Combine(directory,"export-"+Guid.NewGuid().ToString("N")+".sqlite");
        try
        {
            lock (writeGate)
            { using var connection = Open(SavePath,true); Backup(connection,temp); }
            if(new FileInfo(temp).Length>MaxBackupBytes) throw new InvalidDataException("백업 크기 제한을 초과했습니다.");
            var game = LoadDatabase(temp);
            string hash;
            using (var file = File.OpenRead(temp)) hash = Convert.ToHexString(SHA256.HashData(file));
            var manifest = new BackupManifest(5,"com.alphaexchange.offline",game.State.RunId,game.State.CompletedHours,new FileInfo(temp).Length,hash);
            using var zip = new ZipArchive(destination,ZipArchiveMode.Create,true);
            using (var entry = zip.CreateEntry("history-v5.sqlite",CompressionLevel.NoCompression).Open())
            using (var file = File.OpenRead(temp)) file.CopyTo(entry);
            using (var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open(),new UTF8Encoding(false)))
                writer.Write(JsonSerializer.Serialize(manifest));
            if (includeCsv)
            {
                using var output = zip.CreateEntry("market.csv",CompressionLevel.Fastest).Open();
                ExportCsv(output,temp,game.State.RunId);
            }
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    void ExportCsv(Stream destination,string path,string run)
    {
        using var writer = new StreamWriter(destination,new UTF8Encoding(false),leaveOpen:true);
        writer.WriteLine("game_hour,season,day,hour,resolution,capitalization,price_index,total_return_index,volume,turnover,institution_equity,retail_equity,institution_cash,retail_cash,dividends,government_cash,bank_cash,exchange_cash,real_economy_cash");
        using var connection = Open(path,true); using var command = connection.CreateCommand();
        command.CommandText = "SELECT h.data,h.hash FROM hours h WHERE h.run=$run AND h.resolution=(SELECT min(resolution) FROM hours x WHERE x.run=h.run AND x.hour=h.hour) ORDER BY h.hour";
        command.Parameters.AddWithValue("$run",run); using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var s = JsonSerializer.Deserialize<DailySnapshot>(CheckedDecode((byte[])reader[0],reader.GetString(1),2*1024*1024))!;
            writer.WriteLine(string.Join(',',new object[] { s.Hour,s.Hour/720+1,s.Hour%720/24+1,s.Hour%24,s.Resolution,s.Capitalization,
                s.PriceIndex.ToString("R",CultureInfo.InvariantCulture),s.TotalReturnIndex.ToString("R",CultureInfo.InvariantCulture),
                s.Volume,s.Turnover,s.InstitutionEquity,s.RetailEquity,s.InstitutionCash,s.RetailCash,s.Dividends,
                s.GovernmentCash,s.BankCash,s.ExchangeCash,s.RealEconomyCash }));
        }
    }
    public GameEngine Import(Stream source)
    {
        string temp = Path.Combine(directory,"import-"+Guid.NewGuid().ToString("N")+".sqlite");
        try
        {
            using (var zip = new ZipArchive(source,ZipArchiveMode.Read,true))
            {
                if (zip.Entries.Count is < 2 or > 4 || zip.Entries.Any(e => e.FullName is not ("history-v5.sqlite" or "manifest.json" or "market.csv" or "companies.csv")) ||
                    zip.Entries.Select(e => e.FullName).Distinct().Count() != zip.Entries.Count)
                    throw new InvalidDataException("지원하지 않는 백업 파일 구성");
                var manifestEntry = zip.GetEntry("manifest.json") ?? throw new InvalidDataException("백업 manifest 누락");
                if (manifestEntry.Length > 4096) throw new InvalidDataException("백업 manifest 크기 오류");
                BackupManifest manifest;
                using (var stream = manifestEntry.Open()) manifest = JsonSerializer.Deserialize<BackupManifest>(stream) ?? throw new InvalidDataException("백업 manifest 오류");
                if (manifest.Version != 5 || manifest.ApplicationId != "com.alphaexchange.offline" || manifest.Bytes <= 0 || manifest.Bytes > MaxBackupBytes ||
                    !Guid.TryParseExact(manifest.RunId,"N",out _)) throw new InvalidDataException("백업 버전/시장 ID 오류");
                var entry = zip.GetEntry("history-v5.sqlite") ?? throw new InvalidDataException("통계 파일 누락");
                if (entry.Length != manifest.Bytes) throw new InvalidDataException("백업 크기 불일치");
                using (var input = entry.Open()) using (var output = File.Create(temp))
                {
                    byte[] buffer = new byte[65536]; int read; long copied = 0;
                    while ((read = input.Read(buffer)) > 0)
                    { copied += read; if (copied > MaxBackupBytes) throw new InvalidDataException("백업 크기 제한"); output.Write(buffer,0,read); }
                    if (copied != manifest.Bytes) throw new InvalidDataException("불완전한 백업");
                    output.Flush(true);
                }
                using (var file = File.OpenRead(temp)) if (Convert.ToHexString(SHA256.HashData(file)) != manifest.Sha256) throw new InvalidDataException("백업 체크섬 오류");
                var game = LoadDatabase(temp);
                if (game.State.RunId != manifest.RunId || game.State.CompletedHours != manifest.Hour) throw new InvalidDataException("백업 시장/시간 불일치");
                using (var connection = Open(temp,true)) using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT count(*) FROM hours h LEFT JOIN runs r ON r.id=h.run WHERE r.id IS NULL OR h.hour>r.hour OR h.hour<0 OR h.resolution NOT IN (1,24)";
                    if (Convert.ToInt64(command.ExecuteScalar()) != 0) throw new InvalidDataException("백업 기록 범위 오류");
                }
                lock (writeGate)
                {
                    string? originalBackup=null;
                    if (File.Exists(SavePath))
                    {
                        using var original = Open(SavePath);
                        using var checkpoint=original.CreateCommand(); checkpoint.CommandText="PRAGMA wal_checkpoint(TRUNCATE)";
                        using(var result=checkpoint.ExecuteReader()) if(!result.Read() || result.GetInt32(0)!=0) throw new IOException("기존 기록 저장을 기다려야 합니다.");
                        originalBackup=SavePath+".before-import-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                        Backup(original,originalBackup);
                    }
                    // Prepare a matching healthy backup before atomically replacing the active DB.
                    using(var importedConnection=Open(temp,true)) Backup(importedConnection,BackupPath);
                    try
                    {
                        foreach (string suffix in new[] { "-wal","-shm" }) if (File.Exists(SavePath+suffix)) File.Delete(SavePath+suffix);
                        File.Move(temp,SavePath,true);
                    }
                    catch
                    {
                        if(originalBackup is not null) File.Copy(originalBackup,BackupPath,true);
                        throw;
                    }
                    lock (pending) { pending.Clear(); pendingEvents.Clear(); pendingVotes.Clear(); pendingBankruptcies.Clear(); pendingReports.Clear(); pendingActivities.Clear(); pendingWorldVotes.Clear(); }
                    queuedDailyThrough.Clear(); databaseReady = true;
                    activeRun = game.State.RunId; LastCommittedHour = game.State.CompletedHours;
                }
                Attach(game); return game;
            }
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
