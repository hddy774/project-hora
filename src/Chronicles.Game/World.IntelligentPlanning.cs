using System.Text.Json;
using Chronicles.Game.Inventory;
using Chronicles.Game.Knowledge;
using Chronicles.Game.Politics;

namespace Chronicles.Game;

public sealed partial class GameWorld
{
    private sealed record PlanningReceipt(string Kind, long Id, string Work, string Definition, long Started, long Due, string Source);

    private bool ContinueIntelligentPlan(Person p)
    {
        while (p.Plan.Count > 0)
        {
            if (!VisitPlanningNode(p))
            { p.Reason = "지속 중인 계획의 다음 검사를 다음 결정으로 이월"; return Start(p, "wait", 0, "계획 검토") is null; }
            var step = p.Plan[0];
            if (step.Action == "replan")
            {
                p.Plan.RemoveAt(0);
                return InventoryDefinitions.Recipes.TryGetValue(step.Data, out var recipe) && StartProductionPlan(p, recipe);
            }
            if (step.Action == "awaitKnowledge")
            {
                var learned = KnowledgeFor(p, step.Data);
                if (learned is not null && learned.Comprehension >= state.Knowledge.Rules.PracticeMinimumComprehension && learned.Belief != KnowledgeBelief.Contradicted)
                { p.Plan.RemoveAt(0); continue; }
                if (!p.Beliefs.TryGetValue("plan:request:" + step.Data, out var request) || request.Expires <= Month)
                { PlanningFailure(p, step, "요청 기억이 없거나 응답 기한이 지남·상대의 지식은 아직 모름", "요청 기록/기한"); return PausePlanning(p); }
                p.Reason = "실제로 전달한 배움 요청·상대가 독립적으로 선택하는 전수 응답을 기다림";
                return Start(p, "wait", 0, "배움 응답 대기") is null;
            }
            if (step.Action == "confirm")
            {
                var confirmation = JsonSerializer.Deserialize<PlanningReceipt>(step.Data) ?? throw new InvalidDataException("계획 결과 출처가 없습니다.");
                if (confirmation.Kind == "market" && Month < confirmation.Due)
                { p.Reason = "자기가 실제 입찰한 공개 경매의 정산 기한을 기다림"; return PausePlanning(p); }
                if (!ConfirmPlanningReceipt(p, confirmation, out var failure))
                { PlanningFailure(p, step, failure, confirmation.Source); return PausePlanning(p); }
                p.Plan.RemoveAt(0);
                Remember(p, "plan:lastSuccess", confirmation.Definition, confirmation.Source, 100);
                if (confirmation.Kind == "market") p.Habits["Trade"] = Math.Min(100, p.Habits.GetValueOrDefault("Trade") + 1);
                continue;
            }
            if (step.Action == "finishGoal")
            {
                p.Plan.RemoveAt(0);
                Remember(p, "plan:completed", step.Data, p.Beliefs.GetValueOrDefault("plan:lastSuccess")?.Source ?? "실제 계획 결과", 100);
                p.Reason = "예상 효과가 아닌 실제 공정 결과를 확인하여 생산 목표 완료";
                return PausePlanning(p);
            }
            var started = Month;
            var error = ExecutePlanningStep(p, step, out var source);
            if (error is not null)
            { PlanningFailure(p, step, error, source); return PausePlanning(p); }
            p.Plan.RemoveAt(0);
            if (step.Action == "requestTeaching")
            {
                Remember(p, "plan:request:" + step.Data, PlanningNumber(step.Target), source, 100);
                if (p.Beliefs.TryGetValue("plan:request:" + step.Data, out var fact))
                    p.Beliefs[fact.Key] = fact with { Expires = Month + Math.Min(12, Law("ai.beliefLifetime")) };
            }
            var job = state.Inventory.Jobs.Find(j => j.Actor == p.Id && j.Id == p.ActionTarget && j.Status is "reserved" or "processing");
            PlanningReceipt? receipt = job is not null ? new("inventory", job.Id, job.Recipe, step.Data, started, job.Due, "inventory:job:" + job.Id) : null;
            if (receipt is null && TryReadKnowledgeAction(p, out var context))
                receipt = new("knowledge", context.Activity, context.Work, step.Data, started, p.Due, "knowledge:activity:" + context.Activity);
            if (receipt is null && step.Action is "trade" or "bid") receipt = new("market", step.Target, "", step.Data, started,
                step.Action == "bid" ? state.Inventory.Contracts.Single(c => c.Id == step.Target).Due : Month + 1, source);
            if (receipt is not null) p.Plan.Insert(0, new("confirm", receipt.Id, JsonSerializer.Serialize(receipt)));
            if (p.Action == ActionKind.Idle) return PausePlanning(p);
            return true;
        }
        return false;
    }

