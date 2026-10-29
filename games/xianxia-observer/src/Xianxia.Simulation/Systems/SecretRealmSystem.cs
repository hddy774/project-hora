// 비경: 드물게 열리는 보물 공간. 경지 범위가 맞는 수선자가 들어가 보물을 다투고, 닫힐 때 최대 수혜자를 기록한다.
using System;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Systems
{
    /// <summary>비경 규칙.</summary>
    public static class SecretRealmSystem
    {
        private const double OpenChancePerYear = 0.0015;
        private const int MaxEntrants = 40;

        /// <summary>새 비경을 열지 정하고, 열린 비경을 진행·종료한다.</summary>
        /// <param name="world">세계.</param>
        public static void Update(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            if (world.SecretRealms.Count < 2 && world.Random.Chance(OpenChancePerYear
                * EraSystem.SecretRealmFactor(world)))
            {
                Open(world, world.Random.Pick(RegionCatalog.EventRegionIds), summoned: false);
            }

            for (int index = world.SecretRealms.Count - 1; index >= 0; index--)
            {
                SecretRealmState realm = world.SecretRealms[index];
                if (world.Year >= realm.CloseYear || realm.Treasures <= 0)
                {
                    Close(world, realm);
                    world.SecretRealms.RemoveAt(index);
                }
                else
                {
                    Explore(world, realm);
                }
            }
        }

        /// <summary>비경을 연다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="regionId">입구 지역.</param>
        /// <param name="summoned">천기 개입으로 열었는가.</param>
        /// <returns>열린 비경.</returns>
        public static SecretRealmState Open(World world, int regionId, bool summoned)
        {
            ArgumentNullException.ThrowIfNull(world);
            int band = world.Random.Range(0, 5);
            SecretRealmState realm = new()
            {
                Name = world.Random.Pick(NameData.SecretRealmPrefixes) + " "
                    + world.Random.Pick(NameData.SecretRealmSuffixes),
                RegionId = regionId,
                OpenYear = world.Year,
                CloseYear = world.Year + world.Random.Range(30, 60),
                MinRealm = band,
                MaxRealm = Math.Min(Realms.Max, band + 2),
                Treasures = world.Random.Range(20, 60),
                Summoned = summoned,
            };
            world.SecretRealms.Add(realm);
            Region region = world.Regions[regionId];
            region.AuraModifier = 1.2;
            region.Status = "비경 개방: " + realm.Name;
            string text = "[" + region.Name + "] " + realm.Name + "이(가) 열렸다. (" + Realms.Name(realm.MinRealm) + "~"
                + Realms.Name(realm.MaxRealm) + " 입장)";
            Narrator.Log(world, LogKind.SecretRealm, text);
            if (realm.MaxRealm >= 5 || summoned)
            {
                Narrator.Chronicle(world, ChronicleKind.SecretRealm, text);
            }

            return realm;
        }

        /// <summary>지역에 이 수선자가 들어갈 수 있는 비경이 열려 있는가.</summary>
        /// <param name="world">세계.</param>
        /// <param name="regionId">지역.</param>
        /// <param name="cultivator">수선자.</param>
        /// <returns>들어갈 수 있으면 true.</returns>
        public static bool IsOpenFor(World world, int regionId, Cultivator cultivator)
        {
            ArgumentNullException.ThrowIfNull(world);
            ArgumentNullException.ThrowIfNull(cultivator);
            foreach (SecretRealmState realm in world.SecretRealms)
            {
                if (realm.RegionId == regionId && cultivator.Realm >= realm.MinRealm
                    && cultivator.Realm <= realm.MaxRealm)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Explore(World world, SecretRealmState realm)
        {
            Admit(world, realm);
            for (int index = 0; index < realm.Entrants.Count && realm.Treasures > 0; index++)
            {
                Cultivator? entrant = world.Find(realm.Entrants[index]);
                if (entrant is null || entrant.RegionId != realm.RegionId || entrant.RecoveryYears > 0)
                {
                    continue;
                }

                if (world.Random.Chance(0.15))
                {
                    realm.Treasures--;
                    realm.Gains[index] += Treasure(world, entrant);
                }
                else if (world.Random.Chance(0.05))
                {
                    Setbacks.Injure(world, entrant, world.Random.Range(0.1, 0.25), "비경의 금제");
                }
                else if (world.Random.Chance(0.1))
                {
                    Contest(world, realm, index, entrant);
                }
            }
        }

        private static void Admit(World world, SecretRealmState realm)
        {
            foreach (Cultivator resident in world.Regions[realm.RegionId].Residents)
            {
                if (realm.Entrants.Count >= MaxEntrants || realm.Entrants.Contains(resident.Id)
                    || resident.Realm < realm.MinRealm || resident.Realm > realm.MaxRealm)
                {
                    continue;
                }

                double eagerness = 0.3 * (0.5 + resident.Ambition) * (resident.Personality == Personality.Hermit
                    ? 0.3 : 1.0);
                if (world.Random.Chance(eagerness))
                {
                    realm.Entrants.Add(resident.Id);
                    realm.Gains.Add(0);
                    Narrator.Biography(world, resident, realm.Name + "에 들어갔다.");
                }
            }
        }

        /// <summary>기존 공법과 속성을 보존하면서 비경 보물을 지급한다.</summary>
        /// <param name="world">세계.</param>
        /// <param name="entrant">입장 인물.</param>
        /// <returns>기존 수확 점수.</returns>
        internal static int Treasure(World world, Cultivator entrant)
        {
            string gain;
            int value;
            switch (world.Random.NextInt(5))
            {
                case 0:
                    int previous = entrant.TechniqueGrade;
                    entrant.TechniqueGrade = Techniques.ImproveByReward(previous, 1, 18);
                    gain = (entrant.TechniqueGrade == previous ? "(이미 익힌) " : "")
                        + Techniques.Name(entrant.TechniqueGrade) + " 공법";
                    value = 3;
                    break;
                case 1:
                    entrant.ArtifactGrade = Math.Min(5, entrant.ArtifactGrade + 1);
                    gain = Labels.ArtifactGrade(entrant.ArtifactGrade) + " 법보";
                    value = 3;
                    break;
                case 2:
                    Growth.AddInsight(entrant, world.Random.Range(100, 300));
                    gain = "고대 석각의 깨달음";
                    value = 2;
                    break;
                case 3:
                    entrant.Qi += entrant.QiRequirement * 0.3;
                    gain = "영천의 영기";
                    value = 1;
                    break;
                default:
                    entrant.Pills[(int)PillKind.Breakthrough]++;
                    gain = "파경단";
                    value = 2;
                    break;
            }

            if (world.IsFollowed(entrant.Id) || (value >= 3 && world.LogRandom.Chance(0.3)))
            {
                Narrator.Log(world, LogKind.SecretRealm, entrant.Name + "이(가) 비경에서 " + gain + "을(를) 얻었다.", entrant,
                    notable: true);
            }

            return value;
        }

        private static void Contest(World world, SecretRealmState realm, int index, Cultivator entrant)
        {
            int rivalIndex = world.Random.NextInt(realm.Entrants.Count);
            Cultivator? rival = world.Find(realm.Entrants[rivalIndex]);
            if (rival is null || rival.Id == entrant.Id || rival.RegionId != realm.RegionId
                || (rival.SectId == entrant.SectId && !entrant.IsRogue))
            {
                return;
            }

            DuelResult result = CombatSystem.Fight(world, entrant, rival, BattleKind.SecretRealm);
            int winnerIndex = result.Winner == entrant ? index : rivalIndex;
            int loserIndex = result.Winner == entrant ? rivalIndex : index;
            int stolen = realm.Gains[loserIndex] / 2;
            realm.Gains[loserIndex] -= stolen;
            realm.Gains[winnerIndex] += stolen;
        }

        private static void Close(World world, SecretRealmState realm)
        {
            Region region = world.Regions[realm.RegionId];
            region.AuraModifier = 1.0;
            region.Status = "평온";
            int best = -1;
            for (int index = 0; index < realm.Gains.Count; index++)
            {
                if (best < 0 || realm.Gains[index] > realm.Gains[best])
                {
                    best = index;
                }
            }

            Cultivator? winner = best >= 0 && realm.Gains[best] >= 3 ? world.Find(realm.Entrants[best]) : null;
            string text = realm.Name + "이(가) 닫혔다. 들어간 수선자 " + realm.Entrants.Count + "명"
                + (winner is null ? "." : ", 최대 수혜자 " + winner.DisplayName + ".");
            Narrator.Log(world, LogKind.SecretRealm, text, winner);
            if (winner is null)
            {
                return;
            }

            TitleSystem.Award(world, winner, TitleId.RealmConqueror);
            Narrator.Chronicle(world, ChronicleKind.SecretRealm, text, winner);
        }
    }
}
