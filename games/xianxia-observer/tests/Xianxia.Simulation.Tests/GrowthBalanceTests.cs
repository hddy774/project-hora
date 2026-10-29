// 변경한 소비 경로의 장부 보존과 이전 저장의 배열 확장을 실제 정산·직렬화로 검증한다.
using System.Linq;
using NUnit.Framework;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Persistence;
using Xianxia.Simulation.Systems;

namespace Xianxia.Simulation.Tests
{
    /// <summary>0.2.0 밸런스 변경의 보존 계약.</summary>
    public sealed class GrowthBalanceTests
    {
        /// <summary>백년 수행 소비는 소유액에 비례하고 가난한 인물에게 음수를 만들지 않는다.</summary>
        [Test]
        public void PersonalUpkeep_PreservesLedgerAndPoorCultivator()
        {
            World world = WorldFactory.Create(new WorldSettings { Seed = 7, Population = 100 });
            Cultivator[] rogues = world.Cultivators.Where(person => person.IsRogue).Take(2).ToArray();
            rogues[0].SpiritStones = 10000;
            rogues[1].SpiritStones = 19;
            world.Ledger = new StoneLedger();
            world.Ledger.Create(StoneSource.Initial, Actual(world));
            SectEconomy.Settle(world);
            Assert.That(rogues[0].SpiritStones, Is.EqualTo(9500));
            Assert.That(rogues[1].SpiritStones, Is.EqualTo(19));
            Assert.That(world.Ledger.Destroyed[(int)StoneSink.Cultivation], Is.GreaterThanOrEqualTo(500));
            Assert.That(world.Ledger.Expected(), Is.EqualTo(Actual(world)));
        }

        /// <summary>기존8항목 소멸 장부는0으로 확장되어 정상 이어 진행한다.</summary>
        [Test]
        public void LegacyLedger_LoadPadsNewSinkAndContinues()
        {
            World world = WorldFactory.Create(new WorldSettings { Seed = 7, Population = 100 });
            world.Ledger.Destroyed.RemoveAt(8);
            World loaded = WorldSerializer.FromBytes(WorldSerializer.ToBytes(world));
            Assert.That(loaded.Ledger.Destroyed, Has.Count.EqualTo(9));
            Assert.That(loaded.Ledger.Destroyed[8], Is.Zero);
            WorldSimulator.Run(loaded, 200);
            Assert.That(loaded.Ledger.Destroyed[8], Is.Positive);
            Assert.That(loaded.Ledger.Expected(), Is.EqualTo(Actual(loaded)));
            Assert.That(loaded.Cultivators, Has.Count.EqualTo(100));
        }

        private static long Actual(World world) => world.Cultivators.Sum(person => person.SpiritStones)
            + world.Sects.Sum(sect => sect.Treasury);
    }
}
