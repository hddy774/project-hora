using System.Buffers;
using System.Security.Cryptography;
using Chronicles.Simulation;

namespace Chronicles.Persistence;

internal enum WorldCheckpointSectionKind : byte
{
    Agents = 1,
    Events = 2,
    WorldChronicle = 3,
    RankingHistory = 4,
    CenturyChronicle = 5,
    Relations = 6,
    Reincarnation = 7,
    Causal = 8,
    Atlas = 9,
    Organizations = 10,
    Content = 11,
    Knowledge = 12,
    Laws = 13,
    Generation = 14,
    DecisionRankings = 15,
}

internal sealed record EncodedCheckpointSection(
    long Id,
    WorldCheckpointSectionKind Kind,
    int Ordinal,
    int ItemOffset,
    int ItemCount,
    WorldSaveCodec.StreamingEncodeResult Encoding);

internal sealed class EncodedCheckpointLayout : IDisposable
{
    public EncodedCheckpointLayout(
        int formatVersion,
        WorldSaveCodec.StreamingEncodeResult header,
        IReadOnlyList<EncodedCheckpointSection> sections,
        int agentCount,
        int eventCount,
        int worldChronicleCount)
    {
        FormatVersion = formatVersion;
        Header = header;
        Sections = sections;
        AgentCount = agentCount;
        EventCount = eventCount;
        WorldChronicleCount = worldChronicleCount;
        JsonBytes = header.JsonBytes + sections.Sum(section => section.Encoding.JsonBytes);
        ContainerBytes = header.ContainerBytes + sections.Sum(section => (long)section.Encoding.ContainerBytes);
    }

    public int FormatVersion { get; }
    public WorldSaveCodec.StreamingEncodeResult Header { get; }
    public IReadOnlyList<EncodedCheckpointSection> Sections { get; }
    public int AgentCount { get; }
    public int EventCount { get; }
    public int WorldChronicleCount { get; }
    public long JsonBytes { get; }
    public long ContainerBytes { get; }

    public void Dispose()
    {
        WorldCheckpointSectionStore.TryDelete(Header.Path);
        foreach (var section in Sections)
        {
            WorldCheckpointSectionStore.TryDelete(section.Encoding.Path);
        }
    }
}

internal sealed record StoredCheckpointLayout(
    int FormatVersion,
    int AgentCount,
    int EventCount,
    int WorldChronicleCount,
    int SectionCount,
    long JsonBytes,
    long ContainerBytes);

internal sealed record StoredCheckpointSection(
    long Id,
    WorldCheckpointSectionKind Kind,
    int Ordinal,
    int ItemOffset,
    int ItemCount,
    long JsonBytes,
    int PayloadBytes,
    byte[] PayloadHash);

/// <summary>
/// Splits the large authority graph into independently compressed, checksummed SQLite BLOBs.
/// A slot remains one atomic database, while no serializer or loader handles one unbounded JSON.
/// </summary>
internal static class WorldCheckpointSectionStore
{
    public const int FormatVersion = 2;
    private const int MinimumSupportedFormatVersion = 1;
    public const int AgentChunkSize = 512;
    private const int EventChunkSize = 4_096;
    private const int ChronicleChunkSize = 4_096;
    private const int IoBufferBytes = 256 * 1024;
    private const string CreateLayoutSql = """
        CREATE TABLE authority_layout (
            id INTEGER NOT NULL PRIMARY KEY CHECK (id = 1),
            format_version INTEGER NOT NULL,
            agent_count INTEGER NOT NULL,
            event_count INTEGER NOT NULL,
            world_chronicle_count INTEGER NOT NULL,
            section_count INTEGER NOT NULL,
            json_bytes INTEGER NOT NULL,
            container_bytes INTEGER NOT NULL
        ) STRICT;
        """;
    private const string CreateSectionSql = """
        CREATE TABLE authority_section (
            id INTEGER NOT NULL PRIMARY KEY,
            kind INTEGER NOT NULL,
            ordinal INTEGER NOT NULL,
            item_offset INTEGER NOT NULL,
            item_count INTEGER NOT NULL,
            json_bytes INTEGER NOT NULL,
            payload_bytes INTEGER NOT NULL,
            payload_sha256 BLOB NOT NULL CHECK (length(payload_sha256) = 32),
            payload BLOB NOT NULL,
            UNIQUE(kind, ordinal)
        ) STRICT;
        """;
    private const string SelectLayoutSql = """
        SELECT format_version, agent_count, event_count, world_chronicle_count,
               section_count, json_bytes, container_bytes
          FROM authority_layout
         WHERE id = 1;
        """;
    private const string SelectSectionsSql = """
        SELECT id, kind, ordinal, item_offset, item_count, json_bytes,
               payload_bytes, payload_sha256, length(payload)
          FROM authority_section
         ORDER BY id;
        """;

