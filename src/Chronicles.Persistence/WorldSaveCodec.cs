using System.Buffers;
using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;
using System.Text.Json;
using Chronicles.Simulation;

namespace Chronicles.Persistence;

public static class WorldSaveCodec
{
    private const int ContainerVersion = 1;
    private const int ChecksumLength = 32;
    private const int MaximumPayloadBytes = 256 * 1024 * 1024;
    // 구형 단일 파일 encoder는 호환성을 위해 기존 상한을 유지한다.
    private const int MaximumLegacyJsonBytes = 512 * 1024 * 1024;
    // 장기 세계의 개인 영구 연대기는 정상적으로 512 MiB를 넘을 수 있다.
    // 슬롯 저장은 전체 JSON 배열을 만들지 않는 streaming 경로이므로 더 큰 권위 그래프를
    // 허용하되, 손상 파일의 무한 압축 해제는 1.5 GiB에서 계속 차단한다.
    internal const long MaximumStreamingJsonBytes = 1_536L * 1024 * 1024;
    private static readonly byte[] Magic = "CMIP"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    /// <summary>
    /// SQLite 슬롯 저장이 허용하는 압축 전 권위 JSON의 안전 상한입니다.
    /// 진단 UI와 회귀 테스트가 실제 저장 계약을 추측하지 않도록 공개합니다.
    /// </summary>
    public static long StreamingJsonSizeLimitBytes => MaximumStreamingJsonBytes;

    internal static int CompressedPayloadSizeLimitBytes => MaximumPayloadBytes;

    internal readonly record struct StreamingEncodeResult(
        string Path,
        long JsonBytes,
        int CompressedPayloadBytes,
        int ContainerBytes);

    public static byte[] Encode(WorldCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var json = JsonSerializer.SerializeToUtf8Bytes(checkpoint, JsonOptions);
        if (json.Length > MaximumLegacyJsonBytes)
        {
            throw new InvalidDataException(
                $"구형 저장 JSON이 허용 크기를 초과했습니다. ({json.Length:N0} / {MaximumLegacyJsonBytes:N0} bytes)");
        }
        using var payloadStream = new MemoryStream();
        using (var compression = new BrotliStream(payloadStream, CompressionLevel.Optimal, leaveOpen: true))
        {
            compression.Write(json);
        }

        var payload = payloadStream.ToArray();
        if (payload.Length > MaximumPayloadBytes)
        {
            throw new InvalidDataException(
                $"저장 payload가 허용 크기를 초과했습니다. ({payload.Length:N0} / {MaximumPayloadBytes:N0} bytes)");
        }

        var checksum = SHA256.HashData(payload);
        using var container = new MemoryStream(Magic.Length + 8 + ChecksumLength + payload.Length);
        using var writer = new BinaryWriter(container);
        writer.Write(Magic);
        writer.Write(ContainerVersion);
        writer.Write(payload.Length);
        writer.Write(checksum);
        writer.Write(payload);
        writer.Flush();
        return container.ToArray();
    }

