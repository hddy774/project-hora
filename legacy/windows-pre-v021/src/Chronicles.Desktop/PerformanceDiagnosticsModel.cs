using System.Diagnostics;
using System.Globalization;
using Chronicles.Runtime;

namespace Chronicles.Desktop;

internal enum PerformanceHealthLevel : byte
{
    Measuring,
    Healthy,
    Warning,
    Overloaded,
}

internal sealed record PerformanceDiagnosticSample(
    long Sequence,
    DateTimeOffset RecordedAt,
    RuntimePerformanceSnapshot Runtime,
    PerformanceHealthLevel Health,
    double ProcessCpuPercent,
    double WorkingSetMegabytes,
    double ManagedHeapMegabytes,
    int ProcessThreadCount)
{
    public string RecordedAtText => RecordedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    public string HealthText => Health switch
    {
        PerformanceHealthLevel.Measuring => "측정 중",
        PerformanceHealthLevel.Healthy => "정상",
        PerformanceHealthLevel.Warning => "주의",
        PerformanceHealthLevel.Overloaded => "과부하",
        _ => Health.ToString(),
    };
    public string SimulationText => $"{Runtime.LastSimulationMilliseconds:0.00} ms";
    public string ObservationText => $"{Runtime.LastObservationMilliseconds:0.00} ms";
    public string BacklogText => $"{Runtime.EstimatedBacklogMilliseconds:0.00} ms";
    public string CpuText => $"{ProcessCpuPercent:0.0}%";
    public string WorkingSetText => $"{WorkingSetMegabytes:0.0} MiB";
    public string RuntimeStateText => Runtime.IsPaused
        ? $"일시정지 · {Runtime.Speed}×"
        : $"진행 · {Runtime.Speed}×";

    public string ToLogLine() =>
        $"{RecordedAt.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture)} " +
        $"[PERFORMANCE] #{Sequence:N0} 상태={HealthText} 실행={RuntimeStateText} " +
        $"pulse={Runtime.LastSimulationMilliseconds:0.000}ms " +
        $"pulseAvg={Runtime.AverageSimulationMilliseconds:0.000}ms " +
        $"pulseMax={Runtime.MaximumSimulationMilliseconds:0.000}ms " +
        $"query={Runtime.LastObservationMilliseconds:0.000}ms " +
        $"queryAvg={Runtime.AverageObservationMilliseconds:0.000}ms " +
        $"queryMax={Runtime.MaximumObservationMilliseconds:0.000}ms " +
        $"backlog={Runtime.EstimatedBacklogMilliseconds:0.000}ms " +
        $"cpu={ProcessCpuPercent:0.0}% workingSet={WorkingSetMegabytes:0.0}MiB " +
        $"managedHeap={ManagedHeapMegabytes:0.0}MiB threads={ProcessThreadCount:N0} " +
        $"pulseSamples={Runtime.SimulationPulseCount:N0} querySamples={Runtime.ObservationCount:N0}";
}

internal sealed class PerformanceDiagnosticsHistory
{
    public const double ObservationBudgetMilliseconds = 16d;
    private static readonly long SampleIntervalTicks = Stopwatch.Frequency;

    private readonly Queue<PerformanceDiagnosticSample> _samples =
        new(GameLogService.PerformanceLogCapacity);
    private long _sequence;
    private long _nextSampleTimestamp;
    private long _previousCpuTimestamp;
    private TimeSpan _previousTotalProcessorTime;

    public int Count => _samples.Count;

    public void Clear()
    {
        _samples.Clear();
        _sequence = 0;
        _nextSampleTimestamp = 0;
        _previousCpuTimestamp = 0;
        _previousTotalProcessorTime = TimeSpan.Zero;
    }

    public bool TryCapture(
        RuntimePerformanceSnapshot snapshot,
        out PerformanceDiagnosticSample? sample)
    {
        sample = null;
        if (!snapshot.IsEnabled)
        {
            _nextSampleTimestamp = 0;
            return false;
        }

        var timestamp = Stopwatch.GetTimestamp();
        if (_nextSampleTimestamp != 0 && timestamp < _nextSampleTimestamp)
        {
            return false;
        }
        _nextSampleTimestamp = timestamp + SampleIntervalTicks;

        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var processorTime = process.TotalProcessorTime;
        var cpuPercent = 0d;
        if (_previousCpuTimestamp != 0 && timestamp > _previousCpuTimestamp)
        {
            var wallSeconds = (timestamp - _previousCpuTimestamp) / (double)Stopwatch.Frequency;
            var cpuSeconds = (processorTime - _previousTotalProcessorTime).TotalSeconds;
            cpuPercent = Math.Clamp(
                cpuSeconds / wallSeconds / Environment.ProcessorCount * 100d,
                0d,
                999d);
        }
        _previousCpuTimestamp = timestamp;
        _previousTotalProcessorTime = processorTime;

        sample = new PerformanceDiagnosticSample(
            ++_sequence,
            DateTimeOffset.Now,
            snapshot,
            Evaluate(snapshot),
            cpuPercent,
            process.WorkingSet64 / 1024d / 1024d,
            GC.GetTotalMemory(forceFullCollection: false) / 1024d / 1024d,
            process.Threads.Count);
        if (_samples.Count == GameLogService.PerformanceLogCapacity)
        {
            _samples.Dequeue();
        }
        _samples.Enqueue(sample);
        return true;
    }

    public PerformanceDiagnosticSample[] GetChronological() => _samples.ToArray();

    public PerformanceDiagnosticSample[] GetNewestFirst() =>
        _samples.Reverse().ToArray();

    public string[] GetLogLines() => _samples
        .Select(item => item.ToLogLine())
        .ToArray();

    public static PerformanceHealthLevel Evaluate(RuntimePerformanceSnapshot snapshot)
    {
        if (snapshot.SimulationPulseCount == 0 || snapshot.ObservationCount == 0)
        {
            return PerformanceHealthLevel.Measuring;
        }
        if (snapshot.EstimatedBacklogMilliseconds > 0.01
            || snapshot.LastSimulationMilliseconds > snapshot.PulseBudgetMilliseconds
            || snapshot.LastObservationMilliseconds > 50d)
        {
            return PerformanceHealthLevel.Overloaded;
        }
        if (snapshot.LastSimulationMilliseconds > snapshot.PulseBudgetMilliseconds * 0.75d
            || snapshot.LastObservationMilliseconds > ObservationBudgetMilliseconds)
        {
            return PerformanceHealthLevel.Warning;
        }
        return PerformanceHealthLevel.Healthy;
    }

    public static double Percentile95(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(value => value).ToArray();
        if (sorted.Length == 0)
        {
            return 0d;
        }

        var index = Math.Clamp(
            (int)Math.Ceiling(sorted.Length * 0.95d) - 1,
            0,
            sorted.Length - 1);
        return sorted[index];
    }
}
