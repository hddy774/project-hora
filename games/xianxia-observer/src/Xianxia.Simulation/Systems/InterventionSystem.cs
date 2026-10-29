// 천기 개입: 관찰자가 천기(200년마다 1점, 최대 10점)를 써서 세계에 작은 변수를 던진다.
using System;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>천기 개입 종류.</summary>
    public enum InterventionKind
    {
        /// <summary>기연 하사(수선자 한 명).</summary>
        BestowFortune = 0,

        /// <summary>비경 개방(지역).</summary>
        OpenSecretRealm = 1,

        /// <summary>수조 소환(지역).</summary>
        SummonBeastTide = 2,
    }

    /// <summary>천기 개입 규칙.</summary>
    public static class InterventionSystem
    {
        /// <summary>개입 비용(천기).</summary>
        /// <param name="kind">종류.</param>
        /// <returns>비용.</returns>
        public static int Cost(InterventionKind kind)
        {
            return kind == InterventionKind.BestowFortune ? 2 : 3;
        }

        /// <summary>개입 이름.</summary>
        /// <param name="kind">종류.</param>
        /// <returns>이름.</returns>
        public static string Name(InterventionKind kind)
        {
            return kind switch
            {
                InterventionKind.BestowFortune => "기연 하사",
                InterventionKind.OpenSecretRealm => "비경 개방",
                _ => "수조 소환",
            };
        }

        /// <summary>천기를 회복한다(200년마다 1점).</summary>
        /// <param name="world">세계.</param>
        public static void Regenerate(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            if (world.Year % Balance.FavorRegenYears == 0 && world.HeavenlyFavor < Balance.MaxHeavenlyFavor)
            {
                world.HeavenlyFavor++;
            }
        }

        /// <summary>
        /// 기연을 내린다: 깨달음 +400, 현재 단계 영기 50%, 부상 완치, 도심 +20.
        /// </summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">대상.</param>
        /// <returns>천기가 충분해 실행했으면 true.</returns>
        public static bool BestowFortune(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            if (!CanBestowFortune(world, cultivator))
            {
                return false;
            }

            world.HeavenlyFavor -= Cost(InterventionKind.BestowFortune);
            Growth.AddInsight(cultivator, 400);
            cultivator.Qi += cultivator.QiRequirement * 0.5;
            cultivator.Injury = 0;
            cultivator.RecoveryYears = 0;
            Growth.AddDaoHeart(cultivator, 20);
            TitleSystem.Award(world, cultivator, TitleId.Chosen);
            string text = "하늘이 " + cultivator.DisplayName + "에게 기연을 내렸다.";
            Narrator.Log(world, LogKind.Intervention, text, cultivator, notable: true);
            Narrator.Chronicle(world, ChronicleKind.Intervention, text, cultivator);
            Narrator.Biography(world, cultivator, "하늘이 내린 기연을 받았다.");
            return true;
        }

        /// <summary>지역에 비경을 연다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="regionId">지역.</param>
        /// <returns>실행했으면 true.</returns>
        public static bool OpenSecretRealm(World world, int regionId)
        {
            ArgumentNullException.ThrowIfNull(world);
            if (!CanOpenSecretRealm(world, regionId))
            {
                return false;
            }

            world.HeavenlyFavor -= Cost(InterventionKind.OpenSecretRealm);
            SecretRealmState realm = SecretRealmSystem.Open(world, regionId, summoned: true);
            Narrator.Log(world, LogKind.Intervention, "천기가 움직여 " + realm.Name + "이(가) 열렸다.");
            return true;
        }

        /// <summary>지역에 수조를 부른다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="regionId">지역.</param>
        /// <returns>실행했으면 true.</returns>
        public static bool SummonBeastTide(World world, int regionId)
        {
            ArgumentNullException.ThrowIfNull(world);
            if (!CanSummonBeastTide(world, regionId))
            {
                return false;
            }

            world.HeavenlyFavor -= Cost(InterventionKind.SummonBeastTide);
            BeastTideState tide = BeastTideSystem.Start(world, regionId, summoned: true);
            Narrator.Log(world, LogKind.Intervention, "천기가 움직여 " + tide.BeastName + "이(가) 깨어났다.");
            return true;
        }

        /// <summary>본산·범위 밖·중복 비경·부족한 천기를 차감 전에 거부한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="regionId">대상 지역.</param>
        /// <returns>현재 실행 가능 여부.</returns>
        public static bool CanOpenSecretRealm(World world, int regionId)
        {
            ArgumentNullException.ThrowIfNull(world);
            return IsEventRegion(regionId) && world.HeavenlyFavor >= Cost(InterventionKind.OpenSecretRealm)
                && !world.SecretRealms.Exists(entry => entry.RegionId == regionId);
        }

        /// <summary>본산·범위 밖·중복 수조·부족한 천기를 차감 전에 거부한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="regionId">대상 지역.</param>
        /// <returns>현재 실행 가능 여부.</returns>
        public static bool CanSummonBeastTide(World world, int regionId)
        {
            ArgumentNullException.ThrowIfNull(world);
            return IsEventRegion(regionId) && world.HeavenlyFavor >= Cost(InterventionKind.SummonBeastTide)
                && !world.BeastTides.Exists(entry => entry.RegionId == regionId);
        }

        /// <summary>세계에 현재 존재하는 동일 객체와 천기 잔액을 확인한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="cultivator">대상 인물.</param>
        /// <returns>현재 실행 가능 여부.</returns>
        public static bool CanBestowFortune(World world, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            return ReferenceEquals(world.Find(cultivator.Id), cultivator)
                && world.HeavenlyFavor >= Cost(InterventionKind.BestowFortune);
        }

        private static bool IsEventRegion(int regionId)
        {
            foreach (int allowed in RegionCatalog.EventRegionIds)
            {
                if (allowed == regionId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
