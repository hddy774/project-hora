using Chronicles.Game;
using Chronicles.Game.Ecology;

internal static partial class GrowthScenarios
{
    private static readonly int[] CalendarSpeeds = [1, 10, 25, 50, 100];
    private sealed record GrowthPoint(long Month, int Population, int TotalPeople, int FounderDescendants, int EverFounderDescendants,
        int Generation, int LivingGeneration, int GoldenCoreOrHigher, int DescendantGoldenCoreOrHigher, int Mortal, int MortalWithoutMartial,
        int Martial, int DescendantMortal, int DescendantMartial, int Households, int Families, int Sects, int Dynasties,
        int TerritorylessDynasties, int ActiveMarkets, int ActiveWorkshops, int ActiveSettlements,
        long Births, long Deaths, long IntervalBirths, long IntervalDeaths, int ExplicitArrivals,
        double SimulationMs, double CaseElapsedMs);

    private sealed class GrowthMilestone(string source, bool historyComplete)
    {
        public long? ExactMonth { get; set; }
        public long? AfterMonthExclusive { get; set; }
        public long? ByMonthInclusive { get; set; }
        public long? Entity { get; set; }
        public string Source { get; } = source;
        public bool HistoryComplete { get; } = historyComplete;
        public string Outcome(bool completed) => ExactMonth is not null ? "observed-exact-record" : ByMonthInclusive is not null ? "observed-in-interval" :
            !HistoryComplete ? "unknown-bounded-history" : completed ? "not-formed-within-observed-horizon" : "not-yet-observed-partial";
    }

    private sealed class GrowthCohort(int scenario, ulong seed)
    {
        public string Scenario { get; } = new[] { "a", "b", "c" }[scenario];
        public bool SelectedDefault { get; } = scenario == 0;
        public string Assumption { get; } = scenario switch { 0 => "a: actual selected product defaults; kinship allowed; no outside inflow",
            1 => "b: comparison only; kinship restricted; exactly two explicit asset-free Mortal outsiders at month 240",
            _ => "c: comparison only; kinship restricted; no outside inflow" };
        public ulong Seed { get; } = seed;
        public string Status { get; set; } = "running";
        public string? Error { get; set; }
        public long ObservedThroughMonth => TimeSeries.LastOrDefault()?.Month ?? 0;
        public List<GrowthPoint> TimeSeries { get; } = [];
        public List<object> ExternalArrivals { get; } = [];
        public IReadOnlyDictionary<string, long>? UsedLaws { get; set; }
        public object[] ProvisionalPolicies { get; set; } = [];
        public object[] GenerationTimes { get; set; } = [];
        public double SimulationMs { get; set; }
        public double CommandMs { get; set; }
        public double CaseElapsedMs { get; set; }
        public string TimingScope { get; } = "simulationMs sums AdvanceTo only; commandMs covers explicit outsider command/snapshot/validation workflow; caseElapsedMs includes genesis, observations and prior report writes, before the current report write; calendar conversion is separate";
        public string? FinalHash { get; set; }
        internal Dictionary<string, GrowthMilestone> Milestones { get; } = new(StringComparer.Ordinal)
        {
            ["firstFounderDescendantBirth"] = new("actual retained Person.Born and biological parent DAG", true),
            ["firstFounderDescendantCore"] = new("actual body Cultivation.Formations[GoldenCore].Month, including departed bodies", true),
            ["firstAdditionalFamily"] = new("actual Group.Founded; original family creation, not later dynasty promotion", true),
            ["firstSect"] = new("actual Group.Founded, including later dissolved sects", true),
            ["firstDynasty"] = new("persistent Group.Kind promotion first observed between samples; Group.Founded is not its promotion date", true),
            ["firstMarket"] = new("persistent Ecology.Facilities[Market].Founded, including currently inactive activity sites", true),
            ["firstWorkshop"] = new("persistent Ecology.Facilities[Workshop].Founded, including currently inactive activity sites", true),
            ["firstSettlement"] = new("persistent Ecology.Facilities[Settlement].Founded, including currently inactive activity sites", true)
        };
        public object MilestoneOutcomes => Milestones.ToDictionary(p => p.Key,
            p => new { outcome = p.Value.Outcome(Status == "completed"), p.Value.ExactMonth, p.Value.AfterMonthExclusive,
                p.Value.ByMonthInclusive, p.Value.Entity, p.Value.Source, p.Value.HistoryComplete }, StringComparer.Ordinal);
        public object CalendarConversion => new { secondsPerYearAt1x = 10, observedSecondsAt1x = ObservedThroughMonth / 12d * 10,
            horizonSecondsAt1x = HorizonMonths / 12d * 10,
            currentUiIntervalMillisecondsPerMonthAt1x = 833, nominalUiObservedSecondsAt1x = ObservedThroughMonth * .833,
            convertedSecondsBySpeed = CalendarSpeeds.ToDictionary(speed => speed, speed => ObservedThroughMonth / 12d * 10 / speed),
            scope = "calendar model and ideal current 833ms UI interval only; excludes busy/backpressure/dialog pauses/timer throttling; actual simulation/command/case milliseconds are separate, not measured play/device speed" };
    }

