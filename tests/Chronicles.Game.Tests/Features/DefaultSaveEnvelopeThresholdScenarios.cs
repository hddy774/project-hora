using System.Globalization;
using System.Text.Json;
using Chronicles.Game;

namespace Chronicles.Game.Tests;

public sealed class DefaultSaveEnvelopeThresholdScenario : IFeatureScenario
{
    public string Name => "INV-004/006/022 저장 봉투 기본 경계: 16MiB 양쪽의 기존 JSON·이진·원자파일 호환";

    public void Run()
    {
        const int boundary = 16 * 1024 * 1024;
        const long sequence = 11;
        var initial = GameWorld.Create(191).Checkpoint();
        var original = initial.Events[0];
        var originalSize = JsonSerializer.SerializeToUtf8Bytes(initial, LargeSaveFixture.Options).Length;
        var oldTextSize = JsonSerializer.SerializeToUtf8Bytes(original.Text, LargeSaveFixture.Options).Length - 2;
        var padding = boundary - (originalSize - oldTextSize);
        LargeSaveFixture.Check(padding > 0, "경계 fixture의 본문이 기본 경계를 이미 넘었습니다.");

        for (var extra = 0; extra <= 1; extra++)
        {
            // 기존 사건의 텍스트만 늘린 저장 fixture다. 인구·전이·게임 규칙을 보정하지 않는다.
            initial.Events[0] = original with { Text = new string('x', padding + extra) };
            var world = GameWorld.Restore(initial);
            var payload = JsonSerializer.SerializeToUtf8Bytes(world.Checkpoint(), LargeSaveFixture.Options);
            LargeSaveFixture.Check(payload.Length == boundary + extra, "실제 canonical 본문 경계가 다릅니다.");
            var hash = world.StateHash();
            var random = world.Checkpoint().RandomState;
            var encoded = GameCodec.Encode(world, sequence);

            if (extra == 0)
            {
                LargeSaveFixture.Check(encoded[0] == '{', "16MiB 본문은 기존 JSON 봉투를 유지해야 합니다.");
                LargeSaveFixture.Check(encoded.AsSpan().SequenceEqual(LargeSaveFixture.Legacy(world, sequence)),
                    "경계 이하의 기존 JSON 바이트가 달라졌습니다.");
            }
            else
            {
                LargeSaveFixture.Check(encoded.AsSpan().StartsWith("SEONRO-021B\0"u8),
                    "16MiB를 넘으면 기존 이진 봉투를 선택해야 합니다.");
                LargeSaveFixture.Check(encoded.AsSpan(64).SequenceEqual(payload), "이진 본문이 canonical 원문과 다릅니다.");
                var legacy = LargeSaveFixture.Legacy(world, sequence);
                LargeSaveFixture.Check(GameCodec.DecodeEnvelope(legacy).World.StateHash() == hash,
                    "새 경계보다 큰 기존 JSON 저장을 읽지 못합니다.");
                VerifyAtomicFile(world, hash);
            }

            var restored = GameCodec.DecodeEnvelope(encoded);
            LargeSaveFixture.Check(restored.Sequence == sequence && restored.World.StateHash() == hash,
                "형식 선택 뒤 순번 또는 상태 해시가 달라졌습니다.");
            LargeSaveFixture.Check(world.StateHash() == hash && restored.World.Checkpoint().RandomState == random,
                "저장·복원 과정에서 원본 세계 또는 RNG가 달라졌습니다.");
        }
    }

    private static void VerifyAtomicFile(GameWorld world, string hash)
    {
        var directory = Path.Combine(Path.GetTempPath(), "seonro-default-save-" +
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var path = Path.Combine(directory, "world.save");
        try
        {
            GameSaveStore.SaveAtomic(world, path);
            LargeSaveFixture.Check(File.ReadAllBytes(path).AsSpan().StartsWith("SEONRO-021B\0"u8),
                "기본 원자 저장 경로가 새 형식 선택을 사용하지 않습니다.");
            LargeSaveFixture.Check(GameSaveStore.Load(path).StateHash() == hash, "기본 이진 원자파일 왕복이 다릅니다.");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
