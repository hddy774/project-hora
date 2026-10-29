// 생애 주기: 지역 이동, 노화·좌화, 윤회, 비승(새 영혼 탄생), 전생 각성.
using System;
using System.Collections.Generic;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>생애 주기 규칙.</summary>
    public static class LifeCycleSystem
    {
        /// <summary>지역을 옮기고 거주자 색인을 갱신한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <param name="regionId">새 지역.</param>
        public static void MoveTo(World world, Cultivator cultivator, int regionId)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            if (cultivator.RegionId == regionId)
            {
                return;
            }

            world.Regions[cultivator.RegionId].Residents.Remove(cultivator);
            cultivator.RegionId = regionId;
            world.Regions[regionId].Residents.Add(cultivator);
        }

        /// <summary>한 해 나이를 먹고 수명이 다했으면 좌화 후 윤회한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <returns>이번 해에 생이 끝났으면 true.</returns>
        public static bool Age(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            cultivator.Age++;
            if (cultivator.Age < cultivator.Lifespan)
            {
                return false;
            }

            // 경지가 떨어져 수명이 줄어든 경우를 따로 센다(원본에서는 이 원인이 수명종료와 구분되지 않았다).
            bool collapsed = cultivator.Realm < cultivator.HighestRealm
                && cultivator.Age < Realms.BaseLifespan(cultivator.HighestRealm);
            Die(world, cultivator, collapsed ? DeathCause.Collapse : DeathCause.Lifespan);
            return true;
        }

        /// <summary>좌화: 기록을 남기고 인연을 정리한 뒤 곧바로 윤회한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <param name="cause">원인.</param>
        public static void Die(World world, Cultivator cultivator, DeathCause cause)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            if (cause == DeathCause.Collapse)
            {
                world.Totals.CollapseDeaths++;
            }
            else
            {
                world.Totals.NaturalDeaths++;
            }

            string ending = Labels.Of(cause);
            bool wasLeader = world.FindSect(cultivator.SectId)?.LeaderId == cultivator.Id;
            RecordSystem.EndLife(world, cultivator, ending);
            RelationshipSystem.OnLifeEnd(world, cultivator, ascended: false);
            bool notable = cultivator.Realm >= 4 || wasLeader || world.IsFollowed(cultivator.Id);
            string text = cultivator.DisplayName + "(" + cultivator.RealmText + ", " + cultivator.Age + "세)이(가) "
                + (cause == DeathCause.Collapse ? "경지가 무너져 남은 수명을 잃고 " : "수명을 다해 ") + "좌화했다.";
            if (notable || world.LogRandom.Chance(0.05))
            {
                Narrator.Log(world, LogKind.Rebirth, text, cultivator, notable: notable);
            }
            else
            {
                world.Log.CountOnly(LogKind.Rebirth);
            }

            if (cultivator.Realm >= 6 || wasLeader)
            {
                Narrator.Chronicle(world, ChronicleKind.Death, text, cultivator);
            }

            Inherit(world, cultivator);
            int previousSect = cultivator.SectId;
            SectSystem.Leave(world, cultivator);
            Rebirth(world, cultivator, previousSect);
        }

        /// <summary>
        /// 윤회: 같은 영혼이 열여섯 살로 다시 태어난다. 전생 업보만큼 재능 하한이 오르고,
        /// 55%는 전생 종문에, 나머지는 떠돌이로 태어난다.
        /// </summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <param name="previousSect">전생 종문.</param>
        public static void Rebirth(World world, Cultivator cultivator, int previousSect)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            int karma = RecordSystem.KarmaFloor(world, cultivator.Id);
            (Personality, Element) previous = (cultivator.Personality, cultivator.Element);
            Sect? sect = previousSect != 0 && world.Random.Chance(0.55) ? world.FindSect(previousSect) : null;
            cultivator.Reincarnations++;
            cultivator.SectId = sect?.Id ?? 0;
            CultivatorFactory.RollLife(world, cultivator, karma, previous);
            world.Diagnostics?.BeginLife(world.Year, cultivator);
            cultivator.SectId = 0;
            cultivator.Rank = SectRank.None;
            int region;
            if (sect is not null)
            {
                SectSystem.Join(world, cultivator, sect);
                region = world.Random.Pick(RegionCatalog.TerritoryOf(sect.Id));
            }
            else
            {
                region = world.Random.Pick(RegionCatalog.BorderIds);
            }

            MoveTo(world, cultivator, region);
            world.Ledger.Create(StoneSource.Initial, cultivator.SpiritStones);
            string where = sect is null ? "떠돌이로" : sect.Name + "에서";
            Narrator.Biography(world, cultivator, cultivator.LifeNumber + "번째 생. " + where + " 다시 태어났다.");
            if (world.IsFollowed(cultivator.Id))
            {
                Narrator.Log(world, LogKind.Rebirth, cultivator.Name + "이(가) " + where + " " + cultivator.LifeNumber
                    + "번째 생을 시작했다. (" + Labels.TalentGrade(cultivator.TalentGrade) + ")", cultivator, notable: true);
            }
        }

        /// <summary>
        /// 비승: 영혼이 선계로 떠나 비승 명부에 오른다. 같은 자리에 새 영혼이 태어나 인구가 유지된다.
        /// </summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">비승하는 수선자.</param>
        public static void Ascend(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            Sect? sect = world.FindSect(cultivator.SectId);
            string affiliation = Narrator.Affiliation(world, cultivator.SectId);
            world.Totals.Ascensions++;
            if (sect is not null)
            {
                sect.Ascensions++;
            }

            world.Immortals.Add(new ImmortalRecord(world.Year, cultivator.Id, cultivator.DisplayName,
                cultivator.LifeNumber, cultivator.Age, affiliation));
            RecordSystem.EndLife(world, cultivator, "비승");
            world.FindSoul(cultivator.Id)!.Ascended = true;
            RelationshipSystem.OnLifeEnd(world, cultivator, ascended: true);
            string text = cultivator.DisplayName + "(" + affiliation + ")이(가) " + cultivator.LifeNumber
                + "생의 수행 끝에 비승했다! 하늘 문이 열리고 선계로 올랐다.";
            Narrator.Log(world, LogKind.Ascension, text, cultivator, notable: true);
            Narrator.Chronicle(world, ChronicleKind.Ascension, text, cultivator);
            world.Ledger.Destroy(StoneSink.Ascension, cultivator.SpiritStones);
            cultivator.SpiritStones = 0;
            SectSystem.Leave(world, cultivator);
            world.SetFollowed(cultivator.Id, false);
            ReplaceWithNewSoul(world, cultivator);
        }

        /// <summary>
        /// 전생 각성: 윤회한 영혼이 드물게 전생의 깨달음과 공법을 되찾는다.
        /// </summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        public static void TryAwaken(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            if (cultivator.MemoryAwakened || cultivator.Reincarnations == 0 || cultivator.Age < 25
                || cultivator.Age > 300)
            {
                return;
            }

            if (!world.Random.Chance(0.0008 * Math.Min(10, cultivator.Reincarnations)))
            {
                return;
            }

            cultivator.MemoryAwakened = true;
            SoulRecord? soul = world.FindSoul(cultivator.Id);
            int remembered = Math.Min(12, (soul?.BestRealm ?? 0) * 2);
            cultivator.TechniqueGrade = Math.Max(cultivator.TechniqueGrade, remembered);
            Growth.AddInsight(cultivator, 300);
            Growth.AddDaoHeart(cultivator, 10);
            TitleSystem.Award(world, cultivator, TitleId.Awakened);
            Narrator.Log(world, LogKind.Fortune, cultivator.Name + "이(가) 전생의 기억을 되찾았다. ("
                + Techniques.Name(cultivator.TechniqueGrade) + " 공법)", cultivator, notable: true);
        }

        /// <summary>
        /// [보존] 유산: 남은 영석은 살아 있는 제자 → 도려 → 종문 금고 순으로 넘어가고, 받을 이가 없으면 흩어진다(장부에 소멸로 기록).
        /// </summary>
        private static void Inherit(World world, Cultivator departed)
        {
            long estate = departed.SpiritStones;
            departed.SpiritStones = 0;
            if (estate <= 0)
            {
                return;
            }

            Cultivator? heir = null;
            foreach (int discipleId in departed.DiscipleIds)
            {
                heir ??= world.Find(discipleId);
            }

            heir ??= world.Find(departed.CompanionId);
            if (heir is not null)
            {
                heir.SpiritStones += estate;
                Narrator.Biography(world, heir, departed.Name + "의 유산 영석 " + KoreanNumber.Compact(estate) + "을 물려받았다.");
                return;
            }

            Sect? sect = world.FindSect(departed.SectId);
            if (sect is not null)
            {
                sect.Treasury += estate;
                return;
            }

            world.Ledger.Destroy(StoneSink.Death, estate);
        }

        private static void ReplaceWithNewSoul(World world, Cultivator departed)
        {
            int slot = world.Cultivators.IndexOf(departed);
            world.Regions[departed.RegionId].Residents.Remove(departed);
            world.Unregister(departed.Id);
            HashSet<string> names = new();
            foreach (Cultivator living in world.Cultivators)
            {
                names.Add(living.Name);
            }

            int sectId = world.Random.Chance(0.5) ? 0 : world.Random.Range(1, world.Sects.Count);
            int region = sectId == 0 ? world.Random.Pick(RegionCatalog.BorderIds) : RegionCatalog.HomeOf(sectId);
            Cultivator newcomer = CultivatorFactory.CreateSoul(world, 0, region, names);
            world.Diagnostics?.BeginLife(world.Year, newcomer);
            world.Cultivators[slot] = newcomer;
            world.Register(newcomer);
            world.Regions[region].Residents.Add(newcomer);
            world.Ledger.Create(StoneSource.Initial, newcomer.SpiritStones);
            Sect? sect = world.FindSect(sectId);
            if (sect is not null)
            {
                SectSystem.Join(world, newcomer, sect);
            }

            Narrator.Biography(world, newcomer, "비승한 " + departed.Name + "의 빈자리에 새 영혼으로 태어났다.");
        }
    }
}