    private static void ObserveGrowth(GrowthCohort cohort, GameState state, HashSet<long> founders, HashSet<long> initialGroups, double elapsedMs)
    {
        if (state.People.Count != founders.Count + state.BirthCount + cohort.ExternalArrivals.Count)
            throw new InvalidDataException("Growth population must reconcile to actual initial people, births and declared arrivals.");
        var generations = new Dictionary<long, int>(); var lineage = new HashSet<long>(founders);
        foreach (var p in state.People.OrderBy(p => p.Id))
        {
            generations[p.Id] = p.ParentOne == 0 && p.ParentTwo == 0 ? 0 :
                Math.Max(generations.GetValueOrDefault(p.ParentOne), generations.GetValueOrDefault(p.ParentTwo)) + 1;
            if (lineage.Contains(p.ParentOne) || lineage.Contains(p.ParentTwo)) lineage.Add(p.Id);
        }
        var descendants = state.People.Where(p => !founders.Contains(p.Id) && lineage.Contains(p.Id)).ToArray();
        var living = state.People.Where(p => p.Status == LifeStatus.Alive).ToArray();
        var livingDescendants = descendants.Where(p => p.Status == LifeStatus.Alive).ToArray();
        var previous = cohort.TimeSeries.LastOrDefault(); var previousMonth = previous?.Month ?? 0;
        var groups = state.Groups.Where(g => !g.Dissolved).ToArray();
        var people = state.People.ToDictionary(p => p.Id);
        var facilities = state.Ecology.Facilities.Where(f => f.Active && f.Operators.Any(id =>
            people.TryGetValue(id, out var p) && p.Status == LifeStatus.Alive && p.Place == f.Place)).ToArray();
        cohort.TimeSeries.Add(new(state.Month, living.Length, state.People.Count, livingDescendants.Length, descendants.Length,
            descendants.Select(p => generations[p.Id]).DefaultIfEmpty().Max(), livingDescendants.Select(p => generations[p.Id]).DefaultIfEmpty().Max(),
            living.Count(p => p.Realm >= Realm.GoldenCore),
            livingDescendants.Count(p => p.Realm >= Realm.GoldenCore), living.Count(p => p.Realm == Realm.Mortal),
            living.Count(p => p.Realm == Realm.Mortal && p.Martial == 0), living.Count(p => p.Martial > 0),
            livingDescendants.Count(p => p.Realm == Realm.Mortal), livingDescendants.Count(p => p.Martial > 0),
            living.Where(p => p.Household != 0).Select(p => p.Household).Distinct().Count(), groups.Count(g => g.Kind == GroupKind.Family),
            groups.Count(g => g.Kind == GroupKind.Sect), groups.Count(g => g.Kind == GroupKind.Dynasty),
            groups.Count(g => g.Kind == GroupKind.Dynasty && g.Provinces.Count == 0), facilities.Count(f => f.Kind == EcologicalFacilityKind.Market),
            facilities.Count(f => f.Kind == EcologicalFacilityKind.Workshop), facilities.Count(f => f.Kind == EcologicalFacilityKind.Settlement),
            state.BirthCount, state.DeathCount, state.BirthCount - (previous?.Births ?? 0), state.DeathCount - (previous?.Deaths ?? 0),
            cohort.ExternalArrivals.Count, cohort.SimulationMs, elapsedMs));
        void Exact(string key, long? month, long? entity = null)
        {
            var milestone = cohort.Milestones[key];
            if (month is not null && (milestone.ExactMonth is null || month < milestone.ExactMonth))
            { milestone.ExactMonth = month; milestone.Entity = entity; }
        }
        var firstBirth = descendants.OrderBy(p => p.Born).ThenBy(p => p.Id).FirstOrDefault();
        if (firstBirth is not null) Exact("firstFounderDescendantBirth", firstBirth.Born, firstBirth.Id);
        var firstCore = descendants.SelectMany(p => state.Cultivation[p.BodyId].Formations.Where(f => f.Realm == Realm.GoldenCore)
            .Select(f => new { Person = p.Id, f.Month })).OrderBy(f => f.Month).ThenBy(f => f.Person).FirstOrDefault();
        if (firstCore is not null) Exact("firstFounderDescendantCore", firstCore.Month, firstCore.Person);
        var family = state.Groups.Where(g => !initialGroups.Contains(g.Id) && g.Kind != GroupKind.Sect).OrderBy(g => g.Founded).ThenBy(g => g.Id).FirstOrDefault();
        if (family is not null) Exact("firstAdditionalFamily", family.Founded, family.Id);
        var sect = state.Groups.Where(g => g.Kind == GroupKind.Sect).OrderBy(g => g.Founded).ThenBy(g => g.Id).FirstOrDefault();
        if (sect is not null) Exact("firstSect", sect.Founded, sect.Id);
        var dynasty = state.Groups.FirstOrDefault(g => g.Kind == GroupKind.Dynasty);
        if (dynasty is not null && cohort.Milestones["firstDynasty"].ByMonthInclusive is null)
        {
            var milestone = cohort.Milestones["firstDynasty"]; milestone.AfterMonthExclusive = previousMonth;
            milestone.ByMonthInclusive = state.Month; milestone.Entity = dynasty.Id;
        }
        foreach (var pair in new[] { (EcologicalFacilityKind.Market, "firstMarket"), (EcologicalFacilityKind.Workshop, "firstWorkshop"),
                     (EcologicalFacilityKind.Settlement, "firstSettlement") })
        {
            var facility = state.Ecology.Facilities.Where(f => f.Kind == pair.Item1).OrderBy(f => f.Founded).ThenBy(f => f.Id).FirstOrDefault();
            if (facility is not null) Exact(pair.Item2, facility.Founded, facility.Id);
        }
        var firstBirths = descendants.GroupBy(p => generations[p.Id]).ToDictionary(g => g.Key, g => g.Min(p => p.Born));
        cohort.GenerationTimes = descendants.GroupBy(p => generations[p.Id]).OrderBy(g => g.Key).Select(g => (object)new
        {
            generation = g.Key, births = g.Count(), living = g.Count(p => p.Status == LifeStatus.Alive),
            firstBornMonth = g.Min(p => p.Born), lastBornMonth = g.Max(p => p.Born),
            monthsSincePreviousGenerationFirstBirth = firstBirths.TryGetValue(g.Key - 1, out var prior) ? (long?)(g.Min(p => p.Born) - prior) : null,
            parentOneAgeAtBirthMonths = Range(g.Where(p => p.ParentOne != 0).Select(p => (double)(p.Born - people[p.ParentOne].Born))),
            parentTwoAgeAtBirthMonths = Range(g.Where(p => p.ParentTwo != 0).Select(p => (double)(p.Born - people[p.ParentTwo].Born))),
            scope = "biological founder lineage; external adult arrivals are generation 0, not invented births or soul-identity inheritance"
        }).ToArray();
    }

