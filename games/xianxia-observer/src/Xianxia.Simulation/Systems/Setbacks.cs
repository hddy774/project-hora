// 부상·경지 하락 처리. 전투 패배·돌파 실패·천겁 실패가 같은 규칙을 쓴다.
using System;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>좌절(부상·경지 하락) 규칙.</summary>
    public static class Setbacks
    {
        /// <summary>
        /// 부상을 입힌다. 최대 체력을 넘으면 중상: 단계가 하나 떨어지고 10~30년 요양한다(원본 "체력 고갈" 규칙을 실제로 작동하게 함).
        /// </summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <param name="ratio">최대 체력 대비 부상 비율.</param>
        /// <param name="cause">원인(로그용).</param>
        /// <returns>중상이면 true.</returns>
        public static bool Injure(World world, Cultivator cultivator, double ratio, string cause)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            cultivator.Injury += cultivator.MaxHealth * Math.Max(0, ratio);
            if (cultivator.Injury < cultivator.MaxHealth)
            {
                return false;
            }

            cultivator.Injury = cultivator.MaxHealth * 0.5;
            cultivator.RecoveryYears = world.Random.Range(10, 30);
            string before = cultivator.RealmText;
            DropStage(world, cultivator);
            string text = cultivator.Name + "이(가) " + cause + "(으)로 중상을 입어 " + before + " → " + cultivator.RealmText;
            Narrator.Log(world, LogKind.Injury, text, cultivator, notable: true);
            Narrator.Biography(world, cultivator, cause + "(으)로 중상, " + before + "에서 " + cultivator.RealmText
                + "(으)로 떨어졌다.");
            return true;
        }

        /// <summary>단계를 하나 내린다. 초기 단계면 아래 경지의 대원만으로 떨어진다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        public static void DropStage(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            cultivator.Qi *= 0.5;
            if (cultivator.Stage > 0)
            {
                cultivator.Stage--;
                return;
            }

            if (cultivator.Realm > 0)
            {
                cultivator.Realm--;
                world.Diagnostics?.RealmChanged(world.Year, cultivator);
                cultivator.Stage = Realms.PeakStage;
                cultivator.Lifespan = Lifespans.For(cultivator);
            }
        }

        /// <summary>대경지를 하나 내린다(초기 단계로). 영기와 내공을 크게 잃는다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">수선자.</param>
        /// <returns>실제로 떨어졌으면 true.</returns>
        public static bool DropRealm(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            if (cultivator.Realm == 0)
            {
                cultivator.Stage = 0;
                cultivator.Qi = 0;
                return false;
            }

            cultivator.Realm--;
            world.Diagnostics?.RealmChanged(world.Year, cultivator);
            cultivator.Stage = 0;
            cultivator.Qi = 0;
            cultivator.InnerPower = Math.Max(1, cultivator.InnerPower * 0.7);
            cultivator.Lifespan = Lifespans.For(cultivator);
            Growth.AddDaoHeart(cultivator, -15);
            return true;
        }
    }
}
