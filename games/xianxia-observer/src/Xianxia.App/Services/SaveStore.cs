// 저장 파일 관리. 앱 전용 폴더에 world.sav(gzip JSON) 하나를 둔다. 인터넷 권한 없이 기기 안에만 저장한다.
// 쓰기는 임시 파일에 쓴 뒤 바꿔치기해, 저장 중 앱이 꺼져도 이전 저장이 깨지지 않는다.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Persistence;

namespace Xianxia.App.Services
{
    /// <summary>저장 파일 저장소.</summary>
    internal sealed class SaveStore
    {
        private const string FileName = "world.sav";
        private const string BrokenPrefix = FileName + ".broken-";
        private readonly string _directory;
        private readonly TimeProvider _clock;

        /// <summary>저장소를 만든다.</summary>
        /// <param name="directory">저장 폴더.</param>
        /// <param name="clock">격리 이름용 UTC 시계. 세계 난수와 무관하다.</param>
        internal SaveStore(string directory, TimeProvider? clock = null)
        {
            _directory = directory;
            _clock = clock ?? TimeProvider.System;
        }

        /// <summary>저장 파일 경로.</summary>
        internal string FilePath => Path.Combine(_directory, FileName);

        /// <summary>저장 파일이 있는가.</summary>
        internal bool Exists => File.Exists(FilePath);

        /// <summary>저장 파일 크기(바이트, 없으면 0).</summary>
        internal long FileSize => Exists ? new FileInfo(FilePath).Length : 0;

        /// <summary>기기별 기본 저장소(LocalApplicationData/XianxiaObserver).</summary>
        /// <returns>저장소.</returns>
        internal static SaveStore CreateDefault()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.Create);
            if (string.IsNullOrEmpty(root))
            {
                root = AppContext.BaseDirectory;
            }

            return new SaveStore(Path.Combine(root, "XianxiaObserver"));
        }

        /// <summary>저장 바이트를 파일에 쓴다.</summary>
        /// <param name="bytes">gzip JSON.</param>
        internal void Write(byte[] bytes)
        {
            Directory.CreateDirectory(_directory);
            string temporary = FilePath + ".tmp";
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, FilePath, overwrite: true);
        }

        /// <summary>저장 파일을 읽는다. 없으면 null, 깨졌으면 옆에 보관하고 null.</summary>
        /// <param name="problem">읽지 못한 이유(사용자 안내용).</param>
        /// <returns>세계 또는 null.</returns>
        internal World? TryLoad(out string? problem)
        {
            problem = null;
            if (!Exists)
            {
                return null;
            }

            try
            {
                if (FileSize > WorldSerializer.MaxCompressedBytes)
                {
                    throw new SaveFormatException("저장 파일이 허용 크기를 초과했습니다.");
                }

                return WorldSerializer.FromBytes(File.ReadAllBytes(FilePath));
            }
            catch (SaveFormatException exception)
            {
                problem = "저장 파일을 읽지 못해 따로 보관했습니다(" + exception.Message + ").";
                Quarantine();
                return null;
            }
            catch (IOException exception)
            {
                problem = "저장 파일을 열 수 없습니다(" + exception.Message + ").";
                return null;
            }
            catch (UnauthorizedAccessException exception)
            {
                problem = "저장 파일 접근이 거부되었습니다(" + exception.Message + ").";
                return null;
            }
        }

        private void Quarantine()
        {
            try
            {
                File.Move(FilePath, NextBrokenPath());
                string[] files = Directory.GetFiles(_directory, BrokenPrefix + "*")
                    .Where(IsTimestampedBroken)
                    .OrderBy(path => Path.GetFileName(path)[..(BrokenPrefix.Length + 14)], StringComparer.Ordinal)
                    .ThenBy(BrokenSequence).ToArray();
                foreach (string path in files.Take(Math.Max(0, files.Length - 3)))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // 격리·정리 권한이 없어도 시작 화면은 열 수 있어야 한다.
            }
            catch (UnauthorizedAccessException)
            {
                // 파일 시스템 오류를 세계 손상 예외와 구분해 시작 중단을 피한다.
            }
        }

        /// <summary>같은 초에 오래된 기본 이름이 정리됐어도 번호를 재사용하지 않는다.</summary>
        private string NextBrokenPath()
        {
            string stamp = _clock.GetUtcNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            string stem = Path.Combine(_directory, BrokenPrefix + stamp);
            string[] files = Directory.GetFiles(_directory, BrokenPrefix + stamp + "*")
                .Where(IsTimestampedBroken).ToArray();
            int next = files.Length == 0 ? 0 : files.Max(BrokenSequence) + 1;
            return next == 0 ? stem : stem + "-" + next.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>기존 .broken 또는 사용자가 만든 다른 이름의 자료는 정리하지 않는다.</summary>
        private static bool IsTimestampedBroken(string path)
        {
            string name = Path.GetFileName(path);
            if (name.Length < BrokenPrefix.Length + 14)
            {
                return false;
            }

            string stamp = name.Substring(BrokenPrefix.Length, 14);
            return DateTimeOffset.TryParseExact(stamp, "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out _) && (name.Length == BrokenPrefix.Length + 14
                || (name[BrokenPrefix.Length + 14] == '-' && BrokenSequence(path) > 0));
        }

        /// <summary>10以上の連番も文字列順でなく数値順に並べる。</summary>
        private static int BrokenSequence(string path)
        {
            string name = Path.GetFileName(path);
            int start = BrokenPrefix.Length + 15;
            return name.Length > start && int.TryParse(name.AsSpan(start), NumberStyles.None,
                CultureInfo.InvariantCulture, out int value) ? value : 0;
        }
    }
}
