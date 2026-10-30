using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Chronicles.Game.Inventory;
using Chronicles.Game.Knowledge;

namespace Chronicles.Game.Tests;

// Opt-in measurement collector. Feature discovery never runs this class.
internal static class AgentMeasurements
{
    private static readonly JsonSerializerOptions Json = new()
    { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    private static readonly int[] CooperativeAxes = [2, 4, 5, 9, 12];
    private static readonly string[] EcologyOverrides = ["ai.planBudget", "ai.beliefBudget"];

    internal sealed class MeasurementPlan
    {
        public string SourceCommit { get; init; } = "";
        public ulong[] Seeds { get; init; } = [42];
        public int HorizonMonths { get; init; } = 240;
        public int Repetitions { get; init; } = 3;
        public int WarmupMonths { get; init; } = 2;
        public int[] PlanningBudgets { get; init; } = [1, 8, 64];
        public int BeliefBudget { get; init; } = 64;
        public int AxisDelta { get; init; } = 1;
        public int StagnationMonths { get; init; } = 12;
        public int ReplayEveryMonths { get; init; } = 12;
        public int PreparationLimitMonths { get; init; } = 120;
    }

    private sealed record Case(string Family, ulong Seed, int Budget, int Repetition, int? Axis = null);
    private sealed record PublicStep(long Month, GameCommand Command, bool Accepted, string Message, string AfterHash);
    private sealed record Prepared(GameWorld World, long Actor, object Declaration, List<PublicStep> Preparation,
        long StaleSource = 0, GameWorld? HiddenAlternative = null);
    private sealed record DecisionSample(long Person, DecisionRecord Decision);
    private sealed record LatencySample(long Month, long AdvanceTicks, long AdvanceAllocatedBytes,
        int NewDecisions, int Expansions, int ActualBudget);
    private sealed record GoalReceipt(long Person, string Goal, long Month, string Evidence);
    private sealed record FailureReceipt(long Person, long Month, string Reason, string Evidence);
    private sealed record PaidJob(long Id, long Actor, string Recipe, string Status, long Source,
        long Due, string Output, int OutputQuantity, IReadOnlyList<ConsumedMaterial> Consumed, IReadOnlyList<string> Provenance);
    private sealed record Snapshot(long Month, string Hash, int Living, int Decisions, int PaidCompletions,
        int Goals, int KnownSources, int Waiting, int ActivePaidProcesses);
    private sealed record AxisDelta(string Goal, long Baseline, long Changed, long Difference);

    public static void RunArguments(string[] args)
    {
        string Value(string option)
        {
            var index = Array.IndexOf(args, option);
            if (index < 0 || index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException(option + " 뒤에 경로를 지정하세요.", nameof(args));
            return args[index + 1];
        }
        Run(Value("--agent-metrics"), Value("--agent-metrics-plan"));
    }

    public static void Run(string path, string planPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(planPath);
        var planBytes = File.ReadAllBytes(planPath);
        var plan = JsonSerializer.Deserialize<MeasurementPlan>(planBytes, Json) ?? throw new InvalidDataException("AI 측정 계획이 없습니다.");
        ValidatePlan(plan);
        var productVersion = VersionOf(typeof(GameWorld).Assembly);
        var harnessVersion = VersionOf(typeof(AgentMeasurements).Assembly);
        if (!productVersion.EndsWith(plan.SourceCommit, StringComparison.OrdinalIgnoreCase) ||
            !harnessVersion.EndsWith(plan.SourceCommit, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("계획 sourceCommit과 제품/측정기 빌드의 SourceRevisionId가 일치해야 합니다.");
        var reports = new List<object>();
        var axes = new List<object>();
        var baselineScores = new Dictionary<(ulong Seed, int Repetition), Dictionary<string, long>>();
        var errors = 0;
        var runtime = new { framework = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
            processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(), processors = Environment.ProcessorCount,
            culture = CultureInfo.CurrentCulture.Name, stopwatchFrequency = Stopwatch.Frequency,
            productVersion, harnessVersion, frameworkTarget = AppContext.TargetFrameworkName };
        var notes = new[]
        {
            "기존 공개 AgentPlanning 시나리오를 확장한 최종 측정용 수집기. 측정 결과 자체는 전체 기능/출시 수용 판정이 아님.",
            "advanceTicks/할당은 AI를 포함한 공개 월 경계 전체 비용이다. 순수 추론 지연은 분리하지 않으며 실제 탐색량은 DecisionRecord.Expansions로 기록한다.",
            "인과 대조는 한 축만 바꾼 실제 후보 점수이며 선택 행동이 달라지지 않은 결과도 보존한다. 지배/정체는 관측 분포이지 최적 전략이나 무한 교착의 증명이 아님.",
            "고정 조건과 기본 생태계는 분리한다. 무료 Stock, 무료 지식/숙련, 합성 인구, 성공까지 초기조건 재추첨을 사용하지 않는다.",
            "완수하지 못한 목표·교정하지 못한 기억·예산 소진·사망도 고정 horizon 결과에 남긴다. 성공할 때까지 horizon이나 법칙을 바꾸지 않는다.",
            "실패 분포는 매 월 자기 plan:lastFailure 기억에서 수집한 중복 제거 영수증이다. 같은 월 안에서 덮어써진 내부 실패 전체를 계측했다고 주장하지 않는다.",
            "자원은 품목별 실제 공정 소비/출력, 노동은 실제 월 구간의 점유로 제시한다. 다른 품목 수량을 가치 효율 한 점수로 합치지 않는다.",
            "숨은 비교 세계는 다른 두 인물 사이의 기존 자금17만 달라지며 관측 대상의 정보/선택을 비교한다. 모든 숨은 사실 종류의 공정성을 증명하지 않는다."
        };
        void Save(string status)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
            Directory.CreateDirectory(directory);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { schemaVersion = 1, kind = "seonro-agent-measurements",
                executionStatus = status, sourceCommit = plan.SourceCommit, runtime, plan,
                planSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(planBytes)).ToLowerInvariant(),
                notes, failedCases = errors, axisCausalComparisons = axes, reports }, Json));
            File.Move(temporary, path, true);
        }
        Save("running");
        foreach (var specification in Cases(plan))
        {
            try
            {
                var prepared = Prepare(specification, plan);
                var result = Measure(specification, plan, prepared);
                reports.Add(result.Report);
                if (!result.ContractChecksPassed) errors++;
                if (specification.Family == "axes" && specification.Axis is null)
                    baselineScores.Add((specification.Seed, specification.Repetition), result.FirstScores);
                if (specification.Axis is { } axis)
                {
                    var baseline = baselineScores[(specification.Seed, specification.Repetition)];
                    var deltas = result.FirstScores.Where(kv => baseline.ContainsKey(kv.Key)).Select(kv =>
                        new AxisDelta(kv.Key, baseline[kv.Key], kv.Value, kv.Value - baseline[kv.Key])).ToArray();
                    axes.Add(new { specification.Seed, specification.Repetition, axis, name = Catalog.PersonalityAxes[axis],
                        baseValue = 50, changedValue = 50 + plan.AxisDelta, deltas,
                        changedScoredGoals = deltas.Count(d => d.Difference != 0),
                        scoreCausalityObserved = deltas.Count(d => d.Difference != 0) >= 2 });
                }
            }
            catch (Exception error)
            {
                errors++;
                reports.Add(new { specification, collectionFailed = true, error = error.ToString() });
            }
            Save("running");
        }
        Save(errors == 0 ? "completed" : "completed_with_errors");
        Console.WriteLine($"AGENT-METRICS 수집 {reports.Count}건, 수집 오류 {errors}건: {path}");
        if (errors != 0) throw new InvalidOperationException("AI 측정 수집 오류가 있습니다. 부분 보고서와 원본 오류를 확인하세요.");
    }

