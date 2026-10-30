using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using Chronicles.Game;
using Chronicles.Game.People;

namespace Chronicles.Game.Tests.Features;

// Synthetic JSON validator fixtures only. None of these scenarios creates/advances a world or captures a measurement host.
internal static class SensitivityContractFixture
{
    internal static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    internal const string Commit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    internal const string Fingerprint = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static readonly string[] Milestones = ["firstFounderDescendantBirth", "firstFounderDescendantCore", "firstAdditionalFamily", "firstSect", "firstDynasty", "firstMarket", "firstWorkshop", "firstSettlement"];
    private static readonly string[] Counts = ["population", "totalPeople", "founderDescendants", "everFounderDescendants", "generation", "livingGeneration",
        "goldenCoreOrHigher", "descendantGoldenCoreOrHigher", "mortal", "mortalWithoutMartial", "martial", "descendantMortal", "descendantMartial", "households",
        "families", "sects", "dynasties", "territorylessDynasties", "activeMarkets", "activeWorkshops", "activeSettlements", "births", "deaths", "intervalBirths", "intervalDeaths", "explicitArrivals"];
    internal static byte[] Bytes(JsonNode node) => Encoding.UTF8.GetBytes(node.ToJsonString());
    internal static SensitivitySource Source() => new(Commit, Fingerprint, true,
        GrowthSensitivityContracts.Assemblies.Select(name => new SensitivityBuild(name, "0.2.1+" + Commit, new string('d', 64))).ToArray());
    internal static GrowthSensitivityPlan Plan(byte[] baseline)
    {
        var value = Laws.ById["cultivation.foundation"].DefaultValue;
        return new() { SchemaVersion = 1, Kind = "seonro-growth-sensitivity-plan", SourceCommit = Commit, ProductSourceFingerprint = Fingerprint,
            BaselineReportPath = "explicit-synthetic-contract-fixture-not-a-measurement.json", BaselineReportSha256 = GrowthSensitivityContracts.Digest(baseline),
            LawKey = "cultivation.foundation", Values = [value - 1, value + 1], Seeds = [42, 98, 2026], HorizonMonths = 120,
            ObservationIntervalMonths = 120, Rationale = "Synthetic schema/validator fixture only; this is not an adopted experiment plan or growth result." };
    }
    internal static JsonObject Baseline()
    {
        var environment = new JsonObject { ["sourceCommit"] = Commit, ["sourceWorktreeClean"] = true, ["buildConfiguration"] = "Release", ["engine"] = "none",
            ["platform"] = "host", ["runtime"] = "synthetic", ["os"] = "synthetic", ["device"] = "synthetic", ["architecture"] = "synthetic", ["thermalState"] = "synthetic", ["powerState"] = "synthetic" };
        var rows = new JsonArray(); var requested = new JsonArray();
        foreach (var scenario in new[] { "a", "b", "c" }) foreach (var seed in GrowthSensitivityContracts.Seeds)
        {
            var laws = Laws.CreateDefaults(); if (scenario != "a") laws["birth.kinship"] = 0;
            var points = new JsonArray();
            for (var month = 0; month <= 6000; month += 120)
            {
                var arrivals = scenario == "b" && month >= 240 ? 2 : 0;
                var point = new JsonObject(); foreach (var name in Counts) point[name] = 0;
                point["month"] = month; point["population"] = 2 + arrivals; point["totalPeople"] = 2 + arrivals; point["goldenCoreOrHigher"] = 2;
                point["mortal"] = arrivals; point["mortalWithoutMartial"] = arrivals; point["households"] = 1; point["families"] = 1;
                point["explicitArrivals"] = arrivals; point["simulationMs"] = month; point["caseElapsedMs"] = month; points.Add(point);
            }
            var milestones = new JsonObject();
            foreach (var name in Milestones) milestones[name] = new JsonObject { ["outcome"] = "not-formed-within-observed-horizon", ["exactMonth"] = null,
                ["afterMonthExclusive"] = null, ["byMonthInclusive"] = null, ["entity"] = null, ["historyComplete"] = true, ["source"] = "synthetic contract fixture only" };
            var policies = new JsonArray();
            foreach (var law in Laws.All.Where(l => l.Provisional)) policies.Add(JsonSerializer.SerializeToNode(new { id = law.Id, value = laws[law.Id], defaultValue = law.DefaultValue, unit = law.Unit, rationale = law.Rationale }));
            var outsiders = new JsonArray();
            if (scenario == "b") for (var i = 0; i < 2; i++) outsiders.Add(new JsonObject { ["month"] = 240, ["person"] = 90000 + i, ["species"] = "human",
                ["gestates"] = i == 1, ["born"] = 240 - laws["birth.maturity"], ["realm"] = "Mortal", ["money"] = 0, ["items"] = 0, ["skills"] = 0, ["knowledge"] = 0 });
            rows.Add(new JsonObject { ["scenario"] = scenario, ["seed"] = seed, ["status"] = "completed", ["error"] = null, ["observedThroughMonth"] = 6000,
                ["selectedDefault"] = scenario == "a", ["finalHash"] = new string('e', 64), ["usedLaws"] = JsonSerializer.SerializeToNode(laws),
                ["provisionalPolicies"] = policies, ["timeSeries"] = points, ["externalArrivals"] = outsiders, ["milestoneOutcomes"] = milestones,
                ["simulationMs"] = 6000, ["caseElapsedMs"] = 6000, ["commandMs"] = 0 });
            requested.Add(new JsonObject { ["scenario"] = scenario, ["seed"] = seed });
        }
        return new() { ["schema_version"] = 2, ["kind"] = "seonro-growth-cohorts", ["sourceCommit"] = Commit, ["productSourceFingerprint"] = Fingerprint,
            ["complete"] = true, ["requested_complete"] = true, ["status"] = "completed", ["runningCase"] = null, ["horizonMonths"] = 6000,
            ["observationIntervalMonths"] = 120, ["plannedCases"] = 9, ["completedCases"] = 9, ["samplesPerScenario"] = 3, ["environment"] = environment,
            ["requestedCases"] = requested, ["reports"] = rows, ["limitations"] = "synthetic contract fixture only; never actual measurement evidence" };
    }
    internal static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        catch (ArgumentException) { return; }
        catch (JsonException) { return; }
        throw new InvalidOperationException("Malformed or partial sensitivity evidence was accepted.");
    }
    internal static void Check(bool condition) { if (!condition) throw new InvalidOperationException("Sensitivity contract assertion failed."); }
}

