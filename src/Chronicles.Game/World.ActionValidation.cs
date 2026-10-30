using System.Globalization;
using System.Text.Json;
using Chronicles.Game.Cultivation;
using Chronicles.Game.Knowledge;
using Chronicles.Game.People;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private static readonly string[] TypedActionPrefixes = ["knowledge:", "inventory:", "people:", "politics:", "combat:", "cultivation.", "exploration:"];
    private static bool IsTypedActionMarker(string data) => data is not null &&
        TypedActionPrefixes.Any(prefix => data.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static void RequireAction(bool valid, string detail)
    {
        if (!valid) throw new InvalidDataException("Action authority mismatch: " + detail);
    }

    // Ownership is checked independently from current physical prerequisites. A lost book or spent
    // input must resume into its actual interruption/cancellation, never be fabricated during restore.
    private void ValidateActionState()
    {
        foreach (var person in state.People) ValidateActionOwner(person);
        foreach (var job in state.Inventory.Jobs.Where(j => j.Status is "reserved" or "processing"))
            RequireAction(people.TryGetValue(job.Actor, out var actor) && actor.Status == LifeStatus.Alive &&
                actor.ActionTarget == job.Id && actor.ActionData == "inventory:" + job.Recipe,
                $"production {job.Id} has no current actor action");
        foreach (var cargo in state.Inventory.Contracts.Where(c => c.Kind == "transport" && c.Status == "open"))
            RequireAction(people.TryGetValue(cargo.Carrier, out var carrier) && carrier.Status == LifeStatus.Alive &&
                carrier.Action == ActionKind.Guard && carrier.ActionTarget == cargo.Id && carrier.ActionData == "inventory:transport",
                $"cargo {cargo.Id} has no current carrier action");
        foreach (var activity in state.Knowledge.Activities.Where(a => a.Status == KnowledgeActivityStatus.Active))
        {
            var actor = people[activity.ActorId];
            var owned = TryReadKnowledgeAction(actor, out var action) && action.Activity == activity.Id;
            if (activity.Kind == KnowledgeActivityKind.Practice)
                owned |= actor.Action == ActionKind.Cultivate && actor.ActionTarget == activity.Id ||
                    state.Inventory.Jobs.Any(j => j.Actor == actor.Id && j.KnowledgeActivity == activity.Id &&
                        j.Status is "reserved" or "processing" && actor.ActionTarget == j.Id);
            RequireAction(owned, $"knowledge activity {activity.Id} has no process action");
            if (activity.TeacherId > 0)
                RequireAction(TryReadKnowledgeAction(people[activity.TeacherId], out var teacher) && teacher.Activity == activity.Id,
                    $"knowledge activity {activity.Id} has no teacher action");
        }
        foreach (var person in state.People.Where(p => p.Status == LifeStatus.Alive))
        {
            var cultivation = state.Cultivation[person.BodyId];
            if (cultivation.PendingTrial is not null)
                RequireAction(person.Action == ActionKind.Trial, $"body {person.BodyId} pending trial has no action");
            else if (CultivationEngine.ActiveAttempt(cultivation))
                RequireAction(person.Action == ActionKind.Breakthrough, $"body {person.BodyId} active attempt has no action");
        }
        foreach (var exchange in state.Combat.Exchanges)
            foreach (var participant in exchange.Participants.Where(p => p.OccupiedUntil > Month))
                if (people[participant.Person].Status == LifeStatus.Alive)
                    RequireAction(people[participant.Person].ActionData == "combat:" + exchange.Id.ToString(CultureInfo.InvariantCulture),
                        $"combat {exchange.Id} paid occupancy lost its owner marker");
    }

    private void ValidateActionOwner(Person person)
    {
        var data = person.ActionData ?? throw new InvalidDataException($"Action authority mismatch: person {person.Id} has null data");
        if (person.Action == ActionKind.Idle)
        { RequireAction(!IsTypedActionMarker(data), $"idle person {person.Id} retains a module action"); return; }
        RequireAction(person.Status == LifeStatus.Alive && Enum.IsDefined(person.Action) && person.Due > Month,
            $"person {person.Id} action time/life");
        if (data.StartsWith("knowledge:", StringComparison.Ordinal)) { ValidateKnowledgeActionOwner(person); return; }
        if (data.StartsWith("inventory:", StringComparison.Ordinal)) { ValidateInventoryActionOwner(person); return; }
        if (data.StartsWith("people:", StringComparison.Ordinal)) { ValidatePeopleActionOwner(person); return; }
        if (data.StartsWith("exploration:", StringComparison.Ordinal))
        { RequireAction(ValidateExplorationAction(person) is null, $"person {person.Id} actual secret entry"); return; }
        if (data.StartsWith("politics:", StringComparison.Ordinal))
        {
            RequireAction(person.Action == ActionKind.Guard && state.Politics.Labor.Any(r => r.Person == person.Id &&
                r.Until == person.Due && r.Group == person.ActionTarget && data == $"politics:{r.Purpose}:{r.Reference}"),
                $"person {person.Id} political labor");
            return;
        }
        if (data.StartsWith("combat:", StringComparison.Ordinal))
        {
            RequireAction(long.TryParse(data.AsSpan("combat:".Length), NumberStyles.None, CultureInfo.InvariantCulture, out var receipt),
                $"person {person.Id} combat receipt marker");
            var exchange = state.Combat.Exchanges.SingleOrDefault(e => e.Id == receipt);
            var participant = exchange?.Participants.SingleOrDefault(p => p.Person == person.Id);
            RequireAction(participant is not null && participant.Body == person.BodyId && participant.Soul == person.SoulId &&
                participant.OccupiedUntil > 0 && person.Due >= participant.OccupiedUntil &&
                (person.Action == ActionKind.Guard && person.ActionTarget == receipt && participant.OccupiedUntil > Month ||
                 person.Action == ActionKind.Travel && exchange!.Reaction.Person == person.Id && exchange.Reaction.Choice == "retreat" &&
                 person.ActionTarget == exchange.Reaction.Destination), $"person {person.Id} real combat occupancy");
            return;
        }
        if (data is "cultivation.medical" or "cultivation.recovery")
        {
            RequireAction(person.Action == ActionKind.Heal && state.Cultivation.ContainsKey(person.BodyId), $"person {person.Id} recovery");
            return;
        }
        RequireAction(!IsTypedActionMarker(data), $"person {person.Id} malformed module marker");
        var metadata = state.Cultivation[person.BodyId];
        switch (person.Action)
        {
            case ActionKind.Cultivate:
                var path = CultivationProfiles.Find(data);
                RequireAction(path?.Definition.Realm == (person.Realm == Realm.Mortal ? Realm.Qi : person.Realm) &&
                    !CultivationEngine.ActiveAttempt(metadata) && metadata.PendingTrial is null,
                    $"person {person.Id} cultivation path/boundary");
                if (person.ActionTarget != 0)
                    RequireAction(state.Knowledge.Activities.Any(a => a.Id == person.ActionTarget && a.ActorId == person.Id &&
                        a.Status == KnowledgeActivityStatus.Active && a.Kind == KnowledgeActivityKind.Practice &&
                        a.DueMonth == person.Due && a.WorkId == (person.Realm == Realm.Mortal ? "entry" : data)),
                        $"person {person.Id} actual cultivation practice");
                break;
            case ActionKind.Breakthrough:
                RequireAction(CultivationEngine.ActiveAttempt(metadata) && metadata.PendingTrial is null &&
                    long.TryParse(metadata.Attempt!.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var attemptId) &&
                    attemptId > 0 && attemptId < state.NextId && person.ActionTarget == attemptId && data == metadata.Attempt.Path && person.ActionRevision == metadata.Attempt.LawRevision &&
                    person.Due >= metadata.Attempt.Due, $"person {person.Id} active formation");
                break;
            case ActionKind.Trial:
                RequireAction(metadata.PendingTrial is { } trial && person.Realm == Realm.Tribulation &&
                    person.PendingLayer == trial.TargetLayer && person.Due >= trial.Due && metadata.Structure is TribulationStructure trials &&
                    (trials.CompletedTrials & (1 << (trial.Completion ? 9 : trial.TargetLayer - 1))) == 0,
                    $"person {person.Id} actual pending trial");
                break;
            case ActionKind.Martial:
                RequireAction(metadata.PendingTrial is null && !CultivationEngine.ActiveAttempt(metadata) && person.Martial < 10 &&
                    PeopleActionError(person, "martial") is null,
                    $"person {person.Id} independent martial work");
                break;
            case ActionKind.Travel:
                RequireAction(person.ActionTarget is >= 0 and < 100, $"person {person.Id} travel destination");
                break;
            case ActionKind.Care:
                RequireAction(people.ContainsKey(person.ActionTarget), $"person {person.Id} actual care recipient");
                break;
            case ActionKind.Explore or ActionKind.Heal or ActionKind.Guard or ActionKind.Wait: break;
            default: throw new InvalidDataException($"Action authority mismatch: person {person.Id} typed work has no owning process");
        }
    }

    private void ValidateKnowledgeActionOwner(Person person)
    {
        RequireAction(TryReadKnowledgeAction(person, out var action), $"person {person.Id} knowledge marker");
        var activity = state.Knowledge.Activities.SingleOrDefault(a => a.Id == action.Activity);
        RequireAction(activity is not null && activity.Status == KnowledgeActivityStatus.Active && action.Body == person.BodyId &&
            action.Work == activity.WorkId && person.Due == activity.DueMonth &&
            person.ActionTarget == (action.Reservation > 0 ? action.Reservation : activity.Id), $"person {person.Id} knowledge identity/time");
        var partner = activity!.TeacherId == person.Id ? activity.ActorId : activity.TeacherId;
        RequireAction(partner == 0 ? action.PartnerBody == 0 : people.TryGetValue(partner, out var other) && action.PartnerBody == other.BodyId,
            $"person {person.Id} knowledge partner body");
        var valid = action.Mode switch
        {
            "study" or "correctKnowledge" => person.Action == ActionKind.Study && activity.Kind == KnowledgeActivityKind.Study &&
                activity.ActorId == person.Id && action.SourceItem == activity.MediumItemId,
            "teach" => person.Action == ActionKind.Teach && activity.Kind == KnowledgeActivityKind.Teaching && activity.TeacherId == person.Id,
            "learn" => person.Action == ActionKind.Wait && activity.Kind == KnowledgeActivityKind.Teaching && activity.ActorId == person.Id,
            "transcribe" => person.Action == ActionKind.Craft && activity.Kind == KnowledgeActivityKind.Transcription && activity.ActorId == person.Id &&
                action.TargetItem == activity.TargetItemId && action.Reservation > 0,
            "experiment" => person.Action == ActionKind.Craft && activity.Kind == KnowledgeActivityKind.Practice && activity.ActorId == person.Id,
            _ => false
        };
        RequireAction(valid, $"person {person.Id} knowledge process kind");
    }

    private void ValidateInventoryActionOwner(Person person)
    {
        if (person.ActionData == "inventory:transport")
        {
            RequireAction(person.Action == ActionKind.Guard && state.Inventory.Contracts.Any(c => c.Id == person.ActionTarget &&
                c.Kind == "transport" && c.Status == "open" && c.Carrier == person.Id && c.Due == person.Due),
                $"person {person.Id} actual transport");
            return;
        }
        var job = state.Inventory.Jobs.SingleOrDefault(j => j.Id == person.ActionTarget && j.Actor == person.Id);
        RequireAction(job is not null && job.Status is "reserved" or "processing" && person.Due == job.Due &&
            person.ActionData == "inventory:" + job.Recipe, $"person {person.Id} actual production");
        var valid = job!.Recipe switch
        {
            "extraction" => person.Action == ActionKind.Gather && job.Source != 0,
            "repair" or "medical-treatment" => person.Action == ActionKind.Heal,
            "appraisal" or "exploration" => person.Action == ActionKind.Explore,
            "farm.food" => person.Action is ActionKind.Farm or ActionKind.Craft,
            _ => person.Action == ActionKind.Craft
        };
        RequireAction(valid, $"person {person.Id} production action kind");
    }

    private void ValidatePeopleActionOwner(Person person)
    {
        try
        {
            RequireAction(TryPeopleAction(person, out var action), $"person {person.Id} people marker");
            var session = state.PeopleDomain.Social.Sessions.SingleOrDefault(s => s.Id == action.Session);
            RequireAction(session is not null && session.Status == PeopleSessionStatus.Active && action.Body == person.BodyId &&
                person.ActionTarget == session.Id && person.Due == session.Due &&
                (session.Proposer == person.Id && person.Action == ActionKind.Explore ||
                 session.Recipient == person.Id && person.Action == ActionKind.Wait), $"person {person.Id} actual personal session");
        }
        catch (JsonException exception) { throw new InvalidDataException("Action authority mismatch: invalid people marker", exception); }
    }
}
