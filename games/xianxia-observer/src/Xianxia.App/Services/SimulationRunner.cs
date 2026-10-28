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

        /// <summary>실행 스레드를 시작한다(처음엔 멈춤 상태).</summary>
        internal void Start()
        {
            if (_thread is not null)
            {
                return;
            }

            _thread = new Thread(Loop) { IsBackground = true, Name = "Xianxia simulation" };
            _thread.Start();
        }

        /// <summary>스레드를 끝낸다.</summary>
        internal void Stop()
        {
            lock (_gate)
            {
                _stopping = true;
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
                _running = running;
                if (running)
                {
                    Error = null;
                }

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
                _world = world;
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
                WorldSimulator.Run(_world, years);
                Interlocked.Increment(ref _version);
            }
        }

        private void Loop()
        {
            long last = Stopwatch.GetTimestamp();
            long lastSave = last;
            long savedVersion = Version;
            long windowStart = last;
            int windowYears = 0;
            double owed = 0;
            while (true)
            {
                int speed;
                lock (_gate)
                {
                    while (!_running && !_stopping)
                    {
                        Monitor.Wait(_gate);
                        last = Stopwatch.GetTimestamp();
                        windowStart = last;
                        windowYears = 0;
                        owed = 0;
                    }

                    if (_stopping)
                    {
                        return;
                    }

                    speed = s_speeds[SpeedIndex];
                }

                long now = Stopwatch.GetTimestamp();
                double elapsed = Stopwatch.GetElapsedTime(last, now).TotalSeconds;
                last = now;
                int budget = int.MaxValue;
                if (speed != MaxSpeed)
                {
                    // 밀린 해는 0.2초 분량까지만 따라잡는다(멈췄다 풀릴 때 한꺼번에 뛰지 않게).
                    owed = Math.Min(owed + (elapsed * speed), Math.Max(1, speed * 0.2));
                    budget = (int)owed;
                    if (budget == 0)
                    {
                        Thread.Sleep(Math.Clamp((int)Math.Ceiling((1 - owed) / speed * 1000), 1, 50));
                        continue;
                    }
                }

                int done = RunSlice(budget, now);
                owed = Math.Max(0, owed - done);
                windowYears += done;
                double window = Stopwatch.GetElapsedTime(windowStart).TotalSeconds;
                if (window >= 1)
                {
                    Volatile.Write(ref _measuredYearsPerSecond, windowYears / window);
                    windowStart = Stopwatch.GetTimestamp();
                    windowYears = 0;
                }

                Thread.Sleep(1);
                if (_autosave is not null && Stopwatch.GetElapsedTime(lastSave).TotalSeconds
                    >= _autosaveInterval.TotalSeconds && Version != savedVersion)
                {
                    lastSave = Stopwatch.GetTimestamp();
                    savedVersion = Version;
                    _autosave();
                }
            }
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
                        WorldSimulator.Step(_world);
                        done++;
                    }
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    // 진행 중 오류는 화면에 알리고 멈춘다. 세계는 오류 직전 해까지 그대로다.
                    Error = exception.GetType().Name + ": " + exception.Message;
                    _running = false;
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
