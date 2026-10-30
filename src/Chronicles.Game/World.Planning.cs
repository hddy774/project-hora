using System.Globalization;
using Chronicles.Game.Inventory;
using Chronicles.Game.Knowledge;
using Chronicles.Game.Politics;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private sealed record IntelligentGoal(string Key, string Name, string Habit, int MoodWeight, int[] Weights);
    private static readonly IntelligentGoal[] IntelligentGoals =
    [
        new("production", "기예·생산", "Craft", 1, [2, 1, 2, 2, 2, 0, -1, 3, 3, 1, 2, 3, 1, 3]),
        new("information", "현장 정보 탐색", "Explore", 1, [1, 2, 0, 5, 0, 0, 3, 1, 2, 0, 3, 1, 0, 1]),
        new("trade", "교역·교섭", "Trade", 3, [2, 0, 2, 1, 3, 3, 1, 3, 2, 2, 1, 1, 1, 3]),
        new("learning", "지식·수련", "Study", 0, [2, 4, 2, 4, 3, 3, -1, 1, 4, 2, 0, 3, 1, 2]),
        new("cooperation", "약속·협력", "Teach", 2, [2, 1, 5, 0, 5, 4, -1, 0, 2, 4, -2, 2, 5, 0]),
        new("retreat", "위험 회피·후퇴", "Guard", -4, [5, 0, 4, 0, 0, -1, -5, 0, 2, 1, 1, 2, 2, -1]),
        new("politics", "공동체·정책", "Guard", 1, [2, 1, 3, 0, 3, 1, 0, 5, 2, 4, 3, 5, 2, 3]),
        new("cultivation", "구도·수행", "Cultivate", 0, [1, 5, 0, 2, 0, 0, 0, 3, 4, 2, 2, 4, 0, 3]),
        new("martial", "무예·자립", "Martial", -1, [3, 1, 0, 1, 0, 0, 4, 2, 3, 1, 2, 3, 0, 5])
    ];

    // During common hook rollout the legacy entry delegates once, never to the old omniscient recipe planner.
    private bool DiversePlan(Person p) => p.PlanExpansions == 0 && TryIntelligentGoal(p);

    private bool TryIntelligentGoal(Person p)
    {
        try { return RunIntelligentGoal(p); }
        finally { TrimPlanningCognition(p); }
    }

    private bool RunIntelligentGoal(Person p)
    {
        if (p.Action != ActionKind.Idle || p.Status != LifeStatus.Alive) return false;
        TrimPlanningCognition(p);
        if (p.Plan.Count > 0)
        {
            p.DecisionCandidates.Add(new("진행 중 목표", 0, "저장한 자기 계획의 실제 결과 영수증과 현재 선행 조건을 재검사"));
            if (ContinueIntelligentPlan(p)) return true;
        }
        var local = KnownPlanningSources(p).Where(s => s.Place == p.Place).ToArray();
        var offers = PlanningOffers(p);
        var ranked = IntelligentGoals.Select(goal => (Goal: goal, Score: IntelligentScore(p, goal, local, offers)))
            .OrderByDescending(c => c.Score).ThenBy(c => c.Goal.Key, StringComparer.Ordinal).ToArray();
        foreach (var candidate in ranked)
            p.DecisionCandidates.Add(new(candidate.Goal.Name, candidate.Score,
                $"고정 9목표 점수·14축 자기 성향·완료 습성 {p.Habits.GetValueOrDefault(candidate.Goal.Habit)}·감정 {p.Mood}"));
        foreach (var candidate in ranked)
        {
            if (!VisitPlanningNode(p)) break;
            var goal = candidate.Goal;
            if (candidate.Score < 0) continue;
            p.Goal = goal.Name;
            if (TryIntelligentOption(p, goal.Key, offers)) return true;
        }
        if (p.PlanExpansions >= Law("ai.planBudget"))
        {
            p.Reason = "이번 결정의 실제 탐색 예산을 소진하여 다음 판단까지 대기";
            return Start(p, "wait", 0, "판단 예산 회복") is null;
        }
        return false;
    }

    private long IntelligentScore(Person p, IntelligentGoal goal, SourceObservation[] sources,
        IReadOnlyList<AdvertisedInventoryOffer> offers)
    {
        long score = p.Personality.Select((value, axis) => value * goal.Weights[axis]).Sum() +
            p.Habits.GetValueOrDefault(goal.Habit) * 3L + p.Mood * goal.MoodWeight + PeoplePreference(p, goal.Key) +
            PeoplePhysiologyPreference(p, goal.Key);
        return score + (goal.Key switch
        {
            "production" => PlanningStock(p, "paper") == 0 || PlanningStock(p, "ink") == 0 ? 250 : 0,
            "information" => sources.Length == 0 ? 300 : p.KnownPlaces.Count < 2 ? 100 : 0,
            "trade" => CurrencyPlanningPriority(p) + (offers.Any(o => PlanningNeed(p, o.Definition) > 0 && o.Price <= p.Money) ? 600 :
                PlanningSurplus(p) is not null || HasEcologyPlanningSurplus(p) ? 400 : 0),
            "learning" => p.LessonRequests.Count > 0 ? 100 : 0,
            "cooperation" => p.LessonRequests.Any(r => r.Expires > Month && PlanningKnownPerson(p, r.Pupil)) ||
                state.Politics.Obligations.Any(o => o.Debtor == PoliticalAccount.Person(p.Id) && o.Outstanding > 0 && p.Money > 0) ? 700 : -10000,
            "retreat" => PlanningThreat(p) ? 3000 : -10000,
            "politics" => Month - p.Born >= PeopleMaturityMonths(p) && (p.Sect != 0 || p.Money >= Law("politics.foundCost")) ? 0 : -10000,
            "cultivation" => KnowledgeFor(p, "entry") is not null || Catalog.Paths.Any(path =>
                path.Realm == p.Realm && KnowledgeFor(p, path.Id) is not null) ? Law("ai.studyWeight") * 5 : -10000,
            _ => 0
        });
    }

    private bool TryIntelligentOption(Person p, string goal, IReadOnlyList<AdvertisedInventoryOffer> offers)
    {
        switch (goal)
        {
            case "production":
                foreach (var recipe in ProductionTargets(p))
                {
                    if (!VisitPlanningNode(p)) return false;
                    if (StartProductionPlan(p, recipe)) return true;
                }
                return false;
            case "information": return TryExplorationInformationGoal(p) || StartInformationPlan(p, "", "현장에 관한 개인 관측을 늘릴 필요");
            case "trade": return TryCurrencyPlanningGoal(p) || StartTradePlan(p, offers) || TryEcologyPlan(p);
            case "learning": return StartLearningPlan(p);
            case "cooperation": return StartCooperationPlan(p);
            case "retreat": return StartRetreatPlan(p);
            case "politics": return StartStewardshipPlan(p);
            case "cultivation":
                return StartCultivationPlan(p);
            case "martial":
                p.Reason = "자기 육신·경쟁/생존 성향·실제 단련 습성";
                return Start(p, "martial", 0, "") is null;
            default: return false;
        }
    }

    private IEnumerable<ProductionRecipe> ProductionTargets(Person p)
    {
        var priorities = new List<string>();
        if (PlanningStock(p, "tool") == 0) priorities.Add("tool");
        if (PlanningStock(p, "paper") < 2) priorities.Add("paper");
        if (PlanningStock(p, "ink") < 2) priorities.Add("ink");
        if (p.Health < 80 && PlanningStock(p, "pill") == 0) priorities.Add("pill");
        if (p.Realm >= Realm.GoldenCore && p.SoulPactUntil <= Month && PlanningStock(p, "soulWard") == 0) priorities.Add("soulWard");
        var recipes = InventoryDefinitions.Recipes.Values.OrderBy(r => r.Id, StringComparer.Ordinal).ToArray();
        var offset = (int)((p.Id + Month) % recipes.Length);
        priorities.AddRange(Enumerable.Range(0, Math.Min(4, recipes.Length)).Select(i => recipes[(offset + i) % recipes.Length].Id));
        return priorities.Distinct(StringComparer.Ordinal).Where(InventoryDefinitions.Recipes.ContainsKey)
            .Where(id => !PlanningBlocked(p, "recipe:" + id)).Select(id => InventoryDefinitions.Recipes[id]);
    }

    private bool StartProductionPlan(Person p, ProductionRecipe recipe)
    {
        if (PlanningBlocked(p, "recipe:" + recipe.Id))
        {
            p.Plan = [new("replan", 0, recipe.Id)];
            return StartInformationPlan(p, recipe.Output, "실제 실패 뒤 다른 정보를 확보하며 재시도 전제를 검토", preserveGoal: true);
        }
        var knowledge = KnowledgeFor(p, "recipe:" + recipe.Id);
        if (knowledge is null || knowledge.Comprehension < state.Knowledge.Rules.PracticeMinimumComprehension || knowledge.Belief == KnowledgeBelief.Contradicted)
            return RequestPlanningKnowledge(p, "recipe:" + recipe.Id, recipe.Id);
        var workspace = new ProductionWorkspace(p.Money, OwnPlanningItems(p).GroupBy(i => i.Definition)
            .ToDictionary(g => g.Key, g => g.Sum(i => (long)i.Quantity), StringComparer.Ordinal), KnownPlanningSources(p), PlanningOffers(p));
        if (!BuildProductionPlan(p, recipe, workspace, new(StringComparer.Ordinal)))
        {
            if (workspace.MissingKnowledge.Length > 0) return RequestPlanningKnowledge(p, workspace.MissingKnowledge, recipe.Id);
            if (workspace.UnknownMaterial.Length > 0)
            {
                p.Plan = [new("replan", 0, recipe.Id)];
                return StartInformationPlan(p, workspace.UnknownMaterial, "생산에 필요한 재료의 실제 위치가 아직 개인 기억에 없음", preserveGoal: true);
            }
            if (p.PlanExpansions >= Law("ai.planBudget"))
            {
                p.Plan = [new("replan", 0, recipe.Id)]; p.Reason = "목표를 보존하고 이번 결정의 제한 탐색을 다음으로 이월";
                return PausePlanning(p);
            }
            return false;
        }
        workspace.Steps.Add(new("finishGoal", 0, recipe.Id)); p.Plan = workspace.Steps;
        p.Goal = "생산·" + InventoryDefinitions.Definition(recipe.Output).Item.Name;
        p.Reason = "자신의 공정 지식·재고·관측한 원천/공개 제안으로 세운 다단계 계획";
        return ContinueIntelligentPlan(p);
    }

    private sealed class ProductionWorkspace(long money, Dictionary<string, long> stock,
        IReadOnlyList<SourceObservation> sources, IReadOnlyList<AdvertisedInventoryOffer> offers)
    {
        public long Money { get; set; } = money;
        public Dictionary<string, long> Stock { get; } = stock;
        public IReadOnlyList<SourceObservation> Sources { get; } = sources;
        public IReadOnlyList<AdvertisedInventoryOffer> Offers { get; } = offers;
        public Dictionary<long, long> SourceRemaining { get; } = sources.ToDictionary(s => s.Source, s => s.Remaining);
        public HashSet<long> PurchasedOffers { get; } = [];
        public List<PlanStep> Steps { get; } = [];
        public string UnknownMaterial { get; set; } = "";
        public string MissingKnowledge { get; set; } = "";
    }

    private bool BuildProductionPlan(Person p, ProductionRecipe recipe, ProductionWorkspace workspace, HashSet<string> visiting)
    {
        if (!visiting.Add(recipe.Id) || workspace.Steps.Count >= Law("ai.planBudget")) return false;
        foreach (var input in recipe.Inputs.OrderBy(i => i.Key, StringComparer.Ordinal))
        {
            if (!VisitPlanningNode(p) || !EnsurePlannedMaterial(p, input.Key, input.Value, workspace, visiting)) return false;
        }
        visiting.Remove(recipe.Id); workspace.Steps.Add(new("craft", 0, recipe.Id));
        workspace.Stock[recipe.Output] = workspace.Stock.GetValueOrDefault(recipe.Output) + recipe.Quantity;
        return workspace.Steps.Count <= Law("ai.planBudget");
    }

    private bool EnsurePlannedMaterial(Person p, string definition, int quantity, ProductionWorkspace workspace, HashSet<string> visiting)
    {
        var available = workspace.Stock.GetValueOrDefault(definition);
        if (available >= quantity) { workspace.Stock[definition] = available - quantity; return true; }
        var missing = quantity - available; workspace.Stock[definition] = 0;
        foreach (var source in workspace.Sources.Where(s => s.Place == p.Place && s.Definition == definition &&
            workspace.SourceRemaining[s.Source] > 0 && !PlanningBlocked(p, "source:" + s.Source)).OrderBy(s => s.Source))
        {
            while (missing > 0 && workspace.SourceRemaining[source.Source] > 0)
            {
                if (!VisitPlanningNode(p) || workspace.Steps.Count >= Law("ai.planBudget")) return false;
                var yield = Math.Min(workspace.SourceRemaining[source.Source], Law("economy.gatherYield"));
                workspace.Steps.Add(new("gather", source.Source, definition));
                workspace.SourceRemaining[source.Source] -= yield; missing -= yield;
            }
            if (missing <= 0) { workspace.Stock[definition] = -missing; return true; }
        }
        foreach (var offer in workspace.Offers.Where(o => o.Definition == definition && o.Kind == "sale" && o.Price <= workspace.Money &&
            !workspace.PurchasedOffers.Contains(o.Contract) && !PlanningBlocked(p, "offer:" + o.Contract)).OrderBy(o => o.Price).ThenBy(o => o.Contract))
        {
            if (!VisitPlanningNode(p)) return false;
            workspace.Steps.Add(new("trade", offer.Contract, "")); workspace.PurchasedOffers.Add(offer.Contract);
            workspace.Money -= offer.Price; missing -= offer.Quantity;
            if (missing <= 0) { workspace.Stock[definition] = -missing; return true; }
        }
        foreach (var precursor in InventoryDefinitions.Recipes.Values.Where(r => r.Output == definition && !visiting.Contains(r.Id)).OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            if (!VisitPlanningNode(p)) return false;
            var knowledge = KnowledgeFor(p, "recipe:" + precursor.Id);
            if (knowledge is null || knowledge.Comprehension < state.Knowledge.Rules.PracticeMinimumComprehension || knowledge.Belief == KnowledgeBelief.Contradicted)
            { workspace.MissingKnowledge = "recipe:" + precursor.Id; continue; }
            while (missing > 0)
            {
                if (!BuildProductionPlan(p, precursor, workspace, visiting)) return false;
                var made = Math.Min(missing, workspace.Stock.GetValueOrDefault(definition));
                workspace.Stock[definition] -= made; missing -= made;
            }
            return true;
        }
        workspace.UnknownMaterial = definition; return false;
    }

    private bool VisitPlanningNode(Person p)
    {
        if (p.PlanExpansions >= Law("ai.planBudget")) return false;
        p.PlanExpansions++; return true;
    }

    private SourceObservation[] KnownPlanningSources(Person p) => InventoryKnowledgeQueries.KnownSources(state.Inventory, p.Id)
        .Where(s => s.Month <= Month && Month - s.Month < Law("ai.beliefLifetime")).ToArray();
    private AdvertisedInventoryOffer[] PlanningOffers(Person p) => InventoryKnowledgeQueries.LocalOffers(
        new InventoryState { Month = Month, People = [p], Ledger = state.Inventory }, p.Id).Where(o => o.Seller != p.Id).ToArray();
    private IEnumerable<Item> OwnPlanningItems(Person p) => Owned(p.Id).Where(i => i.Reserved == 0 && (i.Place == -1 || i.Place == p.Place) &&
        ExplorationCarriedItemAccessible(p, i) &&
        (i.BoundSoul == 0 || i.BoundSoul == p.SoulId) && !state.Inventory.Metadata.Any(m => m.Item == i.Id &&
            (m.Carrier > 0 && m.Carrier != p.Id || m.Custodian > 0 && m.Custodian != p.Id)));
    private long PlanningStock(Person p, string definition) => OwnPlanningItems(p).Where(i => i.Definition == definition).Sum(i => (long)i.Quantity);
    private bool HasEcologyPlanningSurplus(Person p)
    {
        var own = OwnPlanningItems(p).Where(i => !p.Equipment.ContainsValue(i.Id)).ToArray();
        return own.Any(item => item.Container == 0 && item.BoundSoul == 0 && item.Definition is "food" or "herb" or "ore" or "wood" &&
            !state.Inventory.Arrays.Any(a => a.Item == item.Id) &&
            own.Where(i => i.Definition == item.Definition).Sum(i => (long)i.Quantity) > (item.Definition == "food" ? EcologyFoodTarget(p) : 2) &&
            !state.Inventory.Contracts.Any(c => c.Seller == p.Id && c.Status == "open" && c.Due > Month && c.AdvertisedDefinition == item.Definition));
    }
    private long PlanningNeed(Person p, string definition) => Math.Max(0, (definition switch
    {
        "food" => EcologyFoodTarget(p), "tool" or "armor" or "weapon" or "bag" or "pill" or "soulWard" => 1,
        "paper" or "ink" => 2, "herb" or "ore" or "wood" => 4, _ => 1
    }) - PlanningStock(p, definition));
    private bool PlanningKnownPerson(Person p, long other) => p.KnownPeople.Contains(other) && p.ObservedPeople.TryGetValue(other, out var seen) &&
        seen.Place == p.Place && seen.Month <= Month && Month - seen.Month < Law("ai.beliefLifetime");
    private bool PlanningBlocked(Person p, string key) => p.Beliefs.TryGetValue("plan:blocked:" + key, out var failure) && failure.Expires > Month;
    private static string PlanningNumber(long value) => value.ToString(CultureInfo.InvariantCulture);
}
