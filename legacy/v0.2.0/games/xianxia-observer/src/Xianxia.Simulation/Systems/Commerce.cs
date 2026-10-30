// 거래와 기예. 기예가가 만든 단약·부적은 지역 시장에 쌓이고 필요한 사람이 사 간다.
// [보존] 상회 판매는 영석 생성, 상회·시장 구입은 영석 소멸로 장부에 기록한다.
using System;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>거래·기예 규칙.</summary>
    public static class Commerce
    {
        private const int MarketStockLimit = 30;

        /// <summary>단약 시장 가격(영석).</summary>
        /// <param name="pill">단약.</param>
        /// <param name="realm">기준 경지(파경단).</param>
        /// <returns>가격.</returns>
        public static long PillValue(PillKind pill, int realm)
        {
            return pill switch
            {
                PillKind.Healing => 60,
                PillKind.Breakthrough => 80L * (realm + 1),
                PillKind.Clarity => 90,
                _ => 2000,
            };
        }

        /// <summary>부적 시장 가격(영석).</summary>
        /// <param name="talisman">부적.</param>
        /// <returns>가격.</returns>
        public static long TalismanValue(TalismanKind talisman)
        {
            return talisman == TalismanKind.Thunder ? 300 : 50;
        }

        /// <summary>
        /// 거래: 남는 재료를 상회에 팔고, 종문 제자는 공헌으로 단약·공법을 받고,
        /// 떠돌이는 상회에서 공법을 사며, 필요한 단약·부적을 지역 시장에서 산다.
        /// </summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        public static void Trade(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            SellSurplus(world, cultivator);
            if (cultivator.IsRogue)
            {
                BuyTechnique(world, cultivator);
            }
            else
            {
                SectEconomy.RequestPill(world, cultivator);
                SectEconomy.StudyAtPavilion(world, cultivator);
            }

            BuyFromMarket(world, cultivator);
        }

        /// <summary>기예 제작. 재료가 없으면 수련으로 대신한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">기예가.</param>
        public static void Craft(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            bool crafted = cultivator.Art switch
            {
                ArtKind.Alchemy => Crafting.Alchemy(world, cultivator),
                ArtKind.Refining => Crafting.Refining(world, cultivator),
                ArtKind.Talisman => Crafting.Talismans(world, cultivator),
                ArtKind.Formation => Crafting.Formation(world, cultivator),
                _ => false,
            };
            if (!crafted)
            {
                ActionEffects.Cultivate(world, cultivator, 0.7);
                return;
            }

            cultivator.ArtSkill = Math.Min(100, cultivator.ArtSkill + (1.5 * (1 - (cultivator.ArtSkill / 110))));
            TitleSystem.CheckArt(world, cultivator);
        }

        /// <summary>단약을 지역 시장에 내놓는다. 상회가 값의 60%를 기예가에게 치른다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="crafter">기예가.</param>
        /// <param name="pill">단약.</param>
        public static void ListPill(World world, Cultivator crafter, PillKind pill)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(crafter);
            Region region = world.Regions[crafter.RegionId];
            if (region.MarketPills[(int)pill] >= MarketStockLimit)
            {
                return;
            }

            region.MarketPills[(int)pill]++;
            long paid = PillValue(pill, crafter.Realm) * 6 / 10;
            crafter.SpiritStones += paid;
            world.Ledger.Create(StoneSource.MerchantSale, paid);
        }

        /// <summary>부적을 지역 시장에 내놓는다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="crafter">기예가.</param>
        /// <param name="talisman">부적.</param>
        public static void ListTalisman(World world, Cultivator crafter, TalismanKind talisman)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(crafter);
            Region region = world.Regions[crafter.RegionId];
            if (region.MarketTalismans[(int)talisman] >= MarketStockLimit)
            {
                return;
            }

            region.MarketTalismans[(int)talisman]++;
            long paid = TalismanValue(talisman) * 6 / 10;
            crafter.SpiritStones += paid;
            world.Ledger.Create(StoneSource.MerchantSale, paid);
        }

        /// <summary>상회에서 재료를 산다(판매가의 2배).</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <param name="herbs">영초 수.</param>
        /// <param name="ore">영철 수.</param>
        /// <returns>샀으면 true.</returns>
        public static bool BuyMaterials(World world, Cultivator cultivator, int herbs, int ore)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            long cost = (long)Math.Round(((herbs * Balance.HerbSellPrice) + (ore * Balance.OreSellPrice))
                * Balance.MerchantMarkup);
            if (cultivator.SpiritStones < cost * 2)
            {
                return false;
            }

            cultivator.SpiritStones -= cost;
            world.Ledger.Destroy(StoneSink.MerchantPurchase, cost);
            cultivator.Herbs += herbs;
            cultivator.Ore += ore;
            return true;
        }

        private static void SellSurplus(World world, Cultivator cultivator)
        {
            int keepHerbs = cultivator.Art is ArtKind.Alchemy ? 12 : cultivator.Art == ArtKind.Talisman ? 6 : 4;
            int keepOre = cultivator.Art is ArtKind.Refining ? 12 : cultivator.Art == ArtKind.Talisman ? 6 : 2;
            int herbs = Math.Max(0, cultivator.Herbs - keepHerbs);
            int ore = Math.Max(0, cultivator.Ore - keepOre);
            long income = (herbs * (long)Balance.HerbSellPrice) + (ore * (long)Balance.OreSellPrice)
                + (cultivator.BeastCores * (long)Balance.BeastCoreSellPrice);
            cultivator.Herbs -= herbs;
            cultivator.Ore -= ore;
            cultivator.BeastCores = 0;
            cultivator.SpiritStones += income;
            world.Ledger.Create(StoneSource.MerchantSale, income);
        }

        private static void BuyTechnique(World world, Cultivator cultivator)
        {
            int next = cultivator.TechniqueGrade + 1;
            long price = Techniques.MarketPrice(next);
            if (next > 9 || cultivator.SpiritStones < price * 6 / 5)
            {
                return;
            }

            cultivator.SpiritStones -= price;
            world.Ledger.Destroy(StoneSink.MerchantPurchase, price);
            cultivator.TechniqueGrade = next;
            Narrator.Biography(world, cultivator, "상회에서 " + Techniques.Name(next) + " 공법서를 샀다.");
        }

        private static void BuyFromMarket(World world, Cultivator cultivator)
        {
            Region region = world.Regions[cultivator.RegionId];
            bool atPeak = cultivator.Stage == Realms.PeakStage;
            if (atPeak && cultivator.Pills[(int)PillKind.Breakthrough] == 0)
            {
                TryBuyPill(world, cultivator, region, PillKind.Breakthrough);
            }

            if (cultivator.HealthRatio < 0.6 && cultivator.Pills[(int)PillKind.Healing] == 0)
            {
                TryBuyPill(world, cultivator, region, PillKind.Healing);
            }

            bool tribulationAhead = atPeak && cultivator.Realm + 1 >= Realms.FirstTribulationRealm;
            int thunder = (int)TalismanKind.Thunder;
            long thunderPrice = TalismanValue(TalismanKind.Thunder);
            if (tribulationAhead && cultivator.Talismans[thunder] == 0 && region.MarketTalismans[thunder] > 0
                && cultivator.SpiritStones >= thunderPrice)
            {
                region.MarketTalismans[thunder]--;
                cultivator.Talismans[thunder]++;
                cultivator.SpiritStones -= thunderPrice;
                world.Ledger.Destroy(StoneSink.MerchantPurchase, thunderPrice);
            }
        }

        private static void TryBuyPill(World world, Cultivator cultivator, Region region, PillKind pill)
        {
            int index = (int)pill;
            long price = PillValue(pill, cultivator.Realm);
            if (region.MarketPills[index] <= 0 || cultivator.SpiritStones < price)
            {
                return;
            }

            region.MarketPills[index]--;
            cultivator.Pills[index]++;
            cultivator.SpiritStones -= price;
            world.Ledger.Destroy(StoneSink.MerchantPurchase, price);
        }
    }
}