public sealed class SensitivityPlanContractScenario : IFeatureScenario
{
    public string Name => "T04 sensitivity contract: fixed policies, law bounds, source/build identity and strict plan JSON; no measurement";
    public void Run()
    {
        var bytes = SensitivityContractFixture.Bytes(SensitivityContractFixture.Baseline()); var plan = SensitivityContractFixture.Plan(bytes); var source = SensitivityContractFixture.Source();
        _ = GrowthSensitivityContracts.Validate(plan, bytes, source);
        foreach (var key in new[] { "birth.kinship", "birth.bothParents", "birth.minimumRealm", "birth.enabled", "life.qi", "missing-law" })
            SensitivityContractFixture.Reject(() => GrowthSensitivityContracts.Validate(plan with { LawKey = key }, bytes, source));
        foreach (var values in new long[][] { [0, 1], [plan.Values[0], plan.Values[0]], [Laws.ById[plan.LawKey].DefaultValue, plan.Values[1]], [1], [] })
            SensitivityContractFixture.Reject(() => GrowthSensitivityContracts.Validate(plan with { Values = values }, bytes, source));
        foreach (var seeds in new ulong[][] { [42, 98], [42, 42, 2026], [42, 98, 77] })
            SensitivityContractFixture.Reject(() => GrowthSensitivityContracts.Validate(plan with { Seeds = seeds }, bytes, source));
        foreach (var changed in new[] { plan with { HorizonMonths = 0 }, plan with { HorizonMonths = 6120 }, plan with { ObservationIntervalMonths = 0 },
            plan with { ObservationIntervalMonths = 7 }, plan with { Rationale = "" }, plan with { SourceCommit = new string('f', 40) }, plan with { ProductSourceFingerprint = new string('f', 64) } })
            SensitivityContractFixture.Reject(() => GrowthSensitivityContracts.Validate(changed, bytes, source));
        SensitivityContractFixture.Reject(() => GrowthSensitivityContracts.Validate(plan, bytes, source with { Clean = false }));
        var builds = source.Builds.ToArray(); builds[0] = builds[0] with { InformationalVersion = "0.2.1+" + new string('f', 40) };
        SensitivityContractFixture.Reject(() => GrowthSensitivityContracts.Validate(plan, bytes, source with { Builds = builds }));
        var json = JsonSerializer.SerializeToNode(plan, SensitivityContractFixture.Json)!.AsObject();
        _ = GrowthSensitivityContracts.ReadPlan(SensitivityContractFixture.Bytes(json));
        json["hiddenLawOverride"] = 0; SensitivityContractFixture.Reject(() => GrowthSensitivityContracts.ReadPlan(SensitivityContractFixture.Bytes(json)));
        SensitivityContractFixture.Reject(() => GrowthSensitivityContracts.ReadPlan(Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"schemaVersion\":1}")));
    }
}

