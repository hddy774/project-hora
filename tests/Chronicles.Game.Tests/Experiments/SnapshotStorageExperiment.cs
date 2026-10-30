using System.IO.Compression;
using System.Security.Cryptography;

namespace Chronicles.Game.Tests.Experiments;

internal static class SnapshotStorageExperiment
{
    private const int MaximumPlainBytes = 512 * 1024 * 1024;
    internal sealed record RecoveryProof(bool CompletePending, bool PartialPending, bool CorruptPrimaryBackup,
        bool CorruptPayloadRejected, bool UnsupportedOriginalPreserved, string SecondEnvelopeHash);
    internal sealed record StorageCorrectness(bool SameCanonicalEnvelope, string CanonicalEnvelopeHash,
        int CanonicalBytes, RecoveryProof JsonAtomic, RecoveryProof BrotliAtomic, string CandidateVersion, string Limitations);

    internal static StorageCorrectness Verify(PublicReadFixture fixture, string? workspace = null)
    {
        var compressed = CompressedAtomicSnapshot.Pack(fixture.Envelope);
        var unpacked = CompressedAtomicSnapshot.Unpack(compressed, MaximumPlainBytes);
        ExecutionExperimentScenarios.Require(unpacked.SequenceEqual(fixture.Envelope) && GameCodec.Decode(unpacked).StateHash() == fixture.Hash,
            "선택 저장 후보가 같은 GameCodec 바이트/정체성을 보존하지 못했습니다.");
        var baseline = ProbeRecovery(fixture, compressedStore: false, workspace);
        var candidate = ProbeRecovery(fixture, compressedStore: true, workspace);
        ExecutionExperimentScenarios.Require(baseline.SecondEnvelopeHash == candidate.SecondEnvelopeHash,
            "원자 저장 후보의 동일 sequence/schema 바이트가 다릅니다.");
        return new(true, Convert.ToHexStringLower(SHA256.HashData(fixture.Envelope)), fixture.Envelope.Length, baseline, candidate,
            typeof(BrotliStream).Assembly.GetName().Name + " " + typeof(BrotliStream).Assembly.GetName().Version + "; 현재 .NET BCL/MIT·추가 패키지/제품 채택 없음",
            "기존 JSON GameCodec를 바꾸지 않는 압축 저장 후보. SQLite/선택 serializer 호환을 실행했다고 하지 않음. 동기 파일·동일 호스트의 주입 복구만 검사하며 실제 전원 손실/Android·native 메모리는 별도.");
    }

    internal static ReadStorageComparison Measure(PublicReadFixture warm, PublicReadFixture measured, ReadStorageExperimentOptions options)
    {
        var correctness = Verify(measured, options.Workspace);
        var variants = new[]
        {
            ExecutionExperimentScenarios.Sample("EX-05", "json-codec-roundtrip", "실제 GameCodec 전체 encode·schema/checksum decode·권위hash", "파일 I/O는 atomic variant, fixture 생성은 별도", warm, measured, options,
                fixture => { var bytes = GameCodec.Encode(fixture.World, 1); return new(GameCodec.Decode(bytes).StateHash(), bytes.Length); }, measured.Hash),
            ExecutionExperimentScenarios.Sample("EX-05", "brotli-codec-roundtrip", "동일 GameCodec encode·Brotli 변환/복사·길이/내용검증·GameCodec decode·권위hash", "파일 I/O는 atomic variant·native compressor 메모리 미계측", warm, measured, options,
                fixture => { var bytes = CompressedAtomicSnapshot.Pack(GameCodec.Encode(fixture.World, 1)); return new(GameCodec.Decode(CompressedAtomicSnapshot.Unpack(bytes, MaximumPlainBytes)).StateHash(), bytes.Length); }, measured.Hash),
            ExecutionExperimentScenarios.Sample("EX-05", "json-atomic-full-roundtrip", "고유 작업폴더 생성/정리·현재 GameSaveStore 전체 encode/검증·Flush(true)·rename·읽기/decode·동일바이트 확인·hash", "실제 전원/프로세스 강제중단·OS/native 할당은 별도", warm, measured, options,
                fixture => StoreRoundTrip(fixture, compressed: false, options.Workspace), measured.Hash),
            ExecutionExperimentScenarios.Sample("EX-05", "brotli-atomic-full-roundtrip", "같은 전체 codec·압축/복사·길이/해시검증·Flush(true)·rename·읽기/해제·권위검증·고유폴더 정리", "실제 전원/프로세스 강제중단·native compressor/OS 메모리 미계측", warm, measured, options,
                fixture => StoreRoundTrip(fixture, compressed: true, options.Workspace), measured.Hash)
        };
        return new("EX-05", "현재 schema의 같은 GameCodec envelope를 JSON 원자파일과 BCL Brotli 원자파일 후보로 저장. 새 앱 경로/serializer/SQLite를 채택하지 않음.",
            measured.Hash, measured.CommandHash, correctness, variants, variants.Any(row => row.Measurement.Decision == "reject") ? "reject" : "revisit", "변환·검증·파일 크기·전체 지연/관리할당·복구 한계를 함께 비교한 후 판단. 작은 파일의 압축만으로 채택하지 않음.");
    }

