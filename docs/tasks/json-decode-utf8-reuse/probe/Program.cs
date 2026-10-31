using System.Diagnostics.Tracing;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chronicles.Game;

var mode = args[0];
var root = args[1];
var output = args[2];
var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
if (mode == "generate")
{
    Generate(root, output);
    return;
}
var inputs = JsonSerializer.Deserialize<Input[]>(File.ReadAllBytes(Path.Combine(root, "manifest.json")))!;
var rows = new List<object>();
if (mode == "oracle")
{
    foreach (var input in inputs)
    {
        byte[] bytes = File.ReadAllBytes(input.Path);
        string sha = Sha(bytes);
        if (sha != input.Sha256) throw new InvalidDataException("input SHA mismatch");
        try
        {
            var decoded = DecodeEnvelope(bytes);
            var world = decoded.World;
            var state = world.Checkpoint();
            rows.Add(new { input.Name, input.Sha256, Accepted = true, decoded.Sequence, world.Month,
                Hash = world.StateHash(), state.RandomState, EncodedSha256 = Sha(Encode(world, decoded.Sequence)),
                OriginalUnchanged = Sha(bytes) == sha });
        }
        catch (Exception error)
        {
            var errors = new List<object>();
            for (Exception? current = error; current is not null; current = current.InnerException)
                errors.Add(new { Type = current.GetType().FullName, current.Message });
            rows.Add(new { input.Name, input.Sha256, Accepted = false, Errors = errors, OriginalUnchanged = Sha(bytes) == sha });
        }
    }
}
else if (mode == "memory")
{
    using var listener = new PoolEvents();
    foreach (var input in inputs.Where(input => input.RealMonth is not null))
        rows.Add(Measure(input, listener));
}
else throw new ArgumentException("unknown mode");
File.WriteAllBytes(output, JsonSerializer.SerializeToUtf8Bytes(new
{
    mode, Runtime = RuntimeInformation.FrameworkDescription, Environment.ProcessorCount,
    TieredPgoEnvironment = Environment.GetEnvironmentVariable("DOTNET_TieredPGO"),
    ComPlusTieredPgoEnvironment = Environment.GetEnvironmentVariable("COMPlus_TieredPGO"),
    RuntimeConfig = File.ReadAllText(Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".runtimeconfig.json")),
    GameSource = typeof(GameCodec).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
    CoreSource = typeof(Chronicles.Core.ProductInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    Assemblies = new[] { "Chronicles.Game", "Chronicles.Core", "Chronicles.Hosting", "Chronicles.Game.Tests" }
        .Select(name => Path.Combine(AppContext.BaseDirectory, name + ".dll")).Where(File.Exists)
        .Select(path => new { Name = Path.GetFileName(path), Bytes = new FileInfo(path).Length, Sha256 = Sha(File.ReadAllBytes(path)) }),
    Rows = rows,
    Scope = "bounded codec-only diagnostic; no month advance; not T04 acceptance"
}, jsonOptions));

static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
static byte[] LogicalHash(string payload, long sequence) => SHA256.HashData(Encoding.UTF8.GetBytes(
    sequence.ToString(CultureInfo.InvariantCulture) + "\n" + payload));
static byte[] Wrap(string? payload, long sequence = 0, string format = "SEONRO-021", int schema = 2, string? hash = null) =>
    JsonSerializer.SerializeToUtf8Bytes(new Envelope(format, schema,
        hash ?? Convert.ToHexStringLower(LogicalHash(payload ?? "", sequence)), payload, sequence));
static (GameWorld World, long Sequence) DecodeEnvelope(byte[] bytes) =>
    typeof(GameCodec).GetMethod("DecodeEnvelope", BindingFlags.NonPublic | BindingFlags.Static)!
        .CreateDelegate<Decode>()(bytes);