public sealed class SensitivityBaselineContractScenario : IFeatureScenario
{
    public string Name => "T04 sensitivity contract: completed nine-cohort baseline, physical conditions, hashes and aggregate originals; no growth";
    public void Run()
    {
        var baseline = SensitivityContractFixture.Baseline(); var bytes = SensitivityContractFixture.Bytes(baseline); var source = SensitivityContractFixture.Source();
        var plan = SensitivityContractFixture.Plan(bytes);
        SensitivityContractFixture.Reject(() => GrowthSensitivityContracts.Validate(plan with { BaselineReportSha256 = new string('f', 64) }, bytes, source));
        Action<JsonObject>[] mutations = [b => b["complete"] = false, b => b["requested_complete"] = false, b => b["status"] = "partial",
            b => b["runningCase"] = "a:42", b => b["sourceCommit"] = new string('f', 40), b => b["productSourceFingerprint"] = new string('f', 64),
            b => b["environment"]!["sourceWorktreeClean"] = false, b => b["reports"]!.AsArray().RemoveAt(8),
            b => b["reports"]![1]!["seed"] = 42, b => b["reports"]![0]!["status"] = "failed", b => b["reports"]![0]!["timeSeries"] = new JsonArray(),
            b => b["reports"]![0]!["usedLaws"]!["ecology.foodUse"] = 0,
            b => b["reports"]![0]!["timeSeries"]![1]!["totalPeople"] = 999,
            b => b["reports"]![0]!["provisionalPolicies"]!.AsArray().RemoveAt(0),
            b => b["reports"]![3]!["externalArrivals"]![0]!["money"] = 1,
            b => b["reports"]![0]!["milestoneOutcomes"]!["firstFounderDescendantCore"]!["outcome"] = "observed-exact-record"];
        foreach (var mutate in mutations)
        {
            var changed = baseline.DeepClone().AsObject(); mutate(changed); var payload = SensitivityContractFixture.Bytes(changed);
            SensitivityContractFixture.Reject(() => GrowthSensitivityContracts.Validate(SensitivityContractFixture.Plan(payload), payload, source));
        }
        var aggregate = baseline.DeepClone().AsObject(); aggregate["kind"] = "seonro-growth-cohorts-aggregate"; aggregate["schema_version"] = 1;
        aggregate.Remove("environment"); aggregate.Remove("requestedCases"); aggregate.Remove("runningCase");
        var keys = new JsonArray(); foreach (var scenario in new[] { "a", "b", "c" }) foreach (var seed in GrowthSensitivityContracts.Seeds) keys.Add(new JsonArray(JsonValue.Create(scenario), JsonValue.Create(seed)));
        aggregate["inputArtifacts"] = new JsonArray(new JsonObject { ["path"] = "synthetic-source.json", ["sha256"] = GrowthSensitivityContracts.Digest(bytes),
            ["environment"] = baseline["environment"]!.DeepClone(), ["cohortKeys"] = keys });
        var aggregateBytes = SensitivityContractFixture.Bytes(aggregate); var aggregatePlan = SensitivityContractFixture.Plan(aggregateBytes);
        _ = GrowthSensitivityContracts.Validate(aggregatePlan, aggregateBytes, source, _ => bytes);
        SensitivityContractFixture.Reject(() => GrowthSensitivityContracts.Validate(aggregatePlan, aggregateBytes, source));
        SensitivityContractFixture.Reject(() => GrowthSensitivityContracts.Validate(aggregatePlan, aggregateBytes, source, _ => Encoding.UTF8.GetBytes("{}")));
        aggregate["reports"]![0]!["finalHash"] = new string('f', 64); aggregateBytes = SensitivityContractFixture.Bytes(aggregate);
        SensitivityContractFixture.Reject(() => GrowthSensitivityContracts.Validate(SensitivityContractFixture.Plan(aggregateBytes), aggregateBytes, source, _ => bytes));
    }
}

