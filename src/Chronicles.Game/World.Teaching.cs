namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private string? RequestTeaching(Person pupil, long teacherId, string knowledge)
    {
        if (pupil.Action != ActionKind.Idle || !people.TryGetValue(teacherId, out var teacher) ||
            teacher.Id == pupil.Id || !pupil.KnownPeople.Contains(teacherId) || teacher.Status != LifeStatus.Alive ||
            teacher.Place != pupil.Place) return "전수 요청 조건을 확인하세요.";
        if (knowledge.Length == 0)
            knowledge = pupil.Realm == Realm.Mortal ? "entry" : Catalog.Paths.First(p => p.Realm == pupil.Realm).Id;
        if (knowledge != "entry" && !Catalog.Paths.Any(p => p.Id == knowledge) && !Catalog.Recipes.Any(r => r.Skill == knowledge))
            return "배울 수 없는 지식입니다.";
        if (teacher.LessonRequests.Count >= Law("ai.planBudget")) return "상대가 새 요청을 받을 수 없습니다.";
        if (teacher.LessonRequests.Any(r => r.Pupil == pupil.Id && r.Knowledge == knowledge && r.Expires > Month))
            return "이미 배움 요청의 응답을 기다리고 있습니다.";
        if (!teacher.LessonRequests.Any(r => r.Pupil == pupil.Id && r.Knowledge == knowledge))
            teacher.LessonRequests.Add(new(pupil.Id, knowledge, Month, Month + 12));
        pupil.Action = ActionKind.Wait; pupil.Due = Month + 1;
        Event("배움 요청", pupil.Id, teacher.Id, $"{pupil.Name} {teacher.Name}에게 배움 요청", "실제 대화·상대의 독립 판단 대기");
        return null;
    }
}
