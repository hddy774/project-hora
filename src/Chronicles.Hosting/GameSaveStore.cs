using System.Security.Cryptography;

namespace Chronicles.Game;

/// <summary>Filesystem slot adapter. The world and byte format remain owned by Chronicles.Game.</summary>
public static class GameSaveStore
{
    public static void SaveAtomic(GameWorld world, string path) => SaveAtomic(world, path, GameCodec.JsonPayloadLimit);

    // Force the large-envelope branch on small functional fixtures without a process-wide setting.
    internal static void SaveAtomic(GameWorld world, string path, int jsonPayloadLimit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temporary = full + ".pending";
        var sequence = 0L;
        foreach (var candidate in new[] { full, temporary, full + ".backup" })
        {
            if (!File.Exists(candidate)) continue;
            try { sequence = Math.Max(sequence, GameCodec.DecodeEnvelope(File.ReadAllBytes(candidate)).Sequence); }
            catch (InvalidDataException) { PreserveOriginal(candidate); }
        }
        var bytes = GameCodec.Encode(world, checked(sequence + 1), jsonPayloadLimit);
        using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough))
        { output.Write(bytes); output.Flush(true); }
        // Validate the exact bytes before replacing a valid slot. Backup stays readable after interruption.
        GameCodec.Decode(File.ReadAllBytes(temporary));
        if (File.Exists(full))
        {
            try { GameCodec.Decode(File.ReadAllBytes(full)); File.Copy(full, full + ".backup", true); }
            catch (InvalidDataException) { PreserveOriginal(full); }
        }
        File.Move(temporary, full, true);
    }

    private static void PreserveOriginal(string path)
    {
        using var source = File.OpenRead(path);
        var hash = Convert.ToHexStringLower(SHA256.HashData(source));
        var directory = Path.Combine(Path.GetDirectoryName(path)!, "preserved");
        Directory.CreateDirectory(directory);
        var retained = Path.Combine(directory, Path.GetFileName(path) + "." + hash + ".original");
        if (!File.Exists(retained)) File.Copy(path, retained, false);
    }

    public static GameWorld Load(string path) => LoadWithSource(path).World;

    internal static (GameWorld World, string Source) LoadWithSource(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        GameWorld? selected = null; var sequence = -1L; var source = "";
        foreach (var candidate in new[] { (Path: path, Source: "primary"), (Path: path + ".pending", Source: "pending"),
                     (Path: path + ".backup", Source: "backup") })
        {
            try
            {
                var value = GameCodec.DecodeEnvelope(File.ReadAllBytes(candidate.Path));
                if (value.Sequence > sequence) { selected = value.World; sequence = value.Sequence; source = candidate.Source; }
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { }
        }
        return selected is null ? throw new InvalidDataException("복구할 수 있는 0.2.1 저장이 없습니다. 기존 파일은 보존됩니다.") : (selected, source);
    }
}