public sealed class SensitivityPartitionContractScenario : IFeatureScenario
{
    public string Name => "T04 sensitivity contract: two-case partitions remain partial, only six complete, duplicate/CLI/output overwrite rejected";
    public void Run()
    {
        long[] values = [100, 200];
        var selected = values.Select(value => (value, 42UL, "completed")).ToArray();
        var state = GrowthSensitivityContracts.Completion(selected, values, 42);
        SensitivityContractFixture.Check(state.RequestedComplete && !state.Complete && state.Status == "requested-completed");
        var all = (from value in values from seed in GrowthSensitivityContracts.Seeds select (value, seed, "completed")).ToArray();
        state = GrowthSensitivityContracts.Completion(all, values, null); SensitivityContractFixture.Check(state.RequestedComplete && state.Complete && state.Status == "completed");
        state = GrowthSensitivityContracts.Completion(all[..5], values, null); SensitivityContractFixture.Check(!state.RequestedComplete && !state.Complete);
        all[5] = (values[1], 2026, "failed"); state = GrowthSensitivityContracts.Completion(all, values, null); SensitivityContractFixture.Check(!state.Complete && state.Status == "failed");
        SensitivityContractFixture.Reject(() => GrowthSensitivityContracts.Completion([selected[0], selected[0]], values, 42));
        SensitivityContractFixture.Check(GrowthSensitivityContracts.CoreOutcome(null, LifeStatus.Dead, true) == "death-before-core" &&
            GrowthSensitivityContracts.CoreOutcome(null, LifeStatus.Departed, true) == "departure-before-core" &&
            GrowthSensitivityContracts.CoreOutcome(null, LifeStatus.Alive, true) == "right-censored-at-plan-horizon" &&
            GrowthSensitivityContracts.CoreOutcome(null, LifeStatus.Alive, false) == "not-yet-observed-partial" &&
            GrowthSensitivityContracts.CoreOutcome(60, LifeStatus.Dead, true) == "observed-exact-record");
        var request = GrowthSensitivityContracts.Arguments(["--sensitivity", "new.json", "--sensitivity-plan", "plan.json", "--sensitivity-seed", "42"]);
        SensitivityContractFixture.Check(request?.Seed == 42 && GrowthSensitivityContracts.Arguments([]) is null);
        foreach (var args in new string[][] { ["--sensitivity-plan", "plan.json"], ["--sensitivity", "new.json"],
            ["--sensitivity", "new.json", "--sensitivity-plan", "plan.json", "--sensitivity-seed", "77"],
            ["--sensitivity", "new.json", "--sensitivity-plan", "plan.json", "--sensitivity-plan", "other.json"] })
            SensitivityContractFixture.Reject(() => GrowthSensitivityContracts.Arguments(args));
        var directory = Path.Combine(Path.GetTempPath(), "sensitivity-contract-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var baseline = Path.Combine(directory, "baseline.json"); File.WriteAllText(baseline, "original");
            SensitivityContractFixture.Reject(() => SensitivityOutput.Open(baseline, [baseline]));
            var path = Path.Combine(directory, "unique.json");
            using (var writer = SensitivityOutput.Open(path, [baseline]))
            {
                SensitivityContractFixture.Reject(() => SensitivityOutput.Open(path, [baseline]));
                writer.Write(new { kind = "synthetic-contract-fixture", complete = false, month = 0 });
                writer.Write(new { kind = "synthetic-contract-fixture", complete = false, month = 1 });
                SensitivityContractFixture.Check(JsonDocument.Parse(File.ReadAllBytes(path)).RootElement.GetProperty("month").GetInt32() == 1 && !File.Exists(path + ".pending"));
            }
            SensitivityContractFixture.Reject(() => SensitivityOutput.Open(path, [baseline]));
            SensitivityContractFixture.Check(File.ReadAllText(baseline) == "original");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}

public sealed class SensitivityAdoptedPolicyContractScenario : IFeatureScenario
{
    public string Name => "T04 sensitivity contract: saved People policy values take precedence and unobserved species/models are not used claims";
    public void Run()
    {
        var checkpoint = PeopleState.Empty() with { Tuning = new PeopleTuning(Revision: 77), ReputationPolicy = new ReputationPolicy(MaximumClaimsPerObserver: 17) };
        var snapshot = JsonSerializer.SerializeToElement(GrowthScenarios.SensitivityPeoplePolicies(checkpoint, 12), SensitivityContractFixture.Json);
        var definitions = JsonSerializer.SerializeToElement(GrowthScenarios.SensitivityDeclaredPeoplePolicies(), SensitivityContractFixture.Json);
        SensitivityContractFixture.Check(snapshot.GetProperty("tuning").GetProperty("revision").GetInt64() == 77 &&
            snapshot.GetProperty("reputationPolicy").GetProperty("maximumClaimsPerObserver").GetInt32() == 17 &&
            snapshot.GetProperty("observedMonth").GetInt64() == 12 && snapshot.GetProperty("observedSpeciesIds").GetArrayLength() == 0 &&
            snapshot.GetProperty("actualFrozenConceptionModels").GetArrayLength() == 0 &&
            definitions.GetProperty("reputation").GetProperty("maximumClaimsPerObserver").GetInt32() == new ReputationPolicy().MaximumClaimsPerObserver &&
            definitions.GetProperty("speciesCatalog").GetArrayLength() > 0 &&
            definitions.GetProperty("genetics").GetProperty("policyId").GetString() == "PC-TEMP-V021-GENETICS-01");
    }
}

public sealed class SensitivityReportInputPreservationScenario : IFeatureScenario
{
    public string Name => "T04 sensitivity contract: actual CLI rejects report alias before functional writes and preserves plan/baseline bytes";
    public void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "sensitivity-input-guard-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var baselinePath = Path.Combine(directory, "baseline.json"); var planPath = Path.Combine(directory, "plan.json");
            var baseline = SensitivityContractFixture.Bytes(SensitivityContractFixture.Baseline());
            var plan = JsonSerializer.SerializeToUtf8Bytes(SensitivityContractFixture.Plan(baseline) with { BaselineReportPath = baselinePath }, SensitivityContractFixture.Json);
            File.WriteAllBytes(baselinePath, baseline); File.WriteAllBytes(planPath, plan);
            foreach (var report in new[] { planPath, baselinePath })
            {
                var output = Path.Combine(directory, Path.GetFileNameWithoutExtension(report) + "-never-collected.json");
                var host = Environment.ProcessPath ?? throw new InvalidOperationException("CLI host path is unavailable.");
                var start = new ProcessStartInfo(host) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                if (Path.GetFileNameWithoutExtension(host).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(typeof(SensitivityReportInputPreservationScenario).Assembly.Location);
                string[] arguments = ["--filter", "no-matching-cli-sentinel-" + Guid.NewGuid().ToString("N"), "--report", report,
                    "--sensitivity", output, "--sensitivity-plan", planPath];
                foreach (var argument in arguments) start.ArgumentList.Add(argument);
                using var process = Process.Start(start) ?? throw new InvalidOperationException("CLI guard child could not start.");
                var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(10000)) { process.Kill(entireProcessTree: true); throw new InvalidOperationException("CLI guard child did not exit promptly."); }
                var text = stdout.GetAwaiter().GetResult(); var error = stderr.GetAwaiter().GetResult();
                SensitivityContractFixture.Check(process.ExitCode != 0 && error.Contains("--sensitivity + --report", StringComparison.Ordinal) &&
                    !text.Contains("PASS ", StringComparison.Ordinal) && !text.Contains("RESULT ", StringComparison.Ordinal) && !text.Contains("SENSITIVITY ", StringComparison.Ordinal) &&
                    File.ReadAllBytes(planPath).SequenceEqual(plan) && File.ReadAllBytes(baselinePath).SequenceEqual(baseline) &&
                    !File.Exists(output) && !File.Exists(output + ".pending") && !File.Exists(output + ".lock"));
            }
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
