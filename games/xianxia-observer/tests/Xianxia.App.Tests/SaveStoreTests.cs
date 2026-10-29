// 의미가 손상된 저장에서도 시작하고, 조사용 손상본을 덮어쓰지 않는지 검사한다.
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;
using NUnit.Framework;
using Xianxia.App.Services;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Persistence;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Tests
{
    /// <summary>앱 저장의 크기 사전 검사와 타임스탬프 격리 회귀.</summary>
    public sealed class SaveStoreTests
    {
        private static readonly string[] s_expectedBroken = { "손상본12", "손상본13", "손상본14" };
        private string _directory = "";

        /// <summary>검사별로 겹치지 않는 임시 폴더를 만든다.</summary>
        [SetUp]
        public void SetUp() => _directory = Directory.CreateTempSubdirectory("xianxia-save-").FullName;

        /// <summary>검사 파일을 정리한다.</summary>
        [TearDown]
        public void TearDown() => Directory.Delete(_directory, recursive: true);

        /// <summary>RegionId=999의 유효 JSON도 예외 없이 새 세계와 안내로 대체한다.</summary>
        [Test]
        public void LoadOrCreate_QuarantinesSemanticErrorAndStartsNewWorld()
        {
            SaveStore store = new(_directory, new FixedClock());
            World world = WorldFactory.Create(new WorldSettings { Seed = 7, Population = 100 });
            using MemoryStream input = new(WorldSerializer.ToBytes(world));
            using GZipStream unzip = new(input, CompressionMode.Decompress);
            JsonNode node = JsonNode.Parse(unzip)!;
            node["Cultivators"]![0]!["RegionId"] = 999;
            using (FileStream output = File.Create(store.FilePath))
            using (GZipStream zip = new(output, CompressionLevel.Fastest))
            using (StreamWriter writer = new(zip))
            {
                writer.Write(node.ToJsonString());
            }

            GameSession session = GameSession.LoadOrCreate(store);
            Assert.That(session.StartupNote, Does.Contain("따로 보관했습니다").And.Contain("지역"));
            Assert.That(session.Runner.Read(value => value.Year), Is.Zero);
            Assert.That(store.Exists, Is.False);
            Assert.That(Directory.GetFiles(_directory, "world.sav.broken-*").Length, Is.EqualTo(1));
            session.Runner.Stop();
        }

        /// <summary>같은 초의 15회 격리에서도 최근 세 본을 수치 순서로 보존한다.</summary>
        [Test]
        public void TryLoad_KeepsThreeNewestBrokenFilesAndLegacyCopy()
        {
            SaveStore store = new(_directory, new FixedClock());
            string legacy = store.FilePath + ".broken";
            File.WriteAllText(legacy, "기존 조사 자료");
            for (int index = 0; index < 15; index++)
            {
                File.WriteAllText(store.FilePath, "손상본" + index);
                Assert.That(store.TryLoad(out string? problem), Is.Null);
                Assert.That(problem, Does.Contain("따로 보관했습니다"));
            }

            string[] files = Directory.GetFiles(_directory, "world.sav.broken-*");
            Assert.That(files, Has.Length.EqualTo(3));
            Assert.That(files.Select(File.ReadAllText), Is.EquivalentTo(s_expectedBroken));
            Assert.That(File.ReadAllText(legacy), Is.EqualTo("기존 조사 자료"));
        }

        /// <summary>32MiB 초과 파일은 바이트 배열을 만들기 전에 길이로 거부한다.</summary>
        [Test]
        public void TryLoad_SkipsOversizedFile()
        {
            SaveStore store = new(_directory, new FixedClock());
            using (FileStream file = File.Create(store.FilePath))
            {
                file.SetLength(WorldSerializer.MaxCompressedBytes + 1L);
            }

            Assert.That(store.TryLoad(out string? problem), Is.Null);
            Assert.That(problem, Does.Contain("허용 크기"));
            Assert.That(store.Exists, Is.False);
            string path = Directory.GetFiles(_directory, "world.sav.broken-*").Single();
            Assert.That(new FileInfo(path).Length, Is.EqualTo(WorldSerializer.MaxCompressedBytes + 1L));
        }

        /// <summary>교체 전 정상본 한 세대를 바이트 그대로 유지한다.</summary>
        [Test]
        public void Write_KeepsPreviousAsBackup()
        {
            SaveStore store = new(_directory);
            World world = WorldFactory.Create(new WorldSettings { Seed = 7, Population = 100 });
            byte[] first = WorldSerializer.ToBytes(world);
            store.Write(first);
            world.Year = 10;
            store.Write(WorldSerializer.ToBytes(world));
            Assert.That(File.ReadAllBytes(store.BackupPath), Is.EqualTo(first));
            Assert.That(WorldSerializer.FromBytes(File.ReadAllBytes(store.FilePath)).Year, Is.EqualTo(10));
        }

        /// <summary>최신 저장 손상 시 백업도 의미 검증하여 복구하고 손상 증거를 남긴다.</summary>
        [Test]
        public void TryLoad_RecoversFromValidatedBackup()
        {
            SaveStore store = new(_directory, new FixedClock());
            World world = WorldFactory.Create(new WorldSettings { Seed = 7, Population = 100 });
            store.Write(WorldSerializer.ToBytes(world));
            world.Year = 10;
            store.Write(WorldSerializer.ToBytes(world));
            File.WriteAllText(store.FilePath, "손상된 본 저장");
            World? recovered = store.TryLoad(out string? problem);
            Assert.That(recovered, Is.Not.Null);
            Assert.That(recovered!.Year, Is.Zero);
            Assert.That(problem, Does.Contain("직전 저장으로 복구했습니다."));
            Assert.That(File.Exists(store.BackupPath), Is.True);
            Assert.That(Directory.GetFiles(_directory, "world.sav.broken-*").Length, Is.EqualTo(1));
            Assert.That(store.HasSave, Is.True);
            Assert.That(store.TryLoad(out _)!.Year, Is.Zero);
            File.WriteAllText(store.FilePath, "다시 손상");
            File.WriteAllText(store.BackupPath, "손상된 백업");
            Assert.That(store.TryLoad(out _), Is.Null);
            Assert.That(File.Exists(store.BackupPath), Is.True);
        }

        /// <summary>같은 초 충돌을 실제 시계 대기 없이 재현한다.</summary>
        private sealed class FixedClock : TimeProvider
        {
            /// <inheritdoc/>
            public override DateTimeOffset GetUtcNow() => new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
        }
    }
}
