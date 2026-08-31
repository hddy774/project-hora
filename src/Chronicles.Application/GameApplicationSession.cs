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

    public static string DefaultSavePath => WorldSaveStore.DefaultPath;

    public static string DefaultDataDirectory =>
        Path.GetDirectoryName(DefaultSavePath)
        ?? throw new InvalidOperationException("게임 데이터 폴더를 확인할 수 없습니다.");

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

    public Task SaveDefaultAsync(CancellationToken cancellationToken = default)
    {
        var checkpoint = _runner.CreateCheckpoint();
        return Task.Run(
            () => WorldSaveStore.Save(DefaultSavePath, checkpoint),
            cancellationToken);
    }

    public async Task<int> LoadDefaultAsync(CancellationToken cancellationToken = default)
    {
        var checkpoint = await Task.Run(
            () => WorldSaveStore.Load(DefaultSavePath),
            cancellationToken).ConfigureAwait(false);
        _runner.RestoreCheckpoint(checkpoint);
        return checkpoint.SchemaVersion;
    }

    public void Dispose() => _runner.Dispose();
}
