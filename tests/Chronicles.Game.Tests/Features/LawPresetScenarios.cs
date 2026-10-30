using Chronicles.Game;

namespace Chronicles.Game.Tests;

public sealed class LawPresetScenarios : IFeatureScenario
{
    public string Name => "INV-003/005 법칙 일괄 적용·검증 실패 원자성·중복 재전송·플레이 권한";

    public void Run()
    {
        var world = GameWorld.Create();
        var original = world.StateHash();
        var invalid = world.Execute(new("bad-preset", 0, "lawBatch", Data: "{\"birth.interval\":42,\"missing\":1}"));
        Require(!invalid.Accepted && world.StateHash() == original, "부분 적용 없이 알 수 없는 법칙 거부");
        Require(!world.Execute(new("duplicate", 0, "lawBatch", Data: "{\"birth.interval\":42,\"birth.interval\":43}")).Accepted,
            "중복 키 거부");
        Require(!world.Execute(new("fractional", 0, "lawBatch", Data: "{\"birth.interval\":1.5}")).Accepted,
            "단위가 다른 소수 값 거부");
        var command = new GameCommand("good-preset", 0, "lawBatch", Data: "{\"birth.interval\":42,\"ecology.spoilage\":7}");
        Require(world.Execute(command).Accepted, "유효 프리셋 적용");
        var applied = world.Checkpoint();
        Require(applied.Laws["birth.interval"] == 42 && applied.Laws["ecology.spoilage"] == 7 &&
            applied.LawHistory.Count == 2 && applied.LawHistory.Select(x => x.Revision).Distinct().Count() == 1,
            "일괄 변경의 단일 revision");
        var after = world.StateHash();
        Require(world.Execute(command).Accepted && world.StateHash() == after, "중복 프리셋 멱등");
        world = GameCodec.Decode(GameCodec.Encode(world));
        Require(world.StateHash() == after, "변경 이력 저장 왕복");
        Require(world.Execute(new("possess", applied.People[0].Id, "possess")).Accepted, "빙의");
        var playing = world.StateHash();
        Require(!world.Execute(new("play-preset", applied.People[0].Id, "lawBatch", Data: "{\"birth.interval\":44}")).Accepted &&
            world.StateHash() == playing, "플레이 세계 법칙 권한 우회 거부");
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
