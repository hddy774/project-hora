using System.Reflection;
using System.Text.Json;
using Chronicles.Game.Inventory;
using Chronicles.Game.Tests.Experiments;

namespace Chronicles.Game.Tests.Features;

public sealed class InventoryProjectionGuardScenario : IFeatureScenario
{
    public string Name => "inventory projection: 70개 schema·실제 IL reader와 컴파일러 람다의 금지 읽기 자기검출";
    public void Run()
    {
        var actual = typeof(Person).GetProperties().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();
        var classified = InventoryPersonReadGuard.Projected.Concat(InventoryPersonReadGuard.Unused).Order(StringComparer.Ordinal).ToArray();
        InventoryFixture.Check(actual.Length == 70 && actual.SequenceEqual(classified), "Person schema에 분류되지 않은 필드가 있습니다.");
        foreach (var field in actual) InventoryFixture.Check(InventoryEngine.OwnedTransaction.IncludesProjectedPersonProperty(field) ==
            InventoryPersonReadGuard.Projected.Contains(field, StringComparer.Ordinal), "투영 입력과 검증된 읽기집합이 다릅니다: " + field);
        var violations = InventoryPersonReadGuard.ForbiddenAccesses(InventoryPersonReadGuard.InventoryMethods());
        InventoryFixture.Check(violations.Count == 0, "분류되지 않은 실제 Person 읽기/쓰기: " + string.Join("; ", violations));
        Func<Person, int> forbidden = p => p.Journal.Count;
        var detected = InventoryPersonReadGuard.ForbiddenAccesses([forbidden.Method]);
        InventoryFixture.Check(detected.Count == 1 && detected[0].Contains("get_Journal", StringComparison.Ordinal), "가드가 실제 금지 람다 읽기를 검출하지 못했습니다.");
        Action<Person> forbiddenWrite = p => p.Work = 1;
        InventoryFixture.Check(InventoryPersonReadGuard.ForbiddenAccesses([forbiddenWrite.Method]).Single().Contains("set_Work", StringComparison.Ordinal), "통과 필드를 실제 규칙 쓰기로 오인했습니다.");
    }
}

public sealed class InventoryProjectionPublicReferenceScenario : IFeatureScenario
{
    public string Name => "inventory projection: 실제 공개 명령·생계 AI가 기본 factory를 거쳐 기존 두 전체 transcript와 저장 속행 일치";
    public void Run()
    {
        new InventoryOwnedWorldReferenceScenario(true).Run();
        new InventoryOwnedEcologyReferenceScenario(true).Run();
    }
}

public sealed class InventoryProjectionDefaultResumeScenario : IFeatureScenario
{
    public string Name => "inventory projection: Create·Restore·Decode 기본 투영과 strict reference의 실제 작업·저장 재개 동등성";
    public void Run()
    {
        var world = InventoryWorldFixture.Create(116); var actor = world.Checkpoint().People[0].Id;
        InventoryFixture.Check(world.InventoryProjectionEnabled && world.InventoryProjectionSnapshots == 0, "새 세계가 기본 투영 경로를 사용하지 않습니다.");
        InventoryWorldFixture.Accept(world, new("adopted-control", actor, "possess"));
        var request = new GameCommand("adopted-gather", actor, "gather", Data: "food"); InventoryWorldFixture.Accept(world, request);
        var pending = world.Checkpoint(); var job = pending.Inventory.Jobs.Single(j => j.Actor == actor && j.Status == "processing");
        InventoryFixture.Check(world.InventoryProjectionSnapshots > 0, "공개 Execute 후보가 기본 투영 factory를 실행하지 않았습니다.");
        var encoded = GameCodec.Encode(world); var restored = GameWorld.Restore(pending); var decoded = GameCodec.Decode(encoded); var strict = GameCodec.Decode(encoded);
        InventoryFixture.Check(restored.InventoryProjectionEnabled && decoded.InventoryProjectionEnabled && restored.InventoryProjectionSnapshots == 0 && decoded.InventoryProjectionSnapshots == 0,
            "복원 기본 모드가 다르거나 진단 scope가 저장·공유됐습니다.");
        strict.UseStrictInventoryForVerification(); InventoryFixture.Check(!strict.InventoryProjectionEnabled, "반대 strict 검증 선택을 유지하지 못했습니다.");
        foreach (var candidate in new[] { world, restored, decoded, strict }) candidate.AdvanceTo(job.Due);
        InventoryFixture.Check(world.StateHash() == restored.StateHash() && world.StateHash() == decoded.StateHash() && world.StateHash() == strict.StateHash() &&
            restored.InventoryProjectionSnapshots > 0 && decoded.InventoryProjectionSnapshots > 0 && strict.InventoryProjectionSnapshots == 0 &&
            GameCodec.Encode(world).SequenceEqual(GameCodec.Encode(decoded)), "기본 투영/strict 참조/저장 재개의 실물·상태·저장 bytes가 다릅니다.");
        var before = world.StateHash(); InventoryWorldFixture.Accept(world, request); InventoryWorldFixture.Accept(decoded, request);
        InventoryFixture.Check(world.StateHash() == before && decoded.StateHash() == before, "기본 투영 저장 후 명령 재전송이 작업을 다시 소비했습니다.");
    }
}