    public static EncodedCheckpointLayout Encode(WorldCheckpoint checkpoint, string scratchDirectory)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        Directory.CreateDirectory(scratchDirectory);
        var sections = new List<EncodedCheckpointSection>();
        var headerPath = CreateScratchPath(scratchDirectory, "header");
        try
        {
            var header = checkpoint with
            {
                Agents = [],
                Events = [],
                WorldChronicle = checkpoint.WorldChronicle is null ? null : [],
                RankingHistory = null,
                CenturyChronicle = null,
                Relations = null,
                Reincarnation = null,
                Causal = null,
                Atlas = null,
                Organizations = null,
                Content = null,
                Knowledge = null,
                Laws = null,
                Generation = null,
                DecisionRankings = null,
            };
            var headerEncoding = WorldSaveCodec.EncodeValueStreamingToFile(
                header,
                headerPath,
                WorldSaveCodec.MaximumSectionJsonBytes,
                "권위 header");
            long nextId = 1;
            EncodeChunks(
                checkpoint.Agents,
                AgentChunkSize,
                WorldCheckpointSectionKind.Agents,
                scratchDirectory,
                sections,
                ref nextId);
            EncodeChunks(
                checkpoint.Events,
                EventChunkSize,
                WorldCheckpointSectionKind.Events,
                scratchDirectory,
                sections,
                ref nextId);
            if (checkpoint.WorldChronicle is { } chronicle)
            {
                EncodeChunks(
                    chronicle,
                    ChronicleChunkSize,
                    WorldCheckpointSectionKind.WorldChronicle,
                    scratchDirectory,
                    sections,
                    ref nextId);
            }

            EncodeSingleton(checkpoint.RankingHistory, WorldCheckpointSectionKind.RankingHistory, scratchDirectory, sections, ref nextId);
            EncodeSingleton(checkpoint.CenturyChronicle, WorldCheckpointSectionKind.CenturyChronicle, scratchDirectory, sections, ref nextId);
            EncodeSingleton(checkpoint.Relations, WorldCheckpointSectionKind.Relations, scratchDirectory, sections, ref nextId);
            EncodeSingleton(checkpoint.Reincarnation, WorldCheckpointSectionKind.Reincarnation, scratchDirectory, sections, ref nextId);
            EncodeSingleton(checkpoint.Causal, WorldCheckpointSectionKind.Causal, scratchDirectory, sections, ref nextId);
            if (checkpoint.Atlas is { } atlas)
            {
                EncodeValue(atlas, WorldCheckpointSectionKind.Atlas, scratchDirectory, sections, ref nextId);
            }
            EncodeSingleton(checkpoint.Organizations, WorldCheckpointSectionKind.Organizations, scratchDirectory, sections, ref nextId);
            EncodeSingleton(checkpoint.Content, WorldCheckpointSectionKind.Content, scratchDirectory, sections, ref nextId);
            EncodeSingleton(checkpoint.Knowledge, WorldCheckpointSectionKind.Knowledge, scratchDirectory, sections, ref nextId);
            EncodeSingleton(checkpoint.Laws, WorldCheckpointSectionKind.Laws, scratchDirectory, sections, ref nextId);
            EncodeSingleton(checkpoint.Generation, WorldCheckpointSectionKind.Generation, scratchDirectory, sections, ref nextId);
            EncodeSingleton(checkpoint.DecisionRankings, WorldCheckpointSectionKind.DecisionRankings, scratchDirectory, sections, ref nextId);

            return new EncodedCheckpointLayout(
                FormatVersion,
                headerEncoding,
                sections,
                checkpoint.Agents.Length,
                checkpoint.Events.Length,
                checkpoint.WorldChronicle?.Length ?? -1);
        }
        catch
        {
            TryDelete(headerPath);
            foreach (var section in sections)
            {
                TryDelete(section.Encoding.Path);
            }
            throw;
        }
    }

    public static void Write(
        WindowsSqlite database,
        EncodedCheckpointLayout layout)
    {
        database.Execute("DROP TABLE IF EXISTS authority_section;");
        database.Execute("DROP TABLE IF EXISTS authority_layout;");
        database.Execute(CreateLayoutSql);
        database.Execute(CreateSectionSql);
        using (var insertLayout = database.Prepare("""
            INSERT INTO authority_layout (
                id, format_version, agent_count, event_count, world_chronicle_count,
                section_count, json_bytes, container_bytes)
            VALUES (1, ?1, ?2, ?3, ?4, ?5, ?6, ?7);
            """))
        {
            insertLayout.BindInt32(1, layout.FormatVersion);
            insertLayout.BindInt32(2, layout.AgentCount);
            insertLayout.BindInt32(3, layout.EventCount);
            insertLayout.BindInt32(4, layout.WorldChronicleCount);
            insertLayout.BindInt32(5, layout.Sections.Count);
            insertLayout.BindInt64(6, layout.JsonBytes);
            insertLayout.BindInt64(7, layout.ContainerBytes);
            insertLayout.Execute();
        }

        foreach (var section in layout.Sections)
        {
            WriteSection(database, section);
        }
    }

    public static StoredCheckpointLayout? TryReadLayout(WindowsSqlite database)
    {
        if (database.QueryScalarInt64(
                "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='authority_layout';") == 0)
        {
            return null;
        }

        using var select = database.Prepare(SelectLayoutSql);
        if (!select.StepRow())
        {
            throw new InvalidDataException("분할 저장 layout이 없습니다.");
        }
        var layout = new StoredCheckpointLayout(
            select.GetInt32(0),
            select.GetInt32(1),
            select.GetInt32(2),
            select.GetInt32(3),
            select.GetInt32(4),
            select.GetInt64(5),
            select.GetInt64(6));
        if (select.StepRow())
        {
            throw new InvalidDataException("분할 저장 layout이 둘 이상입니다.");
        }
        ValidateLayout(layout);
        return layout;
    }

    public static IReadOnlyList<StoredCheckpointSection> ReadSections(
        WindowsSqlite database,
        StoredCheckpointLayout layout)
    {
        var sections = new List<StoredCheckpointSection>(layout.SectionCount);
        using var select = database.Prepare(SelectSectionsSql);
        while (select.StepRow())
        {
            var payloadBytes = select.GetInt32(6);
            var actualPayloadBytes = checked((int)select.GetInt64(8));
            var section = new StoredCheckpointSection(
                select.GetInt64(0),
                ParseKind(select.GetInt32(1)),
                select.GetInt32(2),
                select.GetInt32(3),
                select.GetInt32(4),
                select.GetInt64(5),
                payloadBytes,
                select.GetBlob(7));
            if (payloadBytes != actualPayloadBytes
                || payloadBytes < 44
                || payloadBytes > WorldSaveCodec.CompressedPayloadSizeLimitBytes + 44
                || section.PayloadHash.Length != SHA256.HashSizeInBytes
                || section.JsonBytes <= 0
                || section.JsonBytes > MaximumJsonBytes(section.Kind))
            {
                throw new InvalidDataException($"분할 저장 section {section.Id} metadata가 잘못되었습니다.");
            }
            sections.Add(section);
        }
        if (sections.Count != layout.SectionCount)
        {
            throw new InvalidDataException(
                $"분할 저장 section 수가 다릅니다. ({sections.Count:N0}/{layout.SectionCount:N0})");
        }
        ValidateCoverage(layout, sections);
        return sections;
    }

    public static void ValidatePayloads(
        WindowsSqlite database,
        IReadOnlyList<StoredCheckpointSection> sections)
    {
        foreach (var section in sections)
        {
            using var blob = database.OpenBlob(
                "authority_section",
                "payload",
                section.Id,
                writable: false);
            ValidateBlobHash(blob, section.PayloadBytes, section.PayloadHash, $"section {section.Id}");
        }
    }

    public static WorldCheckpoint Decode(
        WindowsSqlite database,
        WorldCheckpoint header,
        StoredCheckpointLayout layout,
        string scratchDirectory)
    {
        if (header.Agents.Length != 0
            || header.Events.Length != 0
            || header.WorldChronicle is { Length: > 0 }
            || layout.FormatVersion >= 2 && HasSeparatedPayload(header))
        {
            throw new InvalidDataException("분할 저장 header에 분리 대상 권위 데이터가 포함되어 있습니다.");
        }

        var agents = new AgentCheckpoint[layout.AgentCount];
        var events = new WorldEvent[layout.EventCount];
        var worldChronicle = layout.WorldChronicleCount < 0
            ? null
            : new WorldEvent[layout.WorldChronicleCount];
        var sections = ReadSections(database, layout);
        foreach (var section in sections.Where(section => section.Kind <= WorldCheckpointSectionKind.WorldChronicle))
        {
            using var blob = database.OpenBlob(
                "authority_section",
                "payload",
                section.Id,
                writable: false);
            switch (section.Kind)
            {
                case WorldCheckpointSectionKind.Agents:
                    CopyDecoded(
                        WorldSaveCodec.DecodeValue<AgentCheckpoint[]>(
                            blob,
                            section.PayloadBytes,
                            scratchDirectory,
                            WorldSaveCodec.MaximumSectionJsonBytes,
                            $"인물 section {section.Ordinal}",
                            section.PayloadHash),
                        agents,
                        section);
                    break;
                case WorldCheckpointSectionKind.Events:
                    CopyDecoded(
                        WorldSaveCodec.DecodeValue<WorldEvent[]>(
                            blob,
                            section.PayloadBytes,
                            scratchDirectory,
                            WorldSaveCodec.MaximumSectionJsonBytes,
                            $"세계 이벤트 section {section.Ordinal}",
                            section.PayloadHash),
                        events,
                        section);
                    break;
                case WorldCheckpointSectionKind.WorldChronicle when worldChronicle is not null:
                    CopyDecoded(
                        WorldSaveCodec.DecodeValue<WorldEvent[]>(
                            blob,
                            section.PayloadBytes,
                            scratchDirectory,
                            WorldSaveCodec.MaximumSectionJsonBytes,
                            $"세계 연대기 section {section.Ordinal}",
                            section.PayloadHash),
                        worldChronicle,
                        section);
                    break;
                default:
                    throw new InvalidDataException($"지원하지 않는 분할 저장 section {section.Kind}입니다.");
            }
        }

        var restored = header with
        {
            Agents = agents,
            Events = events,
            WorldChronicle = worldChronicle,
        };
        foreach (var section in sections.Where(section => section.Kind > WorldCheckpointSectionKind.WorldChronicle))
        {
            using var blob = database.OpenBlob("authority_section", "payload", section.Id, writable: false);
            restored = section.Kind switch
            {
                WorldCheckpointSectionKind.RankingHistory => restored with { RankingHistory = DecodeSingleton<RankingHistoryCheckpoint>(blob, section, scratchDirectory) },
                WorldCheckpointSectionKind.CenturyChronicle => restored with { CenturyChronicle = DecodeSingleton<WorldCenturyChronicleCheckpoint>(blob, section, scratchDirectory) },
                WorldCheckpointSectionKind.Relations => restored with { Relations = DecodeSingleton<RelationStoreCheckpoint>(blob, section, scratchDirectory) },
                WorldCheckpointSectionKind.Reincarnation => restored with { Reincarnation = DecodeSingleton<ReincarnationStoreCheckpoint>(blob, section, scratchDirectory) },
                WorldCheckpointSectionKind.Causal => restored with { Causal = DecodeSingleton<AgentCausalStoreCheckpoint>(blob, section, scratchDirectory) },
                WorldCheckpointSectionKind.Atlas => restored with { Atlas = DecodeSingleton<WorldAtlasCheckpoint>(blob, section, scratchDirectory) },
                WorldCheckpointSectionKind.Organizations => restored with { Organizations = DecodeSingleton<OrganizationStoreCheckpoint>(blob, section, scratchDirectory) },
                WorldCheckpointSectionKind.Content => restored with { Content = DecodeSingleton<ContentAuthorityCheckpoint>(blob, section, scratchDirectory) },
                WorldCheckpointSectionKind.Knowledge => restored with { Knowledge = DecodeSingleton<KnowledgeAuthorityCheckpoint>(blob, section, scratchDirectory) },
                WorldCheckpointSectionKind.Laws => restored with { Laws = DecodeSingleton<LawAuthorityCheckpoint>(blob, section, scratchDirectory) },
                WorldCheckpointSectionKind.Generation => restored with { Generation = DecodeSingleton<WorldGenerationCheckpoint>(blob, section, scratchDirectory) },
                WorldCheckpointSectionKind.DecisionRankings => restored with { DecisionRankings = DecodeSingleton<DecisionRankingCheckpoint>(blob, section, scratchDirectory) },
                _ => throw new InvalidDataException($"지원하지 않는 분할 저장 section {section.Kind}입니다."),
            };
        }
        return restored;
    }

    private static void EncodeChunks<T>(
        T[] source,
        int chunkSize,
        WorldCheckpointSectionKind kind,
        string scratchDirectory,
        List<EncodedCheckpointSection> destination,
        ref long nextId)
    {
        for (var offset = 0; offset < source.Length; offset += chunkSize)
        {
            var count = Math.Min(chunkSize, source.Length - offset);
            var chunk = new T[count];
            Array.Copy(source, offset, chunk, 0, count);
            var ordinal = offset / chunkSize;
            var path = CreateScratchPath(scratchDirectory, $"{kind}-{ordinal:D4}");
            var encoding = WorldSaveCodec.EncodeValueStreamingToFile(
                chunk,
                path,
                WorldSaveCodec.MaximumSectionJsonBytes,
                $"{kind} section {ordinal}");
            destination.Add(new EncodedCheckpointSection(
                nextId++,
                kind,
                ordinal,
                offset,
                count,
                encoding));
        }
    }

    private static void EncodeSingleton<T>(
        T? source,
        WorldCheckpointSectionKind kind,
        string scratchDirectory,
        List<EncodedCheckpointSection> destination,
        ref long nextId)
        where T : class
    {
        if (source is null)
        {
            return;
        }

        var path = CreateScratchPath(scratchDirectory, kind.ToString());
        var encoding = WorldSaveCodec.EncodeValueStreamingToFile(
            source,
            path,
            WorldSaveCodec.MaximumStreamingJsonBytes,
            $"{kind} section");
        destination.Add(new EncodedCheckpointSection(nextId++, kind, 0, 0, 1, encoding));
    }

    private static void EncodeValue<T>(
        T source,
        WorldCheckpointSectionKind kind,
        string scratchDirectory,
        List<EncodedCheckpointSection> destination,
        ref long nextId)
    {
        var path = CreateScratchPath(scratchDirectory, kind.ToString());
        var encoding = WorldSaveCodec.EncodeValueStreamingToFile(
            source,
            path,
            WorldSaveCodec.MaximumStreamingJsonBytes,
            $"{kind} section");
        destination.Add(new EncodedCheckpointSection(nextId++, kind, 0, 0, 1, encoding));
    }

    private static void WriteSection(
        WindowsSqlite database,
        EncodedCheckpointSection section)
    {
        using (var insert = database.Prepare("""
            INSERT INTO authority_section (
                id, kind, ordinal, item_offset, item_count, json_bytes,
                payload_bytes, payload_sha256, payload)
            VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, zeroblob(?9));
            """))
        {
            insert.BindInt64(1, section.Id);
            insert.BindInt32(2, (int)section.Kind);
            insert.BindInt32(3, section.Ordinal);
            insert.BindInt32(4, section.ItemOffset);
            insert.BindInt32(5, section.ItemCount);
            insert.BindInt64(6, section.Encoding.JsonBytes);
            insert.BindInt32(7, section.Encoding.ContainerBytes);
            insert.BindBlob(8, new byte[SHA256.HashSizeInBytes]);
            insert.BindInt32(9, section.Encoding.ContainerBytes);
            insert.Execute();
        }

        byte[] hash;
        using (var source = new FileStream(
            section.Encoding.Path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            IoBufferBytes,
            FileOptions.SequentialScan))
        using (var destination = database.OpenBlob(
            "authority_section",
            "payload",
            section.Id,
            writable: true))
        {
            hash = CopyAndHash(source, destination, section.Encoding.ContainerBytes);
        }

        using var update = database.Prepare(
            "UPDATE authority_section SET payload_sha256=?1 WHERE id=?2;");
        update.BindBlob(1, hash);
        update.BindInt64(2, section.Id);
        update.Execute();
    }

    private static void ValidateLayout(StoredCheckpointLayout layout)
    {
        if (layout.FormatVersion is < MinimumSupportedFormatVersion or > FormatVersion
            || layout.AgentCount < 0
            || layout.EventCount < 0
            || layout.WorldChronicleCount < -1
            || layout.SectionCount < 0
            || layout.JsonBytes <= 0
            || layout.ContainerBytes <= 0)
        {
            throw new InvalidDataException("지원하지 않거나 손상된 분할 저장 layout입니다.");
        }
    }

    private static void ValidateCoverage(
        StoredCheckpointLayout layout,
        IReadOnlyList<StoredCheckpointSection> sections)
    {
        ValidateKindCoverage(sections, WorldCheckpointSectionKind.Agents, layout.AgentCount);
        ValidateKindCoverage(sections, WorldCheckpointSectionKind.Events, layout.EventCount);
        ValidateKindCoverage(
            sections,
            WorldCheckpointSectionKind.WorldChronicle,
            Math.Max(0, layout.WorldChronicleCount));
        if (layout.FormatVersion >= 2)
        {
            foreach (var kind in Enum.GetValues<WorldCheckpointSectionKind>().Where(kind => kind > WorldCheckpointSectionKind.WorldChronicle))
            {
                ValidateSingletonCoverage(sections, kind);
            }
        }
        var totalJson = sections.Sum(section => section.JsonBytes);
        var totalContainers = sections.Sum(section => (long)section.PayloadBytes);
        if (totalJson >= layout.JsonBytes || totalContainers >= layout.ContainerBytes)
        {
            throw new InvalidDataException("분할 저장 header 크기 합계가 잘못되었습니다.");
        }
    }

    private static void ValidateSingletonCoverage(
        IReadOnlyList<StoredCheckpointSection> sections,
        WorldCheckpointSectionKind kind)
    {
        var matches = sections.Where(section => section.Kind == kind).ToArray();
        if (matches.Length > 1
            || matches is [{ Ordinal: not 0 }]
            || matches is [{ ItemOffset: not 0 }]
            || matches is [{ ItemCount: not 1 }])
        {
            throw new InvalidDataException($"분할 저장 {kind} 단일 section metadata가 잘못되었습니다.");
        }
    }

    private static void ValidateKindCoverage(
        IReadOnlyList<StoredCheckpointSection> sections,
        WorldCheckpointSectionKind kind,
        int expectedCount)
    {
        var offset = 0;
        var ordinal = 0;
        foreach (var section in sections.Where(section => section.Kind == kind))
        {
            if (section.Ordinal != ordinal++
                || section.ItemOffset != offset
                || section.ItemCount <= 0
                || section.ItemCount > expectedCount - offset)
            {
                throw new InvalidDataException($"분할 저장 {kind} section 순서가 잘못되었습니다.");
            }
            offset += section.ItemCount;
        }
        if (offset != expectedCount)
        {
            throw new InvalidDataException(
                $"분할 저장 {kind} 범위가 다릅니다. ({offset:N0}/{expectedCount:N0})");
        }
    }

    private static WorldCheckpointSectionKind ParseKind(int value) => value switch
    {
        (int)WorldCheckpointSectionKind.Agents => WorldCheckpointSectionKind.Agents,
        (int)WorldCheckpointSectionKind.Events => WorldCheckpointSectionKind.Events,
        (int)WorldCheckpointSectionKind.WorldChronicle => WorldCheckpointSectionKind.WorldChronicle,
        (int)WorldCheckpointSectionKind.RankingHistory => WorldCheckpointSectionKind.RankingHistory,
        (int)WorldCheckpointSectionKind.CenturyChronicle => WorldCheckpointSectionKind.CenturyChronicle,
        (int)WorldCheckpointSectionKind.Relations => WorldCheckpointSectionKind.Relations,
        (int)WorldCheckpointSectionKind.Reincarnation => WorldCheckpointSectionKind.Reincarnation,
        (int)WorldCheckpointSectionKind.Causal => WorldCheckpointSectionKind.Causal,
        (int)WorldCheckpointSectionKind.Atlas => WorldCheckpointSectionKind.Atlas,
        (int)WorldCheckpointSectionKind.Organizations => WorldCheckpointSectionKind.Organizations,
        (int)WorldCheckpointSectionKind.Content => WorldCheckpointSectionKind.Content,
        (int)WorldCheckpointSectionKind.Knowledge => WorldCheckpointSectionKind.Knowledge,
        (int)WorldCheckpointSectionKind.Laws => WorldCheckpointSectionKind.Laws,
        (int)WorldCheckpointSectionKind.Generation => WorldCheckpointSectionKind.Generation,
        (int)WorldCheckpointSectionKind.DecisionRankings => WorldCheckpointSectionKind.DecisionRankings,
        _ => throw new InvalidDataException($"알 수 없는 분할 저장 section kind {value}입니다."),
    };

    private static T DecodeSingleton<T>(
        Stream blob,
        StoredCheckpointSection section,
        string scratchDirectory)
    {
        if (section is not { Ordinal: 0, ItemOffset: 0, ItemCount: 1 })
        {
            throw new InvalidDataException($"분할 저장 {section.Kind} 단일 section metadata가 잘못되었습니다.");
        }
        return WorldSaveCodec.DecodeValue<T>(
            blob,
            section.PayloadBytes,
            scratchDirectory,
            MaximumJsonBytes(section.Kind),
            $"{section.Kind} section",
            section.PayloadHash);
    }

    private static bool HasSeparatedPayload(WorldCheckpoint checkpoint) =>
        checkpoint.RankingHistory is not null
        || checkpoint.CenturyChronicle is not null
        || checkpoint.Relations is not null
        || checkpoint.Reincarnation is not null
        || checkpoint.Causal is not null
        || checkpoint.Atlas is not null
        || checkpoint.Organizations is not null
        || checkpoint.Content is not null
        || checkpoint.Knowledge is not null
        || checkpoint.Laws is not null
        || checkpoint.Generation is not null
        || checkpoint.DecisionRankings is not null;

    private static long MaximumJsonBytes(WorldCheckpointSectionKind kind) =>
        kind > WorldCheckpointSectionKind.WorldChronicle
            ? WorldSaveCodec.MaximumStreamingJsonBytes
            : WorldSaveCodec.MaximumSectionJsonBytes;

    private static void CopyDecoded<T>(
        T[] decoded,
        T[] destination,
        StoredCheckpointSection section)
    {
        if (decoded.Length != section.ItemCount)
        {
            throw new InvalidDataException(
                $"분할 저장 section {section.Id} 항목 수가 다릅니다. ({decoded.Length:N0}/{section.ItemCount:N0})");
        }
        Array.Copy(decoded, 0, destination, section.ItemOffset, decoded.Length);
    }

    private static byte[] CopyAndHash(Stream source, Stream destination, int expectedBytes)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(IoBufferBytes);
        try
        {
            var total = 0;
            while (total < expectedBytes)
            {
                var count = source.Read(buffer, 0, Math.Min(buffer.Length, expectedBytes - total));
                if (count == 0)
                {
                    throw new EndOfStreamException("분할 저장 임시 container가 잘렸습니다.");
                }
                destination.Write(buffer, 0, count);
                hash.AppendData(buffer, 0, count);
                total += count;
            }
            if (source.ReadByte() != -1 || destination.Position != expectedBytes)
            {
                throw new InvalidDataException("분할 저장 container 또는 SQLite BLOB 길이가 잘못되었습니다.");
            }
            return hash.GetHashAndReset();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void ValidateBlobHash(
        Stream blob,
        int expectedBytes,
        byte[] expectedHash,
        string subject)
    {
        if (blob.Length != expectedBytes)
        {
            throw new InvalidDataException($"{subject} BLOB 길이가 metadata와 다릅니다.");
        }
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(IoBufferBytes);
        try
        {
            var total = 0;
            while (total < expectedBytes)
            {
                var count = blob.Read(buffer, 0, Math.Min(buffer.Length, expectedBytes - total));
                if (count == 0)
                {
                    throw new EndOfStreamException($"{subject} BLOB가 잘렸습니다.");
                }
                hash.AppendData(buffer, 0, count);
                total += count;
            }
            if (!CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), expectedHash))
            {
                throw new InvalidDataException($"{subject} SHA-256이 일치하지 않습니다.");
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static string CreateScratchPath(string directory, string label) => Path.Combine(
        directory,
        $".authority-{label}-{Guid.NewGuid():N}.cmip.tmp");

    internal static void TryDelete(string? path)
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
            // A later save removes stale section files while holding the slot lock.
        }
    }
}
