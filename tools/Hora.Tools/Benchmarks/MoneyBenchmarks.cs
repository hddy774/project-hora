// 정수 금액 계산·배분의 실행 시간과 관리 힙 할당을 독립 측정한다.
// 관련 명세: docs/tasks/M0-02-money-quantity.md
using BenchmarkDotNet.Attributes;
using Hora.Domain.Values;

namespace Hora.Tools.Benchmarks
{
    /// <summary>BenchmarkDotNet이 파생 실행 코드를 만드는 금액 벤치마크.</summary>
    [MemoryDiagnoser]
    public class MoneyBenchmarks
    {
        private readonly Money _unitPrice = Money.FromCents(1200000000000);
        private readonly Quantity _quantity = Quantity.FromUnits(17000000);
        private long[] _weights = System.Array.Empty<long>();
        private Money[] _output = System.Array.Empty<Money>();

        /// <summary>비례 배분을 받을 계정 수(개). 입력 규모별 비용을 따로 측정한다.</summary>
        [Params(3, 32)]
        public int RecipientCount { get; set; }

        /// <summary>측정 전 재사용할 입력과 출력 배열을 준비한다.</summary>
        [GlobalSetup]
        public void SetUp()
        {
            _weights = new long[RecipientCount];
            _output = new Money[RecipientCount];
            for (int index = 0; index < RecipientCount; index++)
            {
                _weights[index] = index + 1;
            }
        }

        /// <summary>64비트 중간 곱을 넘는 금액·수량을 정확히 나누는 비용을 측정한다.</summary>
        /// <returns>최소 수량 단위로 계산한 매입액(센트).</returns>
        [Benchmark]
        public Money Multiply() =>
            MoneyMath.Multiply(_unitPrice, _quantity, 1000);

        /// <summary>음수 총액을 최대 나머지 규칙으로 보존 배분하는 비용을 측정한다.</summary>
        [Benchmark]
        public void Allocate() => MoneyMath.Allocate(Money.FromCents(-1234567), _weights, _output);
    }
}
