// 새 세계 만들기: 두 종문·13개 지역·첫 세대 수선자·개창 원로·시대.
using System.Collections.Generic;
using Xianxia.Simulation.Core;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>세계 생성기. 같은 설정(시드·인구)이면 항상 같은 세계를 만든다.</summary>
    public static class WorldFactory
    {
        private const long StartingTreasury = 20_000;
        private const ulong RandomStream = 0x5EED_C0DE;
        private static readonly int[] s_peripheryIds = { 1, 2, 3, 5, 6, 7 };

        /// <summary>세계를 만든다.</summary>
        /// <param name="settings">설정.</param>
        /// <returns>0년의 세계.</returns>
        public static World Create(WorldSettings settings)
        {
            System.ArgumentNullException.ThrowIfNull(settings);
            int population = System.Math.Clamp(settings.Population, 100, 3000);
            World world = new() { Settings = settings, Random = new Pcg32(settings.Seed, RandomStream) };
            world.Sects.Add(new Sect { Id = RegionCatalog.OrthodoxSectId, Name = "청운종", Orthodox = true });
            world.Sects.Add(new Sect { Id = RegionCatalog.DemonicSectId, Name = "천마문", Orthodox = false });
            foreach (RegionDefinition definition in RegionCatalog.All)
            {
                world.Regions.Add(new Region { Id = definition.Id, ControllerSectId = definition.HomeSectId });
            }

            foreach (Sect sect in world.Sects)
            {
                sect.Treasury = StartingTreasury;
                world.Ledger.Create(StoneSource.Initial, StartingTreasury);
            }

            CreatePopulation(world, population);
            foreach (Sect sect in world.Sects)
            {
                AppointFounders(world, sect);
                SectSystem.Rerank(world, sect, announce: false);
            }

            EraSystem.Begin(world, EraKind.Calm);
            world.War.LastWarEndYear = 0;
            Narrator.Chronicle(world, ChronicleKind.World,
                "천지개벽. 청운종과 천마문이 산문을 열고 " + population + "명의 수선자가 도를 구하기 시작했다.");
            StatisticsSystem.Sample(world);
            return world;
        }

        private static void CreatePopulation(World world, int population)
        {
            int rogues = (int)System.Math.Round(population * Balance.InitialRogueShare);
            int orthodox = (population - rogues) / 2;
            HashSet<string> names = new();
            for (int slot = 0; slot < population; slot++)
            {
                int sectId = slot < orthodox ? RegionCatalog.OrthodoxSectId
                    : slot < population - rogues ? RegionCatalog.DemonicSectId : 0;
                int regionId = StartingRegion(world.Random, sectId);
                Cultivator cultivator = CultivatorFactory.CreateSoul(world, sectId, regionId, names);
                cultivator.Age = world.Random.Range(Balance.StartingAge, 60);
                cultivator.BornYear = -cultivator.Age + Balance.StartingAge;
                Narrator.Biography(world, cultivator, sectId == 0 ? "떠돌이 산수로 도를 구하기 시작했다." :
                    Narrator.Affiliation(world, sectId) + " 문하에서 수행을 시작했다.");
                world.Cultivators.Add(cultivator);
                world.Register(cultivator);
                world.Regions[regionId].Residents.Add(cultivator);
                world.Ledger.Create(StoneSource.Initial, cultivator.SpiritStones);
            }
        }

        private static int StartingRegion(Pcg32 random, int sectId)
        {
            if (sectId == 0)
            {
                return random.Chance(0.75) ? random.Pick(RegionCatalog.BorderIds) : random.Pick(s_peripheryIds);
            }

            return random.Chance(0.65) ? random.Pick(RegionCatalog.TerritoryOf(sectId))
                : random.Pick(RegionCatalog.BorderIds);
        }

        /// <summary>재능이 가장 뛰어난 문도 13명을 개창 원로(종주·대장로·장로)로 앞세운다.</summary>
        private static void AppointFounders(World world, Sect sect)
        {
            List<Cultivator> members = world.Cultivators.FindAll(c => c.SectId == sect.Id);
            members.Sort((left, right) => right.TalentTotal.CompareTo(left.TalentTotal));
            for (int index = 0; index < members.Count && index < 13; index++)
            {
                Cultivator founder = members[index];
                founder.Realm = index == 0 ? 2 : 1;
                founder.Stage = index == 0 ? 0 : index < 3 ? 3 : world.Random.Range(0, 2);
                founder.Age = index == 0 ? world.Random.Range(150, 220) : world.Random.Range(70, 140);
                founder.BornYear = -founder.Age + Balance.StartingAge;
                founder.TechniqueGrade = index == 0 ? 5 : 3;
                founder.HighestRealm = founder.Realm;
                founder.HighestStage = founder.Stage;
                founder.Lifespan = Lifespans.For(founder);
                founder.Merit = 3000 - (index * 150);
                if (world.Regions[founder.RegionId].Kind == RegionKind.Border)
                {
                    LifeCycleSystem.MoveTo(world, founder, RegionCatalog.HomeOf(sect.Id));
                }

                Narrator.Biography(world, founder, sect.Name + " 개창에 참여했다.");
            }
        }
    }
}
