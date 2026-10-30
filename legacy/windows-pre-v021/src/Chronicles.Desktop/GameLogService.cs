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
    private readonly HashSet<AgentId> _automaticPersonalAgents = [];
    private readonly HashSet<(AgentId AgentId, int LifeNumber, long CenturyIndex)>
        _completedPersonalCenturyVolumes = [];
    private readonly Channel<LogWriteRequest> _writeRequests = Channel.CreateUnbounded<LogWriteRequest>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly Task _writerTask;
    private long _debugSequence;
    private int _autoDebugOutput;
    private int _autoPerformanceOutput;
    private int _autoWorldOutput;
    private int _disposed;

    public GameLogService()
    {
        _writerTask = Task.Run(ProcessWritesAsync);
    }

    public event Action<Exception>? FileWriteFailed;

    public bool AutoDebugOutput => Volatile.Read(ref _autoDebugOutput) != 0;
    public bool AutoPersonalOutput
    {
        get
        {
            lock (_personalGate)
            {
                return _automaticPersonalAgents.Count > 0;
            }
        }
    }
    public bool AutoPerformanceOutput => Volatile.Read(ref _autoPerformanceOutput) != 0;
    public bool AutoWorldOutput => Volatile.Read(ref _autoWorldOutput) != 0;
    public long DebugVersion => Volatile.Read(ref _debugSequence);

    public static string OutputDirectory => LogOutputDirectories.RootDirectory;

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

    public bool IsAutoPersonalOutput(AgentId agentId)
    {
        lock (_personalGate)
        {
            return _automaticPersonalAgents.Contains(agentId);
        }
    }

    public void SetAutoPersonalOutput(AgentId agentId, bool enabled)
    {
        lock (_personalGate)
        {
            if (enabled)
            {
                _automaticPersonalAgents.Add(agentId);
            }
            else
            {
                _automaticPersonalAgents.Remove(agentId);
                _personalLogSequences.Remove(agentId);
                _personalChronicleSequences.Remove(agentId);
                _completedPersonalCenturyVolumes.RemoveWhere(key => key.AgentId == agentId);
            }
        }

        AddDebug(
            DebugLogLevel.Information,
            enabled
                ? $"인물 {agentId} 로그·연대기 자동 파일 기록 시작"
                : $"인물 {agentId} 로그·연대기 자동 파일 기록 중지");
    }

    public void ResetPersonalCaptureState()
    {
        lock (_personalGate)
        {
            _personalLogSequences.Clear();
            _personalChronicleSequences.Clear();
            _completedPersonalCenturyVolumes.Clear();
            _automaticPersonalAgents.Clear();
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
            QueueAutomaticWrite(LogOutputKind.Debug, [FormatDebugEntry(entry)]);
        }
    }

    public void CaptureWorldFeed(
        WorldEventFeed feed,
        Func<WorldEventNotification, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        if (!AutoWorldOutput)
        {
            return;
        }

        if (feed.DroppedCount > 0)
        {
            AddDebug(
                DebugLogLevel.Warning,
                $"자동 출력 대기열 포화로 세계 로그 {feed.DroppedCount:N0}건을 건너뜀");
        }

        if (feed.Events.Count == 0)
        {
            return;
        }

        QueueAutomaticWrite(
            LogOutputKind.World,
            feed.Events.Select(formatter).ToArray());
    }

    public void CaptureWorldChronicleFeed(
        WorldCenturyChronicleFeed feed,
        Func<WorldCenturyChronicleEntry, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        if (!AutoWorldOutput)
        {
            return;
        }

        if (feed.DroppedCount > 0)
        {
            AddDebug(
                DebugLogLevel.Warning,
                $"자동 출력 대기열 포화로 100년 세계 역사서 {feed.DroppedCount:N0}권을 건너뜀");
        }

        if (feed.Entries.Count == 0)
        {
            return;
        }

        QueueAutomaticWrite(
            LogOutputKind.World,
            feed.Entries.Select(formatter).ToArray());
    }

    public void CapturePerformance(string line)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line);
        if (AutoPerformanceOutput)
        {
            QueueAutomaticWrite(LogOutputKind.Performance, [line.Trim()]);
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
        if (!IsAutoPersonalOutput(agentId))
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
            QueueAutomaticWrite(LogOutputKind.Personal, lines.ToArray(), agentId);
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

        return QueueManualWrite(LogOutputKind.Debug, lines);
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
        return QueueManualWrite(LogOutputKind.World, lines.ToArray());
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
        return QueueManualWrite(LogOutputKind.Performance, lines.ToArray());
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
        return QueueManualWrite(LogOutputKind.Personal, lines.ToArray(), agentId);
    }

    public Task<string> ExportRecentPersonalAsync(
        AgentId agentId,
        string personName,
        int lifeNumber,
        string categoryName,
        IReadOnlyList<string> recentPersonalLog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personName);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        ArgumentNullException.ThrowIfNull(recentPersonalLog);
        var lines = new List<string>(recentPersonalLog.Count + 6)
        {
            $"선로만상록 PC {ProductInfo.Version} 최근 개인 로그",
            $"인물: {personName.Trim()} · {agentId} · 현재 제 {lifeNumber:N0}생",
            $"분류: {categoryName.Trim()} · 최근 {recentPersonalLog.Count:N0}/100줄",
            $"출력 시각: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}",
            $"파일 상한: {MaximumLogFileBytes / 1024 / 1024:N0} MiB",
            string.Empty,
        };
        lines.AddRange(recentPersonalLog);
        return QueueManualWrite(LogOutputKind.Personal, lines.ToArray(), agentId);
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
        LogOutputKind kind,
        string[] lines,
        AgentId? agentId = null)
    {
        if (Volatile.Read(ref _disposed) == 0)
        {
            _writeRequests.Writer.TryWrite(
                new LogWriteRequest(
                    new LogOutputTarget(kind, LogOutputMode.Automatic, agentId),
                    lines,
                    null));
        }
    }

    private Task<string> QueueManualWrite(
        LogOutputKind kind,
        string[] lines,
        AgentId? agentId = null)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var completion = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_writeRequests.Writer.TryWrite(
            new LogWriteRequest(
                new LogOutputTarget(kind, LogOutputMode.Manual, agentId),
                lines,
                completion)))
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
                var path = request.Target.Mode == LogOutputMode.Manual
                    ? WriteManualFile(request.Target, request.Lines)
                    : AppendAutomaticFile(request.Target, request.Lines);
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
        LogOutputTarget target,
        string[] lines)
    {
        var directory = LogOutputDirectories.EnsureDirectory(target);
        var result = BoundedLogFileWriter.AppendSeries(
            directory,
            $"{FormatKind(target.Kind)}-auto-{DateTime.Now:yyyyMMdd}",
            lines);
        return result.LastPath;
    }

    private static string WriteManualFile(
        LogOutputTarget target,
        string[] lines)
    {
        var directory = LogOutputDirectories.EnsureDirectory(target);
        var result = BoundedLogFileWriter.WriteNewSeries(
            directory,
            $"{FormatKind(target.Kind)}-export-{DateTime.Now:yyyyMMdd-HHmmss-fff}",
            lines);
        return result.PrimaryPath;
    }

    public static string GetOutputDirectory(
        LogOutputKind kind,
        LogOutputMode mode,
        AgentId? agentId = null) =>
        LogOutputDirectories.GetDirectory(new LogOutputTarget(kind, mode, agentId));

    public static string EnsureOutputDirectory(
        LogOutputKind kind,
        LogOutputMode mode,
        AgentId? agentId = null) =>
        LogOutputDirectories.EnsureDirectory(new LogOutputTarget(kind, mode, agentId));

    private static string FormatKind(LogOutputKind kind) => kind switch
    {
        LogOutputKind.Debug => "debug",
        LogOutputKind.Personal => "personal",
        LogOutputKind.Performance => "performance",
        LogOutputKind.World => "world",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private sealed record LogWriteRequest(
        LogOutputTarget Target,
        string[] Lines,
        TaskCompletionSource<string>? Completion);
}
