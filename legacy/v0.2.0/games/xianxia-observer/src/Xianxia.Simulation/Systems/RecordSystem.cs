// 생애 점수, 영혼 업적(원본 HD), 세계 기록. 원본은 최고 단계 칸에 경지 값을 넣어 "반선경 undefined"가 표시됐다.
using System;
using System.Collections.Generic;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>기록 규칙.</summary>
    public static class RecordSystem
    {
        /// <summary>
        /// 생애 업적 점수: 경지×1000 + 단계×100 + 대경지 돌파×20 + 승리×2 + 천겁×80 + 호칭 점수 + 대회 성적 + 전쟁 공훈/10.
        /// </summary>
        /// <param name="cultivator">수선자.</param>
        /// <returns>점수.</returns>
        public static int LifeScore(Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(cultivator);
            int tournament = cultivator.TournamentBest switch
            {
                1 => 500,
                2 => 250,
                4 => 120,
                8 => 50,
                _ => 0,
            };
            double score = (cultivator.HighestRealm * 1000) + (cultivator.HighestStage * 100)
                + (cultivator.MajorBreakthroughs * 20) + (cultivator.Wins * 2) + (cultivator.TribulationsPassed * 80)
                + TitleSystem.Score(cultivator) + tournament + (cultivator.LifetimeWarMerit / 10);
            return (int)Math.Round(score);
        }

        /// <summary>
        /// 경지·단계 진척을 기록한다. 대경지를 처음 밟은 사람이면 연대기에 남기고 true를 돌려준다.
        /// </summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <returns>세계 최초 도달이면 true.</returns>
        public static bool NoteProgress(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            if (Realms.Rank(cultivator.Realm, cultivator.Stage) > Realms.Rank(cultivator.HighestRealm,
                cultivator.HighestStage))
            {
                cultivator.HighestRealm = cultivator.Realm;
                cultivator.HighestStage = cultivator.Stage;
            }

            if (cultivator.Stage != 0 || cultivator.Realm == 0)
            {
                return false;
            }

            world.Diagnostics?.RealmChanged(world.Year, cultivator);
            int realm = cultivator.Realm;
            RecordEntry entry = new(world.Year, cultivator.Id, cultivator.DisplayName, cultivator.Age,
                Realms.Name(realm) + " " + cultivator.Age + "세");
            RecordEntry? youngest = world.Records.YoungestReach[realm];
            if (youngest is null || cultivator.Age < youngest.Value)
            {
                world.Records.YoungestReach[realm] = entry;
            }

            if (world.Records.FirstReach[realm] is not null)
            {
                return false;
            }

            world.Records.FirstReach[realm] = entry;
            Narrator.Chronicle(world, ChronicleKind.FirstRealm, "천하 최초로 " + cultivator.DisplayName + "("
                + Narrator.Affiliation(world, cultivator.SectId) + ")이(가) " + Realms.Name(realm) + "에 올랐다. ("
                + cultivator.Age + "세, " + cultivator.LifeNumber + "번째 생)", cultivator);
            return true;
        }

        /// <summary>한 생을 마감한다: 전생 요약·영혼 업적·세계 기록을 갱신한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <param name="ending">생의 끝(사망 원인 또는 비승).</param>
        /// <returns>생애 점수.</returns>
        public static int EndLife(World world, Cultivator cultivator, string ending)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            world.Diagnostics?.EndLife(world.Year, cultivator);
            if (cultivator.Age >= 10_000)
            {
                TitleSystem.Award(world, cultivator, TitleId.LongLived);
            }

            int score = LifeScore(cultivator);
            cultivator.PastLives.Add(new PastLife(cultivator.LifeNumber, cultivator.BornYear, world.Year,
                cultivator.Age,
                cultivator.HighestRealm, cultivator.HighestStage, cultivator.SectId, ending, score,
                new List<TitleId>(cultivator.Titles)));
            if (cultivator.PastLives.Count > Balance.PastLifeCapacity)
            {
                cultivator.PastLives.RemoveAt(0);
            }

            SoulRecord soul = world.FindSoul(cultivator.Id) ?? CreateSoul(world, cultivator);
            UpdateSoul(soul, cultivator, score, ending);
            UpdateLifeRecords(world, cultivator, soul);
            return score;
        }

        /// <summary>윤회 업보 하한(0~150): 최고 경지·얻어 본 호칭·생 수에 비례한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="soulId">영혼 번호.</param>
        /// <returns>재능 하한 가산.</returns>
        public static int KarmaFloor(World world, int soulId)
        {
            ArgumentNullException.ThrowIfNull(world);
            SoulRecord? soul = world.FindSoul(soulId);
            if (soul is null)
            {
                return 0;
            }

            return Math.Min(150, (soul.BestRealm * 12) + (soul.TitlesEver.Count * 4) + (soul.Lives * 2));
        }

        /// <summary>100년마다: 최고 전투력·최다 영석 기록을 갱신한다.</summary>
        /// <param name="world">세계.</param>
        public static void UpdatePeriodic(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            foreach (Cultivator cultivator in world.Cultivators)
            {
                double power = CombatSystem.Power(world, cultivator);
                if (world.Records.HighestPower is null || power > world.Records.HighestPower.Value)
                {
                    world.Records.HighestPower = new RecordEntry(world.Year, cultivator.Id, cultivator.DisplayName,
                        power,
                        cultivator.RealmText);
                }

                if (world.Records.Richest is null || cultivator.SpiritStones > world.Records.Richest.Value)
                {
                    world.Records.Richest = new RecordEntry(world.Year, cultivator.Id, cultivator.DisplayName,
                        cultivator.SpiritStones, cultivator.RealmText);
                }
            }
        }

        private static SoulRecord CreateSoul(World world, Cultivator cultivator)
        {
            SoulRecord soul = new() { SoulId = cultivator.Id, Name = cultivator.Name };
            world.AddSoul(soul);
            return soul;
        }

        private static void UpdateSoul(SoulRecord soul, Cultivator cultivator, int score, string ending)
        {
            soul.Name = cultivator.Name;
            soul.Lives++;
            soul.LastEnding = ending;
            soul.TotalMajorBreakthroughs += cultivator.MajorBreakthroughs;
            soul.TotalWins += cultivator.Wins;
            soul.TotalTribulations += cultivator.TribulationsPassed;
            if (cultivator.TournamentBest == 1)
            {
                soul.TournamentWins++;
            }

            if (cultivator.HasTitle(TitleId.WarHero))
            {
                soul.WarHeroCount++;
            }

            foreach (TitleId title in cultivator.Titles)
            {
                if (!soul.TitlesEver.Contains(title))
                {
                    soul.TitlesEver.Add(title);
                }
            }

            if (score > soul.BestScore)
            {
                soul.BestScore = score;
                soul.BestRealm = cultivator.HighestRealm;
                soul.BestStage = cultivator.HighestStage;
                soul.BestLife = cultivator.LifeNumber;
            }

            soul.TopScores.Add(score);
            soul.TopScores.Sort((left, right) => right.CompareTo(left));
            if (soul.TopScores.Count > 5)
            {
                soul.TopScores.RemoveRange(5, soul.TopScores.Count - 5);
            }

            int sum = 0;
            foreach (int value in soul.TopScores)
            {
                sum += value;
            }

            soul.TopFiveSum = sum;
        }

        private static void UpdateLifeRecords(World world, Cultivator cultivator, SoulRecord soul)
        {
            WorldRecords records = world.Records;
            if (records.LongestLife is null || cultivator.Age > records.LongestLife.Value)
            {
                records.LongestLife = new RecordEntry(world.Year, cultivator.Id, cultivator.DisplayName, cultivator.Age,
                    cultivator.RealmText);
            }

            if (records.MostWins is null || cultivator.Wins > records.MostWins.Value)
            {
                records.MostWins = new RecordEntry(world.Year, cultivator.Id, cultivator.DisplayName, cultivator.Wins,
                    cultivator.LifeNumber + "번째 생");
            }

            if (records.MostLives is null || soul.Lives > records.MostLives.Value)
            {
                records.MostLives = new RecordEntry(world.Year, soul.SoulId, soul.Name, soul.Lives, "윤회 "
                    + soul.Lives + "회");
            }

            if (soul.TournamentWins > 0 && (records.MostTournamentWins is null
                || soul.TournamentWins > records.MostTournamentWins.Value))
            {
                records.MostTournamentWins = new RecordEntry(world.Year, soul.SoulId, soul.Name, soul.TournamentWins,
                    "대비무 우승한 생 " + soul.TournamentWins + "번");
            }
        }
    }
}
