using Chronicles.Game;

namespace Chronicles.Game.Tests;

public sealed class SocietyScenarios : IFeatureScenario
{
    public string Name => "INV-004/012/017 실제 양육 시간·식량·사회 관계·감정·판단 이력";

    public void Run()
    {
        var world = GameWorld.Create(42, new Dictionary<string, long>
        { ["birth.chance"] = 0, ["ecology.spoilage"] = 0, ["politics.sectLimit"] = 0 });
        var founders = world.Checkpoint().People;
        var mother = founders.Single(p => p.Gestates);
        var father = founders.Single(p => !p.Gestates);
        Require(world.Execute(new("control-mother", mother.Id, "possess")).Accepted, "양육자 조작");
        Require(world.Execute(new("conception", mother.Id, "conceive", father.Id)).Accepted, "실제 임신");
        world.AdvanceTo(9);
        var born = world.Checkpoint();
        var child = born.People.Single(p => p.ParentOne == mother.Id);
        var consumed = born.Materials["food"].Consumption;
        Require(world.Execute(new("care", mother.Id, "care", child.Id)).Accepted, "실제 돌봄 시작");
        Require(world.Checkpoint().Materials["food"].Consumption == consumed &&
            world.Checkpoint().People.Single(p => p.Id == child.Id).LastCare == 9, "시작 클릭으로 완료·소비하지 않음");
        var active = world.StateHash();
        Require(!world.Execute(new("busy", mother.Id, "cultivate")).Accepted && world.StateHash() == active,
            "돌봄과 수행의 시간 중복 거부");
        world = GameCodec.Decode(GameCodec.Encode(world));
        world.AdvanceTo(10);
        var finished = world.Checkpoint();
        Require(finished.People.Single(p => p.Id == child.Id).LastCare == 10, "복원 후 한 번 완료");
        Require(finished.Materials["food"].Consumption - consumed == 4, "세 명 생활 식량+돌봄 식량 실제 소비");
        var relation = finished.Relations.Single(r => r.Observer == child.Id && r.Other == mother.Id);
        Require(relation.Dependency == 3 && relation.Source.StartsWith("event:", StringComparison.Ordinal), "실제 돌봄 관계 출처");
        Require(finished.People.Single(p => p.Id == mother.Id).Mood > 0, "실제 사건의 감정 변화");
        Require(finished.People.Single(p => p.Id == father.Id).Decisions.Count > 0, "실제 AI 판단 기록");
        var hash = world.StateHash();
        _ = world.View(child.Id);
        Require(world.StateHash() == hash, "사회 상세 조회 무영향");
    }

    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
