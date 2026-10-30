using System.Buffers.Binary;
using System.Globalization;
using Chronicles.Game.Inventory;

namespace Chronicles.Game.Tests.Features;

public sealed class InventoryPublicTransportSaveScenario : IFeatureScenario
{
    public string Name => "INV-004/017 운송 저장: 실제 JSON2·이진3 파일 왕복·명령 재전송·배송 완료·봉쇄 실패";

    public void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "seonro-transport-save-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        string? expectedFinalHash = null;
        try
        {
            foreach (var binary in new[] { false, true })
            {
                var path = Path.Combine(directory, binary ? "binary.save" : "json.save");
                var world = InventoryWorldFixture.Create(); var state = world.Checkpoint(); var actor = state.People[0].Id;
                InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("transport-save-control", actor, "possess"));
                InventoryWorldFixture.Accept(world, InventoryWorldFixture.Command("transport-save-route", actor, "explore"));
                world.AdvanceTo(1); state = world.Checkpoint();
                var item = state.Items.First(i => i.Owner == actor && i.Definition == "tool").Id;
                var outbound = InventoryWorldFixture.Command("transport-save-outbound", actor, "transport", item, 1);
                InventoryWorldFixture.Accept(world, outbound); state = world.Checkpoint();
                var cargo = state.Inventory.Contracts.Single(c => c.Item == item && c.Status == "open");
                var restored = SaveOpenTransport(world, path, binary, cargo, 1);
                ReplayUnchanged(world, restored, outbound);
                world.AdvanceTo(cargo.Due); restored.AdvanceTo(cargo.Due);
                AssertTerminal(world, restored, cargo, "completed", 1, "delivered");
                ReplayUnchanged(world, restored, outbound);

                var returning = InventoryWorldFixture.Command("transport-save-return", actor, "transport", item, 0);
                InventoryWorldFixture.Accept(world, returning); state = world.Checkpoint();
                cargo = state.Inventory.Contracts.Single(c => c.Item == item && c.Status == "open");
                // Explicit obstruction after a real accepted return order; the cargo is still in flight when saved.
                state.Places[0].BlockedUntil = cargo.Due + 1;
                world = GameWorld.Restore(state);
                restored = SaveOpenTransport(world, path, binary, cargo, 2);
                ReplayUnchanged(world, restored, returning);
                world.AdvanceTo(cargo.Due); restored.AdvanceTo(cargo.Due);
                AssertTerminal(world, restored, cargo, "failed", 1, "contract-failed");
                InventoryFixture.Check(world.Checkpoint().Places[0].BlockedUntil == cargo.Due + 1,
                    "실제 봉쇄 기간이 저장 후 변조됐습니다.");
                ReplayUnchanged(world, restored, returning);
                var hash = world.StateHash();
                InventoryFixture.Check(expectedFinalHash is null || expectedFinalHash == hash,
                    "같은 공개 운송·봉쇄 입력이 JSON2/이진3 파일 선택에 따라 다른 최종 세계를 만들었습니다.");
                expectedFinalHash = hash;
            }
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static GameWorld SaveOpenTransport(GameWorld world, string path, bool binary, InventoryContract expected, long sequence)
    {
        var before = world.Checkpoint(); var hash = world.StateHash();
        AssertOpen(before, expected);
        if (binary) GameSaveStore.SaveAtomic(world, path, 0);
        else GameSaveStore.SaveAtomic(world, path);
        var bytes = File.ReadAllBytes(path);
        InventoryFixture.Check(binary
                ? bytes.AsSpan().StartsWith("SEONRO-021B\0"u8) && BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12, 4)) == 3
                : bytes[0] == '{',
            "운송 중 실제 슬롯이 요청한 JSON2/이진3 봉투 경로를 실행하지 않았습니다.");
        var envelope = GameCodec.DecodeEnvelope(bytes); var restored = GameSaveStore.Load(path);
        var after = restored.Checkpoint();
        InventoryFixture.Check(envelope.Sequence == sequence && envelope.World.StateHash() == hash && restored.StateHash() == hash &&
            world.StateHash() == hash && before.RandomState == after.RandomState && before.NextId == after.NextId,
            "운송 중 common codec/실제 파일 왕복이 상태·순번·RNG·ID를 바꿨습니다.");
        AssertOpen(after, expected);
        InventoryFixture.Check(!File.Exists(path + ".pending"), "원자 교체 뒤 pending이 남았습니다.");
        return restored;
    }

    private static void AssertOpen(GameState state, InventoryContract expected)
    {
        var cargo = state.Inventory.Contracts.Single(c => c.Id == expected.Id);
        var item = state.Items.Single(i => i.Id == expected.Item);
        var carrier = state.People.Single(p => p.Id == expected.Carrier);
        var metadata = state.Inventory.Metadata.Single(m => m.Item == item.Id);
        var reservation = state.Inventory.Reservations.Single(r => r.Id == expected.Reservation);
        var labor = state.Inventory.Labor.Single(l => l.Id == expected.Labor);
        InventoryFixture.Check(cargo.Kind == "transport" && cargo.Status == "open" && cargo.Due == expected.Due && cargo.Due > state.Month &&
            cargo.Origin == expected.Origin && cargo.Destination == expected.Destination && cargo.Reservation == expected.Reservation &&
            cargo.Carrier == expected.Carrier && cargo.Buyer == expected.Buyer && cargo.Seller == expected.Seller &&
            item.Owner == expected.Seller && item.Place == -1 && item.Reserved == expected.Reservation &&
            carrier.Place == expected.Origin && carrier.Action == ActionKind.Guard && carrier.ActionTarget == cargo.Id && carrier.Due == cargo.Due &&
            metadata.Carrier == carrier.Id && metadata.Custodian == carrier.Id && reservation.Active &&
            reservation.Actor == expected.Seller && reservation.Items.Contains(item.Id) && labor.Active && labor.Person == carrier.Id && labor.End == cargo.Due,
            "운송 중 소유권·실제 원위치의 운반자·단일 실물/노동 예약·계약 기한이 보존되지 않았습니다.");
        Invariants.Validate(state);
    }

    private static void ReplayUnchanged(GameWorld world, GameWorld restored, GameCommand command)
    {
        var before = world.StateHash(); var restoredBefore = restored.StateHash();
        var result = world.Execute(command); var repeated = restored.Execute(command);
        InventoryFixture.Check(result.Accepted && result == repeated && world.StateHash() == before && restored.StateHash() == restoredBefore && before == restoredBefore,
            "저장 후 같은 운송 명령을 재전송해 계약·예약·세계가 중복 변경됐습니다.");
    }

    private static void AssertTerminal(GameWorld world, GameWorld restored, InventoryContract expected, string status, int actualPlace, string eventKind)
    {
        var state = world.Checkpoint(); var after = restored.Checkpoint();
        var cargo = state.Inventory.Contracts.Single(c => c.Id == expected.Id);
        var item = state.Items.Single(i => i.Id == expected.Item);
        var carrier = state.People.Single(p => p.Id == expected.Carrier);
        InventoryFixture.Check(world.StateHash() == restored.StateHash() && state.RandomState == after.RandomState &&
            cargo.Status == status && cargo.Due == expected.Due && cargo.EndedMonth == expected.Due && cargo.Escrow == 0 &&
            item.Owner == expected.Buyer && item.Place == actualPlace && carrier.Place == actualPlace && item.Reserved == 0 &&
            !state.Inventory.Reservations.Single(r => r.Id == expected.Reservation).Active &&
            !state.Inventory.Labor.Single(l => l.Id == expected.Labor).Active &&
            state.Inventory.Metadata.Single(m => m.Item == item.Id).Carrier == 0 &&
            state.Inventory.Events.Count(e => e.Kind == eventKind && e.Subject == cargo.Id) == 1,
            "저장 후 동일 due의 완료/봉쇄 실패가 실물 위치·소유권·예약 해제·단일 정산·RNG/hash를 보존하지 않았습니다.");
        Invariants.Validate(state); Invariants.Validate(after);
    }
}
