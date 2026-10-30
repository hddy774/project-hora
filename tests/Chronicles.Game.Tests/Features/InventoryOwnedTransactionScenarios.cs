using System.Text.Json;
using Chronicles.Game.Inventory;

namespace Chronicles.Game.Tests.Features;

internal static class OwnedInventoryFixture
{
    internal static GameState Create()
    {
        var inventory = InventoryFixture.Create();
        InventoryFixture.Add(inventory, "herb", 10); InventoryFixture.Add(inventory, "wood", 2);
        InventoryFixture.Add(inventory, "ore", 5); InventoryFixture.Add(inventory, "bag"); InventoryFixture.Add(inventory, "tool");
        inventory.Ledger.Metadata.Add(new() { Item = inventory.Items[0].Id, Custodian = 1, Provenance = ["실제 검사 출처"] });
        return new() { NextId = inventory.NextId, People = inventory.People, Items = inventory.Items, Materials = inventory.Materials, Inventory = inventory.Ledger };
    }
    internal static string Snapshot(GameState world) => JsonSerializer.Serialize(world);
    internal static long Item(GameState world, string definition) => world.Items.Single(i => i.Definition == definition).Id;
    internal static void Closed(InventoryEngine.OwnedTransaction transaction, GameState world)
    {
        var before = Snapshot(world);
        Action[] operations = [() => transaction.CommitTo(world), () => transaction.Apply(new SplitInventoryItem("closed", 1, 5, 1)),
            () => transaction.ItemQuantity(5), () => transaction.Job(1), () => transaction.AttachKnowledgeActivity(1, 1),
            () => transaction.TuneIfNeeded(new("closed-tune", 500, 100, 100, 25)), () => transaction.SynchronizeWorldActions()];
        foreach (var operation in operations)
        {
            var rejected = false; try { operation(); } catch (InvalidOperationException) { rejected = true; }
            InventoryFixture.Check(rejected && before == Snapshot(world), "소비·폐기된 인벤토리 토큰이 재사용되거나 세계를 바꿨습니다.");
        }
        transaction.Dispose(); transaction.Dispose();
    }
}

public sealed class InventoryOwnedLifetimeScenario : IFeatureScenario
{
    public string Name => "inventory owned lifetime: 단일 인계·불변 결과·기존 참조 격리와 소비 후 재사용 거부";
    public void Run()
    {
        var world = OwnedInventoryFixture.Create(); var original = OwnedInventoryFixture.Snapshot(world);
        var person = world.People[0]; var oldItems = world.Items; var oldItem = oldItems[0]; var oldFlow = world.Materials["herb"];
        var oldMaterials = world.Materials; var oldLedger = world.Inventory; var oldMetadata = oldLedger.Metadata[0];
        var oldSkills = person.Skills; var oldEquipment = person.Equipment;
        using var transaction = InventoryEngine.Own(world);
        var result = transaction.Apply(new SplitInventoryItem("owned-once", 1, oldItem.Id, 2));
        InventoryFixture.Check(result.Accepted && transaction.ItemQuantity(result.Subject) == 2 && original == OwnedInventoryFixture.Snapshot(world), "개인 후보가 인계 전에 세계와 참조를 공유했습니다.");
        var retained = result with { Subject = long.MaxValue };
        transaction.CommitTo(world);
        InventoryFixture.Check(ReferenceEquals(person, world.People[0]) && world.Items.Single(i => i.Id == result.Subject).Quantity == 2 && retained.Subject != result.Subject,
            "인계가 실제 Person 객체를 교체하거나 외부 값 결과를 실물 권위로 사용했습니다.");
        var committed = OwnedInventoryFixture.Snapshot(world);
        oldItem.Quantity++; oldItems.Clear(); oldFlow.Opening++; oldMaterials.Clear(); oldMetadata.Provenance.Add("외부 변조");
        oldLedger.Receipts.Clear(); oldLedger.Metadata.Clear(); oldSkills["외부 변조"] = 9999; oldEquipment[3] = oldItem.Id;
        InventoryFixture.Check(committed == OwnedInventoryFixture.Snapshot(world), "기존 아이템·원장·재료·능력·장비 참조가 인계된 세계에 새어 들어갔습니다.");
        OwnedInventoryFixture.Closed(transaction, world);
    }
}

