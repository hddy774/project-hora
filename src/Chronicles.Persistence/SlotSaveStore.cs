using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Chronicles.Simulation;

namespace Chronicles.Persistence;

public enum SlotLoadStatus
{
    Success,
    SlotNotFound,
    NoValidCandidate,
    IncompatibleLegacySave,
    IncompatibleWorldSchema,
    IncompatibleCheckpointSchema,
}

public enum SlotCandidateKind
{
    Primary,
    Temporary,
    Backup,
}

public sealed record SlotPaths(
    string SlotDirectory,
    string ManifestPath,
    string PrimaryPath,
    string BackupPath,
    string TemporaryPath);

public sealed record SlotManifest(
    int ManifestVersion,
    string SlotId,
    int WorldSchema,
    int CheckpointSchema,
    long CreatedSequence,
    DateTimeOffset UpdatedAtUtc,
    string DatabaseFile,
    SlotManifestSummary? Summary = null);

public sealed record SlotManifestSummary(
    long Tick,
    int Population,
    ulong Seed,
    int LeaderAgentId,
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
    ulong StateHash);

public enum SlotCatalogHealth : byte
{
    Empty,
    Ready,
    Recoverable,
    Corrupt,
    Incompatible,
}

public sealed record SlotCatalogInspection(
    string SlotId,
    SlotCatalogHealth Health,
    string Message,
    SlotManifest? Manifest,
    SlotManifestSummary? Summary,
    long DatabaseBytes,
    int WorldSchema,
    int CheckpointSchema,
    long CreatedSequence,
    ulong StateHash,
    bool PrimaryReadable,
    bool TemporaryReadable,
    bool BackupReadable,
    DateTimeOffset? UpdatedAtUtc,
    int StorageFormatVersion = 0,
    int DataPartCount = 0)
{
    public bool CanLoad => Health is SlotCatalogHealth.Ready or SlotCatalogHealth.Recoverable;
}

public sealed record SlotCandidateFailure(
    SlotCandidateKind Kind,
    string Path,
    SlotLoadStatus Status,
    string Reason);

public sealed record SlotSaveResult(
    string SlotId,
    string DatabasePath,
    int WorldSchema,
    int CheckpointSchema,
    long CreatedSequence,
    ulong StateHash,
    long PayloadBytes,
    long JsonBytes,
    int StorageFormatVersion,
    int DataPartCount);

public sealed record SlotLoadResult(
    SlotLoadStatus Status,
    string Message,
    WorldCheckpoint? Checkpoint = null,
    SlotCandidateKind? CandidateKind = null,
    string? CandidatePath = null,
    int WorldSchema = 0,
    int CheckpointSchema = 0,
    long CreatedSequence = 0,
    ulong StateHash = 0,
    IReadOnlyList<SlotCandidateFailure>? Failures = null,
    World? CandidateWorld = null)
{
    public bool IsSuccess => Status == SlotLoadStatus.Success && Checkpoint is not null;
}

public enum SlotPersistencePhase : byte
{
    Preparing,
    EncodingAuthority,
    WritingCandidate,
    VerifyingCandidate,
    ReplacingPrimary,
    WritingManifest,
    InspectingCandidates,
    SelectingCandidate,
    DecodingCandidate,
    ValidatingWorld,
    Complete,
}

public readonly record struct SlotPersistenceProgress(
    int Percent,
    SlotPersistencePhase Phase,
    SlotCandidateKind? CandidateKind = null);

