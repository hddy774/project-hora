// 천하 대비무 대회: 휴전 중 천 년마다(종전 500년 뒤부터) 종문 고수와 떠돌이 고수가 토너먼트로 겨룬다.
using System;
using System.Collections.Generic;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>대비무 대회 규칙.</summary>
    public static class TournamentSystem
    {
        private const int PerSectEntrants = 12;
        private const int RogueEntrants = 8;

        /// <summary>대회가 열리는 해면 연다.</summary>
        /// <param name="world">세계.</param>
        public static void Update(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            if (world.War.AtWar || world.Year <= world.War.LastWarEndYear)
            {
                return;
            }

            int sincePeace = world.Year - world.War.LastWarEndYear;
            if (sincePeace % Balance.TournamentInterval == Balance.TournamentOffset)
            {
                Hold(world);
            }
        }

        /// <summary>다음 대회까지 남은 년수(전쟁 중이면 -1).</summary>
        /// <param name="world">세계.</param>
        /// <returns>년수.</returns>
        public static int YearsUntilNext(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            if (world.War.AtWar)
            {
                return -1;
            }

            int sincePeace = world.Year - world.War.LastWarEndYear;
            int phase = sincePeace % Balance.TournamentInterval;
            int wait = phase < Balance.TournamentOffset ? Balance.TournamentOffset - phase
                : Balance.TournamentInterval - phase + Balance.TournamentOffset;
            return wait < WarSystem.YearsUntilChange(world.Year) ? wait : -1;
        }

        /// <summary>대회를 연다(천기 개입 없이 일정에 따라).</summary>
        /// <param name="world">세계.</param>
        public static void Hold(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            List<Cultivator> entrants = Entrants(world);
            int size = 1;
            while (size * 2 <= entrants.Count)
            {
                size *= 2;
            }

            if (size < 4)
            {
                return;
            }

            entrants.RemoveRange(size, entrants.Count - size);
            TournamentRecord record = new()
            {
                Number = world.Tournaments.Count + 1,
                Year = world.Year,
                Entrants = size
            };
            List<Cultivator> round = Seed(entrants);
            int roundNumber = 1;
            while (round.Count > 1)
            {
                round = PlayRound(world, record, round, roundNumber);
                roundNumber++;
            }

            Conclude(world, record, round[0]);
        }

        private static List<Cultivator> Entrants(World world)
        {
            List<(Cultivator Who, double Power)> orthodox = new();
            List<(Cultivator Who, double Power)> demonic = new();
            List<(Cultivator Who, double Power)> rogues = new();
            foreach (Cultivator cultivator in world.Cultivators)
            {
                if (cultivator.Realm < 1 || cultivator.RecoveryYears > 0)
                {
                    continue;
                }

                List<(Cultivator, double)> pool = cultivator.SectId == RegionCatalog.OrthodoxSectId ? orthodox
                    : cultivator.SectId == RegionCatalog.DemonicSectId ? demonic : rogues;
                pool.Add((cultivator, CombatSystem.Power(world, cultivator)));
            }

            List<(Cultivator Who, double Power)> chosen = new();
            chosen.AddRange(Top(orthodox, PerSectEntrants));
            chosen.AddRange(Top(demonic, PerSectEntrants));
            chosen.AddRange(Top(rogues, RogueEntrants));
            chosen.Sort((left, right) => right.Power.CompareTo(left.Power));
            return chosen.ConvertAll(entry => entry.Who);
        }

        private static List<(Cultivator Who, double Power)> Top(List<(Cultivator Who, double Power)> pool, int count)
        {
            pool.Sort((left, right) => right.Power.CompareTo(left.Power));
            return pool.GetRange(0, Math.Min(count, pool.Count));
        }

        /// <summary>표준 시드 배치: 1번 시드와 마지막 시드가 첫판에서 만난다.</summary>
        private static List<Cultivator> Seed(List<Cultivator> bySeed)
        {
            List<int> order = new() { 0 };
            while (order.Count < bySeed.Count)
            {
                int next = (order.Count * 2) - 1;
                List<int> expanded = new(order.Count * 2);
                foreach (int seed in order)
                {
                    expanded.Add(seed);
                    expanded.Add(next - seed);
                }

                order = expanded;
            }

            return order.ConvertAll(seed => bySeed[seed]);
        }

        private static List<Cultivator> PlayRound(World world, TournamentRecord record, List<Cultivator> round,
            int number)
        {
            List<Cultivator> winners = new(round.Count / 2);
            for (int index = 0; index + 1 < round.Count; index += 2)
            {
                DuelResult result = CombatSystem.Fight(world, round[index], round[index + 1], BattleKind.Tournament);
                winners.Add(result.Winner);
                record.Matches.Add(new TournamentMatch(number, result.Winner.Id, result.Winner.DisplayName,
                    result.Loser.Id, result.Loser.DisplayName, result.Upset));
                // 이번 판에서 진 사람의 순위 = 이 판의 인원(결승 2, 4강 4, 8강 8 ...).
                int placement = round.Count;
                if (result.Loser.TournamentBest == 0 || placement < result.Loser.TournamentBest)
                {
                    result.Loser.TournamentBest = placement;
                }
            }

            return winners;
        }

        private static void Conclude(World world, TournamentRecord record, Cultivator champion)
        {
            TournamentMatch final = record.Matches[^1];
            Cultivator? runnerUp = world.Find(final.LoserId);
            champion.TournamentBest = 1;
            record.ChampionId = champion.Id;
            record.ChampionName = champion.DisplayName;
            record.ChampionAffiliation = Narrator.Affiliation(world, champion.SectId);
            record.RunnerUpName = final.LoserName;
            record.Prize = Prize(world);
            champion.SpiritStones += record.Prize;
            Growth.AddInsight(champion, 300);
            Growth.AddDaoHeart(champion, 10);
            TitleSystem.Award(world, champion, TitleId.TournamentChampion);
            if (runnerUp is not null)
            {
                TitleSystem.Award(world, runnerUp, TitleId.TournamentRunnerUp);
            }

            Sect? sect = world.FindSect(champion.SectId);
            if (sect is not null)
            {
                sect.TournamentWins++;
                SectSystem.AddContribution(world, champion, 1500);
            }

            world.Tournaments.Add(record);
            string text = "제" + record.Number + "회 천하 대비무(" + record.Entrants + "강): " + champion.DisplayName + "("
                + record.ChampionAffiliation + ", " + champion.RealmText + ") 우승. 결승 상대 " + final.LoserName + ".";
            Narrator.Log(world, LogKind.Tournament, text, champion, runnerUp, notable: true);
            Narrator.Chronicle(world, ChronicleKind.Tournament, text, champion);
            Narrator.Biography(world, champion, "제" + record.Number + "회 천하 대비무에서 우승했다.");
        }

        private static long Prize(World world)
        {
            // [보존] 상금은 두 종문 금고에서 2%씩 걷는다.
            long prize = 0;
            foreach (Sect sect in world.Sects)
            {
                long share = sect.Treasury / 50;
                sect.Treasury -= share;
                prize += share;
            }

            return prize;
        }
    }
}
