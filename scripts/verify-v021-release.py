#!/usr/bin/env python3
"""Fail-closed provenance/evidence/APK checks for the owner-run 0.2.1 release.

This verifier never signs, reads credentials, runs performance measurements, or
publishes. Evidence-only commits do not change the product source fingerprint.
"""
import argparse
import copy
from datetime import datetime
import hashlib
import json
import math
import os
from pathlib import Path
import re
import subprocess
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[1]
REPOSITORY = "hddy774/project-hora"
PACKAGE = "org.hora.xianxiaobserver"
CERTIFICATE = "0832f24441f1caf335166fbc16ae0b9536e262ca48f258926a8027fc31dca25a"
WORKFLOW = ".github/workflows/v021-release.yml"
OWNER = "hddy774"
RELEASE_COMMAND = "/선로만상록 0.2.1 릴리스"
RELEASE_PR = 84
READINESS = "docs/evidence/0.2.1/release-readiness.json"
RELEASE_ONLY = {"scripts/verify-v021-release.py", "scripts/v021-release-emulator.py", "scripts/publish-v021-release.py"}
PERFORMANCE_POLICY = "v021-revision6-section11-T04"
LEGACY_SPEED_HISTORY = "docs/evidence/0.2.1/soak.json"
LEGACY_SPEED_HISTORY_SHA256 = "7560f882822979077b51f1ec97a6596f143c1870836515aceb9ad8a252749da8"
PERFORMANCE_METRICS = ("monthlyP95Ms", "queryMs", "saveMs", "loadMs", "allocatedBytes", "saveBytes", "retainedManagedBytes", "serializedCognitionBytes")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def run(*arguments, binary=False, check=True):
    result = subprocess.run([str(a) for a in arguments], cwd=ROOT, capture_output=True, check=False)
    if check:
        require(result.returncode == 0, "Command failed: " + str(arguments[0]))
    if not check:
        return result
    return result.stdout if binary else result.stdout.decode("utf-8", errors="strict")


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def read(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, "Duplicate JSON key: " + key)
            result[key] = value
        return result
    return json.loads(Path(path).read_text(encoding="utf-8"), object_pairs_hook=unique)


