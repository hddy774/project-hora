using System.Buffers;
using System.Security.Cryptography;
using Chronicles.Simulation;

namespace Chronicles.Persistence;

/// <summary>
/// 슬롯 후보 metadata 검사, 순차 decode, hash 검증과 복구 선택 작업집합이다.
/// 저장 쓰기·manifest 갱신과 분리해 불러오기 장애를 독립적으로 진단한다.
/// </summary>
public static partial class SlotSaveStore
{
    public static SlotLoadResult Load(
        string rootDirectory,
        string slotId,
        Action<SlotPersistenceProgress>? progress = null)
    {
        Report(progress, 5, SlotPersistencePhase.InspectingCandidates);
        var paths = GetPaths(rootDirectory, slotId);
        if (!Directory.Exists(paths.SlotDirectory))
        {
            return new SlotLoadResult(
                SlotLoadStatus.SlotNotFound,
                $"저장 슬롯을 찾을 수 없습니다: {slotId}");
        }

        var failures = new List<SlotCandidateFailure>();
        var candidates = new List<StoredCandidate>();
        TryInspectCandidate(paths.PrimaryPath, SlotCandidateKind.Primary, candidates, failures);
        TryInspectCandidate(paths.TemporaryPath, SlotCandidateKind.Temporary, candidates, failures);
        TryInspectCandidate(paths.BackupPath, SlotCandidateKind.Backup, candidates, failures);

        if (candidates.Count == 0)
        {
            var hasAnyCandidate = File.Exists(paths.PrimaryPath)
                || File.Exists(paths.TemporaryPath)
                || File.Exists(paths.BackupPath);
            var failureStatus = failures.Select(failure => failure.Status).FirstOrDefault(status =>
                status is SlotLoadStatus.IncompatibleWorldSchema
                    or SlotLoadStatus.IncompatibleCheckpointSchema);
            return new SlotLoadResult(
                !hasAnyCandidate
                    ? SlotLoadStatus.SlotNotFound
                    : failureStatus is SlotLoadStatus.IncompatibleWorldSchema
                        or SlotLoadStatus.IncompatibleCheckpointSchema
                        ? failureStatus
                        : SlotLoadStatus.NoValidCandidate,
                hasAnyCandidate
                    ? "무결성과 권위 상태 검증을 통과한 SQLite 저장 후보가 없습니다."
                    : $"저장 슬롯을 찾을 수 없습니다: {slotId}",
                Failures: failures);
        }

        var orderedCandidates = candidates
            .OrderByDescending(candidate => candidate.CreatedSequence)
            .ThenBy(candidate => CandidatePriority(candidate.Kind))
            .ToArray();
        Report(progress, 25, SlotPersistencePhase.SelectingCandidate);
        for (var candidateIndex = 0; candidateIndex < orderedCandidates.Length; candidateIndex++)
        {
            var stored = orderedCandidates[candidateIndex];
            try
            {
                var selected = RestoreCandidate(
                    stored,
                    progress,
                    candidateIndex,
                    orderedCandidates.Length);
                Report(progress, 90, SlotPersistencePhase.Complete, selected.Kind);
                return new SlotLoadResult(
                    SlotLoadStatus.Success,
                    $"SQLite {FormatCandidate(selected.Kind)} 후보를 복원했습니다.",
                    selected.Checkpoint,
                    selected.Kind,
                    selected.Path,
                    WorldSchema,
                    selected.Checkpoint.SchemaVersion,
                    selected.CreatedSequence,
                    selected.StateHash,
                    failures,
                    selected.World);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failures.Add(new SlotCandidateFailure(
                    stored.Kind,
                    stored.Path,
                    ClassifyFailure(exception),
                    exception.Message));
            }
        }

        return new SlotLoadResult(
            SlotLoadStatus.NoValidCandidate,
            "SQLite 후보의 canonical World 복원과 state hash 검증에 실패했습니다.",
            Failures: failures);
    }