    private static string VersionOf(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";

    private static void ValidatePlan(MeasurementPlan plan)
    {
        if (plan.SourceCommit.Length != 40 || !plan.SourceCommit.All(Uri.IsHexDigit) ||
            plan.Seeds.Length == 0 || plan.Seeds.Distinct().Count() != plan.Seeds.Length ||
            plan.PlanningBudgets.Length == 0 || plan.PlanningBudgets.Any(b => b < 1) ||
            plan.PlanningBudgets.Distinct().Count() != plan.PlanningBudgets.Length ||
            plan.HorizonMonths < 2 || plan.Repetitions < 1 || plan.WarmupMonths < 0 ||
            plan.AxisDelta is < 1 or > 50 || plan.StagnationMonths < 1 || plan.ReplayEveryMonths < 1 ||
            plan.BeliefBudget < 1 || plan.PreparationLimitMonths < 1)
            throw new InvalidDataException("AI 측정 계획의 소스·seed·예산·기간·반복·축 범위를 확인하세요.");
    }

    private static IEnumerable<Case> Cases(MeasurementPlan plan)
    {
        foreach (var seed in plan.Seeds)
            for (var repetition = 0; repetition < plan.Repetitions; repetition++)
            {
                var budget = plan.PlanningBudgets.Max();
                yield return new("axes", seed, budget, repetition);
                for (var axis = 0; axis < 14; axis++) yield return new("axes", seed, budget, repetition, axis);
                foreach (var configured in plan.PlanningBudgets)
                {
                    yield return new("paper", seed, configured, repetition);
                    yield return new("tool", seed, configured, repetition);
                    yield return new("ecology", seed, configured, repetition);
                }
                yield return new("correction", seed, budget, repetition);
                yield return new("cooperation", seed, budget, repetition);
                yield return new("fairness", seed, budget, repetition);
            }
    }

    private static GameWorld Controlled(Case specification, MeasurementPlan plan, string? goal = null)
    {
        var world = GameWorld.Create(specification.Seed, new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["birth.enabled"] = 0, ["ecology.foodUse"] = 0, ["ecology.disasterChance"] = 0,
            ["ai.planBudget"] = specification.Budget, ["ai.beliefBudget"] = plan.BeliefBudget,
            ["knowledge.misreadChance"] = 0
        });
        var state = world.Checkpoint();
        // Same declared controlled context as AgentPlanningFixture.Fresh: conserved initial money,
        // one non-planning guard, no new items/knowledge and no guaranteed outcome.
        state.People[1].Money = checked(state.People[1].Money + state.People[0].Money);
        state.People[0].Money = 0;
        Hold(state.People[1], state.Month + plan.HorizonMonths + plan.PreparationLimitMonths + 100);
        if (goal is not null) state.People[0].Plan = [new("replan", 0, goal)];
        if (specification.Family == "axes")
        {
            state.People[0].Personality = Enumerable.Repeat(50, 14).ToArray();
            if (specification.Axis is { } axis) state.People[0].Personality[axis] += plan.AxisDelta;
        }
        return GameWorld.Restore(state);
    }