    private bool ConfirmPlanningReceipt(Person p, PlanningReceipt receipt, out string failure)
    {
        failure = "실제 결과를 확인할 수 없음";
        if (receipt.Kind == "inventory")
        {
            var job = state.Inventory.Jobs.Find(j => j.Id == receipt.Id && j.Actor == p.Id);
            if (job?.Status != "completed")
            {
                failure = job?.Status == "failed" ? "실제 공정 실패·이미 소비한 재료는 복제하지 않음" : "실제 공정의 취소 또는 중단";
                if (job?.Source > 0) BlockPlanningFact(p, "source:" + job.Source, failure, receipt.Source);
                if (job is not null && job.Source == 0) BlockPlanningFact(p, "recipe:" + job.Recipe, failure, receipt.Source);
                return false;
            }
            if (job.Source > 0)
            {
                var actual = Owned(p.Id).FirstOrDefault(i => i.Source == $"extraction:{job.Source}:job:{job.Id}");
                if (actual is not null && receipt.Definition.Length > 0 && actual.Definition != receipt.Definition)
                {
                    state.Inventory.Observations.RemoveAll(o => o.Observer == p.Id && o.Source == job.Source);
                    failure = "예상한 재료와 실제 추출 결과의 품목이 달라 원천 주장을 다시 관측해야 함";
                    BlockPlanningFact(p, "source:" + job.Source, failure, receipt.Source); return false;
                }
            }
            if (job.Source > 0) p.Beliefs.Remove("plan:blocked:source:" + job.Source);
            p.Beliefs.Remove("plan:blocked:recipe:" + job.Recipe);
            return true;
        }
        if (receipt.Kind == "knowledge")
        {
            var activity = state.Knowledge.Activities.Find(a => a.Id == receipt.Id && (a.ActorId == p.Id || a.TeacherId == p.Id));
            if (activity?.Status == KnowledgeActivityStatus.Completed) return true;
            failure = "실제 전수·독해 행동의 접근/노동 조건이 중단됨"; return false;
        }
        if (receipt.Kind == "market")
        {
            var contract = state.Inventory.Contracts.Find(c => c.Id == receipt.Id && (c.Buyer == p.Id || c.Seller == p.Id));
            if (contract?.Status == "completed" && contract.Buyer == p.Id) return true;
            failure = contract?.Status == "open" ? "경매가 아직 정산되지 않음" : "실제 공개 거래가 성립하지 않았거나 낙찰되지 않음";
            BlockPlanningFact(p, "offer:" + receipt.Id, failure, receipt.Source); return false;
        }
        return Month >= receipt.Due;
    }

