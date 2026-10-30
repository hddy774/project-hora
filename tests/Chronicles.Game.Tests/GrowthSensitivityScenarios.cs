using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Chronicles.Core;
using Chronicles.Game;
using Chronicles.Game.People;
using Chronicles.Game.Tests.Experiments;

internal static partial class GrowthScenarios
{
    private static readonly string[] SensitivityLimitations =
    [
        "The original default a/b/c x three-seed growth baseline is separate and unchanged. These cases are explicitly counterfactual, not new default-preset successes.",
        "Three paired seeds are descriptive observations, not confidence intervals or proof of viability. Changed RNG paths may diverge after a changed event.",
        "AdvanceTo is called for every actual month. Original growth and cumulative labor series use the plan interval; latest monthly progress and actual hash are saved atomically. A full per-month labor trace is not retained.",
        "Labor is occupied real person-months and active inventory reservation unit-months at interval start, including waits. It is not productive output or a second labor authority; completions and physical flow deltas are separate.",
        "Individual first-core outcomes use actual retained body formations and Person.Ended. Death/departure before core and living right-censoring are distinct; baseline cohort rows do not supply individual terminal histories.",
        "Saved People policies and frozen conception models are actual checkpoint data. Source-declared defaults/catalogs are separate and do not assert every species/trait/formula was used. Formula-only PC-TEMP references need the separate source inventory review; no non-law policy parameter is independently perturbed.",
        "Baseline-window rows are existing observations only, without interpolation. Exact baseline milestones after the plan horizon are censored; a 120-month dynasty interval crossing the horizon remains uncertain.",
        "Monthly snapshot/hash/report costs are part of case elapsed time but outside simulationMs. The last report write is excluded from its own timing. No hardware-speed acceptance is inferred."
    ];
    private sealed class SensitivityCase(long value, ulong seed)
    {
        internal GrowthCohort Cohort { get; } = new(0, seed);
        internal long Value { get; } = value;
        internal long ProgressMonth { get; set; }
        internal long LawRevision { get; set; }
        internal List<object> LaborAtObservationIntervals { get; } = [];
        internal object? LastMonthLabor { get; set; }
        internal long InventoryReservedUnitMonths { get; set; }
        internal Dictionary<string, long> CompletedJobsByRecipe { get; } = new(StringComparer.Ordinal);
        internal object? AdoptedPeoplePolicies { get; set; }
        internal object[] DescendantOutcomes { get; set; } = [];
        internal Dictionary<string, long> OccupiedPersonMonths { get; } = new(StringComparer.Ordinal);
    }

