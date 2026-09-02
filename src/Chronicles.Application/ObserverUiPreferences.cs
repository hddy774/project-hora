using System.Text.Json;

namespace Chronicles.Application;

public enum ObserverUiPreferencesLoadStatus : byte
{
    Loaded,
    Missing,
    Invalid,
    Incompatible,
}

public sealed record ObserverUiPreferences(
    int SchemaVersion,
    string ActiveWorkspace,
    double UiScale,
    int? SelectedAgentId,
    string PeopleListMode,
    int ExplorerOffset,
    string ExplorerSearch,
    string AgentDetailSection,
    double AgentDetailScrollOffset,
    bool CompactPersonShowsDetail,
    string SelectedSaveSlotId,
    int SaveWorkspaceTabIndex,
    IReadOnlyList<int> WatchlistedAgentIds)
{
    public const int CurrentSchemaVersion = 1;

    public static ObserverUiPreferences Default { get; } = new(
        CurrentSchemaVersion,
        "World",
        1d,
        null,
        "CombatPower",
        0,
        string.Empty,
        "Overview",
        0d,
        false,
        SaveSlotRegistry.All[0].SlotId,
        0,
        Array.Empty<int>());
}

public sealed record ObserverUiPreferencesLoadResult(
    ObserverUiPreferencesLoadStatus Status,
    ObserverUiPreferences Preferences,
    string Message,
    string Path);

/// <summary>
/// Stores observer-only presentation state separately from every authoritative world slot.
/// A missing, corrupt or future schema never blocks the game from starting.
/// </summary>
public static class ObserverUiPreferencesStore
{
    private static readonly double[] SupportedScales = [0.9d, 1d, 1.1d, 1.25d, 1.4d];
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ChroniclesOfMyriadImmortalPaths",
        "ui");

    public static string DefaultPath => Path.Combine(DefaultDirectory, "observer-ui.json");

    public static ObserverUiPreferencesLoadResult Load(string? path = null)
    {
        var resolvedPath = ResolvePath(path);
        if (!File.Exists(resolvedPath))
        {
            return new ObserverUiPreferencesLoadResult(
                ObserverUiPreferencesLoadStatus.Missing,
                ObserverUiPreferences.Default,
                "저장된 관찰 UI 상태가 없어 기본값을 사용합니다.",
                resolvedPath);
        }

        try
        {
            var stored = JsonSerializer.Deserialize<ObserverUiPreferences>(
                File.ReadAllText(resolvedPath),
                SerializerOptions);
            if (stored is null)
            {
                return Invalid(resolvedPath, "관찰 UI 상태가 비어 있어 기본값을 사용합니다.");
            }

            if (stored.SchemaVersion != ObserverUiPreferences.CurrentSchemaVersion)
            {
                return new ObserverUiPreferencesLoadResult(
                    ObserverUiPreferencesLoadStatus.Incompatible,
                    ObserverUiPreferences.Default,
                    $"지원하지 않는 관찰 UI schema {stored.SchemaVersion}입니다.",
                    resolvedPath);
            }

            return new ObserverUiPreferencesLoadResult(
                ObserverUiPreferencesLoadStatus.Loaded,
                Normalize(stored),
                "관찰 UI 상태를 복원했습니다.",
                resolvedPath);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException)
        {
            return Invalid(resolvedPath, $"관찰 UI 상태를 읽지 못했습니다: {exception.Message}");
        }
    }

    public static void Save(ObserverUiPreferences preferences, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var normalized = Normalize(preferences with
        {
            SchemaVersion = ObserverUiPreferences.CurrentSchemaVersion,
        });
        var resolvedPath = ResolvePath(path);
        var directory = Path.GetDirectoryName(resolvedPath)
            ?? throw new InvalidOperationException("관찰 UI 상태 경로에 폴더가 없습니다.");
        Directory.CreateDirectory(directory);
        var temporaryPath = resolvedPath + ".tmp";
        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       16 * 1024,
                       FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, normalized, SerializerOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, resolvedPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static ObserverUiPreferences Normalize(ObserverUiPreferences value)
    {
        var scale = SupportedScales.Any(item => Math.Abs(item - value.UiScale) < 0.0001d)
            ? value.UiScale
            : ObserverUiPreferences.Default.UiScale;
        var selectedSlotId = SaveSlotRegistry.All.Any(item => StringComparer.Ordinal.Equals(
            item.SlotId,
            value.SelectedSaveSlotId))
                ? value.SelectedSaveSlotId
                : ObserverUiPreferences.Default.SelectedSaveSlotId;
        var watchlist = (value.WatchlistedAgentIds ?? Array.Empty<int>())
            .Where(item => item >= 0)
            .Distinct()
            .Order()
            .ToArray();
        return value with
        {
            ActiveWorkspace = string.IsNullOrWhiteSpace(value.ActiveWorkspace)
                ? ObserverUiPreferences.Default.ActiveWorkspace
                : value.ActiveWorkspace.Trim(),
            UiScale = scale,
            SelectedAgentId = value.SelectedAgentId is >= 0 ? value.SelectedAgentId : null,
            PeopleListMode = string.IsNullOrWhiteSpace(value.PeopleListMode)
                ? ObserverUiPreferences.Default.PeopleListMode
                : value.PeopleListMode.Trim(),
            ExplorerOffset = Math.Max(0, value.ExplorerOffset),
            ExplorerSearch = value.ExplorerSearch?.Trim() ?? string.Empty,
            AgentDetailSection = string.IsNullOrWhiteSpace(value.AgentDetailSection)
                ? ObserverUiPreferences.Default.AgentDetailSection
                : value.AgentDetailSection.Trim(),
            AgentDetailScrollOffset = double.IsFinite(value.AgentDetailScrollOffset)
                ? Math.Max(0d, value.AgentDetailScrollOffset)
                : 0d,
            SelectedSaveSlotId = selectedSlotId,
            SaveWorkspaceTabIndex = Math.Clamp(value.SaveWorkspaceTabIndex, 0, 1),
            WatchlistedAgentIds = watchlist,
        };
    }

    private static ObserverUiPreferencesLoadResult Invalid(string path, string message) => new(
        ObserverUiPreferencesLoadStatus.Invalid,
        ObserverUiPreferences.Default,
        message,
        path);

    private static string ResolvePath(string? path) => string.IsNullOrWhiteSpace(path)
        ? DefaultPath
        : Path.GetFullPath(path);
}
