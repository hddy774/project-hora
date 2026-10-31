using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Chronicles.Game;

namespace Chronicles.Game.Tests;

public sealed class CanonicalHashUnicodeScenario : IFeatureScenario
{
    public string Name => "INV-004/006 canonical streaming hash: 기존 UTF8 oracle·Unicode·문화권·상태/RNG 보존";

    public void Run()
    {
        var state = GameWorld.Create(42).Checkpoint();
        state.People[0].Skills = new(StringComparer.Ordinal)
        {
            ["I"] = 1,
            ["i"] = 2,
            ["İ"] = 3,
            ["ı"] = 4
        };
        string?[] names = ["", "한글·木🔥 e\u0301 é \"\\\n\r\t\0 <>&+ \u2028\u2029", "\uD800", "\uDC00", null];
        var previousCulture = CultureInfo.CurrentCulture;
        var randomBefore = state.RandomState;
        var nextIdBefore = state.NextId;
        try
        {
            foreach (var name in names)
            {
                state.People[0].Name = name!;
                var before = CanonicalHashFixture.BufferedBytes(state);
                var expected = CanonicalHashFixture.HashBytes(before);
                foreach (var culture in new[] { "en-US", "tr-TR", "ko-KR" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                    CanonicalHashFixture.Check(GameCodec.Hash(state) == expected,
                        "문화권·Unicode·escape/null의 기존 canonical 해시가 달라졌습니다.");
                }

                CanonicalHashFixture.Check(before.AsSpan().SequenceEqual(CanonicalHashFixture.BufferedBytes(state)),
                    "해시 조회가 상태를 변경했습니다.");
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }

        CanonicalHashFixture.Check(state.RandomState == randomBefore && state.NextId == nextIdBefore,
            "해시 조회가 RNG 또는 ID를 소비했습니다.");
    }
}

public sealed class CanonicalHashLargePayloadScenario : IFeatureScenario
{
    public string Name => "INV-004/006 canonical streaming hash: 실제 N100 고정 원본·큰 payload·반복 조회";

    public void Run()
    {
        var state = SyntheticPopulationFixture.Build(100);
        var bytes = CanonicalHashFixture.BufferedBytes(state);
        // df7240556a260202a248e57b1f9868e372b9d995의 수정 전 Release 진단에서 실제 수집한 값이다.
        const string recordedHash = "cd381300e01e8c00d6ae9ac8bc53fea08c55eaa74eca013ab7feca6bc9dea8f2";
        CanonicalHashFixture.Check(bytes.Length == 2544545 &&
            CanonicalHashFixture.HashBytes(bytes) == recordedHash, "기존 실제 N100 기준 입력이 달라졌습니다.");
        var randomBefore = state.RandomState;
        var nextIdBefore = state.NextId;
        for (var repeat = 0; repeat < 3; repeat++)
        {
            CanonicalHashFixture.Check(GameCodec.Hash(state) == recordedHash, "큰 payload의 canonical 해시가 다릅니다.");
        }

        state.People[0].Name = string.Concat(Enumerable.Repeat("한글🔥\"\\\n", 8192));
        var expanded = CanonicalHashFixture.BufferedBytes(state);
        CanonicalHashFixture.Check(expanded.Length > bytes.Length + 100000 &&
            GameCodec.Hash(state) == CanonicalHashFixture.HashBytes(expanded), "큰 escaped 문자열의 해시가 다릅니다.");
        CanonicalHashFixture.Check(expanded.AsSpan().SequenceEqual(CanonicalHashFixture.BufferedBytes(state)) &&
            state.RandomState == randomBefore && state.NextId == nextIdBefore,
            "큰 payload를 해시하며 상태·RNG·ID를 변경했습니다.");
    }
}

public sealed class CanonicalHashFailureScenario : IFeatureScenario
{
    public string Name => "INV-004/006 canonical streaming hash: null·직렬화 실패·실패 후 새 호출의 기존 계약";

    public void Run()
    {
        const string nullHash = "74234e98afe7498fb5daf1f36ac2d78acc339464f950703b8c019892f982b90b";
        CanonicalHashFixture.Check(GameCodec.Hash(null!) == nullHash &&
            CanonicalHashFixture.HashBytes(CanonicalHashFixture.BufferedBytes(null)) == nullHash,
            "null 입력의 기존 JSON null 해시가 달라졌습니다.");
        CanonicalHashFixture.Check(GameCodec.Hash(new GameState()) ==
            CanonicalHashFixture.HashBytes(CanonicalHashFixture.BufferedBytes(new GameState())),
            "빈 상태의 기존 직렬화 계약이 달라졌습니다.");

        var state = GameWorld.Create(997).Checkpoint();
        state.People[0].Name = new string('a', 64000);
        var before = CanonicalHashFixture.BufferedBytes(state);
        var randomBefore = state.RandomState;
        state.People[0].Decisions.Add(new(0, "검사", "직렬화 중단", "", 0, new InterruptedCandidates()));
        var bufferedFailure = CanonicalHashFixture.Failure(() => CanonicalHashFixture.BufferedBytes(state));
        var streamedFailure = CanonicalHashFixture.Failure(() => GameCodec.Hash(state));
        CanonicalHashFixture.Check(bufferedFailure.GetType() == streamedFailure.GetType() &&
            bufferedFailure.Message == streamedFailure.Message && state.RandomState == randomBefore,
            "직렬화 실패의 기존 예외 또는 RNG 계약이 달라졌습니다.");

        state.People[0].Decisions.Clear();
        CanonicalHashFixture.Check(GameCodec.Hash(state) == CanonicalHashFixture.HashBytes(before) &&
            before.AsSpan().SequenceEqual(CanonicalHashFixture.BufferedBytes(state)),
            "실패한 호출의 부분 출력이 다음 해시 또는 상태에 남았습니다.");
    }

    private sealed class InterruptedCandidates : IReadOnlyList<DecisionCandidate>
    {
        public int Count => 2;
        public DecisionCandidate this[int index] => index == 0
            ? new("첫 후보", 1, "실제 열거 전 중단하지 않음")
            : throw new InvalidOperationException("의도된 직렬화 열거 실패");

        public IEnumerator<DecisionCandidate> GetEnumerator()
        {
            yield return this[0];
            throw new InvalidOperationException("의도된 직렬화 열거 실패");
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

internal static class CanonicalHashFixture
{
    // 수정 전 buffered GameCodec.Hash의 serializer 계약을 독립 oracle로 고정한다.
    private static readonly JsonSerializerOptions LegacyOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        MaxDepth = 64
    };

    internal static byte[] BufferedBytes(GameState? state) => JsonSerializer.SerializeToUtf8Bytes(state, LegacyOptions);
    internal static string HashBytes(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    internal static InvalidOperationException Failure(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException error)
        {
            return error;
        }

        throw new InvalidOperationException("직렬화 실패 입력이 거부되지 않았습니다.");
    }

    internal static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
