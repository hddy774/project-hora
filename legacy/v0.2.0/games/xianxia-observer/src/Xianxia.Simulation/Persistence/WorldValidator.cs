// 신뢰하지 않는 저장을 색인 재구축 전에 검사한다. 정상 구 저장의 짧은 배열은 Normalize가 보완한다.
using System;
using System.Collections.Generic;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Persistence
{
    /// <summary>불러온 세계의 의미 검증. 난수와 원본 세계를 변경하지 않는다.</summary>
    public static partial class WorldValidator
    {
        /// <summary>첫 문제를 한국어로 반환한다. null이면 색인을 복원해도 된다.</summary>
        /// <param name="world">역직렬화한 세계.</param>
        /// <returns>문제 또는 null.</returns>
        public static string? FindProblem(World world)
        {
            ArgumentNullException.ThrowIfNull(world);
            string? problem = RootProblem(world);
            return problem ?? GeographyProblem(world) ?? PeopleProblem(world) ?? RecordsProblem(world);
        }

        /// <summary>후속 검사에서 접근할 필수 상태와 난수의 구조를 확인한다.</summary>
        private static string? RootProblem(World world)
        {
            if (world.Settings is null || world.Cultivators is null || world.Sects is null || world.Regions is null
                || world.Souls is null || world.Wars is null || world.Tournaments is null || world.Immortals is null
                || world.Followed is null || world.SecretRealms is null || world.BeastTides is null
                || world.Auctions is null || world.Era is null || world.War is null || world.Log is null
                || world.Chronicle is null || world.Records is null || world.Statistics is null
                || world.Ledger is null || world.Totals is null)
            {
                return "세계의 필수 목록 또는 상태가 없습니다.";
            }

            if (world.FormatVersion < 1 || world.FormatVersion > WorldSerializer.CurrentFormatVersion
                || world.Year < 0 || world.Cultivators.Count is < 1 or > 3000 || world.Sects.Count != 2
                || world.Regions.Count != RegionCatalog.All.Count)
            {
                return "저장 버전·연도·인구·종문·지역 수가 유효하지 않습니다.";
            }

            if ((world.RandomIncrement & 1) == 0
                || (world.LogRandomIncrement != 0 && (world.LogRandomIncrement & 1) == 0))
            {
                return "난수 증분은 홀수여야 합니다.";
            }

            return null;
        }

        /// <summary>지역·종문 인덱스와 저장된 주민 중복을 검사한다.</summary>
        private static string? GeographyProblem(World world)
        {
            for (int index = 0; index < world.Regions.Count; index++)
            {
                Region? region = world.Regions[index];
                if (region is null || region.Id != index || region.ControllerSectId is < 0 or > 2
                    || !Valid(region.AuraModifier) || region.MarketPills is null || region.MarketTalismans is null
                    || region.ControlPoints is < -10 or > 10 || Negative(region.MarketPills)
                    || Negative(region.MarketTalismans))
                {
                    return "지역 번호·보정·시장 재고가 유효하지 않습니다.";
                }

                if (region.SavedResidentIds is { } ids && new HashSet<int>(ids).Count != ids.Count)
                {
                    return "저장된 지역 주민 번호가 중복됐습니다.";
                }
            }

            for (int index = 0; index < world.Sects.Count; index++)
            {
                Sect? sect = world.Sects[index];
                if (sect is null || sect.Id != index + 1 || sect.Facilities is null || sect.RankCounts is null
                    || sect.PillStock is null || sect.Treasury < 0 || Negative(sect.Facilities)
                    || sect.Facilities.Exists(static level => level > 5) || Negative(sect.RankCounts)
                    || Negative(sect.PillStock))
                {
                    return "종문 번호·시설·재고가 유효하지 않습니다.";
                }
            }

            return null;
        }

        /// <summary>영혼 번호의 중복·범위와 다음 번호의 단조성을 확인한다.</summary>
        private static string? PeopleProblem(World world)
        {
            HashSet<int> ids = new();
            int maximum = 0;
            foreach (Cultivator? person in world.Cultivators)
            {
                if (person is null || person.Id <= 0 || !ids.Add(person.Id))
                {
                    return "수선자 번호가 없거나 중복됐습니다.";
                }

                maximum = Math.Max(maximum, person.Id);
                string? problem = PersonProblem(person, world.Regions.Count);
                if (problem is not null)
                {
                    return problem;
                }
            }

            HashSet<int> souls = new();
            foreach (SoulRecord? soul in world.Souls)
            {
                if (soul is null || soul.SoulId <= 0 || !souls.Add(soul.SoulId) || soul.TopScores is null
                    || soul.TitlesEver is null || soul.BestRealm is < 0 or > Realms.Max
                    || soul.BestStage is < 0 or > Realms.PeakStage
                    || soul.TitlesEver.Exists(static title => !Enum.IsDefined(title)))
                {
                    return "영혼 업적의 번호·목록·경지가 유효하지 않습니다.";
                }

                maximum = Math.Max(maximum, soul.SoulId);
            }

            return world.NextSoulId > maximum ? null : "다음 영혼 번호가 기존 번호보다 작거나 같습니다.";
        }

        private static bool Valid(double value) => double.IsFinite(value) && value >= 0;

        private static bool Negative(List<int> values) => values.Exists(static value => value < 0);

        private static bool Negative(List<long> values) => values.Exists(static value => value < 0);
    }
}
