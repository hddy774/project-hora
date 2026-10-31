using System.Security.Cryptography;
using System.Text.Json;
using Chronicles.Game;

var output = args.Single();
Directory.CreateDirectory(output);
var rows = new List<object>();
foreach (var seed in new ulong[] { 42, 73 }) RunTransitions(seed);
RunPhysicalFailure();
File.WriteAllText(Path.Combine(output, "boundaries.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Completed {rows.Count} deterministic boundaries.");

static string Digest(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
static void Stock(GameState state, long owner, string definition, int count)
{
    state.Items.Add(new Item { Id = state.NextId++, Owner = owner, Definition = definition,
        Quantity = count, Quality = 5000, Place = -1, Source = "동일 입력 비교의 명시 유한 재료" });
    if (!state.Materials.TryGetValue(definition, out var flow)) state.Materials.Add(definition, flow = new());
    flow.Opening += count;
}
static GameWorld Fresh(ulong seed)
{
    var world = GameWorld.Create(seed, new Dictionary<string, long>(StringComparer.Ordinal)
    {
        ["birth.enabled"] = 0, ["ecology.foodUse"] = 0, ["ecology.disasterChance"] = 0,
        ["knowledge.misreadChance"] = 0
    });
    var result = world.Execute(new("declared-pupil", 0, "create", Data: "유한 비교의 학습자"));
    Require(result.Accepted, result.Message);
    var state = world.Checkpoint();
    foreach (var person in state.People) { person.Action = ActionKind.Guard; person.Due = 1000; }
    var teacher = state.People[0];
    var pupil = state.People[^1];
    teacher.Action = pupil.Action = ActionKind.Idle;
    teacher.Due = pupil.Due = 0;
    pupil.Place = teacher.Place;
    if (!teacher.KnownPeople.Contains(pupil.Id)) teacher.KnownPeople.Add(pupil.Id);
    if (!pupil.KnownPeople.Contains(teacher.Id)) pupil.KnownPeople.Add(teacher.Id);
    Stock(state, teacher.Id, "paper", 5);
    Stock(state, teacher.Id, "ink", 5);
    return GameWorld.Restore(state);
}
void Capture(string scenario, string boundary, GameWorld world, ref GameWorld resumed)
{
    Require(world.StateHash() == resumed.StateHash(), "저장재개 월별 상태 차이: " + boundary);
    var state = world.Checkpoint();
    var bytes = GameCodec.Encode(world);
    Require(bytes.SequenceEqual(GameCodec.Encode(resumed)), "실제 저장 bytes 차이: " + boundary);
    var path = Path.Combine(output, scenario + "-" + boundary + ".save");
    File.WriteAllBytes(path, bytes);
    resumed = GameCodec.Decode(File.ReadAllBytes(path));
    Require(world.StateHash() == resumed.StateHash(), "실제 저장 bytes 재개 차이: " + boundary);
    rows.Add(new { scenario, boundary, state.Month, hash = world.StateHash(), save = Digest(bytes),
        knowledge = Digest(JsonSerializer.SerializeToUtf8Bytes(state.Knowledge)), state.RandomState, state.NextId,
        activities = state.Knowledge.Activities.Count, evidence = state.Knowledge.Evidence.Count,
        memories = state.Knowledge.Memories.Count, mastery = state.Knowledge.Mastery.Count });
}
void Command(string scenario, GameWorld world, GameWorld resumed, GameCommand command, bool expected = true)
{
    var before = world.StateHash();
    var result = world.Execute(command);
    var reference = resumed.Execute(command);
    Require(result.Accepted == expected && result == reference, command.Id + ": " + result.Message);
    if (!expected) Require(world.StateHash() == before, "거부 상태 변경: " + command.Id);
    Require(world.StateHash() == resumed.StateHash(), "명령 뒤 저장재개 차이: " + command.Id);
    rows.Add(new { scenario, command, result, hash = world.StateHash() });
}
void RunTransitions(ulong seed)
{
    var scenario = "transitions-" + seed;
    var world = Fresh(seed);
    var resumed = GameCodec.Decode(GameCodec.Encode(world));
    var state = world.Checkpoint();
    var teacher = state.People[0].Id;
    var pupil = state.People[^1].Id;
    var book = state.Items.First(i => i.Owner == teacher && i.Definition == "path.1.0").Id;
    Capture(scenario, "opening", world, ref resumed);
    Command(scenario, world, resumed, new("request", pupil, "requestTeaching", teacher, Data: "entry"));
    var lesson = new GameCommand("lesson", teacher, "teach", pupil, 2, "entry");
    Command(scenario, world, resumed, lesson);
    Command(scenario, world, resumed, lesson);
    Command(scenario, world, resumed, new("busy-gather", teacher, "gather", Data: "food"), false);
    Command(scenario, world, resumed, new("control", teacher, "possess"));
    for (var month = 1; month <= 2; month++)
    {
        world.AdvanceTo(month); resumed.AdvanceTo(month);
        Capture(scenario, "month" + month, world, ref resumed);
    }
    Command(scenario, world, resumed, new("study", teacher, "study", book));
    world.AdvanceTo(3); resumed.AdvanceTo(3);
    Capture(scenario, "month3", world, ref resumed);
    var copy = new GameCommand("copy", teacher, "transcribe", book);
    Command(scenario, world, resumed, copy);
    Command(scenario, world, resumed, copy);
    world.AdvanceTo(4); resumed.AdvanceTo(4);
    Capture(scenario, "month4", world, ref resumed);
    Command(scenario, world, resumed, new("experiment", teacher, "experiment", book));
    world.AdvanceTo(5); resumed.AdvanceTo(5);
    Capture(scenario, "month5", world, ref resumed);
    Command(scenario, world, resumed, new("cultivate", teacher, "cultivate"));
    world.AdvanceTo(6); resumed.AdvanceTo(6);
    Capture(scenario, "month6", world, ref resumed);
    Command(scenario, world, resumed, new("cancelled-study", teacher, "study", book, 2));
    world.AdvanceTo(7); resumed.AdvanceTo(7);
    Capture(scenario, "month7", world, ref resumed);
    Command(scenario, world, resumed, new("cancel-study", teacher, "cancel"));
    Command(scenario, world, resumed, new("final-study", teacher, "study", book));
    world.AdvanceTo(8); resumed.AdvanceTo(8);
    Capture(scenario, "month8", world, ref resumed);
}
void RunPhysicalFailure()
{
    const string scenario = "physical-failure-42";
    var state = Fresh(42).Checkpoint();
    var actor = state.People[0].Id;
    var book = state.Items.First(i => i.Owner == actor && i.Definition == "path.1.0").Id;
    foreach (var paper in state.Items.Where(i => i.Owner == actor && i.Definition == "paper"))
        paper.BoundSoul = state.People[1].SoulId;
    var world = GameWorld.Restore(state);
    var resumed = GameCodec.Decode(GameCodec.Encode(world));
    Command(scenario, world, resumed, new("physical-control", actor, "possess"));
    world.AdvanceTo(1); resumed.AdvanceTo(1);
    Capture(scenario, "primed", world, ref resumed);
    Command(scenario, world, resumed, new("physical-failure", actor, "experiment", book), false);
    Command(scenario, world, resumed, new("post-failure-study", actor, "study", book));
    world.AdvanceTo(2); resumed.AdvanceTo(2);
    Capture(scenario, "month2", world, ref resumed);
}
