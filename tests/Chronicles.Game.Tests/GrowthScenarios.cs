using System.Diagnostics;
using System.Text.Json;
using Chronicles.Game;

internal static partial class GrowthScenarios
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private const long HorizonMonths = 6000;
    private const long ObservationInterval = 120;
    private static readonly ulong[] Seeds = [42, 98, 2026];
    public static void Run(string path)
    {
        var total = Stopwatch.StartNew(); var environment = PerformanceScenarios.MeasurementEnvironment();
        var reports = new List<GrowthCohort>(); string? runningCase = null;
        void SaveProgress()
        {
            var complete = reports.Count == 9 && reports.All(r => r.Status == "completed");
            var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            var report = new { schema_version = 2, kind = "seonro-growth-cohorts", environment,
                complete, status = reports.Any(r => r.Status == "failed") ? "failed" : complete ? "completed" : "partial",
                runningCase, plannedCases = 9, completedCases = reports.Count(r => r.Status == "completed"), samplesPerScenario = 3,
                horizonMonths = HorizonMonths, observationIntervalMonths = ObservationInterval, totalElapsedMs = total.Elapsed.TotalMilliseconds,
                completionMeaning = "completed means the finite observation finished; it is not a claim that the selected preset is balanced or indefinitely viable",
                reports, distributions = GrowthDistributions(reports),
                establishmentConditions = new {
                    birth = "actual mature compatible parents, both Golden Core by selected default law, consent/health/care/resources; no forced birth",
                    family = "actual adult founder authority and real transferred founding funds; recorded Group.Founded is the creation date",
                    sect = "actual founder/real funds/membership and current concurrent limit; no empty pre-generated sect",
                    dynasty = "actual independent complete-province sovereignty, resident household, observed territory and administration labor; ownership/claims alone are insufficient",
                    facility = "actual saved operators, household/work/production/public offers and physical sources; describes open-air market/workshop/trade-settlement activity, not a separately created formal city; no population/year unlock or free capacity" },
                timeModel = new { secondsPerYearAt1x = 10, convertedHorizonSecondsAt1x = HorizonMonths / 12d * 10,
                    currentUiIntervalMillisecondsPerMonthAt1x = 833, nominalUiHorizonSecondsAt1x = HorizonMonths * .833,
                    interpretation = "UI calendar conversion only, not measured device play time or an achieved speed claim; actual advance/command/case times are recorded separately" },
                limitations = new[] { "Three fixed seeds are finite observations, not confidence bounds or a proof of future population viability.",
                    "Scenario a is the selected actual default; b/c are explicit comparison assumptions. Only b receives two declared asset-free Mortal outsiders at month 240.",
                    "All used laws and provisional coefficients are disclosed; no parameter sweep or causal sensitivity result is implied.",
                    "Persistent body/organization ledgers provide exact birth/core/family/sect dates; dynasty promotion has no dedicated typed date and is bounded by 120-month observations.",
                    "Ecology.Facilities preserves first Founded even while inactive; reopening/closure change history is bounded and is not reconstructed from sparse observations.",
                    "Total time is captured before each atomic report write; last report serialization/write is excluded. Separate stability harness covers save/resume; this growth run does not add save loops." } };
            File.WriteAllText(full + ".pending", JsonSerializer.Serialize(report, Options)); File.Move(full + ".pending", full, true);
        }
        SaveProgress();
        for (var scenario = 0; scenario < 3; scenario++)
            foreach (var seed in Seeds)
            {
                var cohort = new GrowthCohort(scenario, seed); reports.Add(cohort);
                runningCase = cohort.Scenario + ":" + seed; SaveProgress();
                var watch = Stopwatch.StartNew();
                try
                {
                    // a uses actual product defaults. The only b/c law override is the stated kinship comparison.
                    var world = GameWorld.Create(seed, scenario == 0 ? null : new Dictionary<string, long> { ["birth.kinship"] = 0 });
                    var opening = world.Checkpoint(); cohort.UsedLaws = opening.Laws;
                    if (opening.Laws["birth.kinship"] != (scenario == 0 ? 1 : 0) || opening.Laws["birth.bothParents"] != 1 ||
                        opening.Laws["birth.minimumRealm"] != (long)Realm.GoldenCore || opening.Laws["birth.enabled"] != 1)
                        throw new InvalidOperationException("Selected kinship, enabled reproduction and both-parent Golden Core policy must remain actual inputs.");
                    var founders = opening.People.Select(p => p.Id).ToHashSet(); var initialGroups = opening.Groups.Select(g => g.Id).ToHashSet();
                    cohort.ProvisionalPolicies = Laws.All.Where(l => l.Provisional).Select(l => new { l.Id, value = opening.Laws[l.Id], l.DefaultValue, l.Unit, l.Rationale }).ToArray();
                    void Observe()
                    {
                        var state = world.Checkpoint(); Invariants.Validate(state);
                        ObserveGrowth(cohort, state, founders, initialGroups, watch.Elapsed.TotalMilliseconds);
                        cohort.FinalHash = world.StateHash(); cohort.CaseElapsedMs = watch.Elapsed.TotalMilliseconds;
                        var point = cohort.TimeSeries[^1];
                        Console.WriteLine($"GROWTH {cohort.Scenario}/{seed} month={state.Month}/{HorizonMonths} population={point.Population} descendants={point.FounderDescendants} generation={point.Generation} core={point.GoldenCoreOrHigher} sects={point.Sects} dynasties={point.Dynasties} births={point.Births} deaths={point.Deaths} elapsed={cohort.CaseElapsedMs:F1}ms");
                        SaveProgress();
                    }
                    Observe();
                    for (var month = ObservationInterval; month <= HorizonMonths; month += ObservationInterval)
                    {
                        var advance = Stopwatch.StartNew(); world.AdvanceTo(month); advance.Stop();
                        cohort.SimulationMs += advance.Elapsed.TotalMilliseconds;
                        if (scenario == 1 && month == 240) AddExplicitOutsiders(world, cohort);
                        Observe();
                    }
                    cohort.Status = "completed"; cohort.CaseElapsedMs = watch.Elapsed.TotalMilliseconds;
                    runningCase = null; SaveProgress();
                }
                catch (Exception error) when (error is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException)
                {
                    cohort.Status = "failed"; cohort.Error = error.ToString(); cohort.CaseElapsedMs = watch.Elapsed.TotalMilliseconds;
                    runningCase = null; SaveProgress(); throw;
                }
            }
    }

    private static void AddExplicitOutsiders(GameWorld world, GrowthCohort cohort)
    {
        var watch = Stopwatch.StartNew();
        for (var profile = 0; profile < 2; profile++)
        {
            var before = world.Checkpoint(); var ids = before.People.Select(p => p.Id).ToHashSet();
            var result = world.Execute(new($"scenario-external-{profile}", 0, "create", 0, Data:
                JsonSerializer.Serialize(new { Name = $"명시 편입 {profile + 1}", Species = "human", Gestates = profile == 1 })));
            if (!result.Accepted) throw new InvalidOperationException(result.Message);
            var after = world.Checkpoint(); var arrival = after.People.Single(p => !ids.Contains(p.Id));
            if (after.People.Count != before.People.Count + 1 || arrival.Realm != Realm.Mortal || arrival.Money != 0 ||
                arrival.Skills.Count != 0 || arrival.Understanding.Count != 0 || after.Items.Any(i => i.Owner == arrival.Id) || after.CreatedMoney != before.CreatedMoney)
                throw new InvalidDataException("An explicit comparison outsider cannot receive hidden assets, mastery or currency.");
            cohort.ExternalArrivals.Add(new { month = world.Month, person = arrival.Id, arrival.Species, arrival.Gestates, arrival.Born,
                realm = arrival.Realm.ToString(), arrival.Money, items = 0, skills = 0, knowledge = 0 });
        }
        cohort.CommandMs += watch.Elapsed.TotalMilliseconds;
    }
}
