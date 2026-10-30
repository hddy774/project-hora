// 기예별 제작: 연단(단약), 연기(법보), 부록(부적), 진법(진 유지). 숙련이 높을수록 성공률과 품질이 오른다.
using System;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>기예 제작 규칙.</summary>
    public static class Crafting
    {
        /// <summary>연단: 영초 3개로 단약 하나. 숙련 70 이상이면 드물게 연수단.</summary>
        /// <param name="world">세계.</param>
        /// <param name="alchemist">연단사.</param>
        /// <returns>제작을 시도했으면 true.</returns>
        public static bool Alchemy(World world, Cultivator alchemist)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(alchemist);
            if (alchemist.Herbs < 3 && !Commerce.BuyMaterials(world, alchemist, 3, 0))
            {
                return false;
            }

            alchemist.Herbs -= 3;
            if (!world.Random.Chance(Math.Min(0.95, 0.4 + (alchemist.ArtSkill / 150))))
            {
                return true;
            }

            PillKind pill = ChoosePill(world, alchemist);
            if (alchemist.Pills[(int)pill] < 2)
            {
                alchemist.Pills[(int)pill]++;
            }
            else
            {
                Commerce.ListPill(world, alchemist, pill);
            }

            if (pill == PillKind.Longevity)
            {
                Narrator.Log(world, LogKind.Craft, alchemist.Name + "이(가) 연수단을 빚어냈다.", alchemist, notable: true);
            }
            else if (world.Random.Chance(0.02))
            {
                Narrator.Log(world, LogKind.Craft, alchemist.Name + "이(가) " + Labels.Of(pill) + "을 빚었다.", alchemist);
            }
            else
            {
                world.Log.CountOnly(LogKind.Craft);
            }

            return true;
        }

        /// <summary>연기: 영철 3개와 영석으로 법보를 단련한다. 자기 법보보다 좋으면 바꾸고 아니면 상회에 판다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="refiner">연기사.</param>
        /// <returns>제작을 시도했으면 true.</returns>
        public static bool Refining(World world, Cultivator refiner)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(refiner);
            int grade = Math.Min(5, 1 + (int)(refiner.ArtSkill / 22));
            long fuel = 30L * (grade + 1);
            if ((refiner.Ore < 3 && !Commerce.BuyMaterials(world, refiner, 0, 3)) || refiner.SpiritStones < fuel)
            {
                return false;
            }

            refiner.Ore -= 3;
            refiner.SpiritStones -= fuel;
            world.Ledger.Destroy(StoneSink.Refining, fuel);
            if (!world.Random.Chance(Math.Min(0.9, 0.35 + (refiner.ArtSkill / 150))))
            {
                return true;
            }

            if (grade > refiner.ArtifactGrade)
            {
                refiner.ArtifactGrade = grade;
                Narrator.Log(world, LogKind.Craft, refiner.Name + "이(가) " + Labels.ArtifactGrade(grade)
                    + " 법보를 단련해 몸에 지녔다.",
                    refiner, notable: grade >= 4);
                return true;
            }

            long price = 150L * grade;
            refiner.SpiritStones += price;
            world.Ledger.Create(StoneSource.MerchantSale, price);
            world.Log.CountOnly(LogKind.Craft);
            return true;
        }

        /// <summary>부록: 영초·영철 하나씩으로 부적을 만든다. 숙련 40 이상이면 피뢰부도 만든다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="maker">부록사.</param>
        /// <returns>제작을 시도했으면 true.</returns>
        public static bool Talismans(World world, Cultivator maker)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(maker);
            if ((maker.Herbs < 1 || maker.Ore < 1) && !Commerce.BuyMaterials(world, maker, 1, 1))
            {
                return false;
            }

            maker.Herbs--;
            maker.Ore--;
            if (!world.Random.Chance(Math.Min(0.95, 0.5 + (maker.ArtSkill / 150))))
            {
                return true;
            }

            TalismanKind kind = maker.ArtSkill >= 40 && world.Random.Chance(0.3) ? TalismanKind.Thunder
                : TalismanKind.Attack;
            if (maker.Talismans[(int)kind] < 2)
            {
                maker.Talismans[(int)kind]++;
            }
            else
            {
                Commerce.ListTalisman(world, maker, kind);
            }

            world.Log.CountOnly(LogKind.Craft);
            return true;
        }

        /// <summary>진법: 재료 없이 진을 손본다. 종문 공헌을 얻고 진법 숙련이 오른다(전쟁·수조 방어에 쓰인다).</summary>
        /// <param name="world">세계.</param>
        /// <param name="master">진법사.</param>
        /// <returns>항상 true.</returns>
        public static bool Formation(World world, Cultivator master)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(master);
            SectSystem.AddContribution(world, master, 5 * (master.Realm + 1));
            ActionEffects.Cultivate(world, master, 0.4);
            world.Log.CountOnly(LogKind.Craft);
            return true;
        }

        /// <summary>진법사가 지역 방어에 보태는 배율(진법 숙련 합에 비례, 최대 +20%).</summary>
        /// <param name="region">지역.</param>
        /// <param name="sectId">방어하는 종문(0이면 모든 진법사).</param>
        /// <returns>배율(1.0~1.2).</returns>
        public static double FormationSupport(Region region, int sectId)
        {
            ArgumentNullException.ThrowIfNull(region);
            double skill = 0;
            foreach (Cultivator resident in region.Residents)
            {
                if (resident.Art == ArtKind.Formation && (sectId == 0 || resident.SectId == sectId))
                {
                    skill += resident.ArtSkill;
                }
            }

            return 1 + Math.Min(0.2, skill / 1000);
        }

        private static PillKind ChoosePill(World world, Cultivator alchemist)
        {
            if (alchemist.ArtSkill >= 70 && world.Random.Chance(0.08))
            {
                return PillKind.Longevity;
            }

            double roll = world.Random.NextDouble();
            return roll < 0.45 ? PillKind.Healing : roll < 0.8 ? PillKind.Breakthrough : PillKind.Clarity;
        }
    }
}
