// 결정론·저장 왕복 검사: 같은 시드는 같은 역사, 저장 후 이어 돌려도 같은 역사.
using NUnit.Framework;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Persistence;

namespace Xianxia.Simulation.Tests
{
    public sealed class DeterminismTests
    {
        [Test]
        public void SameSeed_ProducesIdenticalWorld()
        {
            World first = TestWorlds.Run(1500, seed: 11);
            World second = TestWorlds.Run(1500, seed: 11);
            Assert.That(WorldSerializer.ToBytes(first), Is.EqualTo(WorldSerializer.ToBytes(second)));
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentWorlds()
        {
            World first = TestWorlds.Run(300, seed: 1);
            World second = TestWorlds.Run(300, seed: 2);
            Assert.That(WorldSerializer.ToBytes(first), Is.Not.EqualTo(WorldSerializer.ToBytes(second)));
        }

        [Test]
        public void SaveAndLoad_ContinuesExactlyTheSameHistory()
        {
            World original = TestWorlds.Run(800, seed: 5);
            World restored = WorldSerializer.FromBytes(WorldSerializer.ToBytes(original));
            WorldSimulator.Run(original, 700);
            WorldSimulator.Run(restored, 700);
            Assert.That(WorldSerializer.ToBytes(restored), Is.EqualTo(WorldSerializer.ToBytes(original)));
        }

        [Test]
        public void Load_RejectsCorruptData()
        {
            byte[] garbage = { 1, 2, 3, 4, 5 };
            Assert.Throws<SaveFormatException>(() => WorldSerializer.FromBytes(garbage));
        }

        [Test]
        public void Load_RestoresIndexes()
        {
            World original = TestWorlds.Run(200, seed: 9);
            World restored = WorldSerializer.FromBytes(WorldSerializer.ToBytes(original));
            foreach (Cultivator cultivator in restored.Cultivators)
            {
                Assert.That(restored.Find(cultivator.Id), Is.SameAs(cultivator));
                Assert.That(restored.Regions[cultivator.RegionId].Residents, Does.Contain(cultivator));
            }
        }
    }
}