    private static ReadStorageOutput StoreRoundTrip(PublicReadFixture fixture, bool compressed, string? workspace)
    {
        using var directory = new OwnedExperimentDirectory(workspace);
        var path = Path.Combine(directory.Path, compressed ? "world.brotli-experiment" : "world.seonro");
        if (compressed) CompressedAtomicSnapshot.Save(fixture.World, path, MaximumPlainBytes);
        else GameSaveStore.SaveAtomic(fixture.World, path);
        var plain = compressed ? CompressedAtomicSnapshot.Read(path, MaximumPlainBytes).Plain : File.ReadAllBytes(path);
        ExecutionExperimentScenarios.Require(plain.SequenceEqual(fixture.Envelope), "전체 저장 후보의 실제 바이트가 정답과 다릅니다.");
        var restored = compressed ? CompressedAtomicSnapshot.Load(path, MaximumPlainBytes) : GameSaveStore.Load(path);
        return new(restored.StateHash(), Directory.EnumerateFiles(directory.Path, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length));
    }

    private static RecoveryProof ProbeRecovery(PublicReadFixture fixture, bool compressedStore, string? workspace)
    {
        using var directory = new OwnedExperimentDirectory(workspace); var path = Path.Combine(directory.Path, "checkpoint");
        void Save(GameWorld world) { if (compressedStore) CompressedAtomicSnapshot.Save(world, path, MaximumPlainBytes); else GameSaveStore.SaveAtomic(world, path); }
        GameWorld Load() => compressedStore ? CompressedAtomicSnapshot.Load(path, MaximumPlainBytes) : GameSaveStore.Load(path);
        Save(fixture.World); var first = File.ReadAllBytes(path);
        var next = GameCodec.Decode(fixture.Envelope);
        var accepted = next.Execute(new("ex:next-valid-checkpoint", 0, "observe"));
        ExecutionExperimentScenarios.Require(accepted.Accepted && next.StateHash() != fixture.Hash, "실제 다음 체크포인트 전이가 필요합니다.");
        Save(next); var second = File.ReadAllBytes(path);
        var secondPlain = compressedStore ? CompressedAtomicSnapshot.Read(path, MaximumPlainBytes).Plain : second;
        var secondEnvelopeHash = Convert.ToHexStringLower(SHA256.HashData(secondPlain));
        File.WriteAllBytes(path, first); File.WriteAllBytes(path + ".pending", second);
        var complete = Load().StateHash() == next.StateHash();
        File.WriteAllText(path + ".pending", "부분 저장"); var partial = Load().StateHash() == fixture.Hash;
        File.WriteAllText(path, "손상 primary"); var backup = Load().StateHash() == fixture.Hash;
        var corrupt = second.ToArray(); corrupt[corrupt.Length / 2] ^= 0x40;
        var rejected = false;
        try { if (compressedStore) GameCodec.Decode(CompressedAtomicSnapshot.Unpack(corrupt, MaximumPlainBytes)); else GameCodec.Decode(corrupt); }
        catch (InvalidDataException) { rejected = true; }
        foreach (var file in new[] { path, path + ".pending", path + ".backup" }) if (File.Exists(file)) File.Delete(file);
        var unsupported = new byte[] { 0xff, 0, 0x12, 0x7f }; File.WriteAllBytes(path, unsupported); Save(next);
        var preserved = Directory.Exists(Path.Combine(directory.Path, "preserved")) && Directory.GetFiles(Path.Combine(directory.Path, "preserved"))
            .Any(file => File.ReadAllBytes(file).SequenceEqual(unsupported));
        ExecutionExperimentScenarios.Require(complete && partial && backup && rejected && preserved,
            "동일 호스트 주입 저장/손상/복구/원본 보존 후보가 실패했습니다.");
        return new(complete, partial, backup, rejected, preserved, secondEnvelopeHash);
    }