    // Explicit opt-in only. Contract feature scenarios never call this method.
    internal static void RunSensitivity(SensitivityRequest request)
    {
        var planPath = Path.GetFullPath(request.Plan); var planBytes = File.ReadAllBytes(planPath);
        var plan = GrowthSensitivityContracts.ReadPlan(planBytes);
        var baselinePath = Path.GetFullPath(plan.BaselineReportPath, Path.GetDirectoryName(planPath)!);
        var baselineBytes = File.ReadAllBytes(baselinePath); var source = CaptureSensitivitySource();
        var baselineSources = new HashSet<string>(StringComparer.Ordinal) { planPath, baselinePath };
        var validated = GrowthSensitivityContracts.Validate(plan, baselineBytes, source, path =>
        {
            var full = Path.GetFullPath(path, Path.GetDirectoryName(baselinePath)!); baselineSources.Add(full); return File.ReadAllBytes(full);
        });
        if (request.Seed is { } selected && !GrowthSensitivityContracts.Seeds.Contains(selected)) throw new InvalidDataException("계획 밖 민감도 seed입니다.");
        var environment = PerformanceScenarios.MeasurementEnvironment();
        using var output = SensitivityOutput.Open(request.Output, baselineSources);
        var cases = new List<SensitivityCase>(); var total = Stopwatch.StartNew(); string? runningCase = null;
        var requested = (from value in plan.Values from seed in GrowthSensitivityContracts.Seeds where request.Seed is null || seed == request.Seed select new { value, seed }).ToArray();
        object Project(SensitivityCase c) => new
        {
            lawValue = c.Value, deltaFromDefault = c.Value - validated.DefaultValue, c.Cohort.Seed,
            scenario = "a", selectedBloodPolicy = "a", usesProductDefaultLaws = false, changedLaw = plan.LawKey,
            selectedPolicy = "a: kinship allowed, both parents Golden Core, no outsiders; one provisional-law counterfactual",
            counterfactual = true, c.ProgressMonth, c.LawRevision, c.Cohort.Status, c.Cohort.Error, c.Cohort.ObservedThroughMonth,
            c.Cohort.UsedLaws, c.Cohort.ProvisionalPolicies, c.Cohort.TimeSeries, c.Cohort.GenerationTimes,
            milestonesObservedThroughMonth = c.Cohort.ObservedThroughMonth, c.Cohort.MilestoneOutcomes,
            c.Cohort.SimulationMs, c.Cohort.CommandMs, c.Cohort.CaseElapsedMs, c.Cohort.FinalHash,
            lawDifferences = new[] { new { id = plan.LawKey, baseline = validated.DefaultValue, actual = c.Value, delta = c.Value - validated.DefaultValue } },
            c.LaborAtObservationIntervals, c.LastMonthLabor, c.InventoryReservedUnitMonths, c.CompletedJobsByRecipe,
            occupiedPersonMonthsByAction = c.OccupiedPersonMonths, c.DescendantOutcomes, c.AdoptedPeoplePolicies,
            calendarConversion = new { secondsPerYearAt1x = 10, observedSecondsAt1x = c.ProgressMonth / 12d * 10,
                horizonSecondsAt1x = plan.HorizonMonths / 12d * 10, scope = "calendar conversion only; actual simulation/case milliseconds are separate" }
        };
        void SaveProgress()
        {
            var state = GrowthSensitivityContracts.Completion(cases.Select(c => (c.Value, c.Cohort.Seed, c.Cohort.Status)).ToArray(), plan.Values, request.Seed);
            output.Write(new
            {
                schema_version = 1, kind = "seonro-growth-sensitivity", counterfactual = true, selectedBloodPolicy = "a", usesProductDefaultLaws = false, changedLaw = plan.LawKey,
                sourceCommit = source.Commit, productSourceFingerprint = source.Fingerprint, source.Builds, environment,
                sourceDeclaredPeoplePolicyDefaults = SensitivityPeopleDefaults,
                plan, planSha256 = GrowthSensitivityContracts.Digest(planBytes), baselineReportResolvedPath = baselinePath,
                baselineReportSha256 = GrowthSensitivityContracts.Digest(baselineBytes), baselineCompletedCases = 9,
                requestedCases = requested, requested_complete = state.RequestedComplete, complete = state.Complete, status = state.Status,
                plannedCases = 6, completedCases = cases.Count(c => c.Cohort.Status == "completed"), runningCase, totalElapsedMs = total.Elapsed.TotalMilliseconds,
                completionMeaning = "Only fixed-plan observation completion. No target population, survival, first-core deadline or balance acceptance is asserted.",
                reports = cases.Select(Project).ToArray(),
                distributionsByValue = cases.GroupBy(c => c.Value).Select(g => new { value = g.Key, distributions = GrowthDistributions(g.Select(c => c.Cohort).ToArray()) }).ToArray(),
                baselineWindow = SensitivityBaselineWindow(validated.Baseline, plan),
                limitations = SensitivityLimitations
            });
        }
        SaveProgress();
        foreach (var specification in requested)
        {
            var current = new SensitivityCase(specification.value, specification.seed); cases.Add(current);
            runningCase = plan.LawKey + "=" + specification.value + ":" + specification.seed; SaveProgress();
            var watch = Stopwatch.StartNew();
            try
            {
                var world = GameWorld.Create(specification.seed, new Dictionary<string, long> { [plan.LawKey] = specification.value });
                var before = world.Checkpoint(); var expected = Laws.CreateDefaults(); expected[plan.LawKey] = specification.value;
                GrowthSensitivityContracts.SameLaws(JsonSerializer.SerializeToElement(before.Laws), expected);
                GrowthSensitivityContracts.Need(before.Laws["birth.kinship"] == 1 && before.Laws["birth.bothParents"] == 1 &&
                    before.Laws["birth.minimumRealm"] == (long)Realm.GoldenCore && before.People.Count == 2 && before.People.All(p => p.Realm == Realm.HalfImmortal) &&
                    before.Groups.All(g => g.Kind != GroupKind.Sect), "민감도 창세가 선택 정책/실제 두 반선 조건과 다릅니다.");
                var founders = before.People.Select(p => p.Id).ToHashSet(); var initialGroups = before.Groups.Select(g => g.Id).ToHashSet();
                current.Cohort.UsedLaws = before.Laws; current.LawRevision = before.LawRevision;
                current.AdoptedPeoplePolicies = SensitivityPeoplePolicies(before.PeopleDomain, before.Month);
                current.Cohort.ProvisionalPolicies = Laws.All.Where(l => l.Provisional).Select(l => (object)new
                { l.Id, value = before.Laws[l.Id], l.DefaultValue, l.Unit, l.Minimum, l.Maximum, l.Provisional, l.Rationale, lawRevision = before.LawRevision }).ToArray();
                void Observe(GameState state)
                {
                    Invariants.Validate(state);
                    ObserveGrowth(current.Cohort, state, founders, initialGroups, watch.Elapsed.TotalMilliseconds);
                }
                Observe(before); current.Cohort.FinalHash = world.StateHash(); current.Cohort.CaseElapsedMs = watch.Elapsed.TotalMilliseconds;
                SaveProgress();
                for (var month = 1L; month <= plan.HorizonMonths; month++)
                {
                    var advance = Stopwatch.StartNew(); world.AdvanceTo(month); advance.Stop(); current.Cohort.SimulationMs += advance.Elapsed.TotalMilliseconds;
                    var after = world.Checkpoint(); Invariants.Validate(after);
                    current.LastMonthLabor = SensitivityLabor(before, after, current); current.ProgressMonth = month;
                    current.AdoptedPeoplePolicies = SensitivityPeoplePolicies(after.PeopleDomain, after.Month);
                    current.DescendantOutcomes = SensitivityDescendants(after, founders, completed: false);
                    if (month % plan.ObservationIntervalMonths == 0)
                    {
                        Observe(after);
                        current.LaborAtObservationIntervals.Add(new { month, occupiedPersonMonthsByAction = new Dictionary<string, long>(current.OccupiedPersonMonths, StringComparer.Ordinal),
                            current.InventoryReservedUnitMonths, completedJobsByRecipe = new Dictionary<string, long>(current.CompletedJobsByRecipe, StringComparer.Ordinal),
                            actualCumulativeMaterialFlows = after.Materials });
                    }
                    current.Cohort.FinalHash = world.StateHash(); current.Cohort.CaseElapsedMs = watch.Elapsed.TotalMilliseconds;
                    Console.WriteLine($"SENSITIVITY {runningCase} month={month}/{plan.HorizonMonths} population={after.People.Count(p => p.Status == LifeStatus.Alive)} births={after.BirthCount} deaths={after.DeathCount} elapsed={current.Cohort.CaseElapsedMs:F1}ms");
                    SaveProgress(); before = after;
                }
                current.Cohort.Status = "completed"; current.DescendantOutcomes = SensitivityDescendants(before, founders, completed: true);
                current.Cohort.CaseElapsedMs = watch.Elapsed.TotalMilliseconds; runningCase = null; SaveProgress();
            }
            catch (Exception error)
            {
                current.Cohort.Status = "failed"; current.Cohort.Error = error.ToString(); current.Cohort.CaseElapsedMs = watch.Elapsed.TotalMilliseconds;
                runningCase = null; SaveProgress(); throw;
            }
        }
    }

