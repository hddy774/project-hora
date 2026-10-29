// 인물의 범위·유한수·컬렉션·자기 참조를 검사한다. 사라진 관계 상대는 기존 정리 규칙에 맡긴다.
using System;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;

namespace Xianxia.Simulation.Persistence
{
    /// <summary>인물 상태 검증.</summary>
    public static partial class WorldValidator
    {
        /// <summary>색인 전에 참조·범위를 확인하고, 현생/전생 목록을 별도 검사한다.</summary>
        private static string? PersonProblem(Cultivator person, int regionCount)
        {
            if (person.Pills is null || person.Talismans is null || person.DiscipleIds is null
                || person.RivalIds is null || person.Titles is null || person.Biography is null
                || person.PastLives is null)
            {
                return "수선자의 필수 목록이 없습니다.";
            }

            if (string.IsNullOrWhiteSpace(person.Name) || person.RegionId < 0 || person.RegionId >= regionCount
                || person.SectId is < 0 or > 2
                || person.Realm is < 0 or > Realms.Max || person.Stage is < 0 or > Realms.PeakStage
                || person.HighestRealm is < 0 or > Realms.Max || person.HighestStage is < 0 or > Realms.PeakStage
                || person.TechniqueGrade is < 0 or > Techniques.MaxGrade || person.ArtifactGrade is < 0 or > 5
                || person.Age < 0 || person.Lifespan <= 0 || person.SpiritStones < 0 || person.Herbs < 0
                || person.Ore < 0 || person.BeastCores < 0 || Negative(person.Pills) || Negative(person.Talismans))
            {
                return "수선자의 지역·소속·경지·수명·소지품 범위가 유효하지 않습니다.";
            }

            if (!NumbersValid(person) || !Enum.IsDefined(person.Element) || !Enum.IsDefined(person.TechniqueElement)
                || !Enum.IsDefined(person.Personality) || !Enum.IsDefined(person.Art) || !Enum.IsDefined(person.Rank)
                || !Enum.IsDefined(person.LastAction) || person.Titles.Exists(static title => !Enum.IsDefined(title)))
            {
                return "수선자의 재능·수치·분류가 유효하지 않습니다.";
            }

            if (person.MasterId == person.Id || person.CompanionId == person.Id
                || person.DiscipleIds.Contains(person.Id) || person.RivalIds.Contains(person.Id))
            {
                return "수선자의 인연이 자기 자신을 가리킵니다.";
            }

            return HistoryProblem(person);
        }

        /// <summary>전생의 비어 있는 항목·호칭 목록을 거부한다.</summary>
        private static string? HistoryProblem(Cultivator person)
        {
            foreach (PastLife? life in person.PastLives)
            {
                if (life is null || life.Titles is null || life.Realm is < 0 or > Realms.Max
                    || life.Stage is < 0 or > Realms.PeakStage)
                {
                    return "전생 기록의 목록·경지가 유효하지 않습니다.";
                }
            }

            return person.Biography.Exists(static entry => entry is null) ? "생애 기록이 비어 있습니다." : null;
        }

        private static bool NumbersValid(Cultivator person)
        {
            return person.Comprehension is >= 10 and <= 500 && person.Constitution is >= 10 and <= 500
                && person.Bone is >= 10 and <= 500 && person.Root is >= 10 and <= 500
                && Valid(person.Qi) && Valid(person.Insight) && Valid(person.DaoHeart) && Valid(person.Injury)
                && Valid(person.InnerPower) && Valid(person.Merit) && Valid(person.ContributionPoints)
                && Valid(person.WarMerit) && Valid(person.LifetimeWarMerit) && Valid(person.ArtSkill)
                && Valid(person.Ambition) && Valid(person.Caution) && Valid(person.Charisma)
                && person.ArtSkill <= 100 && person.DaoHeart <= 100 && person.Insight <= 1000
                && person.Ambition <= 1 && person.Caution <= 1 && person.Charisma <= 1;
        }
    }
}
