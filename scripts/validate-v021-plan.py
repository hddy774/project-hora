#!/usr/bin/env python3
"""Validate the v0.2.1 planning mirror. This does not run game tests."""
import argparse
import copy
import hashlib
import html
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
NEW_IDS = {"V021-R09", "V021-Z04", "V021-T07", "V021-H10"}
TASK_RE = re.compile(r"V021-[A-Z][0-9]{2}\Z")


def check(condition, message):
    if not condition:
        raise ValueError(message)


def dag(nodes, label):
    visiting, done, depth = set(), set(), {}

    def visit(node):
        check(node in nodes, f"{label}: unknown dependency {node}")
        check(node not in visiting, f"{label}: cycle at {node}")
        if node in done:
            return depth[node]
        visiting.add(node)
        depth[node] = 1 + max((visit(d) for d in nodes[node]), default=0)
        visiting.remove(node)
        done.add(node)
        return depth[node]

    for node in nodes:
        visit(node)
    return depth


def table_acceptance(task):
    text = task["acceptance"]
    if task["kind"] == "feature":
        text += " / 새 상태의 저장 왕복·INV 등록(T07)"
    return text + " / INV: " + ", ".join(task["inv_ids"])


def canonical_digest(data):
    payload = {key: data[key] for key in
               ("tasks", "questions", "milestones", "invariants", "v020_mapping")}
    raw = json.dumps(payload, ensure_ascii=False, sort_keys=True,
                     separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(raw).hexdigest()


def validate(data, root=None, invariants_path=None, notion_path=None):
    tasks = data["tasks"]
    check(len(tasks) == 111, "expected 111 tasks")
    ids = [task["id"] for task in tasks]
    check(len(ids) == len(set(ids)), "duplicate task ID")
    check(all(TASK_RE.fullmatch(i) for i in ids), "invalid task ID")
    by_id = {task["id"]: task for task in tasks}
    original = data["required_original_ids"]
    check(len(original) == len(set(original)) == 107, "original 107 IDs invalid")
    check(set(original) <= set(ids), "original task ID lost")
    check(set(ids) - set(original) == NEW_IDS, "new task ID mismatch")
    check(set(data["required_new_ids"]) == NEW_IDS, "required new ID mismatch")
    nodes = {task["id"]: task["deps"] for task in tasks}
    check(all(len(v) == len(set(v)) for v in nodes.values()), "duplicate dependency")
    depth = dag(nodes, "tasks")
    closure = set()

    def cover(node):
        if node not in closure:
            closure.add(node)
            for dep in nodes[node]:
                cover(dep)

    cover("V021-T06")
    check(closure == set(ids), "tasks disconnected from T06: " +
          ",".join(sorted(set(ids) - closure)))

    mappings = data["v020_mapping"]
    check(len(mappings) == 55, "expected 55 V020 mappings")
    check(len({m["id"] for m in mappings}) == 55, "duplicate V020 ID")
    check(all(re.fullmatch(r"V020-[A-Z][0-9]{2}", m["id"])
              and m["targets"] and set(m["targets"]) <= set(ids)
              for m in mappings), "invalid or missing V020 target")

    questions = data["questions"]
    qids = [q["id"] for q in questions]
    check(len(qids) == len(set(qids)), "duplicate Q ID")
    check(all(re.fullmatch(r"Q-[0-9]{2}", q) for q in qids), "invalid Q ID")
    qby = {q["id"]: q for q in questions}
    for q in questions:
        check(set(q["blocks"]) <= set(ids), "Q references unknown task")
        check(q["status"] in {"open", "optional", "resolved"}, "invalid Q status")
        if q["status"] != "open":
            check(not q["blocks"], "optional/resolved Q blocks base tasks")
    check(qby["Q-16"]["status"] == "optional" and not qby["Q-16"]["blocks"],
          "strict-play option blocks base requirements")
    check(qby["Q-17"]["status"] == "resolved", "Q-17 known instruction reopened")

    invariants = data["invariants"]
    inv_ids = [inv["id"] for inv in invariants]
    check(len(inv_ids) == len(set(inv_ids)) and inv_ids, "duplicate/missing INV")
    names = set()
    for inv in invariants:
        check(re.fullmatch(r"INV-[0-9]{3}", inv["id"]), "invalid INV ID")
        check(set(inv["tasks"]) <= set(ids), "INV references unknown task")
        check(inv["contract"] and inv["proposed_tests"], "empty INV contract/tests")
        check(inv["test_status"] == "not_implemented",
              "domain tests must not be reported as implemented")
        names.update(inv["proposed_tests"])

    milestones = {m["id"]: m for m in data["milestones"]}
    check(set(milestones) == {"M1", "M2", "M3", "M4", "M5"}, "milestone mismatch")
    for task in tasks:
        check(task["milestone"] in milestones, "unknown task milestone")
        check(task["acceptance"] and task["minimal_scope"] and task["later_scope"],
              "missing acceptance or stage scope")
        check(set(task["inv_ids"]) <= set(inv_ids) and task["inv_ids"],
              "unknown/missing task INV")
        check(set(task["proposed_tests"]) <= names and task["proposed_tests"],
              "unknown/missing proposed test reference")
        expected = {q["id"] for q in questions
                    if q["status"] == "open" and task["id"] in q["blocks"]}
        check(set(task["blocked_by_q"]) == expected, "Q/task gate mismatch")
        check(task["status"] in {"planned", "in_progress", "blocked", "validated", "done"},
              "invalid task status")
        if task["kind"] == "feature":
            check(task["early_harness"] == "V021-T07" and task["common_acceptance"],
                  "feature missing T07/round-trip requirement")
        if task["status"] == "done":
            check(not expected and task["evidence"], "done task lacks decision/evidence")
            check(all(by_id[d]["status"] == "done" for d in task["deps"]),
                  "done task has unfinished full dependency")

    slices = [part for m in milestones.values() for part in m["slices"]]
    slice_ids = [part["id"] for part in slices]
    check(len(slice_ids) == len(set(slice_ids)), "duplicate stage output")
    stage_nodes = {part["id"]: part["deps"] for part in slices}
    dag(stage_nodes, "stage outputs")
    for part in slices:
        check(part["task_id"] in by_id, "stage references unknown task")
        check(part["completes_task"] is False, "partial stage completes full task")
        check(set(part["policy_refs"]) <= set(qids), "stage references unknown Q")
        if part["status"] in {"validated", "done"}:
            check(part["evidence"], "stage lacks execution evidence")
            check(all(qby[q]["status"] != "open" for q in part["policy_refs"]),
                  "stage bypasses open decision")

    if root is not None:
        review = root / data["source"]["review_source_path"]
        check(review.is_file(), "received review source missing")
        actual = hashlib.sha256(review.read_bytes()).hexdigest()
        check(actual == data["source"]["review_source_sha256"], "review source changed")
    if invariants_path is not None:
        text = invariants_path.read_text(encoding="utf-8")
        found = set(re.findall(r"^## (INV-[0-9]{3})", text, re.M))
        check(found == set(inv_ids), "Markdown/JSON INV mismatch")
        check(all(name in text for name in names), "Markdown missing proposed test")
    if notion_path is not None:
        text = notion_path.read_text(encoding="utf-8")
        text = re.sub(r"<code>(.*?)</code>", r"\1", text)
        text = html.unescape(text).replace("\\~", "~")
        rows = {}
        for line in text.splitlines():
            if line.startswith("| V021-"):
                cols = [x.strip() for x in line.split("|")[1:-1]]
                check(len(cols) == 6, "Notion task table must have six columns")
                check(cols[0] not in rows, "duplicate Notion task row")
                rows[cols[0]] = cols
        check(set(rows) == set(ids), "Notion/JSON task ID mismatch")
        for task in tasks:
            expected = [task["id"], task["title"],
                        "·".join(d.removeprefix("V021-") for d in task["deps"]) or "—",
                        table_acceptance(task), task["milestone"],
                        "·".join(task["blocked_by_q"]) or "—"]
            normalized = [s.replace("\\~", "~") for s in expected]
            check(rows[task["id"]] == normalized,
                  "Notion/JSON projection mismatch: " + task["id"])

    return {"tasks": len(tasks), "v020_mappings": len(mappings),
            "invariants": len(invariants), "m1_partial_outputs": len(slices),
            "max_task_depth": max(depth.values()), "t06_depth": depth["V021-T06"],
            "n01_depth": depth["V021-N01"],
            "direct_decision_gated_tasks": sum(bool(t["blocked_by_q"]) for t in tasks),
            "projection_sha256": canonical_digest(data),
            "game_tests_executed": False}


def self_test(data):
    def change_task(d, task_id):
        return next(t for t in d["tasks"] if t["id"] == task_id)

    cases = [
        ("unknown dependency", lambda d: change_task(d, "V021-N01")["deps"].append("V021-Z99")),
        ("cycle", lambda d: change_task(d, "V021-N01")["deps"].append("V021-N01")),
        ("duplicate task", lambda d: d["tasks"].append(copy.deepcopy(d["tasks"][0]))),
        ("missing source mapping", lambda d: d["v020_mapping"].pop()),
        ("bad mapping", lambda d: d["v020_mapping"][0]["targets"].append("V021-Z99")),
        ("disconnected T07", lambda d: [
            t.update(deps=[x for x in t["deps"] if x != "V021-T07"])
            for t in d["tasks"]]),
        ("unknown invariant", lambda d: d["tasks"][0]["inv_ids"].append("INV-999")),
        ("decision bypass", lambda d: change_task(d, "V021-P02")["blocked_by_q"].clear()),
        ("partial completion", lambda d: d["milestones"][0]["slices"][0].update(completes_task=True)),
        ("optional gate", lambda d: next(q for q in d["questions"]
                                      if q["id"] == "Q-16")["blocks"].append("V021-N03")),
        ("domain pass fabrication", lambda d: d["invariants"][0].update(test_status="passed")),
    ]
    for name, mutate in cases:
        altered = copy.deepcopy(data)
        mutate(altered)
        try:
            validate(altered)
        except ValueError:
            continue
        raise ValueError("negative self-test did not reject: " + name)
    return len(cases)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ledger", type=Path, default=ROOT / "docs/plans/v021-tasks.json")
    parser.add_argument("--invariants", type=Path, default=ROOT / "docs/plans/v021-invariants.md")
    parser.add_argument("--notion-snapshot", type=Path)
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--write-report", type=Path)
    args = parser.parse_args()
    try:
        data = json.loads(args.ledger.read_text(encoding="utf-8"))
        result = validate(data, ROOT, args.invariants, args.notion_snapshot)
        if args.self_test:
            result["negative_self_tests_passed"] = self_test(data)
        report = json.dumps(result, ensure_ascii=False, indent=2)
        print(report)
        if args.write_report:
            args.write_report.parent.mkdir(parents=True, exist_ok=True)
            args.write_report.write_text(report + "\n", encoding="utf-8")
        return 0
    except (OSError, KeyError, TypeError, ValueError, RecursionError) as error:
        print("Plan validation failed: " + str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
