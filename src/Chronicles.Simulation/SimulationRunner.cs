using System.Collections.Concurrent;
using Chronicles.Core;

namespace Chronicles.Simulation;

public sealed class SimulationRunner : IDisposable
{
    private const int MaximumPendingWorldEvents = 10_000;
    private const int MaximumPendingWorldChronicles = 1_024;
    private readonly object _worldGate = new();
    private World _world;
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

    public SimulationRunner(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
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
        if (speed is not (1 or 10 or 25 or 50 or 100))
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
        int worldChronicleCount = int.MaxValue)
    {
        lock (_worldGate)
        {
            return _world.CreateSnapshot(
                rankingCount,
                eventCount,
                sections,
                detailedMapLocationId,
                detailedMapAgentOffset,
                worldChronicleCount);
        }
    }

    public AgentSnapshot GetAgent(AgentId agentId)
    {
        lock (_worldGate)
        {
            return _world.GetAgentSnapshot(agentId);
        }
    }

    public WorldObservation GetObservation(
        AgentId? selectedAgent = null,
        int rankingCount = 1_000,
        int eventCount = 150,
        ObservationSections sections = ObservationSections.All,
        ushort? detailedMapLocationId = null,
        int detailedMapAgentOffset = 0,
        int worldChronicleCount = int.MaxValue)
    {
        lock (_worldGate)
        {
            var world = _world.CreateSnapshot(
                rankingCount,
                eventCount,
                sections,
                detailedMapLocationId,
                detailedMapAgentOffset,
                worldChronicleCount);
            var agentId = selectedAgent ?? world.LeaderId;
            var agent = _world.GetAgentSnapshot(
                agentId,
                includeAptitudeDetails:
                    (sections & ObservationSections.AptitudeDetails) != 0,
                includeRelations:
                    (sections & ObservationSections.Relations) != 0,
                includeTeaching:
                    (sections & ObservationSections.Teaching) != 0);
            return new WorldObservation(
                world,
                agent,
                (sections & ObservationSections.PersonalLog) != 0
                    ? _world.GetAgentPersonalLog(agentId)
                    : Array.Empty<PersonalLogEntry>(),
                (sections & ObservationSections.Chronicle) != 0
                    ? _world.GetAgentChronicle(agentId)
                    : Array.Empty<LifeChronicleEntry>(),
                (sections & ObservationSections.Reincarnation) != 0
                    ? _world.GetAgentReincarnationSnapshot(agentId)
                    : null);
        }
    }

    public IReadOnlyList<AgentSnapshot> BrowseAgents(int offset, int limit = 500)
    {
        lock (_worldGate)
        {
            return _world.BrowseAgents(offset, limit);
        }
    }

    public IReadOnlyList<AgentSnapshot> SearchAgents(string? query, int limit = 1_000)
    {
        lock (_worldGate)
        {
            return _world.SearchAgents(query, limit);
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

    public void RestoreCheckpoint(WorldCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var restored = World.Restore(checkpoint);
        lock (_worldGate)
        {
            _world.ImportantWorldEventRecorded -= World_ImportantWorldEventRecorded;
            _world.WorldCenturyChronicleRecorded -= World_WorldCenturyChronicleRecorded;
            _world = restored;
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
                        var ticks = SimulationPacing.GetTicksPerPulse(Speed);
                        _world.AdvanceTo(new SimulationTick(checked(_world.Tick.Value + ticks)));
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

public static class SimulationPacing
{
    public const int PulseMilliseconds = 80;
    public const int BaseTicksPerPulse = 16;

    public static int GetTicksPerPulse(int speed)
    {
        if (speed is not (1 or 10 or 25 or 50 or 100))
        {
            throw new ArgumentOutOfRangeException(nameof(speed));
        }

        return checked(BaseTicksPerPulse * speed);
    }
}
