using System.Text.Json;

namespace Chronicles.Game;

public sealed record SessionRequest(string Type = "view", GameCommand? Command = null,
    long Months = 0, long Detail = 0, ulong Seed = 42, int Slot = 0);
public sealed record SessionReply(GameView View, CommandResult? Result, string Message);

/// <summary>Serializes all host access. Paths and deserialization never cross the UI bridge.</summary>
public sealed class GameSession
{
    private readonly object gate = new();
    private readonly string saveDirectory;
    private GameWorld world;
    private long detail;
    private long hostGeneration;
    private static readonly JsonSerializerOptions JsonOptions = new()
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, MaxDepth = 32 };

    public GameSession(string saveDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveDirectory);
        this.saveDirectory = saveDirectory;
        var path = SlotPath(0);
        if (File.Exists(path) || File.Exists(path + ".backup"))
        {
            try { world = GameCodec.Load(path); }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            { world = GameWorld.Create(); }
        }
        else world = GameWorld.Create();
    }

    private string SlotPath(int slot) => Path.Combine(saveDirectory, $"world-{slot}.seonro");
    public string HandleJson(string json)
    {
        try { return JsonSerializer.Serialize(Handle(JsonSerializer.Deserialize<SessionRequest>(json, JsonOptions) ?? new()), JsonOptions); }
        catch (JsonException) { return JsonSerializer.Serialize(Handle(new()), JsonOptions); }
    }
    public SessionReply Handle(SessionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (gate)
        {
            CommandResult? result = null;
            var message = "";
            if (request.Slot is < 0 or > 2) return Reply(null, "저장 슬롯은 1~3입니다.");
            switch (request.Type)
            {
                case "command":
                    if (request.Command is not null) { result = world.Execute(request.Command); message = result.Message; }
                    break;
                case "advance":
                    if (request.Months is > 0 and <= 1200) world.AdvanceTo(checked(world.Month + request.Months));
                    else message = "진행 기간은 1~1200개월입니다.";
                    break;
                case "detail": detail = request.Detail; break;
                case "new": world = GameWorld.Create(request.Seed); detail = 0; hostGeneration++; message = "새 세계를 만들었습니다."; break;
                case "save":
                    try { GameCodec.SaveAtomic(world, SlotPath(request.Slot)); message = $"슬롯 {request.Slot + 1} 저장 완료"; }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { message = "저장 공간 또는 접근 권한을 확인하세요."; }
                    break;
                case "load":
                    try { world = GameCodec.Load(SlotPath(request.Slot)); detail = 0; hostGeneration++; message = "저장한 세계를 불러왔습니다."; }
                    catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { message = "열 수 없는 저장입니다. 기존 파일은 보존됩니다."; }
                    break;
            }
            return Reply(result, message);
        }
    }

    private SessionReply Reply(CommandResult? result, string message) =>
        new(world.View(detail) with { Generation = hostGeneration * 1000000 + world.ViewGeneration }, result, message);

    public void Suspend()
    {
        lock (gate) GameCodec.SaveAtomic(world, SlotPath(0));
    }
}
