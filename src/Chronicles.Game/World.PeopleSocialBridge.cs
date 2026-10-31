using System.Globalization;
using System.Text.Json;
using Chronicles.Game.People;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private const string PeopleActionPrefix = "people:";
    private sealed record PeopleAction(long Session, long Body);
    private sealed record PeopleAssessmentNotice(long Session, long Subject, int Talent, int Minimum, int Maximum, int Confidence,
        ReputationTopic Opinion, string Statement);

    internal bool TryParsePeopleCreation(GameCommand command, out PeopleCreationProfile profile, out string? error) =>
        PeopleCreationParser.TryParse(command, state.Laws, out profile, out error);
    private void SetPeopleSocial(PeopleSocialState social) => state.PeopleDomain = state.PeopleDomain with { Social = social };

    private void EnsurePeopleSocialBaselines()
    {
        var social = state.PeopleDomain.Social;
        if (social.Groups.Length == 0) social = social with { Groups = state.Groups.Select(g => new PeopleGroupSnapshot(g.Id, g.Kind, g.Leader, g.Provinces.Count > 0, Month)).ToArray() };
        var known = social.Habits.Select(h => h.Person).ToHashSet();
        var habits = social.Habits.ToList();
        foreach (var person in state.People.Where(p => !known.Contains(p.Id)))
        {
            habits.Add(new(person.Id, person.BodyId, "baseline", 0, Month, Month));
            habits.AddRange(person.Habits.Select(h => new PeopleHabitSnapshot(person.Id, person.BodyId, h.Key, 0, Month, Month, h.Value)));
        }
        SetPeopleSocial(social with { Habits = habits.ToArray() });
    }

    internal void SettlePeopleSocial()
    {
        if (state.PeopleDomain.Social.SettledThrough >= Month) return;
        EnsurePeopleSocialBaselines();
        SetPeopleSocial(state.PeopleDomain.Social with { SettledThrough = Month });
        foreach (var session in state.PeopleDomain.Social.Sessions.Where(s => s.Status == PeopleSessionStatus.Proposed && s.Expires <= Month).ToArray())
            StorePeopleSession(session with { Status = PeopleSessionStatus.Expired, Ended = Month, CompletionSource = "actual request deadline" });
        foreach (var session in state.PeopleDomain.Social.Sessions.Where(s => s.Status == PeopleSessionStatus.Active).ToArray())
            if (!PeopleSessionParticipantsAvailable(session, people[session.Proposer], people[session.Recipient]))
                CancelPeopleSession(session.Id, "actual participant condition changed");
        foreach (var lesson in state.Knowledge.Activities.Where(a => a.Kind == Knowledge.KnowledgeActivityKind.Teaching &&
            a.Status == Knowledge.KnowledgeActivityStatus.Completed && a.EndedMonth == Month).OrderBy(a => a.Id))
            if (!state.Events.Any(e => e.Source == $"people:teaching:{lesson.Id}"))
                Event("전수", lesson.TeacherId, lesson.ActorId, "양측의 실제 시간을 쓴 수업을 완료했다.", $"people:teaching:{lesson.Id}");
        var snapshots = state.PeopleDomain.Social.Habits.ToList();
        foreach (var person in state.People.Where(p => p.Status == LifeStatus.Alive).OrderBy(p => p.Id))
        {
            foreach (var (habit, count) in person.Habits)
            {
                var index = snapshots.FindIndex(h => h.Person == person.Id && h.Habit == habit);
                if (index < 0) snapshots.Add(new(person.Id, person.BodyId, habit, count, Month, Month, count));
                else if (count > snapshots[index].LastObservedCounter) snapshots[index] = snapshots[index] with
                { Count = snapshots[index].Count + count - snapshots[index].LastObservedCounter, LastObservedCounter = count, LastCompleted = Month };
            }
        }
        SetPeopleSocial(state.PeopleDomain.Social with { Habits = snapshots.OrderBy(h => h.Person).ThenBy(h => h.Habit, StringComparer.Ordinal).ToArray() });
        foreach (var person in state.People.Where(p => p.Status == LifeStatus.Alive).OrderBy(p => p.Id))
        {
            FormPeopleExperiencedTraits(person);
            AwakenPeoplePrototypes(person);
            ReconcilePeopleMarker(person, "body.biased", person.Roots.Max() - person.Roots.Min() >= 3000, TraitFact.ElementBiased, $"body.{person.BodyId}.element-balance");
            ReconcilePeopleMarker(person, "scar.meridian", state.Cultivation.TryGetValue(person.BodyId, out var structure) && structure.RootDamage.Total > 0,
                TraitFact.MeridianDamage, $"body.{person.BodyId}.channel-damage");
            ReconcilePeopleMarker(person, "scar.core", person.Realm == Realm.GoldenCore && state.Cultivation.TryGetValue(person.BodyId, out structure) && structure.Damage > 2000,
                TraitFact.CoreDefect, $"body.{person.BodyId}.core-defect");
            ReconcilePeopleMarker(person, "soul.stable", souls[person.SoulId].Integrity == 100 &&
                state.PeopleDomain.Souls.Single(s => s.SoulId == person.SoulId).Foundation is { Value: >= 7000 } &&
                PeopleProfile(person).Learning.Any(l => l.TalentIndex == 10), TraitFact.SoulStable, $"soul.{person.SoulId}.measured-stability");
            SettlePeopleEcology(person);
        }
        ReconcilePeopleAuthority(); ReconcilePeopleOaths();
        state.PeopleDomain = state.PeopleDomain with { Reputation = new(state.PeopleDomain.Reputation.Claims
            .Where(c => c.ExpiresMonth is null || c.ExpiresMonth > Month).ToArray()) };
        foreach (var person in state.People) TrimCognition(person);
    }

    private void ValidatePeopleSocial()
    {
        foreach (var session in state.PeopleDomain.Social.Sessions)
        {
            if (people[session.Proposer].BodyId != session.ProposerBody || people[session.Recipient].BodyId != session.RecipientBody)
                throw new InvalidDataException("People session cannot reuse another body's identity.");
            if (session.Status == PeopleSessionStatus.Active)
                foreach (var participant in new[] { people[session.Proposer], people[session.Recipient] })
                    if (!TryPeopleAction(participant, out var action) || action.Session != session.Id || participant.Due != session.Due)
                        throw new InvalidDataException("An active personal session must retain both actual action reservations.");
        }
        foreach (var person in state.People)
            if (PeopleCognitiveLoad(person) > Math.Min(state.PeopleDomain.Social.Policy.ClaimBudget, Math.Max(1, IntLaw("ai.beliefBudget") / 4)))
                throw new InvalidDataException("Personal reputation exceeds its bounded observation budget.");
    }
}
