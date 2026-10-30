using Chronicles.Game.Inventory;

namespace Chronicles.Game.Tests.Features;

public sealed class InventorySynchronizationScopeScenario : IFeatureScenario
{
    // Captured with the original scans at f0b8c9b, before adding call-local indexes.
    private const string Baseline = "a7035140b4f98dd6b78032c590f6fe2bcd216284138fcc88e7fd36d1b733c3d0";
    public string Name => "inventory sync: 실제 제작·감정·운송과 마지막 노동 행·사람 순서·반복 및 취소 후 새 범위 보존";
    public void Run()
    {
        var world = OwnedInventoryFixture.Create(); var crafter = world.People[0]; var carrier = world.People[1];
        Person AddPerson() { var p = new Person { Id = world.NextId++, SoulId = world.NextId++, Place = 0 }; world.People.Add(p); return p; }
        var appraiser = AddPerson(); var repeat = AddPerson(); var sameDue = AddPerson(); var otherTarget = AddPerson();
        var idle = AddPerson(); var dead = AddPerson(); dead.Status = LifeStatus.Dead;
        // Deliberately keep people in an order different from their IDs. New labor IDs follow this original order.
        (world.People[4], world.People[5]) = (world.People[5], world.People[4]);
        long Apply(InventoryCommand command)
        {
            var result = InventoryEngine.Apply(InventoryState.FromWorld(world, world.Inventory), command);
            InventoryFixture.Check(result.Accepted, result.Message); world.Inventory = result.State.ApplyTo(world); return result.Subject;
        }
        var ore = OwnedInventoryFixture.Item(world, "ore"); Apply(new GrantUse("sync-appraisal-access", crafter.Id, ore, appraiser.Id, true));
        var craft = Apply(new StartProduction("sync-real-production", crafter.Id, "pill", 5));
        var appraisal = Apply(new AppraisePhysical("sync-real-appraisal", appraiser.Id, ore, 5));
        Apply(new BeginProduction("sync-appraisal-process", appraiser.Id, appraisal));
        var transport = Apply(new BeginTransport("sync-real-transport", crafter.Id, OwnedInventoryFixture.Item(world, "tool"), carrier.Id, crafter.Id, 1, 5, 0, true, true));
        crafter.Action = ActionKind.Craft; crafter.ActionTarget = craft; carrier.Action = ActionKind.Guard; carrier.ActionTarget = transport;
        appraiser.Action = ActionKind.Explore; appraiser.ActionTarget = appraisal;
        repeat.Action = ActionKind.Explore; repeat.ActionTarget = appraisal; repeat.ActionData = "inventory:appraisal";
        sameDue.Action = ActionKind.Guard; sameDue.ActionTarget = transport; sameDue.ActionData = "inventory:transport";
        otherTarget.Action = ActionKind.Guard; otherTarget.ActionTarget = craft; otherTarget.ActionData = "people:assessment";
        foreach (var p in world.People) p.Due = 5;
        long Labor(Person p, long end, bool active = false, string purpose = "world-action")
        {
            var id = world.NextId++; world.Inventory.Labor.Add(new() { Id = id, Person = p.Id, Purpose = purpose, Start = 0, End = end, Units = 100, Active = active }); return id;
        }
        var first = Labor(repeat, 5); var last = Labor(repeat, 5); var profession = Labor(repeat, 5, purpose: "independent-profession");
        var otherEnd = Labor(sameDue, 6, true); var idleRow = Labor(idle, 5, true); var deadRow = Labor(dead, 5, true);
        Labor(crafter, 5); Labor(carrier, 5); Labor(appraiser, 5);
        var next = world.NextId; var before = OwnedInventoryFixture.Snapshot(world);
        using (var transaction = InventoryEngine.Own(world))
        {
            transaction.SynchronizeWorldActions(); transaction.SynchronizeWorldActions();
            InventoryFixture.Check(before == OwnedInventoryFixture.Snapshot(world), "동기화가 인계 전에 원본을 바꿨습니다.");
            transaction.CommitTo(world);
        }
        var rows = world.Inventory.Labor;
        InventoryFixture.Check(world.NextId == next + 2 && rows.Single(l => l.Id == last).Active && !rows.Single(l => l.Id == first).Active &&
            !rows.Single(l => l.Id == profession).Active && !rows.Single(l => l.Id == otherEnd).Active && !rows.Single(l => l.Id == idleRow).Active && !rows.Single(l => l.Id == deadRow).Active,
            "같은 Due의 다른 사람 또는 실제 마지막 world-action 노동 행을 혼동했습니다.");
        InventoryFixture.Check(rows.Single(l => l.Id == next).Person == otherTarget.Id && rows.Single(l => l.Id == next + 1).Person == sameDue.Id &&
            rows.Where(l => l.Active && l.Purpose == "world-action").Select(l => l.Person).Order().SequenceEqual(new[] { repeat.Id, sameDue.Id, otherTarget.Id }.Order()),
            "다른 actor의 같은 작업 Target/ActionData를 직종 예약으로 오인하거나 원래 사람 순서의 ID 할당을 바꿨습니다.");
        using (var transaction = InventoryEngine.Own(world))
        {
            transaction.SynchronizeWorldActions();
            InventoryFixture.Check(transaction.Apply(new CancelProduction("sync-cancel-production", crafter.Id, craft)).Accepted &&
                transaction.Apply(new FinishTransport("sync-cancel-cargo", crafter.Id, transport, false)).Accepted, "실제 직종 취소 준비 실패.");
            transaction.SynchronizeWorldActions(); transaction.SynchronizeWorldActions(); transaction.CommitTo(world);
        }
        rows = world.Inventory.Labor;
        InventoryFixture.Check(world.NextId == next + 2 && rows.Single(l => l.Person == crafter.Id && l.Purpose == "world-action").Active &&
            rows.Single(l => l.Person == carrier.Id && l.Purpose == "world-action").Active && !rows.Single(l => l.Person == appraiser.Id && l.Purpose == "world-action").Active,
            "같은 토큰에서 취소/운송 실패 뒤 다시 만든 범위가 직전 active job/carrier 인덱스를 사용했습니다.");
        var hash = GameCodec.Hash(world);
        InventoryFixture.Check(hash == Baseline, "기존 반복 동기화·직종 취소·노동·ID 결과가 달라졌습니다: " + hash);
    }
}