    private static SensitivitySource CaptureSensitivitySource()
    {
        var host = ExecutionMeasurementHost.Capture();
        Assembly[] assemblies = [typeof(ProductInfo).Assembly, typeof(GameWorld).Assembly, typeof(GameSaveStore).Assembly, typeof(GrowthScenarios).Assembly];
        return new(host.Commit, host.ProductSourceFingerprint, true, assemblies.Select(assembly => new SensitivityBuild(assembly.GetName().Name!,
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "", GrowthSensitivityContracts.Digest(File.ReadAllBytes(assembly.Location)))).ToArray());
    }

    private static readonly object SensitivityPeopleDefaults = new
    {
        scope = "Source declarations/catalogs only, not claims of adoption or execution. Actual checkpoint policies are separate. No manual non-law policy overrides are performed.",
        peopleTuning = new PeopleTuning(), evaluation = new EvaluationPolicy(), reputation = new ReputationPolicy(), social = new PeopleSocialPolicy(),
        genetics = GeneticModel.Provisional(), reproduction = new ReproductionPolicy(), speciesCatalog = SpeciesCatalog.All, traitCatalog = TraitCatalog.All,
        sourceAnchors = new { peopleTuning = "src/Chronicles.Game/People/Aptitudes.cs:PeopleTuning", evaluation = "src/Chronicles.Game/People/Evaluation.cs:EvaluationPolicy",
            reputation = "src/Chronicles.Game/People/Reputation.cs:ReputationPolicy", social = "src/Chronicles.Game/People/SocialState.cs:PeopleSocialPolicy",
            genetics = "src/Chronicles.Game/People/Inheritance.cs:GeneticModel.Provisional", reproduction = "src/Chronicles.Game/People/Embodiment.cs:ReproductionPolicy",
            species = "src/Chronicles.Game/People/Embodiment.cs:SpeciesCatalog", traits = "src/Chronicles.Game/People/Traits.cs:TraitCatalog" },
        formulaOnlyReferencesForSeparateReview = new { martial = "PC-TEMP-V021-MARTIAL-APTITUDE-01; src/Chronicles.Game/People/MartialAptitude.cs:Assess",
            combat = "PC-TEMP-V021-COMBAT-ASSESSMENT-01; src/Chronicles.Game/People/CombatAssessment.cs",
            traces = "PC-TEMP-V021-TRACE-02; src/Chronicles.Game/World.PeopleBridge.cs" }
    };

