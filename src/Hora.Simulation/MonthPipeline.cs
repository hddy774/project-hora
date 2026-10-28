// [핫 패스] 고정 순서의 시스템을 월마다 한 번 실행하며 컨텍스트와 결과 버퍼를 재사용한다.
// 관련 기획: docs/tasks/M0-04-month-pipeline.md
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Hora.Simulation
{
    /// <summary>
    /// 단일 실행 스레드용 월 정산기. 시스템 등록 이후 구성은 바뀌지 않는다.
    /// 세계 복사와 매월 관리 힙 할당을 피하며 같은 인스턴스의 재진입을 거절한다.
    /// </summary>
    public sealed class MonthPipeline
    {
        private const long MicrosecondsPerSecond = 1000000;

        private readonly ISimulationSystem[] _systems;
        private readonly string[] _systemNames;
        private readonly SystemTiming[] _timings;
        private readonly MonthContext _context;
        private readonly ResolveResult _success;
        private readonly ResolveResult _failure;
        private bool _isResolving;

        /// <summary>시스템 목록을 복사하여 등록 순서와 이름을 고정한다.</summary>
        /// <param name="systems">0~1000개 시스템. 기존 인덱스를 유지하려면 끝에 추가한다.</param>
        /// <exception cref="ArgumentNullException">목록이 null이다.</exception>
        /// <exception cref="ArgumentException">항목 또는 이름이 null이거나 1000개를 넘는다.</exception>
        public MonthPipeline(IReadOnlyList<ISimulationSystem> systems)
        {
            ValidateSystems(systems);
            _systems = new ISimulationSystem[systems.Count];
            _systemNames = new string[systems.Count];
            _timings = new SystemTiming[systems.Count];
            _context = new MonthContext(systems.Count);
            _success = new ResolveResult(true, Array.AsReadOnly(_timings));
            _failure = new ResolveResult(false, Array.Empty<SystemTiming>());
            for (int index = 0; index < systems.Count; index++)
            {
                _systems[index] = systems[index];
                _systemNames[index] = systems[index].Name;
            }
        }

        /// <summary>
        /// Planning에서만 한 달을 처리한다. 성공 결과는 다음 허용된 실행 전까지만 유효하다.
        /// 시스템 또는 진행률 콜백이 예외를 던지면 전파하고 세계를 Resolving으로 남긴다.
        /// 이는 원자적 저장·복구 기능이 아니며 후속 저장 계층에서 복구해야 한다.
        /// </summary>
        /// <param name="world">단독 접근할 세계. null이면 인수 오류.</param>
        /// <param name="progress">각 시스템 완료 직후 한 번 호출. null이면 진행률 할당이 없다.</param>
        /// <returns>성공 결과 또는 단계 오류·재진입을 나타내는 실패 결과.</returns>
        public ResolveResult Resolve(WorldState world, IProgress<PipelineProgress>? progress = null)
        {
            if (world is null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (_isResolving || world.Phase != TurnPhase.Planning)
            {
                return _failure;
            }

            int nextMonth = checked(world.MonthIndex + 1);
            _context.Reset(world.Seed, world.MonthIndex);
            _isResolving = true;
            world.Phase = TurnPhase.Resolving;
            try
            {
                ExecuteSystems(world, progress);
                world.MonthIndex = nextMonth;
                world.Phase = TurnPhase.Report;
                return _success;
            }
            finally
            {
                // 예외 뒤 세계는 실패 지점에 남긴다. 다른 세계를 처리할 인스턴스 잠금만 푼다.
                _isResolving = false;
            }
        }

        /// <summary>완료한 단계만 측정하고 보고하며 중간 경제 상태를 복사하지 않는다.</summary>
        private void ExecuteSystems(WorldState world, IProgress<PipelineProgress>? progress)
        {
            for (int index = 0; index < _systems.Length; index++)
            {
                long started = Stopwatch.GetTimestamp();
                _systems[index].Execute(world, _context);
                long elapsed = Stopwatch.GetTimestamp() - started;
                _timings[index] = new SystemTiming(_systemNames[index], ToMicroseconds(elapsed));
                progress?.Report(new PipelineProgress(index + 1, _systems.Length, _systemNames[index]));
            }
        }

        private static long ToMicroseconds(long elapsedTicks)
        {
            long wholeSeconds = elapsedTicks / Stopwatch.Frequency;
            long remainingTicks = elapsedTicks % Stopwatch.Frequency;
            return checked((wholeSeconds * MicrosecondsPerSecond) +
                (remainingTicks * MicrosecondsPerSecond / Stopwatch.Frequency));
        }

        /// <summary>월별 난수 충돌과 실행 중 null 참조를 등록 시점에 방지한다.</summary>
        private static void ValidateSystems(IReadOnlyList<ISimulationSystem> systems)
        {
            if (systems is null)
            {
                throw new ArgumentNullException(nameof(systems));
            }

            if (systems.Count > MonthContext.StreamStride)
            {
                throw new ArgumentException("시스템은 월별 난수 구간 1000개를 넘을 수 없습니다.", nameof(systems));
            }

            for (int index = 0; index < systems.Count; index++)
            {
                if (systems[index] is null || systems[index].Name is null)
                {
                    throw new ArgumentException("시스템과 이름은 null일 수 없습니다.", nameof(systems));
                }
            }
        }
    }
}
