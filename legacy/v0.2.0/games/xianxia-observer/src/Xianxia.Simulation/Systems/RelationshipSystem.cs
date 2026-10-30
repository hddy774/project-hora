// 인연: 기명 제자(사제), 도려(도반), 숙적. 원본의 기명 제자 규칙을 계승하고, 쓰이지 않던 숙적(ei)·도려(dw) 칸을 실제 규칙으로 만들었다.
using System;
using System.Collections.Generic;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>인연 규칙.</summary>
    public static class RelationshipSystem
    {
        private const int CandidateScanLimit = 40;

        /// <summary>한 해 동안 새 인연이 생길 수 있는지 확인한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        public static void Update(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            if (cultivator.Realm >= 3 && cultivator.DiscipleIds.Count < Balance.MaxDisciples
                && world.Random.Chance(0.005))
            {
                TryTakeDisciple(world, cultivator);
            }

            if (cultivator.CompanionId == 0 && cultivator.Realm >= 2 && world.Random.Chance(0.002))
            {
                TryFindCompanion(world, cultivator);
            }
        }

        /// <summary>두 사람을 숙적으로 맺는다(서로 3명 미만일 때).</summary>
        /// <param name="world">세계.</param>
        /// <param name="first">한쪽.</param>
        /// <param name="second">다른 쪽.</param>
        public static void TryFormRivalry(World world, Cultivator first, Cultivator second)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(first);
            ArgumentNullException.ThrowIfNull(second);
            if (first.RivalIds.Contains(second.Id) || first.RivalIds.Count >= Balance.MaxRivals
                || second.RivalIds.Count >= Balance.MaxRivals || first.Id == second.Id)
            {
                return;
            }

            first.RivalIds.Add(second.Id);
            second.RivalIds.Add(first.Id);
            string text = first.Name + "와(과) " + second.Name + "이(가) 숙적이 되었다.";
            Narrator.Log(world, LogKind.Relation, text, first, second, notable: true);
            Narrator.Biography(world, first, second.Name + "와(과) 숙적이 되었다.");
            Narrator.Biography(world, second, first.Name + "와(과) 숙적이 되었다.");
        }

        /// <summary>
        /// 100년마다: 경지 차이가 두 경지 이상 벌어진 숙적 관계는 끝난다.
        /// </summary>
        /// <param name="world">세계.</param>
        public static void Prune(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            foreach (Cultivator cultivator in world.Cultivators)
            {
                for (int index = cultivator.RivalIds.Count - 1; index >= 0; index--)
                {
                    Cultivator? rival = world.Find(cultivator.RivalIds[index]);
                    if (rival is null || Math.Abs(rival.Realm - cultivator.Realm) >= 2)
                    {
                        cultivator.RivalIds.RemoveAt(index);
                        rival?.RivalIds.Remove(cultivator.Id);
                    }
                }
            }
        }

        /// <summary>제자를 지도한다. 같은 지역의 제자가 깨달음과 영기를 얻고 스승은 공헌을 얻는다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="master">스승.</param>
        /// <returns>지도한 제자 수.</returns>
        public static int Teach(World world, Cultivator master)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(master);
            int taught = 0;
            foreach (int discipleId in master.DiscipleIds)
            {
                Cultivator? disciple = world.Find(discipleId);
                if (disciple is null || disciple.RegionId != master.RegionId)
                {
                    continue;
                }

                Growth.AddInsight(disciple, 20 * (0.5 + (master.Comprehension / 500.0)));
                double previousQi = disciple.Qi;
                disciple.Qi += disciple.QiRequirement * 0.05;
                world.Diagnostics?.AddQi(disciple, QiSource.Teaching, disciple.Qi - previousQi);
                taught++;
            }

            SectSystem.AddContribution(world, master, 10 * taught);
            Growth.AddInsight(master, 3 * taught);
            return taught;
        }

        /// <summary>한 생이 끝날 때 인연을 정리하고 남은 사람의 생애·도심에 반영한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">생이 끝난 수선자.</param>
        /// <param name="ascended">비승이면 true(슬픔 대신 감화).</param>
        public static void OnLifeEnd(World world, Cultivator cultivator, bool ascended)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            string verb = ascended ? "비승했다" : "좌화했다";
            Cultivator? master = world.Find(cultivator.MasterId);
            master?.DiscipleIds.Remove(cultivator.Id);
            foreach (int discipleId in cultivator.DiscipleIds)
            {
                Cultivator? disciple = world.Find(discipleId);
                if (disciple is null)
                {
                    continue;
                }

                disciple.MasterId = 0;
                Growth.AddDaoHeart(disciple, ascended ? 10 : -5);
                Narrator.Biography(world, disciple, "스승 " + cultivator.Name + "이(가) " + verb + ".");
            }

            Cultivator? companion = world.Find(cultivator.CompanionId);
            if (companion is not null)
            {
                companion.CompanionId = 0;
                Growth.AddDaoHeart(companion, ascended ? 5 : -20);
                Narrator.Biography(world, companion, "도려 " + cultivator.Name + "이(가) " + verb + ".");
            }

            foreach (int rivalId in cultivator.RivalIds)
            {
                world.Find(rivalId)?.RivalIds.Remove(cultivator.Id);
            }
        }

        private static void TryTakeDisciple(World world, Cultivator master)
        {
            List<Cultivator> residents = world.Regions[master.RegionId].Residents;
            List<Cultivator> candidates = new();
            int start = residents.Count == 0 ? 0 : world.Random.NextInt(residents.Count);
            for (int offset = 0; offset < residents.Count && offset < CandidateScanLimit; offset++)
            {
                Cultivator candidate = residents[(start + offset) % residents.Count];
                if (candidate.Id != master.Id && candidate.MasterId == 0 && candidate.Realm >= 1
                    && candidate.Realm <= master.Realm - 2 && candidate.SectId == master.SectId
                    && candidate.Id != master.MasterId && !master.DiscipleIds.Contains(candidate.Id))
                {
                    candidates.Add(candidate);
                }
            }

            if (candidates.Count == 0)
            {
                return;
            }

            Cultivator disciple = world.Random.Pick(candidates);
            master.DiscipleIds.Add(disciple.Id);
            disciple.MasterId = master.Id;
            string text = master.DisplayName + "이(가) " + disciple.Name + "을(를) 기명 제자로 받아들였다.";
            Narrator.Log(world, LogKind.Relation, text, master, disciple, notable: true);
            Narrator.Biography(world, master, disciple.Name + "을(를) 기명 제자로 받아들였다.");
            Narrator.Biography(world, disciple, master.DisplayName + "의 기명 제자가 되었다.");
        }

        private static void TryFindCompanion(World world, Cultivator seeker)
        {
            List<Cultivator> residents = world.Regions[seeker.RegionId].Residents;
            int start = residents.Count == 0 ? 0 : world.Random.NextInt(residents.Count);
            for (int offset = 0; offset < residents.Count && offset < CandidateScanLimit; offset++)
            {
                Cultivator other = residents[(start + offset) % residents.Count];
                if (!IsCompatibleCompanion(seeker, other))
                {
                    continue;
                }

                double chance = 0.3 + (0.4 * (seeker.Charisma + other.Charisma) / 2);
                if (!world.Random.Chance(chance))
                {
                    return;
                }

                seeker.CompanionId = other.Id;
                other.CompanionId = seeker.Id;
                seeker.CompanionSinceYear = world.Year;
                other.CompanionSinceYear = world.Year;
                string text = seeker.DisplayName + "와(과) " + other.DisplayName + "이(가) 도려를 맺었다.";
                Narrator.Log(world, LogKind.Relation, text, seeker, other, notable: true);
                Narrator.Biography(world, seeker, other.Name + "와(과) 도려를 맺었다.");
                Narrator.Biography(world, other, seeker.Name + "와(과) 도려를 맺었다.");
                return;
            }
        }

        private static bool IsCompatibleCompanion(Cultivator seeker, Cultivator other)
        {
            bool clash = (seeker.Personality == Personality.Righteous && other.Personality == Personality.Demonic)
                || (seeker.Personality == Personality.Demonic && other.Personality == Personality.Righteous);
            return other.Id != seeker.Id && other.CompanionId == 0 && other.Realm >= 2
                && Math.Abs(other.Realm - seeker.Realm) <= 1 && !clash
                && other.MasterId != seeker.Id && seeker.MasterId != other.Id
                && !seeker.RivalIds.Contains(other.Id);
        }
    }
}