    private string? ExecutePlanningStep(Person p, PlanStep step, out string source)
    {
        source = $"plan:{p.BodyId}:{Month}:{p.Decisions.Count}:{p.PlanExpansions}:{step.Action}";
        var command = new GameCommand(source, p.Id, step.Action, step.Target, Data: step.Data);
        if (step.Action == "offer")
        {
            if (!items.TryGetValue(step.Target, out var item) || item.Owner != p.Id) return "자기 실제 출품 재고가 바뀜";
            command = command with { Value = Math.Max(1, (long)item.Quantity * (5 + p.Skills.GetValueOrDefault("trade") / 1000)) };
        }
        if (step.Action is "split" or "bid" or "repayDebt")
        {
            if (!long.TryParse(step.Data, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var amount) || amount <= 0)
                return "실제 수량/입찰 금액이 유효하지 않음";
            command = command with { Value = amount, Data = "" };
        }
        if (PeopleActionError(p, command.Kind) is { } capabilityError) return capabilityError;
        if (TryCultivationCommand(command, p, out var error) || TryKnowledgeCommand(command, p, out error) ||
            TryInventoryCommand(command, p, out error) || TryPoliticsCommand(command, p, out error)) return error;
        return Start(p, step.Action, step.Target, step.Data);
    }

    private bool RequestPlanningKnowledge(Person p, string work, string targetRecipe)
    {
        var own = KnowledgeFor(p, work);
        if (own?.Belief == KnowledgeBelief.Contradicted)
        {
            var book = state.Knowledge.Media.Where(m => !m.Destroyed && !m.Unreadable &&
                state.Knowledge.Editions.Any(e => e.Id == m.EditionId && e.WorkId == work) && CanConsultKnowledge(p, work, m.EditionId))
                .OrderBy(m => m.ItemId).FirstOrDefault();
            if (book is not null)
            {
                p.Plan = [new("correctKnowledge", book.ItemId, ""), new("replan", 0, targetRecipe)];
                p.Reason = "직접 검증으로 반박된 개인 이해를 실제 접근 매체로 다시 확인";
                return ContinueIntelligentPlan(p);
            }
        }
        var teacher = p.ObservedPeople.Values.Where(o => o.Id != p.Id && PlanningKnownPerson(p, o.Id) &&
            !PlanningBlocked(p, "teacher:" + o.Id + ":" + work)).OrderByDescending(o =>
                state.Relations.Where(r => r.Observer == p.Id && r.Other == o.Id).Select(r => r.Trust + r.Respect).DefaultIfEmpty().Max()).ThenBy(o => o.Id).FirstOrDefault();
        if (teacher.Id == 0) return StartInformationPlan(p, work, "아직 직접 만난 전수 상대가 없어 실제 만남을 탐색");
        p.Plan = [new("requestTeaching", teacher.Id, work), new("awaitKnowledge", teacher.Id, work), new("replan", 0, targetRecipe)];
        p.Goal = "배움·생산 준비"; p.Reason = "관측한 상대에게 실제 요청·상대의 숨은 지식이나 동의를 가정하지 않음";
        return ContinueIntelligentPlan(p);
    }

    private bool StartInformationPlan(Person p, string subject, string reason, bool preserveGoal = false)
    {
        var explored = p.Beliefs.TryGetValue("plan:explored:" + p.Place, out var fact) && fact.Expires > Month;
        PlanStep step;
        if (!explored) step = new("exploreSource", 0, "");
        else
        {
            var next = KnownPlanningNeighbors(p).FirstOrDefault(-1);
            if (next < 0) step = new("explore", 0, "");
            else step = new("travel", next, "");
        }
        if (!preserveGoal) p.Plan.Clear();
        var label = InventoryDefinitions.Items.TryGetValue(subject, out var definition) ? definition.Item.Name :
            state.Knowledge.Works.FirstOrDefault(w => w.Id == subject)?.Title ?? "현장";
        p.Plan.Insert(0, step); p.Goal = "정보·" + label;
        p.Reason = reason + "·알 수 없는 원천은 일반 현장 탐색 명령으로 확인";
        return ContinueIntelligentPlan(p);
    }

