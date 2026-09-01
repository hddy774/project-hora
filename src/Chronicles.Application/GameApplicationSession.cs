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
    private readonly object _legacyCatalogGate = new();
    private readonly Dictionary<(string SlotId, long Sequence), SlotManifestSummary>
        _legacyCatalogSummaries = [];

    public GameApplicationSession(World world)
    {
        _runner = new SimulationRunner(world ?? throw new ArgumentNullException(nameof(world)));
    }

    public static string DefaultSaveRoot => SlotSaveStore.DefaultRoot;

    public static string DefaultSavePath =>
        SlotSaveStore.GetPaths(DefaultSaveRoot, SlotSaveStore.DefaultSlotId).PrimaryPath;

    public static string LegacySavePath => WorldSaveStore.DefaultPath;

    public static string DefaultDataDirectory => SlotSaveStore.DefaultDataDirectory;

    public static AuthorityRegistryDiagnosticsSnapshot AuthorityRegistryDiagnostics =>
        AuthorityRegistryDiagnosticsProjection.Current;

    public static string EnsureSaveRootDirectory() =>
        SlotSaveStore.EnsureRootDirectory(DefaultSaveRoot);

    public static string GetSaveSlotDirectory(string slotId) =>
        SlotSaveStore.GetPaths(DefaultSaveRoot, SaveSlotRegistry.Get(slotId).SlotId).SlotDirectory;

    public static string EnsureSaveSlotDirectory(string slotId) =>
        SlotSaveStore.EnsureSlotDirectory(DefaultSaveRoot, SaveSlotRegistry.Get(slotId).SlotId);

    public static GameApplicationSession CreateDefault(ulong seed) =>
        new(World.CreateDefault(seed));

    public static GameApplicationSession Create(
        ulong seed,
        int population,
        WorldBalanceConfiguration? balance = null) =>
        new(World.Create(
            seed,
            population,
            balance ?? WorldBalanceConfiguration.Default));

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
    public void SetPerformanceDiagnosticsEnabled(bool enabled) =>
        _runner.SetPerformanceDiagnosticsEnabled(enabled);
    public RuntimePerformanceSnapshot GetPerformanceSnapshot() =>
        _runner.GetPerformanceSnapshot();
    public WorldRuntimeMetadata GetWorldMetadata() => _runner.GetWorldMetadata();

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
        int knowledgeEditionLimit = 100) =>
        _runner.GetSnapshot(
            rankingCount,
            eventCount,
            sections,
            detailedMapLocationId,
            detailedMapAgentOffset,
            worldChronicleCount,
            contentSecretRealmOffset,
            contentSecretRealmLimit,
            knowledgeEditionOffset,
            knowledgeEditionLimit);

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
        int knowledgeEditionLimit = 100) =>
        _runner.GetObservation(
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
            knowledgeEditionLimit);

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
        IProgress<SaveOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var definition = SaveSlotRegistry.Get(slotId);
        return Task.Run(
            () =>
            {
                progress?.Report(new SaveOperationProgress(0, "안전 경계 대기"));
                cancellationToken.ThrowIfCancellationRequested();
                var capture = _runner.CreateSaveCapture();
                progress?.Report(new SaveOperationProgress(10, "권위 스냅샷 고정"));
                var summary = CreateManifestSummary(
                    capture.SummarySnapshot,
                    capture.Authority.StateHash,
                    capture.Authority.Checkpoint.Seed,
                    organizationCount: capture.SummarySnapshot.Organizations?.Organizations.Count ?? 0);
                return SlotSaveStore.Save(
                    DefaultSaveRoot,
                    definition.SlotId,
                    capture.Authority,
                    summary,
                    persistence => progress?.Report(
                        SaveOperationProgressProjection.FromPersistence(persistence)));
            },
            cancellationToken);
    }

    public Task<SlotSaveResult> SaveDefaultAsync(CancellationToken cancellationToken = default) =>
        SaveSlotAsync(SlotSaveStore.DefaultSlotId, cancellationToken: cancellationToken);

    public async Task<SlotLoadResult> LoadSlotAsync(
        string slotId,
        IProgress<SaveOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var definition = SaveSlotRegistry.Get(slotId);
        progress?.Report(new SaveOperationProgress(0, "불러오기 준비"));
        var result = await Task.Run(
            () => SlotSaveStore.Load(
                DefaultSaveRoot,
                definition.SlotId,
                persistence => progress?.Report(
                    SaveOperationProgressProjection.FromPersistence(persistence))),
            cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            progress?.Report(new SaveOperationProgress(94, "Candidate World 원자 교체"));
            _runner.RestoreCheckpoint(result.Checkpoint!);
            progress?.Report(new SaveOperationProgress(100, "권위 세계 복원 완료"));
        }

        return result;
    }

    public Task<IReadOnlyList<SaveSlotCatalogEntry>> ListSaveSlotsAsync(
        CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<SaveSlotCatalogEntry>>(
            () =>
            {
                var entries = new SaveSlotCatalogEntry[SaveSlotRegistry.All.Count];
                for (var index = 0; index < entries.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var definition = SaveSlotRegistry.All[index];
                    var inspection = SlotSaveStore.InspectCatalog(
                        DefaultSaveRoot,
                        definition.SlotId);
                    var summary = inspection.Summary;
                    if (summary is null && inspection.CanLoad)
                    {
                        var key = (definition.SlotId, inspection.CreatedSequence);
                        lock (_legacyCatalogGate)
                        {
                            _legacyCatalogSummaries.TryGetValue(key, out summary);
                        }

                        if (summary is null)
                        {
                            var load = SlotSaveStore.Load(DefaultSaveRoot, definition.SlotId);
                            if (load.IsSuccess)
                            {
                                var restored = World.Restore(load.Checkpoint!);
                                var snapshot = restored.CreateSnapshot(
                                    rankingCount: 1,
                                    eventCount: 0,
                                    sections: ObservationSections.StatisticsSummary
                                        | ObservationSections.CombatPowerRanking,
                                    worldChronicleCount: 0);
                                summary = CreateManifestSummary(
                                    snapshot,
                                    load.StateHash,
                                    load.Checkpoint!.Seed,
                                    organizationCount: snapshot.Organizations?.Organizations.Count ?? 0);
                                lock (_legacyCatalogGate)
                                {
                                    _legacyCatalogSummaries[key] = summary;
                                }
                            }
                        }
                    }

                    entries[index] = CreateCatalogEntry(definition, inspection, summary);
                }

                return entries;
            },
            cancellationToken);

    public async Task<int> LoadDefaultAsync(CancellationToken cancellationToken = default)
    {
        var result = await LoadSlotAsync(
            SlotSaveStore.DefaultSlotId,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new InvalidDataException(result.Message);
        }

        return result.WorldSchema;
    }

    public void Dispose() => _runner.Dispose();

    private static SlotManifestSummary CreateManifestSummary(
        WorldSnapshot snapshot,
        ulong stateHash,
        ulong seed,
        int organizationCount)
    {
        var leader = snapshot.CombatPowerRankings.Count > 0
            ? snapshot.CombatPowerRankings[0]
            : throw new InvalidDataException("세이브 요약에 전투력 1위가 없습니다.");
        return new SlotManifestSummary(
            snapshot.Tick.Value,
            snapshot.Population,
            seed,
            leader.AgentId.Value,
            leader.Name,
            leader.HasEnteredCultivation,
            leader.RealmStage,
            leader.RealmLayer,
            leader.GreatPerfectionAchieved,
            leader.CombatPower.Total,
            snapshot.TotalDeaths,
            snapshot.TotalReincarnations,
            snapshot.TotalCombatsCompleted,
            snapshot.ActiveCombats,
            snapshot.TotalBreakthroughs,
            organizationCount,
            stateHash);
    }

    private static SaveSlotCatalogEntry CreateCatalogEntry(
        SaveSlotDefinition definition,
        SlotCatalogInspection inspection,
        SlotManifestSummary? summary)
    {
        var status = inspection.Health switch
        {
            SlotCatalogHealth.Empty => SaveSlotCatalogStatus.Empty,
            SlotCatalogHealth.Ready => SaveSlotCatalogStatus.Ready,
            SlotCatalogHealth.Recoverable => SaveSlotCatalogStatus.Recoverable,
            SlotCatalogHealth.Corrupt => SaveSlotCatalogStatus.Corrupt,
            SlotCatalogHealth.Incompatible => SaveSlotCatalogStatus.Incompatible,
            _ => throw new ArgumentOutOfRangeException(nameof(inspection)),
        };
        return new SaveSlotCatalogEntry(
            definition,
            status,
            inspection.Message,
            inspection.CanLoad,
            inspection.UpdatedAtUtc,
            new SimulationTick(summary?.Tick ?? 0),
            summary?.Population ?? 0,
            summary?.Seed ?? 0,
            summary is null ? null : new AgentId(summary.LeaderAgentId),
            summary?.LeaderName ?? string.Empty,
            summary?.LeaderHasEnteredCultivation ?? false,
            summary?.LeaderRealmStage ?? RealmStage.QiRefining,
            summary?.LeaderRealmLayer ?? 0,
            summary?.LeaderGreatPerfectionAchieved ?? false,
            summary?.LeaderCombatPower ?? 0,
            summary?.TotalDeaths ?? 0,
            summary?.TotalReincarnations ?? 0,
            summary?.TotalCombatsCompleted ?? 0,
            summary?.ActiveCombats ?? 0,
            summary?.TotalBreakthroughs ?? 0,
            summary?.OrganizationCount ?? 0,
            inspection.DatabaseBytes,
            inspection.WorldSchema,
            inspection.CheckpointSchema,
            inspection.CreatedSequence,
            inspection.StateHash,
            inspection.PrimaryReadable,
            inspection.TemporaryReadable,
            inspection.BackupReadable,
            summary is not null);
    }
}
