// 게임 세션: 앱 하나에 하나. 세계·실행기·저장소를 묶고 수명 주기(백그라운드·종료)에 맞춰 저장한다.
// Android에서 화면(Activity)이 다시 만들어져도 세션은 그대로라 진행이 끊기지 않는다.
using System;
using System.IO;
using System.Threading;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Persistence;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Services
{
    /// <summary>게임 세션.</summary>
    internal sealed class GameSession
    {
        /// <summary>부분 처리된 세계를 정상 저장에 덮어쓰지 않는 이유.</summary>
        internal const string FaultMessage = "오류가 난 세계는 저장하지 않습니다. 마지막 저장을 불러오거나 새 세계를 시작하세요.";

        private readonly object _saveGate = new();
        private bool _resumeOnForeground;
        private int _worldGeneration;

        /// <summary>세션을 만든다.</summary>
        /// <param name="store">저장소.</param>
        /// <param name="world">처음 세계.</param>
        /// <param name="note">시작 안내(불러오기 실패 등).</param>
        internal GameSession(SaveStore store, World world, string? note = null)
        {
            Store = store;
            Runner = new SimulationRunner(world, () => Save());
            StartupNote = note;
        }

        /// <summary>저장소.</summary>
        internal SaveStore Store { get; }

        /// <summary>성공한 세계 교체 횟수. 일반 시뮬레이션 갱신과 구분한다.</summary>
        internal int WorldGeneration => Volatile.Read(ref _worldGeneration);

        /// <summary>실행기.</summary>
        internal SimulationRunner Runner { get; }

        /// <summary>시작 때 알릴 내용.</summary>
        internal string? StartupNote { get; }

        /// <summary>마지막 저장 결과(화면 표시용).</summary>
        internal string LastSaveMessage { get; private set; } = "아직 저장하지 않았습니다.";

        /// <summary>저장 파일이 있으면 불러오고, 없으면 기본 시드로 새 세계를 만든다.</summary>
        /// <param name="store">저장소.</param>
        /// <returns>세션.</returns>
        internal static GameSession LoadOrCreate(SaveStore store)
        {
            World? world = store.TryLoad(out string? problem);
            if (world is not null)
            {
                GameSession loaded = new(store, world, world.Year.ToString("#,0",
                    System.Globalization.CultureInfo.InvariantCulture) + "년의 세계를 이어서 관찰합니다.");
                loaded.LastSaveMessage = "저장 파일에서 불러왔습니다.";
                UiTrace.Session(world.Year, true);
                return loaded;
            }

            UiTrace.Session(0, false);
            return new GameSession(store, WorldFactory.Create(new WorldSettings()), problem);
        }

        /// <summary>캡처와 파일 쓰기 순서를 보장해 저장한다. 파일 쓰기 중에는 시뮬레이션 잠금을 놓는다.</summary>
        /// <returns>성공하면 true.</returns>
        internal bool Save()
        {
            lock (_saveGate)
            {
                // 동시에 요청된 자동·수동 저장도 캡처 순서대로 쓴다. 오래된 캡처가 새 저장을 덮지 못하게 한다.
                // 오류 확인과 캡처는 같은 잠금 안에서 한다. 확인 직후 Step이 실패하는 경합을 막는다.
                (byte[]? bytes, int year) = Runner.Read(world => Runner.IsFaulted
                    ? ((byte[]?)null, world.Year) : (WorldSerializer.ToBytes(world), world.Year));
                if (bytes is null)
                {
                    LastSaveMessage = FaultMessage;
                    return false;
                }

                return WriteCapture(bytes, year);
            }
        }

        /// <summary>이미 검증한 캡처를 쓰고 환경 오류를 사용자 문구로 반환한다.</summary>
        private bool WriteCapture(byte[] bytes, int year)
        {
            try
            {
                Store.Write(bytes);
                LastSaveMessage = year.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture)
                    + "년 상태를 저장했습니다(" + (bytes.Length / 1024) + "KB).";
                return true;
            }
            catch (IOException exception)
            {
                LastSaveMessage = "저장 실패: " + exception.Message;
                return false;
            }
            catch (UnauthorizedAccessException exception)
            {
                LastSaveMessage = "저장 실패: " + exception.Message;
                return false;
            }
        }

        /// <summary>마지막 저장을 불러와 현재 세계를 바꾼다.</summary>
        /// <param name="message">결과 안내.</param>
        /// <returns>성공하면 true.</returns>
        internal bool LoadSaved(out string message)
        {
            lock (_saveGate)
            {
                World? world = Store.TryLoad(out string? problem);
                if (world is null)
                {
                    message = problem ?? "저장 파일이 없습니다.";
                    return false;
                }

                Runner.Replace(world);
                Interlocked.Increment(ref _worldGeneration);
                message = world.Year.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture)
                    + "년 저장을 불러왔습니다.";
                LastSaveMessage = message;
                return true;
            }
        }

        /// <summary>새 세계를 연다.</summary>
        /// <param name="settings">시드·인구.</param>
        internal void NewWorld(WorldSettings settings)
        {
            Runner.Replace(WorldFactory.Create(settings));
            Interlocked.Increment(ref _worldGeneration);
            Save();
        }

        /// <summary>앱이 백그라운드로 갈 때: 멈추고 저장한다.</summary>
        internal void OnBackground()
        {
            _resumeOnForeground = Runner.IsRunning;
            Runner.SetRunning(false);
            Save();
        }

        /// <summary>앱이 다시 앞으로 올 때: 진행 중이었으면 이어 간다.</summary>
        internal void OnForeground()
        {
            if (_resumeOnForeground)
            {
                Runner.SetRunning(true);
            }

            _resumeOnForeground = false;
        }

        /// <summary>앱 종료: 실행기를 멈추고 저장한다.</summary>
        internal void Shutdown()
        {
            Runner.Stop();
            Save();
        }

        /// <summary>새 세계 시드를 시계에서 고른다.</summary>
        /// <returns>시드.</returns>
        internal static ulong ClockSeed()
        {
            // [결정론] 시뮬레이션 안에서는 시계를 쓰지 않는다. 새 세계의 시드만 여기서 고르고 화면에 보여 준다.
            return (ulong)TimeProvider.System.GetUtcNow().ToUnixTimeMilliseconds() % 1_000_000_000UL;
        }
    }
}
