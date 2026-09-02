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
    private readonly HashSet<int> _chronicleRetentionWatchlist = [];
    private World _world;
    private WorldObservationQueries _queries;
    private readonly ManualResetEventSlim _stop = new(initialState: false);
    private readonly Thread _worker;
    private int _speed = 1;
    private int _paused;
    private int _started;
    private int _disposed;
    private readonly object _pendingWorldEventsGate = new();
    private readonly Queue<WorldEventNotification> _pendingWorldEvents = new();
    private int _droppedWorldEventCount;
    private readonly object _pendingWorldChroniclesGate = new();
    private readonly Queue<WorldCenturyChronicleEntry> _pendingWorldChronicles = new();
    private int _droppedWorldChronicleCount;
    private int _worldFeedCaptureEnabled;
    private int _performanceDiagnosticsEnabled;
    private long _simulationPulseCount;
    private long _simulationTotalMicroseconds;
    private long _simulationLastMicroseconds;
    private long _simulationMaximumMicroseconds;
    private long _simulationBacklogMicroseconds;
    private long _observationCount;
    private long _observationTotalMicroseconds;
    private long _observationLastMicroseconds;
    private long _observationMaximumMicroseconds;

    public SimulationRunner(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _queries = new WorldObservationQueries(_world);
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
    public bool IsWorldFeedCaptureEnabled =>
        Volatile.Read(ref _worldFeedCaptureEnabled) != 0;

    public void SetPerformanceDiagnosticsEnabled(bool enabled)
    {
        Volatile.Write(ref _performanceDiagnosticsEnabled, enabled ? 1 : 0);
        if (!enabled)
        {
            lock (_worldGate)
            {
                Interlocked.Exchange(ref _simulationBacklogMicroseconds, 0L);
            }
        }
    }

    public RuntimePerformanceSnapshot GetPerformanceSnapshot()
    {
        var simulationCount = Interlocked.Read(ref _simulationPulseCount);
        var observationCount = Interlocked.Read(ref _observationCount);
        var lastSimulation = Interlocked.Read(ref _simulationLastMicroseconds);
        return new RuntimePerformanceSnapshot(
            IsEnabled: Volatile.Read(ref _performanceDiagnosticsEnabled) != 0,
            Speed,
            IsPaused,
            SimulationPacing.PerformanceBudgetMilliseconds,
            simulationCount,
            lastSimulation / 1_000d,
            simulationCount == 0
                ? 0d
                : Interlocked.Read(ref _simulationTotalMicroseconds) / 1_000d / simulationCount,
            Interlocked.Read(ref _simulationMaximumMicroseconds) / 1_000d,
            Interlocked.Read(ref _simulationBacklogMicroseconds) / 1_000d,
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
                _world.Balance,
                _world.Generation);
        }
    }

    public void SetDetailedChronicleProtection(AgentId agentId, bool isProtected)
    {
        lock (_worldGate)
        {
            _world.SetDetailedChronicleProtection(agentId, isProtected);
            if (isProtected)
            {
                _chronicleRetentionWatchlist.Add(agentId.Value);
            }
            else
            {
                _chronicleRetentionWatchlist.Remove(agentId.Value);
            }
        }
    }

    public IReadOnlyList<AgentId> GetDetailedChronicleProtectionList()
    {
        lock (_worldGate)
        {
            return _world.GetDetailedChronicleProtectionList();
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

    public void SetPaused(bool paused)
    {
        Volatile.Write(ref _paused, paused ? 1 : 0);
        if (paused)
        {
            lock (_worldGate)
            {
                Interlocked.Exchange(ref _simulationBacklogMicroseconds, 0L);
            }
        }
    }

    public void SetSpeed(int speed)
    {
        if (!RuntimeSpeedRules.IsSupported(speed))
        {
            throw new ArgumentOutOfRangeException(nameof(speed), "배속은 1, 10, 25, 50, 100 중 하나여야 합니다.");
        }

        Volatile.Write(ref _speed, speed);
    }

    public void SetWorldFeedCaptureEnabled(bool enabled)
    {
        lock (_worldGate)
        {
            if (enabled == IsWorldFeedCaptureEnabled)
            {
                return;
            }

            if (enabled)
            {
                Volatile.Write(ref _worldFeedCaptureEnabled, 1);
                _world.ImportantWorldEventRecorded += World_ImportantWorldEventRecorded;
                _world.WorldCenturyChronicleRecorded += World_WorldCenturyChronicleRecorded;
            }
            else
            {
                _world.ImportantWorldEventRecorded -= World_ImportantWorldEventRecorded;
                _world.WorldCenturyChronicleRecorded -= World_WorldCenturyChronicleRecorded;
                Volatile.Write(ref _worldFeedCaptureEnabled, 0);
            }
        }

        if (!enabled)
        {
            ClearPendingWorldFeeds();
        }
    }

    public WorldSnapshot GetSnapshot(
        int rankingCount = 1_000,
        int eventCount = 150,
        ObservationSections sections = ObservationSections.All,
        ushort? detailedMapLocationId = null,
        int detailedMapAgentOffset = 0,
        int worldChronicleCount = int.MaxValue,
        int contentSecretRealmOffset = 0,
        int contentSecretRealmLimit = 100,
        int knowledgeEditionOffset = 0,
        int knowledgeEditionLimit = 100,
        int lawCandidateOffset = 0,
        int lawCandidateLimit = 54,
        int contentItemOffset = 0,
        int contentItemLimit = 0) => GetSnapshot(new ObservationRequest(
            null,
            rankingCount,
            eventCount,
            sections,
            detailedMapLocationId,
            detailedMapAgentOffset,
            worldChronicleCount,
            contentSecretRealmOffset,
            contentSecretRealmLimit,
            knowledgeEditionOffset,
            knowledgeEditionLimit,
            lawCandidateOffset,
            lawCandidateLimit,
            contentItemOffset,
            contentItemLimit));

    public WorldSnapshot GetSnapshot(ObservationRequest request)
    {
        var started = BeginPerformanceMeasurement();
        lock (_worldGate)
        {
            var snapshot = _queries.GetSnapshot(request);
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
        int contentSecretRealmLimit = 100,
        int knowledgeEditionOffset = 0,
        int knowledgeEditionLimit = 100,
        int lawCandidateOffset = 0,
        int lawCandidateLimit = 54,
        int contentItemOffset = 0,
        int contentItemLimit = 0) => GetObservation(new ObservationRequest(
            selectedAgent,
            rankingCount,
            eventCount,
            sections,
            detailedMapLocationId,
            detailedMapAgentOffset,
            worldChronicleCount,
            contentSecretRealmOffset,
            contentSecretRealmLimit,
            knowledgeEditionOffset,
            knowledgeEditionLimit,
            lawCandidateOffset,
            lawCandidateLimit,
            contentItemOffset,
            contentItemLimit));

    public WorldObservation GetObservation(ObservationRequest request)
    {
        var started = BeginPerformanceMeasurement();
        lock (_worldGate)
        {
            var observation = _queries.GetObservation(request);
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

    public AgentListPage BrowseAgentsById(
        IReadOnlyList<AgentId> agentIds,
        string? query,
        int offset,
        int limit)
    {
        var started = BeginPerformanceMeasurement();
        lock (_worldGate)
        {
            var page = _queries.BrowseAgentsById(agentIds, query, offset, limit);
            RecordObservationMeasurement(started);
            return page;
        }
    }

    public WorldEventFeed DrainWorldEventFeed()
    {
        lock (_pendingWorldEventsGate)
        {
            var events = new List<WorldEventNotification>(_pendingWorldEvents.Count);
            while (_pendingWorldEvents.TryDequeue(out var notification))
            {
                events.Add(notification);
            }

            return new WorldEventFeed(
                events,
                Interlocked.Exchange(ref _droppedWorldEventCount, 0));
        }
    }

    public WorldCenturyChronicleFeed DrainWorldCenturyChronicleFeed()
    {
        lock (_pendingWorldChroniclesGate)
        {
            var entries = new List<WorldCenturyChronicleEntry>(_pendingWorldChronicles.Count);
            while (_pendingWorldChronicles.TryDequeue(out var entry))
            {
                entries.Add(entry);
            }

            return new WorldCenturyChronicleFeed(
                entries,
                Interlocked.Exchange(ref _droppedWorldChronicleCount, 0));
        }
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

    /// <summary>
    /// 영구 기록을 배열로 복제하지 않은 checkpoint view를 안전 경계 안에서만 사용한다.
    /// callback이 반환된 뒤 capture를 보관하면 안 된다.
    /// </summary>
    public TResult ExecuteSaveAtSafeBoundary<TResult>(
        Func<RuntimeSaveCapture, TResult> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_worldGate)
        {
            return operation(new RuntimeSaveCapture(
                _world.CreateStreamingAuthorityCheckpoint(),
                _world.CreateSnapshot(
                    rankingCount: 1,
                    eventCount: 0,
                    sections: ObservationSections.StatisticsSummary
                        | ObservationSections.CombatPowerRanking
                        | ObservationSections.Organization,
                    worldChronicleCount: 0)));
        }
    }

    public void RestoreCheckpoint(WorldCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        AgentId[] protectedAgents;
        lock (_worldGate)
        {
            protectedAgents = _chronicleRetentionWatchlist
                .Select(value => new AgentId(value))
                .ToArray();
        }
        var restored = World.Restore(checkpoint, protectedAgents);
        ReplaceWorld(restored);
    }

    public void RestoreValidatedWorld(World restored)
    {
        ArgumentNullException.ThrowIfNull(restored);
        AgentId[] protectedAgents;
        lock (_worldGate)
        {
            protectedAgents = _chronicleRetentionWatchlist
                .Select(value => new AgentId(value))
                .ToArray();
        }
        foreach (var agentId in protectedAgents)
        {
            restored.SetDetailedChronicleProtection(agentId, isProtected: true);
        }
        restored.TrimOrganizationHistoriesForLoadedSession();
        ReplaceWorld(restored);
    }

    private void ReplaceWorld(World restored)
    {
        lock (_worldGate)
        {
            _world.ImportantWorldEventRecorded -= World_ImportantWorldEventRecorded;
            _world.WorldCenturyChronicleRecorded -= World_WorldCenturyChronicleRecorded;
            _world = restored;
            _queries = new WorldObservationQueries(_world);
            _chronicleRetentionWatchlist.Clear();
            foreach (var agentId in _world.GetDetailedChronicleProtectionList())
            {
                _chronicleRetentionWatchlist.Add(agentId.Value);
            }
            if (IsWorldFeedCaptureEnabled)
            {
                _world.ImportantWorldEventRecorded += World_ImportantWorldEventRecorded;
                _world.WorldCenturyChronicleRecorded += World_WorldCenturyChronicleRecorded;
            }
            ClearPendingWorldFeeds();
            Interlocked.Exchange(ref _simulationBacklogMicroseconds, 0L);
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
            var pulseStopwatchTicks = checked(
                Stopwatch.Frequency * SimulationPacing.PulseMilliseconds / 1_000L);
            var nextPulse = checked(Stopwatch.GetTimestamp() + pulseStopwatchTicks);
            var pulsesSinceInvalidation = 0;
            while (WaitUntil(nextPulse))
            {
                nextPulse = checked(nextPulse + pulseStopwatchTicks);
                lock (_worldGate)
                {
                    if (!IsPaused)
                    {
                        var started = BeginPerformanceMeasurement();
                        var ticks = SimulationPacing.GetTicksPerPulse(Speed);
                        _world.AdvanceTo(new SimulationTick(checked(_world.Tick.Value + ticks)));
                        RecordSimulationMeasurement(started, nextPulse);
                    }
                }

                var deadlineLag = Stopwatch.GetTimestamp() - nextPulse;
                if (deadlineLag > SimulationPacing.MaximumCatchUpStopwatchTicks)
                {
                    nextPulse = checked(Stopwatch.GetTimestamp() + pulseStopwatchTicks);
                }

                if (++pulsesSinceInvalidation >= SimulationPacing.PulsesPerObservationInvalidation)
                {
                    pulsesSinceInvalidation = 0;
                    PublishObservationInvalidated();
                }
            }
        }
        catch (Exception exception)
        {
            Faulted?.Invoke(exception);
        }
    }

    private bool WaitUntil(long deadline)
    {
        while (!_stop.IsSet)
        {
            var remainingStopwatchTicks = deadline - Stopwatch.GetTimestamp();
            if (remainingStopwatchTicks <= 0)
            {
                return true;
            }

            var remaining = TimeSpan.FromSeconds(
                remainingStopwatchTicks / (double)Stopwatch.Frequency);
            if (_stop.Wait(remaining))
            {
                return false;
            }
        }

        return false;
    }

    private void PublishObservationInvalidated()
    {
        ObservationInvalidated?.Invoke();
    }

    private long BeginPerformanceMeasurement() =>
        Volatile.Read(ref _performanceDiagnosticsEnabled) != 0
            ? Stopwatch.GetTimestamp()
            : 0L;

    private void RecordSimulationMeasurement(long started, long nextPulse)
    {
        if (started == 0)
        {
            return;
        }

        var backlogStopwatchTicks = Math.Max(0L, Stopwatch.GetTimestamp() - nextPulse);
        Interlocked.Exchange(
            ref _simulationBacklogMicroseconds,
            checked(backlogStopwatchTicks * 1_000_000L / Stopwatch.Frequency));
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
        lock (_pendingWorldEventsGate)
        {
            if (!IsWorldFeedCaptureEnabled)
            {
                return;
            }
            if (_pendingWorldEvents.Count == MaximumPendingWorldEvents)
            {
                _pendingWorldEvents.Dequeue();
                Interlocked.Increment(ref _droppedWorldEventCount);
            }
            _pendingWorldEvents.Enqueue(notification);
        }
    }

    private void ClearPendingWorldFeeds()
    {
        lock (_pendingWorldEventsGate)
        {
            _pendingWorldEvents.Clear();
            Interlocked.Exchange(ref _droppedWorldEventCount, 0);
        }
        lock (_pendingWorldChroniclesGate)
        {
            _pendingWorldChronicles.Clear();
            Interlocked.Exchange(ref _droppedWorldChronicleCount, 0);
        }
    }

    private void World_WorldCenturyChronicleRecorded(WorldCenturyChronicleEntry entry)
    {
        lock (_pendingWorldChroniclesGate)
        {
            if (!IsWorldFeedCaptureEnabled)
            {
                return;
            }
            if (_pendingWorldChronicles.Count == MaximumPendingWorldChronicles)
            {
                _pendingWorldChronicles.Dequeue();
                Interlocked.Increment(ref _droppedWorldChronicleCount);
            }
            _pendingWorldChronicles.Enqueue(entry);
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
    WorldBalanceConfiguration Balance,
    WorldGenerationConfiguration Generation);

public sealed record RuntimeSaveCapture(
    WorldAuthorityCheckpoint Authority,
    WorldSnapshot SummarySnapshot);

public static class SimulationPacing
{
    public const int PulseMilliseconds = 50;
    public const int BaseTicksPerPulse = 10;
    public const int PulsesPerObservationInvalidation = 2;
    public const int PerformanceBudgetMilliseconds = 80;
    public const int MaximumCatchUpMilliseconds = 1_000;

    internal static long MaximumCatchUpStopwatchTicks { get; } = checked(
        Stopwatch.Frequency * MaximumCatchUpMilliseconds / 1_000L);

    public static int GetTicksPerPulse(int speed)
    {
        if (!RuntimeSpeedRules.IsSupported(speed))
        {
            throw new ArgumentOutOfRangeException(nameof(speed));
        }

        return checked(BaseTicksPerPulse * speed);
    }
}
