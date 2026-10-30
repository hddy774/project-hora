using System.Diagnostics;
using System.Text.Json;
using Chronicles.Game;

internal static class GrowthScenarios
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static void Run(string path)
    {
        var reports = new List<object>();
        for (var scenario = 0; scenario < 3; scenario++)
            foreach (var seed in new ulong[] { 42, 98, 2026 })
            {
                var watch = Stopwatch.StartNew();
                // Scenario a uses actual product defaults; it must detect an accidental default-policy change.
                var world = GameWorld.Create(seed, scenario == 0 ? null : new Dictionary<string, long> { ["birth.kinship"] = 0 });
                var opening = world.Checkpoint();
                if (opening.Laws["birth.kinship"] != (scenario == 0 ? 1 : 0) || opening.Laws["birth.bothParents"] != 1 ||
                    opening.Laws["birth.minimumRealm"] != (long)Realm.GoldenCore)
                    throw new InvalidOperationException("Growth scenarios must retain the selected kinship policy and both-parents Golden Core fertility boundary.");
                for (var month = 120; month <= 6000; month += 120)
                {
                    world.AdvanceTo(month);
                    if (scenario == 1 && month == 240)
                        foreach (var profile in Enumerable.Range(0, 2))
                        {
                            var result = world.Execute(new($"scenario-external-{profile}", 0, "create", 0, Data:
                                JsonSerializer.Serialize(new { Name = $"명시 편입 {profile + 1}", Species = "human", Gestates = profile == 1 })));
                            if (!result.Accepted) throw new InvalidOperationException(result.Message);
                        }
                }
                var s = world.Checkpoint(); Invariants.Validate(s);
                var generations = new Dictionary<long, int>();
                foreach (var p in s.People.OrderBy(p => p.Id)) generations[p.Id] =
                    Math.Max(generations.GetValueOrDefault(p.ParentOne), generations.GetValueOrDefault(p.ParentTwo)) + (p.ParentOne == 0 ? 0 : 1);
                reports.Add(new { scenario = scenario == 0 ? "a: kinship allowed, selected default" : scenario == 1 ?
                    "b: restricted, two explicitly created outsiders at year20" : "c: restricted, no inflow",
                    seed, horizonMonths = 6000, simulationMs = watch.ElapsedMilliseconds,
                    living = s.People.Count(p => p.Status == LifeStatus.Alive), born = s.BirthCount, deaths = s.DeathCount,
                    maxObservedGeneration = generations.Values.Max(),
                    goldenCoreOrHigher = s.People.Count(p => p.Status == LifeStatus.Alive && p.Realm >= Realm.GoldenCore),
                    mortal = s.People.Count(p => p.Status == LifeStatus.Alive && p.Realm == Realm.Mortal),
                    martial = s.People.Count(p => p.Status == LifeStatus.Alive && p.Martial > 0),
                    sects = s.Groups.Count(g => g.Kind == GroupKind.Sect && !g.Dissolved),
                    firstDescendantCore = s.People.Where(p => p.ParentOne != 0).SelectMany(p => s.Cultivation[p.BodyId].Formations)
                        .Where(f => f.Realm == Realm.GoldenCore).Select(f => (long?)f.Month).Min(),
                    explicitOutsiders = scenario == 1 ? 2 : 0,
                    outsiderProfile = "explicit public create JSON: adult human donor and gestator; Mortal, zero assets and no granted knowledge/mastery",
                    finalHash = world.StateHash(), usedLaws = s.Laws });
                Console.WriteLine($"GROWTH {scenario}/{seed}: living={s.People.Count(p => p.Status == LifeStatus.Alive)}, births={s.BirthCount}");
            }
        File.WriteAllText(path, JsonSerializer.Serialize(new { samplesPerScenario = 3,
            observation = "finite 500-year observations, not a proof of future population bounds or viability",
            timeModel = "1x: 10 seconds/year; 500 years = 5000 seconds; 100x target = 50 seconds. Device speed was not measured.",
            limitations = "Only scenario b receives the two explicit outsiders; no scenario has hidden NPCs, forced births or target population.", reports }, Options));
    }
}
