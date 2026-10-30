#!/usr/bin/env python3
"""검증된 같은 소스의 완료 성장 cohort만 통합한다. 세계/성장 측정을 실행하지 않는다."""
import argparse
import copy
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import re
import statistics
import subprocess

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("release_contract", ROOT / "scripts/verify-v021-release.py")
VERIFY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VERIFY)
SEEDS = (42, 98, 2026)
KEYS = {(scenario, seed) for scenario in "abc" for seed in SEEDS}
MILESTONES = ("firstFounderDescendantBirth", "firstFounderDescendantCore", "firstAdditionalFamily", "firstSect", "firstDynasty", "firstMarket", "firstWorkshop", "firstSettlement")
COUNTS = ("population", "totalPeople", "founderDescendants", "everFounderDescendants", "generation", "livingGeneration",
          "goldenCoreOrHigher", "descendantGoldenCoreOrHigher", "mortal", "mortalWithoutMartial", "martial", "descendantMortal", "descendantMartial",
          "households", "families", "sects", "dynasties", "territorylessDynasties", "activeMarkets", "activeWorkshops", "activeSettlements",
          "births", "deaths", "intervalBirths", "intervalDeaths", "explicitArrivals")
RANGES = {"population": "population", "founderDescendants": "founderDescendants", "generation": "generation", "livingGeneration": "livingGeneration",
          "goldenCoreOrHigher": "goldenCoreOrHigher", "descendantCore": "descendantGoldenCoreOrHigher", "mortal": "mortal", "martial": "martial",
          "households": "households", "sects": "sects", "dynasties": "dynasties", "births": "births", "deaths": "deaths"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def integer(value, minimum=0):
    return type(value) is int and value >= minimum


def number(value):
    return type(value) in (int, float) and math.isfinite(value) and value >= 0


def key(row):
    require(isinstance(row, dict) and row.get("scenario") in "abc" and isinstance(row.get("scenario"), str) and
            len(row["scenario"]) == 1 and type(row.get("seed")) is int and row["seed"] in SEEDS, "알 수 없는 조건/seed")
    return row["scenario"], row["seed"]


def read(path):
    def unique(pairs):
        result = {}
        for name, value in pairs:
            require(name not in result, "중복 JSON key: " + name)
            result[name] = value
        return result
    def constant(value):
        raise ValueError("비유한 JSON 수치: " + value)
    payload = path if isinstance(path, bytes) else Path(path).read_bytes()
    return json.loads(payload.decode("utf-8"), object_pairs_hook=unique, parse_constant=constant)


def law_definitions(source):
    """현재의 literal D(...) registry만 해석한다. 새 표현식은 추측하지 않고 거절한다."""
    quoted = r'"(?:[^"\\]|\\.)*"'; numeric = r'-?\d[\d_]*[lL]?'
    pattern = re.compile(r'^\s*D\((' + quoted + r'),\s*' + quoted + r',\s*(' + quoted + r'),\s*' + numeric + r',\s*' + numeric +
                         r',\s*(' + numeric + r'),\s*(' + quoted + r')(?:,\s*(true|false))?\),?\s*$', re.M)
    matches = list(pattern.finditer(source))
    require(matches and len(matches) == len(re.findall(r'^\s*D\(', source, re.M)), "기본 법칙 registry를 완전히 해석하지 못했습니다")
    result = {}
    for match in matches:
        name, unit, value, reason, provisional = match.groups(); name = json.loads(name)
        require(name not in result, "중복 기본 법칙")
        result[name] = {"value": int(value.rstrip("lL").replace("_", "")), "unit": json.loads(unit),
                        "rationale": json.loads(reason), "provisional": provisional != "false"}
    return result


def distribution(values):
    values = sorted(values)
    return {"samples": len(values), "minimum": values[0] if values else None,
            "median": statistics.median(values) if values else None, "maximum": values[-1] if values else None}


def summarize(rows):
    result = []
    for scenario in sorted({r["scenario"] for r in rows}):
        cases = sorted((r for r in rows if r["scenario"] == scenario), key=lambda r: r["seed"])
        months = sorted({p["month"] for r in cases for p in r["timeSeries"]})
        curves = []
        for month in months:
            points = [p for r in cases for p in r["timeSeries"] if p["month"] == month]
            curves.append({"month": month, "observedSeeds": len(points), **{name: distribution([p[field] for p in points]) for name, field in RANGES.items()}})
        milestones = {}
        for name in MILESTONES:
            records = [r["milestoneOutcomes"][name] for r in cases]
            milestones[name] = {"exactObserved": sum(r["exactMonth"] is not None for r in records),
                                "intervalObserved": sum(r["byMonthInclusive"] is not None for r in records),
                                "absentWithinCompletedHorizon": sum(r["outcome"] == "not-formed-within-observed-horizon" for r in records),
                                "unknownOrPartial": sum(r["outcome"] in ("unknown-bounded-history", "not-yet-observed-partial") for r in records),
                                "exactMonthRange": distribution([r["exactMonth"] for r in records if r["exactMonth"] is not None]),
                                "scope": "observed fixed-seed counts only; interval/unknown outcomes are not zero months or probabilities"}
        completed = [r["seed"] for r in cases if r["status"] == "completed"]
        result.append({"scenario": scenario, "expectedSeeds": list(SEEDS), "startedSeeds": [r["seed"] for r in cases], "completedSeeds": completed,
                       "complete": len(completed) == len(SEEDS), "timeSeriesRanges": curves, "milestones": milestones})
    return result


def validate_cohort(row, definitions):
    scenario, _ = key(row)
    require(row.get("status") == "completed" and row.get("error") is None and row.get("observedThroughMonth") == 6000,
            "실패/미완료 cohort는 통합할 수 없습니다")
    require(row.get("selectedDefault") is (scenario == "a"), "선택 기본값/비교 조건 표시 불일치")
    expected = {name: data["value"] for name, data in definitions.items()}
    if scenario != "a":
        expected["birth.kinship"] = 0
    require(row.get("usedLaws") == expected and all(type(v) is int for v in row["usedLaws"].values()), "실행 소스 기본값/유일한 혈연 비교 외의 법칙 변경")
    policies = row.get("provisionalPolicies", [])
    require(isinstance(policies, list) and len(policies) == len({p.get("id") for p in policies}) and
            {p.get("id") for p in policies} == {name for name, data in definitions.items() if data["provisional"]}, "잠정 법칙 근거 누락/중복")
    for policy in policies:
        original = definitions[policy["id"]]
        require(type(policy.get("value")) is int and type(policy.get("defaultValue")) is int and
                policy["value"] == expected[policy["id"]] and policy["defaultValue"] == original["value"] and
                policy.get("unit") == original["unit"] and policy.get("rationale") == original["rationale"], "조작된 잠정 법칙")
    points = row.get("timeSeries", [])
    require(isinstance(points, list) and [p.get("month") for p in points] == list(range(0, 6001, 120)), "빈/누락/중복/변경된 관측행")
    previous = None
    for point in points:
        require(integer(point.get("month")) and all(integer(point.get(field)) for field in COUNTS) and number(point.get("simulationMs")) and number(point.get("caseElapsedMs")), "잘못된 실제 관측 값")
        arrivals = 2 if scenario == "b" and point["month"] >= 240 else 0
        require(point["explicitArrivals"] == arrivals and point["totalPeople"] == 2 + point["births"] + arrivals and
                point["population"] <= point["totalPeople"] and point["founderDescendants"] <= min(point["population"], point["everFounderDescendants"]) and
                point["everFounderDescendants"] <= point["births"] and point["livingGeneration"] <= point["generation"] <= point["everFounderDescendants"], "인구/혈연/세대/편입 회계 불일치")
        require(all(point[field] <= point["population"] for field in ("goldenCoreOrHigher", "mortal", "martial", "households")) and
                point["mortalWithoutMartial"] <= point["mortal"] and point["descendantGoldenCoreOrHigher"] <= min(point["founderDescendants"], point["goldenCoreOrHigher"]) and
                point["descendantMortal"] <= point["founderDescendants"] and point["descendantMartial"] <= point["founderDescendants"] and
                point["territorylessDynasties"] <= point["dynasties"], "경지/가계/조직 집계 불일치")
        require(point["intervalBirths"] == point["births"] - (previous["births"] if previous else 0) and
                point["intervalDeaths"] == point["deaths"] - (previous["deaths"] if previous else 0), "구간 출생/사망 불일치")
        if previous:
            require(all(point[field] >= previous[field] for field in ("births", "deaths", "totalPeople", "everFounderDescendants", "generation", "simulationMs", "caseElapsedMs")), "보존 이력/경과 시간 역행")
        previous = point
    require(points[0]["population"] == 2 and points[0]["births"] == points[0]["deaths"] == points[0]["sects"] == points[0]["dynasties"] == 0 and
            all(number(row.get(field)) for field in ("simulationMs", "commandMs", "caseElapsedMs")) and
            row["simulationMs"] == points[-1]["simulationMs"] and row["caseElapsedMs"] >= points[-1]["caseElapsedMs"] and
            re.fullmatch(r"[0-9a-f]{64}", row.get("finalHash", "")), "창세/최종 시점/실제 시간/hash 불일치")
    arrivals = row.get("externalArrivals", [])
    require(isinstance(arrivals, list) and len(arrivals) == (2 if scenario == "b" else 0), "비교 외부 편입 수 불일치")
    if arrivals:
        require({a.get("gestates") for a in arrivals} == {False, True} and len({a.get("person") for a in arrivals}) == 2, "편입 생식 프로필/신원 불일치")
    for arrival in arrivals:
        require(arrival.get("month") == 240 and integer(arrival.get("person"), 1) and arrival.get("species") == "human" and
                type(arrival.get("gestates")) is bool and
                arrival.get("born") == 240 - expected["birth.maturity"] and arrival.get("realm") == "Mortal" and
                all(type(arrival.get(field)) is int and arrival[field] == 0 for field in ("money", "items", "skills", "knowledge")), "외부 편입 조건/자산 조작")
    milestones = row.get("milestoneOutcomes", {})
    require(set(milestones) == set(MILESTONES), "성립/부재 근거 누락")
    for name, record in milestones.items():
        require(record.get("historyComplete") is True and isinstance(record.get("source"), str) and record["source"], "성립 근거 정확도 불일치")
        exact, lower, upper = record.get("exactMonth"), record.get("afterMonthExclusive"), record.get("byMonthInclusive")
        if exact is not None:
            require(name != "firstDynasty" and integer(exact) and exact <= 6000 and lower is None and upper is None and
                    integer(record.get("entity"), 1) and record.get("outcome") == "observed-exact-record", "정확 성립 시각 조작")
        elif upper is not None:
            require(name == "firstDynasty" and integer(lower) and integer(upper, 1) and upper <= 6000 and upper - lower == 120 and
                    upper % 120 == 0 and integer(record.get("entity"), 1) and record.get("outcome") == "observed-in-interval", "왕조 관측 구간 조작")
        else:
            require(lower is None and record.get("entity") is None and record.get("outcome") == "not-formed-within-observed-horizon", "미성립/부분 결과 재명명")
    generations = row.get("generationTimes", [])
    require(isinstance(generations, list) and [g.get("generation") for g in generations] == list(range(1, points[-1]["generation"] + 1)) and
            sum(g.get("births", -1) for g in generations) == points[-1]["everFounderDescendants"] and
            sum(g.get("living", -1) for g in generations) == points[-1]["founderDescendants"], "세대 관측 누락/합계 불일치")
    prior_first = None
    for generation in generations:
        require(integer(generation.get("births"), 1) and integer(generation.get("living")) and generation["living"] <= generation["births"] and
                integer(generation.get("firstBornMonth")) and integer(generation.get("lastBornMonth")) and
                generation["firstBornMonth"] <= generation["lastBornMonth"] <= 6000 and
                generation.get("monthsSincePreviousGenerationFirstBirth") == (None if prior_first is None else generation["firstBornMonth"] - prior_first), "세대 시간 불일치")
        for name in ("parentOneAgeAtBirthMonths", "parentTwoAgeAtBirthMonths"):
            ages = generation.get(name, {})
            require(ages.get("samples") == generation["births"] and all(number(ages.get(k)) for k in ("minimum", "median", "maximum")) and
                    0 < ages["minimum"] <= ages["median"] <= ages["maximum"], "부모 출산 연령 분포 불일치")
        prior_first = generation["firstBornMonth"]
    require(milestones["firstFounderDescendantBirth"]["exactMonth"] == (min(g["firstBornMonth"] for g in generations) if generations else None), "후손 첫 출생 근거 불일치")


def aggregate(documents, source, fingerprint, definitions):
    rows, seen, common = [], set(), None
    for document in documents:
        require(document.get("schema_version") == 2 and document.get("kind") == "seonro-growth-cohorts" and document.get("requested_complete") is True and
                document.get("runningCase") is None and document.get("horizonMonths") == 6000 and document.get("observationIntervalMonths") == 120 and
                document.get("plannedCases") == 9 and document.get("samplesPerScenario") == 3 and number(document.get("totalElapsedMs")), "부분/다른 기간/조건/미완료 성장 원본")
        env = document.get("environment", {})
        require(document.get("sourceCommit") == env.get("sourceCommit") == source and document.get("productSourceFingerprint") == fingerprint and
                env.get("sourceWorktreeClean") is True and env.get("buildConfiguration") == "Release" and env.get("engine") == "none" and env.get("platform") == "host", "다른/미확인 소스·제품 지문")
        require(all(isinstance(env.get(k), str) and env[k] for k in ("runtime", "os", "device", "architecture", "thermalState", "powerState")), "실행 환경 누락")
        cases = document.get("reports", []); requested = document.get("requestedCases", [])
        require(isinstance(cases, list) and cases and isinstance(requested, list) and requested and
                len({key(r) for r in cases}) == len(cases) and len({key(r) for r in requested}) == len(requested) and
                {key(r) for r in cases} == {key(r) for r in requested} and document.get("completedCases") == len(cases), "요청/완료 cohort key 불일치")
        requested_keys = {key(r) for r in requested}; selected_scenarios = {k[0] for k in requested_keys}; selected_seeds = {k[1] for k in requested_keys}
        require(len(selected_scenarios) in (1, 3) and len(selected_seeds) in (1, 3) and
                requested_keys == {(s, seed) for s in selected_scenarios for seed in selected_seeds}, "실제 선택 인수로 만들 수 없는 요청 범위")
        complete = {key(r) for r in cases} == KEYS
        require(document.get("complete") is complete and document.get("status") == ("completed" if complete else "requested-completed"), "선택 완주를 전체 완주로 재명명")
        for row in cases:
            validate_cohort(row, definitions); current = key(row)
            require(current not in seen, "중복 cohort key: " + str(current)); seen.add(current); rows.append(copy.deepcopy(row))
        require(document["totalElapsedMs"] >= sum(row["caseElapsedMs"] for row in cases), "원본 총시간이 실제 case 합계보다 작습니다")
        require(document.get("distributions") == summarize(cases), "원본 요약이 실제 관측행과 다릅니다")
        context = {name: document.get(name) for name in ("establishmentConditions", "timeModel", "completionMeaning", "limitations")}
        require(all(context.values()) and (common is None or context == common), "다른 성립/시간/한계 계약")
        common = context
    require(seen == KEYS, "서로 다른 완료 9개 cohort가 모두 필요합니다")
    rows.sort(key=key)
    return {"schema_version": 1, "kind": "seonro-growth-cohorts-aggregate", "complete": True, "requested_complete": True, "status": "completed",
            "sourceCommit": source, "productSourceFingerprint": fingerprint, "horizonMonths": 6000, "observationIntervalMonths": 120,
            "plannedCases": 9, "completedCases": 9, "samplesPerScenario": 3, "reports": rows, "distributions": summarize(rows), **common,
            "sumInputReportedElapsedMs": sum(d["totalElapsedMs"] for d in documents),
            "aggregationScope": "원본 cohort 사실만 병합. 입력 경과시간 합계는 병렬 실행의 전체 wall time이 아니며, 완료는 균형/생존 통과가 아니다."}


def self_test():
    source = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    laws = law_definitions(subprocess.check_output(["git", "show", source + ":src/Chronicles.Game/Laws.cs"], cwd=ROOT, text=True))
    fingerprint = VERIFY.fingerprint(source); documents = []
    for scenario, seed in sorted(KEYS):
        used = {name: data["value"] for name, data in laws.items()}; used["birth.kinship"] = 1 if scenario == "a" else 0
        points = []
        for month in range(0, 6001, 120):
            arrivals = 2 if scenario == "b" and month >= 240 else 0
            point = dict.fromkeys(COUNTS, 0)
            point.update(month=month, totalPeople=2 + arrivals, population=2 + arrivals, goldenCoreOrHigher=2,
                         mortal=arrivals, mortalWithoutMartial=arrivals, households=1, families=1, explicitArrivals=arrivals,
                         simulationMs=month, caseElapsedMs=month + 1)
            points.append(point)
        row = {"scenario": scenario, "seed": seed, "status": "completed", "error": None, "observedThroughMonth": 6000, "selectedDefault": scenario == "a",
               "usedLaws": used, "provisionalPolicies": [{"id": name, "value": used[name], "defaultValue": data["value"], "unit": data["unit"], "rationale": data["rationale"]}
                                                       for name, data in laws.items() if data["provisional"]],
               "timeSeries": points, "simulationMs": 6000, "commandMs": 0, "caseElapsedMs": 6001, "finalHash": "a" * 64, "generationTimes": [],
               "externalArrivals": [{"person": n + 100, "month": 240, "species": "human", "gestates": bool(n), "born": 240 - used["birth.maturity"], "realm": "Mortal",
                                     "money": 0, "items": 0, "skills": 0, "knowledge": 0} for n in range(2)] if scenario == "b" else [],
               "milestoneOutcomes": {name: {"historyComplete": True, "source": "validator fixture only", "exactMonth": None, "afterMonthExclusive": None,
                                             "byMonthInclusive": None, "entity": None, "outcome": "not-formed-within-observed-horizon"} for name in MILESTONES}}
        document = {"schema_version": 2, "kind": "seonro-growth-cohorts", "sourceCommit": source, "productSourceFingerprint": fingerprint,
                    "environment": {"sourceCommit": source, "sourceWorktreeClean": True, "buildConfiguration": "Release", "engine": "none", "platform": "host",
                                    **dict.fromkeys(("runtime", "os", "device", "architecture", "thermalState", "powerState"), "validator fixture only")},
                    "requested_complete": True, "requestedCases": [{"scenario": scenario, "seed": seed}], "complete": False, "status": "requested-completed", "runningCase": None,
                    "horizonMonths": 6000, "observationIntervalMonths": 120, "plannedCases": 9, "samplesPerScenario": 3, "completedCases": 1,
                    "totalElapsedMs": 6002, "reports": [row], "distributions": summarize([row]),
                    **dict.fromkeys(("establishmentConditions", "timeModel", "completionMeaning", "limitations"), "validator fixture only")}
        documents.append(document)
    result = aggregate(documents, source, fingerprint, laws)
    require(len(result["reports"]) == 9 and result["distributions"][0]["milestones"]["firstSect"]["absentWithinCompletedHorizon"] == 3 and
            result["distributions"][0]["milestones"]["firstSect"]["exactMonthRange"]["samples"] == 0, "부재를 0개월로 바꾸면 안 됩니다")
    def pack(inputs):
        report = copy.deepcopy(inputs[0]); report["reports"] = [copy.deepcopy(r) for document in inputs for r in document["reports"]]
        report["requestedCases"] = [{"scenario": r["scenario"], "seed": r["seed"]} for r in report["reports"]]
        report["completedCases"] = len(report["reports"]); report["complete"] = len(report["reports"]) == 9
        report["totalElapsedMs"] = sum(document["totalElapsedMs"] for document in inputs)
        report["status"] = "completed" if report["complete"] else "requested-completed"
        report["distributions"] = summarize(report["reports"])
        return report
    for inputs in ([pack(documents)], [pack([d for d in documents if d["reports"][0]["scenario"] == s]) for s in "abc"],
                   [pack([d for d in documents if d["reports"][0]["seed"] == seed]) for seed in SEEDS]):
        require(aggregate(inputs, source, fingerprint, laws)["distributions"] == result["distributions"], "분담 방식에 따른 요약 불일치")
    negative = [lambda x: x.pop(), lambda x: x.append(copy.deepcopy(x[0])), lambda x: x[0].update(requested_complete=False),
                lambda x: x[0].update(complete=True), lambda x: x[0].update(productSourceFingerprint="b" * 64),
                lambda x: x[0].update(sourceCommit="c" * 40), lambda x: x[0]["reports"][0]["usedLaws"].update({"ecology.foodUse": 0}),
                lambda x: x[0]["reports"][0].update(status="failed"), lambda x: x[0]["reports"][0].update(timeSeries=[]),
                lambda x: x[0]["reports"][0]["timeSeries"].pop(), lambda x: x[0]["reports"][0]["timeSeries"][1].update(month=0),
                lambda x: x[0]["distributions"][0]["milestones"]["firstSect"].update(absentWithinCompletedHorizon=0),
                lambda x: x[0].update(horizonMonths=120), lambda x: x[3]["reports"][0]["externalArrivals"][0].update(money=1),
                lambda x: x.__setitem__(slice(0, 2), [pack(x[:2])]), lambda x: x[0].update(requested_complete=1),
                lambda x: x[0]["reports"][0]["usedLaws"].update({"currency.stoneValue": 0}),
                lambda x: [d["reports"][0]["usedLaws"].update({"ecology.foodUse": 0}) for d in x]]
    for mutation in negative:
        candidate = copy.deepcopy(documents); mutation(candidate)
        try:
            aggregate(candidate, source, fingerprint, laws)
        except ValueError:
            continue
        raise ValueError("통합 부정 계약 검사 실패")
    print(json.dumps({"growth_aggregation_self_test": "passed", "negative_cases": len(negative), "actual_growth_or_performance_claim": False}))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("inputs", nargs="*"); parser.add_argument("--output"); parser.add_argument("--source"); parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    if args.self_test:
        self_test(); return
    require(args.inputs and args.output, "원본 입력들과 별도 --output을 지정하세요")
    paths = [Path(name).resolve() for name in args.inputs]; output = Path(args.output).resolve()
    require(len(set(paths)) == len(paths) and output not in paths, "원본/중복 입력을 덮어쓸 수 없습니다")
    payloads = [path.read_bytes() for path in paths]
    documents = [read(payload) for payload in payloads]; source = args.source or documents[0].get("sourceCommit", "")
    require(re.fullmatch(r"[0-9a-f]{40}", source) is not None, "실제 실행 source commit이 필요합니다")
    fingerprint = VERIFY.fingerprint(source)
    laws = law_definitions(subprocess.check_output(["git", "show", source + ":src/Chronicles.Game/Laws.cs"], cwd=ROOT, text=True))
    result = aggregate(documents, source, fingerprint, laws)
    result["inputArtifacts"] = [{"path": str(path), "sha256": hashlib.sha256(payload).hexdigest(),
                                 "environment": document["environment"], "cohortKeys": [list(key(row)) for row in document["reports"]]}
                                for path, payload, document in zip(paths, payloads, documents)]
    output.parent.mkdir(parents=True, exist_ok=True); pending = output.with_name(output.name + ".pending")
    pending.write_text(json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False) + "\n", encoding="utf-8"); pending.replace(output)
    print(json.dumps({"aggregate_complete": True, "cohorts": 9, "sourceCommit": source, "actual_measurement_run_by_aggregator": False}))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, TypeError, OSError, subprocess.CalledProcessError) as error:
        raise SystemExit("성장 원본 통합 거절: " + str(error)) from error