    private bool StartTradePlan(Person p, IReadOnlyList<AdvertisedInventoryOffer> offers)
    {
        var offer = offers.Where(o => o.Kind == "sale" && o.Price <= p.Money && PlanningNeed(p, o.Definition) > 0 &&
            !PlanningBlocked(p, "offer:" + o.Contract)).OrderByDescending(o => PlanningNeed(p, o.Definition)).ThenBy(o => o.Price).ThenBy(o => o.Contract).FirstOrDefault();
        if (offer is not null)
        {
            p.Plan = [new("trade", offer.Contract, "")]; p.Reason = "현지 공개 출품의 표시 수량/가격과 자기 수요·실제 돈만 비교";
            return ContinueIntelligentPlan(p);
        }
        var auction = offers.Where(o => o.Kind == "auction" && PlanningNeed(p, o.Definition) > 0 &&
            o.MinimumBid is { } minimum && minimum <= p.Money + o.OwnDeposit && !PlanningBlocked(p, "offer:" + o.Contract))
            .OrderBy(o => o.MinimumBid).ThenBy(o => o.Contract).FirstOrDefault();
        if (auction is not null && p.Personality[8] + p.Personality[5] >= p.Personality[10])
        {
            p.Plan = auction.OwnDeposit > 0 ? [new("confirm", auction.Contract, JsonSerializer.Serialize(
                new PlanningReceipt("market", auction.Contract, "", auction.Definition, Month, auction.Due, "공개 입찰 영수증")))] :
                [new("bid", auction.Contract, PlanningNumber(auction.MinimumBid!.Value))];
            p.Reason = "공개 최저 입찰액·자기 실제 예치/잔액·기다림 성향으로 입찰 선택";
            return ContinueIntelligentPlan(p);
        }
        var product = PlanningSurplus(p);
        if (product is null) return false;
        var surplus = PlanningStock(p, product.Definition) - 1;
        if (product.Quantity > surplus)
        {
            if (!VisitPlanningNode(p)) return false;
            var error = ExecutePlanningStep(p, new("split", product.Id, PlanningNumber(surplus)), out var source);
            if (error is not null) { PlanningFailure(p, new("split", product.Id, product.Definition), error, source); return PausePlanning(p); }
            if (!state.Inventory.Receipts.TryGetValue(source, out var split) || !items.TryGetValue(split.Subject, out product) || product.Owner != p.Id)
                throw new InvalidDataException("실제 잉여 분할 결과가 없습니다.");
        }
        p.Plan = [new("offer", product.Id, "sale")]; p.Reason = "자기 실제 잉여 실물을 공개 제안·매입자나 거래 성사를 생성하지 않음";
        return ContinueIntelligentPlan(p);
    }

    private Item? PlanningSurplus(Person p) => OwnPlanningItems(p).Where(i => i.Container == 0 && !p.Equipment.ContainsValue(i.Id) &&
        InventoryDefinitions.Definition(i.Definition).Item.Kind is ItemKind.Pill or ItemKind.Weapon or ItemKind.Armor or ItemKind.Talisman or ItemKind.Tool &&
        PlanningStock(p, i.Definition) > 1).OrderBy(i => i.Id).FirstOrDefault();

    private bool StartLearningPlan(Person p)
    {
        var memory = state.Knowledge.Memories.Where(m => m.PersonId == p.Id && m.Chapter == 1)
            .GroupBy(m => m.WorkId, StringComparer.Ordinal).Select(g => g.MaxBy(m => m.Revision)!)
            .Where(m => m.Comprehension < 100 && state.Knowledge.Media.Any(book => book.EditionId == m.EditionId && CanConsultKnowledge(p, m.WorkId, book.EditionId)))
            .OrderBy(m => m.Comprehension).ThenBy(m => m.WorkId, StringComparer.Ordinal).FirstOrDefault();
        if (memory is null) return false;
        var medium = state.Knowledge.Media.Where(m => m.EditionId == memory.EditionId && CanConsultKnowledge(p, memory.WorkId, m.EditionId))
            .OrderBy(m => m.ItemId).FirstOrDefault();
        if (medium is null) return false;
        p.Plan = [new(memory.Belief == KnowledgeBelief.Contradicted ? "correctKnowledge" : "study", medium.ItemId, "")];
        p.Reason = "실제로 접근 가능한 자기 지식 출처를 읽어 이해를 높임·실전 숙련과 분리";
        return ContinueIntelligentPlan(p);
    }