    private static void Hold(Person person, long until)
    {
        person.Action = ActionKind.Guard; person.Due = until; person.ActionTarget = 0; person.ActionData = "";
    }

    private static Prepared Prepare(Case specification, MeasurementPlan plan)
    {
        var steps = new List<PublicStep>();
        if (specification.Family == "ecology")
        {
            var ecosystem = GameWorld.Create(specification.Seed, new Dictionary<string, long>(StringComparer.Ordinal)
            { ["ai.planBudget"] = specification.Budget, ["ai.beliefBudget"] = plan.BeliefBudget });
            return new(ecosystem, ecosystem.Checkpoint().People[0].Id,
                new { context = "기본 생태계", initialPeople = 2, initialSects = 0, syntheticPopulation = false,
                    forcedBirths = false, lawOverrides = EcologyOverrides }, steps);
        }
        var target = specification.Family is "paper" or "tool" ? specification.Family : null;
        var world = Controlled(specification, plan, target);
        var state = world.Checkpoint(); var actor = state.People[0].Id;
        if (specification.Family == "cooperation")
        {
            state.People[0].Personality = new int[14];
            foreach (var axis in CooperativeAxes) state.People[0].Personality[axis] = 100;
            state.People[1].Personality[4] = 100;
            world = GameWorld.Restore(state);
            Public(world, steps, "loan", state.People[1].Id, actor, 10);
        }
        if (specification.Family == "correction") return PrepareCorrection(world, specification, plan, steps);
        GameWorld? hidden = null;
        if (specification.Family == "fairness")
        {
            Public(world, steps, "create", target: 99, data: "측정용 명시 외부 인물·자산 없음");
            state = world.Checkpoint();
            var other = state.People[1]; var outsider = state.People[^1];
            Hold(outsider, state.Month + plan.HorizonMonths + 100);
            Require(other.Money >= 17 && outsider.Money == 0, "비공개 비교의 기존 자금17 전제가 없습니다.");
            other.Money -= 17; outsider.Money += 17;
            state.People[0].Plan = [new("replan", 0, "paper")];
            world = GameWorld.Restore(state);
            var alternative = world.Checkpoint(); alternative.People[1].Money += 17; alternative.People[^1].Money -= 17;
            hidden = GameWorld.Restore(alternative);
            Require(OwnSignature(world.Checkpoint(), actor) == OwnSignature(hidden.Checkpoint(), actor),
                "숨은 비교 세계가 자기 실제 입력까지 바꿨습니다.");
        }
        return new(world, actor, new { context = "AgentPlanningFixture 고정 조건", initialTarget = target,
            personalityChange = specification.Axis, otherGuarded = true, initialFundsReassignedWithoutMinting = true,
            addedItems = 0, addedKnowledge = 0, addedMastery = 0, hiddenDifference = hidden is null ? "없음" : "타인 간 기존 자금17 분배만 변경" }, steps,
            HiddenAlternative: hidden);
    }

