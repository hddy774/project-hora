using System.Text.Json;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private string? ImportLaws(GameCommand command)
    {
        if (command.Data.Length > 65536) return "법칙 파일이 너무 큽니다.";
        try
        {
            using var input = JsonDocument.Parse(command.Data);
            if (input.RootElement.ValueKind != JsonValueKind.Object) return "법칙은 항목과 정수 값으로 구성해야 합니다.";
            var changes = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var property in input.RootElement.EnumerateObject())
            {
                if (!property.Value.TryGetInt64(out var value) || !changes.TryAdd(property.Name, value))
                    return "법칙 값은 정수이며 항목은 중복할 수 없습니다.";
            }
            if (changes.Count == 0 || changes.Count > Laws.All.Count) return "가져올 법칙 항목을 확인하세요.";
            return ChangeLaws(changes, command.Id);
        }
        catch (JsonException) { return "법칙 JSON을 읽을 수 없습니다."; }
        catch (InvalidOperationException) { return "법칙 값은 정수여야 합니다."; }
    }

    private string? ChangeLaws(IReadOnlyDictionary<string, long> changes, string source)
    {
        if (state.Controlled != 0) return "관찰 관리에서만 세계 법칙을 바꿀 수 있습니다.";
        var candidate = new SortedDictionary<string, long>(state.Laws, StringComparer.Ordinal);
        foreach (var (key, value) in changes)
        {
            if (!Laws.ById.TryGetValue(key, out var definition) || value < definition.Minimum || value > definition.Maximum)
                return $"법칙 범위를 확인하세요: {key}";
            candidate[key] = value;
        }
        if (Laws.Validate(candidate) is { } error) return error;
        var changed = changes.Where(x => state.Laws[x.Key] != x.Value).OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
        if (changed.Length == 0) return null;
        var revision = state.LawRevision + 1;
        foreach (var (key, value) in changed)
            state.LawHistory.Add(new(Month, revision, key, state.Laws[key], value));
        state.Laws = candidate; state.LawRevision = revision;
        if (changes.ContainsKey("ai.beliefBudget")) foreach (var person in state.People) TrimCognition(person);
        Event("법칙", 0, 0, $"세계 법칙 {changed.Length}개 적용·r{revision}", source);
        return null;
    }
}