    /// <summary>
    /// File-picker preflight. Legacy .cmip paths are classified without opening,
    /// reading, moving, deleting or rewriting the source file.
    /// </summary>
    public static SlotLoadResult InspectSelectedSave(string selectedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        var fullPath = Path.GetFullPath(selectedPath);
        if (!string.Equals(Path.GetFileName(fullPath), DatabaseFileName, StringComparison.OrdinalIgnoreCase))
        {
            return new SlotLoadResult(
                SlotLoadStatus.IncompatibleLegacySave,
                "호환되지 않는 이전 저장입니다. 원본 파일은 변경되지 않았습니다.");
        }

        var slotDirectory = Path.GetDirectoryName(fullPath);
        var rootDirectory = slotDirectory is null ? null : Path.GetDirectoryName(slotDirectory);
        var slotId = slotDirectory is null ? null : Path.GetFileName(slotDirectory);
        if (string.IsNullOrWhiteSpace(rootDirectory) || string.IsNullOrWhiteSpace(slotId))
        {
            return new SlotLoadResult(
                SlotLoadStatus.NoValidCandidate,
                "선택한 SQLite 저장의 슬롯 경로를 확인할 수 없습니다.");
        }

        return Load(rootDirectory, slotId);
    }

    private static byte[] WriteDatabase(
        string path,
        WorldCheckpoint checkpoint,
        ulong stateHash,
        EncodedCheckpointLayout encoded,
        long createdSequence)
    {
        // A previous crash candidate has already contributed to the next sequence.
        // The new transaction now owns the explicit temporary path.
        using (var truncate = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1,
            FileOptions.WriteThrough))
        {
            truncate.Flush(flushToDisk: true);
        }