static byte[] Encode(GameWorld world, long sequence) =>
    typeof(GameCodec).GetMethod("Encode", BindingFlags.NonPublic | BindingFlags.Static, [typeof(GameWorld), typeof(long)])!
        .CreateDelegate<EncodeWithSequence>()(world, sequence);
static long Retained()
{
    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
    GC.WaitForPendingFinalizers();
    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
    return GC.GetTotalMemory(false);
}
[MethodImpl(MethodImplOptions.NoInlining)]
static object Measure(Input input, PoolEvents listener)
{
    byte[] bytes = File.ReadAllBytes(input.Path);
    if (Sha(bytes) != input.Sha256) throw new InvalidDataException("real slot SHA mismatch");
    long before = Retained();
    long allocated = GC.GetAllocatedBytesForCurrentThread();
    listener.Begin();
    var world = GameCodec.Decode(bytes);
    long afterDecodeAllocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
    long after = Retained();
    listener.End();
    GC.KeepAlive(world);
    GC.KeepAlive(bytes);
    return new { input.Name, input.RealMonth, input.Sha256, InputBytes = bytes.Length,
        BeforeRetainedBytes = before, AfterRetainedBytes = after, DeltaRetainedBytes = after - before,
        DecodeThreadAllocatedBytes = afterDecodeAllocated, world.Month, Hash = world.StateHash(),
        LargePoolEvents = listener.Events.ToArray(),
        Limits = "live world and original input kept alive; EventListener >=8MiB only; retained includes runtime/pools; no heap-root proof" };
}
static void Generate(string root, string output)
{
    Directory.CreateDirectory(root);
    var items = new List<Input>();
    void Add(string name, byte[] bytes)
    {
        string path = Path.Combine(root, name + ".json");
        File.WriteAllBytes(path, bytes);
        items.Add(new(name, path, Sha(bytes), null));
    }
    var state = GameWorld.Create(412).Checkpoint();
    state.Events[0] = state.Events[0] with { Text = "한글🔥 e\u0301 é \"\\\n\t\0 <>&+ \u2028" };
    var world = GameWorld.Restore(state);
    using var document = JsonDocument.Parse(GameCodec.Encode(world));
    string payload = document.RootElement.GetProperty("Payload").GetString()!;
    Add("valid-sequence0", Wrap(payload));
    Add("valid-sequence7", Wrap(payload, 7));
    Add("valid-sequenceMax", Wrap(payload, long.MaxValue));
    Add("valid-leadingWhitespace", Wrap(" \r\n" + payload));
    Add("valid-trailingWhitespace", Wrap(payload + " \r\n\t"));
    Add("valid-rawUnicode", Wrap(payload.Replace("\\uD55C\\uAE00", "한글", StringComparison.Ordinal)
        .Replace("\\uD83D\\uDD25", "🔥", StringComparison.Ordinal)));
    Add("valid-outerTrailingWhitespace", [.. Wrap(payload), 32, 10, 9]);
    Add("valid-duplicateEnvelopePayload", Encoding.UTF8.GetBytes("{\"Payload\":\"ignored\"," + Encoding.UTF8.GetString(Wrap(payload))[1..]));
    Add("invalid-format-before-checksum", Wrap("{", format: "wrong", hash: "wrong"));
    Add("invalid-schema1-before-checksum", Wrap("{", schema: 1, hash: "wrong"));
    Add("invalid-schema3-before-checksum", Wrap("{", schema: 3, hash: "wrong"));
    Add("invalid-negativeSequence-before-checksum", Wrap("{", -1, hash: "wrong"));
    Add("invalid-checksum-before-json", Wrap("{", hash: "wrong"));
    Add("invalid-uppercaseChecksum", Wrap(payload, hash: Convert.ToHexString(LogicalHash(payload, 0))));
    Add("invalid-nullPayload", Wrap(null));
    Add("invalid-emptyPayload", Wrap(""));
    Add("invalid-truncatedPayload", Wrap("{"));
    Add("invalid-nullState", Wrap("null"));
    Add("invalid-arrayState", Wrap("[]"));
    Add("invalid-stringState", Wrap("\"world\""));
    Add("invalid-innerBOM", Wrap("\uFEFF" + payload));
    Add("invalid-innerTrailingObject", Wrap(payload + "{}"));
    Add("invalid-innerTrailingNul", Wrap(payload + "\0"));
    Add("invalid-outerEmpty", []);
    Add("invalid-outerWhitespace", " \r\n"u8.ToArray());
    Add("invalid-outerNull", "null"u8.ToArray());
    Add("invalid-outerTruncated", "{"u8.ToArray());
    Add("invalid-outerBOM", [0xEF, 0xBB, 0xBF, .. Wrap(payload)]);
    Add("invalid-outerTrailingObject", [.. Wrap(payload), (byte)'{', (byte)'}']);
    Add("invalid-outerInvalidUtf8", [0xFF, .. Wrap(payload)]);
    Add("invalid-outerHighSurrogate", Encoding.UTF8.GetBytes("{\"Format\":\"SEONRO-021\",\"Schema\":2,\"Hash\":\"wrong\",\"Payload\":\"\\uD800\",\"Sequence\":0}"));
    Add("invalid-outerLowSurrogate", Encoding.UTF8.GetBytes("{\"Format\":\"SEONRO-021\",\"Schema\":2,\"Hash\":\"wrong\",\"Payload\":\"\\uDC00\",\"Sequence\":0}"));
    var node = JsonNode.Parse(payload)!;
    node["Events"]![0]!["Text"] = "__BAD_SURROGATE__";
    Add("invalid-innerEscapedHighSurrogate", Wrap(node.ToJsonString().Replace("\"__BAD_SURROGATE__\"", "\"\\uD800\"", StringComparison.Ordinal)));
    Add("invalid-innerEscapedLowSurrogate", Wrap(node.ToJsonString().Replace("\"__BAD_SURROGATE__\"", "\"\\uDC00\"", StringComparison.Ordinal)));
    string realRoot = "/workspace/scratch/knowledge-owned-db0feae-n100-month30-cpu1/diagnostic-partial-saves/population-2.seonro";
    foreach (var slot in new[] {
        new Input("real-N2-M90", realRoot + ".backup", "0e5903c307695ba4154b30123b9a178d8b9560f63931bda9808c803410f00f13", 90),
        new Input("real-N2-M120", realRoot, "374447ce41ce70a9bd0cbc17c57ece65023167931a715ef4f8aa484f960b03f6", 120) })
    {
        if (Sha(File.ReadAllBytes(slot.Path)) != slot.Sha256) throw new InvalidDataException("real slot SHA mismatch");
        items.Add(slot);
    }
    File.WriteAllBytes(output, JsonSerializer.SerializeToUtf8Bytes(items, new JsonSerializerOptions { WriteIndented = true }));
}
record Input(string Name, string Path, string Sha256, int? RealMonth);
record Envelope(string Format, int Schema, string Hash, string? Payload, long Sequence);
delegate (GameWorld World, long Sequence) Decode(ReadOnlySpan<byte> bytes);
delegate byte[] EncodeWithSequence(GameWorld world, long sequence);
sealed class PoolEvents : EventListener
{
    private bool capture;
    public List<object> Events { get; } = [];
    public void Begin() { Events.Clear(); capture = true; }
    public void End() => capture = false;
    protected override void OnEventSourceCreated(EventSource source)
    {
        if (source.Name == "System.Buffers.ArrayPoolEventSource") EnableEvents(source, EventLevel.Verbose);
    }
    protected override void OnEventWritten(EventWrittenEventArgs data)
    {
        if (!capture || data.Payload is null || data.Payload.Count < 2 || data.Payload[1] is not int size || size < 8 * 1024 * 1024) return;
        Events.Add(new { data.EventName, Size = size, Payload = data.Payload.ToArray() });
    }
}