internal static class InventoryProjectionFixture
{
    internal static string UnusedSnapshot(Person person) => JsonSerializer.Serialize(InventoryPersonReadGuard.Unused.ToDictionary(
        name => name, name => typeof(Person).GetProperty(name)!.GetValue(person), StringComparer.Ordinal));

    internal static object Sentinel(PropertyInfo property, object? current)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (type == typeof(string)) return "개인정보·일지·계획 sentinel";
        if (type == typeof(bool)) return !(bool)(current ?? false);
        if (type == typeof(int)) return 777;
        if (type == typeof(long)) return 777L;
        if (type == typeof(uint)) return 777U;
        if (type.IsEnum) return Enum.GetValues(type).GetValue(Enum.GetValues(type).Length - 1)!;
        if (type.IsArray) return Enumerable.Repeat(777, ((Array)current!).Length).ToArray();
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var element = type.GetGenericArguments()[0];
            return JsonSerializer.Deserialize(element == typeof(int) || element == typeof(long) ? "[777]" : "[{}]", type)!;
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(SortedDictionary<,>))
        {
            var value = type.GetGenericArguments()[1];
            return JsonSerializer.Deserialize(value == typeof(int) ? "{\"777\":777}" : "{\"777\":{}}", type)!;
        }
        throw new InvalidOperationException("새 sentinel 분류가 필요합니다: " + property);
    }
}