    private sealed class OwnedExperimentDirectory : IDisposable
    {
        internal string Path { get; }
        internal OwnedExperimentDirectory(string? parent)
        { Path = System.IO.Path.Combine(parent ?? System.IO.Path.GetTempPath(), "seonro-ex05-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); }
        public void Dispose() { Directory.Delete(Path, recursive: true); }
    }

    /// <summary>Test-only bounded compressed transport around the existing authenticated GameCodec; never an application save owner.</summary>
    private static class CompressedAtomicSnapshot
    {
        private static readonly byte[] Magic = "SEONRO-BROTLI-EX05\n"u8.ToArray();
        internal sealed record Decoded(byte[] Plain, GameWorld World, long Sequence);
        internal static byte[] Pack(byte[] plain)
        {
            using var output = new MemoryStream();
            output.Write(Magic); using (var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true)) writer.Write(plain.Length);
            output.Write(SHA256.HashData(plain));
            using (var compressor = new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true)) compressor.Write(plain);
            return output.ToArray();
        }
        internal static byte[] Unpack(byte[] wrapped, int maximum)
        {
            using var input = new MemoryStream(wrapped, writable: false);
            if (wrapped.Length < Magic.Length + sizeof(int) + 32 || !wrapped.AsSpan(0, Magic.Length).SequenceEqual(Magic))
                throw new InvalidDataException("선택 저장 후보 형식이 아닙니다.");
            input.Position = Magic.Length;
            using var reader = new BinaryReader(input, System.Text.Encoding.UTF8, leaveOpen: true);
            var length = reader.ReadInt32();
            if (length is <= 0 || length > maximum) throw new InvalidDataException("선택 저장 후보의 해제 크기 제한 위반");
            var expected = reader.ReadBytes(32); var plain = new byte[length];
            using var decompressor = new BrotliStream(input, CompressionMode.Decompress, leaveOpen: true);
            try { decompressor.ReadExactly(plain); }
            catch (EndOfStreamException error) { throw new InvalidDataException("중단된 압축 저장", error); }
            if (decompressor.ReadByte() != -1 || !SHA256.HashData(plain).AsSpan().SequenceEqual(expected))
                throw new InvalidDataException("선택 저장 후보의 길이/내용 불일치");
            return plain;
        }
        internal static Decoded Read(string path, int maximum)
        {
            if (new FileInfo(path).Length > (long)BrotliEncoder.GetMaxCompressedLength(maximum) + Magic.Length + sizeof(int) + 32)
                throw new InvalidDataException("선택 저장 후보 파일 크기 제한 위반");
            var plain = Unpack(File.ReadAllBytes(path), maximum);
            var envelope = GameCodec.DecodeEnvelope(plain);
            return new(plain, envelope.World, envelope.Sequence);
        }
        internal static void Save(GameWorld world, string path, int maximum)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
            var sequence = 0L;
            foreach (var candidate in new[] { path, path + ".pending", path + ".backup" })
            {
                if (!File.Exists(candidate)) continue;
                try { sequence = Math.Max(sequence, Read(candidate, maximum).Sequence); }
                catch (InvalidDataException) { Preserve(candidate); }
            }
            var bytes = Pack(GameCodec.Encode(world, checked(sequence + 1)));
            using (var output = new FileStream(path + ".pending", FileMode.Create, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough))
            { output.Write(bytes); output.Flush(true); }
            Read(path + ".pending", maximum);
            if (File.Exists(path))
            {
                try { Read(path, maximum); File.Copy(path, path + ".backup", overwrite: true); }
                catch (InvalidDataException) { Preserve(path); }
            }
            File.Move(path + ".pending", path, overwrite: true);
        }
        internal static GameWorld Load(string path, int maximum)
        {
            Decoded? chosen = null;
            foreach (var candidate in new[] { path, path + ".pending", path + ".backup" })
            {
                try { var value = Read(candidate, maximum); if (chosen is null || value.Sequence > chosen.Sequence) chosen = value; }
                catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { }
            }
            return chosen?.World ?? throw new InvalidDataException("선택 저장 후보에 정상 snapshot이 없습니다.");
        }
        private static void Preserve(string path)
        {
            var bytes = File.ReadAllBytes(path); var directory = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path)!, "preserved");
            Directory.CreateDirectory(directory);
            var retained = System.IO.Path.Combine(directory, System.IO.Path.GetFileName(path) + "." + Convert.ToHexStringLower(SHA256.HashData(bytes)) + ".original");
            if (!File.Exists(retained)) File.WriteAllBytes(retained, bytes);
        }
    }
}