def write(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def version():
    value = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    require(re.fullmatch(r"\d+\.\d+\.\d+", value) is not None, "VERSION must be semantic numeric version")
    require(value == "0.2.1", "This workflow is only for VERSION 0.2.1")
    major, minor, patch = map(int, value.split("."))
    return value, major * 1000000 + minor * 1000 + patch


def product_path(name):
    roots = {"VERSION", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "global.json",
             "NuGet.config", "NuGet.Config", "Chronicles.slnx", "package.json", "package-lock.json", ".github/workflows/v021-product.yml"}
    return name in roots or name.startswith(("src/", "tests/")) or (name.startswith("scripts/") and name not in RELEASE_ONLY)


def fingerprint(commit="HEAD"):
    require(re.fullmatch(r"HEAD|[0-9a-f]{40}", commit) is not None, "Invalid fingerprint commit")
    paths = run("git", "ls-tree", "-rz", "--name-only", commit, binary=True).split(b"\0")
    digest = hashlib.sha256()
    selected = sorted(p.decode("utf-8") for p in paths if p and product_path(p.decode("utf-8")))
    require(selected and "VERSION" in selected, "Missing product sources")
    for name in selected:
        blob = run("git", "show", commit + ":" + name, binary=True)
        digest.update(name.encode("utf-8") + b"\0" + hashlib.sha256(blob).digest())
    return digest.hexdigest()


def input_boolean(inputs, name, default):
    value = inputs.get(name, default)
    require(type(value) is bool or type(value) is str and value in ("true", "false"), "Invalid release boolean input: " + name)
    return value is True or value == "true"


def release_timestamp(value):
    require(isinstance(value, str), "Release timestamp missing")
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    require(parsed.utcoffset() is not None, "Release timestamp must have a timezone")
    return parsed


def owner_comment(comment):
    require(isinstance(comment, dict) and type(comment.get("id")) is int and comment["id"] > 0,
            "Owner release comment identity missing")
    require(comment.get("body") == RELEASE_COMMAND and comment.get("user", {}).get("login") == OWNER and
            comment.get("author_association") == "OWNER", "Exact repository-owner release command required")
    require(comment.get("issue_url") == "https://api.github.com/repos/" + REPOSITORY + "/issues/84" and
            comment.get("html_url") == "https://github.com/" + REPOSITORY + "/pull/84#issuecomment-" + str(comment["id"]),
            "Release comment belongs to a different pull request")
    created = comment.get("created_at")
    require(isinstance(created, str) and comment.get("updated_at") == created, "Edited release requests are rejected")
    release_timestamp(created)
    return comment["id"], created


def release_request_context(environment=None, event=None):
    environment = os.environ if environment is None else environment
    require(environment.get("GITHUB_ACTIONS") == "true" and environment.get("GITHUB_REPOSITORY") == REPOSITORY,
            "Release operations require this repository's GitHub Actions")
    require(environment.get("GITHUB_ACTOR") == OWNER and environment.get("GITHUB_TRIGGERING_ACTOR") == OWNER,
            "Repository owner must request and rerun release")
    require(environment.get("GITHUB_REF") == "refs/heads/main", "Release must run from merged main")
    commit = environment.get("GITHUB_SHA", "")
    require(re.fullmatch(r"[0-9a-f]{40}", commit) is not None and
            environment.get("GITHUB_WORKFLOW_SHA") == commit and
            environment.get("GITHUB_WORKFLOW_REF") == REPOSITORY + "/" + WORKFLOW + "@refs/heads/main",
            "Exact main release workflow/source required")
    require(all(re.fullmatch(r"[1-9][0-9]*", environment.get(key, "")) is not None
                for key in ("GITHUB_RUN_ID", "GITHUB_RUN_ATTEMPT")), "Actual run and attempt identifiers required")
    name = environment.get("GITHUB_EVENT_NAME")
    require(name in ("workflow_dispatch", "issue_comment"), "Unsupported actual release event")
    if event is None:
        require(environment.get("GITHUB_EVENT_PATH"), "Actual GitHub event payload required")
        event = read(environment["GITHUB_EVENT_PATH"])
    require(isinstance(event, dict) and event.get("repository", {}).get("full_name") == REPOSITORY and
            event.get("sender", {}).get("login") == OWNER, "Actual owner/repository event payload required")
    request = {"event": name, "mode": "manual", "source_commit": commit,
               "publish": False, "verify_upgrade": True, "pull_request": None,
               "comment_id": None, "comment_created_at": None, "command": None}
    if name == "workflow_dispatch":
        require(event.get("ref") in ("main", "refs/heads/main") and "issue" not in event and "comment" not in event,
                "Dispatch payload cannot impersonate a comment request")
        inputs = event.get("inputs", {})
        require(isinstance(inputs, dict), "Manual release inputs missing")
        request.update(publish=input_boolean(inputs, "publish", False),
                       verify_upgrade=input_boolean(inputs, "verify_upgrade", True))
    else:
        issue = event.get("issue", {})
        require(event.get("action") == "created" and "inputs" not in event and
                type(issue.get("number")) is int and issue["number"] == RELEASE_PR and issue.get("state") == "closed" and
                issue.get("pull_request", {}).get("url") == "https://api.github.com/repos/" + REPOSITORY + "/pulls/84",
                "A created command on merged main PR84 is required")
        identifier, created = owner_comment(event.get("comment"))
        request.update(mode="owner-comment", publish=True, verify_upgrade=True, pull_request=RELEASE_PR,
                       comment_id=identifier, comment_created_at=created, command=RELEASE_COMMAND)
    return request


def remote_request_context(request, main_ref, pull_request=None, comment=None):
    require(isinstance(main_ref, dict) and main_ref.get("ref") == "refs/heads/main" and main_ref.get("object", {}).get("type") == "commit" and
            main_ref["object"].get("sha") == request["source_commit"], "Main changed after the release request")
    if request["mode"] == "manual":
        return None
    require(isinstance(pull_request, dict) and type(pull_request.get("number")) is int and pull_request["number"] == RELEASE_PR and
            pull_request.get("merged") is True and pull_request.get("state") == "closed" and
            pull_request.get("base", {}).get("ref") == "main" and
            pull_request.get("base", {}).get("repo", {}).get("full_name") == REPOSITORY and
            pull_request.get("url") == "https://api.github.com/repos/" + REPOSITORY + "/pulls/84",
            "PR84 must actually be merged into this repository's main")
    merge = pull_request.get("merge_commit_sha", "")
    merged_at = pull_request.get("merged_at")
    require(re.fullmatch(r"[0-9a-f]{40}", merge) is not None and isinstance(merged_at, str), "Actual PR84 merge evidence missing")
    identifier, created = owner_comment(comment)
    require(identifier == request["comment_id"] and created == request["comment_created_at"],
            "The actual original owner request changed")
    require(release_timestamp(merged_at) <= release_timestamp(created),
            "Release command must be created after PR84 merged")
    require(request["publish"] is True and request["verify_upgrade"] is True and request["command"] == RELEASE_COMMAND,
            "Comment release requires publication and retained-signature upgrade")
    return merge


def github_read(path):
    require(os.environ.get("GH_TOKEN"), "Current Actions token required for release provenance reads")
    return json.loads(run("gh", "api", "repos/" + REPOSITORY + "/" + path))


def owner_context():
    request = release_request_context()
    require(run("git", "rev-parse", "HEAD").strip() == request["source_commit"], "Checkout is not the exact requested source")
    pull, comment = None, None
    if request["mode"] == "owner-comment":
        pull = github_read("pulls/84")
        comment = github_read("issues/comments/" + str(request["comment_id"]))
    merge = remote_request_context(request, github_read("git/ref/heads/main"), pull, comment)
    if merge is not None:
        run("git", "merge-base", "--is-ancestor", merge, request["source_commit"])
    return request


def actual_run_context(source, request, metadata):
    require(source.get("request") == request and source.get("event") == request["event"] and request["publish"] is True,
            "Publisher must use the same actual publication request")
    require(metadata.get("event") == request["event"] and metadata.get("actor", {}).get("login") == OWNER and
            metadata.get("triggering_actor", {}).get("login") == OWNER and
            metadata.get("head_repository", {}).get("full_name") == REPOSITORY and
            metadata.get("repository", {}).get("full_name") == REPOSITORY and
            metadata.get("head_sha") == source["source_commit"] == request["source_commit"] and
            metadata.get("head_branch") == "main" and metadata.get("path") == WORKFLOW and
            type(metadata.get("run_attempt")) is int and metadata["run_attempt"] > 0 and str(metadata["run_attempt"]) == source["run_attempt"] and
            type(metadata.get("id")) is int and metadata["id"] > 0 and str(metadata["id"]) == source["run_id"],
            "Actual release event/owner/run/attempt/main source mismatch")
    if request["mode"] == "owner-comment":
        require(request["verify_upgrade"] is True, "Comment publication cannot omit the upgrade check")


def source_record():
    request = release_request_context()
    v, code = version()
    commit = run("git", "rev-parse", "HEAD").strip()
    require(re.fullmatch(r"[0-9a-f]{40}", commit) is not None, "Missing source commit")
    require(commit == request["source_commit"], "Source changed after release request verification")
    require(not run("git", "status", "--porcelain", "--untracked-files=no").strip(), "Tracked release checkout must be clean")
    return {"schema_version": 1, "repository": REPOSITORY, "source_commit": commit,
            "product_source_fingerprint": fingerprint(), "version": v, "version_code": code, "application_id": PACKAGE,
            "certificate_sha256": CERTIFICATE, "tag": "xianxia-observer/v" + v,
            "apk_name": "SeonroMansangrok-v" + v + ".apk", "release_name": "선로만상록 v" + v,
            "workflow": WORKFLOW, "event": request["event"], "request": request,
            "run_id": os.environ.get("GITHUB_RUN_ID"), "run_attempt": os.environ.get("GITHUB_RUN_ATTEMPT")}


def check_source(record):
    current = source_record()
    for key in ("schema_version", "repository", "source_commit", "product_source_fingerprint", "version", "version_code",
                "application_id", "certificate_sha256", "tag", "apk_name", "workflow", "event", "request", "run_id", "run_attempt"):
        require(record.get(key) == current[key], "Source/run mismatch: " + key)
    return current


def evidence_file(path, expected):
    require(isinstance(path, str) and path.startswith("docs/evidence/0.2.1/") and ".." not in Path(path).parts,
            "Evidence path must be a checked-in 0.2.1 report")
    file = ROOT / path
    require(file.is_file() and re.fullmatch(r"[0-9a-f]{64}", expected or "") is not None and sha(file) == expected,
            "Evidence missing or checksum mismatch: " + str(path))
    return read(file)


def functional_report(report):
    require(isinstance(report, dict) and report.get("selection") == "all",
            "A selected subset is not a complete functional acceptance report")
    require(isinstance(report, dict) and type(report.get("total")) is int and report["total"] > 0 and
            type(report.get("failed")) is int and report["failed"] == 0,
            "Functional suite did not pass")
    require(isinstance(report.get("results"), list) and len(report["results"]) == report["total"] and
            all(r.get("passed") is True for r in report["results"]), "Every functional scenario needs a real passing result")


def browser_report(reports):
    require(isinstance(reports, list), "Browser report list missing")
    for width in (1280, 390):
        require(any(r.get("kind") == "real_session_functional" and r.get("width") == width and r.get("javascript_errors") == [] and
                    r.get("document_horizontal_overflow") is False and r.get("selected_known_path_command") is True and
                    r.get("save_load_scope_cleanup") is True and r.get("law_export_import_validation_and_apply") is True for r in reports),
                "Actual browser bridge/action/save checks missing for width " + str(width))


def feature_gate(source, readiness_path=READINESS):
    ready = read(ROOT / readiness_path)
    require(ready.get("schema_version") == 1 and ready.get("repository") == REPOSITORY and ready.get("version") == source["version"],
            "Readiness contract mismatch")
    require(ready.get("feature_functional_complete") is True, "Feature audit is not complete; signing is disabled")
    require(ready.get("product_source_fingerprint") == source["product_source_fingerprint"], "Feature evidence is for different product sources")
    audited = ready.get("audited_source_commit")
    require(re.fullmatch(r"[0-9a-f]{40}", audited or "") is not None, "Audited source commit missing")
    run("git", "merge-base", "--is-ancestor", audited, source["source_commit"])
    require(fingerprint(audited) == source["product_source_fingerprint"], "Audited commit has different product inputs")
    ledger = ROOT / "docs/plans/v021-tasks.json"
    require(sha(ledger) == ready.get("task_ledger_sha256"), "Task ledger differs from audited ledger")
    tasks = read(ledger)["tasks"]
    features = [t for t in tasks if t.get("kind") == "feature"]
    require(features and all(t.get("status") == "done" for t in features), "All feature acceptance tasks must be complete")
    # T06 is the release itself. Requiring it to be done here would make publication impossible.
    require("V021-T06" not in {t["id"] for t in features}, "Release task must remain outside the pre-release feature gate")
    functional_report(evidence_file(ready.get("functional_report"), ready.get("functional_report_sha256")))
    return ready


def finite_number(value, positive=False):
    return type(value) in (int, float) and math.isfinite(value) and (value > 0 if positive else value >= 0)


def measurement_rows(report):
    require(isinstance(report, dict), "Population measurement report is missing")
    require(report.get("schema_version") == 2 and report.get("kind") == "seonro-population-baseline" and report.get("complete") is True,
            "Current declared population measurement schema is required")
    environment = report.get("environment", {})
    require(environment.get("buildConfiguration") == "Release" and environment.get("sourceWorktreeClean") is True and environment.get("engine") == "none" and
            environment.get("platform") == "host" and re.fullmatch(r"[0-9a-f]{40}", environment.get("sourceCommit", "")) and
            all(isinstance(environment.get(k), str) and environment[k].strip() for k in
                ("device", "runtime", "os", "architecture", "thermalState", "powerState", "precedingFunctionalSelection", "processScope")),
            "Record source, Release build, host/runtime/device and explicit thermal/power scope")
    require(type(environment.get("cpu")) is int and environment["cpu"] > 0 and finite_number(environment.get("totalAvailableMemoryBytes"), positive=True),
            "Actual processor count and runtime-available memory are required")
    require(finite_number(report.get("totalElapsedSeconds"), positive=True), "Total measurement time is missing")
    rows = report.get("reports", [])
    require(isinstance(rows, list) and len(rows) == len({r.get("population") for r in rows}) and
            {r.get("population") for r in rows} >= {2, 100, 1000, 10000}, "Four population baselines, including 10,000, are required")
    require(set(report.get("completedSizes", [])) == {r["population"] for r in rows}, "Partial/incremental population reports cannot pass")
    for row in rows:
        require(type(row.get("population")) is int and row["population"] >= 2, "Measured initial population is invalid")
        samples = row.get("monthlySamplesMs", [])
        require(type(row.get("measuredMonths")) is int and row["measuredMonths"] > 0 and
                type(row.get("repetitions")) is int and row["repetitions"] > 0 and
                type(row.get("warmupMonths")) is int and row["warmupMonths"] > 0 and
                len(samples) == row["measuredMonths"] * row["repetitions"] and all(finite_number(v) for v in samples),
                "Actual monthly distribution, warmup and repetition counts are required")
        require(all(finite_number(row.get(k)) for k in PERFORMANCE_METRICS), "Latency, allocations, memory and save-size measurements are required")
        require(all(type(row.get(k)) is int and row[k] > 0 for k in ("saveBytes", "retainedManagedBytes", "serializedCognitionBytes")) and
                type(row.get("openingRetainedManagedBytes")) is int and row["openingRetainedManagedBytes"] > 0,
                "Retained managed memory and serialized bytes must be actual positive byte counts")
        require(math.isclose(row["monthlyP95Ms"], sorted(samples)[math.ceil(len(samples) * .95) - 1], rel_tol=1e-9, abs_tol=1e-9),
                "Monthly p95 does not match the recorded samples")
        fixture = row.get("fixture", {})
        require(fixture.get("population") == row["population"] and fixture.get("founders") == 2 and
                fixture.get("syntheticEntrants") == row["population"] - 2 and isinstance(row.get("usedLaws"), dict) and row["usedLaws"],
                "Disclose the real initial roster, synthetic status and laws")
        require(type(row.get("workload", {}).get("peopleWithDecisions")) is int and row["workload"]["peopleWithDecisions"] > 0,
                "A live AI workload cannot be replaced by an inert population")
        cognition = row.get("cognition", {})
        require(type(cognition.get("people")) is int and cognition["people"] >= row["population"] and
                finite_number(cognition.get("beliefBudgetPerPerson"), positive=True) and finite_number(cognition.get("activeClaimBudgetPerPerson"), positive=True) and
                finite_number(cognition.get("maximumBudgetUnitsPerPerson")) and cognition["maximumBudgetUnitsPerPerson"] <= cognition["beliefBudgetPerPerson"] and
                cognition.get("peopleExceedingBeliefBudget") == 0 and cognition.get("peopleExceedingClaimBudget") == 0 and
                cognition.get("serializedBytes") == row["serializedCognitionBytes"] and isinstance(cognition.get("serializedScope"), str) and
                isinstance(row.get("retainedMemoryScope"), str) and row["retainedMemoryScope"].strip(), "L04 budget, collected-memory and cognition-byte scopes are required")
        for name in ("beliefs", "storedClaims", "activeClaims", "peopleObservations", "placeObservations", "sourceObservations", "knownPeople", "knownPlaces"):
            count, maximum = cognition.get("totals", {}).get(name), cognition.get("maximumPerPerson", {}).get(name)
            require(type(count) is int and type(maximum) is int and 0 <= maximum <= count <= maximum * cognition["people"],
                    "L04 actual totals and per-person maxima are inconsistent: " + name)
        require(cognition["maximumPerPerson"]["activeClaims"] <= cognition["activeClaimBudgetPerPerson"], "Active personal claims exceeded their real budget")
        require(cognition["totals"]["activeClaims"] <= cognition["totals"]["storedClaims"] and
                cognition["maximumPerPerson"]["activeClaims"] <= cognition["maximumPerPerson"]["storedClaims"], "Active claims must be a subset of stored claims")
        counted = ("beliefs", "activeClaims", "peopleObservations", "placeObservations", "sourceObservations")
        maximum = cognition["maximumBudgetUnitsPerPerson"]
        require(max(cognition["maximumPerPerson"][k] for k in counted) <= maximum <= sum(cognition["maximumPerPerson"][k] for k in counted) and
                sum(cognition["totals"][k] for k in counted) <= maximum * cognition["people"], "Actual cognition budget-unit totals/maxima disagree")
    return {row["population"]: row for row in rows}


def canonical_performance_reports(baseline, measured, budget, plan, stability, baseline_sha, plan_sha):
    """Pure evidence-contract checks. This function never executes a simulation or a benchmark."""
    before, after = measurement_rows(baseline), measurement_rows(measured)
    require(budget.get("schema_version") == 1 and budget.get("kind") == "seonro-performance-budget" and
            budget.get("policy") == PERFORMANCE_POLICY and budget.get("established_after_baseline") is True and
            budget.get("baseline_report_sha256") == baseline_sha and isinstance(budget.get("rationale"), str) and budget["rationale"].strip(),
            "Numerical budgets must be documented after and tied to the actual baseline")
    regression = budget.get("maximum_regression_percent")
    require(finite_number(regression), "A measured-baseline regression allowance must be explicit")
    for population, row in after.items():
        limits = budget.get("limits", {}).get(str(population), {})
        require(population in before, "A measured population has no comparison baseline")
        for name in PERFORMANCE_METRICS:
            require(finite_number(limits.get(name), positive=True), "Missing post-baseline population budget: " + name)
            require(row[name] <= limits[name] and row[name] <= before[population][name] * (1 + regression / 100),
                    "Actual population measurement exceeds the adopted budget/regression: " + name)
    require(plan.get("schemaVersion") == 1 and plan.get("policyId") == PERFORMANCE_POLICY and
            plan.get("baselineReportSha256") == baseline_sha and isinstance(plan.get("rationale"), str) and plan["rationale"].strip(),
            "A documented post-baseline long-run workload plan is required")
    cases = plan.get("cases", [])
    require(isinstance(cases, list) and len(cases) == len({c.get("population") for c in cases}) and
            any(c.get("population") == 10000 for c in cases), "Long-run stability must include the 10,000-person initial fixture")
    require(stability.get("schema_version") == 1 and stability.get("kind") == "seonro-long-run-stability" and
            stability.get("policy") == PERFORMANCE_POLICY and stability.get("passed") is True and
            stability.get("complete") is True and stability.get("status") == "completed" and
            stability.get("runningPopulation") is None and stability.get("executionFailure") is None and
            stability.get("baselineReportSha256") == baseline_sha and stability.get("planSha256") == plan_sha and
            finite_number(stability.get("totalElapsedSeconds"), positive=True), "Actual planned long-run stability did not complete")
    env = stability.get("environment", {})
    require(env.get("buildConfiguration") == "Release" and env.get("sourceWorktreeClean") is True and env.get("platform") == "host" and env.get("engine") == "none" and
            all(isinstance(env.get(k), str) and env[k].strip() for k in
                ("sourceCommit", "device", "runtime", "os", "architecture", "thermalState", "powerState", "precedingFunctionalSelection", "processScope")), "Long-run environment/scope is missing")
    require(type(env.get("cpu")) is int and env["cpu"] > 0 and finite_number(env.get("totalAvailableMemoryBytes"), positive=True),
            "Long-run processor and memory environment is missing")
    rows = stability.get("reports", [])
    require(len(rows) == len(cases) and {r.get("population") for r in rows} == {c["population"] for c in cases}, "Long-run cases are incomplete or duplicated")
    require(isinstance(stability.get("finishedCases"), list) and len(stability["finishedCases"]) == len(cases) and
            set(stability["finishedCases"]) == {c["population"] for c in cases}, "A completion flag cannot replace actual finished stability cases")
    require(all(finite_number(r.get("elapsedSeconds"), positive=True) for r in rows) and
            stability["totalElapsedSeconds"] >= sum(r["elapsedSeconds"] for r in rows), "Record the actual total long-run time and every case")
    for case in cases:
        population = case["population"]; row = next(r for r in rows if r["population"] == population)
        horizon, interval, validation = case.get("horizonMonths"), case.get("checkpointEveryMonths"), case.get("validateEveryMonths")
        require(population in before and type(horizon) is int and horizon > before[population]["measuredMonths"] and
                type(interval) is int and 0 < interval <= horizon // 2 and type(validation) is int and 0 < validation <= interval and
                finite_number(case.get("wallTimeLimitSeconds"), positive=True), "Long-run horizon/checkpoint/invariant/time plan is invalid")
        require(row.get("passed") is True and row.get("failure") is None and row.get("targetMonths") == horizon and
                row.get("completedMonths") == horizon and finite_number(row.get("elapsedSeconds"), positive=True) and
                row["elapsedSeconds"] <= case["wallTimeLimitSeconds"] and type(row.get("boundaryChecks")) is int and
                row["boundaryChecks"] >= horizon // validation, "A failed, timed-out or incomplete stability case cannot pass")
        checkpoints = row.get("checkpoints", [])
        require(len(checkpoints) >= 3 and checkpoints[0].get("month") == 0 and checkpoints[-1].get("month") == horizon and
                all(a["month"] < b["month"] and b["month"] - a["month"] <= interval for a, b in zip(checkpoints, checkpoints[1:])),
                "Long-run I/O/resume samples must span the declared horizon")
        limits = budget.get("stability_limits", {}).get(str(population), {})
        for checkpoint in checkpoints:
            require(all(checkpoint.get(k) is True for k in ("invariantValid", "saveResumeEqual", "queryUnchanged", "publicCommandAccepted")) and
                    type(checkpoint.get("people")) is int and checkpoint["people"] >= population and
                    type(checkpoint.get("living")) is int and 0 <= checkpoint["living"] <= checkpoint["people"] and
                    type(checkpoint.get("births")) is int and checkpoint["births"] >= 0 and
                    type(checkpoint.get("deaths")) is int and checkpoint["deaths"] >= 0 and checkpoint["people"] == population + checkpoint["births"] and
                    re.fullmatch(r"[0-9a-f]{64}", checkpoint.get("hash", "")) is not None,
                    "Actual boundary, public I/O and atomic save/resume checks must pass; natural population change is allowed")
            for name in ("retainedManagedBytes", "saveBytes"):
                require(finite_number(limits.get(name), positive=True) and finite_number(checkpoint.get(name)) and checkpoint[name] <= limits[name],
                        "Long-run memory/save growth exceeded its documented budget: " + name)
        require(row.get("living") == checkpoints[-1]["living"] and row.get("births") == checkpoints[-1]["births"] and
                row.get("deaths") == checkpoints[-1]["deaths"] and row.get("finalHash") == checkpoints[-1]["hash"] and
                all(a["births"] <= b["births"] and a["deaths"] <= b["deaths"] for a, b in zip(checkpoints, checkpoints[1:])),
                "Natural demographic outcomes must be retained consistently")
        require(type(row.get("workload", {}).get("peopleWithDecisions")) is int and row["workload"]["peopleWithDecisions"] > 0,
                "Long-run evidence must retain actual AI activity")


def legacy_speed_comparison(report):
    require(report.get("population") == 10000 and report.get("targetMonths") == 7200 and report.get("targetSpeed") == 100 and
            finite_number(report.get("elapsedSeconds"), positive=True) and type(report.get("completedMonths")) is int and
            0 <= report["completedMonths"] <= 7200 and type(report.get("living")) is int and report["living"] >= 0 and
            finite_number(report.get("achievedSpeed")), "Preserve the original V020-G03 comparison scope")
    speed = report["completedMonths"] / 12 * 10 / report["elapsedSeconds"]
    met = report["completedMonths"] == 7200 and report["living"] == 10000 and report["elapsedSeconds"] <= 60 and speed >= 100
    require(math.isclose(report["achievedSpeed"], speed, rel_tol=1e-9) and report.get("passed") is met,
            "The legacy comparison result must not be relabelled or recalculated as a pass")
    return met  # A truthful historical miss is retained; it is not the revision-6 numerical gate.


def performance_gate(source, ready):
    require(ready.get("final_performance_complete") is True and ready.get("publication_enabled") is True,
            "Final performance/publication readiness is false")
    report = evidence_file(ready.get("final_performance_report"), ready.get("final_performance_report_sha256"))
    require(report.get("schema_version") == 2 and report.get("kind") == "seonro-final-performance" and report.get("passed") is True and
            report.get("policy") == PERFORMANCE_POLICY,
            "Final performance audit did not pass")
    require(report.get("after_feature_functional_completion") is True and
            report.get("product_source_fingerprint") == source["product_source_fingerprint"] and
            report.get("functional_report_sha256") == ready["functional_report_sha256"], "Stale or pre-feature performance evidence")
    artifacts = {name: evidence_file(report.get(name + "_report"), report.get(name + "_report_sha256"))
                 for name in ("baseline", "population", "budget", "stability")}
    plan = evidence_file(report.get("stability_plan"), report.get("stability_plan_sha256"))
    canonical_performance_reports(artifacts["baseline"], artifacts["population"], artifacts["budget"], plan, artifacts["stability"],
                                  report["baseline_report_sha256"], report["stability_plan_sha256"])
    baseline_commit = artifacts["baseline"]["environment"]["sourceCommit"]
    run("git", "merge-base", "--is-ancestor", baseline_commit, source["source_commit"])
    require(report.get("baseline_product_source_fingerprint") == fingerprint(baseline_commit), "Baseline source fingerprint is not the actual measured source")
    before_environment = artifacts["baseline"]["environment"]
    after_environment = artifacts["population"]["environment"]
    if any(before_environment.get(k) != after_environment.get(k) for k in
           ("device", "runtime", "os", "architecture", "cpu", "totalAvailableMemoryBytes", "thermalState", "powerState", "precedingFunctionalSelection", "processScope")):
        require(isinstance(artifacts["budget"].get("environment_difference_rationale"), str) and
                artifacts["budget"]["environment_difference_rationale"].strip(), "Different measurement environments require an explicit comparison limitation")
    for measured in (artifacts["population"], artifacts["stability"]):
        commit = measured["environment"]["sourceCommit"]
        require(re.fullmatch(r"[0-9a-f]{40}", commit) is not None and fingerprint(commit) == source["product_source_fingerprint"],
                "Population/stability evidence measured different product inputs")
    require(report.get("legacy_speed_history_report") == LEGACY_SPEED_HISTORY and
            report.get("legacy_speed_history_report_sha256") == LEGACY_SPEED_HISTORY_SHA256,
            "Retain the original failed V020-G03 evidence without modification")
    legacy_speed_comparison(evidence_file(LEGACY_SPEED_HISTORY, LEGACY_SPEED_HISTORY_SHA256))
    if report.get("legacy_speed_comparison_report") is not None:
        legacy_speed_comparison(evidence_file(report["legacy_speed_comparison_report"], report.get("legacy_speed_comparison_report_sha256")))


def build_tools():
    sdk = os.environ.get("ANDROID_HOME") or os.environ.get("ANDROID_SDK_ROOT")
    require(bool(sdk), "Android SDK path missing")
    path = Path(sdk) / "build-tools/36.0.0"
    require((path / "aapt").is_file() and (path / "apksigner").is_file(), "Android build-tools 36.0.0 required")
    return path


def inspect_apk(apk, kind, source=None, signing=None):
    apk = Path(apk)
    require(apk.is_file() and 0 < apk.stat().st_size <= 256 * 1024 * 1024, "Missing or oversized APK")
    tools = build_tools()
    badging = run(tools / "aapt", "dump", "badging", apk)
    package = re.search(r"package: name='([^']+)' versionCode='(\d+)' versionName='([^']+)'", badging)
    require(package is not None, "APK identity could not be read")
    expected_version, expected_code = ("0.2.0", 2) if kind == "previous" else version()
    require(package.groups() == (PACKAGE, str(expected_code), expected_version), "APK package/version/code mismatch")
    require("sdkVersion:'24'" in badging and "application-debuggable" not in badging, "APK minSdk/debug policy mismatch")
    require("android.permission.INTERNET" not in run(tools / "aapt", "dump", "permissions", apk), "APK must be offline without INTERNET")
    manifest = run(tools / "aapt", "dump", "xmltree", apk, "AndroidManifest.xml")
    require("android:debuggable(0x0101000f)=(type 0x12)0xffffffff" not in manifest, "APK is debuggable")
    require("android:screenOrientation(0x0101001e)=(type 0x10)0x1" in manifest, "APK must retain portrait orientation")
    with zipfile.ZipFile(apk) as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)), "Duplicate APK ZIP entries")
        require({n.split("/")[1] for n in names if n.startswith("lib/") and n.endswith(".so")} == {"arm64-v8a", "x86_64"}, "APK ABI set mismatch")
    signature = run(tools / "apksigner", "verify", "--verbose", "--print-certs", apk, check=False)
    certificate = None
    if kind == "unsigned":
        require(signature.returncode != 0, "Signing input must be exactly the unsigned APK")
    else:
        require(signature.returncode == 0, "APK signature does not verify")
        verification = signature.stdout.decode("utf-8")
        certificates = re.findall(r"(?m)^Signer #1 certificate SHA-256 digest:\s*([a-fA-F0-9]{64})\s*$", verification)
        require([c.lower() for c in certificates] == [CERTIFICATE] and
                re.findall(r"(?m)^Number of signers:\s*(\d+)\s*$", verification) == ["1"], "Retained publisher certificate mismatch")
        certificate = CERTIFICATE
    activity = re.search(r"launchable-activity: name='([^']+)'", badging)
    require(activity is not None, "Launchable activity missing")
    result = {"schema_version": 1, "kind": kind, "sha256": sha(apk), "bytes": apk.stat().st_size,
              "application_id": PACKAGE, "version": expected_version, "version_code": expected_code,
              "minimum_sdk": 24, "abis": ["arm64-v8a", "x86_64"], "offline": True, "debuggable": False,
              "portrait": True, "certificate_sha256": certificate, "launchable_activity": activity[1]}
    if source is not None:
        check_source(source)
        result.update({k: source[k] for k in ("source_commit", "product_source_fingerprint", "run_id", "run_attempt")})
    if signing is not None:
        require(signing.get("source_commit") == source["source_commit"] and signing.get("signed_sha256") == result["sha256"] and
                signing.get("certificate_sha256") == CERTIFICATE and signing.get("payload_matches_verified_unsigned") is True and
                signing.get("original_bundle_modified") is False and signing.get("key_created") is False and
                signing.get("key_exported_to_workspace") is False, "Retained signing provenance mismatch")
    return result


