using System.IO;
using System.Text;
using System.Threading.Channels;
using Chronicles.Core;
using Chronicles.Persistence;
using Chronicles.Simulation;

namespace Chronicles.Desktop;

internal enum DebugLogLevel : byte
{
    Information,
    Warning,
    Error,
}

internal readonly record struct DebugLogEntry(
    long Sequence,
    DateTimeOffset Timestamp,
    DebugLogLevel Level,
    string Message);

internal sealed class GameLogService : IDisposable
{
    public const int DebugLogCapacity = 500;

    private readonly object _entryGate = new();
    private readonly Queue<DebugLogEntry> _entries = new(DebugLogCapacity);
    private readonly Channel<LogWriteRequest> _writeRequests = Channel.CreateUnbounded<LogWriteRequest>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly Task _writerTask;
    private long _debugSequence;
    private int _autoDebugOutput;
    private int _autoWorldOutput;
    private int _disposed;

    public GameLogService()
    {
        _writerTask = Task.Run(ProcessWritesAsync);
    }

    public event Action<Exception>? FileWriteFailed;

    public bool AutoDebugOutput => Volatile.Read(ref _autoDebugOutput) != 0;
    public bool AutoWorldOutput => Volatile.Read(ref _autoWorldOutput) != 0;
    public long DebugVersion => Volatile.Read(ref _debugSequence);

    public static string OutputDirectory => Path.Combine(
        Path.GetDirectoryName(WorldSaveStore.DefaultPath)
            ?? throw new InvalidOperationException("게임 데이터 폴더를 확인할 수 없습니다."),
        "logs");

    public IReadOnlyList<DebugLogEntry> GetDebugEntries()
    {
        lock (_entryGate)
        {
            return _entries.ToArray();
        }
    }

    public void SetAutoDebugOutput(bool enabled)
    {
        Volatile.Write(ref _autoDebugOutput, enabled ? 1 : 0);
        AddDebug(
            DebugLogLevel.Information,
            enabled ? "디버그 로그 자동 파일 기록 시작" : "디버그 로그 자동 파일 기록 중지");
    }

    public void SetAutoWorldOutput(bool enabled)
    {
        Volatile.Write(ref _autoWorldOutput, enabled ? 1 : 0);
        AddDebug(
            DebugLogLevel.Information,
            enabled ? "세계 로그 자동 파일 기록 시작" : "세계 로그 자동 파일 기록 중지");
    }

    public void AddDebug(DebugLogLevel level, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var entry = new DebugLogEntry(
            Interlocked.Increment(ref _debugSequence),
            DateTimeOffset.Now,
            level,
            message.Trim());
        lock (_entryGate)
        {
            if (_entries.Count == DebugLogCapacity)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue(entry);
        }

        if (AutoDebugOutput)
        {
            QueueAutomaticWrite(LogFileKind.Debug, [FormatDebugEntry(entry)]);
        }
    }

    public void CaptureWorldFeed(
        WorldEventFeed feed,
        Func<WorldEventNotification, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        if (feed.DroppedCount > 0)
        {
            AddDebug(
                DebugLogLevel.Warning,
                $"자동 출력 대기열 포화로 세계 로그 {feed.DroppedCount:N0}건을 건너뜀");
        }

        if (!AutoWorldOutput || feed.Events.Count == 0)
        {
            return;
        }

        QueueAutomaticWrite(
            LogFileKind.World,
            feed.Events.Select(formatter).ToArray());
    }

    public Task<string> ExportDebugAsync()
    {
        var entries = GetDebugEntries();
        var lines = new string[entries.Count + 2];
        lines[0] = $"선로만상록 PC {ProductInfo.Version} 디버그 로그";
        lines[1] = $"출력 시각: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}";
        for (var index = 0; index < entries.Count; index++)
        {
            lines[index + 2] = FormatDebugEntry(entries[index]);
        }

        return QueueManualWrite(LogFileKind.Debug, lines);
    }

    public Task<string> ExportWorldAsync(
        IReadOnlyList<string> recentWorldLog,
        IReadOnlyList<string> permanentWorldChronicle)
    {
        ArgumentNullException.ThrowIfNull(recentWorldLog);
        ArgumentNullException.ThrowIfNull(permanentWorldChronicle);
        var lines = new List<string>(recentWorldLog.Count + permanentWorldChronicle.Count + 7)
        {
            $"선로만상록 PC {ProductInfo.Version} 세계 기록",
            $"출력 시각: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}",
            string.Empty,
            $"[최근 세계 로그 · {recentWorldLog.Count:N0}건]",
        };
        lines.AddRange(recentWorldLog);
        lines.Add(string.Empty);
        lines.Add($"[영구 세계 연대기 · {permanentWorldChronicle.Count:N0}건]");
        lines.AddRange(permanentWorldChronicle);
        return QueueManualWrite(LogFileKind.World, lines.ToArray());
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _writeRequests.Writer.TryComplete();
        try
        {
            _writerTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException exception)
        {
            FileWriteFailed?.Invoke(exception.GetBaseException());
        }
    }

    private static string FormatDebugEntry(DebugLogEntry entry) =>
        $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{FormatLevel(entry.Level)}] #{entry.Sequence:N0} {entry.Message}";

    private static string FormatLevel(DebugLogLevel level) => level switch
    {
        DebugLogLevel.Information => "INFO",
        DebugLogLevel.Warning => "WARN",
        DebugLogLevel.Error => "ERROR",
        _ => level.ToString().ToUpperInvariant(),
    };

    private void QueueAutomaticWrite(LogFileKind kind, string[] lines)
    {
        if (Volatile.Read(ref _disposed) == 0)
        {
            _writeRequests.Writer.TryWrite(new LogWriteRequest(kind, lines, IsManual: false, null));
        }
    }

    private Task<string> QueueManualWrite(LogFileKind kind, string[] lines)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var completion = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_writeRequests.Writer.TryWrite(new LogWriteRequest(kind, lines, IsManual: true, completion)))
        {
            completion.SetException(new IOException("로그 출력 대기열을 사용할 수 없습니다."));
        }

        return completion.Task;
    }

    private async Task ProcessWritesAsync()
    {
        await foreach (var request in _writeRequests.Reader.ReadAllAsync())
        {
            try
            {
                Directory.CreateDirectory(OutputDirectory);
                var path = request.IsManual
                    ? WriteManualFile(request.Kind, request.Lines)
                    : AppendAutomaticFile(request.Kind, request.Lines);
                request.Completion?.SetResult(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                request.Completion?.SetException(exception);
                FileWriteFailed?.Invoke(exception);
            }
        }
    }

    private static string AppendAutomaticFile(LogFileKind kind, string[] lines)
    {
        var path = Path.Combine(
            OutputDirectory,
            $"{FormatKind(kind)}-auto-{DateTime.Now:yyyyMMdd}.log");
        File.AppendAllLines(path, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    private static string WriteManualFile(LogFileKind kind, string[] lines)
    {
        var path = Path.Combine(
            OutputDirectory,
            $"{FormatKind(kind)}-export-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log");
        var temporaryPath = path + ".tmp";
        try
        {
            File.WriteAllLines(
                temporaryPath,
                lines,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            File.Move(temporaryPath, path, overwrite: true);
            return path;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string FormatKind(LogFileKind kind) => kind switch
    {
        LogFileKind.Debug => "debug",
        LogFileKind.World => "world",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private enum LogFileKind : byte
    {
        Debug,
        World,
    }

    private sealed record LogWriteRequest(
        LogFileKind Kind,
        string[] Lines,
        bool IsManual,
        TaskCompletionSource<string>? Completion);
}
