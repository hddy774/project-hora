using System.Security.Cryptography;
using System.Text.Json;
using Chronicles.Game.Inventory;

namespace Chronicles.Game.Tests.Features;

internal sealed class InventoryCommitmentTranscript
{
    private readonly List<string> rows = [];
    internal long Apply(ref InventoryState state, InventoryCommand command)
    {
        var before = Hash(state); var result = InventoryEngine.Apply(state, command);
        InventoryFixture.Check(before == Hash(state) && result.Accepted, command.Id + ": " + result.Message);
        state = result.State;
        rows.Add(JsonSerializer.Serialize(new { command.Id, result.Accepted, result.Message, result.Subject, result.Replayed, Hash = Hash(state) }));
        return result.Subject;
    }
    internal void Valid(InventoryState state)
    {
        var before = Hash(state); InventoryEngine.Validate(state);
        InventoryFixture.Check(before == Hash(state), "읽기 검증이 입력을 변경했습니다."); rows.Add(before);
    }
    internal void Reject(InventoryState state, Action<InventoryState> corrupt, string expected)
    {
        var candidate = Copy(state); corrupt(candidate); var before = Hash(candidate); string? failure = null;
        try { InventoryEngine.Validate(candidate); }
        catch (Exception error) when (error is InvalidDataException or OverflowException or NullReferenceException or ArgumentNullException)
        { failure = error.GetType().Name + ":" + error.Message; }
        InventoryFixture.Check(failure == expected && before == Hash(candidate), "기존 오류 우선순위가 달라졌습니다: " + failure);
        rows.Add(failure!); rows.Add(before);
    }
    internal void Finish(string expected)
    {
        var actual = Hash(rows);
        InventoryFixture.Check(actual == expected, "3ba78ce 검증 응답·순서·입력 해시가 달라졌습니다: " + actual);
    }
    internal static InventoryState Copy(InventoryState state) => JsonSerializer.Deserialize<InventoryState>(JsonSerializer.Serialize(state))!;
    private static string Hash<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    internal static long Source(InventoryState state, string definition, long remaining = 10)
    {
        var id = state.NextId++;
        state.Ledger.Sources.Add(new() { Id = id, Place = 0, Definition = definition,
            Activity = InventoryDefinitions.ExtractionActivity(definition)!, Remaining = remaining, Capacity = remaining, Origin = "명시된 유한 검증 원천" });
        return id;
    }
}

public sealed class InventorySourceCommitmentReferenceScenario : IFeatureScenario
{
    // Captured from the unchanged product validator at 3ba78ce8d6dd734ebf1ec1a1fb939db34e90d923.
    private const string Baseline = "06b5a5a8247bc114b5da061f2e62b825e87b7c7bdb71952027084f81683ac13c";
    public string Name => "inventory commitment reference: 같은 원천의 동시 예약·진행·취소 소비와 호출 사이 변경을 보존";
    public void Run()
    {
        var trace = new InventoryCommitmentTranscript(); var state = InventoryFixture.Create();
        var source = InventoryCommitmentTranscript.Source(state, "wood");
        InventoryCommitmentTranscript.Source(state, "ore");
        var first = trace.Apply(ref state, new ExtractResource("reserve-first", 1, source, 4, 1));
        var second = trace.Apply(ref state, new ExtractResource("reserve-second", 2, source, 3, 1));
        trace.Apply(ref state, new BeginProduction("begin-first", 1, first));
        trace.Valid(state); trace.Valid(state);
        trace.Reject(state, s => s.Ledger.Sources[0].Remaining = 6, "InvalidDataException:Natural stock is overcommitted.");
        var widened = InventoryCommitmentTranscript.Copy(state);
        widened.Ledger.Sources[0].Capacity = widened.Ledger.Sources[0].Remaining = long.MaxValue;
        foreach (var job in widened.Ledger.Jobs) job.OutputQuantity = int.MaxValue;
        trace.Valid(widened);
        // Keep even malformed negative quantities in the original source sum before the job clause rejects them.
        trace.Reject(state, s => { s.Ledger.Jobs[0].OutputQuantity = int.MaxValue; s.Ledger.Jobs[1].OutputQuantity = int.MinValue; },
            "InvalidDataException:Extraction lacks an actual finite source or positive quantity.");
        trace.Apply(ref state, new CancelProduction("cancel-consumed-first", 1, first));
        InventoryFixture.Check(state.Ledger.Sources[0].Remaining == 6 && state.Ledger.Jobs[0].Status == "cancelled",
            "진행 중 취소가 실제 원천 소비를 복구했습니다.");
        trace.Apply(ref state, new BeginProduction("begin-second", 2, second)); state.Month = 1;
        trace.Apply(ref state, new FinishProduction("finish-second", 2, second));
        InventoryFixture.Check(state.Ledger.Sources[0].Remaining == 3 && state.Items.Single(i => i.Definition == "wood").Quantity == 3,
            "취소/완료 예약을 다시 더하거나 유한 원천을 복제했습니다.");
        trace.Reject(state, s => s.Ledger.Jobs[0].Source = long.MaxValue,
            "InvalidDataException:Extraction lacks an actual finite source or positive quantity.");
        InventoryFixture.RoundTrip(ref state); trace.Valid(state);
        trace.Finish(Baseline);
    }
}

