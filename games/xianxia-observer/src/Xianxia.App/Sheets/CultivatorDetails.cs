// 수선자 상세의 수치 표시. 전투력과 돌파 확률은 코어의 계산 결과를 표시하며 별도 규칙을 만들지 않는다.
using System;
using System.Linq;
using Xianxia.App.Controls;
using Xianxia.App.Services;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Sheets
{
    /// <summary>상세 하위 탭의 읽기 전용 수치 표시.</summary>
    internal static class CultivatorDetails
    {
        /// <summary>선택한 하위 탭의 수치를 채운다.</summary>
        /// <param name="facts">갱신 중인 목록.</param>
        /// <param name="world">세계.</param>
        /// <param name="person">현재 수선자.</param>
        /// <param name="tab">하위 탭 번호.</param>
        internal static void Fill(KeyValueList facts, World world, Cultivator person, int tab)
        {
            switch (tab)
            {
                case 0:
                    Overview(facts, person);
                    break;
                case 1:
                    Combat(facts, world, person);
                    break;
                case 2:
                    Practice(facts, world, person);
                    break;
                case 3:
                    Inventory(facts, person);
                    break;
                case 4:
                    facts.Add("도반과 함께한 기간", Fmt.Years(person.CompanionId == 0 ? 0
                        : Math.Max(0, world.Year - person.CompanionSinceYear)));
                    break;
                case 5:
                    facts.Add("현재 생", person.LifeNumber + "번째 · " + Fmt.Years(person.BornYear) + " 시작");
                    break;
                default:
                    facts.Add("현생 / 전생 기억", person.LifeNumber + "생 / "
                        + (person.MemoryAwakened ? "각성" : "미각성"));
                    break;
            }
        }

        private static void Overview(KeyValueList facts, Cultivator person)
        {
            facts.Add("경지", person.RealmText);
            facts.Add("나이 / 수명", Fmt.Age(person));
            facts.Add("재능", Labels.TalentGrade(person.TalentGrade) + " · " + person.TalentTotal);
            facts.Add("오성 / 체질 / 근골 / 영근", person.Comprehension + " / " + person.Constitution
                + " / " + person.Bone + " / " + person.Root);
            facts.Add("영근 / 성향", Labels.Of(person.Element) + " / " + Labels.Of(person.Personality));
            facts.Add("최근 행동", Labels.Of(person.LastAction));
            facts.Add("기예", Labels.Of(person.Art) + " · 숙련 " + Fmt.Decimal(person.ArtSkill));
            facts.Add("호칭", person.Titles.Count == 0 ? "없음"
                : string.Join(" · ", person.Titles.Select(title => TitleCatalog.Get(title).Name)));
        }

        private static void Combat(KeyValueList facts, World world, Cultivator person)
        {
            PowerBreakdown power = CombatSystem.Breakdown(world, person);
            facts.Add("총 전투력", Fmt.Number(power.Total));
            facts.Add("경지 기준", Fmt.Number(power.RealmBase));
            facts.Add("근골 / 공법 / 법보", Fmt.Factor(power.Bone) + " / " + Fmt.Factor(power.Technique)
                + " / " + Fmt.Factor(power.Artifact));
            facts.Add("내공 / 경험", Fmt.Factor(power.InnerPower) + " / " + Fmt.Factor(power.Experience));
            facts.Add("몸 상태 / 종문", Fmt.Factor(power.Condition) + " / " + Fmt.Factor(power.Sect));
            facts.Add("승 / 패 / 연승", person.Wins + " / " + person.Losses + " / " + person.WinStreak);
            facts.Add("누적 전쟁 공훈", Fmt.Number(person.LifetimeWarMerit));
        }

        private static void Practice(KeyValueList facts, World world, Cultivator person)
        {
            facts.Add("영기 / 필요 영기", Fmt.Number(person.Qi) + " / " + Fmt.Number(person.QiRequirement));
            facts.Add("깨달음 / 도심", Fmt.Number(person.Insight) + " / " + Fmt.Number(person.DaoHeart));
            facts.Add("공법", Techniques.Name(person.TechniqueGrade) + " · " + Labels.Of(person.TechniqueElement));
            facts.Add("공법이 감당하는 경지", Realms.Name(Techniques.RealmCap(person.TechniqueGrade)));
            facts.Add("소경지 돌파 확률", Fmt.Chance(BreakthroughSystem.MinorChance(person)));
            facts.Add("대경지 돌파 확률", person.Realm == Realms.Max ? "비승 단계"
                : Fmt.Chance(BreakthroughSystem.MajorChance(world, person)));
            facts.Add("건강 / 요양", Fmt.Percent(person.HealthRatio) + " / " + Fmt.Years(person.RecoveryYears));
            facts.Add("대경지 돌파 / 천겁 통과", person.MajorBreakthroughs + " / " + person.TribulationsPassed);
        }

        private static void Inventory(KeyValueList facts, Cultivator person)
        {
            facts.Add("영석", Fmt.Number(person.SpiritStones));
            facts.Add("영초 / 영철 / 내단", person.Herbs + " / " + person.Ore + " / " + person.BeastCores);
            facts.Add("법보", Labels.ArtifactGrade(person.ArtifactGrade));
            facts.Add("공헌 / 업적", Fmt.Number(person.ContributionPoints) + " / " + Fmt.Number(person.Merit));
            foreach (PillKind kind in Enum.GetValues<PillKind>())
            {
                facts.Add(Labels.Of(kind), person.Pills[(int)kind] + "개");
            }

            foreach (TalismanKind kind in Enum.GetValues<TalismanKind>())
            {
                facts.Add(Labels.Of(kind), person.Talismans[(int)kind] + "개");
            }
        }
    }
}