public sealed class InventoryOwnedFailureScenario : IFeatureScenario
{
    public string Name => "inventory owned failure: 두 번째 처리·판매 실패와 예외가 전체 후보·ID·재료를 폐기";
    public void Run()
    {
        var world = OwnedInventoryFixture.Create(); var before = OwnedInventoryFixture.Snapshot(world);
        using (var transaction = InventoryEngine.Own(world))
        {
            var started = transaction.Apply(new StartProduction("owned-start", 1, "pill", 1));
            InventoryFixture.Check(started.Accepted, started.Message);
            var publicStarted = InventoryEngine.Apply(InventoryState.FromWorld(world, world.Inventory), new StartProduction("owned-start", 1, "pill", 1));
            var expected = InventoryEngine.Apply(publicStarted.State, new BeginProduction("owned-wrong-actor", 2, started.Subject));
            var rejected = transaction.Apply(new BeginProduction("owned-wrong-actor", 2, started.Subject));
            InventoryFixture.Check(!rejected.Accepted && rejected.Message == expected.Message && before == OwnedInventoryFixture.Snapshot(world), "두 번째 처리 실패가 예약·실물·ID 또는 거부 이유를 바꿨습니다.");
            OwnedInventoryFixture.Closed(transaction, world);
        }
        using (var transaction = InventoryEngine.Own(world))
        {
            var split = transaction.Apply(new SplitInventoryItem("owned-rollback-split", 1, OwnedInventoryFixture.Item(world, "herb"), 2));
            InventoryFixture.Check(split.Accepted && !transaction.Apply(new OfferItem("owned-bad-offer", 1, split.Subject, 0, 12)).Accepted && before == OwnedInventoryFixture.Snapshot(world),
                "판매 실패가 앞선 분할과 ID 할당을 세계에 남겼습니다.");
            OwnedInventoryFixture.Closed(transaction, world);
        }
        using (var transaction = InventoryEngine.Own(world))
        {
            var rejected = transaction.Apply(new StartProduction("owned-missing", 1, "armor", 1));
            InventoryFixture.Check(!rejected.Accepted && before == OwnedInventoryFixture.Snapshot(world), "부분 예약 뒤 재료 부족이 후보 밖에 유출됐습니다.");
            OwnedInventoryFixture.Closed(transaction, world);
        }
        using (var transaction = InventoryEngine.Own(world))
        {
            var rejected = false; try { transaction.Apply(null!); } catch (ArgumentNullException) { rejected = true; }
            InventoryFixture.Check(rejected && before == OwnedInventoryFixture.Snapshot(world), "예외가 세계를 바꾸거나 유효한 결과를 반환했습니다.");
            OwnedInventoryFixture.Closed(transaction, world);
        }
        using (var transaction = InventoryEngine.Own(world))
        {
            var started = transaction.Apply(new StartProduction("owned-bad-attachment", 1, "pill", 1));
            InventoryFixture.Check(started.Accepted, started.Message);
            var rejected = false; try { transaction.AttachKnowledgeActivity(long.MaxValue, 1); } catch (InvalidOperationException) { rejected = true; }
            InventoryFixture.Check(rejected && before == OwnedInventoryFixture.Snapshot(world), "잘못된 전용 연결 작업이 부분 후보를 남겼습니다.");
            OwnedInventoryFixture.Closed(transaction, world);
        }
    }
}

public sealed class InventoryOwnedStaleScenario : IFeatureScenario
{
    public string Name => "inventory owned stale: 월·할당자·개정·실물·사람·재료 지문과 원래 세계 인계 경계";
    public void Run()
    {
        Action<GameState>[] changes = [w => w.Month++, w => w.NextId++, w => w.Revision++, w => w.Items[0].Quality++,
            w => { w.People[0].Money++; w.People[1].Money--; }, w => w.Materials["herb"].Opening++];
        foreach (var change in changes)
        {
            var world = OwnedInventoryFixture.Create(); using var transaction = InventoryEngine.Own(world);
            InventoryFixture.Check(transaction.Apply(new SplitInventoryItem("owned-stale", 1, world.Items[0].Id, 2)).Accepted, "오래된 후보 검사 준비 실패.");
            change(world); var before = OwnedInventoryFixture.Snapshot(world); var stale = false;
            try { transaction.CommitTo(world); } catch (InvalidOperationException error) { stale = error.Message.Contains("stale", StringComparison.Ordinal); }
            InventoryFixture.Check(stale && before == OwnedInventoryFixture.Snapshot(world), "오래된 인계가 최신 권위를 덮어썼습니다.");
            OwnedInventoryFixture.Closed(transaction, world);
        }
        var original = OwnedInventoryFixture.Create(); var equalWorld = GameCodec.Clone(original);
        using var wrong = InventoryEngine.Own(original); var unchanged = OwnedInventoryFixture.Snapshot(equalWorld); var rejected = false;
        try { wrong.CommitTo(equalWorld); } catch (InvalidOperationException) { rejected = true; }
        InventoryFixture.Check(rejected && unchanged == OwnedInventoryFixture.Snapshot(equalWorld) && unchanged == OwnedInventoryFixture.Snapshot(original), "소비형 후보가 동일 내용의 다른 세계에 인계됐습니다.");
        OwnedInventoryFixture.Closed(wrong, original);
    }
}

