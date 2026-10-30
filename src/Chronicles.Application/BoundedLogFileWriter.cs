using System.Text;

namespace Chronicles.Application;

public readonly record struct BoundedLogWriteResult(
    string PrimaryPath,
    string LastPath,
    int FileCount,
    long BytesWritten);

/// <summary>
/// Writes UTF-8 log series without allowing an individual file to exceed the
/// product-wide 5 MiB log limit. The writer is reusable by every local log kind.
/// </summary>
public static class BoundedLogFileWriter
{
    public const long MaximumFileBytes = 5L * 1024L * 1024L;

    private static readonly UTF8Encoding Utf8NoBom =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly byte[] NewLineBytes = Utf8NoBom.GetBytes(Environment.NewLine);

    public static BoundedLogWriteResult AppendSeries(
        string outputDirectory,
        string fileStem,
        IReadOnlyList<string> lines)
    {
        ValidateArguments(outputDirectory, fileStem, lines);
        Directory.CreateDirectory(outputDirectory);

        var part = 1;
        var path = GetPartPath(outputDirectory, fileStem, part);

        string? primaryPath = null;
        string? lastPath = null;
        var touchedFiles = 0;
        long writtenBytes = 0;
        FileStream? stream = null;
        try
        {
            foreach (var segment in EncodeBoundedSegments(lines))
            {
                if (stream is null)
                {
                    path = FindAppendPath(outputDirectory, fileStem, ref part, segment.Length);
                    stream = OpenAppend(path);
                }

                if (stream.Length + segment.Length > MaximumFileBytes)
                {
                    stream.Dispose();
                    stream = null;
                    part++;
                    path = FindAppendPath(outputDirectory, fileStem, ref part, segment.Length);
                    stream = OpenAppend(path);
                }

                if (!StringComparer.OrdinalIgnoreCase.Equals(lastPath, path))
                {
                    primaryPath ??= path;
                    lastPath = path;
                    touchedFiles++;
                }

                stream.Write(segment);
                writtenBytes += segment.Length;
            }
        }
        finally
        {
            stream?.Dispose();
        }

        return new BoundedLogWriteResult(
            primaryPath!,
            lastPath!,
            touchedFiles,
            writtenBytes);
    }

    public static BoundedLogWriteResult WriteNewSeries(
        string outputDirectory,
        string fileStem,
        IReadOnlyList<string> lines)
    {
        ValidateArguments(outputDirectory, fileStem, lines);
        Directory.CreateDirectory(outputDirectory);

        var uniqueStem = GetUniqueStem(outputDirectory, fileStem);
        var completedPaths = new List<string>();
        string? temporaryPath = null;
        FileStream? stream = null;
        var part = 1;
        long writtenBytes = 0;
        try
        {
            var finalPath = GetPartPath(outputDirectory, uniqueStem, part);
            temporaryPath = finalPath + ".tmp";
            stream = OpenTemporary(temporaryPath);
            foreach (var segment in EncodeBoundedSegments(lines))
            {
                if (stream.Length + segment.Length > MaximumFileBytes)
                {
                    stream.Dispose();
                    stream = null;
                    File.Move(temporaryPath, finalPath);
                    completedPaths.Add(finalPath);

                    finalPath = GetPartPath(outputDirectory, uniqueStem, ++part);
                    temporaryPath = finalPath + ".tmp";
                    stream = OpenTemporary(temporaryPath);
                }

                stream.Write(segment);
                writtenBytes += segment.Length;
            }

            stream.Dispose();
            stream = null;
            File.Move(temporaryPath, finalPath);
            completedPaths.Add(finalPath);
            temporaryPath = null;
        }
        catch
        {
            stream?.Dispose();
            if (temporaryPath is not null && File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
            foreach (var completedPath in completedPaths)
            {
                if (File.Exists(completedPath))
                {
                    File.Delete(completedPath);
                }
            }
            throw;
        }

        return new BoundedLogWriteResult(
            completedPaths[0],
            completedPaths[^1],
            completedPaths.Count,
            writtenBytes);
    }

    private static FileStream OpenAppend(string path) => new(
        path,
        FileMode.Append,
        FileAccess.Write,
        FileShare.Read,
        bufferSize: 16 * 1024,
        FileOptions.SequentialScan);

    private static FileStream OpenTemporary(string path) => new(
        path,
        FileMode.CreateNew,
        FileAccess.Write,
        FileShare.Read,
        bufferSize: 16 * 1024,
        FileOptions.SequentialScan);

    private static IEnumerable<byte[]> EncodeBoundedSegments(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var value = line ?? string.Empty;
            if (Utf8NoBom.GetByteCount(value) + NewLineBytes.Length <= MaximumFileBytes)
            {
                yield return Utf8NoBom.GetBytes(value + Environment.NewLine);
                continue;
            }

            var builder = new StringBuilder();
            var byteCount = 0;
            var part = 1;
            foreach (var rune in value.EnumerateRunes())
            {
                var prefix = $"[분할 기록 {part:N0}] ";
                var prefixBytes = Utf8NoBom.GetByteCount(prefix);
                if (builder.Length > 0
                    && prefixBytes + byteCount + rune.Utf8SequenceLength + NewLineBytes.Length
                        > MaximumFileBytes)
                {
                    yield return Utf8NoBom.GetBytes(prefix + builder + Environment.NewLine);
                    builder.Clear();
                    byteCount = 0;
                    part++;
                }

                builder.Append(rune.ToString());
                byteCount += rune.Utf8SequenceLength;
            }

            yield return Utf8NoBom.GetBytes(
                $"[분할 기록 {part:N0}] {builder}{Environment.NewLine}");
        }
    }

    private static string FindAppendPath(
        string outputDirectory,
        string fileStem,
        ref int part,
        int requiredBytes)
    {
        while (part < int.MaxValue)
        {
            var candidate = GetPartPath(outputDirectory, fileStem, part);
            if (!File.Exists(candidate)
                || new FileInfo(candidate).Length + requiredBytes <= MaximumFileBytes)
            {
                return candidate;
            }

            part++;
        }

        throw new IOException("새 로그 파일 번호를 만들 수 없습니다.");
    }

    private static string GetUniqueStem(string directory, string requestedStem)
    {
        if (!File.Exists(GetPartPath(directory, requestedStem, 1)))
        {
            return requestedStem;
        }

        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            var candidate = $"{requestedStem}-{suffix:000}";
            if (!File.Exists(GetPartPath(directory, candidate, 1)))
            {
                return candidate;
            }
        }

        throw new IOException("새 로그 파일 이름을 만들 수 없습니다.");
    }

    private static string GetPartPath(string directory, string stem, int part) =>
        Path.Combine(directory, $"{stem}-{part:000}.log");

    private static void ValidateArguments(
        string outputDirectory,
        string fileStem,
        IReadOnlyList<string> lines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileStem);
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            throw new ArgumentException("기록할 로그 행이 없습니다.", nameof(lines));
        }
        if (!StringComparer.Ordinal.Equals(fileStem, Path.GetFileName(fileStem))
            || fileStem.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("로그 파일 이름이 올바르지 않습니다.", nameof(fileStem));
        }
    }
}
