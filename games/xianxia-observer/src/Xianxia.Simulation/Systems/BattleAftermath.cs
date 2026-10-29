// 대결 뒤 처리: 부상·경지 하락·전리품·공헌·숙적 형성·로그. 영석은 패자에게서 승자로 옮겨 갈 뿐 새로 생기지 않는다.
using System;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>대결 결과 적용.</summary>
    public static class BattleAftermath
    {
        /// <summary>비무·대비무·대회처럼 목숨을 걸지 않는 대결의 결과.</summary>
        /// <param name="world">세계.</param>
        /// <param name="result">대결 결과.</param>
        /// <param name="kind">대결 종류.</param>
        public static void ApplyFriendly(World world, DuelResult result, BattleKind kind)
        {
            ArgumentNullException.ThrowIfNull(world);
            Cultivator winner = result.Winner;
            Cultivator loser = result.Loser;
            double injury = kind switch
            {
                BattleKind.Tournament => world.Random.Range(0.05, 0.1),
                BattleKind.CrossSectSpar => world.Random.Range(0.04, 0.08),
                _ => world.Random.Range(0.03, 0.06),
            };
            Setbacks.Injure(world, loser, injury, "비무");
            if (kind != BattleKind.Tournament)
            {
                long wager = Math.Min(20, (long)(loser.SpiritStones * 0.03));
                Transfer(loser, winner, wager);
            }

            double merit = kind == BattleKind.CrossSectSpar ? 5 : 2;
            SectSystem.AddContribution(world, winner, merit);
            if (kind == BattleKind.CrossSectSpar)
            {
                world.Totals.CrossSectSpars++;
            }

            if (kind == BattleKind.Tournament)
            {
                return;
            }

            bool notable = winner.Realm >= 5 || world.IsFollowed(winner.Id) || world.IsFollowed(loser.Id);
            double sample = kind == BattleKind.CrossSectSpar ? 0.1 : 0.015;
            if (notable || world.Random.Chance(sample))
            {
                string prefix = kind == BattleKind.CrossSectSpar ? "[대비무] " : "[비무] ";
                Narrator.Log(world, LogKind.Spar, prefix + winner.Name + "이(가) " + loser.Name + "에게 이겼다.",
                    winner, loser, notable);
            }
            else
            {
                world.Log.CountOnly(LogKind.Spar);
            }
        }

        /// <summary>결투·전쟁·비경 다툼의 결과. 부상이 크고 경지가 떨어질 수 있으며 전리품을 빼앗긴다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="result">대결 결과.</param>
        /// <param name="kind">대결 종류.</param>
        public static void ApplySerious(World world, DuelResult result, BattleKind kind)
        {
            ArgumentNullException.ThrowIfNull(world);
            Cultivator winner = result.Winner;
            Cultivator loser = result.Loser;
            string beforeRealm = loser.RealmText;
            bool dropped = Wound(world, loser, kind);
            long loot = Plunder(world, winner, loser, kind);
            Growth.AddDaoHeart(winner, 1);
            Growth.AddDaoHeart(loser, -3);
            double meritBase = kind == BattleKind.War ? 30 : 5;
            SectSystem.AddContribution(world, winner, meritBase * (loser.Realm + 1));
            if (kind == BattleKind.War)
            {
                winner.WarMerit += 10.0 * (loser.Realm + 1) * (result.Upset ? 2 : 1);
                winner.LifetimeWarMerit += 10.0 * (loser.Realm + 1);
                if (dropped)
                {
                    world.War.Casualties++;
                }
            }

            if (kind is BattleKind.Grudge or BattleKind.War && world.Random.Chance(0.1))
            {
                RelationshipSystem.TryFormRivalry(world, winner, loser);
            }

            LogSerious(world, result, kind, dropped, beforeRealm, loot);
        }

        private static bool Wound(World world, Cultivator loser, BattleKind kind)
        {
            (double low, double high, double stageDrop, double realmDrop) = kind switch
            {
                BattleKind.War => (0.25, 0.5, 0.12, 0.03),
                BattleKind.SecretRealm => (0.15, 0.3, 0.05, 0.0),
                _ => (0.2, 0.4, 0.08, 0.0),
            };
            int rankBefore = Realms.Rank(loser.Realm, loser.Stage);
            Setbacks.Injure(world, loser, world.Random.Range(low, high), kind == BattleKind.War ? "전투 패배" : "결투 패배");
            if (Realms.Rank(loser.Realm, loser.Stage) < rankBefore)
            {
                return true;
            }

            if (world.Random.Chance(realmDrop))
            {
                return Setbacks.DropRealm(world, loser);
            }

            if (world.Random.Chance(stageDrop))
            {
                Setbacks.DropStage(world, loser);
                return Realms.Rank(loser.Realm, loser.Stage) < rankBefore;
            }

            return false;
        }

        private static long Plunder(World world, Cultivator winner, Cultivator loser, BattleKind kind)
        {
            (double low, double high) = kind switch
            {
                BattleKind.War => (0.2, 0.4),
                BattleKind.SecretRealm => (0.1, 0.25),
                _ => (0.15, 0.35),
            };
            Sect? sect = world.FindSect(winner.SectId);
            double ideology = sect is null ? 1.0 : SectRules.IdeologyLoot(sect.Orthodox);
            long amount = (long)(loser.SpiritStones * Math.Min(0.6, world.Random.Range(low, high) * ideology));
            Transfer(loser, winner, amount);
            if (world.Random.Chance(0.2) && loser.Herbs > 0)
            {
                int herbs = Math.Max(1, loser.Herbs / 3);
                loser.Herbs -= herbs;
                winner.Herbs += herbs;
            }

            return amount;
        }

        private static void LogSerious(World world, DuelResult result, BattleKind kind, bool dropped,
            string before, long loot)
        {
            Cultivator winner = result.Winner;
            Cultivator loser = result.Loser;
            bool notable = dropped || result.Upset || winner.Realm >= 5
                || world.IsFollowed(winner.Id) || world.IsFollowed(loser.Id);
            double sample = kind == BattleKind.War ? 0.08 : 0.3;
            if (!notable && !world.Random.Chance(sample))
            {
                world.Log.CountOnly(LogKind.Battle);
                return;
            }

            string tag = kind == BattleKind.War ? "[전투] " : kind == BattleKind.SecretRealm ? "[비경] " : "[결투] ";
            string text = tag + winner.Name + "이(가) " + loser.Name + "을(를) 꺾었다";
            text += loot > 0 ? " (영석 " + KoreanNumber.Compact(loot) + " 탈취)" : string.Empty;
            text += dropped ? ". " + loser.Name + " " + before + " → " + loser.RealmText : ".";
            Narrator.Log(world, kind == BattleKind.War ? LogKind.War : LogKind.Battle, text, winner, loser, notable);
        }

        /// <summary>영석을 옮긴다(음수·초과 방지).</summary>
        /// <param name="from">주는 쪽.</param>
        /// <param name="to">받는 쪽.</param>
        /// <param name="amount">양.</param>
        public static void Transfer(Cultivator from, Cultivator to, long amount)
        {
            ArgumentNullException.ThrowIfNull(from);
            ArgumentNullException.ThrowIfNull(to);
            long moved = Math.Clamp(amount, 0, from.SpiritStones);
            from.SpiritStones -= moved;
            to.SpiritStones += moved;
        }
    }
}