def proof_matches(source, proof):
    for name in ("source_commit", "product_source_fingerprint", "run_id", "run_attempt"):
        require(proof.get(name) == source[name], "Platform/artifact provenance mismatch: " + name)


def windows_proof(source, raw):
    proof = read(raw)
    require(proof.get("passed") is True and proof.get("actualRenderAndBridge") is True and proof.get("version") == source["version"] and
            proof.get("host") == "WPF WebView2", "Actual Windows WebView2 render/bridge check failed")
    diagnostic = proof.get("diagnostic")
    for _ in range(3):
        if not isinstance(diagnostic, str):
            break
        diagnostic = json.loads(diagnostic)
    require(isinstance(diagnostic, dict) and diagnostic.get("hasView") is True and diagnostic.get("error") is None and
            diagnostic.get("connection") == "연결됨" and diagnostic.get("heading") == "만상의 흐름",
            "Windows visible DOM/connection diagnostics did not verify")
    return {"kind": "windows-webview2", "passed": True, "actual_render_and_bridge": True, "raw_sha256": sha(raw),
            **{k: source[k] for k in ("source_commit", "product_source_fingerprint", "run_id", "run_attempt")}}


def platform_gate(source, signed, windows, android, require_upgrade=False):
    proof_matches(source, signed)
    require(signed.get("kind") == "signed" and signed.get("certificate_sha256") == CERTIFICATE, "Retained-signed APK proof missing")
    proof_matches(source, windows)
    require(windows.get("passed") is True and windows.get("actual_render_and_bridge") is True, "Windows actual render check missing")
    require(len(android) == 2 and {r.get("api") for r in android} == {24, 35}, "Both actual Android API 24 and 35 checks required")
    for report in android:
        proof_matches(source, report)
        require(report.get("signed_apk_sha256") == signed["sha256"] and report.get("passed") is True and
                report.get("fresh_install_render") is True, "Android checked a different APK or failed real render")
        if require_upgrade or report.get("upgrade_requested") is True:
            require(report.get("upgrade_requested") is True and report.get("retained_signature_upgrade") is True and report.get("legacy_save_preserved") is True,
                    "Requested actual retained-signature/data upgrade failed")


