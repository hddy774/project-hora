using System.Text.Json;
using Chronicles.Game;

internal static partial class PerformanceScenarios
{
    private static long CollectedManagedBytes()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        return GC.GetTotalMemory(forceFullCollection: false);
    }

    private static object CognitionMeasurements(GameState state, out int serializedBytes)
    {
        var claims = state.PeopleDomain.Reputation.Claims.GroupBy(c => c.ObserverId).ToDictionary(g => g.Key, g => g.ToArray());
        var sources = state.Inventory.Observations.GroupBy(o => o.Observer).ToDictionary(g => g.Key, g => g.Count());
        var counts = state.People.Select(p => new
        {
            beliefs = p.Beliefs.Count, storedClaims = claims.GetValueOrDefault(p.Id)?.Length ?? 0,
            activeClaims = claims.GetValueOrDefault(p.Id)?.Count(c => c.ExpiresMonth is null || c.ExpiresMonth > state.Month) ?? 0,
            peopleObservations = p.ObservedPeople.Count, placeObservations = p.ObservedPlaces.Count,
            sourceObservations = sources.GetValueOrDefault(p.Id), knownPeople = p.KnownPeople.Count, knownPlaces = p.KnownPlaces.Count
        }).ToArray();
        var units = counts.Select(c => c.beliefs + c.activeClaims + c.peopleObservations + c.placeObservations + c.sourceObservations).ToArray();
        var beliefBudget = state.Laws["ai.beliefBudget"];
        var claimBudget = Math.Min(state.PeopleDomain.Social.Policy.ClaimBudget, Math.Max(1, beliefBudget / 4));
        serializedBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            people = state.People.Select(p => new { p.Id, p.Beliefs, p.KnownPeople, p.KnownPlaces, p.ObservedPeople,
                p.ObservedPlaces, p.Decisions, p.DecisionCandidates, p.Plan, p.LessonRequests, p.Habits, p.Journal, p.Skills, p.Understanding }),
            sourceObservations = state.Inventory.Observations, appraisals = state.Inventory.Appraisals,
            claims = state.PeopleDomain.Reputation, state.PeopleDomain.TraitObservations, state.PeopleDomain.TraceObservations,
            state.PeopleDomain.PotentialObservations, state.PeopleDomain.Social.OathObservations,
            knowledgeMemories = state.Knowledge.Memories, practicalKnowledge = state.Knowledge.Mastery
        }).Length;
        return new
        {
            people = counts.Length, living = state.People.Count(p => p.Status == LifeStatus.Alive), beliefBudgetPerPerson = beliefBudget,
            activeClaimBudgetPerPerson = claimBudget, maximumBudgetUnitsPerPerson = units.Max(),
            peopleExceedingBeliefBudget = units.Count(v => v > beliefBudget), peopleExceedingClaimBudget = counts.Count(c => c.activeClaims > claimBudget),
            totals = new { beliefs = counts.Sum(c => c.beliefs), storedClaims = counts.Sum(c => c.storedClaims), activeClaims = counts.Sum(c => c.activeClaims),
                peopleObservations = counts.Sum(c => c.peopleObservations), placeObservations = counts.Sum(c => c.placeObservations),
                sourceObservations = counts.Sum(c => c.sourceObservations), knownPeople = counts.Sum(c => c.knownPeople), knownPlaces = counts.Sum(c => c.knownPlaces) },
            maximumPerPerson = new { beliefs = counts.Max(c => c.beliefs), storedClaims = counts.Max(c => c.storedClaims), activeClaims = counts.Max(c => c.activeClaims),
                peopleObservations = counts.Max(c => c.peopleObservations), placeObservations = counts.Max(c => c.placeObservations),
                sourceObservations = counts.Max(c => c.sourceObservations), knownPeople = counts.Max(c => c.knownPeople), knownPlaces = counts.Max(c => c.knownPlaces) },
            serializedBytes,
            serializedScope = "uncompressed UTF-8 JSON projection of personal beliefs/maps/observations/decisions/plans/requests/habits/journals/skill mirrors, inventory observations/appraisals, reputation/trait/trace/potential/oath observations and knowledge memories/mastery; excludes body/genome, physical goods and world chronicles; includes projection field overhead, not attributed heap size"
        };
    }
}
