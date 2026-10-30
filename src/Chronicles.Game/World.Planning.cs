namespace Chronicles.Game;

public sealed partial class GameWorld
{
    // All fourteen dispositions have explicit contributions. Other characters' private values are never inputs.
    private static readonly int[][] GoalWeights =
    [
        [1, 1, 0, 4, 1, 0, 2, 1, 1, 0, 3, -1, 0, 0], // exploration
        [1, 2, 2, 3, 1, 0, -1, 1, 4, 0, 1, 3, 0, 0], // production
        [1, 0, 1, 1, 3, 2, 1, 3, 0, 2, 2, 1, 1, 2], // trade
        [0, 1, 3, 0, 3, 1, 0, 4, 2, 3, -1, 3, 2, 2], // organization
    ];

    private bool DiversePlan(Person p)
    {
        if (p.Plan.Count > 0)
        {
            var step = p.Plan[0];
            var failure = Start(p, step.Action, step.Target, step.Data);
            if (failure is null) { p.Plan.RemoveAt(0); return true; }
            Remember(p, "plan:lastFailure", failure, "실제 전제 재검사", 100);
            p.Plan.Clear();
        }
        if (Month % 12 != p.Id % 12) return false;
        p.PlanExpansions = 0;
        var choices = Enumerable.Range(0, 4).Select(goal => new
        {
            Goal = goal,
            Score = p.Personality.Select((value, axis) => value * GoalWeights[goal][axis]).Sum()
        }).OrderByDescending(x => x.Score).ThenBy(x => x.Goal);
        foreach (var choice in choices)
        {
            if (++p.PlanExpansions > Law("ai.planBudget")) break;
            switch (choice.Goal)
            {
                case 0:
                    p.Goal = "탐색·정보 획득"; p.Reason = "호기심·자율·위험 성향과 개인의 발견 지도";
                    if (p.KnownPlaces.Count < 2) return Start(p, "explore", 0, "") is null;
                    var candidates = state.Places[p.Place].Neighbors.Where(p.KnownPlaces.Contains).ToArray();
                    if (candidates.Length > 0)
                        return Start(p, "travel", candidates[(int)((p.Id + Month / 12) % candidates.Length)], "") is null;
                    break;
                case 1:
                    var recipes = Catalog.Recipes.Where(r => p.Understanding.GetValueOrDefault(r.Skill) > 0).ToArray();
                    if (recipes.Length == 0) break;
                    var recipe = recipes[(int)((p.Id + Month / 12) % recipes.Length)];
                    var plan = new List<PlanStep>(); var visiting = new HashSet<string>(StringComparer.Ordinal);
                    if (PlanRecipe(p, recipe, plan, visiting))
                    {
                        p.Goal = "공방·생산"; p.Reason = "자기 지식·재고·재료 획득→가공→제작의 제한 탐색";
                        p.Plan = plan;
                        var step = p.Plan[0];
                        if (Start(p, step.Action, step.Target, step.Data) is null) { p.Plan.RemoveAt(0); return true; }
                        p.Plan.Clear();
                    }
                    break;
                case 2:
                    var product = Owned(p.Id).FirstOrDefault(i => i.Reserved == 0 && i.Container == 0 &&
                        !p.Equipment.ContainsValue(i.Id) && Catalog.Items[i.Definition].Kind is ItemKind.Pill or ItemKind.Weapon or ItemKind.Talisman or ItemKind.Tool);
                    if (product is not null)
                    {
                        p.Goal = "교역·분업"; p.Reason = "자기 실물과 현지 시장·상재/협력 성향";
                        if (Offer(p, product.Id, 10 + p.Skills.GetValueOrDefault("trade") / 100, "sale") is null)
                        { p.Action = ActionKind.Guard; p.Due = Month + 1; return true; }
                    }
                    break;
                case 3:
                    if (Month - p.Born < Law("birth.maturity")) break;
                    p.Goal = "조직·공동체"; p.Reason = "가족·협력·야심·규율 성향과 자기 직책/재원";
                    if (p.Sect == 0 && Found(p, "sect") is null) return true;
                    if (groups.TryGetValue(p.Sect, out var g) && g.Leader == p.Id && g.Money >= Law("politics.policyCost"))
                    {
                        var policy = Catalog.PolicyNames[(int)((p.Id + Month / 12) % Catalog.PolicyNames.Count)];
                        if (Policy(p, g.Id, policy, (g.Policies[policy] + 1) % 3) is null) return true;
                    }
                    break;
            }
        }
        return false;
    }

    private bool PlanRecipe(Person p, Recipe recipe, List<PlanStep> plan, HashSet<string> visiting)
    {
        if (++p.PlanExpansions > Law("ai.planBudget") || !visiting.Add(recipe.Id)) return false;
        foreach (var (definition, quantity) in recipe.Inputs)
        {
            var missing = quantity - Inventory(p.Id, definition);
            if (missing <= 0) continue;
            if (definition is "herb" or "ore" or "wood")
            {
                var batches = (missing + IntLaw("economy.gatherYield") - 1) / IntLaw("economy.gatherYield");
                for (var i = 0; i < batches; i++) plan.Add(new("gather", 0, definition));
            }
            else
            {
                var precursor = Catalog.Recipes.FirstOrDefault(r => r.Output == definition && p.Understanding.GetValueOrDefault(r.Skill) > 0);
                if (precursor is null) return false;
                var batches = (missing + precursor.Quantity - 1) / precursor.Quantity;
                for (var i = 0; i < batches; i++) if (!PlanRecipe(p, precursor, plan, visiting)) return false;
            }
        }
        plan.Add(new("craft", 0, recipe.Id)); visiting.Remove(recipe.Id);
        return plan.Count <= Law("ai.planBudget");
    }
}
