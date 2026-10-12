using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace AlphaExchange.Core;

public sealed partial class GameStore
{
    IEnumerable<string> LegacyFiles()
    {
        foreach (string name in new[] { "history-v5.sqlite", "market-v4.json", "market-v3.json", "season-v2.json" })
            foreach (string path in Directory.EnumerateFiles(directory, name + "*"))
                yield return path;
        string seasons = Path.Combine(directory, "seasons");
        if (Directory.Exists(seasons))
            foreach (string path in Directory.EnumerateFiles(seasons, "*.json", SearchOption.AllDirectories)) yield return path;
    }

    // Preserve the previous database and its WAL verbatim. Never open it with
    // the current schema, checkpoint it, or reinterpret 30-stock portfolios.
    public void ExportLegacy(Stream destination)
    {
        lock (writeGate)
        {
            if (!HasLegacyMarket) throw new InvalidDataException("보관된 이전 시장이 없습니다.");
            using var zip = new ZipArchive(destination, ZipArchiveMode.Create, true);
            var files = new List<object>();
            long total = 0;
            foreach (string path in LegacyFiles().Order())
            {
                long bytes = new FileInfo(path).Length;
                total = checked(total + bytes);
                if (total > MaxBackupBytes) throw new InvalidDataException("보관본 크기 제한을 초과했습니다.");
                string name = Path.GetRelativePath(directory, path).Replace('\\', '/');
                using var input = File.OpenRead(path);
                string hash = Convert.ToHexString(SHA256.HashData(input));
                input.Position = 0;
                using (var output = zip.CreateEntry(name, CompressionLevel.NoCompression).Open()) input.CopyTo(output);
                files.Add(new { name, bytes, sha256 = hash });
            }
            using var manifest = zip.CreateEntry("archive-manifest.json").Open();
            JsonSerializer.Serialize(manifest, new
            {
                format = "alpha-exchange-legacy-archive-1", files,
                description = "이전 시장 원본 보관본입니다. 현재 15종목 시장에 가져올 수 없습니다."
            });
        }
    }
}
