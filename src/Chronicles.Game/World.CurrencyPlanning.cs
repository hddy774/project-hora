using Chronicles.Game.Currency;
using Chronicles.Game.Inventory;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private long CurrencyPlanningPriority(Person p)
    {
        if (p.Status != LifeStatus.Alive || p.Action != ActionKind.Idle || Law("currency.stoneValue") == 0 ||
            p.Money >= Law("politics.foundCost") || PeopleActionError(p, "redeemCurrency") is not null) return 0;
        return CurrencyPlanningStone(p) is not null || CurrencyPlanningSource(p) is not null ? Law("ai.currencyWeight") : 0;
    }

    private bool TryCurrencyPlanningGoal(Person p)
    {
        if (CurrencyPlanningPriority(p) == 0 || !VisitPlanningNode(p)) return false;
        var stone = CurrencyPlanningStone(p);
        if (stone is not null)
        {
            var shortfall = Law("politics.foundCost") - p.Money;
            var quantity = Math.Min(stone.Quantity, 1 + (shortfall - 1) / Law("currency.stoneValue"));
            // Secure the ordinary one-month action before an irreversible physical redemption.
            // This uses the same labor/capability checks as a player's Guard, without new assets.
            if (Start(p, "guard", 0, "환전 결과 확인") is not null) return false;
            var step = new PlanStep("redeemCurrency", stone.Id, PlanningNumber(quantity));
            var error = ExecutePlanningStep(p, step, out var source);
            if (error is not null)
            {
                BlockPlanningFact(p, "currency-item:" + stone.Id, error, source);
                PlanningFailure(p, step, error, source); return true;
            }
            p.Goal = "교역·실물 영석 전환";
            p.Reason = "자기 현지 영석을 실제 소비해 부족한 창설 재원까지 전환·다음 결정까지 결과 확인";
            return true;
        }

        var observation = CurrencyPlanningSource(p);
        if (observation is null) return false;
        var mining = new PlanStep("mining", observation.Source, CurrencyPolicy.StoneDefinition);
        var failure = ExecutePlanningStep(p, mining, out var evidence);
        if (failure is not null)
        {
            BlockPlanningFact(p, "source:" + observation.Source, failure, evidence);
            state.Inventory.Observations.RemoveAll(o => o.Observer == p.Id && o.Source == observation.Source);
            PlanningFailure(p, mining, failure, evidence); return PausePlanning(p);
        }
        p.Goal = "교역·영석 채광";
        p.Reason = "날짜가 있는 자기 현지 영석 관측으로 실제 채광·실물 완성 전에는 잔액을 만들지 않음";
        return true;
    }

    private Item? CurrencyPlanningStone(Person p) => Owned(p.Id).Where(item => CurrencyPolicy.Functional(item) &&
        !string.IsNullOrWhiteSpace(item.Source) && !PlanningBlocked(p, "currency-item:" + item.Id) &&
        InventoryAccessible(p, item.Id, out _)).OrderBy(item => item.Id).FirstOrDefault();

    private SourceObservation? CurrencyPlanningSource(Person p) => KnownPlanningSources(p).Where(source =>
        source.Place == p.Place && source.Definition == CurrencyPolicy.StoneDefinition && source.Remaining > 0 &&
        source.Activity == InventoryDefinitions.ExtractionActivity(CurrencyPolicy.StoneDefinition) &&
        !PlanningBlocked(p, "source:" + source.Source)).OrderBy(source => source.Source).FirstOrDefault();
}
