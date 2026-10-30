using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Chronicles.Game;

internal sealed record GrowthSensitivityPlan
{
    public required int SchemaVersion { get; init; }
    public required string Kind { get; init; }
    public required string SourceCommit { get; init; }
    public required string ProductSourceFingerprint { get; init; }
    public required string BaselineReportPath { get; init; }
    public required string BaselineReportSha256 { get; init; }
    public required string LawKey { get; init; }
    public required long[] Values { get; init; }
    public required ulong[] Seeds { get; init; }
    public required long HorizonMonths { get; init; }
    public required long ObservationIntervalMonths { get; init; }
    public required string Rationale { get; init; }
}

internal sealed record SensitivityBuild(string Assembly, string InformationalVersion, string Sha256);
internal sealed record SensitivitySource(string Commit, string Fingerprint, bool Clean, SensitivityBuild[] Builds);
internal sealed record ValidatedSensitivity(GrowthSensitivityPlan Plan, JsonElement Baseline,
    long DefaultValue, string[] BaselineSourcePaths);
internal sealed record SensitivityRequest(string Output, string Plan, ulong? Seed);

// Pure validation only: no world, advancing, profiling, file writes or source capture.
internal static class GrowthSensitivityContracts
{
    internal static readonly ulong[] Seeds = [42, 98, 2026];
    internal static readonly string[] Assemblies = ["Chronicles.Core", "Chronicles.Game", "Chronicles.Hosting", "Chronicles.Game.Tests"];
    private static readonly string[] BaselineScenarios = ["a", "b", "c"];
    private static readonly string[] AssetFields = ["money", "items", "skills", "knowledge"];
    private static readonly JsonSerializerOptions PlanJson = new()
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private static readonly string[] Counts = ["population", "totalPeople", "founderDescendants", "everFounderDescendants", "generation", "livingGeneration",
        "goldenCoreOrHigher", "descendantGoldenCoreOrHigher", "mortal", "mortalWithoutMartial", "martial", "descendantMortal", "descendantMartial",
        "households", "families", "sects", "dynasties", "territorylessDynasties", "activeMarkets", "activeWorkshops", "activeSettlements",
        "births", "deaths", "intervalBirths", "intervalDeaths", "explicitArrivals"];

