// 종문 재정: 영지 산출, 녹봉, 시설 건설, 연단방 단약 생산, 장경각·연단방 공헌 교환.
// [보존] 원본 녹봉은 어디서도 빠지지 않고 생겨났다. 여기서는 영지 산출만 새로 생기고 녹봉은 금고에서 나간다.
using System;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>종문 재정 규칙.</summary>
    public static class SectEconomy
    {
        // 연단방 단약 공헌 가격. 인덱스는 PillKind.
        private static readonly int[] s_pillPrice = { 40, 150, 60, 2000 };

        /// <summary>100년 정산: 영지 산출 → 녹봉 → 시설 → 단약 생산.</summary>
        /// <param name="world">세계.</param>
        public static void Settle(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            PayPersonalUpkeep(world);
            foreach (Sect sect in world.Sects)
            {
                CollectTerritory(world, sect);
                PayUpkeep(world, sect);
                PayStipends(world, sect);
                InvestInFacility(world, sect);
                ProducePills(world, sect);
            }
        }

        private static void PayPersonalUpkeep(World world)
        {
            // 녹봉은 이전이므로 세계 총량을 줄이지 않는다. 개인 자원의5%를 백년 수행에 소모한다.
            // 가난한 인물은 반올림 없이0을 내며, 실제 차감액만 장부에 기록한다.
            foreach (Cultivator person in world.Cultivators)
            {
                long expense = person.SpiritStones / 20;
                person.SpiritStones -= expense;
                world.Ledger.Destroy(StoneSink.Cultivation, expense);
            }
        }

        /// <summary>연단방 단약의 공헌 가격.</summary>
        /// <param name="pill">단약.</param>
        /// <param name="realm">받는 사람 경지(파경단은 경지가 높을수록 비싸다).</param>
        /// <returns>공헌점.</returns>
        public static int PillPrice(PillKind pill, int realm)
        {
            int price = s_pillPrice[(int)pill];
            return pill == PillKind.Breakthrough ? price * (realm + 1) : price;
        }

        /// <summary>
        /// 공헌점으로 연단방 단약을 받는다. 필요한 사람(대원만에 가까운데 파경단이 없거나, 부상이 큰 사람)만 신청한다.
        /// </summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">구성원.</param>
        /// <returns>받았으면 true.</returns>
        public static bool RequestPill(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            Sect? sect = world.FindSect(cultivator.SectId);
            if (sect is null)
            {
                return false;
            }

            PillKind? wanted = cultivator.Stage == Realms.PeakStage && cultivator.Pills[(int)PillKind.Breakthrough] == 0
                ? PillKind.Breakthrough
                : cultivator.HealthRatio < 0.6 ? PillKind.Healing
                : cultivator.DaoHeart < 40 ? PillKind.Clarity : null;
            if (wanted is null)
            {
                return false;
            }

            int index = (int)wanted.Value;
            int price = PillPrice(wanted.Value, cultivator.Realm);
            if (sect.PillStock[index] <= 0 || cultivator.ContributionPoints < price)
            {
                return false;
            }

            sect.PillStock[index]--;
            cultivator.ContributionPoints -= price;
            cultivator.Pills[index]++;
            return true;
        }

        /// <summary>장경각에서 다음 등급 공법을 익힌다(공헌점 소모).</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">구성원.</param>
        /// <returns>익혔으면 true.</returns>
        public static bool StudyAtPavilion(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            Sect? sect = world.FindSect(cultivator.SectId);
            if (sect is null)
            {
                return false;
            }

            int next = cultivator.TechniqueGrade + 1;
            int limit = Techniques.PavilionGradeLimit(sect.Level(FacilityKind.Pavilion));
            int cost = Techniques.PavilionCost(next);
            if (next > limit || cultivator.ContributionPoints < cost)
            {
                return false;
            }

            cultivator.ContributionPoints -= cost;
            cultivator.TechniqueGrade = next;
            if (world.Random.Chance(0.5))
            {
                cultivator.TechniqueElement = cultivator.Element;
            }

            string text = cultivator.Name + "이(가) 장경각에서 " + Techniques.Name(next) + " 공법을 익혔다.";
            if (next >= 10 || world.IsFollowed(cultivator.Id))
            {
                Narrator.Log(world, LogKind.Sect, text, cultivator, notable: true);
            }

            Narrator.Biography(world, cultivator, Techniques.Name(next) + " 공법을 익혔다.");
            return true;
        }

        private static void CollectTerritory(World world, Sect sect)
        {
            long income = 0;
            foreach (Region region in world.Regions)
            {
                if (region.ControllerSectId == sect.Id)
                {
                    income += (long)Math.Round(1500 * region.Aura);
                }
            }

            sect.Treasury += income;
            world.Ledger.Create(StoneSource.Territory, income);
        }

        private static void PayStipends(World world, Sect sect)
        {
            foreach (Cultivator member in world.Cultivators)
            {
                if (member.SectId != sect.Id || member.Rank == SectRank.None)
                {
                    continue;
                }

                // 녹봉은 직위 기본액 × (1 + 경지/4). 고수가 많아질수록 지출도 커져 금고가 끝없이 불어나지 않는다.
                long pay = Math.Min(sect.Treasury, SectRules.Stipend(member.Rank) * (4L + member.Realm) / 4);
                sect.Treasury -= pay;
                member.SpiritStones += pay;
            }
        }

        /// <summary>전쟁 중 매년 금고의 0.1%를 군비로 쓴다.</summary>
        /// <param name="world">세계.</param>
        public static void PayWarExpenses(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            foreach (Sect sect in world.Sects)
            {
                long expense = sect.Treasury / 1000;
                sect.Treasury -= expense;
                world.Ledger.Destroy(StoneSink.War, expense);
            }
        }

        private static void PayUpkeep(World world, Sect sect)
        {
            long upkeep = 0;
            foreach (int level in sect.Facilities)
            {
                upkeep += 300L * level * level;
            }

            upkeep = Math.Min(upkeep, sect.Treasury);
            sect.Treasury -= upkeep;
            world.Ledger.Destroy(StoneSink.Upkeep, upkeep);
        }

        private static void InvestInFacility(World world, Sect sect)
        {
            foreach (FacilityKind facility in SectRules.FacilityPriority(sect.Orthodox))
            {
                int level = sect.Level(facility);
                long cost = SectRules.UpgradeCost(level);
                // 금고를 비우지 않도록 비용의 1.5배가 있을 때만 짓는다.
                if (level >= SectRules.MaxFacilityLevel || sect.Treasury < cost * 3 / 2)
                {
                    continue;
                }

                sect.Treasury -= cost;
                world.Ledger.Destroy(StoneSink.Facility, cost);
                sect.Facilities[(int)facility] = level + 1;
                string text = "[" + sect.Name + "] " + SectRules.FacilityName(facility) + "을(를) " + (level + 1)
                    + "단계로 증축했다. (" + SectRules.FacilityEffect(facility) + ")";
                Narrator.Log(world, LogKind.Sect, text);
                if (level + 1 >= 3)
                {
                    Narrator.Chronicle(world, ChronicleKind.Facility, text);
                }

                return;
            }
        }

        private static void ProducePills(World world, Sect sect)
        {
            int batch = 6 * sect.Level(FacilityKind.AlchemyHall);
            for (int index = 0; index < batch; index++)
            {
                double roll = world.Random.NextDouble();
                PillKind pill = roll < 0.5 ? PillKind.Breakthrough : roll < 0.85 ? PillKind.Healing : PillKind.Clarity;
                sect.PillStock[(int)pill] = Math.Min(60, sect.PillStock[(int)pill] + 1);
            }
        }
    }
}