    /// <summary>
    /// Schema-25 SQLite writer path. JSON and Brotli output are both streamed into a
    /// temporary CMIP-v1 container. No authority-sized managed byte array is created.
    /// The caller owns deletion of <paramref name="path"/> on success and failure.
    /// </summary>
    internal static StreamingEncodeResult EncodeStreamingToFile(
        WorldCheckpoint checkpoint,
        string path)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var file = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 256 * 1024,
            FileOptions.SequentialScan | FileOptions.WriteThrough);
        using var writer = new BinaryWriter(file, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(ContainerVersion);
        writer.Write(0); // compressed payload length, finalized after Brotli is closed
        writer.Write(new byte[ChecksumLength]);
        writer.Flush();

        using var compressedHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long jsonBytes;
        int compressedBytes;
        byte[] compressedChecksum;
        using (var payload = new HashingWriteStream(
            file,
            compressedHash,
            MaximumPayloadBytes,
            leaveOpen: true))
        {
            using (var compression = new BrotliStream(
                payload,
                CompressionLevel.Optimal,
                leaveOpen: true))
            using (var bounded = new BoundedWriteStream(
                compression,
                MaximumStreamingJsonBytes,
                leaveOpen: true))
            {
                JsonSerializer.Serialize(bounded, checkpoint, JsonOptions);
                bounded.Flush();
                jsonBytes = bounded.BytesWritten;
            }

            compressedBytes = checked((int)payload.BytesWritten);
            compressedChecksum = compressedHash.GetHashAndReset();
        }

        file.Position = Magic.Length + sizeof(int);
        writer.Write(compressedBytes);
        writer.Write(compressedChecksum);
        writer.Flush();
        file.Flush(flushToDisk: true);
        var containerBytes = checked((int)file.Length);
        return new StreamingEncodeResult(
            path,
            jsonBytes,
            compressedBytes,
            containerBytes);
    }

    public static WorldCheckpoint Decode(byte[] container)
    {
        ArgumentNullException.ThrowIfNull(container);
        return DecodeArray(container);
    }

    public static WorldCheckpoint Decode(ReadOnlySpan<byte> container) =>
        Decode(container.ToArray());

    private static WorldCheckpoint DecodeArray(byte[] container)
    {
        var minimumLength = Magic.Length + 8 + ChecksumLength;
        if (container.Length < minimumLength)
        {
            throw new InvalidDataException("저장 파일이 너무 짧습니다.");
        }

        using var stream = new MemoryStream(container, writable: false);
        using var reader = new BinaryReader(stream);
        if (!reader.ReadBytes(Magic.Length).AsSpan().SequenceEqual(Magic))
        {
            throw new InvalidDataException("저장 파일 magic이 일치하지 않습니다.");
        }

        var version = reader.ReadInt32();
        if (version != ContainerVersion)
        {
            throw new InvalidDataException($"지원하지 않는 저장 container {version}입니다.");
        }

        var payloadLength = reader.ReadInt32();
        if (payloadLength < 0
            || payloadLength > MaximumPayloadBytes
            || payloadLength != stream.Length - stream.Position - ChecksumLength)
        {
            throw new InvalidDataException("저장 payload 길이가 잘못되었습니다.");
        }

        var expectedChecksum = reader.ReadBytes(ChecksumLength);
        var payloadOffset = checked((int)stream.Position);
        var actualChecksum = SHA256.HashData(container.AsSpan(payloadOffset, payloadLength));
        if (!CryptographicOperations.FixedTimeEquals(expectedChecksum, actualChecksum))
        {
            throw new InvalidDataException("저장 파일 checksum이 일치하지 않습니다.");
        }

        try
        {
            using var input = new MemoryStream(
                container,
                payloadOffset,
                payloadLength,
                writable: false,
                publiclyVisible: false);
            using var compression = new BrotliStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            using (var bounded = new BoundedWriteStream(
                output,
                MaximumStreamingJsonBytes,
                leaveOpen: true,
                subject: "압축 해제된 저장 JSON"))
            {
                compression.CopyTo(bounded);
            }

            return JsonSerializer.Deserialize<WorldCheckpoint>(
                    output.GetBuffer().AsSpan(0, checked((int)output.Length)),
                    JsonOptions)
                ?? throw new InvalidDataException("세계 체크포인트를 읽을 수 없습니다.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("세계 체크포인트 JSON이 잘못되었습니다.", exception);
        }
    }

    /// <summary>
    /// Reads one CMIP-v1 container without materializing either the compressed
    /// payload or the decompressed JSON in managed memory. A checksum pass is
    /// followed by a decode pass into a short-lived memory-mapped scratch file.
    /// </summary>
    internal static WorldCheckpoint Decode(
        Stream container,
        long containerBytes,
        string? scratchDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(container);
        if (!container.CanRead || !container.CanSeek)
        {
            throw new ArgumentException(
                "저장 container는 읽기와 위치 이동을 지원해야 합니다.",
                nameof(container));
        }

        var minimumLength = Magic.Length + 8 + ChecksumLength;
        if (containerBytes < minimumLength)
        {
            throw new InvalidDataException("저장 파일이 너무 짧습니다.");
        }

        using var reader = new BinaryReader(
            container,
            System.Text.Encoding.UTF8,
            leaveOpen: true);
        if (!reader.ReadBytes(Magic.Length).AsSpan().SequenceEqual(Magic))
        {
            throw new InvalidDataException("저장 파일 magic이 일치하지 않습니다.");
        }

        var version = reader.ReadInt32();
        if (version != ContainerVersion)
        {
            throw new InvalidDataException($"지원하지 않는 저장 container {version}입니다.");
        }

        var payloadLength = reader.ReadInt32();
        if (payloadLength < 0
            || payloadLength > MaximumPayloadBytes
            || containerBytes != minimumLength + (long)payloadLength)
        {
            throw new InvalidDataException(
                $"저장 payload 길이가 잘못되었습니다. (header {payloadLength:N0}, container {containerBytes:N0} bytes)");
        }

        var expectedChecksum = reader.ReadBytes(ChecksumLength);
        if (expectedChecksum.Length != ChecksumLength)
        {
            throw new EndOfStreamException("저장 파일 checksum이 잘렸습니다.");
        }

        string? jsonPath = null;
        try
        {
            var payloadStart = container.Position;
            using (var payloadHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            using (var checksumPayload = new LimitedReadStream(container, payloadLength, leaveOpen: true))
            {
                var buffer = ArrayPool<byte>.Shared.Rent(256 * 1024);
                try
                {
                    while (true)
                    {
                        var count = checksumPayload.Read(buffer, 0, buffer.Length);
                        if (count == 0)
                        {
                            break;
                        }
                        payloadHash.AppendData(buffer, 0, count);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                if (checksumPayload.Remaining != 0)
                {
                    throw new EndOfStreamException(
                        $"저장 payload가 header 길이보다 짧습니다. (부족 {checksumPayload.Remaining:N0} bytes)");
                }

                var actualChecksum = payloadHash.GetHashAndReset();
                if (!CryptographicOperations.FixedTimeEquals(expectedChecksum, actualChecksum))
                {
                    throw new InvalidDataException("저장 파일 checksum이 일치하지 않습니다.");
                }
            }

            container.Position = payloadStart;
            using var payload = new LimitedReadStream(container, payloadLength, leaveOpen: true);
            var scratchRoot = string.IsNullOrWhiteSpace(scratchDirectory)
                ? Path.GetTempPath()
                : Path.GetFullPath(scratchDirectory);
            Directory.CreateDirectory(scratchRoot);
            jsonPath = Path.Combine(
                scratchRoot,
                $".authority-read-{Guid.NewGuid():N}.json.tmp");
            using (var compression = new BrotliStream(
                payload,
                CompressionMode.Decompress,
                leaveOpen: true))
            using (var jsonFile = new FileStream(
                jsonPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 256 * 1024,
                FileOptions.SequentialScan))
            using (var boundedJson = new BoundedWriteStream(
                jsonFile,
                MaximumStreamingJsonBytes,
                leaveOpen: true,
                subject: "압축 해제된 저장 JSON"))
            {
                compression.CopyTo(boundedJson, 256 * 1024);
                boundedJson.Flush();
                jsonFile.Flush(flushToDisk: false);
            }

            if (payload.Remaining != 0)
            {
                throw new InvalidDataException(
                    $"Brotli payload 뒤에 읽히지 않은 데이터가 있습니다. ({payload.Remaining:N0} bytes)");
            }

            var jsonBytes = new FileInfo(jsonPath).Length;
            return DeserializeMappedJson(jsonPath, jsonBytes);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("세계 체크포인트 JSON이 잘못되었습니다.", exception);
        }
        finally
        {
            if (jsonPath is not null)
            {
                try
                {
                    File.Delete(jsonPath);
                }
                catch (IOException)
                {
                    // Best effort. The next storage operation does not rely on it.
                }
                catch (UnauthorizedAccessException)
                {
                    // Best effort. The next storage operation does not rely on it.
                }
            }
        }
    }

    private static unsafe WorldCheckpoint DeserializeMappedJson(string path, long jsonBytes)
    {
        if (jsonBytes <= 0 || jsonBytes > MaximumStreamingJsonBytes || jsonBytes > int.MaxValue)
        {
            throw new SaveCapacityExceededException(
                "압축 해제된 저장 JSON",
                jsonBytes,
                MaximumStreamingJsonBytes);
        }

        using var mapped = MemoryMappedFile.CreateFromFile(
            path,
            FileMode.Open,
            mapName: null,
            capacity: 0,
            MemoryMappedFileAccess.Read);
        using var view = mapped.CreateViewAccessor(0, jsonBytes, MemoryMappedFileAccess.Read);
        byte* pointer = null;
        view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
        try
        {
            pointer += view.PointerOffset;
            return JsonSerializer.Deserialize<WorldCheckpoint>(
                    new ReadOnlySpan<byte>(pointer, checked((int)jsonBytes)),
                    JsonOptions)
                ?? throw new InvalidDataException("세계 체크포인트를 읽을 수 없습니다.");
        }
        finally
        {
            view.SafeMemoryMappedViewHandle.ReleasePointer();
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        options.Converters.Add(new AgentIdJsonConverter());
        options.Converters.Add(new SimulationTickJsonConverter());
        return options;
    }

    private sealed class BoundedWriteStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _maximumBytes;
        private readonly bool _leaveOpen;
        private readonly string _subject;
        private long _written;

        public BoundedWriteStream(
            Stream inner,
            long maximumBytes,
            bool leaveOpen,
            string subject = "저장 JSON")
        {
            _inner = inner;
            _maximumBytes = maximumBytes;
            _leaveOpen = leaveOpen;
            _subject = subject;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _written;
        public long BytesWritten => _written;
        public override long Position
        {
            get => _written;
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        public override void Write(byte[] buffer, int offset, int count)
        {
            ValidateWrite(count);
            _inner.Write(buffer, offset, count);
            _written += count;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ValidateWrite(buffer.Length);
            _inner.Write(buffer);
            _written += buffer.Length;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveOpen)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private void ValidateWrite(int count)
        {
            if (count < 0 || _written > _maximumBytes - count)
            {
                throw new SaveCapacityExceededException(
                    _subject,
                    checked(_written + count),
                    _maximumBytes);
            }
        }
    }

    private sealed class HashingWriteStream : Stream
    {
        private readonly Stream _inner;
        private readonly IncrementalHash _hash;
        private readonly long _maximumBytes;
        private readonly bool _leaveOpen;
        private long _written;

        public HashingWriteStream(
            Stream inner,
            IncrementalHash hash,
            long maximumBytes,
            bool leaveOpen)
        {
            _inner = inner;
            _hash = hash;
            _maximumBytes = maximumBytes;
            _leaveOpen = leaveOpen;
        }

        public long BytesWritten => _written;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _written;
        public override long Position
        {
            get => _written;
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        public override void Write(byte[] buffer, int offset, int count)
        {
            ValidateWrite(count);
            _inner.Write(buffer, offset, count);
            _hash.AppendData(buffer, offset, count);
            _written += count;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ValidateWrite(buffer.Length);
            _inner.Write(buffer);
            _hash.AppendData(buffer);
            _written += buffer.Length;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveOpen)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private void ValidateWrite(int count)
        {
            if (count < 0 || _written > _maximumBytes - count)
            {
                throw new SaveCapacityExceededException(
                    "압축된 저장 payload",
                    checked(_written + count),
                    _maximumBytes);
            }
        }
    }

    private sealed class LimitedReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly bool _leaveOpen;
        private long _remaining;

        public LimitedReadStream(Stream inner, long length, bool leaveOpen)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(length);
            _inner = inner;
            _remaining = length;
            _leaveOpen = leaveOpen;
        }

        public long Remaining => _remaining;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remaining == 0)
            {
                return 0;
            }

            var read = _inner.Read(buffer, offset, (int)Math.Min(count, _remaining));
            _remaining -= read;
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            if (_remaining == 0)
            {
                return 0;
            }

            var read = _inner.Read(buffer[..(int)Math.Min(buffer.Length, _remaining)]);
            _remaining -= read;
            return read;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_leaveOpen)
            {
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }

}

internal sealed class SaveCapacityExceededException : IOException
{
    public SaveCapacityExceededException(string subject, long actualBytes, long limitBytes)
        : base($"{subject}가 허용 크기를 초과했습니다. ({actualBytes:N0} / {limitBytes:N0} bytes)")
    {
        ActualBytes = actualBytes;
        LimitBytes = limitBytes;
    }

    public long ActualBytes { get; }
    public long LimitBytes { get; }
}
