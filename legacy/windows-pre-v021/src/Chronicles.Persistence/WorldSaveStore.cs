using Chronicles.Simulation;

namespace Chronicles.Persistence;

public static class WorldSaveStore
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ChroniclesOfMyriadImmortalPaths",
        "world.cmip");

    public static void Save(string path, WorldCheckpoint checkpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(checkpoint);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("저장 폴더를 확인할 수 없습니다.");
        Directory.CreateDirectory(directory);
        var temporaryPath = fullPath + ".tmp";
        var data = WorldSaveCodec.Encode(checkpoint);

        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81_920,
                FileOptions.WriteThrough))
            {
                stream.Write(data);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static WorldCheckpoint Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return WorldSaveCodec.Decode(File.ReadAllBytes(Path.GetFullPath(path)));
    }
}