public sealed class InventoryProjectionStaleScenario : IFeatureScenario
{
    private readonly bool typedCopy;
    public InventoryProjectionStaleScenario() { }
    internal InventoryProjectionStaleScenario(bool typedCopy) { this.typedCopy = typedCopy; }
    private InventoryEngine.OwnedTransaction Candidate(GameState world) => typedCopy ? InventoryEngine.OwnTypedProjectedForExperiment(world) : InventoryEngine.OwnProjected(world);
    public string Name => "inventory projection: 15개 입력별 stale와 전체 Person strict 경계·미사용 변화 보존";
    public void Run()
    {
        foreach (var field in InventoryPersonReadGuard.Projected)
        {
            var world = OwnedInventoryFixture.Create(); var strict = GameCodec.Clone(world);
            var full = InventoryState.FromWorld(strict, strict.Inventory); using var strictToken = InventoryEngine.Own(strict);
            using var token = Candidate(world);
            InventoryFixture.Check(token.Apply(new SplitInventoryItem("projection-stale", 1, world.Items[0].Id, 2)).Accepted, "오래된 후보 준비 실패.");
            var p = world.People[1];
            switch (field)
            {
                case "Slot": p.Slot++; break; case "SlotGeneration": p.SlotGeneration++; break; case "Id": p.Id = world.NextId; break;
                case "BodyId": p.BodyId++; break; case "SoulId": p.SoulId++; break; case "Status": p.Status = LifeStatus.Dead; break;
                case "Place": p.Place++; break; case "Money": p.Money++; break; case "Health": p.Health--; break; case "Work": p.Work++; break;
                case "Action": p.Action = ActionKind.Guard; break; case "Due": p.Due++; break; case "ActionTarget": p.ActionTarget++; break;
                case "Skills": p.Skills["새 숙련"] = 1; break; case "Equipment": p.Equipment[1] = world.Items[0].Id; break;
                default: throw new InvalidOperationException(field);
            }
            var property = typeof(Person).GetProperty(field)!; property.SetValue(strict.People[1], property.GetValue(p));
            var strictBefore = OwnedInventoryFixture.Snapshot(strict); var fullRejected = false; var strictRejected = false;
            try { full.ApplyTo(strict); } catch (InvalidOperationException error) { fullRejected = error.Message.Contains("stale", StringComparison.Ordinal); }
            try { strictToken.CommitTo(strict); } catch (InvalidOperationException error) { strictRejected = error.Message.Contains("stale", StringComparison.Ordinal); }
            InventoryFixture.Check(fullRejected && strictRejected && strictBefore == OwnedInventoryFixture.Snapshot(strict), "공개/기존strict 입력필드 stale 계약이 달라졌습니다: " + field);
            var before = OwnedInventoryFixture.Snapshot(world); var rejected = false;
            try { token.CommitTo(world); } catch (InvalidOperationException error) { rejected = error.Message.Contains("stale", StringComparison.Ordinal); }
            InventoryFixture.Check(rejected && before == OwnedInventoryFixture.Snapshot(world), "실험 읽기필드 변경을 인계가 덮어썼습니다: " + field);
            OwnedInventoryFixture.Closed(token, world);
        }
        Action<GameState>[] authorityChanges = [w => w.Month++, w => w.NextId++, w => w.Revision++, w => w.Items[0].Quality++, w => w.Materials["herb"].Opening++];
        foreach (var change in authorityChanges)
        {
            var world = OwnedInventoryFixture.Create(); using var token = Candidate(world);
            change(world); var before = OwnedInventoryFixture.Snapshot(world); var rejected = false;
            try { token.CommitTo(world); } catch (InvalidOperationException error) { rejected = error.Message.Contains("stale", StringComparison.Ordinal); }
            InventoryFixture.Check(rejected && before == OwnedInventoryFixture.Snapshot(world), "Person 외 실제 권위 필드의 지문이 누락됐습니다.");
        }
        foreach (var field in InventoryPersonReadGuard.Unused)
        {
            var property = typeof(Person).GetProperty(field)!;
            var strict = OwnedInventoryFixture.Create(); var projected = GameCodec.Clone(strict);
            var full = InventoryState.FromWorld(strict, strict.Inventory); using var strictToken = InventoryEngine.Own(strict);
            using var token = Candidate(projected);
            property.SetValue(strict.People[1], InventoryProjectionFixture.Sentinel(property, property.GetValue(strict.People[1])));
            property.SetValue(projected.People[1], InventoryProjectionFixture.Sentinel(property, property.GetValue(projected.People[1])));
            var expected = OwnedInventoryFixture.Snapshot(strict); var fullRejected = false; var strictRejected = false;
            try { full.ApplyTo(strict); } catch (InvalidOperationException error) { fullRejected = error.Message.Contains("stale", StringComparison.Ordinal); }
            try { strictToken.CommitTo(strict); } catch (InvalidOperationException error) { strictRejected = error.Message.Contains("stale", StringComparison.Ordinal); }
            token.CommitTo(projected);
            InventoryFixture.Check(fullRejected && strictRejected && expected == OwnedInventoryFixture.Snapshot(strict) && expected == OwnedInventoryFixture.Snapshot(projected),
                "공개/기존strict 전필드 계약 또는 실험 미사용 변화의 원본 보존이 달라졌습니다: " + field);
        }
    }
}

