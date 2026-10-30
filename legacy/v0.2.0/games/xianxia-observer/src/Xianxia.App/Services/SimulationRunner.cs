// 시뮬레이션 실행기: 별도 스레드에서 정해진 속도(년/초)로 세계를 진행한다.
// [결정론] 세계는 이 실행기의 잠금 안에서만 바뀌고 화면은 같은 잠금 안에서 읽기만 한다. 진행은 시드로만 정해진다
// (관찰자 개입은 해와 해 사이에 들어가는 입력이다).
// [성능] 잠금은 해 묶음(최대 약 8ms) 단위로 잡고 풀어, 화면 갱신(초당 4번)이 오래 기다리지 않게 한다.
using System;
using System.Diagnostics;
using System.Threading;
using Xianxia.Simulation;
using Xianxia.Simulation.Model;

namespace Xianxia.App.Services
{
    /// <summary>시뮬레이션 실행기.</summary>
    internal sealed class SimulationRunner
    {
        /// <summary>속도 표에서 "최대"를 뜻하는 값.</summary>
        internal const int MaxSpeed = 0;

        /// <summary>처음 속도 위치(20년/초).</summary>
        internal const int DefaultSpeedIndex = 2;

        private const double SliceSeconds = 0.008;
        private static readonly int[] s_speeds = { 1, 5, 20, 100, 500, MaxSpeed };
        private readonly object _gate = new();
        private readonly TimeSpan _autosaveInterval;
        private readonly Action? _autosave;
        private World _world;
        private Thread? _thread;
        private bool _running;
        private bool _stopping;
        private bool _faulted;
        private long _lastTick;
        private long _windowStarted;
        private int _windowYears;
        private double _owed;
        private int _speedIndex = DefaultSpeedIndex;
        private long _version;
        private double _measuredYearsPerSecond;

        /// <summary>실행기를 만든다(스레드는 <see cref="Start"/>에서 시작).</summary>
        /// <param name="world">처음 세계.</param>
        /// <param name="autosave">주기적 자동 저장 동작(잠금 밖에서 호출).</param>
        /// <param name="autosaveInterval">자동 저장 간격.</param>
        internal SimulationRunner(World world, Action? autosave = null, TimeSpan? autosaveInterval = null)
        {
            _world = world;
            _autosave = autosave;
            _autosaveInterval = autosaveInterval ?? TimeSpan.FromSeconds(90);
        }

        /// <summary>속도 표(년/초, 0은 최대).</summary>
        internal static ReadOnlySpan<int> Speeds => s_speeds;

        /// <summary>진행 중인가.</summary>
        internal bool IsRunning
        {
            get
            {
                lock (_gate)
                {
                    return _running;
                }
            }
        }

        /// <summary>속도 위치.</summary>
        internal int SpeedIndex => Volatile.Read(ref _speedIndex);

        /// <summary>세계가 바뀔 때마다 오르는 번호(화면이 다시 그릴지 판단).</summary>
        internal long Version => Interlocked.Read(ref _version);

        /// <summary>최근 실제 진행 속도(년/초).</summary>
        internal double MeasuredYearsPerSecond => Volatile.Read(ref _measuredYearsPerSecond);

        /// <summary>진행 중 오류(있으면 멈춘 상태).</summary>
        internal string? Error { get; private set; }

        /// <summary>부분 처리된 세계는 Replace 전까지 진행·저장할 수 없다.</summary>
        internal bool IsFaulted
        {
            get
            {
                lock (_gate)
                {
                    return _faulted;
                }
            }
        }

        /// <summary>시험 전용 Step 직전 동작. 부분 변경 뒤 오류도 재현할 수 있다.</summary>
        internal Action<World>? BeforeStepForTest { get; set; }

        /// <summary>일회 수명의 실행 스레드를 시작한다. Stop 뒤 재사용은 허용하지 않는다.</summary>
        internal void Start()
        {
            lock (_gate)
            {
                if (_stopping)
                {
                    throw new InvalidOperationException("실행기는 한 번만 시작할 수 있습니다.");
                }

                if (_thread is not null)
                {
                    return;
                }

                _thread = new Thread(Loop) { IsBackground = true, Name = "Xianxia simulation" };
                _thread.Start();
            }
        }

        /// <summary>스레드를 끝낸다.</summary>
        internal void Stop()
        {
            lock (_gate)
            {
                _stopping = true;
                _running = false;
                Monitor.PulseAll(_gate);
            }

            _thread?.Join(TimeSpan.FromSeconds(2));
            _thread = null;
        }

        /// <summary>진행·멈춤을 바꾼다.</summary>
        /// <param name="running">진행하려면 true.</param>
        internal void SetRunning(bool running)
        {
            lock (_gate)
            {
                if (running && _faulted)
                {
                    return;
                }

                _running = running;
                Monitor.PulseAll(_gate);
            }
        }

        /// <summary>속도를 바꾼다.</summary>
        /// <param name="index">속도 표 위치.</param>
        internal void SetSpeed(int index)
        {
            Volatile.Write(ref _speedIndex, Math.Clamp(index, 0, s_speeds.Length - 1));
        }

