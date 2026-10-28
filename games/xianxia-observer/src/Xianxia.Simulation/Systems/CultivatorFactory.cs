// 새 영혼 만들기와 한 생(재능·영근·성격·기예·소지품)을 굴리는 규칙.
using System.Collections.Generic;
using Xianxia.Simulation.Core;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>수선자 생성 규칙.</summary>
    public static class CultivatorFactory
    {
        /// <summary>새 영혼을 만든다(세계 생성·비승 뒤 빈자리).</summary>
        /// <param name="world">세계.</param>
        /// <param name="sectId">소속(0이면 산수).</param>
        /// <param name="regionId">태어날 지역.</param>
        /// <param name="takenNames">이미 쓰인 이름(중복 방지).</param>
        /// <returns>새 수선자.</returns>
        public static Cultivator CreateSoul(World world, int sectId, int regionId, ISet<string> takenNames)
        {
            System.ArgumentNullException.ThrowIfNull(world);
            System.ArgumentNullException.ThrowIfNull(takenNames);
            Cultivator cultivator = new()
            {
                Id = world.NextSoulId,
                Name = UniqueName(world.Random, takenNames),
                SectId = sectId,
                RegionId = regionId,
            };
            world.NextSoulId++;
            takenNames.Add(cultivator.Name);
            RollLife(world, cultivator, 0, null);
            return cultivator;
        }

        /// <summary>
        /// 한 생을 새로 굴린다. 업보 하한(<paramref name="karmaFloor"/>)이 높으면 재능 하한이 올라간다.
        /// 전생 성격·영근은 일부 이어진다.
        /// </summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <param name="karmaFloor">재능 하한 가산(0~150).</param>
        /// <param name="previous">전생 성격·영근(첫 생이면 null).</param>
        public static void RollLife(World world, Cultivator cultivator, int karmaFloor, (Personality,
            Element)? previous)
        {
            System.ArgumentNullException.ThrowIfNull(world);
            System.ArgumentNullException.ThrowIfNull(cultivator);
            Pcg32 random = world.Random;
            cultivator.Comprehension = Traits.RollStat(random.NextDouble(), karmaFloor);
            cultivator.Constitution = Traits.RollStat(random.NextDouble(), karmaFloor);
            cultivator.Bone = Traits.RollStat(random.NextDouble(), karmaFloor);
            cultivator.Root = Traits.RollStat(random.NextDouble(), karmaFloor);
            cultivator.Element = previous is not null && random.Chance(0.3) ? previous.Value.Item2
                : RollElement(random);
            cultivator.Personality = previous is not null && random.Chance(0.6)
                ? previous.Value.Item1
                : RollPersonality(random, cultivator.SectId);
            cultivator.Ambition = random.NextDouble();
            cultivator.Caution = random.NextDouble();
            cultivator.Charisma = random.NextDouble();
            cultivator.Art = RollArt(random);
            cultivator.ArtSkill = cultivator.Art == ArtKind.None ? 0 : random.Range(1, 15);
            ResetLifeState(world, cultivator);
        }

        /// <summary>현생 상태(경지·자원·전적·인연)를 처음 값으로 되돌린다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        public static void ResetLifeState(World world, Cultivator cultivator)
        {
            System.ArgumentNullException.ThrowIfNull(world);
            System.ArgumentNullException.ThrowIfNull(cultivator);
            Pcg32 random = world.Random;
            cultivator.BornYear = world.Year;
            cultivator.Age = Balance.StartingAge;
            cultivator.Realm = 0;
            cultivator.Stage = 0;
            cultivator.Qi = random.Range(0, 60);
            cultivator.Insight = random.Range(0, 50);
            cultivator.DaoHeart = random.Range(55, 85);
            cultivator.Injury = 0;
            cultivator.InnerPower = random.Range(8, 20);
            cultivator.RecoveryYears = 0;
            cultivator.Rank = SectRank.None;
            cultivator.Merit = 0;
            cultivator.ContributionPoints = 0;
            cultivator.OnWarDuty = false;
            cultivator.TechniqueGrade = random.Range(0, 2);
            cultivator.TechniqueElement = random.Chance(0.7) ? cultivator.Element : RollElement(random);
            cultivator.ArtifactGrade = RollArtifact(random);
            cultivator.SpiritStones = random.Range(30, 140);
            cultivator.Herbs = random.Range(0, 10);
            cultivator.Ore = random.Range(0, 4);
            cultivator.BeastCores = 0;
            cultivator.Pills = new List<int> { 0, 0, 0, 0 };
            cultivator.Talismans = new List<int> { 0, 0 };
            cultivator.LongevityPillsTaken = 0;
            cultivator.MemoryAwakened = false;
            cultivator.LastTribulationYear = 0;
            cultivator.DaoName = null;
            ResetRecords(cultivator);
            cultivator.Lifespan = Lifespans.For(cultivator);
        }

        /// <summary>겹치지 않는 이름을 만든다. 성 1~2글자 + 이름 2글자.</summary>
        /// <param name="random">난수.</param>
        /// <param name="takenNames">쓰인 이름.</param>
        /// <returns>이름.</returns>
        public static string UniqueName(Pcg32 random, ISet<string> takenNames)
        {
            System.ArgumentNullException.ThrowIfNull(random);
            System.ArgumentNullException.ThrowIfNull(takenNames);
            for (int attempt = 0; attempt < 64; attempt++)
            {
                string surname = random.Chance(0.12) ? random.Pick(NameData.CompoundSurnames)
                    : random.Pick(NameData.Surnames);
                string name = surname + random.Pick(NameData.GivenFirst) + random.Pick(NameData.GivenSecond);
                if (!takenNames.Contains(name))
                {
                    return name;
                }
            }

            // 거의 일어나지 않지만, 이름 공간이 찰 경우 번호를 붙여 반드시 끝나게 한다.
            return random.Pick(NameData.Surnames) + random.Pick(NameData.GivenFirst) + takenNames.Count;
        }

        /// <summary>도호를 만든다(예: 청허자, 원영경 이상).</summary>
        /// <param name="random">난수.</param>
        /// <param name="realm">경지. 화신경 이상은 "진인", 대승경 이상은 "노조".</param>
        /// <returns>도호.</returns>
        public static string DaoName(Pcg32 random, int realm)
        {
            System.ArgumentNullException.ThrowIfNull(random);
            string root = random.Pick(NameData.DaoFirst) + random.Pick(NameData.DaoSecond);
            return realm >= 7 ? root + "노조" : realm >= 4 ? root + "진인" : root + "자";
        }

        private static void ResetRecords(Cultivator cultivator)
        {
            cultivator.MasterId = 0;
            cultivator.DiscipleIds = new List<int>();
            cultivator.RivalIds = new List<int>();
            cultivator.CompanionId = 0;
            cultivator.CompanionSinceYear = 0;
            cultivator.Battles = 0;
            cultivator.Wins = 0;
            cultivator.Losses = 0;
            cultivator.WinStreak = 0;
            cultivator.MajorBreakthroughs = 0;
            cultivator.MinorBreakthroughs = 0;
            cultivator.TribulationsPassed = 0;
            cultivator.Fortunes = 0;
            cultivator.WarMerit = 0;
            cultivator.LifetimeWarMerit = 0;
            cultivator.TournamentBest = 0;
            cultivator.HighestRealm = 0;
            cultivator.HighestStage = 0;
            cultivator.Titles = new List<TitleId>();
            cultivator.Biography = new List<LifeEvent>();
        }

        private static Element RollElement(Pcg32 random)
        {
            return random.Chance(0.1)
                ? (Element)(Traits.BasicElementCount + random.NextInt(3))
                : (Element)random.NextInt(Traits.BasicElementCount);
        }

        private static Personality RollPersonality(Pcg32 random, int sectId)
        {
            // 종문에서 태어난 영혼은 종문 기풍에 물든다. 산수는 고르게 퍼진다.
            System.ReadOnlySpan<double> weights = sectId switch
            {
                RegionCatalog.OrthodoxSectId => new double[] { 40, 6, 30, 16, 8 },
                RegionCatalog.DemonicSectId => new double[] { 6, 38, 26, 8, 22 },
                _ => new double[] { 22, 20, 30, 15, 13 },
            };
            return (Personality)random.PickWeighted(weights);
        }

        private static ArtKind RollArt(Pcg32 random)
        {
            System.ReadOnlySpan<double> weights = stackalloc double[] { 57, 15, 10, 8, 10 };
            return (ArtKind)random.PickWeighted(weights);
        }

        private static int RollArtifact(Pcg32 random)
        {
            System.ReadOnlySpan<double> weights = stackalloc double[] { 50, 30, 15, 5 };
            return random.PickWeighted(weights);
        }
    }
}
