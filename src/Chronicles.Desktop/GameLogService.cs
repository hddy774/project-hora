using System.IO;
using System.Threading.Channels;
using Chronicles.Application;
using Chronicles.Core;
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
    public const int PerformanceLogCapacity = 100;
    public const long MaximumLogFileBytes = BoundedLogFileWriter.MaximumFileBytes;

    private readonly object _entryGate = new();
    private readonly object _personalGate = new();
    private readonly Queue<DebugLogEntry> _entries = new(DebugLogCapacity);
    private readonly Dictionary<AgentId, long> _personalLogSequences = [];
    private readonly Dictionary<AgentId, long> _personalChronicleSequences = [];
    private readonly HashSet<(AgentId AgentId, int LifeNumber, long CenturyIndex)>
        _completedPersonalCenturyVolumes = [];
    private readonly Channel<LogWriteRequest> _writeRequests = Channel.CreateUnbounded<LogWriteRequest>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly Task _writerTask;
    private long _debugSequence;
    private int _autoDebugOutput;
    private int _autoPersonalOutput;
    private int _autoPerformanceOutput;
    private int _autoWorldOutput;
    private int _disposed;

    public GameLogService()
    {
        _writerTask = Task.Run(ProcessWritesAsync);
    }

    public event Action<Exception>? FileWriteFailed;

    public bool AutoDebugOutput => Volatile.Read(ref _autoDebugOutput) != 0;
    public bool AutoPersonalOutput => Volatile.Read(ref _autoPersonalOutput) != 0;
    public bool AutoPerformanceOutput => Volatile.Read(ref _autoPerformanceOutput) != 0;
    public bool AutoWorldOutput => Volatile.Read(ref _autoWorldOutput) != 0;
    public long DebugVersion => Volatile.Read(ref _debugSequence);

    public static string OutputDirectory => Path.Combine(
        GameApplicationSession.DefaultDataDirectory,
        "logs");

    public static string EnsureOutputDirectory()
    {
        Directory.CreateDirectory(OutputDirectory);
        return OutputDirectory;
    }

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

    public void SetAutoPerformanceOutput(bool enabled)
    {
        Volatile.Write(ref _autoPerformanceOutput, enabled ? 1 : 0);
        AddDebug(
            DebugLogLevel.Information,
            enabled ? "성능·상태 로그 자동 파일 기록 시작" : "성능·상태 로그 자동 파일 기록 중지");
    }

    public void SetAutoPersonalOutput(bool enabled)
    {
        Volatile.Write(ref _autoPersonalOutput, enabled ? 1 : 0);
        AddDebug(
            DebugLogLevel.Information,
            enabled
                ? "선택 인물 로그·연대기 자동 파일 기록 시작"
                : "선택 인물 로그·연대기 자동 파일 기록 중지");
    }

    public void ResetPersonalCaptureState()
    {
        lock (_personalGate)
        {
            _personalLogSequences.Clear();
            _personalChronicleSequences.Clear();
            _completedPersonalCenturyVolumes.Clear();
        }
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

    public void CaptureWorldChronicleFeed(
        WorldCenturyChronicleFeed feed,
        Func<WorldCenturyChronicleEntry, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        if (feed.DroppedCount > 0)
        {
            AddDebug(
                DebugLogLevel.Warning,
                $"자동 출력 대기열 포화로 100년 세계 역사서 {feed.DroppedCount:N0}권을 건너뜀");
        }

        if (!AutoWorldOutput || feed.Entries.Count == 0)
        {
            return;
        }

        QueueAutomaticWrite(
            LogFileKind.World,
            feed.Entries.Select(formatter).ToArray());
    }

    public void CapturePerformance(string line)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line);
        if (AutoPerformanceOutput)
        {
            QueueAutomaticWrite(LogFileKind.Performance, [line.Trim()]);
        }
    }

    public void CapturePersonalRecords(
        AgentId agentId,
        IReadOnlyList<PersonalLogEntry> recentLog,
        Func<PersonalLogEntry, string> personalLogFormatter,
        IReadOnlyList<LifeChronicleEntry> detailedChronicle,
        Func<LifeChronicleEntry, string> detailedChronicleFormatter,
        IReadOnlyList<PersonalCenturyChronicleEntry> centuryChronicle,
        Func<PersonalCenturyChronicleEntry, string> centuryChronicleFormatter)
    {
        ArgumentNullException.ThrowIfNull(recentLog);
        ArgumentNullException.ThrowIfNull(personalLogFormatter);
        ArgumentNullException.ThrowIfNull(detailedChronicle);
        ArgumentNullException.ThrowIfNull(detailedChronicleFormatter);
        ArgumentNullException.ThrowIfNull(centuryChronicle);
        ArgumentNullException.ThrowIfNull(centuryChronicleFormatter);
        if (!AutoPersonalOutput)
        {
            return;
        }

        List<string> lines = [];
        lock (_personalGate)
        {
            var lastPersonalSequence = GetOrResetSequence(
                _personalLogSequences,
                agentId,
                recentLog.Count == 0 ? 0 : recentLog.Max(entry => entry.Sequence));
            foreach (var entry in recentLog.Where(entry => entry.Sequence > lastPersonalSequence))
            {
                lines.Add($"[최근 개인 로그] {personalLogFormatter(entry)}");
                lastPersonalSequence = Math.Max(lastPersonalSequence, entry.Sequence);
            }
            _personalLogSequences[agentId] = lastPersonalSequence;

            var lastChronicleSequence = GetOrResetSequence(
                _personalChronicleSequences,
                agentId,
                detailedChronicle.Count == 0 ? 0 : detailedChronicle.Max(entry => entry.Sequence));
            foreach (var entry in detailedChronicle.Where(entry => entry.Sequence > lastChronicleSequence))
            {
                lines.Add($"[세부 연대기] {detailedChronicleFormatter(entry)}");
                lastChronicleSequence = Math.Max(lastChronicleSequence, entry.Sequence);
            }
            _personalChronicleSequences[agentId] = lastChronicleSequence;

            foreach (var entry in centuryChronicle.Where(entry => entry.IsComplete))
            {
                var key = (agentId, entry.LifeNumber, entry.CenturyIndex);
                if (_completedPersonalCenturyVolumes.Add(key))
                {
                    lines.Add($"[100년 종합 연대기] {centuryChronicleFormatter(entry)}");
                }
            }
        }

        if (lines.Count > 0)
        {
            QueueAutomaticWrite(LogFileKind.Personal, lines.ToArray(), agentId.ToString());
        }
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
        lines.Add($"[영구 세계 연대기 · 100년 역사서 {permanentWorldChronicle.Count:N0}권]");
        lines.AddRange(permanentWorldChronicle);
        return QueueManualWrite(LogFileKind.World, lines.ToArray());
    }

    public Task<string> ExportPerformanceAsync(IReadOnlyList<string> recentSamples)
    {
        ArgumentNullException.ThrowIfNull(recentSamples);
        var lines = new List<string>(recentSamples.Count + 4)
        {
            $"선로만상록 PC {ProductInfo.Version} 성능·상태 로그",
            $"출력 시각: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}",
            $"파일 상한: {MaximumLogFileBytes / 1024 / 1024:N0} MiB · 최근 표본 {recentSamples.Count:N0}/{PerformanceLogCapacity:N0}",
            string.Empty,
        };
        lines.AddRange(recentSamples);
        return QueueManualWrite(LogFileKind.Performance, lines.ToArray());
    }

    public Task<string> ExportPersonalAsync(
        AgentId agentId,
        string personName,
        int lifeNumber,
        IReadOnlyList<string> recentPersonalLog,
        IReadOnlyList<string> detailedChronicle,
        IReadOnlyList<string> centuryChronicle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personName);
        ArgumentNullException.ThrowIfNull(recentPersonalLog);
        ArgumentNullException.ThrowIfNull(detailedChronicle);
        ArgumentNullException.ThrowIfNull(centuryChronicle);
        var lines = new List<string>(
            recentPersonalLog.Count + detailedChronicle.Count + centuryChronicle.Count + 11)
        {
            $"선로만상록 PC {ProductInfo.Version} 개인 기록",
            $"인물: {personName.Trim()} · {agentId} · 현재 제 {lifeNumber:N0}생",
            $"출력 시각: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}",
            $"파일 상한: {MaximumLogFileBytes / 1024 / 1024:N0} MiB",
            string.Empty,
            $"[최근 개인 로그 · {recentPersonalLog.Count:N0}건]",
        };
        lines.AddRange(recentPersonalLog);
        lines.Add(string.Empty);
        lines.Add($"[세부 영구 연대기 · {detailedChronicle.Count:N0}건]");
        lines.AddRange(detailedChronicle);
        lines.Add(string.Empty);
        lines.Add($"[생별 100년 종합 연대기 · {centuryChronicle.Count:N0}권]");
        lines.AddRange(centuryChronicle);
        return QueueManualWrite(LogFileKind.Personal, lines.ToArray(), agentId.ToString());
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

    private static long GetOrResetSequence(
        Dictionary<AgentId, long> sequences,
        AgentId agentId,
        long currentMaximum)
    {
        if (!sequences.TryGetValue(agentId, out var previous)
            || currentMaximum < previous)
        {
            return 0;
        }

        return previous;
    }

    private void QueueAutomaticWrite(
        LogFileKind kind,
        string[] lines,
        string? scope = null)
    {
        if (Volatile.Read(ref _disposed) == 0)
        {
            _writeRequests.Writer.TryWrite(
                new LogWriteRequest(kind, lines, IsManual: false, scope, null));
        }
    }

    private Task<string> QueueManualWrite(
        LogFileKind kind,
        string[] lines,
        string? scope = null)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var completion = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_writeRequests.Writer.TryWrite(
            new LogWriteRequest(kind, lines, IsManual: true, scope, completion)))
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
                EnsureOutputDirectory();
                var path = request.IsManual
                    ? WriteManualFile(request.Kind, request.Scope, request.Lines)
                    : AppendAutomaticFile(request.Kind, request.Scope, request.Lines);
                request.Completion?.SetResult(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                request.Completion?.SetException(exception);
                FileWriteFailed?.Invoke(exception);
            }
        }
    }

    private static string AppendAutomaticFile(
        LogFileKind kind,
        string? scope,
        string[] lines)
    {
        var result = BoundedLogFileWriter.AppendSeries(
            OutputDirectory,
            $"{FormatKind(kind)}{FormatScope(scope)}-auto-{DateTime.Now:yyyyMMdd}",
            lines);
        return result.LastPath;
    }

    private static string WriteManualFile(
        LogFileKind kind,
        string? scope,
        string[] lines)
    {
        var result = BoundedLogFileWriter.WriteNewSeries(
            OutputDirectory,
            $"{FormatKind(kind)}{FormatScope(scope)}-export-{DateTime.Now:yyyyMMdd-HHmmss-fff}",
            lines);
        return result.PrimaryPath;
    }

    private static string FormatScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return string.Empty;
        }

        var safe = new string(scope
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .ToArray());
        return safe.Length == 0 ? string.Empty : $"-{safe}";
    }

    private static string FormatKind(LogFileKind kind) => kind switch
    {
        LogFileKind.Debug => "debug",
        LogFileKind.Personal => "personal",
        LogFileKind.Performance => "performance",
        LogFileKind.World => "world",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private enum LogFileKind : byte
    {
        Debug,
        Personal,
        Performance,
        World,
    }

    private sealed record LogWriteRequest(
        LogFileKind Kind,
        string[] Lines,
        bool IsManual,
        string? Scope,
        TaskCompletionSource<string>? Completion);
}