        /// <summary>잠금 안에서 세계를 읽는다.</summary>
        /// <typeparam name="T">결과 형식.</typeparam>
        /// <param name="reader">읽는 동작.</param>
        /// <returns>결과.</returns>
        internal T Read<T>(Func<World, T> reader)
        {
            lock (_gate)
            {
                return reader(_world);
            }
        }

        /// <summary>잠금 안에서 세계를 읽는다(결과 없음).</summary>
        /// <param name="reader">읽는 동작.</param>
        internal void Read(Action<World> reader)
        {
            lock (_gate)
            {
                reader(_world);
            }
        }

        /// <summary>잠금 안에서 세계를 바꾼다(관찰자 개입·관심 표시).</summary>
        /// <typeparam name="T">결과 형식.</typeparam>
        /// <param name="writer">바꾸는 동작.</param>
        /// <returns>결과.</returns>
        internal T Write<T>(Func<World, T> writer)
        {
            lock (_gate)
            {
                T result = writer(_world);
                Interlocked.Increment(ref _version);
                return result;
            }
        }

        /// <summary>세계를 통째로 바꾼다(새 세계·불러오기).</summary>
        /// <param name="world">새 세계.</param>
        internal void Replace(World world)
        {
            lock (_gate)
            {
                _running = false;
                _world = world;
                _faulted = false;
                Error = null;
                Interlocked.Increment(ref _version);
            }
        }

        /// <summary>정해진 햇수만큼 즉시 진행한다(시험·디버그용).</summary>
        /// <param name="years">햇수.</param>
        internal void Advance(int years)
        {
            lock (_gate)
            {
                if (_faulted)
                {
                    return;
                }

                WorldSimulator.Run(_world, years);
                Interlocked.Increment(ref _version);
            }
        }

        private void Loop()
        {
            long lastSave = Stopwatch.GetTimestamp();
            long savedVersion = Version;
            _lastTick = lastSave;
            _windowStarted = lastSave;
            while (WaitForActivity())
            {
                if (IsRunning)
                {
                    Tick();
                }

                TryAutosave(ref lastSave, ref savedVersion);
            }
        }

        /// <summary>정지 중에도 주기적으로 깨어나 변경된 관찰 상태를 저장한다.</summary>
        private bool WaitForActivity()
        {
            lock (_gate)
            {
                if (!_running && !_stopping)
                {
                    Monitor.Wait(_gate, TimeSpan.FromSeconds(1));
                    _lastTick = Stopwatch.GetTimestamp();
                    _windowStarted = _lastTick;
                    _windowYears = 0;
                    _owed = 0;
                }

                return !_stopping;
            }
        }

        /// <summary>속도 예산을 계산하고 짧은 해 묶음으로 진행한다.</summary>
        private void Tick()
        {
            long now = Stopwatch.GetTimestamp();
            int speed = s_speeds[SpeedIndex];
            double elapsed = Stopwatch.GetElapsedTime(_lastTick, now).TotalSeconds;
            _lastTick = now;
            int budget = int.MaxValue;
            if (speed != MaxSpeed)
            {
                // 재개 직후 밀린 해를 한꺼번에 처리하지 않는다.
                _owed = Math.Min(_owed + (elapsed * speed), Math.Max(1, speed * 0.2));
                budget = (int)_owed;
                if (budget == 0)
                {
                    Thread.Sleep(Math.Clamp((int)Math.Ceiling((1 - _owed) / speed * 1000), 1, 50));
                    return;
                }
            }

            int done = RunSlice(budget, now);
            _owed = Math.Max(0, _owed - done);
            _windowYears += done;
            double window = Stopwatch.GetElapsedTime(_windowStarted).TotalSeconds;
            if (window >= 1)
            {
                Volatile.Write(ref _measuredYearsPerSecond, _windowYears / window);
                _windowStarted = Stopwatch.GetTimestamp();
                _windowYears = 0;
            }

            Thread.Sleep(1);
        }

        /// <summary>호출은 세계 잠금 밖에서 수행한다. 저장 경로가 오류 여부를 다시 검사한다.</summary>
        private void TryAutosave(ref long lastSave, ref long savedVersion)
        {
            if (_autosave is null || IsFaulted || Version == savedVersion
                || Stopwatch.GetElapsedTime(lastSave) < _autosaveInterval)
            {
                return;
            }

            lastSave = Stopwatch.GetTimestamp();
            savedVersion = Version;
            _autosave();
        }

        private int RunSlice(int maxYears, long started)
        {
            int done = 0;
            lock (_gate)
            {
                try
                {
                    while (_running && done < maxYears
                        && Stopwatch.GetElapsedTime(started).TotalSeconds < SliceSeconds)
                    {
                        BeforeStepForTest?.Invoke(_world);
                        WorldSimulator.Step(_world);
                        done++;
                    }
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    // Step은 중간까지 변경됐을 수 있다. 정상본 교체 전 재개·저장을 막는다.
                    Error = exception.GetType().Name + ": " + exception.Message;
                    _running = false;
                    _faulted = true;
                    Interlocked.Increment(ref _version);
                }

                if (done > 0)
                {
                    Interlocked.Increment(ref _version);
                }
            }

            return done;
        }
    }
}