public sealed class InventoryOwnedEngineParityScenario : IFeatureScenario
{
    public string Name => "inventory owned parity: 공개 복사 경로와 예약·처리·전용 연결·재전송 결과 및 직렬화 일치";
    public void Run()
    {
        var world = OwnedInventoryFixture.Create(); var referenceWorld = GameCodec.Clone(world);
        var reference = InventoryState.FromWorld(referenceWorld, referenceWorld.Inventory); using var owned = InventoryEngine.Own(world);
        long Apply(InventoryCommand command)
        {
            var before = JsonSerializer.Serialize(reference); var expected = InventoryEngine.Apply(reference, command); var actual = owned.Apply(command);
            InventoryFixture.Check(before == JsonSerializer.Serialize(reference) && expected.Accepted && actual.Accepted == expected.Accepted && actual.Message == expected.Message &&
                actual.Subject == expected.Subject && actual.Replayed == expected.Replayed, "공개/소비형 규칙·재전송·입력 격리가 달라졌습니다: " + command.Id);
            reference = expected.State; return actual.Subject;
        }
        Apply(new TuneInventoryProfile("owned-profile", 600, 100, 100, 25));
        var herb = OwnedInventoryFixture.Item(world, "herb"); var split = Apply(new SplitInventoryItem("owned-split", 1, herb, 2));
        Apply(new MergeStacks("owned-merge", 1, herb, split));
        var tool = OwnedInventoryFixture.Item(world, "tool"); Apply(new EquipItem("owned-tool", 1, tool)); Apply(new UnequipItem("owned-tool-back", 1, tool));
        Apply(new StoreItem("owned-bag", 1, OwnedInventoryFixture.Item(world, "ore"), OwnedInventoryFixture.Item(world, "bag")));
        var inputs = new Dictionary<string, int> { ["herb"] = 1 };
        var reservation = Apply(new ReserveInventoryMaterials("owned-external", 1, "실물 예약", inputs)); inputs["herb"] = 999;
        Apply(new ConsumeInventoryAssets("owned-consume", 1, reservation));
        var jobId = Apply(new StartProduction("owned-job", 1, "pill", 2));
        Apply(new BeginProduction("owned-process", 1, jobId)); Apply(new BeginProduction("owned-process", 1, jobId));
        var job = owned.Job(jobId); var modified = job with { Id = 0, Recipe = "외부 값 변경", Due = 999 };
        InventoryFixture.Check(job.Id == jobId && job.Recipe == "pill" && job.Due == 2 && modified != job, "작업 조회가 불변 값 정보를 반환하지 않았습니다.");
        reference.Ledger.Jobs.Single(j => j.Id == jobId).KnowledgeActivity = 17; owned.AttachKnowledgeActivity(jobId, 17);
        referenceWorld.Inventory = reference.ApplyTo(referenceWorld); owned.CommitTo(world);
        InventoryFixture.Check(OwnedInventoryFixture.Snapshot(referenceWorld) == OwnedInventoryFixture.Snapshot(world), "같은 전이가 개정·할당자·재료·실물·영수증·원장에서 다른 결과를 냈습니다.");
        var committed = OwnedInventoryFixture.Snapshot(referenceWorld);
        reference.Items.Clear(); reference.Materials.Clear(); reference.People[0].Skills["격리 검사"] = 1; reference.Ledger.Jobs[0].KnowledgeActivity = 999;
        InventoryFixture.Check(committed == OwnedInventoryFixture.Snapshot(referenceWorld), "기존 공개 ApplyTo가 후보 참조를 인계된 세계와 공유했습니다.");
        OwnedInventoryFixture.Closed(owned, world);
    }
}

public sealed class InventoryOwnedRejectionParityScenario : IFeatureScenario
{
    public string Name => "inventory owned rejection: ID 충돌·빈 ID·오버플로와 기존 오류 우선순위 보존";
    public void Run()
    {
        var initial = OwnedInventoryFixture.Create(); var item = initial.Items[0].Id;
        var prior = InventoryEngine.Apply(InventoryState.FromWorld(initial, initial.Inventory), new SplitInventoryItem("used", 1, item, 2));
        initial.Inventory = prior.State.ApplyTo(initial);
        InventoryCommand[] commands = [new SplitInventoryItem("", 1, item, 1), new SplitInventoryItem("used", 1, item, 1),
            new SplitInventoryItem("overflow-valid", 1, item, 1), new SplitInventoryItem("overflow-invalid", 1, item, 0)];
        foreach (var command in commands)
        {
            var world = GameCodec.Clone(initial);
            if (command.Id.StartsWith("overflow", StringComparison.Ordinal)) world.People[0].Money = long.MaxValue;
            var before = OwnedInventoryFixture.Snapshot(world); var expected = InventoryEngine.Apply(InventoryState.FromWorld(world, world.Inventory), command);
            using var owned = InventoryEngine.Own(world); var actual = owned.Apply(command);
            InventoryFixture.Check(!actual.Accepted && actual.Accepted == expected.Accepted && actual.Message == expected.Message && actual.Subject == expected.Subject &&
                before == OwnedInventoryFixture.Snapshot(world), "거부 이유·오버플로 우선순위 또는 입력 원자성이 달라졌습니다: " + command.Id);
            OwnedInventoryFixture.Closed(owned, world);
        }
    }
}
