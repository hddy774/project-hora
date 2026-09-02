using Chronicles.Core;
using Chronicles.Persistence;
using Chronicles.Simulation;

namespace Chronicles.Application;

public sealed record SaveSlotDefinition(int Number, string SlotId, string DisplayName);

public enum SaveSlotCatalogStatus : byte
{
    Empty,
    Ready,
    Recoverable,
    Corrupt,
    Incompatible,
}

public sealed record SaveSlotCatalogEntry(
    SaveSlotDefinition Definition,
    SaveSlotCatalogStatus Status,
    string StatusMessage,
    bool CanLoad,
    DateTimeOffset? UpdatedAtUtc,
    SimulationTick Tick,
    int Population,
    ulong Seed,
    AgentId? LeaderAgentId,
    string LeaderName,
    bool LeaderHasEnteredCultivation,
    RealmStage LeaderRealmStage,
    int LeaderRealmLayer,
    bool LeaderGreatPerfectionAchieved,
    long LeaderCombatPower,
    int TotalDeaths,
    int TotalReincarnations,
    int TotalCombatsCompleted,
    int ActiveCombats,
    int TotalBreakthroughs,
    int OrganizationCount,
    long DatabaseBytes,
    int WorldSchema,
    int CheckpointSchema,
    long CreatedSequence,
    ulong StateHash,
    bool PrimaryReadable,
    bool TemporaryReadable,
    bool BackupReadable,
    bool HasDetailedSummary,
    int StorageFormatVersion,
    int DataPartCount);

public static class SaveSlotRegistry
{
    private static readonly SaveSlotDefinition[] Definitions =
    [
        new(1, SlotSaveStore.DefaultSlotId, "세이브 1"),
        new(2, "slot-02", "세이브 2"),
        new(3, "slot-03", "세이브 3"),
        new(4, "slot-04", "세이브 4"),
        new(5, "slot-05", "세이브 5"),
        new(6, "slot-06", "세이브 6"),
        new(7, "slot-07", "세이브 7"),
        new(8, "slot-08", "세이브 8"),
        new(9, "slot-09", "세이브 9"),
    ];

    public static IReadOnlyList<SaveSlotDefinition> All => Definitions;

    public static SaveSlotDefinition Get(string slotId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        return Definitions.FirstOrDefault(
                definition => StringComparer.Ordinal.Equals(definition.SlotId, slotId))
            ?? throw new ArgumentException("지원하지 않는 세이브 슬롯입니다.", nameof(slotId));
    }
}
