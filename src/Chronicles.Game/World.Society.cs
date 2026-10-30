namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private string? ValidateCare(Person guardian, long target)
    {
        if (!people.TryGetValue(target, out var child) || child.Status != LifeStatus.Alive ||
            child.Guardian != guardian.Id || child.Place != guardian.Place ||
            Month - child.Born >= Law("birth.maturity") || Inventory(guardian.Id, "food") < Law("care.food"))
            return "양육 책임·현재 위치·돌봄 식량을 확인하세요.";
        return null;
    }

    private void CompleteCare(Person guardian, long target)
    {
        if (ValidateCare(guardian, target) is not null) return;
        var child = people[target];
        Consume(guardian, "food", IntLaw("care.food"));
        child.LastCare = Month;
        child.Health = Math.Min(100, child.Health + IntLaw("cultivation.recovery") + 1);
        Event("돌봄", guardian.Id, child.Id, $"{guardian.Name} {child.Name} 돌봄 완료·식량과 한 달 노동 사용", "실제 보호자 작업");
    }

    private void RecordSocialEvent(GameEvent entry)
    {
        if (entry.TargetKind != "person" || entry.Actor == entry.Target ||
            !people.TryGetValue(entry.Actor, out var actor) || !people.TryGetValue(entry.Target, out var target)) return;
        var positive = entry.Kind switch { "도려" => 15, "거래" => 2, "돌봄" or "입양" => 8, "전수" => 5, "출생" => 10, _ => 0 };
        var grievance = entry.Kind is "전투" ? 15 : entry.Kind is "제안 거절" ? 1 : 0;
        if (positive == 0 && grievance == 0) return;
        void Add(Person observer, Person other)
        {
            var index = state.Relations.FindIndex(r => r.Observer == observer.Id && r.Other == other.Id);
            var before = index < 0 ? new SocialRelation(observer.Id, other.Id, 0, 0, 0, 0, 0, 0, Month, "") : state.Relations[index];
            var relation = before with { Trust = Math.Clamp(before.Trust + positive - grievance / 2, -100, 100),
                Affection = Math.Clamp(before.Affection + positive, -100, 100),
                Grievance = Math.Clamp(before.Grievance + grievance, 0, 100),
                Respect = Math.Clamp(before.Respect + (entry.Kind == "전수" ? 5 : 0), -100, 100),
                Dependency = Math.Clamp(before.Dependency + (entry.Kind == "돌봄" && observer.Id == target.Id ? 3 : 0), 0, 100),
                Obligation = Math.Clamp(before.Obligation + (entry.Kind == "돌봄" && observer.Id == target.Id ? 2 : 0), 0, 100),
                Month = Month, Source = $"event:{entry.Id}" };
            if (index < 0) state.Relations.Add(relation); else state.Relations[index] = relation;
            observer.Mood = Math.Clamp(observer.Mood + positive - grievance, -100, 100);
        }
        Add(actor, target); Add(target, actor);
    }

    private void RecordDecision(Person person)
    {
        person.Decisions.Add(new(Month, person.Goal, person.Reason, person.Action.ToString(), person.PlanExpansions,
            person.DecisionCandidates.ToArray()));
        if (person.Decisions.Count > 24) person.Decisions.RemoveAt(0);
    }

    private void RecordCompletedHabit(Person person, ActionKind kind)
    {
        if (kind == ActionKind.Idle) return;
        var key = kind.ToString();
        person.Habits[key] = Math.Min(100, person.Habits.GetValueOrDefault(key) + 1);
        RecordPeopleCompletedAction(person, kind);
    }
}