        using var database = WindowsSqlite.Open(path, readOnly: false);
        database.Execute("PRAGMA journal_mode=DELETE;");
        database.Execute("PRAGMA synchronous=FULL;");
        database.Execute("PRAGMA foreign_keys=ON;");
        database.Execute("BEGIN IMMEDIATE;");
        try
        {
            database.Execute("DROP TABLE IF EXISTS authority_snapshot;");
            database.Execute(CreateSchemaSql);
            database.Execute($"PRAGMA user_version={WorldSchema};");
            using (var insert = database.Prepare(InsertSql))
            {
                insert.BindInt32(1, WorldSchema);
                insert.BindInt32(2, checkpoint.SchemaVersion);
                insert.BindBlob(3, EncodeUInt64(checkpoint.Seed));
                insert.BindInt64(4, checkpoint.Tick);
                insert.BindBlob(5, EncodeUInt64(stateHash));
                insert.BindBlob(6, new byte[SHA256.HashSizeInBytes]);
                insert.BindInt64(7, createdSequence);
                insert.BindInt64(8, checkpoint.EventSequence);
                insert.BindInt64(9, checkpoint.ChronicleSequence);
                insert.BindInt32(10, encoded.Header.ContainerBytes);
                insert.Execute();
            }

            byte[] payloadHash;
            using (var source = new FileStream(
                encoded.Header.Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 256 * 1024,
                FileOptions.SequentialScan))
            using (var destination = database.OpenBlob(
                "authority_snapshot",
                "payload",
                rowId: 1,
                writable: true))
            {
                if (source.Length != encoded.Header.ContainerBytes
                    || destination.Length != encoded.Header.ContainerBytes)
                {
                    throw new InvalidDataException(
                        "임시 CMIP container와 SQLite zeroblob 길이가 일치하지 않습니다.");
                }

                payloadHash = CopyAndHash(source, destination, encoded.Header.ContainerBytes);
            }

            using (var updateHash = database.Prepare(UpdatePayloadHashSql))
            {
                updateHash.BindBlob(1, payloadHash);
                updateHash.Execute();
            }

            WorldCheckpointSectionStore.Write(database, encoded);

            database.Execute("COMMIT;");
            return payloadHash;
        }
        catch
        {
            try
            {
                database.Execute("ROLLBACK;");
            }
            catch (Exception)
            {
                // Preserve the original write failure.
            }

            throw;
        }
    }

    private static void ValidateWrittenCandidate(
        string path,
        WorldCheckpoint checkpoint,
        ulong stateHash,
        int payloadBytes,
        byte[] payloadHash,
        long createdSequence,
        EncodedCheckpointLayout encoded)
    {
        using var database = WindowsSqlite.Open(path, readOnly: true);
        if (database.QueryScalarInt64("PRAGMA user_version;") != WorldSchema)
        {
            throw new InvalidDataException("임시 SQLite 후보의 world schema가 잘못되었습니다.");
        }

        var integrity = database.QueryScalarText("PRAGMA integrity_check;");
        if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"임시 SQLite 후보 무결성 검사 실패: {integrity}");
        }

        var stored = ReadCandidateMetadata(database, path, SlotCandidateKind.Temporary);
        var actualHash = ComputePayloadHash(database, stored.PayloadBytes);
        var layout = stored.Layout
            ?? throw new InvalidDataException("임시 SQLite 후보에 분할 저장 layout이 없습니다.");
        var sections = WorldCheckpointSectionStore.ReadSections(database, layout);
        WorldCheckpointSectionStore.ValidatePayloads(database, sections);
        if (stored.CheckpointSchema != checkpoint.SchemaVersion
            || stored.Seed != checkpoint.Seed
            || stored.Tick != checkpoint.Tick
            || stored.StateHash != stateHash
            || stored.CreatedSequence != createdSequence
            || stored.EventSequence != checkpoint.EventSequence
            || stored.ChronicleSequence != checkpoint.ChronicleSequence
            || stored.PayloadBytes != payloadBytes
            || !CryptographicOperations.FixedTimeEquals(stored.PayloadHash, payloadHash)
            || !CryptographicOperations.FixedTimeEquals(actualHash, payloadHash)
            || layout.AgentCount != checkpoint.Agents.Length
            || layout.EventCount != checkpoint.Events.Length
            || layout.WorldChronicleCount != (checkpoint.WorldChronicle?.Length ?? -1)
            || layout.SectionCount != encoded.Sections.Count
            || layout.JsonBytes != encoded.JsonBytes
            || layout.ContainerBytes != encoded.ContainerBytes)
        {
            throw new InvalidDataException("임시 SQLite 후보가 검증된 권위 checkpoint와 다릅니다.");
        }
    }

    private static StoredCandidate InspectCandidate(string path, SlotCandidateKind kind)
    {
        using var database = WindowsSqlite.Open(path, readOnly: true);
        var userVersion = database.QueryScalarInt64("PRAGMA user_version;");
        if (userVersion != WorldSchema)
        {
            throw new IncompatibleWorldSchemaException(
                $"지원하지 않는 SQLite world schema {userVersion}입니다.");
        }

        var integrity = database.QueryScalarText("PRAGMA integrity_check;");
        if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"SQLite 무결성 검사 실패: {integrity}");
        }

        return ReadCandidateMetadata(database, path, kind);
    }

    private static Candidate RestoreCandidate(
        StoredCandidate stored,
        Action<SlotPersistenceProgress>? progress,
        int candidateIndex,
        int candidateCount)
    {
        var candidateBand = Math.Max(1, 60 / Math.Max(1, candidateCount));
        var bandStart = 25 + (candidateBand * candidateIndex);
        Report(
            progress,
            Math.Min(80, bandStart + 5),
            SlotPersistencePhase.DecodingCandidate,
            stored.Kind);
        var checkpoint = DecodePayload(stored);
        if (checkpoint.SchemaVersion != stored.CheckpointSchema
            || checkpoint.Seed != stored.Seed
            || checkpoint.Tick != stored.Tick
            || checkpoint.EventSequence != stored.EventSequence
            || checkpoint.ChronicleSequence != stored.ChronicleSequence)
        {
            throw new InvalidDataException("SQLite 메타데이터와 권위 checkpoint가 일치하지 않습니다.");
        }

        Report(
            progress,
            Math.Min(85, bandStart + candidateBand - 3),
            SlotPersistencePhase.ValidatingWorld,
            stored.Kind);
        var restored = World.Restore(checkpoint);
        var actualStateHash = restored.ComputeStateHashForCheckpointSchema(
            stored.CheckpointSchema);
        if (actualStateHash != stored.StateHash)
        {
            throw new InvalidDataException("복원된 권위 세계의 state hash가 일치하지 않습니다.");
        }

        return new Candidate(
            stored.Kind,
            stored.Path,
            checkpoint,
            restored,
            stored.CreatedSequence,
            stored.StateHash);
    }

    private static StoredCandidate ReadCandidateMetadata(
        WindowsSqlite database,
        string path,
        SlotCandidateKind kind)
    {
        using var select = database.Prepare(SelectSql);
        if (!select.StepRow())
        {
            throw new InvalidDataException("SQLite 권위 snapshot이 없습니다.");
        }

        var worldSchema = select.GetInt32(0);
        var checkpointSchema = select.GetInt32(1);
        var seed = DecodeUInt64(select.GetBlob(2), "seed");
        var tick = select.GetInt64(3);
        var stateHash = DecodeUInt64(select.GetBlob(4), "state hash");
        var payloadHash = select.GetBlob(5);
        var createdSequence = select.GetInt64(6);
        var eventSequence = select.GetInt64(7);
        var chronicleSequence = select.GetInt64(8);
        var payloadBytes = checked((int)select.GetInt64(9));
        if (select.StepRow())
        {
            throw new InvalidDataException("SQLite 권위 snapshot이 둘 이상입니다.");
        }

        if (worldSchema != WorldSchema)
        {
            throw new IncompatibleWorldSchemaException(
                $"snapshot world schema {worldSchema}가 DB schema와 다릅니다.");
        }
        if (!IsSupportedCheckpointSchema(checkpointSchema))
        {
            throw new IncompatibleCheckpointSchemaException(
                $"schema 7~24 이전은 지원하지 않습니다. 지원 checkpoint schema 25~{WorldCheckpoint.CurrentSchemaVersion}, 저장 {checkpointSchema}.");
        }
        if (createdSequence < 1 || tick < 0 || eventSequence < 0 || chronicleSequence < 0)
        {
            throw new InvalidDataException("SQLite 저장 sequence가 잘못되었습니다.");
        }
        if (payloadBytes < MagicContainerHeaderBytes
            || payloadBytes > WorldSaveCodec.CompressedPayloadSizeLimitBytes + MagicContainerHeaderBytes)
        {
            throw new InvalidDataException(
                $"SQLite payload 길이가 허용 범위를 벗어났습니다. ({payloadBytes:N0} bytes)");
        }
        if (payloadHash.Length != SHA256.HashSizeInBytes)
        {
            throw new InvalidDataException("SQLite payload SHA-256 길이가 잘못되었습니다.");
        }

        var layout = WorldCheckpointSectionStore.TryReadLayout(database);
        return new StoredCandidate(
            kind,
            path,
            checkpointSchema,
            seed,
            tick,
            stateHash,
            createdSequence,
            eventSequence,
            chronicleSequence,
            payloadBytes,
            payloadHash,
            layout);
    }

    private static WorldCheckpoint DecodePayload(StoredCandidate stored)
    {
        using var database = WindowsSqlite.Open(stored.Path, readOnly: true);
        WorldCheckpoint header;
        using (var blob = database.OpenBlob(
            "authority_snapshot",
            "payload",
            rowId: 1,
            writable: false))
        {
            if (blob.Length != stored.PayloadBytes)
            {
                throw new InvalidDataException("SQLite payload 길이가 metadata와 다릅니다.");
            }
            header = WorldSaveCodec.Decode(
                blob,
                stored.PayloadBytes,
                Path.GetDirectoryName(stored.Path),
                stored.PayloadHash);
        }

        return stored.Layout is { } layout
            ? WorldCheckpointSectionStore.Decode(
                database,
                header,
                layout,
                Path.GetDirectoryName(stored.Path)!)
            : header;
    }

    private static byte[] ComputePayloadHash(WindowsSqlite database, int expectedBytes)
    {
        using var blob = database.OpenBlob(
            "authority_snapshot",
            "payload",
            rowId: 1,
            writable: false);
        if (blob.Length != expectedBytes)
        {
            throw new InvalidDataException("SQLite payload 길이가 metadata와 다릅니다.");
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(IncrementalBlobChunkBytes);
        try
        {
            var total = 0;
            while (total < expectedBytes)
            {
                var count = blob.Read(
                    buffer,
                    0,
                    Math.Min(IncrementalBlobChunkBytes, expectedBytes - total));
                if (count == 0)
                {
                    throw new EndOfStreamException("SQLite payload가 metadata 길이보다 짧습니다.");
                }

                hash.AppendData(buffer, 0, count);
                total += count;
            }

            return hash.GetHashAndReset();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static byte[] CopyAndHash(Stream source, Stream destination, int expectedBytes)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(IncrementalBlobChunkBytes);
        try
        {
            var total = 0;
            while (total < expectedBytes)
            {
                var count = source.Read(
                    buffer,
                    0,
                    Math.Min(IncrementalBlobChunkBytes, expectedBytes - total));
                if (count == 0)
                {
                    throw new EndOfStreamException("임시 CMIP container가 예상 길이보다 짧습니다.");
                }

                destination.Write(buffer, 0, count);
                hash.AppendData(buffer, 0, count);
                total += count;
            }

            if (source.ReadByte() != -1 || destination.Position != expectedBytes)
            {
                throw new InvalidDataException("임시 CMIP container 또는 SQLite BLOB 길이가 잘못되었습니다.");
            }

            return hash.GetHashAndReset();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

}