public sealed class InventoryProcessingCommitmentReferenceScenario : IFeatureScenario
{
    private const string Baseline = "caf0efd99a57151cba9a1a31bae6de7bb41f62eb8cf5aab7f43eb28b53362ae8";
    public string Name => "inventory commitment reference: 소비되어 없는 입력의 예약 연결·동시 작업·위조·취소를 보존";
    public void Run()
    {
        var trace = new InventoryCommitmentTranscript(); var state = InventoryFixture.Create();
        foreach (var person in state.People)
        { InventoryFixture.Add(state, "herb", 3, person.Id); InventoryFixture.Add(state, "wood", 1, person.Id); }
        var first = trace.Apply(ref state, new StartProduction("draft-first", 1, "pill", 1));
        var second = trace.Apply(ref state, new StartProduction("draft-second", 2, "pill", 1));
        trace.Apply(ref state, new BeginProduction("consume-first", 1, first));
        trace.Apply(ref state, new BeginProduction("consume-second", 2, second));
        InventoryFixture.Check(state.Items.Count == 0 && state.Materials["herb"].Consumption == 6, "실제 공정 입력이 소비되지 않았습니다.");
        trace.Valid(state); trace.Valid(state);
        trace.Reject(state, s => s.Ledger.Jobs[1].Inputs.RemoveAt(0), "InvalidDataException:Active reservation lost its physical item.");
        trace.Reject(state, s => s.Ledger.Reservations[0].Items.Add(s.Ledger.Reservations[0].Items[0]), "InvalidDataException:Duplicate reserved physical item.");
        trace.Reject(state, s => s.Ledger.Jobs[1].Inputs.Add(s.Ledger.Jobs[1].Inputs[0]), "InvalidDataException:Production input was reserved twice.");
        trace.Reject(state, s => s.Ledger.Reservations[0].Active = false, "InvalidDataException:Active production lost its commitments.");
        trace.Reject(state, s => s.Ledger.Labor[0].Active = false, "InvalidDataException:Active production lost its commitments.");
        trace.Reject(state, s => s.Ledger.Jobs[1].Labor = s.Ledger.Jobs[0].Labor,
            "InvalidDataException:An asset or labor reservation backs multiple independent commitments.");
        trace.Reject(state, s => { s.Ledger.Jobs[1].Reservation = s.Ledger.Jobs[0].Reservation; s.Ledger.Reservations[1].Items.Clear(); },
            "InvalidDataException:An asset or labor reservation backs multiple independent commitments.");
        trace.Apply(ref state, new CancelProduction("cancel-consumed", 1, first));
        InventoryFixture.Check(state.Items.Count == 0 && state.Materials["herb"].Consumption == 6, "취소가 소비한 입력을 복구했습니다.");
        trace.Reject(state, s => s.Ledger.Jobs[0].Reservation = long.MaxValue, "InvalidDataException:Production lacks reservations.");
        state.Month = 1; trace.Apply(ref state, new FinishProduction("finish-paid", 2, second));
        InventoryFixture.RoundTrip(ref state); trace.Valid(state);
        trace.Finish(Baseline);
    }
}

public sealed class InventoryCommitmentErrorOrderReferenceScenario : IFeatureScenario
{
    private const string Baseline = "2e0087a9b2b08d26dc4a850d35cfc057325d170785ce71309ef0cc8970b2e23b";
    public string Name => "inventory commitment reference: 앞 원천 오류·뒤 입력 오류·long overflow의 기존 거부 순서를 보존";
    public void Run()
    {
        var trace = new InventoryCommitmentTranscript(); var state = InventoryFixture.Create();
        var source = InventoryCommitmentTranscript.Source(state, "wood");
        InventoryCommitmentTranscript.Source(state, "ore");
        var job = trace.Apply(ref state, new ExtractResource("source-order", 1, source, 4, 1));
        trace.Apply(ref state, new BeginProduction("source-processing", 1, job));
        var overflow = InventoryCommitmentTranscript.Copy(state);
        overflow.Materials.Add("ore", new() { Opening = long.MaxValue, Extraction = 1 });
        trace.Reject(overflow, s => s.Ledger.Sources[0].Place = 100, "InvalidDataException:Invalid finite natural stock.");
        trace.Reject(overflow, s => s.Ledger.Sources[0].Remaining = 3, "InvalidDataException:Natural stock is overcommitted.");
        trace.Reject(overflow, s => s.Ledger.Jobs[0].Source = long.MaxValue,
            "InvalidDataException:Extraction lacks an actual finite source or positive quantity.");
        trace.Reject(overflow, _ => { }, "OverflowException:Arithmetic operation resulted in an overflow.");
        // Building a read index must not eagerly visit a later malformed input list before the source clause.
        trace.Reject(state, s => { s.Ledger.Sources[0].Place = 100; s.Ledger.Jobs[0].Inputs = null!; },
            "InvalidDataException:Invalid finite natural stock.");
        trace.Reject(state, s => { s.Ledger.Sources[0].Remaining = 3; s.Ledger.Jobs[0].Inputs = null!; },
            "InvalidDataException:Natural stock is overcommitted.");
        trace.Reject(state, s => { s.Ledger.Sources[1].Place = 100; s.Ledger.Labor[0].Start = -1; },
            "InvalidDataException:Invalid labor interval.");
        trace.Reject(state, s => { s.Ledger.Sources[1].Place = 100; s.Ledger.Sources[0].Remaining = 3; },
            "InvalidDataException:Natural stock is overcommitted.");
        var reversed = InventoryCommitmentTranscript.Copy(state); reversed.Ledger.Sources.Reverse();
        trace.Reject(reversed, s => { s.Ledger.Sources[0].Place = 100; s.Ledger.Sources[1].Remaining = 3; },
            "InvalidDataException:Invalid finite natural stock.");
        trace.Valid(state);
        trace.Finish(Baseline);
    }
}
