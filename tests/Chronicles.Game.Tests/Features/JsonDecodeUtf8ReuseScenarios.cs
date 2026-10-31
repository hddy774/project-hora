using System.Text;
using System.Text.Json;
using Chronicles.Game;

namespace Chronicles.Game.Tests;

/// <summary>체크섬에 쓴 UTF8 본문을 재사용해도 공개 저장 복원의 상태·소유 경계가 유지되는지 검사한다.</summary>
public sealed class JsonDecodeUtf8ReuseWorldScenario : IFeatureScenario
{
    public string Name => "INV-004/006 JSON Decode UTF8 재사용: Unicode·순번·원문·RNG·공개 사본 격리";

    public void Run()
    {
        var state = GameWorld.Create(412).Checkpoint();
        state.Events[0] = state.Events[0] with { Text = "한글🔥 e\u0301 é \"\\\n\t\0 <>&+ \u2028" };
        var world = GameWorld.Restore(state);
        string before = world.StateHash();
        ulong random = world.Checkpoint().RandomState;
        foreach (long sequence in new[] { 0L, 7, long.MaxValue })
        {
            byte[] bytes = LargeSaveFixture.Legacy(world, sequence);
            byte[] original = (byte[])bytes.Clone();
            var decoded = GameCodec.DecodeEnvelope(bytes);
            LargeSaveFixture.Check(decoded.Sequence == sequence && decoded.World.StateHash() == before &&
                decoded.World.Checkpoint().RandomState == random, "복원의 순번·세계·RNG가 달라졌습니다.");
            LargeSaveFixture.Check(bytes.AsSpan().SequenceEqual(original) &&
                GameCodec.Encode(decoded.World, sequence).AsSpan().SequenceEqual(original),
                "원문 또는 같은 세계의 저장 바이트가 달라졌습니다.");
            var detached = decoded.World.Checkpoint();
            detached.Events[0] = detached.Events[0] with { Text = "외부 사본 변경" };
            detached.Knowledge.Works.Clear();
            LargeSaveFixture.Check(decoded.World.StateHash() == before && world.StateHash() == before,
                "공개 복원 또는 Checkpoint가 가변 상태를 공유합니다.");
        }
    }
}

/// <summary>봉투 검증·체크섬·본문 JSON 오류의 기존 거부 순서를 유지한다.</summary>
public sealed class JsonDecodeUtf8ReuseRejectionScenario : IFeatureScenario
{
    public string Name => "INV-004/006 JSON Decode UTF8 재사용: 손상·체크섬·BOM·trailing 거부 우선순위";

    public void Run()
    {
        const string format = "0.2.1 저장 형식이 아닙니다. 원본은 보존됩니다.";
        const string checksum = "저장 체크섬이 다릅니다.";
        const string json = "저장 JSON이 손상되었습니다.";
        Reject(JsonDecodeUtf8Fixture.Wrap("{", format: "wrong", hash: "wrong"), format);
        Reject(JsonDecodeUtf8Fixture.Wrap("{", schema: 3, hash: "wrong"), format);
        Reject(JsonDecodeUtf8Fixture.Wrap("{", sequence: -1, hash: "wrong"), format);
        Reject(JsonDecodeUtf8Fixture.Wrap("{", hash: "wrong"), checksum);
        Reject(JsonDecodeUtf8Fixture.Wrap("{"), json);
        Reject(JsonDecodeUtf8Fixture.Wrap("null"), "빈 세계입니다.");
        Reject(JsonDecodeUtf8Fixture.Wrap("\uFEFF{}"), json);
        Reject(JsonDecodeUtf8Fixture.Wrap("{} {}"), json);
        Reject("{} {}"u8.ToArray(), json);
        Reject([0xEF, 0xBB, 0xBF, (byte)'{', (byte)'}'], json);

        byte[] encoded = GameCodec.Encode(GameWorld.Create(412));
        using var document = JsonDocument.Parse(encoded);
        string payload = document.RootElement.GetProperty("Payload").GetString()!;
        string expected = GameCodec.Decode(encoded).StateHash();
        foreach (string whitespace in new[] { " ", "\r\n\t" })
        {
            var actual = GameCodec.Decode(JsonDecodeUtf8Fixture.Wrap(payload + whitespace));
            LargeSaveFixture.Check(actual.StateHash() == expected, "허용하던 JSON 후행 공백이 거부됐습니다.");
        }
    }

    private static void Reject(byte[] bytes, string expected)
    {
        try
        {
            GameCodec.Decode(bytes);
        }
        catch (InvalidDataException exception)
        {
            LargeSaveFixture.Check(exception.Message == expected, "기존 저장 거부 사유의 우선순위가 달라졌습니다.");
            return;
        }

        throw new InvalidOperationException("기존에 거부한 저장을 수락했습니다.");
    }
}

internal static class JsonDecodeUtf8Fixture
{
    private sealed record Envelope(string Format, int Schema, string Hash, string Payload, long Sequence);

    internal static byte[] Wrap(string payload, long sequence = 0, string format = "SEONRO-021", int schema = 2,
        string? hash = null)
    {
        string checksum = hash ?? Convert.ToHexStringLower(LargeSaveFixture.LogicalHash(Encoding.UTF8.GetBytes(payload),
            sequence));
        return JsonSerializer.SerializeToUtf8Bytes(new Envelope(format, schema, checksum, payload, sequence));
    }
}
