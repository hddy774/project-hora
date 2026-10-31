// 인물 목록의 검색·소속 필터·8종 정렬. 화면 갱신에서만 실행하며 세계와 난수 상태를 바꾸지 않는다.
using System;
using System.Collections.Generic;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Services
{
    /// <summary>인물 목록의 정렬과 검색을 맡는다.</summary>
    internal static class PeopleQuery
    {
        /// <summary>필터 후 값 내림차순, 동점이면 영혼 번호 순으로 돌려준다.</summary>
        /// <param name="world">읽을 세계.</param>
        /// <param name="sort">경지·전투력·나이·재능·승리·영석·윤회·업적 순의 정렬 번호.</param>
        /// <param name="affiliation">전체 0, 청운종 1, 천마문 2, 산수 3, 관심 4.</param>
        /// <param name="search">이름 또는 도호 검색어.</param>
        /// <returns>정렬한 별도 목록.</returns>
        internal static List<Cultivator> Select(World world, int sort, int affiliation, string search)
        {
            List<Cultivator> people = new();
            foreach (Cultivator person in world.Cultivators)
            {
                bool matches = affiliation switch
                {
                    1 or 2 => person.SectId == affiliation,
                    3 => person.IsRogue,
                    4 => world.IsFollowed(person.Id),
                    _ => true,
                };
                if (matches && person.DisplayName.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    people.Add(person);
                }
            }

            people.Sort((left, right) =>
            {
                int comparison = Value(world, right, sort).CompareTo(Value(world, left, sort));
                return comparison == 0 ? left.Id.CompareTo(right.Id) : comparison;
            });
            return people;
        }

        /// <summary>선택한 정렬 기준의 실제 값.</summary>
        /// <param name="world">읽을 세계.</param>
        /// <param name="person">대상 인물.</param>
        /// <param name="sort">정렬 번호.</param>
        /// <returns>표시와 비교에 쓰는 값.</returns>
        internal static double Value(World world, Cultivator person, int sort)
        {
            return sort switch
            {
                0 => (person.Realm * 4) + person.Stage,
                1 => CombatSystem.Power(world, person),
                2 => person.Age,
                3 => person.TalentTotal,
                4 => person.Wins,
                5 => person.SpiritStones,
                6 => person.Reincarnations,
                _ => RecordSystem.LifeScore(person),
            };
        }
    }
}
