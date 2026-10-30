import datetime
import hashlib
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parent
SOURCE = "7ce66943b539420c9dc1e3fa8a611daae58020ff"
FINGERPRINT = "4e228e7ba34e538fd1a69abf05934d8162b2a65559ed4ffd50a73b00a8ba2304"
NAMES = {"Chronicles.Core", "Chronicles.Game", "Chronicles.Hosting", "Chronicles.Game.Tests"}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(root, name):
    return json.loads((root / name).read_text())


prior_path = ROOT / "local-verification.json"
prior = json.loads(prior_path.read_text()) if prior_path.exists() else {}
if prior:
    assert prior["source_commit"] == SOURCE and prior["product_source_fingerprint"] == FINGERPRINT
results = []
for old in prior.get("cohorts", []):
    assert old["source_commit"] == SOURCE and old["product_source_fingerprint"] == FINGERPRINT
    assert old["all_checks_passed"] is True
    old = dict(old)
    old.setdefault("validation_harness_sha256", prior["validator_sha256"])
    old.setdefault("validated_at_utc", prior["verified_at_utc"])
    results.append(old)
previously_verified = {(row["scenario"], row["seed"]) for row in results}
new_count = 0
for root in sorted(p for p in ROOT.iterdir() if p.is_dir() and (p / "out/growth.json").exists()):
    scenario, seed_text = root.name.split("-")
    seed = int(seed_text)
    if (scenario, seed) in previously_verified:
        continue
    expected = {"scenario": scenario, "seed": seed}
    artifact = json.loads((ROOT / (root.name + ".artifact.json")).read_text())
    archive = ROOT / (root.name + ".zip")
    assert archive.stat().st_size == artifact["zip_bytes"]
    assert digest(archive) == artifact["zip_sha256"]

    source = read(root, "provenance/source.json")
    assert source["source_commit"] == SOURCE
    assert source["product_source_fingerprint"] == FINGERPRINT
    assert source["source_worktree_clean"] is True
    assert source["version"] == "0.2.1" and source["sdk"] == "10.0.403"
    assert source["horizon_months"] == 6000 and source["observation_interval_months"] == 120
    assert source["scenario"] == scenario and source["seed"] == seed

    manifest = read(root, "provenance/raw-manifest.json")
    for name, entry in manifest.items():
        path = root / name
        assert path.is_file(), name
        assert path.stat().st_size == entry["bytes"], name
        assert digest(path) == entry["sha256"], name

    assemblies = read(root, "provenance/assemblies.json")
    assert assemblies["source_commit"] == SOURCE
    assert assemblies["runtime"] == ".NET 10.0.14" and assemblies["cpu_count"] == 4
    assert assemblies["total_available_memory_bytes"] > 0
    assert {entry["name"] for entry in assemblies["assemblies"]} == NAMES
    for entry in assemblies["assemblies"]:
        assert entry["version"] == "0.2.1.0"
        assert entry["informationalVersion"] == "0.2.1+" + SOURCE
        assert entry["configuration"] == "Release"
        path = root / "provenance/assemblies" / (entry["name"] + ".dll")
        assert path.stat().st_size == entry["bytes"] and digest(path) == entry["sha256"]

    start = read(root, "provenance/process-start.json")
    end = read(root, "provenance/process-exit.json")
    assert start["source_commit"] == SOURCE and start["wall_cap_minutes"] == 330
    command = start["command"]
    assert command[0] == "dotnet"
    assert command[1].startswith("/home/runner/work/_temp/")
    assert command[1].endswith("/Chronicles.Game.Tests.dll")
    for key, value in [("--filter", "INV-001/002/011"), ("--growth-scenario", scenario), ("--growth-seed", str(seed))]:
        assert command[command.index(key) + 1] == value
    assert end["process_exit"] == 0 and end["log_exit"] == 0
    assert end["raw_growth_report_modified_by_workflow"] is False
    assert not (root / "out/growth.json.pending").exists()

    genesis = read(root, "out/genesis.json")
    assert genesis["selection"] == "INV-001/002/011" and genesis["total"] == 1 and genesis["failed"] == 0
    assert len(genesis["results"]) == 1 and genesis["results"][0]["passed"] is True

    report = read(root, "out/growth.json")
    assert report["schema_version"] == 2 and report["kind"] == "seonro-growth-cohorts"
    assert report["sourceCommit"] == SOURCE and report["productSourceFingerprint"] == FINGERPRINT
    assert report["requestedCases"] == [expected]
    assert report["requested_complete"] is True and report["complete"] is False
    assert report["status"] == "requested-completed" and report["runningCase"] is None
    assert report["plannedCases"] == 9 and report["completedCases"] == 1
    assert report["horizonMonths"] == 6000 and report["observationIntervalMonths"] == 120
    assert report["samplesPerScenario"] == 3 and len(report["reports"]) == 1
    environment = report["environment"]
    assert environment["sourceCommit"] == SOURCE and environment["sourceWorktreeClean"] is True
    assert environment["buildConfiguration"] == "Release" and environment["runtime"] == ".NET 10.0.14"
    assert environment["cpu"] == 4 and environment["precedingFunctionalSelection"] == "INV-001/002/011"

    row = report["reports"][0]
    assert row["scenario"] == scenario and row["seed"] == seed
    assert row["selectedDefault"] is (scenario == "a")
    assert row["status"] == "completed" and row["error"] is None
    assert row["observedThroughMonth"] == 6000
    assert [p["month"] for p in row["timeSeries"]] == list(range(0, 6001, 120))
    assert re.fullmatch("[0-9a-f]{64}", row["finalHash"])
    selected = read(root, "provenance/selected-completion.json")
    assert selected["selected_case_complete"] is True and selected["nine_cases_complete_claim"] is False
    assert selected["source_commit"] == SOURCE and selected["scenario"] == scenario and selected["seed"] == seed
    assert selected["final_hash"] == row["finalHash"]

    before = read(root, "environment/before.json")
    assert before["sdk"].strip() == "10.0.403"
    assert "Microsoft.NETCore.App 10.0.14" in before["runtimes"]
    new_count += 1
    results.append({
        "scenario": scenario, "seed": seed, "all_checks_passed": True,
        "artifact_id": artifact["id"], "zip_sha256": artifact["zip_sha256"],
        "raw_path": str(root / "out/growth.json"), "raw_sha256": digest(root / "out/growth.json"),
        "verified_manifest_files": len(manifest), "source_commit": SOURCE,
        "source_tree": source["source_tree"], "workflow_event_merge_commit": source["workflow_source_commit"],
        "product_source_fingerprint": FINGERPRINT, "sdk": "10.0.403", "runtime": assemblies["runtime"],
        "cpu_count": assemblies["cpu_count"], "available_memory_bytes": assemblies["total_available_memory_bytes"],
        "runner_name": before["runner_name"], "cpu_model": before["cpu_model"], "cgroup": before["cgroup"],
        "thermal_state": before["thermal_state"], "power_state": before["power_state"],
        "observed_through_month": 6000, "observations": len(row["timeSeries"]),
        "process_started": start["started_at_utc"], "process_finished": end["finished_at_utc"],
        "wall_seconds_including_genesis_logging_and_report_io": end["wall_seconds_including_genesis_logging_and_report_io"],
        "case_elapsed_ms": row["caseElapsedMs"], "final_hash": row["finalHash"],
        "final_state": {k: row["timeSeries"][-1][k] for k in ["population", "founderDescendants", "generation", "births", "deaths", "sects", "dynasties"]},
        "assemblies": assemblies["assemblies"],
        "validation_harness_sha256": digest(pathlib.Path(__file__)),
        "validated_at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    })

results.sort(key=lambda row: (row["scenario"], row["seed"]))
summary = {
    "verified_at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    "run_id": 41111524034, "source_commit": SOURCE, "product_source_fingerprint": FINGERPRINT,
    "verified_completed_cohorts": len(results), "expected_cohorts": 9, "nine_cohort_completion_claim": len(results) == 9,
    "newly_verified_this_invocation": new_count, "prior_verified_records_reused_without_rehash": len(previously_verified),
    "validator_sha256": digest(pathlib.Path(__file__)), "no_raw_files_modified": True,
    "scope": "Downloaded raw ZIP/manifest/DLL hashes, actual source/environment, genesis and finite selected cohort completion only; not a balance, device, thermal, retained-signature or release claim.",
    "cohorts": results,
}
(ROOT / "local-verification.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n")
print(json.dumps({k: v for k, v in summary.items() if k != "cohorts"}, ensure_ascii=False))
for row in results:
    print(json.dumps({k: row[k] for k in ["scenario", "seed", "all_checks_passed", "raw_sha256", "observations", "final_hash", "final_state"]}, ensure_ascii=False))
