using System.Collections.Concurrent;
using System.Diagnostics;
using Chronicles.Core;
using Chronicles.Queries;
using Chronicles.Simulation;

namespace Chronicles.Runtime;

public sealed class SimulationRunner : IDisposable
{
    private const int MaximumPendingWorldEvents = 100_000;
    private const int MaximumPendingWorldChronicles = 1_024;
    private readonly object _worldGate = new();
    private World _world;
    private WorldObservationQueries _queries;
    private readonly ManualResetEventSlim _stop = new(initialState: false);
    private readonly Thread _worker;
    private int _speed = 1;
    private int _paused;
    private int _started;
    private int _disposed;
    private readonly ConcurrentQueue<WorldEventNotification> _pendingWorldEvents = new();
    private int _pendingWorldEventCount;
    private int _droppedWorldEventCount;
    private readonly ConcurrentQueue<WorldCenturyChronicleEntry> _pendingWorldChronicles = new();
    private int _pendingWorldChronicleCount;
    private int _droppedWorldChronicleCount;
    private int _performanceDiagnosticsEnabled;
    private long _simulationPulseCount;
    private long _simulationTotalMicroseconds;
    private long _simulationLastMicroseconds;
    private long _simulationMaximumMicroseconds;
    private long _observationCount;
    private long _observationTotalMicroseconds;
    private long _observationLastMicroseconds;
    private long _observationMaximumMicroseconds;

    public SimulationRunner(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _queries = new WorldObservationQueries(_world);
        _world.ImportantWorldEventRecorded += World_ImportantWorldEventRecorded;
        _world.WorldCenturyChronicleRecorded += World_WorldCenturyChronicleRecorded;
        _worker = new Thread(Run)
        {
            IsBackground = true,
            Name = "Chronicles Simulation",
        };
    }

    public event Action? ObservationInvalidated;
    public event Action<Exception>? Faulted;

    public int Speed => Volatile.Read(ref _speed);
    public bool IsPaused => Volatile.Read(ref _paused) != 0;

    public void SetPerformanceDiagnosticsEnabled(bool enabled) =>
        Volatile.Write(ref _performanceDiagnosticsEnabled, enabled ? 1 : 0);

    public RuntimePerformanceSnapshot GetPerformanceSnapshot()
    {
        var simulationCount = Interlocked.Read(ref _simulationPulseCount);
        var observationCount = Interlocked.Read(ref _observationCount);
        var lastSimulation = Interlocked.Read(ref _simulationLastMicroseconds);
        return new RuntimePerformanceSnapshot(
            IsEnabled: Volatile.Read(ref _performanceDiagnosticsEnabled) != 0,
            Speed,
            IsPaused,
            SimulationPacing.PulseMilliseconds,
            simulationCount,
            lastSimulation / 1_000d,
            simulationCount == 0
                ? 0d
                : Interlocked.Read(ref _simulationTotalMicroseconds) / 1_000d / simulationCount,
            Interlocked.Read(ref _simulationMaximumMicroseconds) / 1_000d,
            Math.Max(0d, (lastSimulation / 1_000d) - SimulationPacing.PulseMilliseconds),
            observationCount,
            Interlocked.Read(ref _observationLastMicroseconds) / 1_000d,
            observationCount == 0
                ? 0d
                : Interlocked.Read(ref _observationTotalMicroseconds) / 1_000d / observationCount,
            Interlocked.Read(ref _observationMaximumMicroseconds) / 1_000d);
    }