    private static object Range(IEnumerable<double> source)
    {
        var values = source.Order().ToArray();
        return new { samples = values.Length, minimum = values.Length == 0 ? (double?)null : values[0],
            median = values.Length == 0 ? (double?)null : (values[(values.Length - 1) / 2] + values[values.Length / 2]) / 2,
            maximum = values.Length == 0 ? (double?)null : values[^1] };
    }

    private static object[] GrowthDistributions(IReadOnlyList<GrowthCohort> reports) => reports.GroupBy(r => r.Scenario).OrderBy(g => g.Key).Select(group => (object)new
    {
        scenario = group.Key, expectedSeeds = Seeds, startedSeeds = group.Select(c => c.Seed).ToArray(), completedSeeds = group.Where(c => c.Status == "completed").Select(c => c.Seed).ToArray(),
        complete = group.Count(c => c.Status == "completed") == Seeds.Length,
        timeSeriesRanges = group.SelectMany(c => c.TimeSeries).GroupBy(p => p.Month).OrderBy(g => g.Key).Select(g => new
        {
            month = g.Key, observedSeeds = g.Count(), population = Range(g.Select(p => (double)p.Population)), founderDescendants = Range(g.Select(p => (double)p.FounderDescendants)),
            generation = Range(g.Select(p => (double)p.Generation)), livingGeneration = Range(g.Select(p => (double)p.LivingGeneration)),
            goldenCoreOrHigher = Range(g.Select(p => (double)p.GoldenCoreOrHigher)),
            descendantCore = Range(g.Select(p => (double)p.DescendantGoldenCoreOrHigher)), mortal = Range(g.Select(p => (double)p.Mortal)),
            martial = Range(g.Select(p => (double)p.Martial)), households = Range(g.Select(p => (double)p.Households)), sects = Range(g.Select(p => (double)p.Sects)),
            dynasties = Range(g.Select(p => (double)p.Dynasties)), births = Range(g.Select(p => (double)p.Births)), deaths = Range(g.Select(p => (double)p.Deaths))
        }).ToArray(),
        milestones = group.First().Milestones.Keys.ToDictionary(key => key, key => new
        {
            exactObserved = group.Count(c => c.Milestones[key].ExactMonth is not null), intervalObserved = group.Count(c => c.Milestones[key].ByMonthInclusive is not null),
            absentWithinCompletedHorizon = group.Count(c => c.Milestones[key].Outcome(c.Status == "completed") == "not-formed-within-observed-horizon"),
            unknownOrPartial = group.Count(c => c.Milestones[key].Outcome(c.Status == "completed") is "unknown-bounded-history" or "not-yet-observed-partial"),
            exactMonthRange = Range(group.Where(c => c.Milestones[key].ExactMonth is not null).Select(c => (double)c.Milestones[key].ExactMonth!.Value)),
            scope = "observed fixed-seed counts only; interval/unknown outcomes are not zero months or probabilities"
        }, StringComparer.Ordinal)
    }).ToArray();
}
