// 호칭 수여 규칙. 업적이 생긴 그 순간에 조건을 확인한다(매 틱 전체 검사를 하지 않는다).
using System;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>호칭 규칙.</summary>
    public static class TitleSystem
    {
        /// <summary>호칭을 준다. 이미 있으면 아무 일도 하지 않는다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <param name="title">호칭.</param>
        /// <returns>새로 얻었으면 true.</returns>
        public static bool Award(World world, Cultivator cultivator, TitleId title)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            if (cultivator.HasTitle(title))
            {
                return false;
            }

            cultivator.Titles.Add(title);
            TitleDefinition definition = TitleCatalog.Get(title);
            Narrator.Log(world, LogKind.Title, cultivator.Name + "이(가) '" + definition.Name + "' 호칭을 얻었다. "
                + definition.Description + ".", cultivator, notable: true);
            Narrator.Biography(world, cultivator, "'" + definition.Name + "' 호칭을 얻었다.");
            return true;
        }

        /// <summary>전투 직후 연승·역천 호칭을 확인한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="winner">승자.</param>
        /// <param name="loser">패자.</param>
        /// <param name="upset">약자가 이겼는가.</param>
        public static void CheckCombat(World world, Cultivator winner, Cultivator loser, bool upset)
        {
            ArgumentNullException.ThrowIfNull(winner);
            ArgumentNullException.ThrowIfNull(loser);
            if (winner.WinStreak >= 10)
            {
                Award(world, winner, TitleId.Undefeated);
            }

            CheckVeteran(world, winner);
            if (!upset || loser.Realm <= winner.Realm)
            {
                return;
            }

            if (Award(world, winner, TitleId.Defier) && loser.Realm - winner.Realm >= 2)
            {
                Narrator.Chronicle(world, ChronicleKind.Record, winner.Name + "(" + winner.RealmText + ")이(가) 두 경지 위의 "
                    + loser.Name + "(" + loser.RealmText + ")을(를) 쓰러뜨렸다. 역천의 일격이었다.", winner);
            }
        }

        /// <summary>백전노장 호칭을 확인한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        public static void CheckVeteran(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(cultivator);
            if (cultivator.Battles >= 100)
            {
                Award(world, cultivator, TitleId.Veteran);
            }
        }

        /// <summary>대경지 돌파 직후 신동 호칭을 확인한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        public static void CheckBreakthrough(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(cultivator);
            if (cultivator.Realm >= 2 && cultivator.Age < 50)
            {
                Award(world, cultivator, TitleId.Prodigy);
            }
        }

        /// <summary>천겁 통과 직후 호칭을 확인한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        public static void CheckTribulation(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(cultivator);
            if (cultivator.TribulationsPassed >= 2)
            {
                Award(world, cultivator, TitleId.ThunderSurvivor);
            }
        }

        /// <summary>기연 직후 호칭을 확인한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        public static void CheckFortune(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(cultivator);
            if (cultivator.Fortunes >= 3)
            {
                Award(world, cultivator, TitleId.Fortunate);
            }
        }

        /// <summary>기예 숙련 직후 호칭을 확인한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        public static void CheckArt(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(cultivator);
            if (cultivator.ArtSkill >= 90)
            {
                Award(world, cultivator, TitleId.GrandMaster);
            }
        }

        /// <summary>호칭 점수 합.</summary>
        /// <param name="cultivator">수선자.</param>
        /// <returns>점수.</returns>
        public static int Score(Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(cultivator);
            int total = 0;
            foreach (TitleId title in cultivator.Titles)
            {
                total += TitleCatalog.Get(title).Score;
            }

            return total;
        }
    }
}
