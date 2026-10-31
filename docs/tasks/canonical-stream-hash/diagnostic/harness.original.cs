using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chronicles.Game;

const string source = "df7240556a260202a248e57b1f9868e372b9d995";
const string sourceDlls = "/workspace/seonro-integration-df72405/tests/Chronicles.Game.Tests/bin/Release/net10.0";
var output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
var journal = Path.Combine(output, "events.ndjson");
void Emit(object value) { var line = JsonSerializer.Serialize(value); File.AppendAllText(journal, line + "\n"); Console.WriteLine(line); }
var tests = Assembly.LoadFrom(Path.Combine(sourceDlls, "Chronicles.Game.Tests.dll"));
var assemblies = new[] { typeof(Chronicles.Core.ProductInfo).Assembly, typeof(GameWorld).Assembly, typeof(GameSaveStore).Assembly, tests };
var identities = assemblies.Select(a => new { name = a.GetName().Name,
    informational = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    configuration = a.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration,
    path = a.Location, sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(a.Location))) }).ToArray();
if (identities.Any(i => i.informational != "0.2.1+" + source || i.configuration != "Release")) throw new InvalidDataException("Exact source/Release DLL mismatch");
var fixture = tests.GetType("Chronicles.Game.Tests.SyntheticPopulationFixture", true)!;
var build = fixture.GetMethod("Build", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Func<int, GameState>>();
var describe = fixture.GetMethod("Describe", BindingFlags.Static | BindingFlags.NonPublic)!;
var clone = typeof(GameCodec).GetMethod("Clone", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Func<GameState, GameState>>();
var forceEncode = typeof(GameCodec).GetMethod("Encode", BindingFlags.Static | BindingFlags.NonPublic, [typeof(GameWorld), typeof(long), typeof(int)])!.CreateDelegate<ForceEncode>();
var options = (JsonSerializerOptions)typeof(GameCodec).GetField("Options", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
var deadline = DateTimeOffset.Parse("2026-10-31T01:46:02+00:00");
Emit(new { stage = "provenance", source, productFingerprint = "0cb2f90f (root-provided prefix; no new fingerprint scope)",
    deadlineUtc = deadline, cpuAffinity = "3 via taskset supervisor", sdk = "10.0.403", runtime = RuntimeInformation.FrameworkDescription,
    processorCount = Environment.ProcessorCount, pgO = AppContext.GetData("System.Runtime.TieredPGO"), assemblies = identities,
    scope = "bounded diagnostic, existing synthetic opening fixture, Month0/no AdvanceTo; shared host with CPU1/2 diagnostics; not final benchmark" });

var metrics = new List<object>(); var fixtures = new List<object>();
void CheckTime() { if (DateTimeOffset.UtcNow >= deadline) throw new TimeoutException("Fixed total ten-minute deadline"); }
void Measure<T>(int n, string name, int repetitions, Func<T> action, Action<T>? verify = null)
{
    for (var sample = 1; sample <= repetitions; sample++)
    {
        CheckTime(); Emit(new { stage = "before", population = n, operation = name, sample, utc = DateTimeOffset.UtcNow });
        var threadBefore = GC.GetAllocatedBytesForCurrentThread(); var totalBefore = GC.GetTotalAllocatedBytes(true);
        var start = Stopwatch.GetTimestamp(); var value = action(); var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var threadBytes = GC.GetAllocatedBytesForCurrentThread() - threadBefore; var totalBytes = GC.GetTotalAllocatedBytes(true) - totalBefore;
        verify?.Invoke(value); GC.KeepAlive(value);
        var row = new { stage = "measured", population = n, operation = name, sample, elapsedMs = elapsed,
            allocatedThreadBytes = threadBytes, allocatedProcessBytes = totalBytes };
        metrics.Add(row); Emit(row);
    }
}
foreach (var n in new[] { 100, 1000 })
{
    CheckTime(); Emit(new { stage = "fixture_build", population = n, utc = DateTimeOffset.UtcNow });
    var state = build(n); var world = GameWorld.Restore(state);
    var rng = state.RandomState; var canonical = JsonSerializer.SerializeToUtf8Bytes(state, options);
    var expectedHash = Convert.ToHexStringLower(SHA256.HashData(canonical));
    if (GameCodec.Hash(state) != expectedHash || world.StateHash() != expectedHash) throw new InvalidDataException("Opening canonical identity mismatch");
    using (var oracle = new MemoryStream()) { JsonSerializer.Serialize(oracle, state, options); if (!oracle.GetBuffer().AsSpan(0, (int)oracle.Length).SequenceEqual(canonical)) throw new InvalidDataException("Stream canonical bytes differ"); }
    var jsonString = JsonSerializer.SerializeToUtf8Bytes(Encoding.UTF8.GetString(canonical), options);
    var defaultBytes = GameCodec.Encode(world); var binaryBytes = forceEncode(world, 0, 0);
    if (GameCodec.Decode(defaultBytes).StateHash() != expectedHash || GameCodec.Decode(binaryBytes).StateHash() != expectedHash) throw new InvalidDataException("Envelope roundtrip mismatch");
    var fixtureInfo = new { population = n, month = state.Month, description = describe.Invoke(null, [n]), openingHash = expectedHash,
        randomState = rng, canonicalBytes = canonical.Length, quotedPayloadTokenBytes = jsonString.Length,
        escapeAddedBytes = jsonString.Length - 2 - canonical.Length, defaultEnvelopeBytes = defaultBytes.Length,
        defaultEnvelopeToPayloadRatio = (double)defaultBytes.Length / canonical.Length,
        forcedBinaryEnvelopeBytes = binaryBytes.Length, rawQuotes = canonical.Count(b => b == 34), rawBackslashes = canonical.Count(b => b == 92),
        nonAsciiBytes = canonical.Count(b => b > 127), defaultEnvelopeKind = defaultBytes[0] == (byte)'{' ? "JSON schema2" : "binary schema3",
        forcedBinaryUsesExistingInternalLimitSeam = true, oracleStreamBytesExactlyEqual = true };
    fixtures.Add(fixtureInfo); Emit(new { stage = "fixture_ready", fixture = fixtureInfo });
    Measure(n, "SerializeCanonicalUtf8", 3, () => JsonSerializer.SerializeToUtf8Bytes(state, options), b => { if (!b.AsSpan().SequenceEqual(canonical)) throw new InvalidDataException("Serialization changed"); });
    Measure(n, "GameCodec.Hash", 3, () => GameCodec.Hash(state), h => { if (h != expectedHash) throw new InvalidDataException("Hash changed"); });
    Measure(n, "HashExistingCanonicalBuffer_attribution_only", 3, () => Convert.ToHexStringLower(SHA256.HashData(canonical)), h => { if (h != expectedHash) throw new InvalidDataException("Buffer hash changed"); });
    Measure(n, "ScratchStreamCanonicalHash_candidate", 3, () => { using var sink = new HashSink(); JsonSerializer.Serialize(sink, state, options); if (sink.Bytes != canonical.Length) throw new InvalidDataException("Stream length"); return sink.Hash(); }, h => { if (h != expectedHash) throw new InvalidDataException("Stream hash changed"); });
    Measure(n, "GameCodec.Clone", 3, () => clone(state), s => { if (GameCodec.Hash(s) != expectedHash || s.RandomState != rng) throw new InvalidDataException("Clone identity changed"); });
    Measure(n, "DeserializeExistingBuffer_attribution_only", 3, () => JsonSerializer.Deserialize<GameState>(canonical, options)!, s => { if (GameCodec.Hash(s) != expectedHash || s.RandomState != rng) throw new InvalidDataException("Deserialize identity changed"); });
    Measure(n, "Invariants.Validate", 2, () => { Invariants.Validate(state); return true; });
    Measure(n, "GameCodec.Encode_default", 2, () => GameCodec.Encode(world), b => { if (!b.AsSpan().SequenceEqual(defaultBytes)) throw new InvalidDataException("Encode bytes changed"); });
    Measure(n, "GameCodec.Encode_forced_binary_existing_seam", 2, () => forceEncode(world, 0, 0), b => { if (!b.AsSpan().SequenceEqual(binaryBytes)) throw new InvalidDataException("Binary bytes changed"); });
    Measure(n, "GameCodec.Decode_default", 2, () => GameCodec.Decode(defaultBytes), w => { if (w.StateHash() != expectedHash || w.Checkpoint().RandomState != rng) throw new InvalidDataException("Decode changed"); });
    var slot = Path.Combine(output, "slots", "n" + n, "world.save"); Directory.CreateDirectory(Path.GetDirectoryName(slot)!);
    if (File.Exists(slot)) throw new InvalidDataException("New diagnostic path already exists");
    Measure(n, "GameSaveStore.SaveAtomic_new", 1, () => { GameSaveStore.SaveAtomic(world, slot); return true; });
    Measure(n, "GameSaveStore.SaveAtomic_overwrite", 1, () => { GameSaveStore.SaveAtomic(world, slot); return true; });
    Measure(n, "GameSaveStore.Load_primary_and_backup", 1, () => GameSaveStore.Load(slot), w => { if (w.StateHash() != expectedHash || w.Checkpoint().RandomState != rng) throw new InvalidDataException("File load changed"); });
    var ioPath = Path.Combine(Path.GetDirectoryName(slot)!, "writeflush-only.bin");
    Measure(n, "WriteThroughFlush_existing_encoded_bytes_attribution_only", 1, () => { using var f = new FileStream(ioPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough); f.Write(defaultBytes); f.Flush(true); return true; });
    if (GameCodec.Hash(state) != expectedHash || state.RandomState != rng || world.StateHash() != expectedHash || world.Checkpoint().RandomState != rng) throw new InvalidDataException("Original state/RNG mutated");
    Emit(new { stage = "fixture_complete", population = n, unchangedCanonicalHash = expectedHash, unchangedRandomState = rng,
        primaryBytes = new FileInfo(slot).Length, backupBytes = new FileInfo(slot + ".backup").Length, utc = DateTimeOffset.UtcNow });
    File.WriteAllText(Path.Combine(output, "partial.json"), JsonSerializer.Serialize(new { complete = false, fixtures, metrics }, new JsonSerializerOptions { WriteIndented = true }));
}
File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { source, complete = true, fixedDeadlineUtc = deadline,
    fixtures, metrics, limitations = "Month0 synthetic populations, ordered cold/warm mixed samples, shared 4CPU host, CPU3 only, no 12-month advancement or final performance gates; buffer reuse probe is attribution, not a valid replacement for changing states" }, new JsonSerializerOptions { WriteIndented = true }));
Emit(new { stage = "complete", utc = DateTimeOffset.UtcNow });

delegate byte[] ForceEncode(GameWorld world, long sequence, int limit);
sealed class HashSink : Stream
{
    private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    public long Bytes { get; private set; }
    public string Hash() => Convert.ToHexStringLower(hash.GetHashAndReset());
    public override void Write(byte[] buffer, int offset, int count) { hash.AppendData(buffer, offset, count); Bytes += count; }
    public override void Write(ReadOnlySpan<byte> buffer) { hash.AppendData(buffer); Bytes += buffer.Length; }
    public override void Flush() { }
    public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
    public override long Length => Bytes; public override long Position { get => Bytes; set => throw new NotSupportedException(); }
    public override int Read(byte[] b, int o, int c) => throw new NotSupportedException();
    public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException(); public override void SetLength(long v) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { if (disposing) hash.Dispose(); base.Dispose(disposing); }
}