public sealed class InventorySynchronizationOriginScenario : IFeatureScenario
{
    public string Name => "inventory sync: 중복 ID·교차 namespace·잘못된 프로필·메타데이터의 기존 초기 거부 우선순위 보존";
    public void Run()
    {
        Action<GameState>[] corruptions = [
            w => w.Items[0].Id = w.People[0].Id,
            w => { w.Inventory.Jobs.Add(new() { Id = w.Items[0].Id }); w.Inventory.Contracts.Add(new() { Id = w.Items[0].Id }); },
            w => { w.Inventory.Labor.Add(new() { Id = w.Items[0].Id }); w.Inventory.BaseCarryCapacity = -1; },
            w => w.Inventory.Metadata.Add(new() { Item = w.Items[0].Id, Custodian = long.MaxValue }),
            w => w.Inventory.Metadata[0].Custodian = long.MaxValue
        ];
        string[] expected = ["Inventory IDs are duplicated or the allocator is stale.", "Inventory IDs are duplicated or the allocator is stale.",
            "Invalid inventory clock, carrying profile, or work profile.", "Duplicate item metadata.", "Person is absent or no longer alive."];
        for (var index = 0; index < corruptions.Length; index++)
        {
            var world = OwnedInventoryFixture.Create(); corruptions[index](world); var before = OwnedInventoryFixture.Snapshot(world);
            string? oldError = null; string? newError = null;
            try { InventoryState.FromWorld(world, world.Inventory); } catch (InvalidDataException error) { oldError = error.Message; }
            try { using var transaction = InventoryEngine.Own(world); transaction.SynchronizeWorldActions(); } catch (InvalidDataException error) { newError = error.Message; }
            InventoryFixture.Check(oldError == expected[index] && newError == oldError && before == OwnedInventoryFixture.Snapshot(world), "새 인덱스가 잘못된 원본을 허용하거나 초기 오류 순서를 바꿨습니다.");
        }
    }
}

public sealed class InventoryOwnedOrdinalReceiptScenario : IFeatureScenario
{
    public string Name => "inventory sync: 공개 custom comparer와 내부 Ordinal 영수증의 대소문자 구분·재전송·충돌 보존";
    public void Run()
    {
        var world = OwnedInventoryFixture.Create(); var herb = world.Items[0].Id;
        var source = InventoryState.FromWorld(world, world.Inventory);
        var initial = InventoryEngine.Apply(source, new SplitInventoryItem("Case-ID", 1, herb, 2));
        InventoryFixture.Check(initial.Accepted, initial.Message); world.Inventory = initial.State.ApplyTo(world);
        world.Inventory.Receipts = new(world.Inventory.Receipts, StringComparer.OrdinalIgnoreCase);
        var publicInput = InventoryState.FromWorld(world, world.Inventory);
        publicInput.Ledger.Receipts = new(publicInput.Ledger.Receipts, StringComparer.OrdinalIgnoreCase);
        var before = OwnedInventoryFixture.Snapshot(world); var command = new SplitInventoryItem("case-id", 1, herb, 1);
        var expected = InventoryEngine.Apply(publicInput, command);
        using (var transaction = InventoryEngine.Own(world))
        {
            var actual = transaction.Apply(command); var replay = transaction.Apply(command);
            InventoryFixture.Check(expected.Accepted && !expected.Replayed && actual.Accepted && !actual.Replayed && actual.Subject == expected.Subject &&
                replay.Accepted && replay.Replayed && replay.Subject == actual.Subject && before == OwnedInventoryFixture.Snapshot(world),
                "custom comparer가 대소문자가 다른 별도 ID를 기존 영수증으로 취급했습니다.");
            transaction.CommitTo(world);
        }
        InventoryFixture.Check(world.Inventory.Receipts.Count == 2 && world.Inventory.Receipts.Comparer.Equals(StringComparer.Ordinal), "인계 영수증이 Ordinal이 아니거나 별도 ID를 유실했습니다.");
        using var collision = InventoryEngine.Own(world); var unchanged = OwnedInventoryFixture.Snapshot(world);
        var rejected = collision.Apply(command with { Quantity = 2 });
        InventoryFixture.Check(!rejected.Accepted && rejected.Message == "Command ID already belongs to a different request." && unchanged == OwnedInventoryFixture.Snapshot(world), "같은 ID의 다른 내용이 재수용됐습니다.");
        OwnedInventoryFixture.Closed(collision, world);
    }
}
