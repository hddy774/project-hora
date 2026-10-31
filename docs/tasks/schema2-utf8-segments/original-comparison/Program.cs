using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

const string beforeSource = "5f789c817eb9519b696acd72410643169ff11ef2";
string afterSource = args[0];
string before = "/workspace/seonro-integration-5f789c8/tests/Chronicles.Game.Tests/bin/Release/net10.0";
string after = "/workspace/seonro-schema2-segments-5f789c8/tests/Chronicles.Game.Tests/bin/Release/net10.0";
var baseline = Capture(before, beforeSource);
var candidate = Capture(after, afterSource);
if (baseline.Bytes.Count != candidate.Bytes.Count || baseline.Bytes.Where((b, i) => !b.AsSpan().SequenceEqual(candidate.Bytes[i])).Any())
    throw new InvalidDataException("Original 5f Release DLL and candidate encoded bytes differ.");
File.WriteAllText(args[1], JsonSerializer.Serialize(new { beforeSource, afterSource, original = baseline.Identities,
    candidate = candidate.Identities, actualOldDllByteComparisons = baseline.Rows, allOldDllAndLegacyOracleBytesEqual = true,
    scope = "Independent functional byte compatibility only; no advancement or performance measurement" },
    new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("Actual original 5f DLL / candidate / original Legacy fixture byte equality: " + baseline.Bytes.Count);

static (List<byte[]> Bytes, object[] Rows, object[] Identities) Capture(string path, string source)
{
    using var context = new ScopedContext(path);
    var tests = context.LoadFromAssemblyPath(Path.Combine(path, "Chronicles.Game.Tests.dll"));
    var game = context.LoadFromAssemblyPath(Path.Combine(path, "Chronicles.Game.dll"));
    var identities = new[] { "Chronicles.Core", "Chronicles.Game", "Chronicles.Hosting", "Chronicles.Game.Tests" }
        .Select(name =>
        {
            var assembly = context.LoadFromAssemblyPath(Path.Combine(path, name + ".dll"));
            string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            string? configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration;
            if (informational != "0.2.1+" + source || configuration != "Release")
                throw new InvalidDataException("Exact original/candidate source or configuration mismatch");
            return (object)new { name, informational, configuration,
                sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location))) };
        }).ToArray();
    var worldType = game.GetType("Chronicles.Game.GameWorld", true)!;
    var codec = game.GetType("Chronicles.Game.GameCodec", true)!;
    var encode = codec.GetMethod("Encode", BindingFlags.NonPublic | BindingFlags.Static,
        null, [worldType, typeof(long), typeof(int)], null)!;
    var legacy = tests.GetType("Chronicles.Game.Tests.LargeSaveFixture", true)!
        .GetMethod("Legacy", BindingFlags.NonPublic | BindingFlags.Static)!;
    var bytes = new List<byte[]>();
    var rows = new List<object>();
    var prior = CultureInfo.CurrentCulture;
    try
    {
        foreach (string culture in new[] { "en-US", "tr-TR" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            foreach (string? text in new string?[] { null, new string('x', 16383) + "한글🔥\"\\\n<&>" })
            {
                object world = worldType.GetMethod("Create")!.Invoke(null, [191UL, null])!;
                if (text is not null)
                {
                    object state = worldType.GetMethod("Checkpoint")!.Invoke(world, null)!;
                    var events = (IList)state.GetType().GetProperty("Events")!.GetValue(state)!;
                    object original = events[0]!;
                    var constructor = original.GetType().GetConstructors().Single(c => c.GetParameters().Length == 8);
                    object?[] values = constructor.GetParameters().Select(p => p.Name == "Text" ? text :
                        original.GetType().GetProperty(p.Name!)!.GetValue(original)).ToArray();
                    events[0] = constructor.Invoke(values);
                    world = worldType.GetMethod("Restore")!.Invoke(null, [state])!;
                }
                foreach (long sequence in new[] { 0L, 7, long.MaxValue })
                {
                    var actual = (byte[])encode.Invoke(null, [world, sequence, int.MaxValue])!;
                    var expected = (byte[])legacy.Invoke(null, [world, sequence, 2])!;
                    if (!actual.AsSpan().SequenceEqual(expected)) throw new InvalidDataException("Same DLL Legacy oracle differs");
                    bytes.Add(actual);
                    rows.Add(new { culture, text = text is null ? "original" : "boundary Unicode", sequence,
                        length = actual.Length, sha256 = Convert.ToHexStringLower(SHA256.HashData(actual)) });
                }
            }
        }
    }
    finally { CultureInfo.CurrentCulture = prior; }
    return (bytes, rows.ToArray(), identities);
}

sealed class ScopedContext(string directory) : AssemblyLoadContext(isCollectible: true), IDisposable
{
    protected override Assembly? Load(AssemblyName name)
    {
        string path = Path.Combine(directory, name.Name + ".dll");
        return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
    }
    public void Dispose() => Unload();
}
