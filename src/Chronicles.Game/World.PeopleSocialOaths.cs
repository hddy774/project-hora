using System.Globalization;
using System.Text.Json;
using Chronicles.Game.People;

namespace Chronicles.Game;

public sealed partial class GameWorld
{

    public PeopleOathView[] PeopleKnownOaths(long personId)
    {
        if (state.Controlled != 0 && state.Controlled != personId) return [];
        return state.PeopleDomain.Social.OathObservations.Where(o => (personId == 0 || o.Person == personId) && o.ReceivedMonth <= Month)
            .OrderBy(o => o.Person).ThenBy(o => o.Oath).Select(observation =>
            {
                var oath = state.PeopleDomain.Social.Oaths.Single(o => o.Id == observation.Oath);
                return new PeopleOathView(oath.Id, observation.Person, oath.First == observation.Person ? oath.Second : oath.First,
                    oath.Began, oath.Until, observation.Active && oath.Until > Month, oath.Terms, observation.ObservedMonth, observation.EvidenceId);
            }).ToArray();
    }

    private void ReconcilePeopleOaths()
    {
        foreach (var oath in state.PeopleDomain.Social.Oaths)
            if (!oath.Active || oath.Until <= Month || people[oath.First].Status != LifeStatus.Alive || people[oath.Second].Status != LifeStatus.Alive)
            {
                foreach (var person in new[] { people[oath.First], people[oath.Second] })
                {
                    var other = people[person.Id == oath.First ? oath.Second : oath.First];
                    var breach = person.Journal.LastOrDefault(e => e.Source == $"people:oath-break:{oath.Id}");
                    var known = oath.Until <= Month || breach is not null || other.Ended == Month && other.Place == person.Place;
                    ReleasePeopleTraits(person, "relation.oath", other.Id, $"oath:{oath.Id}:ended:{Month}", known);
                    if (known) RecordPeopleOathObservation(person, oath, false, breach is not null ? $"event:{breach.Id}" :
                        oath.Until <= Month ? $"known-oath-term:{oath.Id}:{oath.Until}" : $"observed-local-end:{oath.Id}:{Month}",
                        oath.Until <= Month ? oath.Until : Month);
                }
            }
        SetPeopleSocial(state.PeopleDomain.Social with { Oaths = state.PeopleDomain.Social.Oaths.Select(o => o.Active &&
            (o.Until <= Month || people[o.First].Status != LifeStatus.Alive || people[o.Second].Status != LifeStatus.Alive)
                ? o with { Active = false, Ended = Month, EndSource = "actual term or participant life ended" } : o).ToArray() });
    }

    private void RecordPeopleOathObservation(Person person, PeopleOath oath, bool active, string evidenceId, long observedMonth)
    {
        var observations = state.PeopleDomain.Social.OathObservations;
        if (observations.Any(o => o.Person == person.Id && o.Oath == oath.Id && o.Active == active)) return;
        SetPeopleSocial(state.PeopleDomain.Social with { OathObservations = observations.Where(o => o.Person != person.Id || o.Oath != oath.Id)
            .Append(new(person.Id, oath.Id, active, observedMonth, Month, evidenceId)).OrderBy(o => o.Person).ThenBy(o => o.Oath).ToArray() });
    }
}
