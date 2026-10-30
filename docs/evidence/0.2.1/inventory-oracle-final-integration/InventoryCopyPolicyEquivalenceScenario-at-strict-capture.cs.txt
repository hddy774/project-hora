namespace Chronicles.Game.Tests.Features;

public sealed class InventoryCopyPolicyEquivalenceScenario : IFeatureScenario
{
    public string Name => "inventory copy oracle: 현행 규칙의 strict·JSON15·typed15 독립 전체 전사·실제 저장 bytes 동등";
    public void Run()
    {
        Compare(new InventoryOwnedWorldReferenceScenario().Capture(), new InventoryOwnedWorldReferenceScenario(true).Capture(),
            new InventoryOwnedWorldReferenceScenario(true, true).Capture(), InventoryOwnedWorldReferenceScenario.Baseline);
        Compare(new InventoryOwnedEcologyReferenceScenario().Capture(), new InventoryOwnedEcologyReferenceScenario(true).Capture(),
            new InventoryOwnedEcologyReferenceScenario(true, true).Capture(), InventoryOwnedEcologyReferenceScenario.Baseline);
    }

    private static void Compare(InventoryReferenceTrace strict, InventoryReferenceTrace json, InventoryReferenceTrace typed, string expected)
    {
        // Every independently built world must match the pre-captured constant, not another execution's actual digest.
        strict.AssertFixed(expected); json.AssertFixed(expected); typed.AssertFixed(expected);
        InventoryFixture.Check(strict.Mode == "strict" && strict.ProjectionSnapshots == 0 && strict.TypedSnapshots == 0 &&
            json.Mode == "json15" && json.ProjectionSnapshots > 0 && json.TypedSnapshots == 0 &&
            typed.Mode == "typed15" && typed.ProjectionSnapshots > 0 && typed.TypedSnapshots > 0, "삼자 대조가 실제 서로 다른 복사 경로를 실행하지 않았습니다.");
        strict.AssertEquivalent(json); strict.AssertEquivalent(typed);
    }
}
