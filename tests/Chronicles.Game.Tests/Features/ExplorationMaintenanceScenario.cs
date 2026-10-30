namespace Chronicles.Game.Tests;

public sealed class ExplorationMaintenanceSpaceScenario : IFeatureScenario
{
    public string Name => "비경 공개 명령: 내부 생활은 실제 휴대한 식량만 쓰고 바깥 바닥 재고나 보호자 식량을 관통하지 않는다";
    public void Run()
    {
        var floor = new ExplorationFixture(new() { ["ecology.foodUse"] = 1 }); floor.Survey(floor.First);
        var original = floor.World.Checkpoint().Items.Single(i => i.Owner == floor.First && i.Definition == "food");
        floor.Send("split", floor.First, original.Id, original.Quantity - 2);
        var ground = floor.World.Checkpoint().Items.Single(i => i.Owner == floor.First && i.Definition == "food" && i.Id != original.Id);
        floor.Send("store", floor.First, ground.Id, data: "ground"); floor.Send("enterSecret", floor.First, 8);
        var health = floor.World.Checkpoint().People.Single(p => p.Id == floor.First).Health;
        var floorHidden = floor.World.View(floor.First).Items.All(i => i.Id != ground.Id);
        floor.World.AdvanceTo(floor.World.Month + 1); var state = floor.World.Checkpoint();
        ExplorationFixture.Check(state.People.Single(p => p.Id == floor.First).Health == health - 2 &&
            state.Items.Single(i => i.Id == ground.Id).Quantity == ground.Quantity,
            "비경 내부에서 입구 바닥의 식량을 순간 소비했다.");

        var family = new ExplorationFixture(new() { ["birth.enabled"] = 1, ["birth.gestation"] = 1, ["ecology.foodUse"] = 1 });
        family.Idle(family.Second); family.Send("conceive", family.Second, family.First); family.Survey(family.Second);
        var child = family.World.Checkpoint().People.Single(p => p.ParentOne != 0);
        family.Send("enterSecret", family.Second, 8); var guardianFood = family.Food(family.Second); var childHealth = child.Health;
        family.World.AdvanceTo(family.World.Month + 1); state = family.World.Checkpoint();
        ExplorationFixture.Check(state.People.Single(p => p.Id == child.Id).Health == childHealth - 2 &&
            family.Food(family.Second) == guardianFood - 1,
            "바깥 아이가 내부 보호자의 실제 휴대 식량을 공간 너머로 소비했다.");
        ExplorationFixture.Check(floorHidden, "바깥 바닥 실물을 내부 휴대 재고로 표시했다.");
        Invariants.Validate(state);
    }
}