    internal static object SensitivityDeclaredPeoplePolicies() => SensitivityPeopleDefaults;

    internal static object SensitivityPeoplePolicies(PeopleState checkpoint, long observedMonth) => new
    {
        observedMonth, checkpoint.Tuning, checkpoint.EvaluationPolicy, checkpoint.ReputationPolicy, socialPolicy = checkpoint.Social.Policy,
        observedSpeciesIds = checkpoint.Profiles.Select(p => p.Body.SpeciesId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
        adoptedBodyReproduction = checkpoint.Profiles.Select(p => new { p.PersonId, p.Body.SpeciesId, p.Body.Reproduction, p.Body.Capabilities, p.Body.Source }).ToArray(),
        actualFrozenConceptionModels = checkpoint.Inheritance.Conceptions.GroupBy(c => ExecutionMeasurementHost.Digest(c.Model), StringComparer.Ordinal)
            .Select(group => new { model = group.First().Model, actualConceptions = group.Count(), firstConceptionMonth = group.Min(c => c.Month),
                lawRevisions = group.Select(c => c.LawRevision).Distinct().Order().ToArray(), sourceAnchor = "src/Chronicles.Game/People/Inheritance.cs:ConceptionRecord.Model" }).ToArray(),
        scope = "Adopted checkpoint values, not replacement static defaults. No conception means no adopted conception-model claim; declared but absent species are not marked used. Source formulas and non-law policies are not independently swept."
    };

    private static object SensitivityLabor(GameState before, GameState after, SensitivityCase current)
    {
        var occupied = before.People.Where(p => p.Status == LifeStatus.Alive && p.Action != ActionKind.Idle && p.Due > before.Month)
            .GroupBy(p => p.Action.ToString()).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        foreach (var pair in occupied) current.OccupiedPersonMonths[pair.Key] = current.OccupiedPersonMonths.GetValueOrDefault(pair.Key) + pair.Value;
        var completed = before.Inventory.Jobs.Where(j => j.Status == "completed").Select(j => j.Id).ToHashSet();
        var newJobs = after.Inventory.Jobs.Where(j => j.Status == "completed" && !completed.Contains(j.Id)).ToArray();
        foreach (var job in newJobs) current.CompletedJobsByRecipe[job.Recipe] = current.CompletedJobsByRecipe.GetValueOrDefault(job.Recipe) + 1;
        var reservedUnits = before.Inventory.Labor.Where(l => l.Active && l.Start <= before.Month && l.End > before.Month).Sum(l => (long)l.Units);
        current.InventoryReservedUnitMonths += reservedUnits;
        var flows = after.Materials.Select(pair =>
        {
            var prior = before.Materials.GetValueOrDefault(pair.Key) ?? new MaterialFlow(); var actual = pair.Value;
            return new { definition = pair.Key, extraction = actual.Extraction - prior.Extraction, production = actual.Production - prior.Production,
                consumption = actual.Consumption - prior.Consumption, loss = actual.Loss - prior.Loss };
        }).Where(f => f.extraction != 0 || f.production != 0 || f.consumption != 0 || f.loss != 0).ToArray();
        return new
        {
            startMonth = before.Month, endMonth = after.Month, occupiedPersonMonthsByAction = occupied,
            inventoryReservedUnits = reservedUnits,
            processingJobsAtStart = before.Inventory.Jobs.Count(j => j.Status == "processing"),
            completedJobs = newJobs.Select(j => new
            { j.Id, j.Actor, j.Recipe, j.Due, j.OutputDefinition, j.OutputQuantity, j.Consumed }).ToArray(),
            actualFlowDeltas = flows, births = after.BirthCount - before.BirthCount, deaths = after.DeathCount - before.DeathCount
        };
    }

