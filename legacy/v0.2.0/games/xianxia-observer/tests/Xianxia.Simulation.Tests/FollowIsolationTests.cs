// 관심 표시의 순수 관찰 계약. 관심 수와 입력 시점을 바꿔도 세계 규칙은 동일해야 한다.
using System.Text.Json.Nodes;
using NUnit.Framework;
using Xianxia.Simulation.Core;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Persistence;
using Xianxia.Simulation.Systems;

namespace Xianxia.Simulation.Tests
{
    /// <summary>관심·로그 난수 분리 회귀.</summary>
    public sealed class FollowIsolationTests
    {
        /// <summary>관심 1명·전원·중도 추가를 세계 상태로 대조한다.</summary>
        /// <param name="count">관심 수.</param>
        /// <param name="startYear">관심 시작 연도.</param>
        [TestCase(1, 0)]
        [TestCase(100, 0)]
        [TestCase(100, 300)]
        public void Following_DoesNotChangeCoreState(int count, int startYear)
        {
            World control = TestWorlds.Run(startYear, seed: 7, population: 100);
            World observed = TestWorlds.Run(startYear, seed: 7, population: 100);
            for (int index = 0; index < count; index++)
            {
                observed.SetFollowed(observed.Cultivators[index].Id, true);
            }

            WorldSimulator.Run(control, 600 - startYear);
            WorldSimulator.Run(observed, 600 - startYear);
            Assert.That(observed.Random.State, Is.EqualTo(control.Random.State));
            Assert.That(Core(observed), Is.EqualTo(Core(control)));
        }

        /// <summary>관심 목록과 두 난수 상태는 저장 뒤에도 동일하게 진행한다.</summary>
        [Test]
        public void SaveAndLoad_PreservesFollowedAndBothRandomStreams()
        {
            World world = TestWorlds.Run(300, seed: 7, population: 100);
            world.SetFollowed(world.Cultivators[0].Id, true);
            World restored = WorldSerializer.FromBytes(WorldSerializer.ToBytes(world));
            Assert.That(restored.IsFollowed(world.Cultivators[0].Id), Is.True);
            WorldSimulator.Run(world, 300);
            WorldSimulator.Run(restored, 300);
            Assert.That(WorldSerializer.ToBytes(restored), Is.EqualTo(WorldSerializer.ToBytes(world)));
        }

        /// <summary>구 저장은 increment 부재로 판별하여 로그 스트림을 만든다.</summary>
        [Test]
        public void Load_WithoutLogRandom_CreatesFromSeed()
        {
            World original = TestWorlds.Run(300, seed: 7, population: 100);
            JsonObject json = SaveTestData.Read(WorldSerializer.ToBytes(original));
            json.Remove("LogRandomState");
            json.Remove("LogRandomIncrement");
            World restored = WorldSerializer.FromBytes(SaveTestData.Write(json));
            Pcg32 expected = new(original.Settings.Seed, World.LogRandomStream);
            Assert.That(restored.LogRandom.State, Is.EqualTo(expected.State));
            Assert.That(restored.LogRandom.Increment, Is.EqualTo(expected.Increment));
            WorldSimulator.Run(original, 300);
            WorldSimulator.Run(restored, 300);
            Assert.That(Core(restored), Is.EqualTo(Core(original)));
        }

        /// <summary>관심 중복·해제와 색인 재구축을 확인한다.</summary>
        [Test]
        public void SetFollowed_UpdatesIndexWithoutDuplicates()
        {
            World world = TestWorlds.Run(0, population: 100);
            int id = world.Cultivators[0].Id;
            world.SetFollowed(id, true);
            world.SetFollowed(id, true);
            Assert.That(world.Followed, Has.Count.EqualTo(1));
            Assert.That(world.IsFollowed(id), Is.True);
            world.SetFollowed(id, false);
            Assert.That(world.IsFollowed(id), Is.False);
            Assert.That(world.Followed, Is.Empty);
            world.SetFollowed(world.Cultivators[0].Id, true);
            world.SetFollowed(world.Cultivators[1].Id, true);
            world.SetFollowed(world.Cultivators[2].Id, true);
            world.SetFollowed(world.Cultivators[1].Id, false);
            Assert.That(world.IsFollowed(world.Cultivators[1].Id), Is.False);
            Assert.That(world.IsFollowed(world.Cultivators[2].Id), Is.True);
        }

        /// <summary>지연 로그는 사건 당시 이름·문장·저장 후 값 동등성을 보존한다.</summary>
        [Test]
        public void FriendlyLog_PreservesSnapshotAndValueEquality()
        {
            World world = TestWorlds.Run(0, population: 100);
            Cultivator winner = world.Cultivators[0];
            Cultivator loser = world.Cultivators[1];
            string expected = "[비무] " + winner.Name + "이(가) " + loser.Name + "에게 이겼다.";
            world.SetFollowed(winner.Id, true);
            Narrator.Friendly(world, winner, loser, crossSect: false, notable: true);
            winner.Name = "다른 이름";
            loser.Name = "바뀐 이름";
            LogEntry entry = world.Log.CopyNewestFirst(1)[0];
            Assert.That(entry.Text, Is.EqualTo(expected));
            Assert.That(world.Chronicle.Notices[^1].Text, Is.EqualTo(expected));
            World restored = WorldSerializer.FromBytes(WorldSerializer.ToBytes(world));
            Assert.That(restored.Log.CopyNewestFirst(1)[0], Is.EqualTo(entry));
        }

        private static string Core(World world)
        {
            JsonObject json = SaveTestData.Read(WorldSerializer.ToBytes(world));
            foreach (string key in new[] { "Log", "Chronicle", "Followed", "LogRandomState", "LogRandomIncrement" })
            {
                json.Remove(key);
            }

            return json.ToJsonString();
        }
    }
}