    private bool StartCooperationPlan(Person p)
    {
        var debt = state.Politics.Obligations.Where(o => o.Debtor == PoliticalAccount.Person(p.Id) && o.Outstanding > 0 &&
            (o.Creditor.Kind == PoliticalAccountKind.Person ? PlanningKnownPerson(p, o.Creditor.Id) : o.Creditor.Id == p.Household || o.Creditor.Id == p.Sect))
            .OrderBy(o => o.Created).ThenBy(o => o.Id).FirstOrDefault();
        if (debt is not null && p.Money > 0)
        {
            p.Plan = [new("repayDebt", debt.Id, PlanningNumber(Math.Min(p.Money, debt.Outstanding)))];
            p.Reason = "자기가 실제로 진 채무와 알려진 상대·자기 잔액으로 약속 이행 선택";
            return ContinueIntelligentPlan(p);
        }
        var request = p.LessonRequests.Where(r => r.Expires > Month && PlanningKnownPerson(p, r.Pupil) &&
            KnowledgeFor(p, r.Knowledge) is { Comprehension: > 0 }).OrderBy(r => r.Requested).ThenBy(r => r.Pupil).FirstOrDefault();
        if (request is null) return false;
        p.Plan = [new("teach", request.Pupil, request.Knowledge)];
        p.Reason = "직접 받은 배움 요청과 자기 지식·상호 노동 조건을 확인하여 협력 선택";
        return ContinueIntelligentPlan(p);
    }

    private bool PlanningThreat(Person p) => p.Journal.Any(e => e.Kind == "전투" && e.Actor != p.Id && e.Target == p.Id &&
        e.Month <= Month && Month - e.Month <= 3 && PlanningKnownPerson(p, e.Actor));
    private IEnumerable<int> KnownPlanningNeighbors(Person p) => p.KnownPlaces.Where(place => place != p.Place &&
        Math.Abs(place / 10 - p.Place / 10) + Math.Abs(place % 10 - p.Place % 10) == 1 &&
        !PlanningBlocked(p, "place:" + place) && (!p.ObservedPlaces.TryGetValue(place, out var seen) || seen.BlockedUntil <= Month))
        .OrderByDescending(place => p.ObservedPlaces.TryGetValue(place, out var seen) ? seen.Source : 0).ThenBy(place => place);
    private bool StartRetreatPlan(Person p)
    {
        if (!PlanningThreat(p)) return false;
        var next = KnownPlanningNeighbors(p).FirstOrDefault(-1);
        p.Plan = [next >= 0 ? new("travel", next, "") : new("guard", 0, "")];
        p.Reason = next >= 0 ? "자기에게 일어난 적대 사건과 아는 길로 후퇴" : "안전한 길을 아직 몰라 현 위치 방어·자동 공격 없음";
        return ContinueIntelligentPlan(p);
    }

    private bool StartStewardshipPlan(Person p)
    {
        var own = new[] { p.Sect, p.Household }.Where(id => id > 0).Select(id => groups.GetValueOrDefault(id))
            .FirstOrDefault(g => g is not null && g.Leader == p.Id && !g.Dissolved && g.Money >= Law("politics.policyCost"));
        if (own is not null)
        {
            var category = p.Personality[2] + p.Personality[12] >= p.Personality[7] + p.Personality[13] ? "민생·구휼" :
                p.Personality[1] + p.Personality[3] >= p.Personality[11] ? "교육·전승" : "기예·산업";
            p.Plan = [new("policy", own.Id, category)];
        }
        else if (p.Sect == 0 && p.Money >= Law("politics.foundCost") && Month - p.Born >= Law("birth.maturity"))
            p.Plan = [new("found", 0, "sect")];
        else return false;
        p.Reason = "자기 실제 직책·금고/개인 재원과 성향으로 공공 작업 제안·권한은 공통 명령에서 재검사";
        return ContinueIntelligentPlan(p);
    }