    private static object[] SensitivityDescendants(GameState state, HashSet<long> founders, bool completed)
    {
        var lineage = new HashSet<long>(founders);
        foreach (var person in state.People.OrderBy(p => p.Id)) if (lineage.Contains(person.ParentOne) || lineage.Contains(person.ParentTwo)) lineage.Add(person.Id);
        return state.People.Where(p => lineage.Contains(p.Id) && !founders.Contains(p.Id)).Select(person =>
        {
            long? First(Realm realm) => state.Cultivation[person.BodyId].Formations.Where(f => f.Realm == realm).Select(f => (long?)f.Month).Min();
            var core = First(Realm.GoldenCore);
            return (object)new { person.Id, person.BodyId, person.Born, person.Ended, status = person.Status.ToString(), entryMonth = First(Realm.Qi),
                foundationMonth = First(Realm.Foundation), firstCoreMonth = core,
                outcome = GrowthSensitivityContracts.CoreOutcome(core, person.Status, completed) };
        }).ToArray();
    }

    private static object[] SensitivityBaselineWindow(JsonElement baseline, GrowthSensitivityPlan plan) => baseline.GetProperty("reports").EnumerateArray()
        .Where(row => row.GetProperty("scenario").GetString() == "a").Select(row => (object)new
        {
            seed = row.GetProperty("seed").GetUInt64(), source = "existing selected-default a cohort; no rerun and no interpolated observations",
            observedBaselinePoints = row.GetProperty("timeSeries").EnumerateArray().Where(p => p.GetProperty("month").GetInt64() <= plan.HorizonMonths).Select(p => p.Clone()).ToArray(),
            milestones = row.GetProperty("milestoneOutcomes").EnumerateObject().Select(p =>
            {
                var exact = p.Value.GetProperty("exactMonth"); var upper = p.Value.GetProperty("byMonthInclusive");
                var exactObserved = exact.ValueKind != JsonValueKind.Null && exact.GetInt64() <= plan.HorizonMonths;
                var intervalObserved = upper.ValueKind != JsonValueKind.Null && upper.GetInt64() <= plan.HorizonMonths;
                var intervalCrosses = upper.ValueKind != JsonValueKind.Null && p.Value.GetProperty("afterMonthExclusive").GetInt64() < plan.HorizonMonths && upper.GetInt64() > plan.HorizonMonths;
                return new { name = p.Name, outcome = exactObserved ? "observed-exact-record" : intervalObserved ? "observed-in-interval" :
                    intervalCrosses ? "uncertain-interval-crosses-plan-horizon" : "not-formed-within-plan-horizon",
                    exactMonth = exactObserved ? exact.GetInt64() : (long?)null,
                    afterMonthExclusive = intervalObserved ? p.Value.GetProperty("afterMonthExclusive").GetInt64() : (long?)null,
                    byMonthInclusive = intervalObserved ? upper.GetInt64() : (long?)null };
            }).ToArray()
        }).ToArray();
}

internal sealed class SensitivityOutput : IDisposable
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly FileStream reservation;
    private readonly string path;
    private SensitivityOutput(string path, FileStream reservation) { this.path = path; this.reservation = reservation; }
    internal static SensitivityOutput Open(string path, IEnumerable<string> protectedInputs)
    {
        var full = Path.GetFullPath(path);
        GrowthSensitivityContracts.Need(!protectedInputs.Select(Path.GetFullPath).Contains(full, StringComparer.Ordinal) &&
            !File.Exists(full) && !File.Exists(full + ".pending") && !File.Exists(full + ".lock"), "기준/계획/기존 결과를 덮어쓸 수 없습니다. 고유 출력 경로를 지정하세요.");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        return new(full, new FileStream(full + ".lock", FileMode.CreateNew, FileAccess.Write, FileShare.None));
    }
    internal void Write(object report)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(report, Json);
        using (var pending = new FileStream(path + ".pending", FileMode.CreateNew, FileAccess.Write, FileShare.None)) pending.Write(bytes);
        File.Move(path + ".pending", path, overwrite: true);
    }
    public void Dispose() { reservation.Dispose(); File.Delete(path + ".lock"); }
}
