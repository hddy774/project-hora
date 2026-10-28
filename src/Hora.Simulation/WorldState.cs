// 결정론 검사에 필요한 최소 세계 상태와 월 전환을 소유한다.
// 관련 기획: docs/tasks/M0-04-month-pipeline.md
namespace Hora.Simulation
{
    /// <summary>
    /// 시뮬레이션의 최소 세계 상태. 정산 중에는 실행 중인 파이프라인만 접근한다.
    /// 시드, 월, 단계만 보유하며 실제 경제 테이블은 후속 작업에서 추가한다.
    /// </summary>
    public sealed class WorldState
    {
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;
        private const int BytesPerInt = 4;
        private const int BytesPerLong = 8;
        private const int BitsPerByte = 8;

        /// <summary>첫 계획 월(0개월)부터 시작하는 세계를 만든다.</summary>
        /// <param name="seed">독립 난수 스트림의 공통 개시 시드.</param>
        public WorldState(ulong seed)
        {
            Seed = seed;
            Phase = TurnPhase.Planning;
        }

        /// <summary>다음에 처리할 계획 월 번호(0부터, 단위: 개월).</summary>
        public int MonthIndex { get; internal set; }

        /// <summary>입력을 허용할지 결정하는 현재 월 처리 단계.</summary>
        public TurnPhase Phase { get; internal set; }

        /// <summary>생성 후 바뀌지 않는 세계 시드.</summary>
        public ulong Seed { get; }

        /// <summary>보고서가 열린 경우 다음 계획을 연다. 다른 단계에서는 상태를 유지한다.</summary>
        /// <returns>Report에서 Planning으로 전환했으면 true.</returns>
        public bool BeginPlanning()
        {
            if (Phase != TurnPhase.Report)
            {
                return false;
            }

            Phase = TurnPhase.Planning;
            return true;
        }

        /// <summary>
        /// 월 번호 4바이트, 단계 4바이트, 시드 8바이트를 순서대로 해시한다.
        /// 각 필드는 리틀 엔디언이며 FNV-1a 64를 사용한다. 실행 시간은 포함하지 않는다.
        /// </summary>
        /// <returns>현재 최소 세계 상태의 결정론적 64비트 해시.</returns>
        public ulong ComputeStateHash()
        {
            ulong hash = AppendBytes(FnvOffsetBasis, unchecked((uint)MonthIndex), BytesPerInt);
            hash = AppendBytes(hash, (uint)Phase, BytesPerInt);
            return AppendBytes(hash, Seed, BytesPerLong);
        }

        /// <summary>정해진 개수의 하위 바이트를 리틀 엔디언 순서로 FNV-1a에 반영한다.</summary>
        private static ulong AppendBytes(ulong hash, ulong value, int byteCount)
        {
            // [결정론] 플랫폼의 메모리 배치 대신 명시적인 바이트 순서를 사용한다.
            for (int index = 0; index < byteCount; index++)
            {
                hash = unchecked((hash ^ (byte)value) * FnvPrime);
                value >>= BitsPerByte;
            }

            return hash;
        }
    }
}
