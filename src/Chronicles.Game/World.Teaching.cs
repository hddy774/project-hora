namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private string? RequestTeaching(Person pupil, long teacherId, string knowledge)
    {
        var command = new GameCommand($"ai-knowledge-request:{pupil.BodyId}:{Month}:{Revision}:{state.NextId}", pupil.Id,
            "requestTeaching", teacherId, Data: knowledge);
        return TryKnowledgeCommand(command, pupil, out var error) ? error : "지식 요청 명령을 확인하세요.";
    }
}
