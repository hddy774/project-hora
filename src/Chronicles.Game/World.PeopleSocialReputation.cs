using System.Globalization;
using System.Text.Json;
using Chronicles.Game.People;

namespace Chronicles.Game;

public sealed partial class GameWorld
{

    /// <summary>No actual other-person aptitude, title, body, wealth or world ranking is read here.</summary>
    internal SocialJudgment PeopleSocialJudgment(Person observer, long subjectId) =>
        Reputation.Judge(state.PeopleDomain.Reputation, new(observer.Id, subjectId, Month));

    public ReputationClaim[] PeopleReceivedClaims(long personId)
    {
        if (state.Controlled != 0 && state.Controlled != personId) return [];
        return state.PeopleDomain.Reputation.Claims.Where(c => (personId == 0 || c.ObserverId == personId) &&
            c.ReceivedMonth <= Month && c.ObservedMonth <= Month && (c.ExpiresMonth is null || c.ExpiresMonth > Month))
            .OrderBy(c => c.ObserverId).ThenBy(c => c.Id, StringComparer.Ordinal)
            .Select(c => c with { Provenance = c.Provenance.ToArray() }).ToArray();
    }

    private void RecordPeopleStandingClaims(GameEvent entry, Person[] witnesses)
    {
        foreach (var trait in state.PeopleDomain.Traits.Traits.Where(t => t.History.Any(h => h.Source.CauseId == $"event:{entry.Id}") &&
            TraitCatalog.Get(t.DefinitionId).Category == TraitCategory.Status))
            foreach (var witness in witnesses)
            {
                var active = Traits.IsActive(trait, Month); var name = TraitCatalog.Get(trait.DefinitionId).Name;
                ReceivePeopleEventClaim(entry, witness, trait.PersonId, ReputationTopic.SocialStanding, active,
                    $"직접 관측한 {name} {(active ? "성립" : "종료")}·기관 {trait.RelatedEntityId}", 9500, $":trait:{trait.Id}");
                state.PeopleDomain = state.PeopleDomain with { TraitObservations = state.PeopleDomain.TraitObservations.Where(o =>
                    o.ObserverId != witness.Id || o.SubjectId != trait.PersonId || o.DefinitionId != trait.DefinitionId)
                    .Append(new(witness.Id, trait.PersonId, trait.DefinitionId, trait.Status, Month, Month, trait.ExpiresMonth, $"event:{entry.Id}")).ToArray() };
            }
    }

    private void ReceivePeopleEventClaim(GameEvent entry, Person observer, long subject, ReputationTopic topic, bool affirmed, string statement, int confidence, string key = "") =>
        ReceivePeopleClaim(new($"event:{entry.Id}:{observer.Id}:{(int)topic}{key}", observer.Id, subject, topic, affirmed, statement, null, null,
            confidence, entry.Month, Month, Month + state.PeopleDomain.Social.Policy.ClaimLifetimeMonths, $"witnessed-event:{entry.Id}", null,
            [new($"event:{entry.Id}", ClaimOrigin.DirectObservation, entry.Actor > 0 ? entry.Actor : subject, observer.Id, entry.Month, Month)]));

    private void ReceivePeopleClaim(ReputationClaim claim)
    {
        var policy = state.PeopleDomain.ReputationPolicy with { MaximumClaimsPerObserver = Math.Min(state.PeopleDomain.ReputationPolicy.MaximumClaimsPerObserver,
            Math.Min(state.PeopleDomain.Social.Policy.ClaimBudget, Math.Max(1, IntLaw("ai.beliefBudget") / 4))) };
        var result = Reputation.Receive(state.PeopleDomain.Reputation, claim, policy);
        if (!result.Accepted) throw new InvalidDataException(result.Code);
        state.PeopleDomain = state.PeopleDomain with { Reputation = result.State };
        TrimCognition(people[claim.ObserverId]);
    }

    internal int PeopleCognitiveLoad(Person person) => state.PeopleDomain.Reputation.Claims.Count(c => c.ObserverId == person.Id &&
        (c.ExpiresMonth is null || c.ExpiresMonth > Month));

    internal void TrimPeopleCognition(Person person)
    {
        var maximum = Math.Min(state.PeopleDomain.ReputationPolicy.MaximumClaimsPerObserver,
            Math.Min(state.PeopleDomain.Social.Policy.ClaimBudget, Math.Max(1, IntLaw("ai.beliefBudget") / 4)));
        var claims = state.PeopleDomain.Reputation.Claims;
        var retained = claims.Where(c => c.ObserverId == person.Id && (c.ExpiresMonth is null || c.ExpiresMonth > Month))
            .OrderByDescending(c => c.Confidence).ThenByDescending(c => c.ReceivedMonth).ThenBy(c => c.Id, StringComparer.Ordinal).Take(maximum).ToArray();
        state.PeopleDomain = state.PeopleDomain with { Reputation = new(claims.Where(c => c.ObserverId != person.Id).Concat(retained)
            .OrderBy(c => c.ObserverId).ThenBy(c => c.Id, StringComparer.Ordinal).ToArray()) };
    }
}