    public WorldRuntimeMetadata GetWorldMetadata()
    {
        lock (_worldGate)
        {
            return new WorldRuntimeMetadata(
                _world.Seed,
                _world.Population,
                _world.Balance);
        }
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.Exchange(ref _started, 1) == 0)
        {
            _worker.Start();
        }
    }

    public void SetPaused(bool paused) => Volatile.Write(ref _paused, paused ? 1 : 0);

    public void SetSpeed(int speed)
    {
        if (!RuntimeSpeedRules.IsSupported(speed))
        {
            throw new ArgumentOutOfRangeException(nameof(speed), "배속은 1, 10, 25, 50, 100 중 하나여야 합니다.");
        }

        Volatile.Write(ref _speed, speed);
    }

    public WorldSnapshot GetSnapshot(
        int rankingCount = 1_000,
        int eventCount = 150,
        ObservationSections sections = ObservationSections.All,
        ushort? detailedMapLocationId = null,
        int detailedMapAgentOffset = 0,
        int worldChronicleCount = int.MaxValue,
        int contentSecretRealmOffset = 0,
        int contentSecretRealmLimit = 100)
    {
        var started = BeginPerformanceMeasurement();
        lock (_worldGate)
        {
            var snapshot = _queries.GetSnapshot(
                rankingCount,
                eventCount,
                sections,
                detailedMapLocationId,
                detailedMapAgentOffset,
                worldChronicleCount,
                contentSecretRealmOffset,
                contentSecretRealmLimit);
            RecordObservationMeasurement(started);
            return snapshot;
        }
    }

    public AgentSnapshot GetAgent(AgentId agentId)
    {
        lock (_worldGate)
        {
            return _queries.GetAgent(agentId);
        }
    }

    public WorldObservation GetObservation(
        AgentId? selectedAgent = null,
        int rankingCount = 1_000,
        int eventCount = 150,
        ObservationSections sections = ObservationSections.All,
        ushort? detailedMapLocationId = null,
        int detailedMapAgentOffset = 0,
        int worldChronicleCount = int.MaxValue,
        int contentSecretRealmOffset = 0,
        int contentSecretRealmLimit = 100)
    {
        var started = BeginPerformanceMeasurement();
        lock (_worldGate)
        {
            var observation = _queries.GetObservation(new ObservationRequest(
                selectedAgent,
                rankingCount,
                eventCount,
                sections,
                detailedMapLocationId,
                detailedMapAgentOffset,
                worldChronicleCount,
                contentSecretRealmOffset,
                contentSecretRealmLimit));
            RecordObservationMeasurement(started);
            return observation;
        }
    }

    public IReadOnlyList<AgentSnapshot> BrowseAgents(int offset, int limit = 500)
    {
        lock (_worldGate)
        {
            return _queries.BrowseAgents(offset, limit);
        }
    }

    public IReadOnlyList<AgentSnapshot> SearchAgents(string? query, int limit = 1_000)
    {
        lock (_worldGate)
        {
            return _queries.SearchAgents(query, limit);
        }
    }

    public WorldEventFeed DrainWorldEventFeed()
    {
        var events = new List<WorldEventNotification>(
            Math.Min(Volatile.Read(ref _pendingWorldEventCount), MaximumPendingWorldEvents));
        while (_pendingWorldEvents.TryDequeue(out var notification))
        {
            Interlocked.Decrement(ref _pendingWorldEventCount);
            events.Add(notification);
        }

        return new WorldEventFeed(
            events,
            Interlocked.Exchange(ref _droppedWorldEventCount, 0));
    }

    public WorldCenturyChronicleFeed DrainWorldCenturyChronicleFeed()
    {
        var entries = new List<WorldCenturyChronicleEntry>(
            Math.Min(
                Volatile.Read(ref _pendingWorldChronicleCount),
                MaximumPendingWorldChronicles));
        while (_pendingWorldChronicles.TryDequeue(out var entry))
        {
            Interlocked.Decrement(ref _pendingWorldChronicleCount);
            entries.Add(entry);
        }

        return new WorldCenturyChronicleFeed(
            entries,
            Interlocked.Exchange(ref _droppedWorldChronicleCount, 0));
    }

    public WorldCheckpoint CreateCheckpoint()
    {
        lock (_worldGate)
        {
            return _world.CreateCheckpoint();
        }
    }

    public WorldAuthorityCheckpoint CreateAuthorityCheckpoint()
    {
        lock (_worldGate)
        {
            return _world.CreateAuthorityCheckpoint();
        }
    }

    public RuntimeSaveCapture CreateSaveCapture()
    {
        lock (_worldGate)
        {
            return new RuntimeSaveCapture(
                _world.CreateAuthorityCheckpoint(),
                _world.CreateSnapshot(
                rankingCount: 1,
                eventCount: 0,
                sections: ObservationSections.StatisticsSummary
                    | ObservationSections.CombatPowerRanking
                    | ObservationSections.Organization,
                    worldChronicleCount: 0));
        }
    }

    public void RestoreCheckpoint(WorldCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var restored = World.Restore(checkpoint);
        lock (_worldGate)
        {
            _world.ImportantWorldEventRecorded -= World_ImportantWorldEventRecorded;
            _world.WorldCenturyChronicleRecorded -= World_WorldCenturyChronicleRecorded;
            _world = restored;
            _queries = new WorldObservationQueries(_world);
            _world.ImportantWorldEventRecorded += World_ImportantWorldEventRecorded;
            _world.WorldCenturyChronicleRecorded += World_WorldCenturyChronicleRecorded;
            while (_pendingWorldEvents.TryDequeue(out _))
            {
            }
            Interlocked.Exchange(ref _pendingWorldEventCount, 0);
            Interlocked.Exchange(ref _droppedWorldEventCount, 0);
            while (_pendingWorldChronicles.TryDequeue(out _))
            {
            }
            Interlocked.Exchange(ref _pendingWorldChronicleCount, 0);
            Interlocked.Exchange(ref _droppedWorldChronicleCount, 0);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _stop.Set();
        if (_worker.IsAlive)
        {
            _worker.Join(TimeSpan.FromSeconds(5));
        }

        lock (_worldGate)
        {
            _world.ImportantWorldEventRecorded -= World_ImportantWorldEventRecorded;
            _world.WorldCenturyChronicleRecorded -= World_WorldCenturyChronicleRecorded;
        }

        _stop.Dispose();
    }

    private void Run()
    {
        try
        {
            PublishObservationInvalidated();
            while (!_stop.Wait(SimulationPacing.PulseMilliseconds))
            {
                lock (_worldGate)
                {
                    if (!IsPaused)
                    {
                        var started = BeginPerformanceMeasurement();
                        var ticks = SimulationPacing.GetTicksPerPulse(Speed);
                        _world.AdvanceTo(new SimulationTick(checked(_world.Tick.Value + ticks)));
                        RecordSimulationMeasurement(started);
                    }
                }

                PublishObservationInvalidated();
            }
        }
        catch (Exception exception)
        {
            Faulted?.Invoke(exception);
        }
    }

    private void PublishObservationInvalidated()
    {
        ObservationInvalidated?.Invoke();
    }

    private long BeginPerformanceMeasurement() =>
        Volatile.Read(ref _performanceDiagnosticsEnabled) != 0
            ? Stopwatch.GetTimestamp()
            : 0L;

    private void RecordSimulationMeasurement(long started)
    {
        if (started == 0)
        {
            return;
        }

        RecordMeasurement(
            GetElapsedMicroseconds(started),
            ref _simulationPulseCount,
            ref _simulationTotalMicroseconds,
            ref _simulationLastMicroseconds,
            ref _simulationMaximumMicroseconds);
    }

    private void RecordObservationMeasurement(long started)
    {
        if (started == 0)
        {
            return;
        }

        RecordMeasurement(
            GetElapsedMicroseconds(started),
            ref _observationCount,
            ref _observationTotalMicroseconds,
            ref _observationLastMicroseconds,
            ref _observationMaximumMicroseconds);
    }

    private static long GetElapsedMicroseconds(long started) =>
        Math.Max(1L, (long)Math.Round(
            Stopwatch.GetElapsedTime(started).TotalMicroseconds,
            MidpointRounding.AwayFromZero));

    private static void RecordMeasurement(
        long microseconds,
        ref long count,
        ref long total,
        ref long last,
        ref long maximum)
    {
        Interlocked.Exchange(ref last, microseconds);
        Interlocked.Increment(ref count);
        Interlocked.Add(ref total, microseconds);
        var observedMaximum = Interlocked.Read(ref maximum);
        while (microseconds > observedMaximum)
        {
            var previous = Interlocked.CompareExchange(
                ref maximum,
                microseconds,
                observedMaximum);
            if (previous == observedMaximum)
            {
                break;
            }
            observedMaximum = previous;
        }
    }

    private void World_ImportantWorldEventRecorded(WorldEventNotification notification)
    {
        _pendingWorldEvents.Enqueue(notification);
        var pending = Interlocked.Increment(ref _pendingWorldEventCount);
        while (pending > MaximumPendingWorldEvents
            && _pendingWorldEvents.TryDequeue(out _))
        {
            pending = Interlocked.Decrement(ref _pendingWorldEventCount);
            Interlocked.Increment(ref _droppedWorldEventCount);
        }
    }

    private void World_WorldCenturyChronicleRecorded(WorldCenturyChronicleEntry entry)
    {
        _pendingWorldChronicles.Enqueue(entry);
        var pending = Interlocked.Increment(ref _pendingWorldChronicleCount);
        while (pending > MaximumPendingWorldChronicles
            && _pendingWorldChronicles.TryDequeue(out _))
        {
            pending = Interlocked.Decrement(ref _pendingWorldChronicleCount);
            Interlocked.Increment(ref _droppedWorldChronicleCount);
        }
    }
}

public readonly record struct RuntimePerformanceSnapshot(
    bool IsEnabled,
    int Speed,
    bool IsPaused,
    int PulseBudgetMilliseconds,
    long SimulationPulseCount,
    double LastSimulationMilliseconds,
    double AverageSimulationMilliseconds,
    double MaximumSimulationMilliseconds,
    double EstimatedBacklogMilliseconds,
    long ObservationCount,
    double LastObservationMilliseconds,
    double AverageObservationMilliseconds,
    double MaximumObservationMilliseconds);

public readonly record struct WorldRuntimeMetadata(
    ulong Seed,
    int Population,
    WorldBalanceConfiguration Balance);

public sealed record RuntimeSaveCapture(
    WorldAuthorityCheckpoint Authority,
    WorldSnapshot SummarySnapshot);

public static class SimulationPacing
{
    public const int PulseMilliseconds = 80;
    public const int BaseTicksPerPulse = 16;

    public static int GetTicksPerPulse(int speed)
    {
        if (!RuntimeSpeedRules.IsSupported(speed))
        {
            throw new ArgumentOutOfRangeException(nameof(speed));
        }

        return checked(BaseTicksPerPulse * speed);
    }
}