    internal static SensitivityRequest? Arguments(string[] args)
    {
        var options = new[] { "--sensitivity", "--sensitivity-plan", "--sensitivity-seed" };
        if (!options.Any(args.Contains)) return null;
        string? Value(string option, bool required)
        {
            var index = Array.IndexOf(args, option);
            if (index < 0) { if (required) throw new ArgumentException(option + "을 명시하세요."); return null; }
            if (index != Array.LastIndexOf(args, option) || index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]) ||
                args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException(option + " 뒤에 중복되지 않은 값을 지정하세요.");
            return args[index + 1];
        }
        var output = Value(options[0], true)!; var plan = Value(options[1], true)!;
        var text = Value(options[2], false); ulong? seed = null;
        if (text is not null)
        {
            if (!ulong.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value) || !Seeds.Contains(value))
                throw new ArgumentException("민감도 seed는 42/98/2026 중 하나여야 합니다.");
            seed = value;
        }
        return new(output, plan, seed);
    }

    internal static GrowthSensitivityPlan ReadPlan(byte[] bytes)
    {
        _ = ReadJson(bytes);
        try { return JsonSerializer.Deserialize<GrowthSensitivityPlan>(bytes, PlanJson) ?? throw new InvalidDataException("민감도 계획이 없습니다."); }
        catch (JsonException error) { throw new InvalidDataException("민감도 계획 schema가 다릅니다.", error); }
    }

    internal static ValidatedSensitivity Validate(GrowthSensitivityPlan plan, byte[] baselineBytes, SensitivitySource source,
        Func<string, byte[]>? readBaselineSource = null)
    {
        Need(plan.SchemaVersion == 1 && plan.Kind == "seonro-growth-sensitivity-plan", "민감도 계획 kind/schema가 다릅니다.");
        Need(Hex(plan.SourceCommit, 40) && Hex(plan.ProductSourceFingerprint, 64) && Hex(plan.BaselineReportSha256, 64), "계획 소스/지문/SHA256이 필요합니다.");
        Need(!string.IsNullOrWhiteSpace(plan.BaselineReportPath) && !string.IsNullOrWhiteSpace(plan.Rationale), "기준 보고서와 비교 근거를 명시하세요.");
        Need(source.Clean && source.Commit == plan.SourceCommit && source.Fingerprint == plan.ProductSourceFingerprint, "dirty/다른 실행 소스·지문은 거부합니다.");
        Need(source.Builds is { Length: 4 } && source.Builds.Select(b => b.Assembly).ToHashSet(StringComparer.Ordinal).SetEquals(Assemblies) &&
            source.Builds.All(b => b.InformationalVersion.EndsWith("+" + plan.SourceCommit, StringComparison.Ordinal) && Hex(b.Sha256, 64)),
            "Core/Game/Hosting/Tests 빌드 SourceRevisionId가 계획과 일치해야 합니다.");
        Need(plan.Seeds is { Length: 3 } && plan.Seeds.Distinct().Count() == 3 && plan.Seeds.ToHashSet().SetEquals(Seeds), "세 실제 seed 42/98/2026을 중복 없이 명시하세요.");
        Need(plan.HorizonMonths is > 0 and <= 6000 && plan.ObservationIntervalMonths > 0 && plan.ObservationIntervalMonths <= plan.HorizonMonths &&
            plan.HorizonMonths % plan.ObservationIntervalMonths == 0, "기준 6000개월 안의 양수 관찰창/간격을 명시하세요.");
        Need(!string.IsNullOrWhiteSpace(plan.LawKey) && Laws.ById.TryGetValue(plan.LawKey, out var definition) && definition.Provisional, "정본/사용자 고정 정책 키는 민감도 대상으로 바꿀 수 없습니다.");
        var law = Laws.ById[plan.LawKey];
        Need(plan.Values is { Length: 2 } && plan.Values.Distinct().Count() == 2 && plan.Values.All(v => v != law.DefaultValue), "기본값과 다른 두 비교값을 중복 없이 명시하세요.");
        foreach (var value in plan.Values)
        {
            var values = Laws.CreateDefaults(); values[plan.LawKey] = value;
            Need(Laws.Validate(values) is null, "민감도 비교값이 법칙 범위/의존 조건을 벗어났습니다.");
        }
        Need(Digest(baselineBytes) == plan.BaselineReportSha256, "기준 보고서 SHA256이 다릅니다.");
        var baseline = ReadJson(baselineBytes); var paths = new List<string>();
        ValidateHeader(baseline, plan, full: true);
        ValidateRows(baseline, full: true);
        if (Text(baseline, "kind") == "seonro-growth-cohorts-aggregate")
        {
            var artifacts = Field(baseline, "inputArtifacts"); Need(artifacts.ValueKind == JsonValueKind.Array && artifacts.GetArrayLength() > 0, "통합 기준의 원본 출처가 없습니다.");
            var originals = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var artifact in artifacts.EnumerateArray())
            {
                var path = Text(artifact, "path"); Need(!paths.Contains(path, StringComparer.Ordinal), "중복 기준 원본 경로입니다."); paths.Add(path);
                var bytes = readBaselineSource?.Invoke(path) ?? throw new InvalidDataException("통합 기준의 실제 원본을 읽어야 합니다.");
                Need(Hex(Text(artifact, "sha256"), 64) && Digest(bytes) == Text(artifact, "sha256"), "통합 기준 원본 SHA256이 다릅니다.");
                var original = ReadJson(bytes); Need(Text(original, "kind") == "seonro-growth-cohorts", "통합 기준은 실제 성장 원본만 참조해야 합니다.");
                ValidateHeader(original, plan, full: false); ValidateRows(original, full: false);
                Need(JsonNode.DeepEquals(JsonNode.Parse(Field(original, "environment").GetRawText()), JsonNode.Parse(Field(artifact, "environment").GetRawText())), "통합 기준의 실행 환경 출처가 다릅니다.");
                var keys = Field(artifact, "cohortKeys").EnumerateArray().Select(k => k[0].GetString() + ":" + k[1].GetUInt64()).ToArray();
                var rows = Field(original, "reports").EnumerateArray().ToArray();
                Need(keys.Length == rows.Length && keys.Distinct(StringComparer.Ordinal).Count() == keys.Length && keys.ToHashSet().SetEquals(rows.Select(Key)), "통합 원본 cohort key가 다릅니다.");
                foreach (var row in rows) Need(originals.TryAdd(Key(row), row.Clone()), "중복 기준 원본 cohort입니다.");
            }
            foreach (var row in Field(baseline, "reports").EnumerateArray())
                Need(originals.TryGetValue(Key(row), out var original) && JsonNode.DeepEquals(JsonNode.Parse(row.GetRawText()), JsonNode.Parse(original.GetRawText())), "통합 기준이 실제 원본 cohort와 다릅니다.");
            Need(originals.Count == 9, "기준 원본의 서로 다른 9개 완주가 필요합니다.");
        }
        return new(plan, baseline, law.DefaultValue, paths.ToArray());
    }

    private static void ValidateHeader(JsonElement report, GrowthSensitivityPlan plan, bool full)
    {
        var kind = Text(report, "kind"); var aggregate = kind == "seonro-growth-cohorts-aggregate";
        Need((aggregate && Integer(report, "schema_version") == 1 || kind == "seonro-growth-cohorts" && Integer(report, "schema_version") == 2) &&
            Text(report, "sourceCommit") == plan.SourceCommit && Text(report, "productSourceFingerprint") == plan.ProductSourceFingerprint,
            "다른/구형 기준 성장 소스·지문입니다.");
        Need(Boolean(report, "requested_complete") && Integer(report, "horizonMonths") == 6000 && Integer(report, "observationIntervalMonths") == 120 &&
            Integer(report, "plannedCases") == 9 && Integer(report, "samplesPerScenario") == 3, "기준은 원형 6000/120·9cohort 조건이어야 합니다.");
        var rows = Field(report, "reports"); Need(rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() > 0 && Integer(report, "completedCases") == rows.GetArrayLength(), "기준 완료 사례가 없습니다.");
        var complete = rows.GetArrayLength() == 9;
        Need((!full || complete) && Boolean(report, "complete") == complete && Text(report, "status") == (complete ? "completed" : "requested-completed"), "부분/실패 기준을 완주로 사용할 수 없습니다.");
        if (!aggregate)
        {
            Need(Field(report, "runningCase").ValueKind == JsonValueKind.Null, "진행 중 기준은 사용할 수 없습니다.");
            var environment = Field(report, "environment");
            Need(Text(environment, "sourceCommit") == plan.SourceCommit && Boolean(environment, "sourceWorktreeClean") &&
                Text(environment, "buildConfiguration") == "Release" && Text(environment, "engine") == "none" && Text(environment, "platform") == "host", "기준 실행 소스/환경이 다릅니다.");
            foreach (var key in new[] { "runtime", "os", "device", "architecture", "thermalState", "powerState" }) Need(!string.IsNullOrWhiteSpace(Text(environment, key)), "기준 실행 환경이 누락됐습니다.");
            var requested = Field(report, "requestedCases").EnumerateArray().Select(Key).ToArray();
            Need(requested.Length == rows.GetArrayLength() && requested.Distinct(StringComparer.Ordinal).Count() == requested.Length && requested.ToHashSet().SetEquals(rows.EnumerateArray().Select(Key)), "기준 요청/완료 key가 다릅니다.");
            var scenarios = rows.EnumerateArray().Select(row => Text(row, "scenario")).Distinct(StringComparer.Ordinal).ToArray();
            var seeds = rows.EnumerateArray().Select(row => Field(row, "seed").GetUInt64()).Distinct().ToArray();
            Need(scenarios.Length is 1 or 3 && seeds.Length is 1 or 3 && requested.ToHashSet().SetEquals(from scenario in scenarios from seed in seeds select scenario + ":" + seed), "실제 원형 CLI 선택으로 만들 수 없는 기준 부분집합입니다.");
        }
    }

    private static void ValidateRows(JsonElement report, bool full)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in Field(report, "reports").EnumerateArray())
        {
            var scenario = Text(row, "scenario"); var seed = Field(row, "seed").GetUInt64();
            Need(scenario is "a" or "b" or "c" && Seeds.Contains(seed) && keys.Add(Key(row)), "기준 scenario/seed 누락·중복입니다.");
            Need(Text(row, "status") == "completed" && Field(row, "error").ValueKind == JsonValueKind.Null && Integer(row, "observedThroughMonth") == 6000 &&
                Boolean(row, "selectedDefault") == (scenario == "a") && Hex(Text(row, "finalHash"), 64), "기준 cohort는 실제 완주 상태/hash여야 합니다.");
            var expected = Laws.CreateDefaults(); if (scenario != "a") expected["birth.kinship"] = 0;
            SameLaws(Field(row, "usedLaws"), expected);
            var policies = Field(row, "provisionalPolicies").EnumerateArray().ToArray();
            Need(policies.Length == Laws.All.Count(l => l.Provisional) && policies.Select(p => Text(p, "id")).Distinct(StringComparer.Ordinal).Count() == policies.Length, "기준 PC-TEMP 목록이 누락/중복됐습니다.");
            foreach (var policy in policies)
            {
                Need(Laws.ById.TryGetValue(Text(policy, "id"), out var law) && law.Provisional, "기준 PC-TEMP key가 다릅니다.");
                var definition = Laws.ById[Text(policy, "id")];
                Need(Integer(policy, "value") == expected[definition.Id] && Integer(policy, "defaultValue") == definition.DefaultValue &&
                    Text(policy, "unit") == definition.Unit && Text(policy, "rationale") == definition.Rationale, "기준 PC-TEMP 값/근거가 다릅니다.");
            }
            var points = Field(row, "timeSeries").EnumerateArray().ToArray(); Need(points.Length == 51, "기준 관측행이 비었습니다/누락됐습니다.");
            long births = 0, deaths = 0; double simulation = 0, elapsed = 0;
            for (var i = 0; i < points.Length; i++)
            {
                var p = points[i]; Need(Integer(p, "month") == i * 120, "기준 관측월이 누락/중복됐습니다.");
                foreach (var key in Counts) Need(Integer(p, key) >= 0, "기준 관측 수가 음수입니다.");
                var expectedArrivals = scenario == "b" && i >= 2 ? 2 : 0;
                Need(Integer(p, "explicitArrivals") == expectedArrivals && Integer(p, "totalPeople") == 2 + Integer(p, "births") + expectedArrivals &&
                    Integer(p, "population") <= Integer(p, "totalPeople") - Integer(p, "deaths") && Integer(p, "founderDescendants") <= Integer(p, "population") &&
                    Integer(p, "descendantGoldenCoreOrHigher") <= Integer(p, "founderDescendants") && Integer(p, "goldenCoreOrHigher") <= Integer(p, "population"), "기준 인구/출생/사망/금단 회계가 다릅니다.");
                Need(Integer(p, "intervalBirths") == Integer(p, "births") - births && Integer(p, "intervalDeaths") == Integer(p, "deaths") - deaths, "기준 구간 출생/사망이 다릅니다.");
                var currentSimulation = Number(p, "simulationMs"); var currentElapsed = Number(p, "caseElapsedMs");
                Need(currentSimulation >= simulation && currentElapsed >= elapsed, "기준 시간이 역행합니다.");
                births = Integer(p, "births"); deaths = Integer(p, "deaths"); simulation = currentSimulation; elapsed = currentElapsed;
            }
            Need(Integer(points[0], "population") == 2 && Integer(points[0], "births") == 0 && Integer(points[0], "deaths") == 0 &&
                Integer(points[0], "sects") == 0 && Integer(points[0], "dynasties") == 0 && Number(row, "simulationMs") == simulation && Number(row, "caseElapsedMs") >= elapsed,
                "기준 창세/경과 시간이 다릅니다.");
            var arrivals = Field(row, "externalArrivals").EnumerateArray().ToArray();
            Need(arrivals.Length == (scenario == "b" ? 2 : 0), "기준의 명시 편입 조건이 다릅니다.");
            foreach (var arrival in arrivals)
                Need(Integer(arrival, "month") == 240 && Integer(arrival, "person") > 0 && Text(arrival, "species") == "human" &&
                    Integer(arrival, "born") == 240 - expected["birth.maturity"] && Text(arrival, "realm") == "Mortal" &&
                    AssetFields.All(k => Integer(arrival, k) == 0), "기준 편입의 출처/무자산 조건이 다릅니다.");
            if (arrivals.Length > 0) Need(arrivals.Select(a => Integer(a, "person")).Distinct().Count() == 2 &&
                arrivals.Select(a => Boolean(a, "gestates")).Distinct().Count() == 2, "기준 편입 신원/생식 프로필이 다릅니다.");
            var milestones = Field(row, "milestoneOutcomes");
            string[] names = ["firstFounderDescendantBirth", "firstFounderDescendantCore", "firstAdditionalFamily", "firstSect", "firstDynasty", "firstMarket", "firstWorkshop", "firstSettlement"];
            Need(milestones.ValueKind == JsonValueKind.Object && milestones.EnumerateObject().Select(p => p.Name).ToHashSet().SetEquals(names), "기준 성립/부재 근거가 누락됐습니다.");
            foreach (var name in names)
            {
                var milestone = Field(milestones, name); var exact = Field(milestone, "exactMonth"); var upper = Field(milestone, "byMonthInclusive");
                Need(Boolean(milestone, "historyComplete") && !string.IsNullOrWhiteSpace(Text(milestone, "source")), "기준 성립 이력이 불완전합니다.");
                if (exact.ValueKind != JsonValueKind.Null)
                    Need(name != "firstDynasty" && Integer(milestone, "exactMonth") is >= 0 and <= 6000 && Text(milestone, "outcome") == "observed-exact-record" &&
                        Integer(milestone, "entity") > 0 && upper.ValueKind == JsonValueKind.Null && Field(milestone, "afterMonthExclusive").ValueKind == JsonValueKind.Null, "기준 정확 성립 시각이 다릅니다.");
                else if (upper.ValueKind != JsonValueKind.Null)
                    Need(name == "firstDynasty" && Integer(milestone, "afterMonthExclusive") >= 0 && Integer(milestone, "byMonthInclusive") <= 6000 &&
                        Integer(milestone, "byMonthInclusive") - Integer(milestone, "afterMonthExclusive") == 120 && Text(milestone, "outcome") == "observed-in-interval", "기준 성립 관측 구간이 다릅니다.");
                else Need(Text(milestone, "outcome") == "not-formed-within-observed-horizon", "기준 부재를 미완료/성공으로 바꿀 수 없습니다.");
            }
        }
        if (full) Need(keys.SetEquals(from scenario in BaselineScenarios from seed in Seeds select scenario + ":" + seed), "서로 다른 a/b/c × 3seed 완주 9개가 필요합니다.");
    }

    internal static void SameLaws(JsonElement actual, IReadOnlyDictionary<string, long> expected)
    {
        Need(actual.ValueKind == JsonValueKind.Object && actual.EnumerateObject().Count() == expected.Count, "법칙 목록이 다릅니다.");
        foreach (var pair in expected) Need(Integer(actual, pair.Key) == pair.Value, "단일 비교 키 이외의 법칙이 다릅니다: " + pair.Key);
    }

    internal static (bool RequestedComplete, bool Complete, string Status) Completion(IReadOnlyList<(long Value, ulong Seed, string Status)> cases,
        long[] values, ulong? selectedSeed)
    {
        var expected = (from value in values from seed in Seeds where selectedSeed is null || seed == selectedSeed select (value, seed)).ToHashSet();
        Need(cases.Select(c => (c.Value, c.Seed)).Distinct().Count() == cases.Count && cases.All(c => expected.Contains((c.Value, c.Seed))), "민감도 사례 key가 중복/계획 밖입니다.");
        Need(cases.All(c => c.Status is "running" or "completed" or "failed"), "알 수 없는 민감도 사례 상태입니다.");
        var requested = cases.Count == expected.Count && cases.All(c => c.Status == "completed");
        var complete = requested && cases.Count == 6 && selectedSeed is null;
        return (requested, complete, cases.Any(c => c.Status == "failed") ? "failed" : complete ? "completed" : requested ? "requested-completed" : "partial");
    }

    internal static string CoreOutcome(long? core, LifeStatus status, bool completed) => core is not null ? "observed-exact-record" :
        status == LifeStatus.Dead ? "death-before-core" : status != LifeStatus.Alive ? "departure-before-core" :
        completed ? "right-censored-at-plan-horizon" : "not-yet-observed-partial";

    internal static JsonElement ReadJson(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        void Unique(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject()) { Need(names.Add(property.Name), "중복 JSON 속성은 거부합니다."); Unique(property.Value); }
            }
            else if (element.ValueKind == JsonValueKind.Array) foreach (var child in element.EnumerateArray()) Unique(child);
        }
        Unique(document.RootElement); return document.RootElement.Clone();
    }
    internal static string Digest(byte[] bytes) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
    internal static bool Hex(string? value, int length) => value is not null && value.Length == length && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static JsonElement Field(JsonElement element, string key) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) ? value : throw new InvalidDataException("필수 필드가 없습니다: " + key);
    internal static string Text(JsonElement element, string key) => Field(element, key).ValueKind == JsonValueKind.String ? Field(element, key).GetString()! : throw new InvalidDataException("문자열 필드가 아닙니다: " + key);
    internal static long Integer(JsonElement element, string key) => Field(element, key).ValueKind == JsonValueKind.Number && Field(element, key).TryGetInt64(out var value) ? value : throw new InvalidDataException("정수 필드가 아닙니다: " + key);
    internal static bool Boolean(JsonElement element, string key) => Field(element, key).ValueKind is JsonValueKind.True or JsonValueKind.False ? Field(element, key).GetBoolean() : throw new InvalidDataException("bool 필드가 아닙니다: " + key);
    internal static double Number(JsonElement element, string key) => Field(element, key).ValueKind == JsonValueKind.Number && Field(element, key).TryGetDouble(out var value) && double.IsFinite(value) && value >= 0 ? value : throw new InvalidDataException("유한한 음수 아닌 시간이 아닙니다: " + key);
    private static string Key(JsonElement row) => Text(row, "scenario") + ":" + Field(row, "seed").GetUInt64();
    internal static void Need(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
