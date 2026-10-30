using System.Text.Json;

namespace Chronicles.Game;

public sealed record SessionRequest(string Type = "view", GameCommand? Command = null,
    long Months = 0, long Detail = 0, ulong Seed = 42, int Slot = 0, bool UseCurrentLaws = false);
public sealed record SessionReply(GameView View, CommandResult? Result, string Message);

/// <summary>Serializes all host access. Paths and deserialization never cross the UI bridge.</summary>
public sealed class GameSession
{
    private readonly object gate = new();
    private readonly string saveDirectory;
    private GameWorld world;
    private long detail;
    private long hostGeneration;
    private string startupNotice = "";
    private static readonly JsonSerializerOptions JsonOptions = new()
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, MaxDepth = 32 };

    public GameSession(string saveDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveDirectory);
        this.saveDirectory = saveDirectory;
        var path = SlotPath(0);
        if (File.Exists(path) || File.Exists(path + ".pending") || File.Exists(path + ".backup"))
        {
            try
            {
                var recovered = GameCodec.LoadWithSource(path); world = recovered.World;
                startupNotice = RecoveryMessage(recovered.Source);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            { world = GameWorld.Create(); startupNotice = "이전 저장을 열 수 없어 새 세계를 열었습니다. 원본 파일은 별도로 보존합니다."; }
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
                case "new":
                    var rules = request.UseCurrentLaws ? world.CopyLawsForHost() : null;
                    world = GameWorld.Create(request.Seed, rules); detail = 0; hostGeneration++;
                    message = request.UseCurrentLaws ? "현재 법칙으로 새 세계를 만들었습니다." : "기본 법칙으로 새 세계를 만들었습니다.";
                    break;
                case "save":
                    try { GameCodec.SaveAtomic(world, SlotPath(request.Slot)); message = $"슬롯 {request.Slot + 1} 저장 완료"; }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { message = "저장 공간 또는 접근 권한을 확인하세요."; }
                    break;
                case "load":
                    try
                    {
                        var recovered = GameCodec.LoadWithSource(SlotPath(request.Slot)); world = recovered.World;
                        detail = 0; hostGeneration++; message = RecoveryMessage(recovered.Source);
                        if (message.Length == 0) message = "저장한 세계를 불러왔습니다.";
                    }
                    catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException) { message = "열 수 없는 저장입니다. 기존 파일은 보존됩니다."; }
                    break;
            }
            return Reply(result, message);
        }
    }

    private static string RecoveryMessage(string source) => source switch
    { "pending" => "중단된 저장에서 최근 진행을 복구했습니다.", "backup" => "백업에서 세계를 복구했습니다.", _ => "" };

    private SessionReply Reply(CommandResult? result, string message)
    {
        if (startupNotice.Length > 0) { message = message.Length == 0 ? startupNotice : message + " " + startupNotice; startupNotice = ""; }
        return new(world.View(detail) with { Generation = hostGeneration * 1000000 + world.ViewGeneration }, result, message);
    }

    public void Suspend()
    {
        lock (gate) GameCodec.SaveAtomic(world, SlotPath(0));
    }
}
