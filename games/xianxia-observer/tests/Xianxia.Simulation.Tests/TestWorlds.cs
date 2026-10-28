// 테스트용 세계 생성 도우미. 작은 인구로 빠르게 돌린다.
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.Simulation.Tests
{
    /// <summary>테스트 세계 도우미.</summary>
    internal static class TestWorlds
    {
        /// <summary>작은 세계를 만든다.</summary>
        public static World Create(ulong seed = 7, int population = 300)
        {
            return WorldFactory.Create(new WorldSettings { Seed = seed, Population = population });
        }

        /// <summary>세계를 만들고 정해진 해만큼 진행한다.</summary>
        public static World Run(int years, ulong seed = 7, int population = 300)
        {
            World world = Create(seed, population);
            WorldSimulator.Run(world, years);
            return world;
        }
    }
}