def request_guard_self_test():
    # Pure validator dictionaries: no API, signing, workflow, app, release or credentials are used.
    commit = "a" * 40
    environment = {"GITHUB_ACTIONS": "true", "GITHUB_REPOSITORY": REPOSITORY,
        "GITHUB_EVENT_NAME": "workflow_dispatch", "GITHUB_REF": "refs/heads/main", "GITHUB_SHA": commit,
        "GITHUB_WORKFLOW_SHA": commit, "GITHUB_WORKFLOW_REF": REPOSITORY + "/" + WORKFLOW + "@refs/heads/main",
        "GITHUB_ACTOR": OWNER, "GITHUB_TRIGGERING_ACTOR": OWNER, "GITHUB_RUN_ID": "123", "GITHUB_RUN_ATTEMPT": "1"}
    common = {"repository": {"full_name": REPOSITORY}, "sender": {"login": OWNER}}
    manual = {**common, "ref": "main", "inputs": {"publish": "true", "verify_upgrade": "true"}}
    comment = {"id": 321, "body": RELEASE_COMMAND, "user": {"login": OWNER}, "author_association": "OWNER",
        "issue_url": "https://api.github.com/repos/" + REPOSITORY + "/issues/84",
        "html_url": "https://github.com/" + REPOSITORY + "/pull/84#issuecomment-321",
        "created_at": "2026-10-30T10:01:00Z", "updated_at": "2026-10-30T10:01:00Z"}
    event = {**common, "action": "created", "issue": {"number": RELEASE_PR, "state": "closed",
        "pull_request": {"url": "https://api.github.com/repos/" + REPOSITORY + "/pulls/84"}}, "comment": comment}
    comment_environment = {**environment, "GITHUB_EVENT_NAME": "issue_comment"}
    main_ref = {"ref": "refs/heads/main", "object": {"type": "commit", "sha": commit}}
    pull = {"number": RELEASE_PR, "merged": True, "state": "closed", "merge_commit_sha": commit,
        "merged_at": "2026-10-30T10:00:00Z", "url": "https://api.github.com/repos/" + REPOSITORY + "/pulls/84",
        "base": {"ref": "main", "repo": {"full_name": REPOSITORY}}}
    manual_request = release_request_context(environment, manual)
    request = release_request_context(comment_environment, event)
    require(request["event"] == "issue_comment" and request["publish"] is True and request["verify_upgrade"] is True,
            "Comment mode must record the actual event and both mandatory options")
    remote_request_context(manual_request, main_ref)
    require(remote_request_context(request, main_ref, pull, comment) == commit, "Valid merged owner command rejected")

    def changed(value, path, replacement):
        result = copy.deepcopy(value); target = result
        for part in path[:-1]:
            target = target[part]
        target[path[-1]] = replacement
        return result

    cases = []
    for name, field, value in (("repository", "GITHUB_REPOSITORY", "other/project-hora"),
            ("actor", "GITHUB_ACTOR", "other"), ("rerun actor", "GITHUB_TRIGGERING_ACTOR", "other"),
            ("branch", "GITHUB_REF", "refs/heads/dev"), ("unsupported event", "GITHUB_EVENT_NAME", "push"),
            ("workflow source", "GITHUB_WORKFLOW_SHA", "b" * 40), ("workflow path", "GITHUB_WORKFLOW_REF", REPOSITORY + "/other.yml@refs/heads/main"),
            ("missing attempt", "GITHUB_RUN_ATTEMPT", "0")):
        cases.append((name, lambda f=field, v=value: release_request_context({**comment_environment, f: v}, event)))
    for name, path, value in (("edited event", ["action"], "edited"), ("other PR", ["issue", "number"], 85),
            ("ordinary issue", ["issue", "pull_request"], {}), ("unmerged event", ["issue", "state"], "open"),
            ("command prefix", ["comment", "body"], "please " + RELEASE_COMMAND),
            ("command newline", ["comment", "body"], RELEASE_COMMAND + "\n"),
            ("different version", ["comment", "body"], "/선로만상록 0.2.0 릴리스"),
            ("other author", ["comment", "user", "login"], "other"), ("non-owner association", ["comment", "author_association"], "MEMBER"),
            ("edited original", ["comment", "updated_at"], "2026-10-30T10:02:00Z"),
            ("other payload sender", ["sender", "login"], "other"),
            ("other payload repository", ["repository", "full_name"], "other/project-hora")):
        cases.append((name, lambda p=path, v=value: release_request_context(comment_environment, changed(event, p, v))))
    cases.append(("comment disguised as dispatch", lambda: release_request_context(environment, event)))
    cases.append(("comment input override", lambda: release_request_context(comment_environment, {**event, "inputs": {"verify_upgrade": False}})))
    cases.append(("malformed manual boolean", lambda: release_request_context(environment, {**manual, "inputs": {"publish": "TRUE"}})))
    for name, path, value in (("main advanced", ["object", "sha"], "b" * 40), ("other main ref", ["ref"], "refs/heads/dev")):
        cases.append((name, lambda p=path, v=value: remote_request_context(request, changed(main_ref, p, v), pull, comment)))
    for name, path, value in (("unmerged PR", ["merged"], False), ("other API PR", ["number"], 85),
            ("other base", ["base", "ref"], "dev"), ("other base repository", ["base", "repo", "full_name"], "other/project-hora"),
            ("command before merge", ["merged_at"], "2026-10-30T10:02:00Z"), ("missing actual merge", ["merge_commit_sha"], "")):
        cases.append((name, lambda p=path, v=value: remote_request_context(request, main_ref, changed(pull, p, v), comment)))
    for name, path, value in (("different original request", ["id"], 322), ("live comment edited", ["updated_at"], "2026-10-30T10:02:00Z"),
            ("live command changed", ["body"], RELEASE_COMMAND + "!"), ("live comment moved", ["issue_url"], "https://api.github.com/repos/" + REPOSITORY + "/issues/85")):
        cases.append((name, lambda p=path, v=value: remote_request_context(request, main_ref, pull, changed(comment, p, v))))
    source = {"event": "issue_comment", "request": request, "source_commit": commit, "run_id": "123", "run_attempt": "1"}
    metadata = {"id": 123, "event": "issue_comment", "actor": {"login": OWNER}, "triggering_actor": {"login": OWNER},
        "head_repository": {"full_name": REPOSITORY}, "repository": {"full_name": REPOSITORY},
        "head_sha": commit, "head_branch": "main", "path": WORKFLOW, "run_attempt": 1}
    actual_run_context(source, request, metadata)
    for name, path, value in (("actual event normalized", ["event"], "workflow_dispatch"), ("actual rerun actor", ["triggering_actor", "login"], "other"),
            ("actual source changed", ["head_sha"], "b" * 40), ("actual branch", ["head_branch"], "dev"),
            ("actual repository", ["head_repository", "full_name"], "other/project-hora"), ("actual workflow", ["path"], "other.yml"),
            ("actual attempt mismatch", ["run_attempt"], 2), ("actual run mismatch", ["id"], 124)):
        cases.append((name, lambda p=path, v=value: actual_run_context(source, request, changed(metadata, p, v))))
    cases.append(("artifact event normalized", lambda: actual_run_context({**source, "event": "workflow_dispatch"}, request, metadata)))
    cases.append(("artifact request swapped", lambda: actual_run_context({**source, "request": {**request, "comment_id": 999}}, request, metadata)))
    cases.append(("comment upgrade removed", lambda: actual_run_context({**source, "request": {**request, "verify_upgrade": False}},
                                                                 {**request, "verify_upgrade": False}, metadata)))
    positives = 3
    # Preserve existing explicit manual diagnostic choices and normal owner reruns.
    diagnostic = release_request_context(environment, {**manual, "inputs": {"publish": False, "verify_upgrade": False}})
    require(diagnostic["publish"] is False and diagnostic["verify_upgrade"] is False, "Manual diagnostics changed")
    optional_upgrade = release_request_context(environment, {**manual, "inputs": {"publish": True, "verify_upgrade": False}})
    actual_run_context({**source, "event": "workflow_dispatch", "request": optional_upgrade}, optional_upgrade,
                       {**metadata, "event": "workflow_dispatch"})
    positives += 2
    for env, payload in ((environment, manual), (comment_environment, event)):
        rerun = release_request_context({**env, "GITHUB_RUN_ATTEMPT": "2"}, payload)
        actual_run_context({**source, "event": rerun["event"], "request": rerun, "run_attempt": "2"}, rerun,
                           {**metadata, "event": rerun["event"], "run_attempt": 2})
        positives += 1
    proof_source = {"source_commit": commit, "product_source_fingerprint": "c" * 64, "run_id": "123", "run_attempt": "1"}
    common_proof = {**proof_source, "sha256": "d" * 64}
    signed = {**common_proof, "kind": "signed", "certificate_sha256": CERTIFICATE}
    windows = {**common_proof, "passed": True, "actual_render_and_bridge": True}
    android = [{**common_proof, "api": api, "signed_apk_sha256": signed["sha256"], "passed": True,
                "fresh_install_render": True, "upgrade_requested": True,
                "retained_signature_upgrade": True, "legacy_save_preserved": True} for api in (24, 35)]
    platform_gate(proof_source, signed, windows, android, require_upgrade=request["verify_upgrade"])
    positives += 1
    for field in ("upgrade_requested", "retained_signature_upgrade", "legacy_save_preserved"):
        bad = copy.deepcopy(android); bad[0][field] = False
        cases.append(("mandatory comment upgrade " + field,
                      lambda value=bad: platform_gate(proof_source, signed, windows, value, require_upgrade=request["verify_upgrade"])))
    bad_apk = copy.deepcopy(android); bad_apk[1]["signed_apk_sha256"] = "e" * 64
    cases.append(("different actual API35 APK", lambda: platform_gate(proof_source, signed, windows, bad_apk, require_upgrade=True)))
    for name, operation in cases:
        try:
            operation()
        except (ValueError, KeyError):
            continue
        raise ValueError("Release request self-test did not reject: " + name)
    return {"positive_cases": positives, "negative_cases": len(cases), "api_signing_or_publication_executed": False}


