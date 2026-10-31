// 종전 처리: 대표 결투, 승패 판정, 배상, 전쟁 영웅, 전쟁 기록.
using System;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>종전 규칙.</summary>
    public static class WarSettlement
    {
        private const int MaxHighlights = 12;
        private const int RegionScore = 30;
        private const int ChampionBonus = 50;

        /// <summary>이번 전쟁의 주요 장면을 남긴다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="text">장면.</param>
        public static void Highlight(World world, string text)
        {
            ArgumentNullException.ThrowIfNull(world);
            if (world.War.Highlights.Count < MaxHighlights)
            {
                world.War.Highlights.Add(world.Year + "년 " + text);
            }
        }

        /// <summary>
        /// 종전: 대표 결투 → 점수(전투 승리 + 점령지×30 + 대표 결투 50) → 패자 금고의 10% 배상 → 공훈 1위 전쟁 영웅.
        /// </summary>
        /// <param name="world">세계.</param>
        public static void EndWar(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            Sect orthodox = world.Sects[0];
            Sect demonic = world.Sects[1];
            WarRecord record = new()
            {
                Number = world.War.WarNumber,
                StartYear = world.War.StartYear,
                EndYear = world.Year,
                OrthodoxWins = world.War.OrthodoxWins,
                DemonicWins = world.War.DemonicWins,
                Casualties = world.War.Casualties,
            };
            foreach (int regionId in RegionCatalog.BorderIds)
            {
                int controller = world.Regions[regionId].ControllerSectId;
                record.OrthodoxRegions += controller == orthodox.Id ? 1 : 0;
                record.DemonicRegions += controller == demonic.Id ? 1 : 0;
            }

            int duelWinnerSect = ChampionDuel(world, record);
            int orthodoxScore = record.OrthodoxWins + (record.OrthodoxRegions * RegionScore)
                + (duelWinnerSect == orthodox.Id ? ChampionBonus : 0);
            int demonicScore = record.DemonicWins + (record.DemonicRegions * RegionScore)
                + (duelWinnerSect == demonic.Id ? ChampionBonus : 0);
            record.VictorSectId = orthodoxScore == demonicScore ? 0 : orthodoxScore > demonicScore ? orthodox.Id
                : demonic.Id;
            PayTribute(world, record, orthodox, demonic);
            CrownHero(world, record);
            record.Highlights.AddRange(world.War.Highlights);
            world.Wars.Add(record);
            world.War.AtWar = false;
            world.War.LastWarEndYear = world.Year;
            foreach (Cultivator cultivator in world.Cultivators)
            {
                cultivator.OnWarDuty = false;
            }

            string verdict = record.VictorSectId == 0 ? "승부를 가리지 못했다"
                : Narrator.Affiliation(world, record.VictorSectId) + "의 승리로 끝났다";
            string text = "제" + record.Number + "차 정마대전이 " + verdict + ". (전투 " + record.OrthodoxWins + ":"
                + record.DemonicWins + ", 점령지 " + record.OrthodoxRegions + ":" + record.DemonicRegions + ", 배상 영석 "
                + KoreanNumber.Compact(record.Tribute) + ")";
            Narrator.Log(world, LogKind.War, text);
            Narrator.Chronicle(world, ChronicleKind.War, text);
        }

        private static int ChampionDuel(World world, WarRecord record)
        {
            Cultivator? orthodoxChampion = Strongest(world, RegionCatalog.OrthodoxSectId);
            Cultivator? demonicChampion = Strongest(world, RegionCatalog.DemonicSectId);
            if (orthodoxChampion is null || demonicChampion is null)
            {
                return 0;
            }

            DuelResult result = CombatSystem.Fight(world, orthodoxChampion, demonicChampion, BattleKind.Grudge);
            TitleSystem.Award(world, result.Winner, TitleId.Champion);
            record.ChampionDuel = result.Winner.DisplayName + "(" + Narrator.Affiliation(world, result.Winner.SectId)
                + ")이(가) " + result.Loser.DisplayName + "을(를) 꺾었다";
            Narrator.Log(world, LogKind.War, "[대표 결투] " + record.ChampionDuel + ".", result.Winner, result.Loser,
                notable: true);
            Narrator.Biography(world, result.Winner, "정마대전 대표 결투에서 " + result.Loser.Name + "을(를) 꺾었다.");
            Narrator.Biography(world, result.Loser, "정마대전 대표 결투에서 " + result.Winner.Name + "에게 졌다.");
            return result.Winner.SectId;
        }

        private static Cultivator? Strongest(World world, int sectId)
        {
            Cultivator? best = null;
            double bestPower = 0;
            foreach (Cultivator cultivator in world.Cultivators)
            {
                if (cultivator.SectId != sectId)
                {
                    continue;
                }

                double power = CombatSystem.Power(world, cultivator);
                if (power > bestPower)
                {
                    best = cultivator;
                    bestPower = power;
                }
            }

            return best;
        }

        private static void PayTribute(World world, WarRecord record, Sect orthodox, Sect demonic)
        {
            if (record.VictorSectId == 0)
            {
                return;
            }

            Sect victor = record.VictorSectId == orthodox.Id ? orthodox : demonic;
            Sect loser = victor == orthodox ? demonic : orthodox;
            long tribute = loser.Treasury / 10;
            loser.Treasury -= tribute;
            victor.Treasury += tribute;
            victor.WarsWon++;
            loser.WarsLost++;
            victor.LosingStreak = 0;
            loser.LosingStreak++;
            record.Tribute = tribute;
        }

        private static void CrownHero(World world, WarRecord record)
        {
            Cultivator? hero = null;
            foreach (Cultivator cultivator in world.Cultivators)
            {
                if (cultivator.WarMerit > 0 && (hero is null || cultivator.WarMerit > hero.WarMerit))
                {
                    hero = cultivator;
                }
            }

            if (hero is null)
            {
                return;
            }

            record.HeroId = hero.Id;
            record.HeroName = hero.DisplayName + "(" + Narrator.Affiliation(world, hero.SectId) + ", 공훈 "
                + KoreanNumber.Grouped(hero.WarMerit) + ")";
            TitleSystem.Award(world, hero, TitleId.WarHero);
            SectSystem.AddContribution(world, hero, 2000);
        }
    }
}
