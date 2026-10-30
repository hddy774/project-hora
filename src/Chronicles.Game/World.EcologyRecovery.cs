using Chronicles.Game.Ecology;
using Chronicles.Game.Inventory;
using Chronicles.Game.Knowledge;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    internal long EcologyFoodTarget(Person person) =>
        checked(EcologyMonthlyFoodNeed(person) * CurrentEcologyRules().FoodReserveMonths);

    private long EcologyMonthlyFoodNeed(Person person)
    {
        var children = dependents.GetValueOrDefault(person.Id) ?? [];
        var count = children.Count(p => p.Status == LifeStatus.Alive && p.Place == person.Place &&
            Month - p.Born < PeopleMaturityMonths(p));
        return checked(Law("ecology.foodUse") * (count + 1L));
    }

    private static string EcologyFailureKey(int place, string definition) => $"ecology:failed:{place}:{definition}";
    private bool RecentEcologyFailure(Person person, string definition, string? kind = null) =>
        person.Beliefs.TryGetValue(EcologyFailureKey(person.Place, definition) + (kind is null ? "" : ":" + kind), out var failure) && failure.Expires > Month;
    private bool EcologyAttemptedThisMonth(Person person, string definition) =>
        person.Beliefs.TryGetValue(EcologyFailureKey(person.Place, definition) + ":exploreSource", out var failure) && failure.Observed == Month;

    private void RecordEcologyFailure(Person person, EcologyPlan decision, string commandId, string error)
    {
        // The same rejection can mean occupied stock or a stale observation. It is not permanent depletion.
        var evidence = $"{commandId}:actual-rejection:{decision.Kind}:{decision.Definition}:target:{decision.Target}";
        Remember(person, "ecology:lastFailure", error, evidence, 100);
        var key = EcologyFailureKey(person.Place, decision.Definition);
        Remember(person, key, error, evidence, 100);
        if (person.Beliefs.TryGetValue(key, out var failure))
            person.Beliefs[key] = failure with { Expires = Month + Math.Min(Law("ai.ecologyFailureMonths"), Law("ai.beliefLifetime")) };
        var actionKey = key + ":" + decision.Kind;
        Remember(person, actionKey, error, evidence, 100);
        if (person.Beliefs.TryGetValue(actionKey, out var actionFailure))
            person.Beliefs[actionKey] = actionFailure with { Expires = Month + Math.Min(Law("ai.ecologyFailureMonths"), Law("ai.beliefLifetime")) };
        if (decision.Kind == "travel")
        {
            key = $"ecology:route-failed:{person.Place}:{decision.Target}";
            Remember(person, key, error, evidence, 100);
            if (person.Beliefs.TryGetValue(key, out var routeFailure))
                person.Beliefs[key] = routeFailure with { Expires = Month + Math.Min(Law("ai.ecologyFailureMonths"), Law("ai.beliefLifetime")) };
        }
    }

    private static bool IsFoodProductionPlan(Person person) => person.Plan.Any(s =>
        s.Action is "replan" or "finishGoal" && s.Data == "farm.food");

    private bool TryEcologyFoodRecovery(Person person, EcologyDecisionInput input)
    {
        var food = input.Sources.Where(s => s.Place == person.Place && s.Definition == "food")
            .OrderByDescending(s => s.Observed).ThenBy(s => s.Source).FirstOrDefault();
        var afterGatherFailure = person.Beliefs.TryGetValue(EcologyFailureKey(person.Place, "food") + ":gather", out var rejected) &&
            rejected.Expires > Month && (food is null || food.Observed < rejected.Observed);
        if ((food is null || food.Observed < Month - 1 || afterGatherFailure) && !EcologyAttemptedThisMonth(person, "food"))
            if (TryEcologyAction(person, new("exploreSource", food?.Source ?? 0, 0, 0, "food", "", "식량 현장 재확인",
                "실제 채집 실패 또는 낡은 개인 관측 뒤 식량만 유료 관측"))) return true;
        // A starving actor cannot waive the survey's physical food cost. Basic food gathering is
        // already a public, paid local action; one failed attempt leads to other timed recovery.
        if (!input.Stock.Any(s => s.Available && s.Definition == "food" && s.Quantity > 0) &&
            (food is null || food.ObservedRemaining != 0 || food.Observed < Month - 1) &&
            !RecentEcologyFailure(person, "food", "gather") && TryEcologyAction(person,
                new("gather", food?.Source ?? 0, 0, 0, "food", "", "관측 비용 부족에서 실제 기본 채집 시도",
                    "식량 관측 비용을 면제하지 않고 기존 현지 채집의 노동·유한 원천으로 확인"))) return true;
        if (TryEcologyFoodProduction(person, input)) return true;
        if (TryEcologyKnownMovement(person, input, "food")) return true;
        if (food is not null && food.Observed < Month && !EcologyAttemptedThisMonth(person, "food"))
            if (TryEcologyAction(person, new("exploreSource", food.Source, 0, 0, "food", "", "식량 예약 경합 재확인",
                "관측 잔량과 사용 가능 여부를 구분하여 한 달을 들여 재확인"))) return true;
        if (person.KnownPlaces.Count <= 1 && TryEcologyAction(person,
            new("explore", 0, 0, 0, "food", "", "알려진 이동로 조사", "생계 실패 뒤 실제 현장 탐색으로 인접 이동로를 확인"))) return true;
        return TryEcologyAction(person, new("wait", 0, 0, 0, "food", "", "생계 조건 재확인 대기",
            "관측·도구·육신·알려진 경로 조건을 충족하지 못해 실제 한 달을 대기하며 재검사"));
    }

    private bool TryEcologyFoodProduction(Person person, EcologyDecisionInput input)
    {
        const string recipeId = "farm.food";
        if (!InventoryDefinitions.Recipes.TryGetValue(recipeId, out var recipe)) return false;
        var adoption = KnowledgeFor(person, "recipe:" + recipeId);
        if (adoption is null || adoption.Comprehension < state.Knowledge.Rules.PracticeMinimumComprehension || adoption.Belief == KnowledgeBelief.Contradicted)
        {
            if (IsFoodProductionPlan(person) && person.Plan.Any(s => s.Action == "awaitKnowledge"))
                return ContinueIntelligentPlan(person);
            var medium = state.Knowledge.Media.Where(m => CanReadKnowledgeItem(person, m.ItemId) &&
                CanConsultKnowledge(person, "recipe:" + recipeId, m.EditionId))
                .OrderBy(m => m.ItemId).FirstOrDefault();
            if (medium is not null)
            {
                person.Plan = [new(adoption?.Belief == KnowledgeBelief.Contradicted ? "correctKnowledge" : "study", medium.ItemId, ""),
                    new("replan", 0, recipeId)];
                person.Goal = "생계 공정의 실제 독해"; person.Reason = "자기에게 실제 읽기 권한·현장 접근이 있는 농사 출처를 시간 들여 읽음";
                return ContinueIntelligentPlan(person);
            }
            if (!person.ObservedPeople.Values.Any(o => o.Id != person.Id && PlanningKnownPerson(person, o.Id) &&
                !PlanningBlocked(person, "teacher:" + o.Id + ":recipe:" + recipeId))) return false;
            return RequestPlanningKnowledge(person, "recipe:" + recipeId, recipeId);
        }
        if (IsFoodProductionPlan(person) && person.Plan.Any(s => s.Action is "confirm" or "finishGoal"))
            return ContinueIntelligentPlan(person);
        if (PeopleActionError(person, "farm") is not null || PeopleActionError(person, "craft") is not null) return false;
        person.Plan = [new("replan", 0, recipeId)];
        var available = input.Stock.Where(s => s.Available && !s.Equipped).ToArray();
        foreach (var material in recipe.Inputs.OrderBy(i => i.Key, StringComparer.Ordinal))
        {
            var own = available.Where(s => s.Definition == material.Key).Sum(s => (long)s.Quantity);
            if (own >= material.Value) continue;
            var offer = input.Listings.Where(l => l.Kind == "sale" && l.Place == person.Place && l.Seller != person.Id &&
                l.Definition == material.Key && l.Quantity >= material.Value - own && l.Price <= person.Money && l.Until > Month)
                .OrderBy(l => l.Price).ThenBy(l => l.Contract).FirstOrDefault();
            if (offer is not null && TryEcologyAction(person, new("trade", offer.Contract, offer.Quantity, offer.Price,
                material.Key, recipeId, "식량 생산재의 실제 매입", offer.Source))) return true;
            var source = input.Sources.Where(s => s.Place == person.Place && s.Definition == material.Key && s.ObservedRemaining > 0)
                .OrderByDescending(s => s.Observed).ThenBy(s => s.Source).FirstOrDefault();
            if (source is not null && !RecentEcologyFailure(person, material.Key, "gather") && TryEcologyAction(person,
                new("gather", source.Source, 0, 0, material.Key, recipeId, "식량 생산재의 실제 채집", source.SourceEvidence))) return true;
            if (!EcologyAttemptedThisMonth(person, material.Key) && TryEcologyAction(person,
                new("exploreSource", source?.Source ?? 0, 0, 0, material.Key, recipeId, "식량 생산재 현장 확인",
                    $"자기가 배운 {recipeId}의 필요한 {material.Key}를 실제 유료 명령으로 확인"))) return true;
            return TryEcologyKnownMovement(person, input, material.Key);
        }
        return StartProductionPlan(person, recipe);
    }

    private bool TryEcologyKnownMovement(Person person, EcologyDecisionInput input, string definition)
    {
        foreach (var next in input.Places.Where(p => p.Adjacent && !p.KnownBlocked &&
            (!person.Beliefs.TryGetValue($"ecology:route-failed:{person.Place}:{p.Place}", out var failed) || failed.Expires <= Month))
            .OrderBy(p => p.Observed).ThenBy(p => p.Place))
            if (TryEcologyAction(person, new("travel", next.Place, 0, 0, definition, "farm.food", "알려진 인접지에서 생계 정보 확인",
                $"개인 이동로 {person.Place}->{next.Place}·식량/생산재 현재 잔량은 미상·관측 {next.Observed}"))) return true;
        return false;
    }

    private bool TryEcologyAction(Person person, EcologyPlan decision)
    {
        var commandId = $"ecology-ai:{person.BodyId}:{Month}:{Revision}:{state.NextId}:{decision.Kind}";
        var error = PeopleActionError(person, decision.Kind);
        var command = new GameCommand(commandId, person.Id, decision.Kind, decision.Target,
            Data: decision.Kind == "craft" ? decision.Recipe : decision.Definition);
        error ??= ValidateExplorationInteraction(command, person);
        if (error is null && decision.Kind is "trade" or "craft" or "gather" or "exploreSource")
        {
            if (!TryInventoryCommand(command, person, out error)) error = "실제 재고 실행 어댑터가 없는 행동입니다.";
            if (error is null && decision.Kind == "trade")
            {
                person.Action = ActionKind.Guard; person.Due = Month + 1;
                person.ActionTarget = decision.Target; person.ActionData = "ecology:purchase";
                if (decision.Recipe.Length > 0) person.Plan.RemoveAll(step => step.Action is "gather" or "mining" && step.Data == decision.Definition);
            }
        }
        else if (error is null) error = Start(person, decision.Kind, decision.Target, "");
        if (error is not null) { RecordEcologyFailure(person, decision, commandId, error); return false; }
        person.Goal = decision.Goal; person.Reason = decision.Evidence; return true;
    }
}
