import datetime
import hashlib
import json
import math
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parent
SOURCE = "7ce66943b539420c9dc1e3fa8a611daae58020ff"
FP = "4e228e7ba34e538fd1a69abf05934d8162b2a65559ed4ffd50a73b00a8ba2304"
EXPECTED = {(s, seed) for s in "abc" for seed in [42, 98, 2026]}


def read(path):
    return json.loads(path.read_text())


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


verified = read(ROOT / "local-verification.json")
assert verified["source_commit"] == SOURCE and verified["product_source_fingerprint"] == FP
assert verified["verified_completed_cohorts"] == 9 and verified["nine_cohort_completion_claim"] is True
assert {(x["scenario"], x["seed"]) for x in verified["cohorts"]} == EXPECTED
assert all(x["all_checks_passed"] for x in verified["cohorts"])
assert len({x["runner_name"] for x in verified["cohorts"]}) == 9

raws = {}
hashes = {}
for key in sorted(EXPECTED):
    path = ROOT / f"{key[0]}-{key[1]}" / "out/growth.json"
    raw = read(path)
    assert raw["sourceCommit"] == SOURCE and raw["productSourceFingerprint"] == FP
    assert raw["requested_complete"] is True and raw["complete"] is False
    assert raw["completedCases"] == 1 and len(raw["reports"]) == 1
    assert raw["requestedCases"] == [{"scenario": key[0], "seed": key[1]}]
    assert raw["reports"][0]["status"] == "completed" and raw["reports"][0]["error"] is None
    assert [x["month"] for x in raw["reports"][0]["timeSeries"]] == list(range(0, 6001, 120))
    hashes[key] = sha(path)
    assert hashes[key] == next(x["raw_sha256"] for x in verified["cohorts"] if (x["scenario"], x["seed"]) == key)
    raws[key] = raw

official_path = ROOT / "official-aggregate/out/growth-aggregate.json"
official = read(official_path)
provenance_path = ROOT / "official-aggregate/provenance/aggregate.json"
provenance = read(provenance_path)
artifact = read(ROOT / "official-aggregate.artifact.json")
assert sha(ROOT / "official-aggregate.zip") == artifact["sha"]
assert official["kind"] == "seonro-growth-cohorts-aggregate"
assert official["sourceCommit"] == SOURCE and official["productSourceFingerprint"] == FP
assert official["complete"] is True and official["requested_complete"] is True
assert official["plannedCases"] == official["completedCases"] == 9
assert official["horizonMonths"] == 6000 and official["observationIntervalMonths"] == 120
assert official["samplesPerScenario"] == 3 and len(official["reports"]) == 9
aggregated_rows = {(row["scenario"], row["seed"]): row for row in official["reports"]}
assert set(aggregated_rows) == EXPECTED
for key, row in aggregated_rows.items():
    assert row == raws[key]["reports"][0], key

assert len(official["inputArtifacts"]) == 9
seen = set()
for entry in official["inputArtifacts"]:
    assert len(entry["cohortKeys"]) == 1
    key = tuple(entry["cohortKeys"][0])
    assert key in EXPECTED and key not in seen
    seen.add(key)
    assert entry["sha256"] == hashes[key]
    assert entry["environment"] == raws[key]["environment"]
    expected_suffix = f"/inputs/v021-growth-41111524034-1-{key[0]}-{key[1]}/out/growth.json"
    assert entry["path"].endswith(expected_suffix)
assert seen == EXPECTED
assert provenance["source_commit"] == SOURCE and provenance["product_source_fingerprint"] == FP
assert provenance["no_new_simulation_in_aggregator"] is True
assert len(provenance["raw_input_sha256"]) == 9
for name, digest in provenance["raw_input_sha256"].items():
    match = re.fullmatch(r"inputs/v021-growth-41111524034-1-([abc])-(42|98|2026)/out/growth.json", name)
    assert match and hashes[(match[1], int(match[2]))] == digest
assert math.isclose(official["sumInputReportedElapsedMs"], sum(raw["totalElapsedMs"] for raw in raws.values()), rel_tol=1e-12, abs_tol=1e-5)
assert len(official["distributions"]) == 3
for distribution in official["distributions"]:
    assert distribution["complete"] is True
    assert distribution["expectedSeeds"] == distribution["completedSeeds"] == [42, 98, 2026]

run = read(ROOT / "final-ci-metadata.json")
assert run["run"]["head_sha"] == SOURCE
assert run["run"]["status"] == "completed" and run["run"]["conclusion"] == "success"
cohorts = [x for x in run["jobs"] if x["name"].startswith("성장 ")]
aggregate_jobs = [x for x in run["jobs"] if not x["name"].startswith("성장 ")]
assert len(cohorts) == 9 and len(aggregate_jobs) == 1
assert all(x["status"] == "completed" and x["conclusion"] == "success" for x in run["jobs"])
assert len(run["artifacts"]) == 10

result = {
    "schema_version": 1,
    "kind": "independent-verification-of-frozen-nine-growth-cohorts",
    "verified_at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "source_commit": SOURCE,
    "source_tree": "5ae1b72e2471122e61f27a4cb9c294e7b40b5793",
    "product_source_fingerprint": FP,
    "run_id": 41111524034,
    "run_url": "https://github.com/hddy774/project-hora/actions/runs/41111524034",
    "pr": 197,
    "independently_verified_cohorts": 9,
    "horizon_months": 6000,
    "observation_interval_months": 120,
    "observations_per_cohort": 51,
    "observations_total": 459,
    "all_nine_individual_checks_passed": True,
    "all_nine_raws_match_official_input_hashes": True,
    "all_nine_official_rows_equal_unmodified_raw_rows": True,
    "official_aggregate_complete": True,
    "official_aggregate_completed_cases": 9,
    "official_aggregate_path": str(official_path),
    "official_aggregate_sha256": sha(official_path),
    "official_provenance_path": str(provenance_path),
    "official_provenance_sha256": sha(provenance_path),
    "official_zip_sha256": artifact["sha"],
    "official_artifact_id": artifact["id"],
    "local_individual_verification_sha256": sha(ROOT / "local-verification.json"),
    "metadata_sha256": sha(ROOT / "final-ci-metadata.json"),
    "this_verifier_sha256": sha(pathlib.Path(__file__)),
    "sdk": "10.0.403",
    "runtime": ".NET 10.0.14",
    "distinct_github_runner_names": 9,
    "actual_cpu_count_per_runner": 4,
    "physical_thermal_or_power_measured": False,
    "case_elapsed_sum_is_paired_performance_or_wall_time": False,
    "raw_files_modified": False,
    "new_simulation_or_parameter_change_during_aggregation": False,
    "growth_balance_or_indefinite_viability_claim": False,
    "new_source94_or_4fe_or_native_release_claim": False,
    "raw_cohorts": [
        {"scenario": key[0], "seed": key[1], "path": str(ROOT / f"{key[0]}-{key[1]}" / "out/growth.json"),
         "sha256": hashes[key], "final_hash": raws[key]["reports"][0]["finalHash"]}
        for key in sorted(EXPECTED)
    ],
    "scope": "9/9 finite observations ended and raw/source/environment/provenance/official aggregation checks passed. Original absence, deaths, unmet goals and provisional-law observations are retained; no outcome is promoted to a balance success.",
}
out = ROOT / "final-verification.json"
assert not out.exists()
out.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n")
print(json.dumps({"path": str(out), "sha256": sha(out), "raw_checks_passed": 9,
                  "official_aggregate_complete": True, "official_aggregate_sha256": sha(official_path),
                  "official_provenance_sha256": sha(provenance_path)}, ensure_ascii=False))