    private bool PausePlanning(Person p) => p.Action != ActionKind.Idle || Start(p, "wait", 0, "실제 계획 결과 검토") is null;
    private void PlanningFailure(Person p, PlanStep step, string failure, string source)
    {
        var target = p.Plan.LastOrDefault(s => s.Action is "replan" or "finishGoal")?.Data;
        var key = step.Action switch { "gather" => "source:" + step.Target, "travel" => "place:" + step.Target,
            "craft" => "recipe:" + step.Data, "trade" or "bid" => "offer:" + step.Target,
            "awaitKnowledge" or "requestTeaching" => "teacher:" + step.Target + ":" + step.Data, _ => "action:" + step.Action };
        BlockPlanningFact(p, key, failure, source);
        if (step.Action == "gather") state.Inventory.Observations.RemoveAll(o => o.Observer == p.Id && o.Source == step.Target);
        if (step.Action == "exploreSource") Remember(p, "plan:explored:" + p.Place, "일반 현장 탐색이 더 찾지 못함", source + "·" + failure, 100);
        Remember(p, "plan:lastFailure", failure, source, 100);
        p.Mood = Math.Clamp(p.Mood - 2, -100, 100);
        p.Reason = "실제 실행 결과로 계획 전제를 수정: " + failure;
        p.Plan = target is not null ? [new("replan", 0, target)] : [];
    }
    private void BlockPlanningFact(Person p, string key, string failure, string source)
    {
        Remember(p, "plan:blocked:" + key, failure, source, 100);
        if (p.Beliefs.TryGetValue("plan:blocked:" + key, out var fact)) p.Beliefs[fact.Key] = fact with
        { Expires = Month + Math.Min(12, Law("ai.beliefLifetime")) };
    }

    private void TrimPlanningCognition(Person p)
    {
        TrimPeopleCognition(p);
        var lifetime = Law("ai.beliefLifetime");
        var latest = state.Inventory.Observations.Select((o, i) => (Observation: o, Index: i)).Where(row => row.Observation.Observer == p.Id)
            .GroupBy(row => row.Observation.Source).Select(g => g.OrderByDescending(row => row.Observation.Month).ThenByDescending(row => row.Index).First().Observation)
            .Where(o => o.Month <= Month && Month - o.Month < lifetime).ToArray();
        state.Inventory.Observations.RemoveAll(o => o.Observer == p.Id);
        state.Inventory.Observations.AddRange(latest);
        foreach (var key in p.Beliefs.Where(kv => kv.Value.Expires <= Month).Select(kv => kv.Key).ToArray()) p.Beliefs.Remove(key);
        var rows = p.Beliefs.Values.Select(b => (Kind: 0, Key: b.Key, Month: b.Observed, Priority: b.Confidence))
            .Concat(p.ObservedPeople.Values.Select(o => (Kind: 1, Key: PlanningNumber(o.Id), Month: o.Month, Priority: 40)))
            .Concat(p.ObservedPlaces.Values.Select(o => (Kind: 2, Key: PlanningNumber(o.Place), Month: o.Month, Priority: o.Place == p.Place ? 100 : 60)))
            .Concat(latest.Select(o => (Kind: 3, Key: PlanningNumber(o.Source), Month: o.Month, Priority: 70)))
            .OrderByDescending(row => row.Month).ThenByDescending(row => row.Priority).ThenBy(row => row.Kind).ThenBy(row => row.Key, StringComparer.Ordinal).ToArray();
        foreach (var row in rows.Skip(Math.Max(0, IntLaw("ai.beliefBudget") - PeopleCognitiveLoad(p))))
            switch (row.Kind)
            {
                case 0: p.Beliefs.Remove(row.Key); break;
                case 1: p.ObservedPeople.Remove(long.Parse(row.Key, System.Globalization.CultureInfo.InvariantCulture)); break;
                case 2: p.ObservedPlaces.Remove(int.Parse(row.Key, System.Globalization.CultureInfo.InvariantCulture)); break;
                case 3: state.Inventory.Observations.RemoveAll(o => o.Observer == p.Id && PlanningNumber(o.Source) == row.Key); break;
            }
    }
}
