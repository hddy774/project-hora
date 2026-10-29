// 화면 조회가 세계를 변경하지 않고 검색·필터·순위 동점과 종합 순위를 올바르게 처리하는지 검사한다.
using System.Linq;
using NUnit.Framework;
using Xianxia.App.Services;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Tests
{
    /// <summary>UI에 표시하는 순위·검색 계산의 회귀 검사.</summary>
    public sealed class QueryTests
    {
        private static readonly int[] s_bestOrder = { 1, 2, 3 };
        private static readonly int[] s_fiveOrder = { 2, 3, 1 };
        private static readonly int[] s_combinedOrder = { 2, 1, 3 };
        /// <summary>소속·관심·이름 필터와 8종 내림차순 정렬은 원본 인물 순서와 난수를 보존한다.</summary>
        [Test]
        public void PeopleQuery_WithFiltersAndEverySort_PreservesWorldAndReturnsMatchingOrder()
        {
            World world = WorldFactory.Create(new WorldSettings { Seed = 21, Population = 100 });
            Cultivator target = world.Cultivators[17];
            target.DaoName = "검증도호";
            world.SetFollowed(target.Id, true);
            int[] ids = world.Cultivators.Select(person => person.Id).ToArray();
            ulong random = world.Random.State;

            Assert.That(PeopleQuery.Select(world, 0, 4, "검증도호"), Is.EqualTo(new[] { target }));
            Assert.That(PeopleQuery.Select(world, 0, 4, "없는이름"), Is.Empty);
            Assert.That(PeopleQuery.Select(world, 0, 1, "").All(person => person.SectId == 1), Is.True);
            Assert.That(PeopleQuery.Select(world, 0, 3, "").All(person => person.IsRogue), Is.True);
            for (int sort = 0; sort < 8; sort++)
            {
                Cultivator[] people = PeopleQuery.Select(world, sort, 0, "").ToArray();
                for (int index = 1; index < people.Length; index++)
                {
                    double previous = PeopleQuery.Value(world, people[index - 1], sort);
                    double current = PeopleQuery.Value(world, people[index], sort);
                    Assert.That(previous, Is.GreaterThanOrEqualTo(current));
                    if (previous == current)
                    {
                        Assert.That(people[index - 1].Id, Is.LessThan(people[index].Id));
                    }
                }
            }

            Assert.That(world.Cultivators.Select(person => person.Id), Is.EqualTo(ids));
            Assert.That(world.Random.State, Is.EqualTo(random));
        }

        /// <summary>종합 순위는 최고/5생의 순위 합이 작을수록 앞이고 동점은 최고 점수로 정한다.</summary>
        [Test]
        public void LegacyQuery_CombinedRanks_UsesRankSumThenBestScore()
        {
            World world = new();
            world.Souls.Add(new SoulRecord { SoulId = 1, BestScore = 100, TopFiveSum = 300 });
            world.Souls.Add(new SoulRecord { SoulId = 2, BestScore = 90, TopFiveSum = 500 });
            world.Souls.Add(new SoulRecord { SoulId = 3, BestScore = 80, TopFiveSum = 400 });

            Assert.That(LegacyQuery.Select(world, 0).Select(row => row.Soul.SoulId), Is.EqualTo(s_bestOrder));
            Assert.That(LegacyQuery.Select(world, 1).Select(row => row.Soul.SoulId), Is.EqualTo(s_fiveOrder));
            Assert.That(LegacyQuery.Select(world, 2).Select(row => row.Soul.SoulId), Is.EqualTo(s_combinedOrder));
        }
    }
}