    private static Prepared PrepareCorrection(GameWorld world, Case specification, MeasurementPlan plan, List<PublicStep> steps)
    {
        Public(world, steps, "law", value: 0, data: "ecology.regrowth");
        var initial = world.Checkpoint(); var actor = initial.People[0].Id; var other = initial.People[1].Id;
        var setupDeadline = initial.Month + plan.PreparationLimitMonths;
        Public(world, steps, "possess", actor);
        DiscoverWood(world, steps, actor, setupDeadline);
        var observed = InventoryKnowledgeQueries.KnownSources(world.Checkpoint().Inventory, actor).Single(o => o.Definition == "wood");
        Require(observed.Remaining > 0, "교정 초기 관측에 실제 남은 목재가 없습니다.");
        var state = world.Checkpoint(); Hold(state.People[0], state.Month + plan.PreparationLimitMonths + plan.HorizonMonths + 100);
        world = GameWorld.Restore(state);
        Public(world, steps, "observe"); Public(world, steps, "cancel", other); Public(world, steps, "possess", other);
        DiscoverWood(world, steps, other, setupDeadline);
        while (InventoryKnowledgeQueries.KnownSources(world.Checkpoint().Inventory, other).Single(o => o.Source == observed.Source).Remaining > 0 && world.Month < setupDeadline)
        {
            Public(world, steps, "gather", other, observed.Source, data: "wood");
            CompletePublic(world, other, setupDeadline);
            Public(world, steps, "exploreSource", other, observed.Source);
            CompletePublic(world, other, setupDeadline);
        }
        state = world.Checkpoint();
        Require(state.Inventory.Sources.Single(s => s.Id == observed.Source).Remaining == 0 &&
            state.Inventory.Observations.Any(o => o.Observer == actor && o.Source == observed.Source && o.Remaining > 0),
            "실제 채집 뒤 낡은 자기 관측/고갈이 함께 성립하지 않았습니다.");
        Public(world, steps, "observe"); Public(world, steps, "cancel", actor);
        state = world.Checkpoint(); Hold(state.People[1], state.Month + plan.HorizonMonths + 100);
        state.People[0].Plan = [new("replan", 0, "paper")];
        world = GameWorld.Restore(state);
        return new(world, actor, new { context = "실제 유료 탐색 뒤 다른 인물이 공개 채집한 원천의 낡은 관측",
            source = observed.Source, originalObservation = observed, actualPreparationMonths = world.Month - initial.Month,
            fixtureBeliefInserted = false, sourceQuantityEdited = false, freeItemsAdded = false, specification.Seed }, steps, observed.Source);
    }

