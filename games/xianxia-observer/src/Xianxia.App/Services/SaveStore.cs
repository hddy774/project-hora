// 저장 파일 관리. 앱 전용 폴더에 world.sav(gzip JSON) 하나를 둔다. 인터넷 권한 없이 기기 안에만 저장한다.
// 쓰기는 임시 파일에 쓴 뒤 바꿔치기해, 저장 중 앱이 꺼져도 이전 저장이 깨지지 않는다.
using System;
using System.IO;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Persistence;

namespace Xianxia.App.Services
{
    /// <summary>저장 파일 저장소.</summary>
    internal sealed class SaveStore
    {
        private const string FileName = "world.sav";
        private readonly string _directory;

        /// <summary>저장소를 만든다.</summary>
        /// <param name="directory">저장 폴더.</param>
        internal SaveStore(string directory)
        {
            _directory = directory;
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
                File.Move(FilePath, FilePath + ".broken", overwrite: true);
            }
            catch (IOException)
            {
                // 보관에 실패해도 다음 저장이 덮어쓰므로 진행에는 문제가 없다.
            }
        }
    }
}
