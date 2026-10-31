using System.Text.Json;
using Chronicles.Game.Inventory;

namespace Chronicles.Game.Tests.Features;

internal static class InventoryValidationBoundaryFixture
{
    internal const string IdError = "Inventory IDs are duplicated or the allocator is stale.";
    internal const string CapacityError = "Shared person or facility labor capacity exceeded.";
    internal static void Check(InventoryState state, string? expected = null)
    {
        var before = JsonSerializer.Serialize(state); string? actual = null;
        try { InventoryEngine.Validate(state); }
        catch (InvalidDataException error) { actual = error.Message; }
        InventoryFixture.Check(actual == expected && before == JsonSerializer.Serialize(state),
            "기존 검증 경계 또는 거부 순서가 달라졌습니다: " + actual);
    }
    internal static InventoryState Copy(InventoryState state) => InventoryCommitmentTranscript.Copy(state);
    internal static void Labor(InventoryState state, long person, long start, long end, int units, long facility = 0, int capacity = 100) =>
        state.Ledger.Labor.Add(new() { Id = state.NextId++, Person = person, Purpose = "명시된 검증 경계", Start = start,
            End = end, Units = units, Facility = facility, FacilityCapacity = capacity });
}

public sealed class InventorySingletonScheduleBoundaryScenario : IFeatureScenario
{
    public string Name => "inventory validation boundary: 단일 노동·인접·겹침·시설 용량과 선행 invalid 오류를 보존";
    public void Run()
    {
        var state = InventoryFixture.Create();
        InventoryValidationBoundaryFixture.Labor(state, 1, 0, 1, 100, 99);
        InventoryValidationBoundaryFixture.Check(state);
        var changed = InventoryValidationBoundaryFixture.Copy(state); changed.Ledger.Labor[0].FacilityCapacity = 99;
        InventoryValidationBoundaryFixture.Check(changed, InventoryValidationBoundaryFixture.CapacityError);
        changed = InventoryValidationBoundaryFixture.Copy(state); changed.Ledger.Labor[0].End = 0;
        InventoryValidationBoundaryFixture.Check(changed, "Invalid labor interval.");
        changed = InventoryValidationBoundaryFixture.Copy(state); changed.Ledger.Labor[0].Units = 101;
        InventoryValidationBoundaryFixture.Check(changed, "Invalid labor interval.");
        changed = InventoryValidationBoundaryFixture.Copy(state); changed.Ledger.Labor[0].FacilityCapacity = 0;
        InventoryValidationBoundaryFixture.Check(changed, "Invalid labor interval.");

        var adjacent = InventoryFixture.Create();
        InventoryValidationBoundaryFixture.Labor(adjacent, 1, 0, 1, 100, 99);
        InventoryValidationBoundaryFixture.Labor(adjacent, 1, 1, 2, 100, 99);
        InventoryValidationBoundaryFixture.Check(adjacent);
        var overlap = InventoryFixture.Create();
        InventoryValidationBoundaryFixture.Labor(overlap, 1, 0, 2, 60);
        InventoryValidationBoundaryFixture.Labor(overlap, 1, 1, 3, 40);
        InventoryValidationBoundaryFixture.Check(overlap);
        changed = InventoryValidationBoundaryFixture.Copy(overlap); changed.Ledger.Labor[0].Units = 61;
        InventoryValidationBoundaryFixture.Check(changed, InventoryValidationBoundaryFixture.CapacityError);
        var facility = InventoryFixture.Create();
        InventoryValidationBoundaryFixture.Labor(facility, 1, 0, 2, 40, 99, 60);
        InventoryValidationBoundaryFixture.Labor(facility, 2, 0, 2, 40, 99, 60);
        InventoryValidationBoundaryFixture.Check(facility, InventoryValidationBoundaryFixture.CapacityError);
        changed = InventoryValidationBoundaryFixture.Copy(facility); changed.Ledger.Labor[1].FacilityCapacity = 70;
        InventoryValidationBoundaryFixture.Check(changed, "Facility commitments disagree about capacity.");

        // Every interval is checked before any group schedule, even if an earlier group's capacity is bad.
        changed = InventoryValidationBoundaryFixture.Copy(state); changed.Ledger.Labor[0].FacilityCapacity = 99;
        InventoryValidationBoundaryFixture.Labor(changed, 2, 0, 0, 100);
        InventoryValidationBoundaryFixture.Check(changed, "Invalid labor interval.");
        var extreme = InventoryFixture.Create();
        InventoryValidationBoundaryFixture.Labor(extreme, 1, long.MaxValue - 1, long.MaxValue, 100, 99);
        InventoryValidationBoundaryFixture.Check(extreme);
    }
}

public sealed class InventorySinglePassIdBoundaryScenario : IFeatureScenario
{
    public string Name => "inventory validation boundary: 빈 ID·양수·교차 중복·원래 수집 순서·최대 allocator 경계를 보존";
    public void Run()
    {
        var empty = new InventoryState { NextId = 1 };
        InventoryValidationBoundaryFixture.Check(empty);
        empty.NextId = 0; InventoryValidationBoundaryFixture.Check(empty, InventoryValidationBoundaryFixture.IdError);
        var state = InventoryFixture.Create(); state.People.Reverse(); state.NextId = 3;
        InventoryValidationBoundaryFixture.Check(state);
        var changed = InventoryValidationBoundaryFixture.Copy(state); changed.NextId = 2;
        InventoryValidationBoundaryFixture.Check(changed, InventoryValidationBoundaryFixture.IdError);
        changed = InventoryValidationBoundaryFixture.Copy(state); changed.People[0].Id = 0;
        InventoryValidationBoundaryFixture.Check(changed, InventoryValidationBoundaryFixture.IdError);
        changed = InventoryValidationBoundaryFixture.Copy(state); changed.Ledger.Reservations.Add(new() { Id = 1, Actor = 1 });
        InventoryValidationBoundaryFixture.Check(changed, InventoryValidationBoundaryFixture.IdError);
        changed.Month = -1;
        InventoryValidationBoundaryFixture.Check(changed, "Invalid inventory clock, carrying profile, or work profile.");
        var maximum = InventoryFixture.Create(); maximum.NextId = long.MaxValue;
        maximum.Ledger.Sources.Add(new() { Id = long.MaxValue - 1, Definition = "wood", Origin = "명시된 ID 경계" });
        InventoryValidationBoundaryFixture.Check(maximum);
        maximum.Ledger.Sources[0].Id = long.MaxValue;
        InventoryValidationBoundaryFixture.Check(maximum, InventoryValidationBoundaryFixture.IdError);
        changed = InventoryValidationBoundaryFixture.Copy(state); changed.NextId = 2;
        changed.Ledger.Metadata.Add(new() { Item = 99 }); changed.Ledger.Metadata.Add(new() { Item = 99 });
        InventoryValidationBoundaryFixture.Check(changed, InventoryValidationBoundaryFixture.IdError);
    }
}
