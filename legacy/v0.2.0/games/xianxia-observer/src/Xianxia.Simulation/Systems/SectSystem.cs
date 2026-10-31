// 종문 구성원과 직위. 원본 버그(종주 표시 미갱신, 재편 뒤 명부 밖 직위 유지, 환생 직후 고위직 배정)를 고친 규칙.
using System;
using System.Collections.Generic;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>종문 구성원·직위 규칙. 직위를 바꾸는 코드는 모두 여기를 거쳐 인원 집계가 어긋나지 않는다.</summary>
    public static class SectSystem
    {
        /// <summary>공헌을 더한다(산수는 무시). 종문 누적 공헌도 함께 오른다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <param name="amount">공헌.</param>
        public static void AddContribution(World world, Cultivator cultivator, double amount)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            Sect? sect = world.FindSect(cultivator.SectId);
            if (sect is null || amount <= 0)
            {
                return;
            }

            cultivator.Merit += amount;
            cultivator.ContributionPoints += amount;
            sect.TotalContribution += amount;
        }

        /// <summary>
        /// 종문에 들인다. 새 구성원은 외문제자 자리가 있으면 외문제자, 없으면 잡역제자로 시작한다.
        /// [원본 버그 수정] 빈 고위직(장로·종주)에 바로 앉지 않는다.
        /// </summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <param name="sect">종문.</param>
        public static void Join(World world, Cultivator cultivator, Sect sect)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            ArgumentNullException.ThrowIfNull(sect);
            Leave(world, cultivator);
            cultivator.SectId = sect.Id;
            SectRank rank = sect.RankCounts[(int)SectRank.Outer] < SectRules.Capacity(SectRank.Outer)
                ? SectRank.Outer
                : SectRank.None;
            SetRank(sect, cultivator, rank);
        }

        /// <summary>종문을 떠나게 한다(배신·사망·떠돌이 환생). 종주였다면 즉시 후계를 정한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        public static void Leave(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            Sect? sect = world.FindSect(cultivator.SectId);
            if (sect is null)
            {
                cultivator.SectId = 0;
                cultivator.Rank = SectRank.None;
                return;
            }

            bool wasLeader = sect.LeaderId == cultivator.Id;
            sect.RankCounts[(int)cultivator.Rank]--;
            cultivator.Rank = SectRank.None;
            cultivator.SectId = 0;
            cultivator.OnWarDuty = false;
            if (wasLeader)
            {
                sect.LeaderId = 0;
                Rerank(world, sect, announce: true);
            }
        }

        /// <summary>
        /// 직위를 다시 매긴다. 경지(단계 포함)와 현생 공헌으로 줄을 세워 위에서부터 정원만큼 채우고,
        /// 나머지는 모두 잡역제자로 둔다. [원본 버그 수정] 명부 밖 구성원의 옛 직위를 지우고, 종주 번호를 갱신한다.
        /// </summary>
        /// <param name="world">세계.</param>
        /// <param name="sect">종문.</param>
        /// <param name="announce">종주 교체를 연대기에 남길지.</param>
        public static void Rerank(World world, Sect sect, bool announce)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(sect);
            List<Cultivator> members = Members(world, sect.Id);
            members.Sort((left, right) =>
            {
                int byScore = RankScore(right).CompareTo(RankScore(left));
                return byScore != 0 ? byScore : left.Id.CompareTo(right.Id);
            });
            for (int index = 0; index < sect.RankCounts.Count; index++)
            {
                sect.RankCounts[index] = 0;
            }

            int previousLeader = sect.LeaderId;
            SectRank tier = SectRank.Leader;
            foreach (Cultivator member in members)
            {
                while (tier > SectRank.None && sect.RankCounts[(int)tier] >= SectRules.Capacity(tier))
                {
                    tier--;
                }

                member.Rank = tier;
                sect.RankCounts[(int)tier]++;
            }

            sect.LeaderId = members.Count > 0 ? members[0].Id : 0;
            if (sect.LeaderId != previousLeader && sect.LeaderId != 0)
            {
                AnnounceLeader(world, sect, members[0], announce);
            }
        }

        /// <summary>직위 순위 점수: 경지·단계가 가장 크게, 현생 공헌이 다음으로 작용한다.</summary>
        /// <param name="cultivator">수선자.</param>
        /// <returns>점수.</returns>
        public static double RankScore(Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(cultivator);
            return (Realms.Rank(cultivator.Realm, cultivator.Stage) * 1500.0) + cultivator.Merit;
        }

        /// <summary>종문 구성원 목록(자리 순서).</summary>
        /// <param name="world">세계.</param>
        /// <param name="sectId">종문 번호.</param>
        /// <returns>구성원.</returns>
        public static List<Cultivator> Members(World world, int sectId)
        {
            ArgumentNullException.ThrowIfNull(world);
            List<Cultivator> members = new();
            foreach (Cultivator cultivator in world.Cultivators)
            {
                if (cultivator.SectId == sectId)
                {
                    members.Add(cultivator);
                }
            }

            return members;
        }

        /// <summary>정기 처리: 녹봉·시설(100년), 입문(50년), 직위 재편(300년), 배신 검사(100년).</summary>
        /// <param name="world">세계.</param>
        public static void Update(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            if (world.War.AtWar)
            {
                SectEconomy.PayWarExpenses(world);
            }

            if (world.Year % Balance.RecruitmentInterval == 0)
            {
                Recruitment.Recruit(world);
            }

            if (world.Year % Balance.StipendInterval == 0)
            {
                SectEconomy.Settle(world);
                Recruitment.CheckDefections(world);
            }

            if (world.Year % Balance.RerankInterval == 0)
            {
                foreach (Sect sect in world.Sects)
                {
                    Rerank(world, sect, announce: true);
                }
            }
        }

        private static void SetRank(Sect sect, Cultivator cultivator, SectRank rank)
        {
            cultivator.Rank = rank;
            sect.RankCounts[(int)rank]++;
        }

        private static void AnnounceLeader(World world, Sect sect, Cultivator leader, bool announce)
        {
            sect.LeaderCount++;
            TitleSystem.Award(world, leader, TitleId.SectMaster);
            if (!announce)
            {
                return;
            }

            string text = "[" + sect.Name + "] " + leader.DisplayName + "(" + leader.RealmText + ")이(가) 제"
                + sect.LeaderCount
                + "대 종주에 올랐다.";
            Narrator.Log(world, LogKind.Sect, text, leader, notable: true);
            Narrator.Chronicle(world, ChronicleKind.Leader, text, leader);
        }
    }
}