/// <summary>
/// Schema 25 PC slot storage. The database schema and canonical authority payload
/// schema are deliberately independent: SQLite user_version is 25 while the
/// canonical payload uses the current checkpoint schema.
/// </summary>
public static partial class SlotSaveStore
{
    public const int WorldSchema = 25;
    public const int ManifestVersion = 2;
    public const string DefaultSlotId = "default";
    private const string DatabaseFileName = "world.db";
    private const string ManifestFileName = "slot.json";
    private const int IncrementalBlobChunkBytes = 256 * 1024;
    private const int MagicContainerHeaderBytes = 4 + sizeof(int) + sizeof(int) + SHA256.HashSizeInBytes;
    private const string CreateSchemaSql = """
        CREATE TABLE authority_snapshot (
            id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
            world_schema INTEGER NOT NULL,
            checkpoint_schema INTEGER NOT NULL,
            seed BLOB NOT NULL CHECK (length(seed) = 8),
            tick INTEGER NOT NULL,
            state_hash BLOB NOT NULL CHECK (length(state_hash) = 8),
            payload_sha256 BLOB NOT NULL CHECK (length(payload_sha256) = 32),
            created_sequence INTEGER NOT NULL,
            event_sequence INTEGER NOT NULL,
            chronicle_sequence INTEGER NOT NULL,
            payload BLOB NOT NULL
        ) STRICT;
        """;
    private const string InsertSql = """
        INSERT INTO authority_snapshot (
            id, world_schema, checkpoint_schema, seed, tick, state_hash,
            payload_sha256, created_sequence, event_sequence, chronicle_sequence, payload)
        VALUES (1, ?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, zeroblob(?10));
        """;
    private const string UpdatePayloadHashSql =
        "UPDATE authority_snapshot SET payload_sha256=?1 WHERE id=1;";
    private const string SelectSql = """
        SELECT world_schema, checkpoint_schema, seed, tick, state_hash,
               payload_sha256, created_sequence, event_sequence, chronicle_sequence,
               length(payload)
          FROM authority_snapshot
         WHERE id = 1;
        """;
    private const string SelectHeaderSql = """
        SELECT world_schema, checkpoint_schema, seed, tick, state_hash,
               created_sequence, event_sequence, chronicle_sequence
          FROM authority_snapshot
         WHERE id = 1;
        """;
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        WriteIndented = true,
    };

    public static string DefaultDataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ChroniclesOfMyriadImmortalPaths");

    public static string DefaultRoot => Path.Combine(DefaultDataDirectory, "saves");

    public static SlotPaths GetPaths(string rootDirectory, string slotId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ValidateSlotId(slotId);
        var root = Path.GetFullPath(rootDirectory);
        var slotDirectory = Path.Combine(root, slotId);
        return new SlotPaths(
            slotDirectory,
            Path.Combine(slotDirectory, ManifestFileName),
            Path.Combine(slotDirectory, DatabaseFileName),
            Path.Combine(slotDirectory, DatabaseFileName + ".bak"),
            Path.Combine(slotDirectory, DatabaseFileName + ".tmp"));
    }

    public static string EnsureRootDirectory(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        var root = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(root);
        return root;
    }

    public static string EnsureSlotDirectory(string rootDirectory, string slotId)
    {
        var paths = GetPaths(rootDirectory, slotId);
        Directory.CreateDirectory(paths.SlotDirectory);
        return paths.SlotDirectory;
    }

    /// <summary>
    /// Reads only the manifest and the fixed-width SQLite header row. The canonical
    /// payload BLOB is deliberately excluded and remains validated by Load.
    /// </summary>
    public static SlotCatalogInspection InspectCatalog(string rootDirectory, string slotId)
    {
        var paths = GetPaths(rootDirectory, slotId);
        var hasPrimary = File.Exists(paths.PrimaryPath);
        var hasTemporary = File.Exists(paths.TemporaryPath);
        var hasBackup = File.Exists(paths.BackupPath);
        var hasManifest = File.Exists(paths.ManifestPath);
        if (!hasPrimary && !hasTemporary && !hasBackup && !hasManifest)
        {
            return new SlotCatalogInspection(
                slotId,
                SlotCatalogHealth.Empty,
                "비어 있는 저장 슬롯입니다.",
                null,
                null,
                0,
                0,
                0,
                0,
                0,
                false,
                false,
                false,
                null);
        }

        SlotManifest? manifest = null;
        string? manifestFailure = null;
        if (hasManifest)
        {
            try
            {
                manifest = JsonSerializer.Deserialize<SlotManifest>(
                    File.ReadAllBytes(paths.ManifestPath),
                    ManifestJsonOptions);
                if (manifest is null
                    || manifest.ManifestVersion is < 1 or > ManifestVersion
                    || !StringComparer.Ordinal.Equals(manifest.SlotId, slotId)
                    || !StringComparer.Ordinal.Equals(manifest.DatabaseFile, DatabaseFileName))
                {
                    manifestFailure = "슬롯 manifest 형식 또는 슬롯 ID가 잘못되었습니다.";
                    manifest = null;
                }
            }
            catch (Exception exception) when (exception is IOException
                or JsonException
                or UnauthorizedAccessException)
            {
                manifestFailure = exception.Message;
            }
        }

        var headers = new List<SlotCandidateHeader>(3);
        var failures = new List<SlotCandidateFailure>(3);
        TryInspectCandidateHeader(
            paths.PrimaryPath,
            SlotCandidateKind.Primary,
            headers,
            failures);
        TryInspectCandidateHeader(
            paths.TemporaryPath,
            SlotCandidateKind.Temporary,
            headers,
            failures);
        TryInspectCandidateHeader(
            paths.BackupPath,
            SlotCandidateKind.Backup,
            headers,
            failures);
        if (headers.Count == 0)
        {
            var incompatible = failures.Any(failure => failure.Status is
                SlotLoadStatus.IncompatibleWorldSchema
                or SlotLoadStatus.IncompatibleCheckpointSchema);
            return new SlotCatalogInspection(
                slotId,
                incompatible ? SlotCatalogHealth.Incompatible : SlotCatalogHealth.Corrupt,
                incompatible
                    ? "현재 버전과 호환되지 않는 저장입니다."
                    : manifestFailure ?? "읽을 수 있는 SQLite 저장 후보가 없습니다.",
                manifest,
                null,
                0,
                manifest?.WorldSchema ?? 0,
                manifest?.CheckpointSchema ?? 0,
                manifest?.CreatedSequence ?? 0,
                0,
                false,
                false,
                false,
                manifest?.UpdatedAtUtc);
        }

        var selected = headers
            .OrderByDescending(header => header.CreatedSequence)
            .ThenBy(header => CandidatePriority(header.Kind))
            .First();
        var manifestMatches = manifest is not null
            && manifest.WorldSchema == selected.WorldSchema
            && manifest.CheckpointSchema == selected.CheckpointSchema
            && manifest.CreatedSequence == selected.CreatedSequence;
        var summary = manifestMatches
            && manifest!.ManifestVersion >= 2
            && manifest.Summary is { } candidateSummary
            && candidateSummary.Tick == selected.Tick
            && candidateSummary.Seed == selected.Seed
            && candidateSummary.StateHash == selected.StateHash
                ? candidateSummary
                : null;
        var isReady = selected.Kind == SlotCandidateKind.Primary
            && manifestMatches
            && manifestFailure is null;
        var updatedAt = manifestMatches
            ? manifest!.UpdatedAtUtc
            : new DateTimeOffset(File.GetLastWriteTimeUtc(selected.Path), TimeSpan.Zero);
        return new SlotCatalogInspection(
            slotId,
            isReady ? SlotCatalogHealth.Ready : SlotCatalogHealth.Recoverable,
            isReady
                ? "정상 저장"
                : "복구 후보가 있으며 불러올 때 전체 무결성을 검사합니다.",
            manifest,
            summary,
            new FileInfo(selected.Path).Length,
            selected.WorldSchema,
            selected.CheckpointSchema,
            selected.CreatedSequence,
            selected.StateHash,
            headers.Any(header => header.Kind == SlotCandidateKind.Primary),
            headers.Any(header => header.Kind == SlotCandidateKind.Temporary),
            headers.Any(header => header.Kind == SlotCandidateKind.Backup),
            updatedAt,
            selected.Layout?.FormatVersion ?? 0,
            selected.Layout is null ? 1 : selected.Layout.SectionCount + 1);
    }

    public static SlotSaveResult Save(
        string rootDirectory,
        string slotId,
        WorldCheckpoint checkpoint,
        SlotManifestSummary? summary = null,
        Action<SlotPersistenceProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        Report(progress, 5, SlotPersistencePhase.Preparing);
        var restored = World.Restore(checkpoint);
        return SaveCore(
            rootDirectory,
            slotId,
            checkpoint,
            restored.ComputeStateHash(),
            summary,
            progress);
    }

    public static SlotSaveResult Save(
        string rootDirectory,
        string slotId,
        WorldAuthorityCheckpoint authority,
        SlotManifestSummary? summary = null,
        Action<SlotPersistenceProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(authority);
        World.ValidateCheckpointForPersistence(authority.Checkpoint);
        return SaveCore(
            rootDirectory,
            slotId,
            authority.Checkpoint,
            authority.StateHash,
            summary,
            progress);
    }

    private static SlotSaveResult SaveCore(
        string rootDirectory,
        string slotId,
        WorldCheckpoint checkpoint,
        ulong stateHash,
        SlotManifestSummary? summary,
        Action<SlotPersistenceProgress>? progress)
    {
        EncodedCheckpointLayout? encoded = null;
        try
        {
            Report(progress, 15, SlotPersistencePhase.Preparing);
            if (checkpoint.SchemaVersion != WorldCheckpoint.CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    $"새 PC 슬롯에는 현재 권위 checkpoint schema {WorldCheckpoint.CurrentSchemaVersion}만 저장할 수 있습니다.");
            }

            ValidateSummary(checkpoint, stateHash, summary);

            var paths = GetPaths(rootDirectory, slotId);
            Directory.CreateDirectory(paths.SlotDirectory);
            using var slotLock = new FileStream(
                Path.Combine(paths.SlotDirectory, "slot.lock"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            DeleteStalePayloadFiles(paths.SlotDirectory);

            Report(progress, 30, SlotPersistencePhase.EncodingAuthority);
            encoded = WorldCheckpointSectionStore.Encode(
                checkpoint,
                paths.SlotDirectory);
            var createdSequence = checked(GetMaximumKnownSequence(paths) + 1);

            Report(progress, 50, SlotPersistencePhase.WritingCandidate);
            var payloadHash = WriteDatabase(
                paths.TemporaryPath,
                checkpoint,
                stateHash,
                encoded,
                createdSequence);

            // The checkpoint was already validated at the safe boundary. Candidate
            // verification therefore streams SQLite metadata, length and SHA only;
            // decoding it here would retain a second authority graph during save.
            Report(progress, 70, SlotPersistencePhase.VerifyingCandidate);
            ValidateWrittenCandidate(
                paths.TemporaryPath,
                checkpoint,
                stateHash,
                encoded.Header.ContainerBytes,
                payloadHash,
                createdSequence,
                encoded);

            Report(progress, 86, SlotPersistencePhase.ReplacingPrimary);
            ReplacePrimary(paths);
            Report(progress, 94, SlotPersistencePhase.WritingManifest);
            WriteManifest(paths, slotId, checkpoint.SchemaVersion, createdSequence, summary);
            Report(progress, 100, SlotPersistencePhase.Complete);

            return new SlotSaveResult(
                slotId,
                paths.PrimaryPath,
                WorldSchema,
                checkpoint.SchemaVersion,
                createdSequence,
                stateHash,
                encoded.ContainerBytes,
                encoded.JsonBytes,
                WorldCheckpointSectionStore.FormatVersion,
                encoded.Sections.Count + 1);
        }
        finally
        {
            encoded?.Dispose();
        }
    }

    private static void DeleteStalePayloadFiles(string slotDirectory)
    {
        foreach (var pattern in new[]
        {
            ".authority-*.cmip.tmp",
            ".authority-read-*.json.tmp",
        })
        {
            foreach (var path in Directory.EnumerateFiles(
                slotDirectory,
                pattern,
                SearchOption.TopDirectoryOnly))
            {
                TryDeleteTemporaryPayload(path);
            }
        }
    }

    private static void TryDeleteTemporaryPayload(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A later save removes stale payload files while holding the slot lock.
        }
    }

    private static void Report(
        Action<SlotPersistenceProgress>? progress,
        int percent,
        SlotPersistencePhase phase,
        SlotCandidateKind? candidateKind = null) =>
        progress?.Invoke(new SlotPersistenceProgress(
            Math.Clamp(percent, 0, 100),
            phase,
            candidateKind));

    private static void TryInspectCandidate(
        string path,
        SlotCandidateKind kind,
        List<StoredCandidate> candidates,
        List<SlotCandidateFailure> failures)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            candidates.Add(InspectCandidate(path, kind));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            var status = ClassifyFailure(exception);
            failures.Add(new SlotCandidateFailure(kind, path, status, exception.Message));
        }
    }

    private static void TryInspectCandidateHeader(
        string path,
        SlotCandidateKind kind,
        List<SlotCandidateHeader> candidates,
        List<SlotCandidateFailure> failures)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            using var database = WindowsSqlite.Open(path, readOnly: true);
            var userVersion = checked((int)database.QueryScalarInt64("PRAGMA user_version;"));
            if (userVersion != WorldSchema)
            {
                throw new IncompatibleWorldSchemaException(
                    $"지원하지 않는 SQLite world schema {userVersion}입니다.");
            }

            using var select = database.Prepare(SelectHeaderSql);
            if (!select.StepRow())
            {
                throw new InvalidDataException("SQLite 권위 snapshot header가 없습니다.");
            }

            var worldSchema = select.GetInt32(0);
            var checkpointSchema = select.GetInt32(1);
            var seed = DecodeUInt64(select.GetBlob(2), "seed");
            var tick = select.GetInt64(3);
            var stateHash = DecodeUInt64(select.GetBlob(4), "state hash");
            var createdSequence = select.GetInt64(5);
            var eventSequence = select.GetInt64(6);
            var chronicleSequence = select.GetInt64(7);
            if (select.StepRow())
            {
                throw new InvalidDataException("SQLite 권위 snapshot header가 둘 이상입니다.");
            }

            if (worldSchema != WorldSchema)
            {
                throw new IncompatibleWorldSchemaException(
                    $"snapshot world schema {worldSchema}가 DB schema와 다릅니다.");
            }
            if (!IsSupportedCheckpointSchema(checkpointSchema))
            {
                throw new IncompatibleCheckpointSchemaException(
                    $"지원 checkpoint schema 25~{WorldCheckpoint.CurrentSchemaVersion}, 저장 {checkpointSchema}.");
            }
            if (createdSequence < 1 || tick < 0 || eventSequence < 0 || chronicleSequence < 0)
            {
                throw new InvalidDataException("SQLite snapshot header sequence가 잘못되었습니다.");
            }

            candidates.Add(new SlotCandidateHeader(
                kind,
                path,
                worldSchema,
                checkpointSchema,
                seed,
                tick,
                stateHash,
                createdSequence,
                WorldCheckpointSectionStore.TryReadLayout(database)));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            failures.Add(new SlotCandidateFailure(
                kind,
                path,
                ClassifyFailure(exception),
                exception.Message));
        }
    }

    private static SlotLoadStatus ClassifyFailure(Exception exception) => exception switch
    {
        IncompatibleWorldSchemaException => SlotLoadStatus.IncompatibleWorldSchema,
        IncompatibleCheckpointSchemaException => SlotLoadStatus.IncompatibleCheckpointSchema,
        _ => SlotLoadStatus.NoValidCandidate,
    };

    private static bool IsSupportedCheckpointSchema(int schema) =>
        schema is >= 25 and <= WorldCheckpoint.CurrentSchemaVersion;

    private static long GetMaximumKnownSequence(SlotPaths paths)
    {
        var maximum = ReadManifestSequence(paths.ManifestPath);
        maximum = Math.Max(maximum, ReadCandidateSequence(paths.PrimaryPath));
        maximum = Math.Max(maximum, ReadCandidateSequence(paths.TemporaryPath));
        maximum = Math.Max(maximum, ReadCandidateSequence(paths.BackupPath));
        return maximum;
    }

    private static long ReadManifestSequence(string path)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<SlotManifest>(File.ReadAllBytes(path));
            return manifest is { WorldSchema: WorldSchema, CreatedSequence: > 0 }
                ? manifest.CreatedSequence
                : 0;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static long ReadCandidateSequence(string path)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        try
        {
            using var database = WindowsSqlite.Open(path, readOnly: true);
            if (database.QueryScalarInt64("PRAGMA user_version;") != WorldSchema)
            {
                return 0;
            }

            var sequence = database.QueryScalarInt64(
                "SELECT created_sequence FROM authority_snapshot WHERE id=1;");
            return sequence > 0 ? sequence : 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return 0;
        }
    }

    private static void ReplacePrimary(SlotPaths paths)
    {
        if (!File.Exists(paths.PrimaryPath))
        {
            File.Move(paths.TemporaryPath, paths.PrimaryPath);
            return;
        }

        var backupStagingPath = paths.BackupPath + ".next";
        File.Copy(paths.PrimaryPath, backupStagingPath, overwrite: true);
        using (var backup = new FileStream(
            backupStagingPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1,
            FileOptions.WriteThrough))
        {
            backup.Flush(flushToDisk: true);
        }

        File.Replace(paths.TemporaryPath, paths.PrimaryPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        File.Move(backupStagingPath, paths.BackupPath, overwrite: true);
    }

    private static void WriteManifest(
        SlotPaths paths,
        string slotId,
        int checkpointSchema,
        long createdSequence,
        SlotManifestSummary? summary)
    {
        var manifest = new SlotManifest(
            ManifestVersion,
            slotId,
            WorldSchema,
            checkpointSchema,
            createdSequence,
            DateTimeOffset.UtcNow,
            DatabaseFileName,
            summary);
        var temporaryPath = paths.ManifestPath + ".tmp";
        var data = JsonSerializer.SerializeToUtf8Bytes(manifest, ManifestJsonOptions);
        using (var stream = new FileStream(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 16_384,
            FileOptions.WriteThrough))
        {
            stream.Write(data);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(paths.ManifestPath))
        {
            File.Replace(temporaryPath, paths.ManifestPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temporaryPath, paths.ManifestPath);
        }
    }

    private static byte[] EncodeUInt64(ulong value)
    {
        var bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return bytes;
    }

    private static void ValidateSummary(
        WorldCheckpoint checkpoint,
        ulong stateHash,
        SlotManifestSummary? summary)
    {
        if (summary is null)
        {
            return;
        }

        if (summary.Tick != checkpoint.Tick
            || summary.Population != checkpoint.Agents.Length
            || summary.Seed != checkpoint.Seed
            || summary.StateHash != stateHash
            || summary.LeaderAgentId < 0
            || summary.LeaderAgentId >= checkpoint.Agents.Length
            || summary.OrganizationCount < 0)
        {
            throw new InvalidDataException(
                "슬롯 표시 요약이 같은 safe-boundary 권위 checkpoint와 일치하지 않습니다.");
        }
    }

    private static ulong DecodeUInt64(byte[] value, string field)
    {
        if (value.Length != sizeof(ulong))
        {
            throw new InvalidDataException($"SQLite {field} 길이가 잘못되었습니다.");
        }

        return BinaryPrimitives.ReadUInt64LittleEndian(value);
    }

    private static void ValidateSlotId(string slotId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        if (slotId.Length > 64
            || slotId.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new ArgumentException("슬롯 ID는 64자 이하 문자·숫자·하이픈·밑줄만 사용할 수 있습니다.", nameof(slotId));
        }
    }

    private static int CandidatePriority(SlotCandidateKind kind) => kind switch
    {
        SlotCandidateKind.Primary => 0,
        SlotCandidateKind.Temporary => 1,
        SlotCandidateKind.Backup => 2,
        _ => 3,
    };

    private static string FormatCandidate(SlotCandidateKind kind) => kind switch
    {
        SlotCandidateKind.Primary => "primary",
        SlotCandidateKind.Temporary => "temporary",
        SlotCandidateKind.Backup => "backup",
        _ => kind.ToString(),
    };

    private sealed record Candidate(
        SlotCandidateKind Kind,
        string Path,
        WorldCheckpoint Checkpoint,
        World World,
        long CreatedSequence,
        ulong StateHash);

    private sealed record StoredCandidate(
        SlotCandidateKind Kind,
        string Path,
        int CheckpointSchema,
        ulong Seed,
        long Tick,
        ulong StateHash,
        long CreatedSequence,
        long EventSequence,
        long ChronicleSequence,
        int PayloadBytes,
        byte[] PayloadHash,
        StoredCheckpointLayout? Layout);

    private sealed record SlotCandidateHeader(
        SlotCandidateKind Kind,
        string Path,
        int WorldSchema,
        int CheckpointSchema,
        ulong Seed,
        long Tick,
        ulong StateHash,
        long CreatedSequence,
        StoredCheckpointLayout? Layout);

    private sealed class IncompatibleWorldSchemaException : IOException
    {
        public IncompatibleWorldSchemaException(string message) : base(message)
        {
        }
    }

    private sealed class IncompatibleCheckpointSchemaException : IOException
    {
        public IncompatibleCheckpointSchemaException(string message) : base(message)
        {
        }
    }
}
