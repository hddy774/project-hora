"""Verify saved metadata and, optionally, original ZIPs without running a world."""

import argparse
import hashlib
import json
from pathlib import Path
import zipfile


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def digest(data):
    return hashlib.sha256(data).hexdigest()


def non_timing(points):
    return [
        {key: value for key, value in point.items()
         if key not in {"simulationMs", "caseElapsedMs"}}
        for point in points
    ]


def verify_originals(root, index, summary, archive_dir):
    baseline = read_json(root.parent / "growth-sensitivity/inputs/baseline.json")
    zip_count = 0
    report_count = 0
    for artifact in index["artifacts"]:
        aliases = {
            13012967859: "prepare.zip",
            13013860275: "inputs.zip",
            13020234556: "seed-98.zip",
            13023894081: "seed-42.zip",
            13024223579: "seed-2026.zip",
            13024674339: "collection.zip",
        }
        candidates = [archive_dir / artifact["planned_asset_name"],
                      archive_dir / aliases[artifact["id"]]]
        path = next((candidate for candidate in candidates if candidate.is_file()), None)
        assert path is not None, artifact["name"]
        data = path.read_bytes()
        assert len(data) == artifact["bytes"]
        assert digest(data) == artifact["sha256"]
        zip_count += 1
        with zipfile.ZipFile(path) as archive:
            assert archive.testzip() is None
            if "-seed-" not in artifact["name"]:
                continue
            raw = archive.read("out/sensitivity.json")
            report = json.loads(raw)
            seed = report["reports"][0]["seed"]
            expected = next(row for row in index["rawReports"] if row["seed"] == seed)
            assert digest(raw) == expected["sha256"]
            assert report["sourceCommit"] == index["measurementSourceCommit"]
            assert report["productSourceFingerprint"] == index["productSourceFingerprint"]
            assert report["planSha256"] == index["planSha256"]
            assert report["baselineReportSha256"] == index["baselineSha256"]
            assert report["complete"] is False
            for original in report["reports"]:
                row = next(item for item in summary["counterfactualReports"]
                           if item["seed"] == seed and item["lawValue"] == original["lawValue"])
                assert row["rawStatus"] == original["status"]
                assert row["progressMonth"] == original["progressMonth"]
                assert row["sampledThroughMonth"] == original["observedThroughMonth"]
                assert row["sampleCount"] == len(original["timeSeries"])
                assert row["individuals"] == original["descendantOutcomes"]
                assert row["generationTimes"] == original["generationTimes"]
                assert row["milestonesAtLastSample"] == original["milestoneOutcomes"]
                for key, value in row["lastSample"].items():
                    assert value == original["timeSeries"][-1][key]
                for field in ["simulationMs", "caseElapsedMs", "occupiedPersonMonthsByAction",
                              "inventoryReservedUnitMonths", "completedJobsByRecipe"]:
                    assert row[field] == original[field]
                for field, target in [("entryMonth", "entryRecords"),
                                      ("foundationMonth", "foundationRecords"),
                                      ("firstCoreMonth", "coreRecords")]:
                    assert row[target] == sum(person[field] is not None
                                              for person in original["descendantOutcomes"])
                base = next(item for item in baseline["reports"]
                            if item["scenario"] == "a" and item["seed"] == seed)
                prefix = [point for point in base["timeSeries"]
                          if point["month"] <= original["observedThroughMonth"]]
                assert row["nonTimingSamplesEqualDefaultPrefix"] == (
                    non_timing(prefix) == non_timing(original["timeSeries"]))
            report_count += 1
    assert zip_count == 6 and report_count == 3
    return zip_count


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archives", type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    index = read_json(root / "evidence-index.json")
    summary = read_json(root / "summary.json")
    assert digest((root / "summary.json").read_bytes()) == index["summarySha256"]
    for record in index["retainedOriginalMetadata"]:
        path = (root / record["path"]).resolve()
        assert path.is_relative_to(root)
        data = path.read_bytes()
        assert len(data) == record["bytes"] and digest(data) == record["sha256"]
    assert index["runOutcome"] == summary["workflowOutcome"] == "failure"
    assert summary["wholePlanAccepted"] is False
    assert summary["plannedCases"] == 6 and summary["completedCases"] == 4
    rows = summary["counterfactualReports"]
    assert {(row["lawValue"], row["seed"]) for row in rows} == {
        (value, seed) for value in [900, 1500] for seed in [42, 98, 2026]
    }
    assert sum(row["rawStatus"] == "completed" for row in rows) == 4
    for row in rows:
        if row["rawStatus"] == "completed":
            assert row["progressMonth"] == row["sampledThroughMonth"] == 6000
            assert row["sampleCount"] == 51
        else:
            assert row["rawStatus"] == "running"
            assert row["progressMonth"] < 6000
    exits = {seed: read_json(root / f"metadata/seed-{seed}/process-exit.json")
             for seed in [42, 98, 2026]}
    assert exits[98]["process_exit"] == 0
    assert exits[42]["process_exit"] == exits[2026]["process_exit"] == 124
    assert all(result["raw_sensitivity_report_modified_by_workflow"] is False
               for result in exits.values())
    assert index["ledgerOrDefaultChanged"] is False
    checked_zips = verify_originals(root, index, summary, args.archives) if args.archives else 0
    print(json.dumps({
        "retainedMetadata": "PASS", "originalZipsChecked": checked_zips,
        "completeObservations": 4, "partialObservations": 2,
        "wholePlanAccepted": False, "newMeasurementsExecuted": 0,
        "durablePublicationVerifiedByThisCommand": False,
        "scope": "Read-only integrity and descriptive correspondence; no balance/final-source acceptance."
    }, ensure_ascii=False))


if __name__ == "__main__":
    main()
