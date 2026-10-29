// 보관된 최근 사건과 현재 전투력에서 읽기 전용으로 관찰할 대상을 고른다.
using System.Collections.Generic;
using System.Linq;
using Xianxia.Simulation.Data;
using Xianxia.Simulation.Model;
using Xianxia.Simulation.Systems;

namespace Xianxia.App.Services
{
    /// <summary>관찰 대상과 추천 근거.</summary>
    /// <param name="Person">현재 살아 있는 대상.</param>
    /// <param name="Reason">보관 자료에서 찾은 이유.</param>
    internal sealed record Recommendation(Cultivator Person, string Reason);

    /// <summary>세계·난수를 바꾸거나 보관 밖 사건을 복원하지 않는다.</summary>
    internal static class RecommendationQuery
    {
        /// <summary>최근100년의 돌파/대회→전투력 순으로 최대3명을 고른다.</summary>
        /// <param name="world">읽기 잠금 안의 세계.</param>
        /// <returns>중복 없는 살아 있는 대상.</returns>
        internal static IReadOnlyList<Recommendation> Select(World world)
        {
            List<(int Id, int Year, string Reason)> candidates = new();
            foreach (LogEntry entry in world.Log.CopyNewestFirst(int.MaxValue, entry =>
                entry.Year >= world.Year - 100 && entry.Year <= world.Year
                && entry.Kind is LogKind.Breakthrough or LogKind.Tournament))
            {
                candidates.Add((entry.SubjectId, entry.Year,
                    Fmt.Years(entry.Year) + " · " + Labels.Of(entry.Kind)));
            }

            foreach (ChronicleEntry entry in world.Chronicle.Entries)
            {
                if (entry.Year >= world.Year - 100 && entry.Year <= world.Year
                    && entry.Kind is ChronicleKind.FirstRealm or ChronicleKind.Tournament)
                {
                    candidates.Add((entry.SubjectId, entry.Year, Fmt.Years(entry.Year) + " · 주요 사건"));
                }
            }

            foreach (TournamentRecord tournament in world.Tournaments)
            {
                if (tournament.Year >= world.Year - 100 && tournament.Year <= world.Year)
                {
                    candidates.Add((tournament.ChampionId, tournament.Year, "최근 대비무 우승"));
                }
            }

            Recommendation[] selected = candidates.Where(entry => world.Find(entry.Id) is not null)
                .OrderByDescending(entry => entry.Year).ThenBy(entry => entry.Id)
                .DistinctBy(entry => entry.Id).Take(3)
                .Select(entry => new Recommendation(world.Find(entry.Id)!, entry.Reason)).ToArray();
            return selected.Length > 0 ? selected : world.Cultivators
                .OrderByDescending(person => CombatSystem.Power(world, person)).ThenBy(person => person.Id).Take(3)
                .Select(person => new Recommendation(person, "최근 보관 후보 없음 · 현재 전투력 상위")).ToArray();
        }
    }
}