    private static void DiscoverWood(GameWorld world, List<PublicStep> steps, long actor, long deadline)
    {
        while (!world.Checkpoint().Inventory.Observations.Any(o => o.Observer == actor && o.Definition == "wood") && world.Month < deadline)
        { Public(world, steps, "exploreSource", actor); CompletePublic(world, actor, deadline); }
        Require(world.Checkpoint().Inventory.Observations.Any(o => o.Observer == actor && o.Definition == "wood"), "제한된 실제 탐색에서 목재를 발견하지 못했습니다.");
    }

    private static void CompletePublic(GameWorld world, long actor, long deadline)
    {
        var due = world.Checkpoint().People.Single(p => p.Id == actor).Due;
        Require(due > world.Month && due <= deadline, "준비 작업이 선언한 기간을 초과했습니다.");
        world.AdvanceTo(due);
    }

    private static void Public(GameWorld world, List<PublicStep> steps, string kind, long actor = 0,
        long target = 0, long value = 0, string data = "")
    {
        var command = new GameCommand("agent-measurement:setup:" + steps.Count.ToString(CultureInfo.InvariantCulture), actor, kind, target, value, data);
        var month = world.Month; var result = world.Execute(command);
        steps.Add(new(month, command, result.Accepted, result.Message, world.StateHash()));
        Require(result.Accepted, "공개 준비 명령 거부: " + result.Message);
    }