public sealed class InventoryProjectionNoninterferenceScenario : IFeatureScenario
{
    public string Name => "inventory projection: 모든 미사용 필드의 실제 명령 비간섭과 개인 원천 관측·물리 ledger 보존";
    public void Run()
    {
        var world = OwnedInventoryFixture.Create();
        foreach (var field in InventoryPersonReadGuard.Unused)
        {
            var property = typeof(Person).GetProperty(field)!;
            property.SetValue(world.People[1], InventoryProjectionFixture.Sentinel(property, property.GetValue(world.People[1])));
        }
        world.People[0].Work = 4321; world.People[0].Skills["기존 타직종"] = 123;
        world.Inventory.Observations.Add(new(1, 987, 0, 12, 0, "자기 실제 관측", "food", "source.gathering"));
        world.Inventory.Observations.Add(new(2, 988, 1, 77, 0, "다른 사람의 비공개 관측", "ore", "source.mining"));
        var privateFacts = InventoryProjectionFixture.UnusedSnapshot(world.People[1]); var journal = world.People[1].Journal;
        var strict = GameCodec.Clone(world); var reference = InventoryState.FromWorld(strict, strict.Inventory);
        var command = new SplitInventoryItem("projection-physical", 1, world.Items[0].Id, 2); var expected = InventoryEngine.Apply(reference, command);
        InventoryFixture.Check(expected.Accepted, expected.Message); strict.Inventory = expected.State.ApplyTo(strict);
        using var token = InventoryEngine.OwnProjected(world); var actual = token.Apply(command);
        InventoryFixture.Check(actual.Accepted && actual.Subject == expected.Subject && actual.Message == expected.Message, "개인지식이나 일지가 실제 입력 전이를 바꿨습니다.");
        token.CommitTo(world);
        InventoryFixture.Check(OwnedInventoryFixture.Snapshot(world) == OwnedInventoryFixture.Snapshot(strict) && privateFacts == InventoryProjectionFixture.UnusedSnapshot(world.People[1]) &&
            ReferenceEquals(journal, world.People[1].Journal) && world.People[0].Work == 4321 && world.People[0].Skills["기존 타직종"] == 123,
            "투영의 기본값이 개인 정보·Work·다른 숙련을 인계에서 덮어썼습니다.");
        var own = InventoryKnowledgeQueries.KnownSources(world.Inventory, 1);
        InventoryFixture.Check(own.Count == 1 && own[0].Source == 987 && own[0].Definition == "food" && world.Inventory.Observations.Count == 2,
            "개인 원천 관측을 지우거나 다른 사람의 원천 지식을 합쳤습니다.");
    }
}