def self_test():
    # Validator fixtures are not platform/performance evidence and never enable a real readiness record.
    failures = 0
    for action in (lambda: functional_report({"selection": "all", "total": 1, "failed": 1, "results": [{"passed": True}]}),
                   lambda: functional_report({"selection": "all", "total": 1, "failed": 0, "results": []}),
                   lambda: functional_report({"selection": "currency", "total": 1, "failed": 0, "results": [{"passed": True}]}),
                   lambda: browser_report([]), lambda: platform_gate({}, {}, {}, [])):
        try:
            action()
        except (ValueError, KeyError):
            failures += 1
    require(failures == 5, "Release negative guard self-test failed")
    performance_guard_self_test()
    request_tests = request_guard_self_test()
    functional_report({"selection": "all", "total": 1, "failed": 0, "results": [{"passed": True}]})
    ready = read(ROOT / READINESS)
    if ready.get("feature_functional_complete") is not True:
        try:
            feature_gate({"version": version()[0], "product_source_fingerprint": fingerprint()})
        except ValueError as error:
            require("signing is disabled" in str(error), "Initial readiness was rejected for an unexpected reason")
        else:
            raise ValueError("Initial false readiness must prevent signing")
    require(not product_path(READINESS) and not product_path("docs/evidence/0.2.1/final-performance.json") and
            product_path("src/Chronicles.Game/World.cs") and product_path("VERSION"), "Product fingerprint scope is wrong")
    print(json.dumps({"release_guard_self_tests": "passed", "request_guards": request_tests, "actual_platform_or_performance_claim": False}))