    private static (object Report, Dictionary<string, long> FirstScores, bool ContractChecksPassed) Measure(Case specification, MeasurementPlan plan, Prepared prepared)
    {
        var world = prepared.World; var initial = world.Checkpoint(); var initialHash = world.StateHash();
        var warm = GameCodec.Decode(GameCodec.Encode(world));
        warm.AdvanceTo(warm.Month + plan.WarmupMonths);
        Require(world.StateHash() == initialHash, "예열이 측정 원본을 변경했습니다.");
        var replica = GameCodec.Decode(GameCodec.Encode(world));
        var hidden = prepared.HiddenAlternative;
        var initialHiddenHash = hidden?.StateHash();
        var samples = new List<LatencySample>(); var decisions = new List<DecisionSample>();
        var goals = new List<GoalReceipt>(); var failures = new List<FailureReceipt>(); var snapshots = new List<Snapshot>();
        var seenGoals = new HashSet<string>(StringComparer.Ordinal); var seenFailures = new HashSet<string>(StringComparer.Ordinal);
        var personMonths = new Dictionary<string, int>(StringComparer.Ordinal);
        var firstScores = new Dictionary<string, long>(StringComparer.Ordinal);
        var firstDecisionSeen = false; var replayEqual = true; var hiddenEqual = true; var budgetBounded = true;
        var maxWaiting = 0; var waiting = 0;
        long? firstFailureMonth = null; long? correctedMonth = null;
        var initialJobs = initial.Inventory.Jobs.Select(j => j.Id).ToHashSet();
        var initialActivities = initial.Knowledge.Activities.Select(a => a.Id).ToHashSet();
        var initialPayments = initial.Politics.Payments.Count;
        var expectedGoal = specification.Family switch
        { "paper" or "tool" => specification.Family, "correction" or "fairness" => "paper", _ => null };
        object? firstTargetCosts = null;
        var before = initial;
        for (var offset = 1; offset <= plan.HorizonMonths; offset++)
        {
            foreach (var person in before.People.Where(p => specification.Family == "ecology" || p.Id == prepared.Actor))
                if (person.Status == LifeStatus.Alive && person.Action != ActionKind.Idle && person.Due > before.Month)
                    personMonths[person.Action.ToString()] = personMonths.GetValueOrDefault(person.Action.ToString()) + 1;
            var allocated = GC.GetAllocatedBytesForCurrentThread(); var started = Stopwatch.GetTimestamp();
            world.AdvanceTo(initial.Month + offset);
            var ticks = Stopwatch.GetTimestamp() - started; var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            var after = world.Checkpoint();
            var actors = after.People.Where(p => specification.Family == "ecology" || p.Id == prepared.Actor).ToArray();
            var actualDecisions = actors.SelectMany(p => p.Decisions.Where(d => d.Month > before.Month).Select(d => new DecisionSample(p.Id, d))).ToArray();
            decisions.AddRange(actualDecisions);
            samples.Add(new(after.Month, ticks, bytes, actualDecisions.Length,
                actualDecisions.Sum(d => d.Decision.Expansions), checked((int)after.Laws["ai.planBudget"])));
            budgetBounded &= actualDecisions.All(d => d.Decision.Expansions <= after.Laws["ai.planBudget"]);
            if (!firstDecisionSeen && actualDecisions.FirstOrDefault(d => d.Person == prepared.Actor) is { } first)
            {
                firstScores = first.Decision.Candidates.GroupBy(c => c.Goal, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Last().Score, StringComparer.Ordinal);
                firstDecisionSeen = true;
            }
            foreach (var person in actors)
            {
                if (person.Beliefs.GetValueOrDefault("plan:completed") is { } completed &&
                    seenGoals.Add($"{person.Id}:{completed.Observed}:{completed.Value}:{completed.Source}"))
                    goals.Add(new(person.Id, completed.Value, completed.Observed, completed.Source));
                if (person.Beliefs.GetValueOrDefault("plan:lastFailure") is { } failure &&
                    seenFailures.Add($"{person.Id}:{failure.Observed}:{failure.Value}:{failure.Source}"))
                {
                    failures.Add(new(person.Id, failure.Observed, failure.Value, failure.Source));
                    firstFailureMonth ??= after.Month;
                }
            }
            if (expectedGoal is not null && firstTargetCosts is null && goals.FirstOrDefault(g => g.Goal == expectedGoal) is { } targetReceipt)
            {
                var paid = after.Inventory.Jobs.Where(j => !initialJobs.Contains(j.Id) && j.Actor == prepared.Actor).ToArray();
                firstTargetCosts = new { targetReceipt, elapsedMonths = after.Month - initial.Month,
                    occupiedPersonMonthsByAction = new Dictionary<string, int>(personMonths, StringComparer.Ordinal),
                    actualJobs = paid.Select(j => j.Id).ToArray(),
                    actualConsumedByDefinition = paid.SelectMany(j => j.Consumed).GroupBy(c => c.Definition, StringComparer.Ordinal)
                        .ToDictionary(g => g.Key, g => g.Sum(c => (long)c.Quantity), StringComparer.Ordinal) };
            }
            if (prepared.StaleSource > 0 && firstFailureMonth.HasValue && correctedMonth is null &&
                after.Inventory.Observations.Any(o => o.Observer == prepared.Actor && o.Source == prepared.StaleSource &&
                    o.Remaining == 0 && o.Month >= firstFailureMonth.Value)) correctedMonth = after.Month;
            var pending = after.Inventory.Jobs.Count(j => actors.Any(p => p.Id == j.Actor) && j.Status is "reserved" or "processing") +
                after.Knowledge.Activities.Count(a => actors.Any(p => p.Id == a.ActorId || p.Id == a.TeacherId) && a.Status == KnowledgeActivityStatus.Active);
            var waitCount = actors.Count(p => p.Status == LifeStatus.Alive && p.Action is ActionKind.Idle or ActionKind.Wait);
            var livingActors = actors.Count(p => p.Status == LifeStatus.Alive);
            waiting = livingActors > 0 && waitCount == livingActors && pending == 0 ? waiting + 1 : 0;
            maxWaiting = Math.Max(maxWaiting, waiting);
            snapshots.Add(new(after.Month, world.StateHash(), after.People.Count(p => p.Status == LifeStatus.Alive), actualDecisions.Length,
                after.Inventory.Jobs.Count(j => !initialJobs.Contains(j.Id) && j.Status == "completed"), goals.Count,
                after.Inventory.Observations.Count(o => o.Observer == prepared.Actor), waitCount, pending));
            replica.AdvanceTo(world.Month);
            replayEqual &= replica.StateHash() == world.StateHash();
            if (offset % plan.ReplayEveryMonths == 0) replica = GameCodec.Decode(GameCodec.Encode(replica));
            if (hidden is not null)
            {
                hidden.AdvanceTo(world.Month);
                hiddenEqual &= OwnSignature(after, prepared.Actor) == OwnSignature(hidden.Checkpoint(), prepared.Actor);
            }
            before = after;
        }
        Invariants.Validate(before);
        var jobs = before.Inventory.Jobs.Where(j => !initialJobs.Contains(j.Id) &&
            (specification.Family == "ecology" || j.Actor == prepared.Actor)).Select(j =>
                new PaidJob(j.Id, j.Actor, j.Recipe, j.Status, j.Source, j.Due, j.OutputDefinition, j.OutputQuantity, j.Consumed.ToArray(), j.Provenance.ToArray())).ToArray();
        var actionCounts = decisions.GroupBy(d => d.Decision.Action, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var decisionCount = decisions.Count;
        var highestShare = decisionCount == 0 ? (double?)null : actionCounts.Values.Max() / (double)decisionCount;
        var inventorySequence = initial.Inventory.NextEventSequence;
        var completedJobs = jobs.Count(j => j.Status == "completed");
        var nonWaitingPersonMonths = personMonths.Where(kv => kv.Key != "Wait").Sum(kv => kv.Value);
        return (new { specification, fixture = prepared.Declaration, preparation = prepared.Preparation, initialHash,
            initialMonth = initial.Month, finalMonth = world.Month, horizonMonths = plan.HorizonMonths,
            actualLaws = initial.Laws, initialPopulation = initial.People.Count, finalPopulation = before.People.Count,
            initialAgents = initial.People.Where(p => specification.Family == "ecology" || p.Id == prepared.Actor).Select(p => new
            { p.Id, p.BodyId, p.Personality, p.Mood, p.Habits, p.Money, p.Goal, p.Plan, p.KnownPlaces,
                observedSources = initial.Inventory.Observations.Where(o => o.Observer == p.Id).ToArray() }).ToArray(),
            warmup = new { independent = true, months = plan.WarmupMonths, initialHash, finalHash = warm.StateHash() },
            rawLatencySamples = samples, decisions, actionDistribution = actionCounts,
            goalDistribution = decisions.GroupBy(d => d.Decision.Goal, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal),
            dominantActionShare = highestShare, distinctSelectedActions = actionCounts.Count,
            decisionExpansions = decisions.Select(d => d.Decision.Expansions).ToArray(),
            decisionsAtBudgetLimit = decisions.Count(d => d.Decision.Expansions == specification.Budget),
            budgetDeferrals = decisions.Where(d => d.Decision.Action == "Wait" &&
                (d.Decision.Reason.Contains("예산", StringComparison.Ordinal) || d.Decision.Reason.Contains("이월", StringComparison.Ordinal))).ToArray(),
            goalReceipts = goals, goalCompletedWithinHorizon = expectedGoal is null ? (bool?)null : goals.Any(g => g.Goal == expectedGoal),
            expectedGoal, firstTargetCosts,
            failures, failureDistribution = failures.GroupBy(f => f.Reason, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal),
            beliefCorrection = new { source = prepared.StaleSource, firstFailureMonth, correctedMonth,
                monthsFromFailure = correctedMonth - firstFailureMonth, observed = correctedMonth.HasValue },
            occupiedPersonMonthsByAction = personMonths, nonWaitingPersonMonths,
            completedPaidJobsPerNonWaitingPersonMonth = nonWaitingPersonMonths == 0 ? (double?)null : completedJobs / (double)nonWaitingPersonMonths,
            jobs, consumedByDefinition = jobs.SelectMany(j => j.Consumed).GroupBy(c => c.Definition, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Sum(c => (long)c.Quantity), StringComparer.Ordinal),
            completedOutputsByDefinition = jobs.Where(j => j.Status == "completed" && j.Output.Length > 0).GroupBy(j => j.Output, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Sum(j => (long)j.OutputQuantity), StringComparer.Ordinal),
            completedKnowledge = before.Knowledge.Activities.Where(a => !initialActivities.Contains(a.Id) &&
                a.Status == KnowledgeActivityStatus.Completed && (specification.Family == "ecology" || a.ActorId == prepared.Actor || a.TeacherId == prepared.Actor)).ToArray(),
            actualPayments = before.Politics.Payments.Skip(initialPayments).ToArray(),
            actualInventoryEvents = before.Inventory.Events.Where(e => e.Sequence >= inventorySequence &&
                (specification.Family == "ecology" || e.Actor == prepared.Actor)).ToArray(),
            ecologyMaterialDeltas = before.Materials.ToDictionary(kv => kv.Key, kv => new
            {
                extraction = kv.Value.Extraction - (initial.Materials.GetValueOrDefault(kv.Key)?.Extraction ?? 0),
                production = kv.Value.Production - (initial.Materials.GetValueOrDefault(kv.Key)?.Production ?? 0),
                consumption = kv.Value.Consumption - (initial.Materials.GetValueOrDefault(kv.Key)?.Consumption ?? 0),
                loss = kv.Value.Loss - (initial.Materials.GetValueOrDefault(kv.Key)?.Loss ?? 0)
            }, StringComparer.Ordinal),
            snapshots, maxConsecutiveWaitingWithoutPaidProcess = maxWaiting,
            stagnationCandidate = maxWaiting >= plan.StagnationMonths,
            replay = new { allMonthlyHashesEqual = replayEqual, restoreEveryMonths = plan.ReplayEveryMonths, finalHash = replica.StateHash() },
            fairness = new { compared = hidden is not null, allMonthlyOwnSignaturesEqual = hidden is null ? (bool?)null : hiddenEqual,
                initialAlternativeHash = initialHiddenHash, finalAlternativeHash = hidden?.StateHash() },
            measuredContracts = new { budgetBounded, replayEqual, hiddenEqual = hidden is null ? (bool?)null : hiddenEqual },
            finalHash = world.StateHash(), collectionFailed = false }, firstScores, budgetBounded && replayEqual && hiddenEqual);
    }

    private static string OwnSignature(GameState state, long actor) => JsonSerializer.Serialize(new
    {
        person = state.People.Single(p => p.Id == actor),
        observedSources = state.Inventory.Observations.Where(o => o.Observer == actor).ToArray()
    }, Json);

    private static void Require(bool valid, string message)
    { if (!valid) throw new InvalidOperationException(message); }
}
