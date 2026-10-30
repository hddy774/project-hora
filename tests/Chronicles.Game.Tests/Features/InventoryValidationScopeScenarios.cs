using System.Security.Cryptography;
using System.Text.Json;
using Chronicles.Game.Inventory;

namespace Chronicles.Game.Tests.Features;

public sealed class InventoryValidationScopeScenario : IFeatureScenario
{
    // Captured with the unchanged validator at 8745eb4440498f7aff3d0b70a3c09eb2004a908e.
    // The reference covers actual command replies, state hashes, malformed-state errors and the adapter fingerprint.
    private const string BaselineFingerprint = "c817b2675a79b1bd1e5fb2b3d1503f695bf04620e100ab4c43a4d6899b411f32";
    private const string BaselineTranscript = "7ed31d02e2947132e8087bc0db6a6d3cf6776a320a1f312677df57bc74e376be";

    public string Name => "inventory validation scope: 이전 실제 명령·해시·거부 결과와 호출 사이 변경을 보존";
    public void Run()
    {
        var trace = new List<string>();
        var world = InventoryWorldFixture.Create(107).Checkpoint();
        var adapter = InventoryState.FromWorld(world, world.Inventory);
        InventoryFixture.Check(adapter.BaseFingerprint == BaselineFingerprint, "세계 후보의 기존 출처 지문이 달라졌습니다.");
        trace.Add("fingerprint:" + adapter.BaseFingerprint);
        world.People[0].Money++; world.People[1].Money--;
        var changedWorld = JsonSerializer.Serialize(world); var staleRejected = false;
        try { adapter.ApplyTo(world); } catch (InvalidOperationException error) { staleRejected = error.Message.Contains("stale", StringComparison.Ordinal); }
        InventoryFixture.Check(staleRejected && changedWorld == JsonSerializer.Serialize(world), "오래된 후보가 세계를 변경했습니다.");

        var state = InventoryFixture.Create(); state.Ledger.BaseCarryCapacity = 6;
        var outer = InventoryFixture.Add(state, "bag"); var inner = InventoryFixture.Add(state, "bag");
        var ore = InventoryFixture.Add(state, "ore", 4); var tool = InventoryFixture.Add(state, "tool");
        var carrierBag = InventoryFixture.Add(state, "bag", owner: 2); var food = InventoryFixture.Add(state, "food", 3, owner: 2);
        state.Ledger.Metadata.Add(new() { Item = outer, Custodian = 1, Capacity = 30 });
        state.Ledger.Metadata.Add(new() { Item = inner, Custodian = 1, Capacity = 12 });

        long Apply(InventoryCommand command, bool expected = true)
        {
            var before = JsonSerializer.Serialize(state); var result = InventoryEngine.Apply(state, command);
            InventoryFixture.Check(result.Accepted == expected && before == JsonSerializer.Serialize(state), "실제 명령 수용 또는 입력 원자성이 달라졌습니다: " + command.Id + " " + result.Message);
            if (result.Accepted) state = result.State;
            trace.Add(JsonSerializer.Serialize(new { command.Id, result.Accepted, result.Message, result.Subject, result.Replayed, Hash = Hash(state) }));
            return result.Subject;
        }
        void RejectChanged(InventoryState source, Action<InventoryState> corrupt, string expected)
        {
            var candidate = Copy(source); corrupt(candidate); var before = JsonSerializer.Serialize(candidate); string? failure = null;
            try { InventoryEngine.Validate(candidate); } catch (InvalidDataException error) { failure = error.Message; }
            InventoryFixture.Check(failure == expected && before == JsonSerializer.Serialize(candidate), "변조 거부가 달라지거나 입력을 고쳤습니다: " + failure);
            trace.Add(failure!); InventoryEngine.Validate(state);
        }

        Apply(new EquipItem("scope-carry-bag", 2, carrierBag));
        Apply(new StoreItem("scope-fill", 1, ore, inner));
        Apply(new StoreItem("scope-nest", 1, inner, outer));
        Apply(new GrantUse("scope-owner-use", 1, outer, 1, true));
        Apply(new GrantUse("scope-borrower-use", 1, outer, 2, true));
        Apply(new SetCustodian("scope-custody", 1, outer, 2));
        Apply(new StoreItem("scope-other-owner-food", 2, food, outer));
        InventoryFixture.Person(state, 2).Place = 1; InventoryEngine.Validate(state);
        InventoryFixture.Check(InventoryEngine.Location(state, ore) == 1 && !InventoryEngine.IsAccessible(state, 1, outer), "현지 보관자가 바뀌었는데 이전 위치를 사용했습니다.");
        trace.Add(Hash(state)); InventoryFixture.Person(state, 2).Place = 0; InventoryEngine.Validate(state);
        Apply(new ReserveInventoryPhysical("scope-reserve-nonempty", 2, "scope-proof", outer, 1), false);
        Apply(new OfferItem("scope-multiple-owners", 1, outer, 5, 10), false);
        var reservation = Apply(new ReserveInventoryPhysical("scope-reserve", 1, "scope-proof", ore, 4));
        var reserved = Copy(state);
        Apply(new StoreItem("scope-reserved-item", 1, ore, Ground: true), false);
        Apply(new ReleaseInventoryAssets("scope-release", 1, reservation));
        Apply(new GrantUse("scope-tool-use", 1, tool, 2, true));
        Apply(new EquipItem("scope-tool-equip", 2, tool)); var equipped = Copy(state);
        Apply(new OfferItem("scope-equipped-offer", 1, tool, 5, 10), false);
        Apply(new UnequipItem("scope-tool-return", 2, tool, ReturnToGround: true));
        var split = Apply(new SplitInventoryItem("scope-split", 1, ore, 2));
        Apply(new MergeStacks("scope-merge", 1, ore, split));
        var replayHash = Hash(state); Apply(new MergeStacks("scope-merge", 1, ore, split));
        InventoryFixture.Check(replayHash == Hash(state), "같은 실제 명령이 실물을 다시 합쳤습니다.");
        InventoryFixture.RoundTrip(ref state); trace.Add(Hash(state));

        RejectChanged(state, s => s.Items.Add(new() { Id = outer }), "Inventory IDs are duplicated or the allocator is stale.");
        RejectChanged(state, s => s.Ledger.Metadata.Add(new() { Item = outer }), "Duplicate item metadata.");
        RejectChanged(state, s => InventoryFixture.Item(s, outer).Container = inner, "Container cycle detected.");
        RejectChanged(state, s => InventoryFixture.Item(s, ore).Container = long.MaxValue, "Physical item does not exist.");
        RejectChanged(state, s => InventoryFixture.Item(s, ore).Owner = long.MaxValue, "Person is absent or no longer alive.");
        RejectChanged(state, s => s.Ledger.Metadata.Single(m => m.Item == outer).Carrier = 1, "Carrying capacity exceeded; use explicit ground storage.");
        RejectChanged(state, s => InventoryFixture.Item(s, inner).Damage = 100, "Recursive container capacity exceeded.");
        RejectChanged(equipped, s => InventoryFixture.Person(s, 1).Equipment[8] = tool, "An item is equipped by multiple people.");
        RejectChanged(reserved, s => s.Ledger.Reservations.Add(new() { Id = s.NextId++, Actor = 2, Purpose = "scope-conflict", Items = [ore] }), "Active reservation lost its physical item.");
        Apply(new DamageItem("scope-broken-container", 1, inner, 100));
        InventoryFixture.Check(InventoryFixture.Item(state, ore).Container == 0 && InventoryFixture.Item(state, ore).Place == 0,
            "파손 후 실물을 실제 지점에서 보존하지 못했습니다.");
        var hash = Hash(state); InventoryEngine.Validate(state); InventoryEngine.Validate(state);
        InventoryFixture.Check(hash == Hash(state), "읽기 검증이 권위 상태를 변경했습니다.");
        trace.Add(hash);
        InventoryFixture.Check(Hash(trace) == BaselineTranscript, "기존 실제 명령·상태 해시·거부 결과가 달라졌습니다: " + Hash(trace));
    }
    private static InventoryState Copy(InventoryState state) => JsonSerializer.Deserialize<InventoryState>(JsonSerializer.Serialize(state))!;
    private static string Hash<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
}
