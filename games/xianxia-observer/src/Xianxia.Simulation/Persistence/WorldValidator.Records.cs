// 기록과 사건의 중첩 컬렉션을 검사한다. 정상 구 저장의 짧은 통계·분류 배열은 거부하지 않는다.
using System;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Persistence
{
    /// <summary>기록·활성 사건의 구조 검증.</summary>
    public static partial class WorldValidator
    {
        /// <summary>Normalize와 UI가 접근할 하위 목록을 먼저 확인한다.</summary>
        private static string? RecordsProblem(World world)
        {
            if (world.Log.Counts is null || world.Chronicle.Entries is null || world.Records.FirstReach is null
                || world.Records.YoungestReach is null || world.Statistics.Samples is null
                || world.Ledger.Created is null || world.Ledger.Destroyed is null || world.War.Highlights is null)
            {
                return "기록·통계·장부 목록이 없습니다.";
            }

            if (world.Statistics.Interval <= 0 || !Enum.IsDefined(world.Era.Kind)
                || Negative(world.Log.Counts) || Negative(world.Ledger.Created) || Negative(world.Ledger.Destroyed)
                || world.Log.Entries.Exists(static entry => entry is null)
                || world.Chronicle.Entries.Exists(static entry => entry is null)
                || world.Immortals.Exists(static entry => entry is null))
            {
                return "기록·통계·시대 상태가 유효하지 않습니다.";
            }

            foreach (StatSample? sample in world.Statistics.Samples)
            {
                if (sample is null || sample.RealmCounts is null || Negative(sample.RealmCounts)
                    || !Valid(sample.OrthodoxPower) || !Valid(sample.DemonicPower)
                    || !Valid(sample.OrthodoxAverageRealm) || !Valid(sample.DemonicAverageRealm))
                {
                    return "통계 표본이 유효하지 않습니다.";
                }
            }

            if (world.Wars.Exists(static war => war is null || war.Highlights is null)
                || world.Tournaments.Exists(static tournament => tournament is null || tournament.Matches is null)
                || world.Auctions.Exists(static auction => auction is null || auction.Lots is null))
            {
                return "역사 기록의 하위 목록이 없습니다.";
            }

            return EventsProblem(world);
        }

        /// <summary>진행 중 사건의 지역과 같은 인덱스를 공유하는 목록 길이를 확인한다.</summary>
        private static string? EventsProblem(World world)
        {
            foreach (SecretRealmState? secret in world.SecretRealms)
            {
                if (secret is null || secret.RegionId < 0 || secret.RegionId >= world.Regions.Count
                    || secret.Entrants is null || secret.Gains is null || secret.Entrants.Count != secret.Gains.Count
                    || secret.MinRealm is < 0 or > 8 || secret.MaxRealm < secret.MinRealm || secret.MaxRealm > 8)
                {
                    return "비경의 지역·경지·진입자 목록이 유효하지 않습니다.";
                }
            }

            foreach (BeastTideState? tide in world.BeastTides)
            {
                if (tide is null || tide.RegionId < 0 || tide.RegionId >= world.Regions.Count
                    || tide.DefenderIds is null || tide.DefenderMerit is null
                    || tide.DefenderIds.Count != tide.DefenderMerit.Count || !Valid(tide.Strength)
                    || !Valid(tide.InitialStrength) || tide.DefenderMerit.Exists(static value => !Valid(value)))
                {
                    return "수조의 지역·세력·방어자 목록이 유효하지 않습니다.";
                }
            }

            return null;
        }
    }
}
