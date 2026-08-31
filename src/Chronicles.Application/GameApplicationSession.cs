using Chronicles.Core;
using Chronicles.Persistence;
using Chronicles.Runtime;
using Chronicles.Simulation;

namespace Chronicles.Application;

/// <summary>
/// Application boundary used by the desktop shell. It composes runtime, queries and
/// persistence without exposing mutable world ownership to WPF.
/// </summary>
public sealed class GameApplicationSession : IDisposable
{
    private readonly SimulationRunner _runner;

    public GameApplicationSession(World world)
    {
        _runner = new SimulationRunner(world ?? throw new ArgumentNullException(nameof(world)));
    }

    public static string DefaultSaveRoot => SlotSaveStore.DefaultRoot;

    public static string DefaultSavePath =>
        SlotSaveStore.GetPaths(DefaultSaveRoot, SlotSaveStore.DefaultSlotId).PrimaryPath;

    public static string LegacySavePath => WorldSaveStore.DefaultPath;

    public static string DefaultDataDirectory => SlotSaveStore.DefaultDataDirectory;

    public static GameApplicationSession CreateDefault(ulong seed) =>
        new(World.CreateDefault(seed));

    public event Action? ObservationInvalidated
    {
        add => _runner.ObservationInvalidated += value;
        remove => _runner.ObservationInvalidated -= value;
    }

    public event Action<Exception>? Faulted
    {
        add => _runner.Faulted += value;
        remove => _runner.Faulted -= value;
    }

    public int Speed => _runner.Speed;
    public bool IsPaused => _runner.IsPaused;

    public void Start() => _runner.Start();
    public void SetPaused(bool paused) => _runner.SetPaused(paused);
    public void SetSpeed(int speed) => _runner.SetSpeed(speed);

    public WorldSnapshot GetSnapshot(
        int rankingCount = 1_000,
        int eventCount = 150,
        ObservationSections sections = ObservationSections.All,
        ushort? detailedMapLocationId = null,
        int detailedMapAgentOffset = 0,
        int worldChronicleCount = int.MaxValue) =>
        _runner.GetSnapshot(
            rankingCount,
            eventCount,
            sections,
            detailedMapLocationId,
            detailedMapAgentOffset,
            worldChronicleCount);

    public WorldObservation GetObservation(
        AgentId? selectedAgent = null,
        int rankingCount = 1_000,
        int eventCount = 150,
        ObservationSections sections = ObservationSections.All,
        ushort? detailedMapLocationId = null,
        int detailedMapAgentOffset = 0,
        int worldChronicleCount = int.MaxValue) =>
        _runner.GetObservation(
            selectedAgent,
            rankingCount,
            eventCount,
            sections,
            detailedMapLocationId,
            detailedMapAgentOffset,
            worldChronicleCount);

    public IReadOnlyList<AgentSnapshot> BrowseAgents(int offset, int limit = 500) =>
        _runner.BrowseAgents(offset, limit);

    public IReadOnlyList<AgentSnapshot> SearchAgents(string? query, int limit = 1_000) =>
        _runner.SearchAgents(query, limit);

    public WorldEventFeed DrainWorldEventFeed() => _runner.DrainWorldEventFeed();

    public WorldCenturyChronicleFeed DrainWorldCenturyChronicleFeed() =>
        _runner.DrainWorldCenturyChronicleFeed();

    public WorldCheckpoint CreateCheckpoint() => _runner.CreateCheckpoint();

    public void RestoreCheckpoint(WorldCheckpoint checkpoint) =>
        _runner.RestoreCheckpoint(checkpoint);

    public Task<SlotSaveResult> SaveSlotAsync(
        string slotId,
        CancellationToken cancellationToken = default)
    {
        var authority = _runner.CreateAuthorityCheckpoint();
        return Task.Run(
            () => SlotSaveStore.Save(DefaultSaveRoot, slotId, authority),
            cancellationToken);
    }

    public Task<SlotSaveResult> SaveDefaultAsync(CancellationToken cancellationToken = default) =>
        SaveSlotAsync(SlotSaveStore.DefaultSlotId, cancellationToken);

    public async Task<SlotLoadResult> LoadSlotAsync(
        string slotId,
        CancellationToken cancellationToken = default)
    {
        var result = await Task.Run(
            () => SlotSaveStore.Load(DefaultSaveRoot, slotId),
            cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            _runner.RestoreCheckpoint(result.Checkpoint!);
        }

        return result;
    }

    public async Task<int> LoadDefaultAsync(CancellationToken cancellationToken = default)
    {
        var result = await LoadSlotAsync(
            SlotSaveStore.DefaultSlotId,
            cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new InvalidDataException(result.Message);
        }

        return result.WorldSchema;
    }

    public void Dispose() => _runner.Dispose();
}