def performance_guard_self_test():
    # Invented dictionaries test validator invariants only. They are never written as measurement evidence.
    baseline_sha, plan_sha = "a" * 64, "b" * 64
    environment = {"sourceCommit": "c" * 40, "sourceWorktreeClean": True, "buildConfiguration": "Release", "engine": "none", "platform": "host",
                   "device": "validator fixture", "runtime": "validator fixture", "os": "validator fixture", "architecture": "test",
                   "precedingFunctionalSelection": "validator only", "processScope": "validator only",
                   "thermalState": "not measured", "powerState": "not measured", "cpu": 4, "totalAvailableMemoryBytes": 1000}
    names = ("beliefs", "storedClaims", "activeClaims", "peopleObservations", "placeObservations", "sourceObservations", "knownPeople", "knownPlaces")
    population = {"schema_version": 2, "kind": "seonro-population-baseline", "complete": True,
                  "completedSizes": [2, 100, 1000, 10000], "environment": environment, "totalElapsedSeconds": 1,
                  "reports": []}
    for size in population["completedSizes"]:
        row = {name: 1 for name in PERFORMANCE_METRICS}
        row.update({"population": size, "measuredMonths": 2, "warmupMonths": 1, "repetitions": 1,
                    "monthlySamplesMs": [1, 1], "fixture": {"population": size, "founders": 2, "syntheticEntrants": size - 2},
                    "openingRetainedManagedBytes": 1,
                    "usedLaws": {"test-only": 1}, "workload": {"peopleWithDecisions": 1}, "retainedMemoryScope": "test only",
                    "cognition": {"people": size, "beliefBudgetPerPerson": 8, "activeClaimBudgetPerPerson": 2,
                                  "maximumBudgetUnitsPerPerson": 0, "peopleExceedingBeliefBudget": 0, "peopleExceedingClaimBudget": 0,
                                  "serializedBytes": 1, "serializedScope": "test only", "totals": dict.fromkeys(names, 0),
                                  "maximumPerPerson": dict.fromkeys(names, 0)}})
        population["reports"].append(row)
    budget = {"schema_version": 1, "kind": "seonro-performance-budget", "policy": PERFORMANCE_POLICY,
              "established_after_baseline": True, "baseline_report_sha256": baseline_sha, "rationale": "validator fixture only",
              "maximum_regression_percent": 10, "limits": {str(n): dict.fromkeys(PERFORMANCE_METRICS, 10) for n in population["completedSizes"]},
              "stability_limits": {"10000": {"retainedManagedBytes": 10, "saveBytes": 10}}}
    plan = {"schemaVersion": 1, "policyId": PERFORMANCE_POLICY, "baselineReportSha256": baseline_sha, "rationale": "validator fixture only",
            "cases": [{"population": 10000, "horizonMonths": 24, "checkpointEveryMonths": 12, "validateEveryMonths": 1, "wallTimeLimitSeconds": 1000}]}
    stability = {"schema_version": 1, "kind": "seonro-long-run-stability", "policy": PERFORMANCE_POLICY, "passed": True,
                 "complete": True, "status": "completed", "runningPopulation": None, "finishedCases": [10000], "executionFailure": None,
                 "environment": environment, "baselineReportSha256": baseline_sha, "planSha256": plan_sha, "totalElapsedSeconds": 101,
                 "reports": [{"population": 10000, "passed": True, "failure": None, "targetMonths": 24, "completedMonths": 24,
                              "elapsedSeconds": 100, "boundaryChecks": 30, "workload": {"peopleWithDecisions": 1},
                              "living": 9976, "births": 0, "deaths": 24,
                              "finalHash": "d" * 64,
                              "checkpoints": [{"month": month, "people": 10000, "living": 10000 - month,
                                               "births": 0, "deaths": month,
                                               "hash": "d" * 64,
                                               "invariantValid": True, "saveResumeEqual": True, "queryUnchanged": True, "publicCommandAccepted": True,
                                               "retainedManagedBytes": 1, "saveBytes": 1} for month in (0, 12, 24)]}]}
    canonical_performance_reports(population, population, budget, plan, stability, baseline_sha, plan_sha)
    negative = [
        ("partial sizes", lambda x: x[1].update(complete=False)),
        ("missing 10k", lambda x: x[1]["reports"].pop()),
        ("wrong baseline", lambda x: x[2].update(baseline_report_sha256="d" * 64)),
        ("pre-baseline budget", lambda x: x[2].update(established_after_baseline=False)),
        ("missing retained budget", lambda x: x[2]["limits"]["10000"].pop("retainedManagedBytes")),
        ("missing collected opening memory", lambda x: x[1]["reports"][-1].pop("openingRetainedManagedBytes")),
        ("actual regression", lambda x: x[1]["reports"][-1].update(saveBytes=9)),
        ("nonfinite limit", lambda x: x[2].update(maximum_regression_percent=float("nan"))),
        ("inert workload", lambda x: x[1]["reports"][-1]["workload"].update(peopleWithDecisions=0)),
        ("dirty measured source", lambda x: x[1]["environment"].update(sourceWorktreeClean=False)),
        ("cognition budget exceeded", lambda x: x[1]["reports"][-1]["cognition"].update(maximumBudgetUnitsPerPerson=9)),
        ("invented per-person maximum", lambda x: x[1]["reports"][-1]["cognition"]["maximumPerPerson"].update(beliefs=1)),
        ("failed stability", lambda x: x[4]["reports"][0].update(failure="actual failure")),
        ("partial stability", lambda x: x[4].update(complete=False)),
        ("running stability", lambda x: x[4].update(status="running", runningPopulation=10000)),
        ("failed execution", lambda x: x[4].update(status="failed", executionFailure="actual failure")),
        ("numeric completion flag", lambda x: x[4].update(complete=1)),
        ("forged completion flag", lambda x: x[4].update(finishedCases=[])),
        ("timeout", lambda x: x[4]["reports"][0].update(elapsedSeconds=1001)),
        ("shortened horizon", lambda x: x[4]["reports"][0].update(completedMonths=12)),
        ("missing boundary checks", lambda x: x[4]["reports"][0].update(boundaryChecks=0)),
        ("resume mismatch", lambda x: x[4]["reports"][0]["checkpoints"][-1].update(saveResumeEqual=False)),
        ("wrong final save hash", lambda x: x[4]["reports"][0].update(finalHash="e" * 64)),
        ("stability memory over budget", lambda x: x[4]["reports"][0]["checkpoints"][-1].update(retainedManagedBytes=11)),
        ("hidden synthetic inflow", lambda x: x[4]["reports"][0]["checkpoints"][-1].update(people=10001)),
    ]
    for name, mutation in negative:
        values = json.loads(json.dumps([population, population, budget, plan, stability]))
        mutation(values)
        try:
            canonical_performance_reports(*values, baseline_sha, plan_sha)
        except ValueError:
            continue
        raise ValueError("Performance contract self-test did not reject: " + name)
    legacy = read(ROOT / LEGACY_SPEED_HISTORY)
    require(sha(ROOT / LEGACY_SPEED_HISTORY) == LEGACY_SPEED_HISTORY_SHA256 and legacy_speed_comparison(legacy) is False,
            "The original failed legacy measurement must stay unchanged and failed")
    legacy["passed"] = True
    try:
        legacy_speed_comparison(legacy)
    except ValueError:
        pass
    else:
        raise ValueError("Relabelled legacy miss must be rejected")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    p = sub.add_parser("source"); p.add_argument("--output", required=True); p.add_argument("--github-output")
    p = sub.add_parser("fingerprint"); p.add_argument("--commit", default="HEAD")
    p = sub.add_parser("gate"); p.add_argument("--stage", choices=("features", "signing", "publish"), required=True)
    p.add_argument("--source", required=True); p.add_argument("--readiness", default=READINESS)
    for field in ("functional", "browser", "signed", "windows"):
        p.add_argument("--" + field)
    p.add_argument("--android", action="append", default=[])
    p.add_argument("--require-upgrade", action="store_true")
    p = sub.add_parser("apk"); p.add_argument("--kind", choices=("unsigned", "signed", "previous"), required=True)
    p.add_argument("--apk", required=True); p.add_argument("--source"); p.add_argument("--signing-proof"); p.add_argument("--output", required=True)
    p.add_argument("--github-output")
    p = sub.add_parser("windows"); p.add_argument("--source", required=True); p.add_argument("--raw", required=True); p.add_argument("--output", required=True)
    sub.add_parser("self-test")
    args = parser.parse_args()
    if args.command == "self-test":
        self_test(); return
    if args.command == "fingerprint":
        print(fingerprint(args.commit)); return
    if args.command == "source":
        owner_context(); record = source_record(); write(args.output, record)
        if args.github_output:
            with Path(args.github_output).open("a", encoding="utf-8") as stream:
                for key in ("source_commit", "product_source_fingerprint", "version", "version_code", "apk_name", "tag"):
                    stream.write(key + "=" + str(record[key]) + "\n")
                for key in ("publish", "verify_upgrade"):
                    stream.write(key + "=" + str(record["request"][key]).lower() + "\n")
        return
    if args.command == "apk":
        record = inspect_apk(args.apk, args.kind, read(args.source) if args.source else None,
                             read(args.signing_proof) if args.signing_proof else None)
        write(args.output, record)
        if args.github_output:
            with Path(args.github_output).open("a", encoding="utf-8") as stream:
                stream.write("sha256=" + record["sha256"] + "\n")
        return
    source = check_source(read(args.source))
    if args.command == "windows":
        write(args.output, windows_proof(source, args.raw)); return
    owner_context(); ready = feature_gate(source, args.readiness)
    if args.stage in ("signing", "publish"):
        require(args.functional and args.browser, "Actual current-run functional/browser reports required")
        functional_report(read(args.functional)); browser_report(read(args.browser))
    if args.stage == "publish":
        performance_gate(source, ready)
        require(args.signed and args.windows, "Signed APK and Windows proofs missing")
        platform_gate(source, read(args.signed), read(args.windows), [read(p) for p in args.android],
                      args.require_upgrade or source["request"]["verify_upgrade"])
    print(json.dumps({"gate": args.stage, "passed": True, "source_commit": source["source_commit"],
                      "product_source_fingerprint": source["product_source_fingerprint"]}))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError, json.JSONDecodeError, zipfile.BadZipFile) as error:
        print("Release verification rejected: " + str(error), file=sys.stderr)
        sys.exit(1)
