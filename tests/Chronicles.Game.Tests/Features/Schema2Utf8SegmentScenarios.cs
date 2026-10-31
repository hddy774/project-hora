using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Chronicles.Game;

namespace Chronicles.Game.Tests;

public sealed class Schema2SegmentWorldCompatibilityScenario : IFeatureScenario
{
    public string Name => "INV-004/006 schema2 UTF8 segments: 실제 세계·Unicode·문화권·순번의 기존 전체 바이트";

    public void Run()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "en-US", "tr-TR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var initial = GameWorld.Create(191).Checkpoint();
                var firstEvent = initial.Events[0];
                foreach (var text in new[] { "", "한글🔥 e\u0301 é \"\\\n\t\0 <>&+ \u2028\uD800", new string('x', 32767) })
                {
                    initial.Events[0] = firstEvent with { Text = text };
                    var world = GameWorld.Restore(initial);
                    VerifyWorld(world);
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static void VerifyWorld(GameWorld world)
    {
        var stateHash = world.StateHash();
        var random = world.Checkpoint().RandomState;
        foreach (long sequence in new[] { 0L, 7, long.MaxValue })
        {
            var before = LargeSaveFixture.Legacy(world, sequence);
            var actual = GameCodec.Encode(world, sequence);
            LargeSaveFixture.Check(actual.AsSpan().SequenceEqual(before),
                "실제 세계의 기존 schema2 전체 바이트가 달라졌습니다.");
            var decoded = GameCodec.DecodeEnvelope(actual);
            LargeSaveFixture.Check(decoded.Sequence == sequence && decoded.World.StateHash() == stateHash &&
                decoded.World.Checkpoint().RandomState == random && world.StateHash() == stateHash,
                "문자열 쓰기가 순번·상태·RNG 또는 원본을 변경했습니다.");
        }
    }
}

public sealed class Schema2SegmentBoundaryCompatibilityScenario : IFeatureScenario
{
    public string Name => "INV-004/006 schema2 UTF8 segments: 빈 값·16KiB 경계의 UTF8 문자와 escape 연속성";

    private static readonly string[] PropertyOrder = ["Format", "Schema", "Hash", "Payload", "Sequence"];
    private delegate byte[] EncodeEnvelope(ReadOnlySpan<byte> payload, string hash, long sequence);
    private sealed record LegacyEnvelope(string Format, int Schema, string Hash, string Payload, long Sequence);

    public void Run()
    {
        // private 포맷 helper를 읽기 delegate로 호출한다. 제품에 테스트 seam이나 새 API를 추가하지 않는다.
        var encode = typeof(GameCodec).GetMethod("EncodeJsonEnvelope", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<EncodeEnvelope>();
        Verify(encode, []);
        foreach (int length in new[] { 1, 16383, 16384, 16385 })
        {
            Verify(encode, Encoding.UTF8.GetBytes(new string('a', length)));
        }

        foreach (int start in new[] { 16381, 16382, 16383, 16384, 16385 })
        {
            // 유효한 UTF8 문자를 경계에 걸친다. 실제 serializer의 Unicode escape 입력과도 별개로 대조한다.
            Verify(encode, Encoding.UTF8.GetBytes(new string('x', start) + "🔥한글\"\\\n<&>"));
            Verify(encode, Encoding.UTF8.GetBytes(new string('x', start) + "\\uD55C\\uAE00\""));
        }
    }

    private static void Verify(EncodeEnvelope encode, byte[] payload)
    {
        foreach (long sequence in new[] { 0L, long.MaxValue })
        {
            var hash = Convert.ToHexStringLower(LargeSaveFixture.LogicalHash(payload, sequence));
            // 수정 전 schema2 serializer를 oracle로 유지한다. segment writer로 기대값을 만들지 않는다.
            var before = JsonSerializer.SerializeToUtf8Bytes(new LegacyEnvelope("SEONRO-021", 2, hash,
                Encoding.UTF8.GetString(payload), sequence), LargeSaveFixture.Options);
            var actual = encode(payload, hash, sequence);
            LargeSaveFixture.Check(actual.AsSpan().SequenceEqual(before),
                "segment 경계 또는 UTF8/escape 처리에서 기존 schema2 바이트가 달라졌습니다.");
            using var document = JsonDocument.Parse(actual);
            var properties = document.RootElement.EnumerateObject().Select(property => property.Name);
            LargeSaveFixture.Check(properties.SequenceEqual(PropertyOrder),
                "기존 봉투의 property 순서가 달라졌습니다.");
        }
    }
}