public sealed class InventoryProjectionOwnershipScenario : IFeatureScenario
{
    public string Name => "inventory projection: mutable 참조·재사용·예외·두 번째 처리 및 소비 후 실패의 원자성";
    public void Run()
    {
        var world = OwnedInventoryFixture.Create();
        long Prepare(InventoryCommand command)
        {
            var result = InventoryEngine.Apply(InventoryState.FromWorld(world, world.Inventory), command);
            InventoryFixture.Check(result.Accepted, result.Message); world.Inventory = result.State.ApplyTo(world); return result.Subject;
        }
        var previousJob = Prepare(new StartProduction("projection-history-start", 1, "pill", 2));
        Prepare(new BeginProduction("projection-history-process", 1, previousJob)); world.Month = 2;
        Prepare(new FinishProduction("projection-history-finish", 1, previousJob));
        var reserved = Prepare(new ReserveInventoryPhysical("projection-retained-ore", 1, "서로 다른 실물 예약", OwnedInventoryFixture.Item(world, "ore"), 1));
        world.Inventory.Sources.Add(new() { Id = world.NextId++, Place = 0, Definition = "food", Activity = "source.gathering", Remaining = 12, Capacity = 12, Origin = "명시적 유한 원천 검사" });
        var person = world.People[0]; var items = world.Items; var flow = world.Materials["herb"];
        var metadata = world.Inventory.Metadata.First(); var receipts = world.Inventory.Receipts; var skills = person.Skills; var equipment = person.Equipment;
        var reservationItems = world.Inventory.Reservations.Single(r => r.Id == reserved).Items;
        var oldJob = world.Inventory.Jobs.Single(); var oldLabor = world.Inventory.Labor.First(); var oldSource = world.Inventory.Sources.Single();
        var before = OwnedInventoryFixture.Snapshot(world);
        using (var token = InventoryEngine.OwnProjected(world))
        {
            var started = token.Apply(new StartProduction("projection-start", 1, "pill", 4));
            InventoryFixture.Check(started.Accepted && token.Apply(new BeginProduction("projection-process", 1, started.Subject)).Accepted, "실제 입력 소비 준비 실패.");
            var job = token.Job(started.Subject); token.AttachKnowledgeActivity(job.Id, 17);
            InventoryFixture.Check(before == OwnedInventoryFixture.Snapshot(world), "실험 후보가 인계 전에 원본을 변경했습니다.");
            token.CommitTo(world); var committed = OwnedInventoryFixture.Snapshot(world);
            items.Clear(); flow.Consumption++; metadata.Provenance.Add("외부 변조"); receipts.Clear(); skills["외부 변조"] = 1; equipment[3] = 999;
            reservationItems.Clear(); oldJob.Inputs.Clear(); oldJob.Consumed.Clear(); oldJob.Provenance.Add("외부 변조"); oldLabor.Active = true; oldSource.Remaining++;
            InventoryFixture.Check(committed == OwnedInventoryFixture.Snapshot(world) && ReferenceEquals(person, world.People[0]) &&
                world.Inventory.Jobs.Single(j => j.Id == job.Id).KnowledgeActivity == 17 && world.Materials["herb"].Consumption == 6, "실험 인계의 nested alias 또는 Person 객체 동일성·실물 소비가 틀렸습니다.");
            OwnedInventoryFixture.Closed(token, world);
        }
        for (var afterConsumption = 0; afterConsumption < 2; afterConsumption++)
        {
            world = OwnedInventoryFixture.Create(); before = OwnedInventoryFixture.Snapshot(world);
            using var token = InventoryEngine.OwnProjected(world);
            var started = token.Apply(new StartProduction("projection-failed-start", 1, "pill", 2)); InventoryFixture.Check(started.Accepted, started.Message);
            if (afterConsumption == 1) InventoryFixture.Check(token.Apply(new BeginProduction("projection-paid-process", 1, started.Subject)).Accepted, "실물 소비 준비 실패.");
            var failed = token.Apply(new BeginProduction("projection-failure", 2, started.Subject));
            InventoryFixture.Check(!failed.Accepted && before == OwnedInventoryFixture.Snapshot(world), "두 번째 처리/소비 후 실패가 예약·실물·ID·원장을 유출했습니다.");
            OwnedInventoryFixture.Closed(token, world);
        }
        world = OwnedInventoryFixture.Create(); before = OwnedInventoryFixture.Snapshot(world);
        using var exceptionToken = InventoryEngine.OwnProjected(world); var threw = false;
        try { exceptionToken.Apply(null!); } catch (ArgumentNullException) { threw = true; }
        InventoryFixture.Check(threw && before == OwnedInventoryFixture.Snapshot(world), "실험 예외가 원본을 바꿨습니다."); OwnedInventoryFixture.Closed(exceptionToken, world);
        using var otherWorld = InventoryEngine.OwnProjected(world); var wrong = false;
        try { otherWorld.CommitTo(GameCodec.Clone(world)); } catch (InvalidOperationException) { wrong = true; }
        InventoryFixture.Check(wrong && before == OwnedInventoryFixture.Snapshot(world), "실험 후보가 다른 세계로 인계됐습니다."); OwnedInventoryFixture.Closed(otherWorld, world);
    }
}
